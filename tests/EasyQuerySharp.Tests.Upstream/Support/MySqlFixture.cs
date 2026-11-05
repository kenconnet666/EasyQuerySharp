using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.Support.MySqlFixture))]

namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>
/// 对应 Java 的 BaseTest：每次测试运行重建库 <c>easy-query-test</c>，执行 Java 的建表脚本，写入同样的种子数据，
/// 并按 BaseTest 注册拦截器等全局配置。没有配置 MySQL 时不报错，用到 <see cref="Db"/> 的测试被跳过。
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    /// <summary>Java 用例使用的库名（部分实体的 schema 写死为它）。</summary>
    public const string Database = "easy-query-test";

    private static readonly string[] Scripts = ["mysql-init-sqk-easy.sql", "relation.sql", "doc.sql"];

    // 不在建表脚本里、由 Java 用例在 @Before 里 syncTable 创建的表
    private static readonly Type[] CodeFirstTables =
        [typeof(SubJoinUser), typeof(SubJoinBook), typeof(SubJoinAuthor), typeof(InterceptorEntity), typeof(Interceptor2Entity)];

    private EasyDb? _db;
    private string? _skipReason;

    /// <summary>连到 <c>easy-query-test</c> 的 EasyDb，带 <see cref="SqlCapture"/> 监听；没有配置 MySQL 时跳过当前测试。</summary>
    public EasyDb Db
    {
        get
        {
            if (_db is null) Assert.Skip(_skipReason ?? "MySQL 固件没有初始化");
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
            await SqlScript.ExecuteAsync(server,
                $"DROP DATABASE IF EXISTS `{Database}`; CREATE DATABASE `{Database}` CHARACTER SET utf8mb4;");
        }

        var cs = TestDatabases.MySql.TryConnectionString(Database)!;
        await using (var connection = new MySqlConnection(cs))
        {
            await connection.OpenAsync();
            foreach (var script in Scripts)
                await SqlScript.ExecuteAsync(connection, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Resources", "mysql", script)),
                    skip: s => s.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase));
        }

        // 与 Java 的 BaseTest 一致：深分页反向排序的阈值为 10
        _db = new EasyDb(o =>
        {
            o.ReverseOffsetThreshold = 10;
            o.UseMySql(new MySqlDataSource(cs)).AddListener(SqlCapture.Instance)
                .AddInterceptor(new Topic1Interceptor())
                .AddInterceptor(new MyEntityInterceptor())
                .AddInterceptor(new MyTenantInterceptor())
                .AddInterceptor(new MyTenantInsertInterceptor())
                .AddInterceptor(new TopicUpdateInterceptor())
                .AddInterceptor(new TestInterceptor())
                .AddInterceptor(new TestInterceptor2())
                .AddLogicDeleteStrategy(nameof(MyLogicDelStrategy), new MyLogicDelStrategy());
        });
        await _db.CodeFirst.CreateTablesAsync(CodeFirstTables);
        await SeedAsync(_db);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // 与 Java BaseTest.initData 相同的数据
    private static async Task SeedAsync(EasyDb db)
    {
        var begin = new DateTime(2020, 1, 1, 1, 1, 1);
        await db.Insert(Enumerable.Range(0, 100).Select(i => new BlogEntity
        {
            Id = i.ToString(),
            CreateBy = i.ToString(),
            CreateTime = begin.AddDays(i),
            UpdateBy = i.ToString(),
            UpdateTime = begin.AddDays(i),
            Title = "title" + i,
            Content = "content" + i,
            Url = "http://blog.easy-query.com/" + i,
            Star = i,
            Score = 1.2m,
            Status = i % 3 == 0 ? 0 : 1,
            Order = 1.2m * i,
            IsTop = i % 2 == 0,
            Top = i % 2 == 0,
            Deleted = false,
        })).ExecuteAsync();

        await db.Insert(Enumerable.Range(0, 10).Select(i => new TopicAuto
        {
            Stars = i,
            Title = "title" + i,
            CreateTime = DateTime.Now.AddDays(i),
        })).FillGeneratedKeys().ExecuteAsync();

        await db.Insert(Enumerable.Range(0, 100).Select(i => new Topic
        {
            Id = i.ToString(),
            Stars = i + 100,
            Title = "标题" + i,
            CreateTime = DateTime.Now.AddDays(i),
        })).ExecuteAsync();

        await db.Insert(Enumerable.Range(0, 100).Select(i => new SysUser
        {
            Id = i.ToString(),
            Username = "username" + i,
            CreateTime = DateTime.Now.AddDays(i),
            Phone = "18888888" + i + i,
            IdCard = "333333333333333" + i + i,
            Address = "18888888" + i + i + "333333333333333" + i + i,
        })).ExecuteAsync();

        await db.Insert(Enumerable.Range(0, 100).Select(i => new LogicDelTopic
        {
            Id = i.ToString(),
            Stars = i + 100,
            Title = "标题" + i,
            CreateTime = DateTime.Now.AddDays(i),
            Deleted = false,
        })).ExecuteAsync();

        await db.Insert(Enumerable.Range(0, 100).Select(i => new LogicDelTopicCustom
        {
            Id = i.ToString(),
            Stars = i + 100,
            Title = "标题" + i,
            CreateTime = DateTime.Now.AddDays(i),
        })).ExecuteAsync();

        await db.Insert(Enumerable.Range(0, 100).Select(i => new TopicInterceptor
        {
            Id = i.ToString(),
            Stars = i + 100,
            Title = "标题" + i,
            CreateTime = DateTime.Now.AddDays(i),
            UpdateTime = DateTime.Now.AddDays(i),
            CreateBy = i.ToString(),
            UpdateBy = i.ToString(),
            TenantId = i.ToString(),
        })).ExecuteAsync();

        await db.Insert(new InterceptorEntity { Id = "1", Name = "123" }).ExecuteAsync();
        await db.Insert(new Interceptor2Entity { Id = "3", Aid = "1", Name = "456" }).ExecuteAsync();
    }
}

/// <summary>
/// 用 MySQL 固件的测试基类（对应 Java 的 BaseTest 子类）。<see cref="Db"/> 对应 Java 的 easyEntityQuery。
/// </summary>
public abstract class MySqlTest(MySqlFixture fixture)
{
    /// <summary>连到 <c>easy-query-test</c> 的 EasyDb。</summary>
    protected EasyDb Db => fixture.Db;

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}
