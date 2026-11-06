using System.Data;
using System.Data.Common;

namespace EasyQuerySharp.Execution;

/// <summary>
/// 环境事务：<see cref="EasyDb.BeginTransaction"/> 开启后，同一异步流程里该 <see cref="EasyDb"/> 的所有执行都走这个事务的连接，
/// 不必把事务对象到处传。连接在第一次执行时才打开。释放时若没有提交就回滚。
/// 已经在事务里时不能再开（与 Java 的 repeat begin transaction 一致）。
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
    private DbConnection? _connection;
    private DbTransaction? _transaction;
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

    /// <summary>取事务的连接与事务对象（首次调用时打开连接并开始事务）。</summary>
    internal async ValueTask<(DbConnection Connection, DbTransaction Transaction)> EnsureStartedAsync(CancellationToken ct)
    {
        if (_connection is null)
        {
            _connection = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
            _transaction = await _connection.BeginTransactionAsync(_isolation, ct).ConfigureAwait(false);
        }
        return (_connection, _transaction!);
    }

    /// <summary>
    /// 提交。没有执行过任何语句时什么也不做。提交成功、事务结束之后通知写入监听器（见 <see cref="IWriteListener"/>）：
    /// 监听器失败时抛出 <see cref="EasyQueryException"/>，这时事务已经提交。
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        if (_transaction is not null) await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await EndAsync().ConfigureAwait(false);
        if (Writes is not null) await Writes.NotifyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>回滚。</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed) return;
        if (_transaction is not null) await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await EndAsync().ConfigureAwait(false);
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
            _transaction?.Rollback();
        }
        finally
        {
            End();
            _transaction?.Dispose();
            _connection?.Dispose();
        }
    }

    private async ValueTask EndAsync()
    {
        End();
        if (_transaction is not null) await _transaction.DisposeAsync().ConfigureAwait(false);
        if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private void End()
    {
        _completed = true;
        _db.AmbientTransaction.Exit(this);
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
