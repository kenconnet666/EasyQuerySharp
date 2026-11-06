using EasyQuerySharp.Metadata;
using EasyQuerySharp.TestKit.Databases;
using Microsoft.Data.SqlClient;
using SysBank = EasyQuerySharp.Tests.Upstream.Mysql8.SysBank;
using SysBankCard = EasyQuerySharp.Tests.Upstream.Mysql8.SysBankCard;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.Mssql.MsSqlFixture))]

namespace EasyQuerySharp.Tests.Upstream.Mssql;

/// <summary>
/// 对应 Java 的 mssql/MsSQLBaseTest：每次测试运行重建 SQL Server 库 <c>mssql_eq</c>（UpperCamelCase 命名），code-first 建表后写入同样的种子数据。
/// 数学函数用例的 <c>t_math_test</c> 由用例自己建表写入（与 MySQL、PG 的数学用例共用同一份固定数据，不是 Java 的 10000 行随机数）。
/// 没有配置 SQL Server 时用到 <see cref="Db"/> 的测试被跳过。
/// </summary>
public sealed class MsSqlFixture : IAsyncLifetime
{
    /// <summary>Java 用例使用的库名。</summary>
    public const string Database = "mssql_eq";

    // Java 在各个 @Before 里 syncTable 的表：基类的银行数据、MsSQLQueryTest 的 t_calc、MsSQLUpdateJoinTest 的 doc_*
    private static readonly Type[] Tables =
        [typeof(MsSQLMyTopic), typeof(SysUser), typeof(SysBank), typeof(SysBankCard), typeof(MsSQLCalc), typeof(DocBankCard), typeof(DocUser), typeof(DocBank)];

    private EasyDb? _db;
    private string? _skipReason;

    /// <summary>连到 <c>mssql_eq</c> 的 EasyDb，带 <see cref="SqlCapture"/> 监听；没有配置 SQL Server 时跳过当前测试。</summary>
    public EasyDb Db
    {
        get
        {
            if (_db is null) Assert.Skip(_skipReason ?? "SQL Server 固件没有初始化");
            return _db;
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var serverCs = TestDatabases.SqlServer.TryConnectionString();
        if (serverCs is null)
        {
            _skipReason = TestDatabases.SqlServer.SkipReason;
            return;
        }
        await using (var server = new SqlConnection(serverCs))
        {
            await server.OpenAsync();
            // 上次运行残留的连接不妨碍删库：先切单用户并回滚它们
            await SqlScript.ExecuteAsync(server, $"IF DB_ID('{Database}') IS NOT NULL ALTER DATABASE [{Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE IF EXISTS [{Database}]; CREATE DATABASE [{Database}];");
        }

        _db = new EasyDb(o =>
        {
            o.UseSqlServer(TestDatabases.SqlServer.CreateDataSource(Database)).AddListener(SqlCapture.Instance);
            o.NameConversion = NameConversion.UpperCamelCase;
        });
        await _db.CodeFirst.CreateTablesAsync(Tables);
        await SeedAsync(_db);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // 与 Java MsSQLBaseTest.initData 相同的数据
    private static async Task SeedAsync(EasyDb db)
    {
        await db.Insert(Enumerable.Range(0, 100).Select(i => new MsSQLMyTopic
        {
            Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Stars = i + 100,
            Title = "标题" + i,
            CreateTime = new DateTime(2021, 12, 1).AddDays(i),
        })).ExecuteAsync();
        await Mysql8.BankSeed.InsertAsync(db);
    }
}

/// <summary>用 SQL Server 固件的测试基类（对应 Java 的 MsSQLBaseTest 子类）。</summary>
public abstract class MsSqlTest(MsSqlFixture fixture)
{
    /// <summary>连到本固件库的 EasyDb（对应 Java 的 entityQuery）。</summary>
    protected EasyDb Db => fixture.Db;

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}
