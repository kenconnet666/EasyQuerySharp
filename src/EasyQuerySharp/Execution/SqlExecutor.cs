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
            (value, var rows) = await ScalarAsync(command, statement, ct).ConfigureAwait(false);
            return rows;
        }).ConfigureAwait(false);
        return value;
    }

    /// <summary>
    /// 执行一批 SQL 相同、参数不同的写入（对应 Java 的 executeBatch）：驱动支持 <see cref="DbBatch"/> 时一次发出，否则在这个连接上逐条执行。
    /// 不论哪种方式，监听器都只收到一次执行，<see cref="SqlExecutionEvent.Statements"/> 里是这一批的全部语句；
    /// 不在事务里时整批在一个本地事务里执行（见 <see cref="InOwnTransactionAsync{T}"/>）。只有一条时就是普通执行。
    /// </summary>
    public async Task<BatchRows> ExecuteBatchAsync(IReadOnlyList<SqlStatement> statements, CancellationToken ct)
    {
        if (statements.Count > 1) return await InOwnTransactionAsync(s => s.ExecuteManyAsync(statements, ct), ct).ConfigureAwait(false);
        var single = await ExecuteAsync(statements[0], ct).ConfigureAwait(false);
        return new BatchRows(single, [single], 1);
    }

    /// <summary>
    /// 插入并读回每条生成的键，返回受影响的总行数与按语句顺序的键。<paramref name="keyQuery"/> 为 null 时语句自己返回键
    /// （RETURNING / OUTPUT，或 Oracle 的输出参数），否则每条插入后执行它取键（LAST_INSERT_ID() 这类连接级的函数）。
    /// 多条时同 <see cref="ExecuteBatchAsync"/>，取键的查询不单独通知监听器。
    /// </summary>
    public async Task<(long Rows, object?[] Keys)> InsertBatchAsync(IReadOnlyList<SqlStatement> statements, string? keyQuery, CancellationToken ct)
    {
        var key = keyQuery is null ? null : new SqlStatement(keyQuery, keyQuery, []);
        if (statements.Count > 1) return await InOwnTransactionAsync(s => s.InsertManyAsync(statements, key, ct), ct).ConfigureAwait(false);
        if (key is null) return (1, [await ScalarAsync(statements[0], ct).ConfigureAwait(false)]);
        var single = await ExecuteAsync(statements[0], ct).ConfigureAwait(false);
        return (single, [await ScalarAsync(key, ct).ConfigureAwait(false)]);
    }

    // 不在事务里时，一批在自己的本地事务里执行：各库一致地整批成功或整批不生效（PostgreSQL 的 DbBatch 本来如此），
    // 也免得逐条自动提交（MySQL 每次提交都刷盘，1000 条慢几十倍）
    private async Task<T> InOwnTransactionAsync<T>(Func<SqlSession, Task<T>> work, CancellationToken ct)
    {
        if (transaction is not null) return await work(this).ConfigureAwait(false);
        await using var local = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var result = await work(new SqlSession(db, connection, local)).ConfigureAwait(false);
        await local.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }

    private async Task<BatchRows> ExecuteManyAsync(IReadOnlyList<SqlStatement> statements, CancellationToken ct)
    {
        BatchRows result = null!;
        await ObserveAsync(statements, async () =>
        {
            if (!CanBatch(statements))
            {
                var rows = new long[statements.Count];
                for (var i = 0; i < rows.Length; i++)
                {
                    await using var command = CreateCommand(statements[i]);
                    rows[i] = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                result = new BatchRows(rows.Sum(), rows, rows.Length);
                return result.Total;
            }
            await using var batch = CreateBatch(statements);
            long total = await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            var perCommand = batch.BatchCommands.Select(c => (long)c.RecordsAffected).ToArray();
            // MySqlConnector 不逐条报告行数（都是 0），只有总数可信
            result = new BatchRows(total, perCommand.Sum() == total ? perCommand : null, statements.Count);
            return total;
        }).ConfigureAwait(false);
        return result;
    }

    private async Task<(long Rows, object?[] Keys)> InsertManyAsync(IReadOnlyList<SqlStatement> statements, SqlStatement? key, CancellationToken ct)
    {
        var keys = new object?[statements.Count];
        long rows = 0;
        await ObserveAsync(statements, async () =>
        {
            if (!CanBatch(statements))
            {
                for (var i = 0; i < keys.Length; i++)
                {
                    await using var command = CreateCommand(statements[i]);
                    if (key is null)
                    {
                        (keys[i], var affected) = await ScalarAsync(command, statements[i], ct).ConfigureAwait(false);
                        rows += affected;
                        continue;
                    }
                    rows += await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    await using var query = CreateCommand(key);
                    (keys[i], _) = await ScalarAsync(query, key, ct).ConfigureAwait(false);
                }
                return rows;
            }
            // 取键的查询紧跟在各自的插入后面；插入本身不产生结果集，读到的第 i 个结果集就是第 i 条的键
            await using var batch = CreateBatch(key is null ? statements : [.. statements.SelectMany(s => new[] { s, key })]);
            await using var reader = await batch.ExecuteReaderAsync(ct).ConfigureAwait(false);
            for (var i = 0; i < keys.Length; i++)
            {
                if (i > 0 && !await reader.NextResultAsync(ct).ConfigureAwait(false))
                    throw new InvalidOperationException($"批量插入 {keys.Length} 条只读回了 {i} 个生成的键");
                keys[i] = await reader.ReadAsync(ct).ConfigureAwait(false) ? DbValue(reader.GetValue(0)) : null;
            }
            await reader.CloseAsync().ConfigureAwait(false);
            return rows = reader.RecordsAffected;
        }).ConfigureAwait(false);
        return (rows, keys);
    }

    private Task RunAsync(SqlStatement statement, Func<DbCommand, Task<long>> body) =>
        ObserveAsync([statement], async () =>
        {
            await using var command = CreateCommand(statement);
            return await body(command).ConfigureAwait(false);
        });

    // 一次执行的外壳：通知监听器、记 Activity 与耗时，失败时包成 EasyQueryException（批量时语句取这一批的第一条）
    private async Task ObserveAsync(IReadOnlyList<SqlStatement> statements, Func<Task<long>> body)
    {
        var statement = statements[0];
        EnsureTransaction(statement);
        var e = new SqlExecutionEvent(statements, db);
        foreach (var l in db.Options.Listeners) l.OnExecuting(e);

        using var activity = SqlExecutor.ActivitySource.StartActivity("db.query", ActivityKind.Client);
        activity?.SetTag("db.system", db.Options.Dialect.Name);
        activity?.SetTag("db.query.text", statement.Text);
        if (statements.Count > 1) activity?.SetTag("db.operation.batch.size", statements.Count);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            e.Rows = await body().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ResultSizeLimitException)
        {
            e.Exception = ex;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // 取消原样抛出，调用方照常按 OperationCanceledException 处理；其余失败包上语句
            if (ex is OperationCanceledException) throw;
            var batch = statements.Count > 1 ? $"（批量 {statements.Count} 条）" : "";
            throw new EasyQueryException($"执行 SQL 失败{batch}：{ex.Message}{Environment.NewLine}{statement.Text}", statement, ex);
        }
        finally
        {
            e.Elapsed = stopwatch.Elapsed;
            foreach (var l in db.Options.Listeners) l.OnExecuted(e);
        }
    }

    // 执行并读第一行第一列；带输出参数的语句（Oracle 的 RETURNING … INTO）执行后读输出参数。返回值与受影响的行数
    private static async Task<(object? Value, long Rows)> ScalarAsync(DbCommand command, SqlStatement statement, CancellationToken ct)
    {
        var output = statement.Parameters.ToList().FindIndex(p => p.IsOutput);
        if (output >= 0)
        {
            var rows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return (DbValue(command.Parameters[output].Value), rows);
        }
        var value = DbValue(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
        return (value, value is null ? 0 : 1);
    }

    private static object? DbValue(object? value) => value is DBNull ? null : value;

    // 输出参数要逐条读回，走不了 DbBatch
    private bool CanBatch(IReadOnlyList<SqlStatement> statements) =>
        connection.CanCreateBatch && statements.All(s => s.Parameters.All(p => !p.IsOutput));

    /// <summary>流式查询：读到一行返回一行，枚举结束（或中途停止）时释放读取器并通知监听器。</summary>
    public async IAsyncEnumerable<T> StreamAsync<T>(SqlStatement statement, Func<DbDataReader, T> shaper, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        EnsureTransaction(statement);
        var e = new SqlExecutionEvent([statement], db);
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
            if (ex is OperationCanceledException) throw;
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

    // 输出参数的驱动类型：只用于读回生成的键
    private static System.Data.DbType OutputDbType(Type clrType) => (Nullable.GetUnderlyingType(clrType) ?? clrType) switch
    {
        var t when t == typeof(int) => System.Data.DbType.Int32,
        var t when t == typeof(long) => System.Data.DbType.Int64,
        var t when t == typeof(decimal) => System.Data.DbType.Decimal,
        var t when t == typeof(Guid) => System.Data.DbType.Guid,
        _ => System.Data.DbType.String,
    };

    private DbCommand CreateCommand(SqlStatement statement)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement.CommandText;
        command.Transaction = transaction;
        Bind(statement, command.Parameters, command.CreateParameter);
        return command;
    }

    private DbBatch CreateBatch(IReadOnlyList<SqlStatement> statements)
    {
        var batch = connection.CreateBatch();
        batch.Transaction = transaction;
        foreach (var statement in statements)
        {
            var command = batch.CreateBatchCommand();
            command.CommandText = statement.CommandText;
            Bind(statement, command.Parameters, command.CreateParameter);
            batch.BatchCommands.Add(command);
        }
        return batch;
    }

    private void Bind(SqlStatement statement, DbParameterCollection target, Func<DbParameter> create)
    {
        for (var i = 0; i < statement.Parameters.Count; i++)
        {
            var p = create();
            p.ParameterName = db.Options.Dialect.ParameterName(i);
            var parameter = statement.Parameters[i];
            if (parameter.IsOutput)
            {
                p.Direction = System.Data.ParameterDirection.Output;
                p.DbType = OutputDbType(parameter.ClrType);
            }
            else p.Value = parameter.Value ?? DBNull.Value;
            db.Options.Dialect.ConfigureParameter(p, parameter);
            target.Add(p);
        }
    }
}

/// <summary>一批写入的受影响行数：总数；驱动逐条报告了行数时还有每条的（MySqlConnector 只报告总数）。</summary>
internal sealed record BatchRows(long Total, IReadOnlyList<long>? PerStatement, int Count)
{
    /// <summary>第 <paramref name="index"/> 条写到了行。每条的行数未知时，只有整批每条都写到（总数不少于条数）才能确定。</summary>
    public bool Wrote(int index) => PerStatement is { } rows ? rows[index] > 0 : Total >= Count;
}
