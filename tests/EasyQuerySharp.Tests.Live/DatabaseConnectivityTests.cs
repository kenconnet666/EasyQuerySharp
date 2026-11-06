using System.Data.Common;
using EasyQuerySharp.TestKit.Databases;

namespace EasyQuerySharp.Tests.Live;

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
        // ClickHouse.Driver 不再提供 ServerVersion（要查 version()）
        var version = db == TestDatabases.ClickHouse ? "" : connection.ServerVersion;
        TestContext.Current.TestOutputHelper?.WriteLine($"{db.DisplayName} 服务器版本：{version}");

        // 表名带随机后缀，多个 CI 任务或本地并行运行时互不干扰
        var table = $"eqs_probe_{Guid.NewGuid():N}"[..20];
        if (db == TestDatabases.TDengine)
        {
            // TDengine 的连接串只连到服务器，表要建在库里；用自己的库：与 TDengineLiveSuite 同时建同一个库会报 "Database in creating status"
            await ExecuteAsync(connection, "CREATE DATABASE IF NOT EXISTS eqs_probe PRECISION 'ms'", ct);
            table = "eqs_probe." + table;
        }
        await ExecuteAsync(connection, CreateProbeTable(db, table), ct);
        try
        {
            var (id, name) = (db.Placeholder("id"), db.Placeholder("name"));
            if (db == TestDatabases.TDengine)
            {
                // 时序库每张表的第一列是时间戳；参数化插入的值都要是参数（不能混写 NOW()）
                await ExecuteAsync(connection, $"INSERT INTO {table} (ts, id, name) VALUES (?, ?, ?)", ct,
                    Parameter(db, "ts", DateTime.UtcNow), Parameter(db, "id", 1), Parameter(db, "name", "easy-query"));
            }
            else await ExecuteAsync(connection, $"INSERT INTO {table} (id, name) VALUES ({id}, {name})", ct, Parameter(db, "id", 1), Parameter(db, "name", "easy-query"));

            await using var select = Command(connection, $"SELECT name FROM {table} WHERE id = {id}", Parameter(db, "id", 1));
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

    private static string CreateProbeTable(TestDatabase db, string table) => db switch
    {
        _ when db == TestDatabases.ClickHouse => $"CREATE TABLE {table} (id Int32, name String) ENGINE = MergeTree ORDER BY id",
        _ when db == TestDatabases.TDengine => $"CREATE TABLE {table} (ts TIMESTAMP, id INT, name NCHAR(50))",
        _ => $"CREATE TABLE {table} (id INT NOT NULL PRIMARY KEY, name VARCHAR(50))",
    };

    // 位置参数的驱动（TDengine）按添加顺序绑定，名字仍要带前缀
    private static (string Name, object Value) Parameter(TestDatabase db, string name, object value) =>
        (db.PositionalParameters ? db.ParameterPrefix + name : name, value);

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
