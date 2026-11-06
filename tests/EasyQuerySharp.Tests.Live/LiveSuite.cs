using EasyQuerySharp.Dialects;
using EasyQuerySharp.TestKit.Databases;

namespace EasyQuerySharp.Tests.Live;

/// <summary>
/// 某个库不支持的能力：对应的用例跳过（与 TS 版 liveSuite 的选项同名）。
/// </summary>
public sealed record LiveSuiteOptions
{
    /// <summary>行锁要两条连接同时持锁：SQLite 只有库级锁，没有行锁。</summary>
    public bool RowLocks { get; init; } = true;

    /// <summary>GROUP BY ROLLUP（MySQL 系写成 WITH ROLLUP）：SQLite 没有。</summary>
    public bool Rollup { get; init; } = true;

    /// <summary>GROUP BY CUBE：SQLite、MySQL 系没有。</summary>
    public bool Cube { get; init; } = true;

    /// <summary>GROUPING() 函数：SQLite、MariaDB 没有。</summary>
    public bool GroupingFunction { get; init; } = true;

    /// <summary>窗口函数 NTH_VALUE：SQL Server 没有。</summary>
    public bool NthValue { get; init; } = true;

    /// <summary>LPAD / RPAD：SQLite 没有这两个函数，SQL Server 也没有；方言没有给出替代写法。</summary>
    public bool PadFunctions { get; init; } = true;

    /// <summary>TRUNC(x, n) 截断小数：SQLite 的 trunc 只有一个参数。</summary>
    public bool TruncateWithScale { get; init; } = true;

    /// <summary>MaxColumns / MinColumns 忽略 NULL：SQLite 的 MAX(a, b) 与 MySQL 系的 GREATEST 碰到 NULL 就返回 NULL。</summary>
    public bool ExtremesIgnoreNull { get; init; } = true;

    /// <summary>MariaDB 11.4 的 bug：ROW_NUMBER()（倒序）、COUNT(*) OVER () 与 LAG() 在同一条语句里时 LAG 算错。</summary>
    public bool MixedWindowLag { get; init; } = true;

    /// <summary>
    /// Sql.Json 函数用在模型的 JSON 列上（统一是 VARCHAR）：PostgreSQL 的 -&gt; / JSONB_EXISTS 要求 json / jsonb 列（与 Java 一样不加转换）；
    /// SQL Server、Oracle（及沿用它的达梦）方言还没有 Sql.Json 的写法（Java 也没有）；openGauss 同 PostgreSQL。
    /// </summary>
    public bool JsonFunctions { get; init; } = true;

    /// <summary>递归 CTE（树查询 AsTreeCte）：SAP HANA 2.0 没有 WITH RECURSIVE。</summary>
    public bool RecursiveCte { get; init; } = true;

    /// <summary>分库用例要第二个库（<see cref="LiveSuite.ShardConnectionStringAsync"/>）：Oracle 的测试用户没有建第二个 schema 的权限。</summary>
    public bool ShardDataSources { get; init; } = true;
}

/// <summary>
/// L3 跨方言共用套件（对应 TS 版的 liveSuite）：同一组用例在每个数据库上跑，每个方言一个薄子类，只提供库与方言。
/// 每个用例用 code-first 重建 <see cref="Tables"/> 并写入同样的种子数据，所以用例之间互不影响；没有配置的库整类跳过。
/// 用例按主题放在 <c>LiveSuite.*.cs</c>。
/// </summary>
public abstract partial class LiveSuite
{
    // 删表顺序：子表在前
    private static readonly Type[] Tables =
    [
        typeof(MemberNote), typeof(Member), typeof(Dept), typeof(UserRole), typeof(Role),
        typeof(Profile), typeof(Setting), typeof(Note), typeof(Comment), typeof(Post), typeof(User), typeof(Company),
    ];

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Data.Common.DbDataSource> DataSources = new();

    /// <summary>种子数据里用户的创建时间（毫秒精度，各库都要原样读回）。</summary>
    protected static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, 6);

    /// <summary>要连的库。</summary>
    protected abstract TestDatabase Database { get; }

    /// <summary>方言。</summary>
    protected abstract SqlDialect Dialect { get; }

    /// <summary>这个库不支持的能力。</summary>
    protected virtual LiveSuiteOptions Options { get; } = new();

    /// <summary>连接串；需要独立库的方言（MySQL、PG）覆盖成先建库再连 <c>eqs_live</c>。</summary>
    protected virtual Task<string> ConnectionStringAsync() => Task.FromResult(Database.ConnectionString());

    /// <summary>分库用例的第二个库（<c>eqs_live_ds1</c> 或另一个文件库）的连接串；<see cref="LiveSuiteOptions.ShardDataSources"/> 为 false 的库不用实现。</summary>
    protected internal virtual Task<string> ShardConnectionStringAsync() => throw new NotSupportedException($"{Database} 没有第二个库");

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>重建表、写入种子数据，返回连到这个库的 EasyDb（带 <see cref="SqlCapture"/> 监听）。</summary>
    protected async Task<EasyDb> SeededAsync(Action<EasyDbOptions>? configure = null)
    {
        var connectionString = await ConnectionStringAsync();
        // 同一个连接串共用一个数据源（连接池）：每个用例各建一个池会把服务器的连接数占满（PG 的 too many clients）
        var dataSource = DataSources.GetOrAdd(connectionString, cs => Database.Factory.CreateDataSource(cs));
        var db = new EasyDb(o =>
        {
            o.Use(Dialect, dataSource).AddListener(SqlCapture.Instance);
            configure?.Invoke(o);
        });
        await db.CodeFirst.DropTablesIfExistsAsync(Tables, Ct);
        await db.CodeFirst.CreateTablesAsync(Tables.Reverse(), Ct);
        await SeedAsync(db);
        return db;
    }

    /// <summary>不支持的能力：跳过当前用例。</summary>
    protected static void SkipUnless(bool supported, string capability)
    {
        if (!supported) Assert.Skip($"这个库不支持{capability}");
    }

    // 与 TS 版 fixture 的 seed 相同：两家公司、三个用户（carol 没有公司）、四个帖子、两条评论
    private static async Task SeedAsync(EasyDb db)
    {
        var acme = new Company { Name = "acme" };
        await db.Insert(acme, new Company { Name = "globex" }).FillGeneratedKeys().ExecuteAsync(Ct);
        var alice = new User { Name = "alice", Age = 30, CompanyId = acme.Id, Attrs = new UserAttrs(["vip"]), CreatedAt = CreatedAt };
        var bob = new User { Name = "bob", Age = null, CompanyId = acme.Id, Attrs = new UserAttrs([]), CreatedAt = CreatedAt };
        var carol = new User { Name = "carol", Age = 17, CompanyId = null, Attrs = new UserAttrs([]), CreatedAt = CreatedAt };
        await db.Insert(alice, bob, carol).FillGeneratedKeys().ExecuteAsync(Ct);
        var tips = new Post { UserId = alice.Id, Title = "orm tips", Amount = 10.50m };
        await db.Insert(
            tips,
            new Post { UserId = alice.Id, Title = "sql 100%", Amount = 2000.00m },
            new Post { UserId = bob.Id, Title = "orm_v2", Amount = 1.25m },
            new Post { UserId = bob.Id, Title = "hello", Amount = 3.00m }).FillGeneratedKeys().ExecuteAsync(Ct);
        await db.Insert(new Comment { PostId = tips.Id, Body = "nice" }, new Comment { PostId = tips.Id, Body = "thanks" }).ExecuteAsync(Ct);
    }
}
