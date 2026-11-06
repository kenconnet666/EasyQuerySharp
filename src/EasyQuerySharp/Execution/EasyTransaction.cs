using System.Data;
using System.Data.Common;

namespace EasyQuerySharp.Execution;

/// <summary>
/// 环境事务：<see cref="EasyDb.BeginTransaction"/> 开启后，同一异步流程里该 <see cref="EasyDb"/> 的所有执行都走这个事务的连接，
/// 不必把事务对象到处传。连接在第一次执行时才打开。释放时若没有提交就回滚。
/// 已经在事务里时不能再开（与 Java 的 repeat begin transaction 一致）。
/// 分库时每个用到的数据源各开一个连接与事务（用到哪个才开哪个），提交时依次提交：这不是分布式事务，
/// 中途提交失败时已经提交的数据源不会回滚（与 Java 一致）。
/// </summary>
/// <example><code>
/// await using var tx = db.BeginTransaction();
/// await db.Insert(order).ExecuteAsync();
/// await db.Update&lt;Stock&gt;().Set(s => new Stock { Count = s.Count - 1 }).Where(s => s.Id == id).ExecuteAsync();
/// await tx.CommitAsync();
/// </code></example>
public sealed class EasyTransaction : IAsyncDisposable, IDisposable
{
    private readonly EasyDb _db;
    private readonly IsolationLevel _isolation;
    // 用到的数据源，按首次使用的顺序；并行执行的单元同时取同一个数据源时只开一次
    private readonly List<Branch> _branches = [];
    private readonly Lock _lock = new();
    private bool _completed;

    internal EasyTransaction(EasyDb db, IsolationLevel isolation)
    {
        _db = db;
        _isolation = isolation;
        db.AmbientTransaction.Enter(this);
        Writes = WriteLog.Create(db);
    }

    /// <summary>事务里成功执行的写入：提交成功后通知写入监听器，回滚时丢弃；没有写入监听器时为 null。</summary>
    internal WriteLog? Writes { get; }

    /// <summary>当前异步流程里属于 <paramref name="db"/> 的事务；没有时为 null。</summary>
    internal static EasyTransaction? Current(EasyDb db) => db.AmbientTransaction.Current;

    /// <summary>事务是否已经结束（提交或回滚）。</summary>
    public bool IsCompleted => _completed;

    /// <summary>取数据源 <paramref name="dataSource"/>（null 是默认数据源）上的连接与事务对象，首次用到时打开连接并开始事务。</summary>
    internal async ValueTask<(DbConnection Connection, DbTransaction Transaction)> EnsureStartedAsync(string? dataSource, CancellationToken ct)
    {
        var name = dataSource ?? _db.Options.DefaultDataSourceName;
        Task<(DbConnection Connection, DbTransaction Transaction)> started;
        lock (_lock)
        {
            if (_branches.Find(b => b.DataSource == name) is { } branch) started = branch.Started;
            else
            {
                started = StartAsync(name, ct);
                _branches.Add(new Branch(name, started));
            }
        }
        return await started.ConfigureAwait(false);
    }

    private async Task<(DbConnection Connection, DbTransaction Transaction)> StartAsync(string dataSource, CancellationToken ct)
    {
        if (!_db.Options.Dialect.SupportsTransactions)
            throw new NotSupportedException($"{_db.Options.Dialect.Name} 没有事务：语句各自生效，不能在 BeginTransaction 里执行");
        var connection = await _db.OpenConnectionAsync(dataSource, ct).ConfigureAwait(false);
        try
        {
            return (connection, await connection.BeginTransactionAsync(_isolation, ct).ConfigureAwait(false));
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 提交（分库时各数据源依次提交）。没有执行过任何语句时什么也不做。全部提交、事务结束之后通知写入监听器（见 <see cref="IWriteListener"/>）：
    /// 监听器失败时抛出 <see cref="EasyQueryException"/>，这时事务已经提交。
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        // 提交成功的分支随即释放、移出：中途失败时，释放事务只回滚还没提交的
        while (_branches.Count > 0)
        {
            var (connection, transaction) = await _branches[0].Started.ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _branches.RemoveAt(0);
            await transaction.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        End();
        if (Writes is not null) await Writes.NotifyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>回滚（分库时每个用到的数据源都回滚）。</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed) return;
        try
        {
            foreach (var branch in _branches)
            {
                if (await branch.OpenedAsync().ConfigureAwait(false) is not var (connection, transaction)) continue;
                try
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _branches.Clear();
            End();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_completed) await RollbackAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_completed) return;
        try
        {
            foreach (var branch in _branches)
            {
                if (!branch.Started.IsCompletedSuccessfully) continue;
                var (connection, transaction) = branch.Started.Result;
                try
                {
                    transaction.Rollback();
                }
                finally
                {
                    transaction.Dispose();
                    connection.Dispose();
                }
            }
        }
        finally
        {
            _branches.Clear();
            End();
        }
    }

    private void End()
    {
        _completed = true;
        _db.AmbientTransaction.Exit(this);
    }

    // 一个数据源上的连接与事务；打开失败的分支没有要回滚的
    private sealed record Branch(string DataSource, Task<(DbConnection Connection, DbTransaction Transaction)> Started)
    {
        public async Task<(DbConnection Connection, DbTransaction Transaction)?> OpenedAsync()
        {
            try
            {
                return await Started.ConfigureAwait(false);
            }
            catch (Exception) when (!Started.IsCompletedSuccessfully)
            {
                return null;
            }
        }
    }
}

/// <summary>
/// 每个 <see cref="EasyDb"/> 一个：当前异步流程里的事务。AsyncLocal 里放可变的 holder，结束时清空 holder 而不是给 AsyncLocal 赋 null——
/// 提交、回滚都是 async 方法，在里面给 AsyncLocal 赋值不会传回调用方，已结束的事务会一直挂在调用方的流程上。
/// </summary>
internal sealed class AmbientTransaction
{
    private sealed class Holder
    {
        public EasyTransaction? Transaction;
    }

    private readonly AsyncLocal<Holder?> _current = new();

    public EasyTransaction? Current => _current.Value?.Transaction;

    public void Enter(EasyTransaction transaction)
    {
        if (Current is not null)
            throw new InvalidOperationException("当前异步流程已经在这个库的事务里，不能再开事务（不支持嵌套）：在外层事务里直接执行，或先提交 / 回滚外层事务");
        _current.Value = new Holder { Transaction = transaction };
    }

    public void Exit(EasyTransaction transaction)
    {
        if (_current.Value is { } holder && ReferenceEquals(holder.Transaction, transaction)) holder.Transaction = null;
    }
}
