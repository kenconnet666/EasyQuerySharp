using System.Data.Common;
using EasyQuerySharp.TestKit.Databases;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.Fork.HanaFixture))]

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// fork 的 HANA 实库用例共用：每次测试运行重建 schema <c>EQS_FORK</c>（HANA 一个租户里按 schema 隔离），表由各用例自己建。
/// 没有配置 HANA 或没有装 HANA 客户端时用到 <see cref="Db"/> 的测试被跳过。
/// </summary>
public sealed class HanaFixture : IAsyncLifetime
{
    /// <summary>本固件使用的 schema。</summary>
    public const string Schema = "EQS_FORK";

    private EasyDb? _db;
    private string? _connectionString;
    private string? _skipReason;

    /// <summary>连到 <see cref="Schema"/> 的 EasyDb，带 <see cref="SqlCapture"/> 监听；没有配置 HANA 时跳过当前测试。</summary>
    public EasyDb Db
    {
        get
        {
            if (_db is null) Assert.Skip(_skipReason ?? "HANA 固件没有初始化");
            return _db;
        }
    }

    /// <summary>在 <see cref="Schema"/> 上直接执行 EasyDb 写不出的 DDL（如全文索引）。</summary>
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        _ = Db;
        await using var connection = TestDatabases.Hana.Factory.CreateConnection()!;
        connection.ConnectionString = _connectionString;
        await connection.OpenAsync(cancellationToken);
        await Execute(connection, sql, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var hana = TestDatabases.Hana;
        if (hana.TryConnectionString() is null)
        {
            _skipReason = hana.SkipReason;
            return;
        }
        await using (var server = await hana.OpenAsync(CancellationToken.None))
        {
            // HANA 没有 DROP SCHEMA IF EXISTS：不存在（错误 362）时忽略
            await Execute(server, $"DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 362 BEGIN END; EXEC 'DROP SCHEMA {Schema} CASCADE'; END", CancellationToken.None);
            await Execute(server, $"CREATE SCHEMA {Schema}", CancellationToken.None);
        }
        _connectionString = hana.TryConnectionString(Schema)!;
        _db = new EasyDb(o => o.WithJavaNulls().UseHana(hana.Factory.CreateDataSource(_connectionString)).AddListener(SqlCapture.Instance));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task Execute(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
