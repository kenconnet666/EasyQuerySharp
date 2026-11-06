using System.Data.Common;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.TestKit.Databases;

namespace EasyQuerySharp.Tests.Live;

/// <summary>
/// ClickHouse 实库冒烟（对应 TS 版的 analyticSuite）：共用套件的种子靠自增主键、批量写入靠事务，ClickHouse 都没有，
/// 这里用主键由应用给值的小模型测查询、导航 join、聚合、窗口、函数、mutation 更新删除与 code-first。
/// 连接串追加两个会话设置：<c>join_use_nulls=1</c>（LEFT JOIN 没匹配上的列是 NULL 而不是类型默认值）、
/// <c>mutations_sync=2</c>（ALTER TABLE … UPDATE / DELETE 等做完再返回，紧接着的查询才看得到）。
/// </summary>
public sealed class ClickHouseLiveSuite
{
    private static readonly Type[] Tables = [typeof(ChPost), typeof(ChUser), typeof(ChCompany), typeof(ChMetric)];
    private static readonly Lazy<DbDataSource> DataSource = new(() =>
        TestDatabases.ClickHouse.Factory.CreateDataSource(TestDatabases.ClickHouse.ConnectionString() + ";set_join_use_nulls=1;set_mutations_sync=2;set_session_timezone=UTC"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<EasyDb> SeededAsync()
    {
        _ = TestDatabases.ClickHouse.ConnectionString();   // 没有配置时在这里跳过
        var db = new EasyDb(o => o.UseClickHouse(DataSource.Value).AddListener(SqlCapture.Instance));
        await db.CodeFirst.DropTablesIfExistsAsync(Tables, Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(ChCompany), typeof(ChUser), typeof(ChPost)], Ct);
        await db.Insert(new ChCompany { Id = 1, Name = "acme" }).ExecuteAsync(Ct);
        // 多行插入：没有事务，逐条执行
        await db.Insert(
            new ChUser { Id = 1, Name = "alice", Age = 30, CompanyId = 1, Attrs = """{"tags":["vip","new"]}""", CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, 6) },
            new ChUser { Id = 2, Name = "bob", Age = null, CompanyId = null, Attrs = """{"tags":[]}""", CreatedAt = new DateTime(2026, 2, 3) },
            new ChUser { Id = 3, Name = "张三", Age = 17, CompanyId = 1, Attrs = """{"tags":["vip"]}""", CreatedAt = new DateTime(2026, 3, 4, 12, 30, 0) }).ExecuteAsync(Ct);
        await db.Insert(
            new ChPost { Id = 10, UserId = 1, Title = "orm tips", Amount = 12.50m },
            new ChPost { Id = 11, UserId = 1, Title = "sql 100%", Amount = 7.25m },
            new ChPost { Id = 12, UserId = 3, Title = "中文标题", Amount = 1.00m }).ExecuteAsync(Ct);
        return db;
    }

    [Fact]
    public async Task FiltersOrdersPagesAndCounts()
    {
        var db = await SeededAsync();
        var query = db.Query<ChUser>().Where(u => u.CreatedAt >= new DateTime(2026, 1, 15) && !u.Name.Contains("ali")).OrderByDescending(u => u.Id);
        Assert.Equal(["张三", "bob"], await query.Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(2, await query.CountAsync(Ct));
        var page = await db.Query<ChUser>().OrderBy(u => u.Id).ToPageAsync(2, 2, Ct);
        Assert.Equal([3L], page.Rows.Select(u => u.Id));
        Assert.Equal(3, page.Total);
        // 值里的 % 按字面匹配
        Assert.Equal(["sql 100%"], await db.Query<ChPost>().Where(p => p.Title.EndsWith("%")).Select(p => p.Title).ToListAsync(Ct));
    }

    [Fact]
    public async Task JoinsNavigationsAggregatesSubqueriesAndWindows()
    {
        var db = await SeededAsync();
        Assert.Equal(["alice", "alice", "张三"], await db.Query<ChPost>().OrderBy(p => p.Id).Select(p => p.User!.Name).ToListAsync(Ct));

        var totals = await db.Query<ChPost>().GroupBy(p => p.UserId).OrderBy(g => g.Key)
            .Select(g => new Draft<long, decimal, string>(g.Key, g.Sum(p => p.Amount), g.StringJoin(p => p.Title, "|")))
            .ToListAsync(Ct);
        Assert.Equal([(1L, 19.75m), (3L, 1.00m)], totals.Select(t => (t.Value1, t.Value2)));
        Assert.Equal(["orm tips", "sql 100%"], totals[0].Value3.Split('|').Order(StringComparer.Ordinal));

        var withPosts = await db.Query<ChUser>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
            .Select(u => new Draft<string, long, long>(u.Name, u.Posts!.Count(), Sql.Over.RowNumber(w => w.OrderByDescending(u.Id))))
            .ToListAsync(Ct);
        Assert.Equal([("alice", 2L, 2L), ("张三", 1L, 1L)], withPosts.Select(u => (u.Value1, u.Value2, u.Value3)));

        // LEFT JOIN 没匹配上的列是 NULL（join_use_nulls = 1）
        Assert.Equal(["acme", null, "acme"], await db.Query<ChUser>().OrderBy(u => u.Id).Select(u => (string?)u.Company!.Name).ToListAsync(Ct));
    }

    [Fact]
    public async Task RunsStringDateJsonAndConditionFunctions()
    {
        var db = await SeededAsync();
        var row = await db.Query<ChUser>().Where(u => u.Id == 1)
            .Select(u => new ChFunctions(
                u.Name.ToUpper(), u.Name.Length, u.Name.Substring(1, 3), u.Name.IndexOf("ice"),
                u.CreatedAt.Year, u.CreatedAt.DateTrunc(DateTruncUnit.Month), u.CreatedAt.AddDays(1), u.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Sql.Json(u.Attrs).Get("tags").Length(), Sql.Json(u.Attrs).Get("tags").Text(0), Sql.Json(u.Attrs).ContainsKey("tags"),
                u.Age >= 18))
            .SingleAsync(Ct);
        Assert.Equal(new ChFunctions(
            "ALICE", 5, "lic", 2,
            2026, new DateTime(2026, 1, 1), new DateTime(2026, 1, 3, 3, 4, 5, 6), "2026-01-02 03:04",
            2, "vip", true,
            true), row);
        // 中文按字符计长、截取
        Assert.Equal(("张", 2), await db.Query<ChUser>().Where(u => u.Id == 3).Select(u => new Draft<string, int>(u.Name.Substring(0, 1), u.Name.Length)).SingleAsync(Ct) is var c ? (c.Value1, c.Value2) : default);
    }

    private sealed record ChFunctions(
        string Upper, int Length, string Sub, int Index,
        int Year, DateTime Month, DateTime Later, string Formatted,
        int Tags, string? First, bool HasTags,
        bool Adult);

    // 更新、删除是 mutation：ALTER TABLE … UPDATE / DELETE，不带别名；mutations_sync=2 让紧接着的查询看得到
    [Fact]
    public async Task UpdatesAndDeletesThroughMutations()
    {
        var db = await SeededAsync();
        using var listen = SqlCapture.Listen();
        await db.Update<ChUser>().Set(u => new ChUser { Name = "alicia", Age = u.Age + 1 }).Where(u => u.Id == 1).ExecuteAsync(Ct);
        Assert.StartsWith("ALTER TABLE `t_ch_user` UPDATE `name` = ", listen.Last.Statements[0].Text);
        await db.Delete<ChPost>().Where(p => p.UserId == 3).ExecuteAsync(Ct);
        Assert.Equal("ALTER TABLE `t_ch_post` DELETE WHERE `user_id` = @p0", listen.Last.Statements[0].CommandText);

        Assert.Equal(("alicia", 31), await db.Query<ChUser>().Where(u => u.Id == 1).Select(u => new Draft<string, int?>(u.Name, u.Age)).SingleAsync(Ct) is var a ? (a.Value1, a.Value2) : default);
        Assert.Equal([10L, 11L], await db.Query<ChPost>().OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(Ct));
        // 经导航的条件要连表，mutation 写不了
        Assert.Throws<NotSupportedException>(() => db.Delete<ChPost>().Where(p => p.User!.Name == "alice").ToSql());
    }

    [Fact]
    public async Task RejectsTransactionsAndUpserts()
    {
        var db = await SeededAsync();
        await using (db.BeginTransaction())
            await Assert.ThrowsAsync<NotSupportedException>(() => db.Query<ChUser>().CountAsync(Ct));
        Assert.Throws<NotSupportedException>(() => db.Insert(new ChCompany { Id = 1, Name = "x" }).OnConflictUpdate(c => new { c.Name }).ToSql());
    }

    // 建表、写入、读回；读库结构后没有要改的（注释、跳数索引、Nullable 列都读得回来）
    [Fact]
    public async Task CreatesTablesAndSyncsNothingAfterward()
    {
        var db = await SeededAsync();
        await db.CodeFirst.CreateTablesAsync([typeof(ChMetric)], Ct);
        var create = db.CodeFirst.CreateTableSql(typeof(ChMetric));
        Assert.True(create.StartsWith(
            "CREATE TABLE IF NOT EXISTS `t_ch_metric` (`id` Int64 NOT NULL, `name` String NULL, `value` Float64 NOT NULL, `ok` Bool NOT NULL, "
            + "`day` Date32 NOT NULL, `at` DateTime64(3) NOT NULL, `note` String NULL) ENGINE = MergeTree ORDER BY (`id`)", StringComparison.Ordinal), create);
        var metric = new ChMetric { Id = 1, Name = "cpu", Value = 0.5, Ok = true, Day = new DateOnly(2026, 1, 2), At = new DateTime(2026, 1, 2, 3, 4, 5, 6), Note = null };
        await db.Insert(metric).ExecuteAsync(Ct);
        var back = await db.Query<ChMetric>().SingleAsync(Ct);
        Assert.Equivalent(metric, back);
        var plan = await db.CodeFirst.PlanSyncAsync([typeof(ChMetric), typeof(ChUser), typeof(ChPost), typeof(ChCompany)], Ct);
        Assert.Empty(plan.Statements);
    }
}

/// <summary>ClickHouse 冒烟的公司。</summary>
[Table("t_ch_company")]
public class ChCompany
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";
}

/// <summary>ClickHouse 冒烟的用户：主键由应用给值；<see cref="Attrs"/> 是 JSON 文本。</summary>
[Table("t_ch_user")]
public class ChUser
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public int? Age { get; set; }

    public long? CompanyId { get; set; }

    public string Attrs { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public ChCompany? Company { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ChPost.UserId)])]
    public List<ChPost>? Posts { get; set; }
}

/// <summary>ClickHouse 冒烟的帖子。</summary>
[Table("t_ch_post")]
public class ChPost
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Title { get; set; } = "";

    [Column(DbType = "Decimal(12, 2)")]
    public decimal Amount { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(UserId)])]
    public ChUser? User { get; set; }
}

/// <summary>code-first 往返：各种列类型、注释、跳数索引。</summary>
[Table("t_ch_metric", Comment = "指标"), TableIndex(nameof(Day), Name = "ix_ch_metric_day")]
public class ChMetric
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    [Column(Comment = "指标名")]
    public string Name { get; set; } = "";

    public double Value { get; set; }

    public bool Ok { get; set; }

    public DateOnly Day { get; set; }

    public DateTime At { get; set; }

    public string? Note { get; set; }
}
