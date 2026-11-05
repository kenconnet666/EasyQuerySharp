using System.Data.Common;
using System.Diagnostics;

namespace EasyQuerySharp.Execution;

/// <summary>
/// 执行渲染好的语句：取连接（有环境事务时用事务的连接）、绑定参数、通知监听器、读结果。
/// 所有对数据库的访问都经过这里，日志、监控、测试捕获只需挂在 <see cref="IExecutionListener"/> 上。
/// </summary>
internal sealed class SqlExecutor(EasyDb db)
{
    private static readonly ActivitySource Activities = new("EasyQuerySharp");

    /// <summary>执行查询，逐行用 <paramref name="shaper"/> 读成结果。</summary>
    public Task<List<T>> QueryAsync<T>(SqlStatement statement, Func<DbDataReader, T> shaper, CancellationToken ct) =>
        SessionAsync(s => s.QueryAsync(statement, shaper, ct), ct);

    /// <summary>执行写入，返回受影响的行数。</summary>
    public Task<long> ExecuteAsync(SqlStatement statement, CancellationToken ct) =>
        SessionAsync(s => s.ExecuteAsync(statement, ct), ct);

    /// <summary>执行并返回第一行第一列。</summary>
    public Task<object?> ScalarAsync(SqlStatement statement, CancellationToken ct) =>
        SessionAsync(s => s.ScalarAsync(statement, ct), ct);

    /// <summary>流式查询：连接在枚举结束时释放（有环境事务时用事务的连接）。</summary>
    public async IAsyncEnumerable<T> StreamAsync<T>(SqlStatement statement, Func<DbDataReader, T> shaper, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        DbConnection? owned = null;
        DbConnection connection;
        DbTransaction? dbTransaction = null;
        if (EasyTransaction.Current(db) is { } transaction) (connection, dbTransaction) = await transaction.EnsureStartedAsync(ct).ConfigureAwait(false);
        else connection = owned = await db.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            await foreach (var row in new SqlSession(db, connection, dbTransaction).StreamAsync(statement, shaper, ct).ConfigureAwait(false))
                yield return row;
        }
        finally
        {
            if (owned is not null) await owned.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>在同一个连接上执行一组语句（插入后取自增键这类必须同连接的操作）。</summary>
    public async Task<T> SessionAsync<T>(Func<SqlSession, Task<T>> work, CancellationToken ct)
    {
        if (EasyTransaction.Current(db) is { } transaction)
        {
            var (connection, dbTransaction) = await transaction.EnsureStartedAsync(ct).ConfigureAwait(false);
            return await work(new SqlSession(db, connection, dbTransaction)).ConfigureAwait(false);
        }
        await using var owned = await db.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await work(new SqlSession(db, owned, null)).ConfigureAwait(false);
    }

    internal static ActivitySource ActivitySource => Activities;
}

/// <summary>绑定在一个连接上的执行会话；每条语句都通知监听器。</summary>
internal sealed class SqlSession(EasyDb db, DbConnection connection, DbTransaction? transaction)
{
    /// <summary>执行查询。</summary>
    public async Task<List<T>> QueryAsync<T>(SqlStatement statement, Func<DbDataReader, T> shaper, CancellationToken ct)
    {
        var result = new List<T>();
        await RunAsync(statement, async command =>
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) result.Add(shaper(reader));
            return result.Count;
        }).ConfigureAwait(false);
        return result;
    }

    /// <summary>执行查询，把整个结果集交给 <paramref name="read"/> 读取。</summary>
    public async Task<T> ReadAsync<T>(SqlStatement statement, Func<DbDataReader, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        T result = default!;
        await RunAsync(statement, async command =>
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            result = await read(reader, ct).ConfigureAwait(false);
            return reader.RecordsAffected;
        }).ConfigureAwait(false);
        return result;
    }

    /// <summary>执行写入，返回受影响的行数。</summary>
    public async Task<long> ExecuteAsync(SqlStatement statement, CancellationToken ct)
    {
        long rows = 0;
        await RunAsync(statement, async command => rows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false)).ConfigureAwait(false);
        return rows;
    }

    /// <summary>执行并返回第一行第一列；数据库 NULL 返回 null。</summary>
    public async Task<object?> ScalarAsync(SqlStatement statement, CancellationToken ct)
    {
        object? value = null;
        await RunAsync(statement, async command =>
        {
            value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return value is null ? 0 : 1;
        }).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    private async Task RunAsync(SqlStatement statement, Func<DbCommand, Task<long>> body)
    {
        EnsureTransaction(statement);
        var e = new SqlExecutionEvent(statement, db);
        foreach (var l in db.Options.Listeners) l.OnExecuting(e);

        using var activity = SqlExecutor.ActivitySource.StartActivity("db.query", ActivityKind.Client);
        activity?.SetTag("db.system", db.Options.Dialect.Name);
        activity?.SetTag("db.query.text", statement.Text);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var command = CreateCommand(statement);
            e.Rows = await body(command).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ResultSizeLimitException)
        {
            e.Exception = ex;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new EasyQueryException($"执行 SQL 失败：{ex.Message}{Environment.NewLine}{statement.Text}", statement, ex);
        }
        finally
        {
            e.Elapsed = stopwatch.Elapsed;
            foreach (var l in db.Options.Listeners) l.OnExecuted(e);
        }
    }

    /// <summary>流式查询：读到一行返回一行，枚举结束（或中途停止）时释放读取器并通知监听器。</summary>
    public async IAsyncEnumerable<T> StreamAsync<T>(SqlStatement statement, Func<DbDataReader, T> shaper, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        EnsureTransaction(statement);
        var e = new SqlExecutionEvent(statement, db);
        foreach (var l in db.Options.Listeners) l.OnExecuting(e);
        var stopwatch = Stopwatch.StartNew();
        DbCommand command;
        DbDataReader reader;
        try
        {
            command = CreateCommand(statement);
            reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            e.Exception = ex;
            e.Elapsed = stopwatch.Elapsed;
            foreach (var l in db.Options.Listeners) l.OnExecuted(e);
            throw new EasyQueryException($"执行 SQL 失败：{ex.Message}{Environment.NewLine}{statement.Text}", statement, ex);
        }
        long rows = 0;
        try
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                rows++;
                yield return shaper(reader);
            }
        }
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
            await command.DisposeAsync().ConfigureAwait(false);
            e.Rows = rows;
            e.Elapsed = stopwatch.Elapsed;
            foreach (var l in db.Options.Listeners) l.OnExecuted(e);
        }
    }

    // 行锁离开事务就随语句结束立即释放，等于没锁；与 Java 一样执行前拒绝
    private void EnsureTransaction(SqlStatement statement)
    {
        if (statement.RequiresTransaction && transaction is null)
            throw new InvalidOperationException($"FOR UPDATE 查询需要在事务里执行，请先调用 db.BeginTransaction()：{statement.Text}");
    }

    private DbCommand CreateCommand(SqlStatement statement)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement.CommandText;
        command.Transaction = transaction;
        for (var i = 0; i < statement.Parameters.Count; i++)
        {
            var p = command.CreateParameter();
            p.ParameterName = db.Options.Dialect.ParameterName(i);
            p.Value = statement.Parameters[i].Value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
        return command;
    }
}
