using System.Data;
using System.Data.Common;

namespace EasyQuerySharp.Execution;

/// <summary>
/// 环境事务：<see cref="EasyDb.BeginTransaction"/> 开启后，同一异步流程里该 <see cref="EasyDb"/> 的所有执行都走这个事务的连接，
/// 不必把事务对象到处传。连接在第一次执行时才打开。释放时若没有提交就回滚。
/// </summary>
/// <example><code>
/// await using var tx = db.BeginTransaction();
/// await db.Insert(order).ExecuteAsync();
/// await db.Update&lt;Stock&gt;().Set(s => new Stock { Count = s.Count - 1 }).Where(s => s.Id == id).ExecuteAsync();
/// await tx.CommitAsync();
/// </code></example>
public sealed class EasyTransaction : IAsyncDisposable, IDisposable
{
    private static readonly AsyncLocal<EasyTransaction?> Ambient = new();

    private readonly EasyDb _db;
    private readonly IsolationLevel _isolation;
    private readonly EasyTransaction? _outer;
    private DbConnection? _connection;
    private DbTransaction? _transaction;
    private bool _completed;

    internal EasyTransaction(EasyDb db, IsolationLevel isolation)
    {
        _db = db;
        _isolation = isolation;
        _outer = Ambient.Value;
        Ambient.Value = this;
    }

    /// <summary>当前异步流程里属于 <paramref name="db"/> 的事务；没有时为 null。</summary>
    internal static EasyTransaction? Current(EasyDb db)
    {
        for (var t = Ambient.Value; t is not null; t = t._outer)
            if (ReferenceEquals(t._db, db) && !t._completed) return t;
        return null;
    }

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

    /// <summary>提交。没有执行过任何语句时什么也不做。</summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        if (_transaction is not null) await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await EndAsync().ConfigureAwait(false);
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
        _transaction?.Rollback();
        EndAsync().AsTask().GetAwaiter().GetResult();
    }

    private async ValueTask EndAsync()
    {
        _completed = true;
        if (ReferenceEquals(Ambient.Value, this)) Ambient.Value = _outer;
        if (_transaction is not null) await _transaction.DisposeAsync().ConfigureAwait(false);
        if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
