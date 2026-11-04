using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest12.java（排序与 NULL 位置、any、连接后整表投影、分组派生表、聚合运算、原生函数里嵌子查询）。</summary>
public sealed partial class QueryTest12(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns =
        "`id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top`";

    private static string Prefixed(string alias, string columns) => alias + "." + columns.Replace(",`", "," + alias + ".`");

    private const string TopicSelect = "SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`";

    // Java 的 asc(OrderByModeEnum.NULLS_LAST)：C# 的 Sql.NullsLast(..)；asc(false) 是不生效的排序
    [Fact, Upstream("QueryTest12.java#orderTest1")]
    public async Task OrderTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().OrderBy(t => t.Id).ToListAsync(Ct);
        listen.AssertSql(TopicSelect + " ORDER BY `id` ASC");
        await Db.Query<Topic>().OrderBy(t => t.Id).OrderByDescending(t => t.CreateTime).ToListAsync(Ct);
        listen.AssertSql(TopicSelect + " ORDER BY `id` ASC,`create_time` DESC");
        await Db.Query<Topic>().OrderByIf(false, t => t.Id).OrderByDescending(t => t.CreateTime).ToListAsync(Ct);
        listen.AssertSql(TopicSelect + " ORDER BY `create_time` DESC");
        await Db.Query<Topic>().OrderByDescending(t => t.CreateTime!.Value.ToString("yyyy-MM-dd")).ToListAsync(Ct);
        listen.AssertSql(TopicSelect + " ORDER BY DATE_FORMAT(`create_time`,'%Y-%m-%d') DESC");
        await Db.Query<Topic>().OrderBy(t => Sql.NullsLast(t.Id)).OrderByDescending(t => Sql.NullsFirst(t.CreateTime)).ToListAsync(Ct);
        listen.AssertSql(TopicSelect + " ORDER BY CASE WHEN `id` IS NULL THEN 1 ELSE 0 END ASC,`id` ASC,CASE WHEN `create_time` IS NULL THEN 0 ELSE 1 END ASC,`create_time` DESC");
    }

    [Fact, Upstream("QueryTest12.java#testAny1")]
    public async Task TestAny1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title == "1").OrderBy(t => Sql.NullsLast(t.Id)).OrderByDescending(t => Sql.NullsFirst(t.CreateTime)).AnyAsync(Ct);
        listen.AssertSql("SELECT  1  FROM `t_topic` WHERE `title` = ? ORDER BY CASE WHEN `id` IS NULL THEN 1 ELSE 0 END ASC,`id` ASC,CASE WHEN `create_time` IS NULL THEN 0 ELSE 1 END ASC,`create_time` DESC LIMIT 1");
        listen.AssertParams("1(String)");
    }

    [Fact, Upstream("QueryTest12.java#testJoinSelect")]
    public async Task TestJoinSelect()
    {
        using var listen = SqlCapture.Listen();
        var joined = Db.Query<Topic>().LeftJoin<BlogEntity>((topic, blog) => topic.Id == blog.Id).Where((topic, blog) => topic.Id == "111");
        const string tail = " FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?";
        await joined.Select((topic, blog) => blog).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t1", BlogColumns) + tail);
        listen.AssertParams("false(Boolean),111(String)");
        await joined.Select((topic, blog) => topic).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`" + tail);
        await joined.Select((topic, blog) => new Topic { Id = topic.Id, Title = topic.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title`" + tail);
        Assert.NotEmpty(await Db.Query<Topic>().Select(o => new Draft<string?>(o.Id)).ToListAsync(Ct));
        Assert.NotEmpty(await Db.Query<Topic>().Select(o => o.Id).ToListAsync(Ct));
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(o => o.CreateTime).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest12.java#testDraft21")]
    public async Task TestDraft21()
    {
        var grouped = Db.Query<BlogEntity>().GroupBy(o => o.Content!.Substring(0, 8))
            .Select(g => new Draft<string?, string>(g.Key, g.StringJoin(e => e.Id, ",")));
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().LeftJoin(grouped, (a, b) => a.Id == b.Value1).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t LEFT JOIN (SELECT SUBSTR(t1.`content`,1,8) AS `value1`,GROUP_CONCAT(t1.`id` SEPARATOR ?) AS `value2` FROM `t_blog` t1 WHERE t1.`deleted` = ? GROUP BY SUBSTR(t1.`content`,1,8)) t3 ON t.`id` = t3.`value1` WHERE t.`deleted` = ?");
        listen.AssertParams(",(String),false(Boolean),false(Boolean)");
    }

    [Fact, Upstream("QueryTest12.java#testDraft23")]
    public async Task TestDraft23()
    {
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(o => new Draft<DateTime?>(o.CreateTime)).ToListAsync(Ct));
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(o => new Draft<string?, DateTime?>(o.Id, o.CreateTime)).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest12.java#testOr26")]
    public async Task TestOr26()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Id == "123" || b.CreateTime <= new DateTime(2021, 1, 1, 1, 1, 0)).ToListAsync(Ct);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND (`id` = ? OR `create_time` <= ?)");
        listen.AssertParams("false(Boolean),123(String),2021-01-01T01:01(LocalDateTime)");
    }

    [Fact, Upstream("QueryTest12.java#testNum1")]
    public async Task TestNum1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new BlogEntity { Score = Sql.Raw<decimal?>("SUM({0})-SUM({1})", b.Score, b.Score) }).ToListAsync(Ct);
        listen.AssertSql("SELECT SUM(t.`score`)-SUM(t.`score`) AS `score` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    [Fact, Upstream("QueryTest12.java#testNum2"), Upstream("QueryTest12.java#testNum3")]
    public async Task TestNum2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new BlogEntity { Score = Sql.Sum(b.Score) - Sql.Sum(b.Score) }).ToListAsync(Ct);
        listen.AssertSql("SELECT (SUM(t.`score`) - SUM(t.`score`)) AS `score` FROM `t_blog` t WHERE t.`deleted` = ?");
        await Db.Query<BlogEntity>().Select(b => Sql.Sum(b.Score) - Sql.Sum(b.Score)).ToListAsync(Ct);
        listen.AssertSql("SELECT (SUM(t.`score`) - SUM(t.`score`)) FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("QueryTest12.java#testNum4"), Upstream("QueryTest12.java#testNum5")]
    public async Task TestNum4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => b.Star * b.Score + b.Score).ToListAsync(Ct);
        listen.AssertSql("SELECT ((t.`star` * t.`score`) + t.`score`) FROM `t_blog` t WHERE t.`deleted` = ?");
        await Db.Query<BlogEntity>().Select(b => b.Star * b.Score + 100).ToListAsync(Ct);
        listen.AssertSql("SELECT ((t.`star` * t.`score`) + ?) FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    [Fact, Upstream("QueryTest12.java#testNum6", Deviation = "参数类型：C# 的 decimal 运算里常量 100 是 decimal，Java 是 Integer")]
    public async Task TestNum6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => Math.Floor(b.Score!.Value) == 0m).Select(b => b.Star * b.Score + 100).ToListAsync(Ct);
        listen.AssertSql("SELECT ((t.`star` * t.`score`) + ?) FROM `t_blog` t WHERE t.`deleted` = ? AND FLOOR(t.`score`) = ?");
        listen.AssertParams("100(BigDecimal),false(Boolean),0(BigDecimal)");
    }

    // FIND_IN_SET 的第二个参数是标量子查询：Sql.Raw 里放 Sql.SubQuery(..)
    [Fact, Upstream("QueryTest12.java#testNum7")]
    public async Task TestNum7()
    {
        var titles = Db.Query<BlogEntity>().Where(b => Math.Floor(b.Score!.Value) == 0m).Select(b => Sql.Raw<string>("GROUP_CONCAT({0})", b.Title));
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => Sql.Raw<bool>("FIND_IN_SET({0},{1})", b.Title, Sql.SubQuery(titles))).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t WHERE t.`deleted` = ? AND FIND_IN_SET(t.`title`,(SELECT GROUP_CONCAT(t1.`title`) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND FLOOR(t1.`score`) = ?))");
        listen.AssertParams("false(Boolean),false(Boolean),0(BigDecimal)");
    }

    [Fact, Upstream("QueryTest12.java#testNum8")]
    public async Task TestNum8()
    {
        var titles = Db.Query<BlogEntity>().Where(b => Math.Floor(b.Score!.Value) == 0m).Select(b => Sql.StringJoin(b.Title, ","));
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => Sql.Raw<bool>("FIND_IN_SET({0},{1})", b.Title, Sql.SubQuery(titles))).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t WHERE t.`deleted` = ? AND FIND_IN_SET(t.`title`,(SELECT GROUP_CONCAT(t1.`title` SEPARATOR ?) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND FLOOR(t1.`score`) = ?))");
        listen.AssertParams("false(Boolean),,(String),false(Boolean),0(BigDecimal)");
    }

    [Fact, Upstream("QueryTest12.java#testOrSub1")]
    public async Task TestOrSub1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b =>
            Sql.Raw<bool>("FIND_IN_SET({0},{1})", b.Title, Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == "1" && x.Title == "2").Select(x => Sql.StringJoin(x.Title, ","))))
            || b.Score > 1m).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t WHERE t.`deleted` = ? AND (FIND_IN_SET(t.`title`,(SELECT GROUP_CONCAT(t1.`title` SEPARATOR ?) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ? AND t1.`title` = ?)) OR t.`score` > ?)");
        listen.AssertParams("false(Boolean),,(String),false(Boolean),1(String),2(String),1(BigDecimal)");
    }

    // Java 的 tableLogicDelete(() -> false) 作用于最近一个表来源：C# 的 DisableTableLogicDelete()
    [Fact, Upstream("QueryTest12.java#testLogicDeleteRecentlyTable")]
    public async Task TestLogicDeleteRecentlyTable()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().LeftJoin<BlogEntity>((b, b2) => b.Id == b2.Id).DisableTableLogicDelete().Where((b1, b2) => b1.Title!.Contains("123")).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t LEFT JOIN `t_blog` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t.`title` LIKE ?");
    }

    [Fact, Upstream("QueryTest12.java#testLogicDeleteRecentlyTable1")]
    public async Task TestLogicDeleteRecentlyTable1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().DisableTableLogicDelete().LeftJoin<BlogEntity>((b, b2) => b.Id == b2.Id).Where((b1, b2) => b1.Title!.Contains("123")).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`title` LIKE ?");
    }

    [Fact, Upstream("QueryTest12.java#testSelectPropTypeColumn1")]
    public async Task TestSelectPropTypeColumn1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().GroupBy(b => b.Id)
            .Select(g => new BlogEntity { Star = g.Sum(e => Sql.Raw<int?>("case {0} when {1} then 1 else 0 end", e.Score, 1)) }).ToListAsync(Ct);
        listen.AssertSql("SELECT SUM(case t.`score` when ? then 1 else 0 end) AS `star` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`");
    }

    [Fact, Upstream("QueryTest12.java#testSelectPropTypeColumn2")]
    public async Task TestSelectPropTypeColumn2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().GroupBy(b => b.Id)
            .Select(g => new BlogEntity { Score = g.Min(e => Sql.Raw<decimal?>("case {0} when {1} then 1 else 0 end", e.Score, 1)) }).ToListAsync(Ct);
        listen.AssertSql("SELECT MIN(case t.`score` when ? then 1 else 0 end) AS `score` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`");
    }

    // Java 在 eq(表达式) 时多写了一个空格（AND  t.`title`），C# 是单个空格
    [Theory, Upstream("QueryTest12.java#testSelectPropTypeColumn3", Deviation = "Java 的 WHERE 里多一个空格（AND  t.`title` = SUBSTR(..)），C# 是单个空格"),
     Upstream("QueryTest12.java#testSelectPropTypeColumn4", Deviation = "同 testSelectPropTypeColumn3"),
     Upstream("QueryTest12.java#testSelectPropTypeColumn5", Deviation = "同 testSelectPropTypeColumn3"),
     Upstream("QueryTest12.java#testSelectPropTypeColumn6")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TestSelectPropTypeColumn3(int variant)
    {
        var query = variant switch
        {
            1 => Db.Query<BlogEntity>().Where(b => b.Title == b.Title!.Substring(1, 2)),
            2 => Db.Query<BlogEntity>().Where(b => b.Title == (b.Title == b.Id ? "1" : "2")),
            _ => Db.Query<BlogEntity>().Where(b => (b.Title == b.Id ? "1" : "2") == b.Title),
        };
        using var listen = SqlCapture.Listen();
        await query.Select(b => new BlogEntity { Score = b.Id == "123" ? 1m : 2m }).ToListAsync(Ct);
        var where = variant switch
        {
            1 => "t.`title` = SUBSTR(t.`title`,2,2)",
            2 => "t.`title` = (CASE WHEN t.`title` = t.`id` THEN ? ELSE ? END)",
            _ => "(CASE WHEN t.`title` = t.`id` THEN ? ELSE ? END) = t.`title`",
        };
        listen.AssertSql("SELECT (CASE WHEN t.`id` = ? THEN ? ELSE ? END) AS `score` FROM `t_blog` t WHERE t.`deleted` = ? AND " + where);
    }
}
