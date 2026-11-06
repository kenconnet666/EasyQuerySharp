using System.Collections.Concurrent;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.TestKit.Databases;

namespace EasyQuerySharp.Tests.Live;

// 每个数据库一个薄子类：只给出库、方言、连接串和不支持的能力，用例全部继承自 LiveSuite

/// <summary>SQLite：临时文件库（内存库每条连接各是一个库，跨连接看不到表）；没有行锁。</summary>
public sealed class SqliteLiveSuite : LiveSuite
{
    private static readonly string File = Path.Combine(Path.GetTempPath(), $"eqs_live_{Environment.ProcessId}.db");

    protected override TestDatabase Database => TestDatabases.Sqlite;
    protected override SqlDialect Dialect => SqliteDialect.Instance;
    protected override LiveSuiteOptions Options { get; } = new()
    {
        RowLocks = false, Rollup = false, Cube = false, GroupingFunction = false,
        TruncateWithScale = false, ExtremesIgnoreNull = false,
    };
    protected override Task<string> ConnectionStringAsync() => Task.FromResult($"Data Source={File};Pooling=False");
    protected internal override Task<string> ShardConnectionStringAsync() => Task.FromResult($"Data Source={Path.ChangeExtension(File, null)}_ds1.db;Pooling=False");
}

/// <summary>MySQL 系（8.4、8.0.18、MariaDB）：在服务器上建独立的库 <c>eqs_live</c>（分库用例另有 <c>eqs_live_ds1</c>）。</summary>
public abstract class MySqlFamilyLiveSuite : LiveSuite
{
    protected override SqlDialect Dialect => MySqlDialect.Instance;
    // MySQL 系只有 WITH ROLLUP，没有 CUBE；GREATEST / LEAST 碰到 NULL 返回 NULL
    protected override LiveSuiteOptions Options { get; } = new() { Cube = false, ExtremesIgnoreNull = false };
    protected override Task<string> ConnectionStringAsync() =>
        LiveDatabase.EnsureAsync(Database, "CREATE DATABASE IF NOT EXISTS `eqs_live` CHARACTER SET utf8mb4");
    protected internal override Task<string> ShardConnectionStringAsync() =>
        LiveDatabase.EnsureAsync(Database, "CREATE DATABASE IF NOT EXISTS `eqs_live_ds1` CHARACTER SET utf8mb4", name: "eqs_live_ds1");
}

/// <summary>MySQL 8.4。</summary>
public sealed class MySqlLiveSuite : MySqlFamilyLiveSuite
{
    protected override TestDatabase Database => TestDatabases.MySql;
}

/// <summary>MySQL 8.0.18（PolarDB 基线）。</summary>
public sealed class MySql8018LiveSuite : MySqlFamilyLiveSuite
{
    protected override TestDatabase Database => TestDatabases.MySql8018;
}

/// <summary>MariaDB 11.4。</summary>
public sealed class MariaDbLiveSuite : MySqlFamilyLiveSuite
{
    protected override TestDatabase Database => TestDatabases.MariaDb;
    protected override LiveSuiteOptions Options { get; } = new() { Cube = false, GroupingFunction = false, MixedWindowLag = false, ExtremesIgnoreNull = false };
}

/// <summary>PostgreSQL 17：独立的库 <c>eqs_live</c>（PG 没有 CREATE DATABASE IF NOT EXISTS，先查 pg_database）。</summary>
public sealed class PostgreSqlLiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.PostgreSql;
    protected override SqlDialect Dialect => PostgreSqlDialect.Instance;
    protected override LiveSuiteOptions Options { get; } = new() { JsonFunctions = false };
    protected override Task<string> ConnectionStringAsync() =>
        LiveDatabase.EnsureAsync(Database, "SELECT 1", "SELECT 1 FROM pg_database WHERE datname = 'eqs_live'", "CREATE DATABASE eqs_live");
    protected internal override Task<string> ShardConnectionStringAsync() =>
        LiveDatabase.EnsureAsync(Database, "SELECT 1", "SELECT 1 FROM pg_database WHERE datname = 'eqs_live_ds1'", "CREATE DATABASE eqs_live_ds1", "eqs_live_ds1");
}

/// <summary>SQL Server 2022：独立的库 <c>eqs_live</c>。</summary>
public sealed class SqlServerLiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.SqlServer;
    protected override SqlDialect Dialect => SqlServerDialect.Instance;
    // SQL Server 没有 FOR UPDATE（要用表提示），与 Java 一样直接报错；也没有 NTH_VALUE
    protected override LiveSuiteOptions Options { get; } = new() { RowLocks = false, NthValue = false, JsonFunctions = false };
    protected override Task<string> ConnectionStringAsync() =>
        LiveDatabase.EnsureAsync(Database, "IF DB_ID('eqs_live') IS NULL CREATE DATABASE eqs_live");
    protected internal override Task<string> ShardConnectionStringAsync() =>
        LiveDatabase.EnsureAsync(Database, "IF DB_ID('eqs_live_ds1') IS NULL CREATE DATABASE eqs_live_ds1", name: "eqs_live_ds1");
}

/// <summary>Oracle 23ai：用连接串里的用户自己的 schema（没有第二个 schema，分库用例跳过）。</summary>
public sealed class OracleLiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.Oracle;
    protected override SqlDialect Dialect => OracleDialect.Instance;
    protected override LiveSuiteOptions Options { get; } = new() { JsonFunctions = false, ShardDataSources = false };
}

/// <summary>达梦 DM8：在 SYSDBA 下建独立的模式 <c>EQS_LIVE</c>（分库用例另有 <c>EQS_LIVE_DS1</c>），连接串用 <c>Schema</c> 指定。</summary>
public sealed class DamengLiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.Dameng;
    protected override SqlDialect Dialect => DamengDialect.Instance;
    // 达梦沿用 Oracle 方言，没有 Sql.Json 的写法
    protected override LiveSuiteOptions Options { get; } = new() { JsonFunctions = false };
    protected override Task<string> ConnectionStringAsync() => SchemaAsync("EQS_LIVE");
    protected internal override Task<string> ShardConnectionStringAsync() => SchemaAsync("EQS_LIVE_DS1");

    private Task<string> SchemaAsync(string schema) =>
        LiveDatabase.EnsureAsync(Database, "SELECT 1", $"SELECT 1 FROM SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = '{schema}'", $"CREATE SCHEMA {schema}", schema);
}

/// <summary>openGauss：独立的库 <c>eqs_live</c>，按 PG 兼容模式建（默认的 A 兼容模式把空字符串当 NULL，与 PG 方言不符）。</summary>
public sealed class OpenGaussLiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.OpenGauss;
    protected override SqlDialect Dialect => GaussDbDialect.Instance;
    // 与 PG 一样：-> / JSONB_EXISTS 要求 json / jsonb 列，模型的 JSON 列是 VARCHAR
    protected override LiveSuiteOptions Options { get; } = new() { JsonFunctions = false };
    protected override Task<string> ConnectionStringAsync() => DatabaseAsync("eqs_live");
    protected internal override Task<string> ShardConnectionStringAsync() => DatabaseAsync("eqs_live_ds1");

    private Task<string> DatabaseAsync(string name) =>
        LiveDatabase.EnsureAsync(Database, "SELECT 1", $"SELECT 1 FROM pg_database WHERE datname = '{name}'", $"CREATE DATABASE {name} DBCOMPATIBILITY 'PG'", name);
}

/// <summary>Db2 12.1：用连接串里的用户自己的 schema（建第二个库要几分钟，分库用例跳过）。</summary>
public sealed class Db2LiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.Db2;
    protected override SqlDialect Dialect => Db2Dialect.Instance;
    // GREATEST / LEAST 碰到 NULL 返回 NULL；没有 NOWAIT；SQL/JSON 路径取不了数组长度
    protected override LiveSuiteOptions Options { get; } = new() { ExtremesIgnoreNull = false, NoWait = false, JsonLength = false, ShardDataSources = false };
}

/// <summary>DuckDB（进程内）：临时文件库（内存库每条连接各是一个库，跨连接看不到表）；没有行锁。</summary>
public sealed class DuckDbLiveSuite : LiveSuite
{
    private static readonly string File = Path.Combine(Path.GetTempPath(), $"eqs_live_{Environment.ProcessId}.duckdb");

    protected override TestDatabase Database => TestDatabases.DuckDb;
    protected override SqlDialect Dialect => DuckDbDialect.Instance;
    protected override LiveSuiteOptions Options { get; } = new() { RowLocks = false };
    // DuckDB.NET 在最后一个连接关闭时关库（落盘），每条语句都重新开关会慢得多：整个进程里留一个连接不关
    private static readonly Lazy<DuckDB.NET.Data.DuckDBConnection> Anchor = new(() => Open(File));
    private static readonly Lazy<DuckDB.NET.Data.DuckDBConnection> ShardAnchor = new(() => Open(Path.ChangeExtension(File, null) + "_ds1.duckdb"));

    protected override Task<string> ConnectionStringAsync() => Task.FromResult(Anchor.Value.ConnectionString);
    protected internal override Task<string> ShardConnectionStringAsync() => Task.FromResult(ShardAnchor.Value.ConnectionString);

    private static DuckDB.NET.Data.DuckDBConnection Open(string file)
    {
        var connection = new DuckDB.NET.Data.DuckDBConnection($"Data Source={file}");
        connection.Open();
        return connection;
    }
}

/// <summary>SAP HANA Express 2.0：租户 HXE 里独立的 schema <c>EQS_LIVE</c>（分库用例另有 <c>EQS_LIVE_DS1</c>），连接串用 <c>Current Schema</c> 选它。</summary>
public sealed class HanaLiveSuite : LiveSuite
{
    protected override TestDatabase Database => TestDatabases.Hana;
    protected override SqlDialect Dialect => HanaDialect.Instance;
    // GREATEST / LEAST 碰到 NULL 返回 NULL；方言还没有 Sql.Json 的写法（Java fork 也没有）
    protected override LiveSuiteOptions Options { get; } = new() { RecursiveCte = false, ExtremesIgnoreNull = false, JsonFunctions = false };
    protected override Task<string> ConnectionStringAsync() => SchemaAsync("EQS_LIVE");
    protected internal override Task<string> ShardConnectionStringAsync() => SchemaAsync("EQS_LIVE_DS1");

    private Task<string> SchemaAsync(string schema) =>
        LiveDatabase.EnsureAsync(Database, "SELECT 1 FROM DUMMY", $"SELECT 1 FROM SCHEMAS WHERE SCHEMA_NAME = '{schema}'", $"CREATE SCHEMA {schema}", schema);
}

/// <summary>按库建一次独立的测试库（默认 <c>eqs_live</c>），返回连到它的连接串。</summary>
internal static class LiveDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<Task>> Created = new();

    /// <summary>
    /// 在服务器连接上执行 <paramref name="create"/>（每个进程每个库只执行一次）。
    /// 给了 <paramref name="existsQuery"/> 时先查：查到行就不再执行 <paramref name="createIfMissing"/>。
    /// </summary>
    public static async Task<string> EnsureAsync(TestDatabase database, string create, string? existsQuery = null, string? createIfMissing = null, string name = "eqs_live")
    {
        _ = database.ConnectionString();   // 没有配置时在这里跳过整个用例
        await Created.GetOrAdd($"{database.Key}/{name}", _ => new Lazy<Task>(() => CreateAsync(database, create, existsQuery, createIfMissing))).Value;
        return database.ConnectionString(name);

        static async Task CreateAsync(TestDatabase database, string create, string? existsQuery, string? createIfMissing)
        {
            await using var connection = await database.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = existsQuery ?? create;
            var exists = await command.ExecuteScalarAsync();
            if (existsQuery is not null && exists is null && createIfMissing is not null)
            {
                command.CommandText = createIfMissing;
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
