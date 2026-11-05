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

    private static readonly Type[] Tables = [typeof(SysUser), typeof(SysBank), typeof(SysBankCard), typeof(SysUserBook)];

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

        _db = new EasyDb(o => o.UseMySql(new MySqlDataSource(TestDatabases.MySql.TryConnectionString(Database)!)).AddListener(SqlCapture.Instance));
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
