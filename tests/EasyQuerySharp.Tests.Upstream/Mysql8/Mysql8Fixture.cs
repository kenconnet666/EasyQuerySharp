using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.Mysql8.Mysql8Fixture))]

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java 的 mysql8/BaseTest：独立的库（表名与根包的实体冲突），code-first 建表后写入同样的种子数据。
/// 没有配置 MySQL 时用到 <see cref="Db"/> 的测试被跳过。
/// </summary>
public sealed class Mysql8Fixture : IAsyncLifetime
{
    /// <summary>本固件使用的库名。</summary>
    public const string Database = "easy-query-test-mysql8";

    // M8MenuOwner 与 M8Menu 同表，只建一次
    private static readonly Type[] Tables =
    [
        typeof(SysUser), typeof(SysBank), typeof(SysBankCard), typeof(SysUserBook),
        typeof(DocBankCard), typeof(DocBank), typeof(DocUser), typeof(DocUserBook),
        typeof(SchoolClass), typeof(SchoolTeacher), typeof(SchoolClassTeacher),
        typeof(M8User), typeof(M8Role), typeof(M8UserRole), typeof(M8User2), typeof(M8Role2), typeof(M8UserRole2),
        typeof(M8UserBook), typeof(M8RoleMenu), typeof(M8Menu), typeof(M8Province), typeof(M8City), typeof(M8Area),
        typeof(M8AreaBuild), typeof(M8Parent), typeof(M8Child), typeof(M8ParentChild), typeof(BatchInsert),
        typeof(TableNoKey), typeof(OffsetChunkTest), typeof(M8UserTest), typeof(Comment),
    ];

    private EasyDb? _db;
    private string? _skipReason;

    /// <summary>连到本固件库的 EasyDb，带 <see cref="SqlCapture"/> 监听。</summary>
    public EasyDb Db
    {
        get
        {
            if (_db is null) Assert.Skip(_skipReason ?? "mysql8 固件没有初始化");
            return _db;
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var serverCs = TestDatabases.MySql.TryConnectionString();
        if (serverCs is null)
        {
            _skipReason = TestDatabases.MySql.SkipReason;
            return;
        }
        await using (var server = new MySqlConnection(serverCs))
        {
            await server.OpenAsync();
            await SqlScript.ExecuteAsync(server, $"DROP DATABASE IF EXISTS `{Database}`; CREATE DATABASE `{Database}` CHARACTER SET utf8mb4;");
        }

        _db = new EasyDb(o => o.UseMySql(new MySqlDataSource(TestDatabases.MySql.TryConnectionString(Database)!)).AddListener(SqlCapture.Instance)
            .AddInterceptor(new MyProtectInterceptor()));
        await _db.CodeFirst.CreateTablesAsync(Tables);
        await SeedAsync(_db);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // 与 Java mysql8/BaseTest 相同的银行数据
    private static async Task SeedAsync(EasyDb db)
    {
        await db.Insert(new[]
        {
            new SysBank { Id = "1", Name = "工商银行", CreateTime = new DateTime(2000, 1, 1) },
            new SysBank { Id = "2", Name = "建设银行", CreateTime = new DateTime(2001, 1, 1) },
            new SysBank { Id = "3", Name = "招商银行", CreateTime = new DateTime(2002, 1, 1) },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new SysBankCard { Id = "bc1", Uid = "u1", Code = "123", Type = "储蓄卡", BankId = "1", OpenTime = new DateTime(2000, 1, 2) },
            new SysBankCard { Id = "bc2", Uid = "u1", Code = "1234", Type = "信用卡", BankId = "1", OpenTime = new DateTime(2000, 1, 2) },
            new SysBankCard { Id = "bc3", Uid = "u2", Code = "1235", Type = "信用卡", BankId = "1", OpenTime = new DateTime(2000, 1, 2) },
            new SysBankCard { Id = "bc4", Uid = "u1", Code = "1236", Type = "储蓄卡", BankId = "2", OpenTime = new DateTime(2001, 1, 2) },
            new SysBankCard { Id = "bc5", Code = "1237", Type = "储蓄卡", BankId = "2", OpenTime = new DateTime(2001, 1, 2) },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new SysUser { Id = "u1", Name = "用户1", Phone = "123", Age = 22, CreateTime = new DateTime(2012, 1, 1) },
            new SysUser { Id = "u2", Name = "用户2", Phone = "1234", Age = 23, CreateTime = new DateTime(2012, 1, 1) },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new SysUserBook { Id = "b1", Name = "b1book", Uid = "u1", Price = 10m },
            new SysUserBook { Id = "b2", Name = "b2book", Uid = "u1", Price = 11m },
            new SysUserBook { Id = "b3", Name = "b3book", Uid = "u2", Price = 9.9m },
        }).ExecuteAsync();

        // 与 Java mysql8/ManyJoinTest 的 before() 相同的用户、角色、菜单
        await db.Insert(new M8User2 { UserId = "123", UserAge = 123, UserName = "123", CreateTime = new DateTime(2020, 1, 1, 1, 1, 0) }).ExecuteAsync();
        await db.Insert(new M8UserRole2 { UserRoleId = "1", UserId = "123", RoleId = "456" }).ExecuteAsync();
        await db.Insert(new M8Role2 { RoleId = "456", RoleName = "456role" }).ExecuteAsync();
        await db.Insert(new M8Role { Id = "r123", Name = "测试角色", CreateTime = new DateTime(2020, 1, 1) }).ExecuteAsync();
        // 与 Java 的 mysql8/BaseTest 相同的三条评论：03ab → 0122 → 1bcc
        const string user = "3b63ddd9-b038-4c24-969e-8b478fe862a5", post = "73f5d341-c6df-43a1-afcd-e246c4d1fcc9";
        await db.Insert(new[]
        {
            new Comment { Id = "03abe9c8-adf1-4934-ae53-3b52c7c3eb2d", ParentId = "0", Content = "写得真详细", UserId = user, PostId = post, CreateAt = new DateTime(2020, 1, 1) },
            new Comment { Id = "01225d2f-e1a5-46d8-8ef9-535b7b1b7754", ParentId = "03abe9c8-adf1-4934-ae53-3b52c7c3eb2d", Content = "@用户E 具体是指哪方面？", UserId = user, PostId = post, CreateAt = new DateTime(2020, 1, 2) },
            new Comment { Id = "1bccba2c-7cff-43af-b117-2e518be4422a", ParentId = "01225d2f-e1a5-46d8-8ef9-535b7b1b7754", Content = "@用户E 你是指...", UserId = user, PostId = post, CreateAt = new DateTime(2020, 1, 3) },
        }).ExecuteAsync();
        await db.Insert(new M8RoleMenu { Id = "rm123", RoleId = "r123", MenuId = "m123" }).ExecuteAsync();
        await db.Insert(new M8Menu { Id = "m123", Name = "测试菜单", Path = "/admin", OwnerId = "o123" }).ExecuteAsync();
        await db.Insert(new M8MenuOwner { Id = "o123", Name = "测试所属菜单" }).ExecuteAsync();

        // 与 Java mysql8/BaseTest 相同的省 → 市 → 区 → 楼
        await db.Insert(new M8Province { Id = "p1", Name = "浙江" }).ExecuteAsync();
        await db.Insert(new[]
        {
            new M8City { Id = "c1", Pid = "p1", Name = "杭州" },
            new M8City { Id = "c2", Pid = "p1", Name = "宁波" },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new M8Area { Id = "a1", Cid = "c1", Name = "上城区" },
            new M8Area { Id = "a2", Cid = "c1", Name = "下城区" },
            new M8Area { Id = "a3", Cid = "c2", Name = "宁波A" },
            new M8Area { Id = "a4", Cid = "c2", Name = "宁波B" },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new M8AreaBuild { Id = "b1", Aid = "a1", Name = "科创楼" },
            new M8AreaBuild { Id = "b2", Aid = "a1", Name = "创新楼" },
            new M8AreaBuild { Id = "b3", Aid = "a2", Name = "创业楼" },
        }).ExecuteAsync();

        // 与 Java 相同的 1000 行按偏移分批读取的数据
        await db.Insert(Enumerable.Range(0, 1000).Select(i => new OffsetChunkTest { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Seq = i }).ToList()).ExecuteAsync();
    }
}

/// <summary>用 mysql8 固件的测试基类（对应 Java 的 mysql8/BaseTest 子类）。</summary>
public abstract class Mysql8Test(Mysql8Fixture fixture)
{
    /// <summary>连到本固件库的 EasyDb。</summary>
    protected EasyDb Db => fixture.Db;

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}

/// <summary>对应 Java：common/MyProtectInterceptor：受保护的查询过滤（DisableInterceptors() 关不掉），只作用于 M8UserProtect，条件是 1=1。</summary>
public sealed class MyProtectInterceptor : EasyQuerySharp.Interceptors.QueryFilter<M8UserProtect>
{
    /// <inheritdoc />
    public override string Name => nameof(MyProtectInterceptor);

    /// <inheritdoc />
    public override bool IsProtected => true;

    /// <inheritdoc />
    public override System.Linq.Expressions.Expression<Func<M8UserProtect, bool>> Filter() => _ => Sql.Raw<bool>("1=1");
}
