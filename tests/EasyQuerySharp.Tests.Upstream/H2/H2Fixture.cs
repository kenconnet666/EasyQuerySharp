using EasyQuerySharp.Dialects;
using Microsoft.Data.Sqlite;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.H2.H2Fixture))]

namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java 的 h2/H2BaseTest。H2 没有 .NET 驱动，与方言无关的 H2 用例改在 SQLite 共享内存库上跑（进程内，总是可用）。
/// Java 把关键字服务换成了 DefaultSQLKeyword（标识符不加引号），这里同样用不加引号的 SQLite 方言，SQL 文本因此能与 Java 逐字对比。
/// 表用 code-first 建（Java 执行 h2-ds2020.sql），种子数据与 Java 的 initData 相同；H2DocTest、TreeCTETest 用到的
/// doc_*、category 两组表 Java 的 H2 库里没有（用例只断言 SQL、吞掉执行错误），这里一并建好并写入与 PG 夹具相同的数据。
/// </summary>
public sealed class H2Fixture : IAsyncLifetime
{
    private const string ConnectionString = "Data Source=eqs-upstream-h2;Mode=Memory;Cache=Shared";

    private static readonly Type[] Tables =
    [
        typeof(DefTable), typeof(DefTableLeft1), typeof(DefTableLeft2), typeof(DefTableLeft3), typeof(ALLTYPE), typeof(TbOrder), typeof(TbAccount),
        typeof(DocUser), typeof(DocBank), typeof(DocBankCard), typeof(MyCategory),
    ];

    // 共享内存库只要还有一个连接开着就一直在
    private readonly SqliteConnection _keepAlive = new(ConnectionString);

    /// <summary>连到本固件库的 EasyDb（对应 Java 的 easyEntityQuery），带 <see cref="SqlCapture"/> 监听。</summary>
    public EasyDb Db { get; } = new(o => o.Use(UnquotedSqliteDialect.Instance, SqliteFactory.Instance.CreateDataSource(ConnectionString)).AddListener(SqlCapture.Instance));

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync();
        await Db.CodeFirst.CreateTablesAsync(Tables);
        await SeedAsync(Db);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    // 与 Java H2BaseTest.initData 相同的数据：四张 t_def_table* 各 1000 行、一行 ALLTYPE
    private static async Task SeedAsync(EasyDb db)
    {
        var begin = new DateTime(2020, 1, 1, 0, 0, 0);
        var ids = Enumerable.Range(0, 1000).ToList();
        await db.Insert(ids.Select(i => new DefTable
        {
            Id = Id(i), UserName = "username" + i, Nickname = "nickname" + i, Enable = i % 2 == 0, Score = i + 0.1m, Mobile = "133" + i,
            Avatar = $"http://www.{i}.com", Number = i, Status = i, Created = begin.AddDays(i),
        })).Batch().ExecuteAsync();
        await db.Insert(ids.Select(i => new DefTableLeft1
        {
            Id = Id(i), DefId = Id(i), UserName = "username" + i, Nickname = "nickname" + i, Enable = i % 2 == 0, Score = i + 0.1m, Mobile = "133" + i,
            Avatar = $"http://www.{i}.com", Number = i, Status = i, Created = begin.AddDays(i),
        })).Batch().ExecuteAsync();
        await db.Insert(ids.Select(i => new DefTableLeft2
        {
            Id = Id(i), DefId = Id(i), Def1Id = Id(i), UserName = "username" + i, Nickname = "nickname" + i, Enable = i % 2 == 0, Score = i + 0.1m,
            Mobile = "133" + i, Avatar = $"http://www.{i}.com", Number = i, Status = i, Created = begin.AddDays(i),
        })).Batch().ExecuteAsync();
        await db.Insert(ids.Select(i => new DefTableLeft3
        {
            Id = Id(i), DefId = Id(i), Def1Id = Id(i), Def2Id = Id(i), UserName = "username" + i, Nickname = "nickname" + i, Enable = i % 2 == 0,
            Score = i + 0.1m, Mobile = "133" + i, Avatar = $"http://www.{i}.com", Number = i, Status = i, Created = begin.AddDays(i),
        })).Batch().ExecuteAsync();
        await db.Insert(ALLTYPE.Sample(BasicTypeTest.SampleId)).ExecuteAsync();

        await db.Insert(new[]
        {
            new DocUser { Id = "小明id", Name = "小明姓名", Phone = "小明手机", Age = 1 },
            new DocUser { Id = "小红id", Name = "小红姓名", Phone = "小红手机", Age = 1 },
            new DocUser { Id = "小黄id", Name = "小黄姓名", Phone = "小黄手机", Age = 1 },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new DocBank { Id = "建设银行", Name = "建设银行" },
            new DocBank { Id = "中国银行", Name = "中国银行" },
            new DocBank { Id = "工商银行", Name = "工商银行" },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new DocBankCard { Id = "小明工商", Code = "1001", Type = "储蓄卡", Uid = "小明id", BankId = "工商银行" },
            new DocBankCard { Id = "小红建设", Code = "1002", Type = "储蓄卡", Uid = "小红id", BankId = "建设银行" },
        }).ExecuteAsync();
        // 分类树：1 → 3 → 5、6；1 → 4
        await db.Insert(new[]
        {
            new MyCategory { Id = "1", ParentId = null, Name = "根节点" },
            new MyCategory { Id = "3", ParentId = "1", Name = "节点3" },
            new MyCategory { Id = "4", ParentId = "1", Name = "节点4" },
            new MyCategory { Id = "5", ParentId = "3", Name = "节点5" },
            new MyCategory { Id = "6", ParentId = "3", Name = "节点6" },
        }).ExecuteAsync();
    }

    private static string Id(int i) => i.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>标识符不加引号的 SQLite 方言（对应 Java H2BaseTest 的 <c>replaceService(SQLKeyword.class, DefaultSQLKeyword.class)</c>）。</summary>
public sealed class UnquotedSqliteDialect : SqliteDialect
{
    /// <summary>共享实例。</summary>
    public static new UnquotedSqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string QuoteIdentifier(string identifier) => identifier;
}

/// <summary>用 H2 固件（SQLite）的测试基类（对应 Java 的 H2BaseTest 子类）。</summary>
public abstract class H2Test(H2Fixture fixture)
{
    /// <summary>连到本固件库的 EasyDb（对应 Java 的 easyEntityQuery）。</summary>
    protected EasyDb Db => fixture.Db;

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}
