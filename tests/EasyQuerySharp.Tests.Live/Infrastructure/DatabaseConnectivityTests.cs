using System.Data.Common;

namespace EasyQuerySharp.Tests.Live.Infrastructure;

/// <summary>
/// 测试基础设施自检：每个登记的数据库都能连上、建表、带参数读写、删表。
/// 这组测试先于 ORM 本身存在，用来确认本地容器与 CI 服务容器配置正确。
/// </summary>
public sealed class DatabaseConnectivityTests
{
    public static TheoryData<string> Databases => new(TestDatabases.All.Select(d => d.Key));

    [Theory]
    [MemberData(nameof(Databases))]
    public async Task RoundTripThroughRealDatabase(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var db = TestDatabases.Get(key);
        await using var connection = await db.OpenAsync(ct);
        TestContext.Current.TestOutputHelper?.WriteLine($"{db.DisplayName} 服务器版本：{connection.ServerVersion}");

        // 表名带随机后缀，多个 CI 任务或本地并行运行时互不干扰
        var table = $"eqs_probe_{Guid.NewGuid():N}"[..20];
        await ExecuteAsync(connection, $"CREATE TABLE {table} (id INT PRIMARY KEY, name VARCHAR(50))", ct);
        try
        {
            var p = db.ParameterPrefix;
            await ExecuteAsync(connection, $"INSERT INTO {table} (id, name) VALUES ({p}id, {p}name)", ct, ("id", 1), ("name", "easy-query"));

            await using var select = Command(connection, $"SELECT name FROM {table} WHERE id = {p}id", ("id", 1));
            Assert.Equal("easy-query", await select.ExecuteScalarAsync(ct));
        }
        finally
        {
            await ExecuteAsync(connection, $"DROP TABLE {table}", CancellationToken.None);
        }
    }

    [Fact]
    public void RequiredDatabasesAreRegistered()
    {
        // CI 里 EQS_REQUIRE_DBS 写错名字时，对应的数据库会被当成“未要求”而静默跳过；这里把错误暴露出来
        var unknown = TestDatabases.RequiredKeys.Where(k => TestDatabases.All.All(d => !d.Key.Equals(k, StringComparison.OrdinalIgnoreCase)));
        Assert.Empty(unknown);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, sql, parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static DbCommand Command(DbConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
