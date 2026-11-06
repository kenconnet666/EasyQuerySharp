using System.Data.Common;
using Dm;
using ClickHouse.Driver.ADO;
using DuckDB.NET.Data;
using IBM.Data.Db2;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using TDengine.Data.Client;

namespace EasyQuerySharp.TestKit.Databases;

/// <summary>
/// 一个可供实库测试连接的数据库。
/// 连接串取自 <c>EQS_{KEY}_CS</c>（见 <see cref="LocalEnvironment"/>）；进程内数据库（SQLite）自带连接串，总是可用。
/// </summary>
/// <param name="Key">短名，用于环境变量名与 <c>EQS_REQUIRE_DBS</c>。</param>
/// <param name="DisplayName">测试输出里显示的名字。</param>
/// <param name="Factory">ADO.NET 驱动工厂。</param>
/// <param name="ParameterPrefix">驱动认的命名参数前缀，Oracle、达梦是 <c>:</c>，DuckDB 是 <c>$</c>，其余是 <c>@</c>。</param>
/// <param name="InProcessConnectionString">进程内数据库的固定连接串；为 null 表示要从环境读。</param>
/// <param name="DatabaseKeyword">连接串里指定库的键：达梦按模式隔离，是 <c>Schema</c>；HANA 一个租户里按 schema 隔离，是 <c>Current Schema</c>。</param>
/// <param name="DriverSettings">这个库必须的驱动设置，追加到配置的连接串后面（如 openGauss 不支持 Npgsql 归还连接时发的 <c>DISCARD ALL</c>）。</param>
/// <param name="MissingDriver">驱动没装时的说明（驱动不在 nuget.org 上、要另装客户端的库）；不为 null 时按没有配置处理。</param>
/// <param name="PositionalParameters">SQL 里只认位置参数 <c>?</c>（TDengine）；参数对象的名字仍要带 <paramref name="ParameterPrefix"/>。</param>
public sealed record TestDatabase(
    string Key,
    string DisplayName,
    DbProviderFactory Factory,
    string ParameterPrefix = "@",
    string? InProcessConnectionString = null,
    string DatabaseKeyword = "Database",
    string? DriverSettings = null,
    string? MissingDriver = null,
    bool PositionalParameters = false)
{
    /// <summary>SQL 里名为 <paramref name="name"/> 的参数占位符。</summary>
    public string Placeholder(string name) => PositionalParameters ? "?" : ParameterPrefix + name;

    /// <summary>连接串所在的环境变量名。</summary>
    public string EnvironmentVariable => $"EQS_{Key.ToUpperInvariant()}_CS";

    /// <summary>
    /// 是否必须可用：进程内数据库总是必须；其余的在 <c>EQS_REQUIRE_DBS</c> 里列出时必须（CI 设置，防止服务容器没起来时测试被静默跳过）。
    /// </summary>
    public bool IsRequired => InProcessConnectionString is not null || TestDatabases.RequiredKeys.Contains(Key);

    /// <summary>
    /// 打开一个连接。没有配置连接串或没装驱动时：必须可用的数据库直接判失败，其余跳过当前测试。
    /// </summary>
    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        // 先取连接串：不可用时在这里跳过，不去碰没装好的驱动
        var connectionString = ConnectionString();
        var connection = Factory.CreateConnection()
                         ?? throw new InvalidOperationException($"{Factory.GetType().Name} 没有创建出连接");
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// 连接串；没有配置时：必须可用的数据库判失败，其余跳过当前测试。
    /// <paramref name="database"/> 不为 null 时替换连接串里的库名（Java 用例要求特定库名，如 <c>easy-query-test</c>）。
    /// </summary>
    public string ConnectionString(string? database = null)
    {
        if (TryConnectionString(database) is { } cs) return cs;
        Assert.Skip(SkipReason);
        return null!;
    }

    /// <summary>
    /// 连接串；没有配置时返回 null（必须可用的数据库直接判失败）。供固件在初始化阶段判断，测试里用 <see cref="ConnectionString"/>。
    /// </summary>
    public string? TryConnectionString(string? database = null)
    {
        var connectionString = InProcessConnectionString ?? LocalEnvironment.Get(EnvironmentVariable);
        if (connectionString is null || MissingDriver is not null)
        {
            if (IsRequired) Assert.Fail($"EQS_REQUIRE_DBS 要求 {Key}，但{(connectionString is null ? $"没有配置 {EnvironmentVariable}" : MissingDriver)}");
            return null;
        }
        if (DriverSettings is not null) connectionString = $"{connectionString.TrimEnd(';')};{DriverSettings}";
        if (database is null) return connectionString;
        var builder = Factory.CreateConnectionStringBuilder()!;
        builder.ConnectionString = connectionString;
        builder[DatabaseKeyword] = database;
        return builder.ConnectionString;
    }

    /// <summary>没有配置时跳过测试的说明。</summary>
    public string SkipReason => MissingDriver is not null && LocalEnvironment.Get(EnvironmentVariable) is not null
        ? $"{MissingDriver}，跳过 {DisplayName}"
        : $"没有配置 {EnvironmentVariable}，跳过 {DisplayName}（本地运行 infra/dev/db.sh 生成 .local/db.env）";

    /// <summary>创建数据源（交给 EasyDb）。</summary>
    public DbDataSource CreateDataSource(string? database = null) => Factory.CreateDataSource(ConnectionString(database));

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>实库测试支持的全部数据库。新增方言时在这里登记，并在 infra/dev/compose.yaml 与 CI 里加对应的容器。</summary>
public static class TestDatabases
{
    /// <summary>SQLite 内存库，零基础设施。</summary>
    public static readonly TestDatabase Sqlite = new("sqlite", "SQLite", SqliteFactory.Instance, InProcessConnectionString: "Data Source=:memory:");

    /// <summary>MySQL 8.4，本地与 CI 都跑。</summary>
    public static readonly TestDatabase MySql = new("mysql", "MySQL 8.4", MySqlConnectorFactory.Instance);

    /// <summary>MySQL 8.0.18：生产 PolarDB 的内核版本基线。</summary>
    public static readonly TestDatabase MySql8018 = new("mysql8018", "MySQL 8.0.18 (PolarDB)", MySqlConnectorFactory.Instance);

    /// <summary>MariaDB 11.4 LTS（MySQL 协议）。</summary>
    public static readonly TestDatabase MariaDb = new("mariadb", "MariaDB 11.4", MySqlConnectorFactory.Instance);

    /// <summary>PostgreSQL 17，本地与 CI 都跑。</summary>
    public static readonly TestDatabase PostgreSql = new("pg", "PostgreSQL 17", NpgsqlFactory.Instance);

    /// <summary>SQL Server 2022。</summary>
    public static readonly TestDatabase SqlServer = new("mssql", "SQL Server 2022", SqlClientFactory.Instance);

    /// <summary>Oracle 23ai Free。</summary>
    public static readonly TestDatabase Oracle = new("oracle", "Oracle 23ai Free", OracleClientFactory.Instance, ParameterPrefix: ":");

    /// <summary>DuckDB（进程内）：连接串里的 <c>:memory:</c> 每条连接各是一个库，用例要共享数据时自己指定临时文件。</summary>
    public static readonly TestDatabase DuckDb = new("duckdb", "DuckDB", DuckDBClientFactory.Instance, ParameterPrefix: "$", InProcessConnectionString: "Data Source=:memory:");

    /// <summary>达梦 DM8（第二档，实库冒烟）：驱动 DM.DmProvider，命名参数 <c>:p0</c>；库名对应达梦的模式（<c>Schema</c>）。</summary>
    public static readonly TestDatabase Dameng = new("dameng", "达梦 DM8", DmClientFactory.Instance, ParameterPrefix: ":", DatabaseKeyword: "Schema");

    /// <summary>openGauss（第二档，实库冒烟）：PG 协议，用 Npgsql；openGauss 没有 DISCARD，连接回池时不重置会话。</summary>
    public static readonly TestDatabase OpenGauss = new("opengauss", "openGauss", NpgsqlFactory.Instance, DriverSettings: "No Reset On Close=true");

    /// <summary>SAP HANA Express 2.0（第二档，实库冒烟）：驱动在运行时从本机的 HANA 客户端加载（见 <see cref="HanaDriver"/>），按 schema 隔离。</summary>
    public static readonly TestDatabase Hana = HanaDriver.Database();

    /// <summary>ClickHouse 25.8（HTTP 接口）：驱动把 <c>@name</c> 改写成带类型的 <c>{name:Type}</c>。</summary>
    public static readonly TestDatabase ClickHouse = new("clickhouse", "ClickHouse 25.8", ClickHouseConnectionFactory.Instance);

    /// <summary>IBM Db2 12.1 Community。</summary>
    public static readonly TestDatabase Db2 = new("db2", "Db2 12.1", Db2Factory());

    /// <summary>TDengine 3.3（WebSocket，不用装客户端）：SQL 里是 <c>?</c>，参数对象的名字要以 <c>@</c> 开头。</summary>
    public static readonly TestDatabase TDengine = new("tdengine", "TDengine 3.3", new TDengineFactory(), PositionalParameters: true);

    // Db2 的 CLI 驱动按系统代码页通信，中文 Windows（936）上连接直接失败、只报 "no error information available"：
    // 在驱动加载前把客户端代码页固定成 UTF-8（1208）。环境里已经设置的不覆盖
    private static DbProviderFactory Db2Factory()
    {
        if (Environment.GetEnvironmentVariable("DB2CODEPAGE") is null) Environment.SetEnvironmentVariable("DB2CODEPAGE", "1208");
        return DB2Factory.Instance;
    }

    /// <summary>全部登记的数据库。</summary>
    public static IReadOnlyList<TestDatabase> All { get; } = [Sqlite, MySql, MySql8018, MariaDb, PostgreSql, SqlServer, Oracle, DuckDb, Dameng, OpenGauss, Hana, ClickHouse, Db2, TDengine];

    /// <summary><c>EQS_REQUIRE_DBS</c>（逗号分隔的 Key）里列出的必须可用的数据库。</summary>
    public static IReadOnlySet<string> RequiredKeys { get; } =
        (LocalEnvironment.Get("EQS_REQUIRE_DBS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>按 Key 取数据库。</summary>
    public static TestDatabase Get(string key) =>
        All.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"未登记的数据库：{key}，可选：{string.Join(", ", All.Select(d => d.Key))}", nameof(key));
}
