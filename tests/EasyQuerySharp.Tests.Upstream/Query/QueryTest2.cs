using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest2.java（多表聚合、分组、LIKE 变体、逗号连接）。whereObject / orderByObject 的用例见 FilterBy。</summary>
public sealed class QueryTest2(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns =
        "`id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top`";

    private const string TopicLeftBlogLeftBlog =
        "FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LEFT JOIN `t_blog` t2 ON t2.`deleted` = ? AND t.`id` = t2.`id`";

    // Java 的 select(LocalDateTime.class, x -> x.columnMax("createTime")) 给聚合列加了别名；C# 的标量投影不加别名
    [Theory, Upstream("QueryTest2.java#query1231", Deviation = "标量子查询里 Java 写 MAX(..) AS `create_time`，C# 的标量投影不写别名"),
     Upstream("QueryTest2.java#query1231_1", Deviation = "同 query1231"), Upstream("QueryTest2.java#query1232", Deviation = "同 query1231")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Query1231(bool raw)
    {
        const string expected = "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE t.`create_time` = (SELECT MAX(t1.`create_time`) FROM `t_topic` t1)";
        var query = raw
            ? Db.Query<Topic>().Where(o => Sql.Raw<bool>("{0} = {1}", o.CreateTime, Sql.SubQuery(Db.Query<Topic>().Select(x => Sql.Max(x.CreateTime)))))
            : Db.Query<Topic>().Where(o => o.CreateTime == Sql.SubQuery(Db.Query<Topic>().Select(x => Sql.Max(x.CreateTime))));
        Assert.Equal(expected, query.ToSql());
        using var listen = SqlCapture.Listen();
        await query.ToListAsync(Ct);
        listen.AssertSql(expected);
        listen.AssertParams("");
    }

    [Fact, Upstream("QueryTest2.java#query124"), Upstream("QueryTest2.java#query124_1")]
    public void Query124() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` " + TopicLeftBlogLeftBlog + " WHERE t.`id` = ? LIMIT 2 OFFSET 1",
            Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).LeftJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id)
                .Where((t, t1, t2) => t.Id == "3").Limit(1, 2).ToSql());

    private Query<BlogEntity> Query125Base() => Db.Query<Topic>()
        .LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
        .InnerJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id)
        .Where((t, t1, t2) => t.Id == "3")
        .Select((t, t1, t2) => new BlogEntity { Id = t.Id, Title = t1.Title, Star = t2.Star });

    [Fact, Upstream("QueryTest2.java#query125")]
    public void Query125() =>
        Assert.Equal("SELECT t.`id`,t1.`title`,t2.`star` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` INNER JOIN `t_blog` t2 ON t2.`deleted` = ? AND t.`id` = t2.`id` WHERE t.`id` = ? LIMIT 2 OFFSET 1",
            Query125Base().Limit(1, 2).ToSql());

    [Fact, Upstream("QueryTest2.java#query126")]
    public async Task Query126()
    {
        Assert.Empty(await Query125Base().Limit(1, 2).ToListAsync(Ct));
        Assert.Single(await Query125Base().Take(1).ToListAsync(Ct));
    }

    private Query<Topic, BlogEntity, BlogEntity> TopicBlogBlog(string[] ids, bool innerSecond = false)
    {
        var q = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id);
        var q3 = innerSecond ? q.InnerJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id) : q.LeftJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id);
        return q3.Where((t, t1, t2) => ids.Contains(t.Id!));
    }

    // Java 的 xxxOrNull / xxxOrDefault / xxxMerge 在 C# 里都是同一个聚合方法，默认值用 ?? 表达
    [Fact, Upstream("QueryTest2.java#query112"), Upstream("QueryTest2.java#query113"), Upstream("QueryTest2.java#query114"), Upstream("QueryTest2.java#query115")]
    public async Task Query112()
    {
        Assert.Equal(1.2m, await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id == "3").SumAsync((t, t1) => t1.Score, Ct) ?? 0m);
        Assert.Null(await TopicBlogBlog(["3x"]).SumAsync((t, t1, t2) => t1.Score, Ct));
        Assert.Equal(1.2m, await TopicBlogBlog(["3"]).SumAsync((t, t1, t2) => t2.Score, Ct));
        Assert.Null(await TopicBlogBlog(["3x"]).SumAsync((t, t1, t2) => t2.Score, Ct));
        Assert.Equal(0m, await TopicBlogBlog(["3x"], innerSecond: true).SumAsync((t, t1, t2) => t1.Score, Ct) ?? 0m);
    }

    [Fact, Upstream("QueryTest2.java#query116"), Upstream("QueryTest2.java#query117"), Upstream("QueryTest2.java#query118"), Upstream("QueryTest2.java#query119")]
    public async Task Query116()
    {
        Assert.Null(await TopicBlogBlog(["3x"]).MaxAsync((t, t1, t2) => t2.Score, Ct));
        Assert.Equal(0m, await TopicBlogBlog(["3x"]).MaxAsync((t, t1, t2) => t1.Score, Ct) ?? 0m);
        Assert.Equal(1.2m, await TopicBlogBlog(["3x", "3"]).MaxAsync((t, t1, t2) => t2.Score, Ct));
        Assert.Null(await TopicBlogBlog(["3x"], innerSecond: true).MinAsync((t, t1, t2) => t1.Score, Ct));
        Assert.Equal(0m, await TopicBlogBlog(["3x"], innerSecond: true).MinAsync((t, t1, t2) => t2.Score, Ct) ?? 0m);
        Assert.Equal(1.2m, await TopicBlogBlog(["3x", "3"], innerSecond: true).MinAsync((t, t1, t2) => t2.Score, Ct));
    }

    [Fact, Upstream("QueryTest2.java#query120", Deviation = "Java 的 avgOrDefault(.., Float.class) 返回 float；C# 的平均值只有 double 与 decimal 两种")]
    public async Task Query120()
    {
        Assert.Equal(1.2m, await TopicBlogBlog(["3", "2", "5"], innerSecond: true).AverageDecimalAsync((t, t1, t2) => t1.Score, Ct));
        Assert.Null(await TopicBlogBlog(["3x", "2x", "5x"], innerSecond: true).AverageDecimalAsync((t, t1, t2) => t2.Score, Ct));
        Assert.Equal(1.2m, await TopicBlogBlog(["3x", "2x", "5"], innerSecond: true).AverageDecimalAsync((t, t1, t2) => t2.Score, Ct) ?? 0m);
        Assert.Equal(3.3333d, await TopicBlogBlog(["3", "2", "5"]).AverageAsync((t, t1, t2) => t1.Star, Ct));
        Assert.Null(await TopicBlogBlog(["3x", "2x", "5x"]).AverageAsync((t, t1, t2) => t1.Star, Ct));
        Assert.Equal(5d, await TopicBlogBlog(["3x", "2x", "5"]).AverageAsync((t, t1, t2) => t2.Star, Ct));
        Assert.Equal(5m, await TopicBlogBlog(["3x", "2x", "5"]).AverageDecimalAsync((t, t1, t2) => t2.Star, Ct));
    }

    [Fact, Upstream("QueryTest2.java#query121")]
    public void Query121()
    {
        string[] ids = ["3", "2", "5"];
        var head = "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` " + TopicLeftBlogLeftBlog + " WHERE t.`id` IN (?,?,?) ORDER BY ";
        Assert.Equal(head + "t1.`order` ASC", TopicBlogBlog(ids).OrderBy((t, t1, t2) => t1.Order).ToSql());
        Assert.Equal(head + "t2.`id` ASC", TopicBlogBlog(ids).OrderByIf(false, (t, t1, t2) => t1.Order).OrderByIf(true, (t, t1, t2) => t2.Id).ToSql());
        Assert.Equal(head + "t2.`order` DESC", TopicBlogBlog(ids).OrderByDescending((t, t1, t2) => t2.Order).ToSql());
        Assert.Equal(head + "t2.`id` DESC,t2.`order` DESC", TopicBlogBlog(ids).OrderByDescendingIf(false, (t, t1, t2) => t2.Order)
            .OrderByDescendingIf(true, (t, t1, t2) => t2.Id).OrderByDescendingIf(true, (t, t1, t2) => t2.Order).ToSql());
    }

    // Java 的 groupTable() 引用组内的列：C# 写 g.Column(e => e.ItemN.X)
    [Fact, Upstream("QueryTest2.java#query122")]
    public void Query122()
    {
        string[] ids = ["3", "2", "5"];
        Assert.Equal("SELECT t.`id` " + TopicLeftBlogLeftBlog + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t1.`order` ASC",
            TopicBlogBlog(ids).GroupBy((t, t1, t2) => t.Id).OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => g.Key).ToSql());
        Assert.Equal("SELECT t2.`id` " + TopicLeftBlogLeftBlog + " WHERE t.`id` IN (?,?,?) GROUP BY t2.`id` ORDER BY t2.`order` ASC",
            TopicBlogBlog(ids).GroupBy((t, t1, t2) => t2.Id).OrderBy(g => g.Column(e => e.Item3.Order)).Select(g => g.Key).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#query123")]
    public void Query123()
    {
        string[] ids = ["3", "2", "5"];
        const string body = "FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` INNER JOIN `t_blog` t2 ON t2.`deleted` = ? AND t.`id` = t2.`id` WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t2.`order` ASC";
        var grouped = TopicBlogBlog(ids, innerSecond: true).GroupBy((t, t1, t2) => t.Id).OrderBy(g => g.Column(e => e.Item3.Order));
        Assert.Equal("SELECT DISTINCT t.`id` " + body + " LIMIT 10", grouped.Select(g => g.Key).Distinct().Take(10).ToSql());
        Assert.Equal("SELECT COUNT(t3.`id`) FROM (SELECT DISTINCT t.`id` AS `id` " + body + ") t3",
            grouped.Select(g => new Topic { Id = g.Key }).Distinct().Select(o => Sql.Count(o.Id)).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#groupTest")]
    public void GroupTest() =>
        Assert.Equal("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `id_count`,SUM(t.`score`) AS `score_sum`,MAX(t.`publish_time`) AS `publish_time_max`,MIN(t.`order`) AS `order_min`,AVG(t.`status`) AS `status_avg` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            Db.Query<BlogEntity>().GroupBy(o => o.Id).Select(g => new BlogEntityGroup
            {
                Id = g.Key, IdCount = (int)g.Count(s => s.Id), ScoreSum = g.Sum(s => s.Score), PublishTimeMax = g.Max(s => s.PublishTime),
                OrderMin = g.Min(s => s.Order), StatusAvg = g.Average(s => s.Status),
            }).ToSql());

    [Fact, Upstream("QueryTest2.java#groupTest1")]
    public void GroupTest1() =>
        Assert.Equal("SELECT t.`id` AS `id`,SUM(t.`score`) AS `score`,MAX(t.`publish_time`) AS `publish_time`,MIN(t.`order`) AS `order`,AVG(t.`status`) AS `status` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            Db.Query<BlogEntity>().GroupBy(o => o.Id).Select(g => new BlogEntity
            {
                Id = g.Key, Score = g.Sum(s => s.Score), PublishTime = g.Max(s => s.PublishTime), Order = g.Min(s => s.Order), Status = g.Average(s => s.Status),
            }).ToSql());

    [Fact, Upstream("QueryTest2.java#groupTest2")]
    public void GroupTest2() =>
        Assert.Equal("SELECT t.`id` AS `id`,SUM(DISTINCT t.`score`) AS `score_sum`,COUNT(DISTINCT t.`id`) AS `id_count`,AVG(DISTINCT t.`status`) AS `status_avg` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            Db.Query<BlogEntity>().GroupBy(o => o.Id).Select(g => new BlogEntityGroup
            {
                Id = g.Key, ScoreSum = g.Distinct().Sum(s => s.Score), IdCount = (int)g.Distinct().Count(s => s.Id), StatusAvg = g.Distinct().Average(s => s.Status),
            }).ToSql());

    [Fact, Upstream("QueryTest2.java#groupTest3", Deviation = "Java 的 Select.of 可以放不映射到属性的列（没有别名）；C# 投影的每一列都有成员名，这里用 Draft 的 value1..")]
    public void GroupTest3() =>
        Assert.Equal("SELECT t.`id` AS `value1`,SUM(DISTINCT t.`score`) AS `value2`,COUNT(DISTINCT t.`order`) AS `value3`,AVG(DISTINCT t.`status`) AS `value4` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            Db.Query<BlogEntity>().GroupBy(o => o.Id)
                .Select(g => new Draft<string?, decimal?, long, int?>(g.Key, g.Distinct().Sum(s => s.Score), g.Distinct().Count(s => s.Order), g.Distinct().Average(s => s.Status)))
                .ToSql());

    [Fact, Upstream("QueryTest2.java#groupTest4")]
    public void GroupTest4() =>
        Assert.Equal("SELECT t.`id` AS `id`,SUM(DISTINCT t.`score`) AS `score`,COUNT(DISTINCT t.`order`) AS `order`,AVG(DISTINCT t.`status`) AS `status` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            Db.Query<BlogEntity>().GroupBy(o => o.Id).Select(g => new BlogEntity
            {
                Id = g.Key, Score = g.Distinct().Sum(s => s.Score), Order = g.Distinct().Count(s => s.Order), Status = g.Distinct().Average(s => s.Status),
            }).ToSql());

    private Query<BlogEntity> GroupTest5Query(decimal orderDefault, int statusDefault) =>
        Db.Query<BlogEntity>().GroupBy(o => o.Id).Select(g => new BlogEntity
        {
            Id = g.Key,
            Score = g.Distinct().Sum(s => s.Score) ?? 0m,
            Order = (decimal?)g.Distinct().Count(s => s.Order) ?? orderDefault,
            Status = g.Distinct().Average(s => s.Status) ?? statusDefault,
        });

    [Fact, Upstream("QueryTest2.java#groupTest5")]
    public void GroupTest5() =>
        Assert.Equal("SELECT t.`id` AS `id`,IFNULL(SUM(DISTINCT t.`score`),?) AS `score`,IFNULL(COUNT(DISTINCT t.`order`),?) AS `order`,IFNULL(AVG(DISTINCT t.`status`),?) AS `status` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            GroupTest5Query(0m, 0).ToSql());

    [Fact, Upstream("QueryTest2.java#groupTest5_1")]
    public async Task GroupTest5_1() => Assert.NotNull(await GroupTest5Query(1m, 3).FirstOrDefaultAsync(Ct));

    [Fact, Upstream("QueryTest2.java#groupTest9")]
    public void GroupTest9() =>
        Assert.Equal("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ? AND `id` <> ? AND `star` <> ?",
            Db.Query<BlogEntity>().Where(t => t.Id != "1").WhereIf(true, t => t.Star != 1).WhereIf(false, t => t.Title != "x").ToSql());

    // Java 还检查参数对象记录的属性名；C# 的参数只有值，这里核对 SQL 与参数值
    [Theory, Upstream("QueryTest2.java#groupTest10"), Upstream("QueryTest2.java#groupTest10_1"), Upstream("QueryTest2.java#groupTest11"),
     Upstream("QueryTest2.java#groupTest12"), Upstream("QueryTest2.java#groupTest13")]
    [InlineData("notStartsWith", "NOT LIKE", "id%", "content%")]
    [InlineData("contains", "LIKE", "%id%", "%content%")]
    [InlineData("notEndsWith", "NOT LIKE", "%id", "%content")]
    [InlineData("notContains", "NOT LIKE", "%id%", "%content%")]
    public void GroupTest10(string kind, string op, string idPattern, string contentPattern)
    {
        var query = kind switch
        {
            "notStartsWith" => Db.Query<BlogEntity>().Where(t => !t.Id!.StartsWith("id")).WhereIf(true, t => !t.Content!.StartsWith("content")).WhereIf(false, t => !t.Title!.StartsWith("title")),
            "contains" => Db.Query<BlogEntity>().Where(t => t.Id!.Contains("id")).WhereIf(true, t => t.Content!.Contains("content")).WhereIf(false, t => t.Title!.Contains("title")),
            "notEndsWith" => Db.Query<BlogEntity>().Where(t => !t.Id!.EndsWith("id")).WhereIf(true, t => !t.Content!.EndsWith("content")).WhereIf(false, t => !t.Title!.EndsWith("title")),
            _ => Db.Query<BlogEntity>().Where(t => !t.Id!.Contains("id")).WhereIf(true, t => !t.Content!.Contains("content")).WhereIf(false, t => !t.Title!.Contains("title")),
        };
        var statement = query.ToStatement();
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` {op} ? AND `content` {op} ?", statement.Text);
        Assert.Equal([false, idPattern, contentPattern], statement.Parameters.Select(p => p.Value));
    }

    [Fact, Upstream("QueryTest2.java#queryTest16")]
    public void QueryTest16()
    {
        var now = DateTime.Now;
        var statement = Db.Query<BlogEntity>().Where(t => t.Star < 9).WhereIf(true, t => t.PublishTime < now).WhereIf(false, t => t.Order < 4m).ToStatement();
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `star` < ? AND `publish_time` < ?", statement.Text);
        Assert.Equal([false, 9, now], statement.Parameters.Select(p => p.Value));
    }

    [Fact, Upstream("QueryTest2.java#query24")]
    public async Task Query24()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "1")
            .GroupBy(o => Sql.Raw<string>("RAND()"))
            .OrderBy(g => Sql.Raw<double>("RAND()"))
            .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(x => x.Id) });
        Assert.Equal("SELECT RAND() AS `id`,COUNT(t.`id`) AS `id_count` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY RAND() ORDER BY RAND() ASC", query.ToSql());
        Assert.Single(await query.ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest2.java#query122x")]
    public async Task Query122x()
    {
        string[] ids = ["3", "2", "5"];
        var grouped = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => ids.Contains(t.Id!)).GroupBy((t, t1) => t.Id);
        var list = await grouped.OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => new Topic { Id = g.Key }).ToListAsync(Ct);
        Assert.Equal(3, list.Count);
        Assert.All(list, t =>
        {
            Assert.NotNull(t.Id);
            Assert.Null(t.Stars);
            Assert.Null(t.Title);
        });
        const string expected = "SELECT t.`id` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` IN (?,?,?) GROUP BY t.`id` HAVING COUNT(t.`id`) >= ? ORDER BY t1.`order` ASC";
        Assert.Equal(expected, grouped.HavingIf(false, g => g.Count(e => e.Item2.Id) >= 1L).HavingIf(true, g => g.Count(e => e.Item1.Id) >= 1L)
            .OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => g.Key).ToSql());
        Assert.Equal(expected, grouped.HavingIf(false, g => g.Count(e => e.Item2.Id) >= 1L).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => g.Key).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#query123x")]
    public void Query123x()
    {
        string[] ids = ["3", "2", "5"];
        var grouped = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => ids.Contains(t.Id!)).GroupBy((t, t1) => t.Id);
        const string head = "SELECT DISTINCT t.`id` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY ";
        Assert.Equal(head + "t1.`order` ASC LIMIT 10", grouped.OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => g.Key).Distinct().Take(10).ToSql());
        Assert.Equal("SELECT count(1) FROM (" + head + "t1.`order` ASC) t2",
            grouped.OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => g.Key).Distinct().SelectRaw("count(1)").ToSql());
        Assert.Equal("SELECT count(1) FROM (" + head + "t.`stars` DESC) t2",
            grouped.OrderByDescending(g => g.Column(e => e.Item1.Stars)).Select(g => g.Key).Distinct().SelectRaw("count(1)").ToSql());
        Assert.Equal("SELECT count(1) FROM (" + head + "t.`stars` ASC) t2",
            grouped.OrderBy(g => g.Column(e => e.Item1.Stars)).Select(g => g.Key).Distinct().SelectRaw("count(1)").ToSql());
    }

    [Fact, Upstream("QueryTest2.java#query12x")]
    public async Task Query12x() =>
        Assert.Equal(20, (await Db.Query<Topic>().Where(o => o.CreateTime != null).Select<Topicx>().ToPageAsync(1, 20, Ct)).Rows.Count);

    // Java 里条件为 false 时值表达式仍会求值（phone.get 抛空指针）；C# 的 WhereIf 整个 lambda 不会求值
    [Theory, Upstream("QueryTest2.java#query13x"), Upstream("QueryTest2.java#query14x"), Upstream("QueryTest2.java#query143x")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Query13x(bool fromMap)
    {
        Dictionary<string, string>? phone = fromMap ? null : new() { ["phone"] = "133" };
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx")
            .WhereIf(!fromMap && false, o => o.Phone!.Contains("133"))
            .WhereIf(phone != null && phone.ContainsKey("phone") && fromMap, o => o.Phone!.Contains(phone!["phone"]));
        Assert.Equal("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE `id` = ?", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest2.java#queryMultiFrom1")]
    public void QueryMultiFrom1() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t , `t_topic` t1 WHERE t.`id` = ? AND t.`id` = ?",
            Db.Query<Topic>().From<Topic>().Where((a, b) => a.Id == "1").Where((a, b) => a.Id == "id").ToSql());

    // ---------- whereObject / orderByObject：C# 的 FilterBy / SortBy ----------

    private static BlogQueryRequest BaseRequest() => new()
    {
        Title = "123", Content = "123", Star = 123, PublishTimeBegin = DateTime.Now, PublishTimeEnd = DateTime.Now,
        Score = 123m, Status = 1, Order = 12m, IsTop = false, Orders = ["status", "score"],
    };

    private const string BlogTail = " AND `score` >= ? AND `status` <= ? AND `order` > ? AND `is_top` <> ? ORDER BY `status` ASC,`score` ASC";

    [Fact, Upstream("QueryTest2.java#queryTest17")]
    public async Task QueryTest17()
    {
        var request = BaseRequest();
        request.PublishTimeBegin = new DateTime(2000, 2, 1);
        request.PublishTimeEnd = new DateTime(2000, 3, 1);
        request.PublishTime = [new DateTime(2000, 1, 1), new DateTime(2001, 1, 1)];
        request.PublishTimes = [new DateTime(2003, 1, 1), new DateTime(2004, 1, 1)];
        request.PublishTimeOpen = [new DateTime(2005, 1, 1), new DateTime(2006, 1, 1)];
        request.PublishTimeOpenClosed = [new DateTime(2007, 1, 1), new DateTime(2008, 1, 1)];
        request.PublishTimeClosedOpen = [new DateTime(2009, 1, 1), new DateTime(2010, 1, 1)];
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().FilterBy(request).SortBy(request).ToListAsync(Ct);
        listen.AssertSql("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE ? AND `url` LIKE ? AND `star` = ? AND `publish_time` >= ? AND `publish_time` <= ? AND (`publish_time` >= ? AND `publish_time` <= ?) AND (`publish_time` >= ? AND `publish_time` <= ?) AND (`publish_time` > ? AND `publish_time` < ?) AND (`publish_time` > ? AND `publish_time` <= ?) AND (`publish_time` >= ? AND `publish_time` < ?)" + BlogTail);
        listen.AssertParams("false(Boolean),%123%(String),%123%(String),123(Integer),2000-02-01T00:00(LocalDateTime),2000-03-01T00:00(LocalDateTime),2000-01-01T00:00(LocalDateTime),2001-01-01T00:00(LocalDateTime),2003-01-01T00:00(LocalDateTime),2004-01-01T00:00(LocalDateTime),2005-01-01T00:00(LocalDateTime),2006-01-01T00:00(LocalDateTime),2007-01-01T00:00(LocalDateTime),2008-01-01T00:00(LocalDateTime),2009-01-01T00:00(LocalDateTime),2010-01-01T00:00(LocalDateTime),123(BigDecimal),1(Integer),12(BigDecimal),false(Boolean)");
    }

    [Fact, Upstream("QueryTest2.java#queryTest18")]
    public void QueryTest18()
    {
        var request = BaseRequest();
        Assert.Equal("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE ? AND `url` LIKE ? AND `star` = ? AND `publish_time` >= ? AND `publish_time` <= ?" + BlogTail,
            Db.Query<BlogEntity>().FilterByIf(true, request).SortByIf(true, request).ToSql());
    }

    // 多属性 OR 的条件挂在第一个属性的节点上，所以紧跟在 title 的条件之后
    [Fact, Upstream("QueryTest2.java#queryTest18_1")]
    public void QueryTest18_1()
    {
        var request = BaseRequest();
        request.Title2 = "123";
        Assert.Equal("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE ? AND (`title` LIKE ? OR `content` LIKE ?) AND `url` LIKE ? AND `star` = ? AND `publish_time` >= ? AND `publish_time` <= ?" + BlogTail,
            Db.Query<BlogEntity>().FilterBy(request).SortBy(request).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#queryTest18_2")]
    public void QueryTest18_2()
    {
        var request = BaseRequest();
        request.Title2 = "123";
        request.Title3 = "123";
        Assert.Equal("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE ? AND (`title` LIKE ? OR `content` LIKE ?) AND (`id` = ? OR `content` = ?) AND `url` LIKE ? AND `star` = ? AND `publish_time` >= ? AND `publish_time` <= ?" + BlogTail,
            Db.Query<BlogEntity>().FilterBy(request).SortBy(request).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#queryTest19")]
    public void QueryTest19()
    {
        var request = BaseRequest();
        Assert.Equal("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ?", Db.Query<BlogEntity>().FilterByIf(false, request).SortByIf(false, request).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#queryTest20")]
    public void QueryTest20()
    {
        var request = BaseRequest();
        request.StatusList.Add(1);
        request.StatusNotList.AddRange([2, 3]);
        Assert.Equal("SELECT " + BlogColumns + " FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE ? AND `url` LIKE ? AND `star` = ? AND `publish_time` >= ? AND `publish_time` <= ? AND `score` >= ? AND `status` <= ? AND `status` IN (?) AND `status` NOT IN (?,?) AND `order` > ? AND `is_top` <> ? ORDER BY `status` ASC,`score` ASC",
            Db.Query<BlogEntity>().FilterBy(request).SortBy(request).ToSql());
    }

    private Query<BlogEntity> SortedBlogs(Filtering.ISortRequest request) =>
        Db.Query<BlogEntity>().SortBy(request).Select(o => new BlogEntity { Id = o.Id, Title = o.Title, Content = o.Content });

    [Fact, Upstream("QueryTest2.java#dynamicSort")]
    public void DynamicSort()
    {
        const string head = "SELECT t.`id`,t.`title`,t.`content` FROM `t_blog` t WHERE t.`deleted` = ?";
        Assert.Equal(head + " ORDER BY t.`title` ASC", SortedBlogs(new BlogSortRequest { Sort = "title", Asc = true }).ToSql());
        Assert.Equal(head + " ORDER BY t.`title` DESC", SortedBlogs(new BlogSortRequest { Sort = "title", Asc = false }).ToSql());
        Assert.Equal(head, SortedBlogs(new BlogSortRequest { Sort = "title" }).ToSql());
        Assert.Equal(head, SortedBlogs(new BlogSortRequest { Asc = true }).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#dynamicSort2")]
    public void DynamicSort2()
    {
        var request = new BlogSortMultiRequest();
        request.Orders.AddRange([("title", true), ("star", false)]);
        Assert.Equal("SELECT t.`id`,t.`title`,t.`content` FROM `t_blog` t WHERE t.`deleted` = ? ORDER BY t.`title` ASC,t.`star` DESC", SortedBlogs(request).ToSql());
    }

    [Fact, Upstream("QueryTest2.java#dynamicSort3"), Upstream("QueryTest2.java#dynamicSort4")]
    public void DynamicSort3()
    {
        var request = new BlogSortMultiRequest(joinTable: true);
        request.Orders.AddRange([("title", true), ("createTime", false)]);
        Assert.Equal("SELECT t.`id`,t.`title`,t.`content` FROM `t_blog` t INNER JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? ORDER BY t.`title` ASC,t1.`create_time` DESC",
            Db.Query<BlogEntity>().InnerJoin<Topic>((t, t1) => t.Id == t1.Id).SortBy(request)
                .Select((t, t1) => new BlogEntity { Id = t.Id, Title = t.Title, Content = t.Content }).ToSql());
    }
}
