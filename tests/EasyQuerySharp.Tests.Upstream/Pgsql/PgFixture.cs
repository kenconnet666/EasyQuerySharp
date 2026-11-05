using EasyQuerySharp.TestKit.Databases;
using Npgsql;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.Pgsql.PgFixture))]

namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>
/// 对应 Java 的 pgsql/PgSQLBaseTest：每次测试运行重建 PostgreSQL 库 <c>easy-query-test</c>，code-first 建表后写入同样的种子数据。
/// 只建已移植用例用到的表，移植新的 pgsql 用例时在 <see cref="Tables"/> 里追加。没有配置 PostgreSQL 时用到 <see cref="Db"/> 的测试被跳过。
/// </summary>
public sealed class PgFixture : IAsyncLifetime
{
    /// <summary>Java 用例使用的库名。</summary>
    public const string Database = "easy-query-test";

    private static readonly Type[] Tables = [typeof(BlogEntity), typeof(DocBank), typeof(DocBankCard), typeof(DocUser), typeof(MyCategory), typeof(SysUser)];

    private EasyDb? _db;
    private string? _skipReason;

    /// <summary>连到本固件库的 EasyDb，带 <see cref="SqlCapture"/> 监听；没有配置 PostgreSQL 时跳过当前测试。</summary>
    public EasyDb Db
    {
        get
        {
            if (_db is null) Assert.Skip(_skipReason ?? "PostgreSQL 固件没有初始化");
            return _db;
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var serverCs = TestDatabases.PostgreSql.TryConnectionString();
        if (serverCs is null)
        {
            _skipReason = TestDatabases.PostgreSql.SkipReason;
            return;
        }
        await using (var server = new NpgsqlConnection(serverCs))
        {
            await server.OpenAsync();
            // WITH (FORCE)：上次运行残留的连接不妨碍删库（PG 13 起）
            await SqlScript.ExecuteAsync(server, $"DROP DATABASE IF EXISTS \"{Database}\" WITH (FORCE); CREATE DATABASE \"{Database}\";");
        }

        _db = new EasyDb(o => o.UsePostgreSql(NpgsqlDataSource.Create(TestDatabases.PostgreSql.TryConnectionString(Database)!)).AddListener(SqlCapture.Instance)
            .AddInterceptor(new MyCategoryInterceptor()));
        // SysUser 的表在 easy-query-test schema 下（Java 的 code-first 一并建 schema）
        await _db.ExecuteSqlAsync("CREATE SCHEMA IF NOT EXISTS \"easy-query-test\"");
        await _db.CodeFirst.CreateTablesAsync(Tables);
        await SeedAsync(_db);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // 与 Java PgSQLBaseTest 的 initData / testBeforex 相同的数据
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
            new DocBankCard { Id = "小明工商", Type = "储蓄卡", Uid = "小明id", BankId = "工商银行" },
            new DocBankCard { Id = "小红建设", Type = "储蓄卡", Uid = "小红id", BankId = "建设银行" },
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
        // 与 Java 一致只有 id 的用户，对应标题为 title0 的博客（BlogEntity.Users 按 title 关联）
        await db.Insert(new SysUser { Id = "title0" }).ExecuteAsync();
    }
}

/// <summary>用 PostgreSQL 固件的测试基类（对应 Java 的 PgSQLBaseTest 子类）。</summary>
public abstract class PgTest(PgFixture fixture)
{
    /// <summary>连到本固件库的 EasyDb（对应 Java 的 entityQuery）。</summary>
    protected EasyDb Db => fixture.Db;

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}

/// <summary>对应 Java：entity/MyCategoryInterceptor（默认关闭，名字 aaa；启用时分类的查询追加 1=1）。</summary>
public sealed class MyCategoryInterceptor : Interceptors.QueryFilter<MyCategory>
{
    public override string Name => "aaa";

    public override bool EnabledByDefault => false;

    public override System.Linq.Expressions.Expression<Func<MyCategory, bool>>? Filter() => _ => Sql.Raw<bool>("1=1");
}
