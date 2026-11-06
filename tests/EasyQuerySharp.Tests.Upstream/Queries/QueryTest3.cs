using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>对应 Java：QueryTest3.java（四表连接、条件值过滤、聚合、OR / AND 组、CASE WHEN、自比较）。</summary>
public sealed partial class QueryTest3(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns =
        "`id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top`";

    private const string TopicColumns = "t.`id`,t.`stars`,t.`title`,t.`create_time`";

    private static string Join(string kind, int index) => $" {kind} JOIN `t_blog` t{index} ON t{index}.`deleted` = ? AND t.`id` = t{index}.`id`";

    private Query<Topic, BlogEntity, BlogEntity, BlogEntity> FourTables(string second = "LEFT", string third = "LEFT")
    {
        var q2 = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id);
        var q3 = second == "INNER" ? q2.InnerJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id) : q2.LeftJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id);
        return third switch
        {
            "INNER" => q3.InnerJoin<BlogEntity>((t, t1, t2, t3) => t.Id == t3.Id),
            "RIGHT" => q3.RightJoin<BlogEntity>((t, t1, t2, t3) => t.Id == t3.Id),
            _ => q3.LeftJoin<BlogEntity>((t, t1, t2, t3) => t.Id == t3.Id),
        };
    }

    private static string FourSql(string second = "LEFT", string third = "LEFT") => " FROM `t_topic` t" + Join("LEFT", 1) + Join(second, 2) + Join(third, 3);

    // Java 的 filterConfigure(NotNullOrEmptyValueFilter)：C# 的 UseValueFilter(ValueFilter.NotNullOrBlank)
    [Fact, Upstream("QueryTest3.java#query124")]
    public void Query124() =>
        Assert.Equal("SELECT " + TopicColumns + FourSql() + " WHERE t.`id` = ? LIMIT 2 OFFSET 1",
            FourTables().UseValueFilter(ValueFilter.NotNullOrBlank).Where(o => o.Id == "3").Skip(1).Take(2).ToSql());

    [Fact, Upstream("QueryTest3.java#query124_5")]
    public void Query124_5() =>
        Assert.Equal("SELECT " + TopicColumns + FourSql() + " LIMIT 2 OFFSET 1",
            FourTables().UseValueFilter(ValueFilter.NotNullOrBlank).Where(o => o.Id == "").UseValueFilter(ValueFilter.All).Skip(1).Take(2).ToSql());

    [Fact, Upstream("QueryTest3.java#query124_4")]
    public void Query124_4() =>
        Assert.Equal("SELECT " + TopicColumns + FourSql() + " LEFT JOIN `t_blog` t4 ON t4.`deleted` = ? AND t.`id` = t3.`id` LEFT JOIN `t_blog` t5 ON t5.`deleted` = ? AND t.`id` = t4.`id` WHERE t.`id` = ? LIMIT 2 OFFSET 1",
            FourTables().LeftJoin<BlogEntity>((t, t1, t2, t3, t4) => t.Id == t3.Id).LeftJoin<BlogEntity>((t, _, _, _, t4, _) => t.Id == t4.Id)
                .Where(o => o.Id == "3").Skip(1).Take(2).ToSql());

    [Fact, Upstream("QueryTest3.java#query124_2")]
    public void Query124_2() =>
        Assert.Equal("SELECT " + TopicColumns + FourSql() + " WHERE t.`id` = ? LIMIT 2 OFFSET 1",
            Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).LeftJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id)
                .LeftJoin<BlogEntity>((t, t1, t2, t3) => t.Id == t3.Id).Where(o => o.Id == "3").Skip(1).Take(2).ToSql());

    [Fact, Upstream("QueryTest3.java#query125")]
    public void Query125() =>
        Assert.Equal("SELECT t.`id`,t1.`title`,t2.`star`" + FourSql("INNER", "INNER") + " WHERE t.`id` = ? LIMIT 2 OFFSET 1",
            FourTables("INNER", "INNER").Where(o => o.Id == "3").Select((t, t1, t2, t3) => new BlogEntity { Id = t.Id, Title = t1.Title, Star = t2.Star }).Skip(1).Take(2).ToSql());

    [Fact, Upstream("QueryTest3.java#query126")]
    public async Task Query126()
    {
        var empty = await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id && t1.Id == "3")
            .InnerJoin<BlogEntity>((t, t1, t2) => t.Id == t2.Id).RightJoin<BlogEntity>((t, t1, t2, t3) => t.Id == t3.Id)
            .Where(o => o.Id == "3").Select((t, t1, t2, t3) => new BlogEntity { Id = t.Id, Title = t1.Title, Star = t2.Star }).Skip(1).Take(2).ToListAsync(Ct);
        Assert.Empty(empty);
        Assert.Single(await FourTables("INNER").Where(o => o.Id == "3").Select((t, t1, t2, t3) => new BlogEntity { Id = t.Id, Title = t1.Title, Star = t2.Star }).Take(1).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest3.java#query112"), Upstream("QueryTest3.java#query113"), Upstream("QueryTest3.java#query114"), Upstream("QueryTest3.java#query115")]
    public async Task Query112()
    {
        Assert.Equal(1.2m, await FourTables("INNER").Where(o => o.Id == "3").SumAsync((t, t1, t2, t3) => t1.Score, Ct) ?? 0m);
        Assert.Null(await FourTables().Where(o => o.Id == "3x").SumAsync((t, t1, t2, t3) => t1.Score, Ct));
        Assert.Equal(1.2m, await FourTables().Where(o => o.Id == "3").SumAsync((t, t1, t2, t3) => t3.Score, Ct));
        Assert.Null(await FourTables().Where(o => o.Id == "3x").SumAsync((t, t1, t2, t3) => t3.Score, Ct));
        Assert.Equal(0m, await FourTables("INNER").Where(o => o.Id == "3x").SumAsync((t, t1, t2, t3) => t3.Score, Ct) ?? 0m);
    }

    [Fact, Upstream("QueryTest3.java#query116"), Upstream("QueryTest3.java#query117"), Upstream("QueryTest3.java#query118"), Upstream("QueryTest3.java#query119")]
    public async Task Query116()
    {
        Assert.Null(await FourTables().Where(o => o.Id == "3x").MaxAsync((t, t1, t2, t3) => t3.Score, Ct));
        Assert.Equal(0m, await FourTables().Where(o => o.Id == "3x").MaxAsync((t, t1, t2, t3) => t1.Score, Ct) ?? 0m);
        string[] ids = ["3x", "3"];
        Assert.Equal(1.2m, await FourTables(third: "INNER").Where(o => ids.Contains(o.Id!)).MaxAsync((t, t1, t2, t3) => t3.Score, Ct));
        Assert.Null(await FourTables(third: "INNER").Where(o => o.Id == "3x").MinAsync((t, t1, t2, t3) => t1.Score, Ct));
        Assert.Equal(1.2m, await FourTables(third: "INNER").Where(o => ids.Contains(o.Id!)).MinAsync((t, t1, t2, t3) => t3.Score, Ct));
    }

    [Fact, Upstream("QueryTest3.java#query120")]
    public async Task Query120()
    {
        string[] hit = ["3", "2", "5"], miss = ["3x", "2x", "5x"];
        Assert.Equal(1.2m, await FourTables(third: "INNER").Where(o => hit.Contains(o.Id!)).AverageDecimalAsync((t, t1, t2, t3) => t1.Score, Ct));
        Assert.Null(await FourTables(third: "INNER").Where(o => miss.Contains(o.Id!)).AverageDecimalAsync((t, t1, t2, t3) => t3.Score, Ct));
        Assert.Equal(3.3333d, await FourTables().Where(o => hit.Contains(o.Id!)).AverageAsync((t, t1, t2, t3) => t1.Star, Ct));
    }

    [Fact, Upstream("QueryTest3.java#query121")]
    public void Query121()
    {
        string[] ids = ["3", "2", "5"];
        var where = " WHERE t.`id` IN (?,?,?) ORDER BY ";
        Assert.Equal("SELECT " + TopicColumns + FourSql() + where + "t1.`order` ASC", FourTables().Where(o => ids.Contains(o.Id!)).OrderBy((t, t1, t2, t3) => t1.Order).ToSql());
        Assert.Equal("SELECT " + TopicColumns + FourSql() + where + "t1.`order` ASC,t2.`score` ASC",
            FourTables().Where(o => ids.Contains(o.Id!)).OrderBy((t, t1, t2, t3) => t1.Order).ThenBy((t, t1, t2, t3) => t2.Score).ToSql());
        Assert.Equal("SELECT " + TopicColumns + FourSql(third: "INNER") + where + "t3.`id` ASC",
            FourTables(third: "INNER").Where(o => ids.Contains(o.Id!)).OrderByIf(false, (t, t1, t2, t3) => t1.Order).OrderByIf(true, (t, t1, t2, t3) => t3.Id).ToSql());
        Assert.Equal("SELECT " + TopicColumns + FourSql(third: "RIGHT") + where + "t3.`order` DESC",
            FourTables(third: "RIGHT").Where(o => ids.Contains(o.Id!)).OrderByDescending((t, t1, t2, t3) => t3.Order).ToSql());
        Assert.Equal("SELECT " + TopicColumns + FourSql() + where + "t3.`id` DESC,t3.`order` DESC",
            FourTables().Where(o => ids.Contains(o.Id!)).OrderByDescendingIf(false, (t, t1, t2, t3) => t3.Order)
                .OrderByDescendingIf(true, (t, t1, t2, t3) => t3.Id).OrderByDescendingIf(true, (t, t1, t2, t3) => t3.Order).ToSql());
    }

    [Fact, Upstream("QueryTest3.java#query122")]
    public void Query122()
    {
        string[] ids = ["3", "2", "5"];
        Assert.Equal("SELECT t.`id`" + FourSql() + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t1.`order` ASC",
            FourTables().Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t.Id).OrderBy(g => g.Column(e => e.Item2.Order)).Select(g => g.Key).ToSql());
        Assert.Equal("SELECT t3.`id`" + FourSql(third: "INNER") + " WHERE t.`id` IN (?,?,?) GROUP BY t3.`id` ORDER BY t3.`order` ASC",
            FourTables(third: "INNER").Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t3.Id).OrderBy(g => g.Column(e => e.Item4.Order)).Select(g => g.Key).ToSql());
    }

    [Fact, Upstream("QueryTest3.java#query123")]
    public void Query123()
    {
        string[] ids = ["3", "2", "5"];
        Assert.Equal("SELECT DISTINCT t.`id`" + FourSql("INNER", "RIGHT") + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t3.`order` ASC LIMIT 10",
            FourTables("INNER", "RIGHT").Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t.Id).OrderBy(g => g.Column(e => e.Item4.Order))
                .Select(g => g.Key).Distinct().Take(10).ToSql());
        Assert.Equal("SELECT COUNT(t4.`id`) FROM (SELECT DISTINCT t.`id` AS `id`" + FourSql("INNER") + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t3.`order` ASC) t4 WHERE t4.`id` = ?",
            FourTables("INNER").Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t.Id).OrderBy(g => g.Column(e => e.Item4.Order))
                .Select(g => new Topic { Id = g.Key }).Distinct().Where(o => o.Id == "x").Select(o => Sql.Count(o.Id)).ToSql());
        Assert.Equal("SELECT count(1) FROM (SELECT DISTINCT t.`id` AS `id`" + FourSql() + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t3.`order` ASC) t4",
            FourTables().Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t.Id).OrderBy(g => g.Column(e => e.Item4.Order))
                .Select(g => new Topic { Id = g.Key }).Distinct().SelectRaw("count(1)").ToSql());
        Assert.Equal("SELECT count(1) FROM (SELECT DISTINCT t.`id`" + FourSql(third: "RIGHT") + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t.`stars` DESC) t4",
            FourTables(third: "RIGHT").Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t.Id).OrderByDescending(g => g.Column(e => e.Item1.Stars))
                .Select(g => g.Key).Distinct().SelectRaw("count(1)").ToSql());
        Assert.Equal("SELECT count(1) FROM (SELECT DISTINCT t.`id`" + FourSql(third: "RIGHT") + " WHERE t.`id` IN (?,?,?) GROUP BY t.`id` ORDER BY t.`stars` ASC) t4",
            FourTables(third: "RIGHT").Where(o => ids.Contains(o.Id!)).GroupBy((t, t1, t2, t3) => t.Id).OrderBy(g => g.Column(e => e.Item1.Stars))
                .Select(g => g.Key).Distinct().SelectRaw("count(1)").ToSql());
    }

    [Fact, Upstream("QueryTest3.java#testLambda3"), Upstream("QueryTest3.java#testProperty3")]
    public async Task TestLambda3() =>
        Assert.NotNull(await Db.Query<Topic>().Where(t => t.Id == "3" || t.Title!.Contains("你好")).FirstOrDefaultAsync(Ct));

    [Fact, Upstream("QueryTest3.java#testProperty3_1", Deviation = "Java 把三个参数写进 IFNULL，C# 嵌套成 IFNULL(a,IFNULL(b,c))；Java 断言查到数据依赖库里已有的旧数据，C# 只执行")]
    public async Task TestProperty3_1()
    {
        _ = (await Db.Query<Topic>().Where(t => (object)(t.Id ?? "1") == (object)(t.Stars ?? 3) || t.Title!.Contains("你好")).FirstOrDefaultAsync(Ct));
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (IFNULL(`id`,?) = IFNULL(`stars`,?) OR `title` LIKE ?)",
            Db.Query<Topic>().Where(t => (object)(t.Id ?? "1") == (object)(t.Stars ?? 3) || t.Title!.Contains("你好")).ToSql());
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (IFNULL(`id`,?) = IFNULL(`stars`,IFNULL(`id`,?)) OR `title` LIKE ?)",
            Db.Query<Topic>().Where(t => (t.Id ?? "1") == ((string)(object)t.Stars! ?? t.Id ?? "3") || t.Title!.Contains("你好")).ToSql());
    }

    [Fact, Upstream("QueryTest3.java#testSelfPredicate1")]
    public void TestSelfPredicate1()
    {
        var head = $"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `score` = ? AND `score` ";
        Assert.Equal(head + "= `order`", Db.Query<BlogEntity>().Where(t => t.Score == 3m && t.Score == t.Order).ToSql());
        Assert.Equal(head + "<> `order`", Db.Query<BlogEntity>().Where(t => t.Score == 3m && t.Score != t.Order).ToSql());
        Assert.Equal(head + "> `order`", Db.Query<BlogEntity>().Where(t => t.Score == 3m && t.Score > t.Order).ToSql());
        Assert.Equal(head + ">= `order`", Db.Query<BlogEntity>().Where(t => t.Score == 3m && t.Score >= t.Order).ToSql());
        Assert.Equal(head + "<= `order`", Db.Query<BlogEntity>().Where(t => t.Score == 3m && t.Score <= t.Order).ToSql());
        Assert.Equal(head + "< `order`", Db.Query<BlogEntity>().Where(t => t.Score == 3m && t.Score < t.Order).ToSql());
    }

    [Fact, Upstream("QueryTest3.java#testSample")]
    public async Task TestSample() =>
        Assert.Empty(await Db.Query<Topic>().Where(o => o.Title!.Contains("someTitle")).OrderBy(o => o.CreateTime).ThenBy(o => o.Id).ToListAsync(Ct));

    [Fact, Upstream("QueryTest3.java#testCaseWhen")]
    public void TestCaseWhen()
    {
        var statement = Db.Query<Topic>().Where(o => o.Title!.Contains("someTitle")).OrderBy(o => o.CreateTime).ThenBy(o => o.Id)
            .Select(t => new Topic
            {
                Title = t.Title == "123" && t.Stars >= 1 ? "first1" : t.Title == "456" ? "first2" : t.Title == "789" ? "first3" : "firstEnd",
            }).ToStatement();
        Assert.Equal("SELECT (CASE WHEN t.`title` = ? AND t.`stars` >= ? THEN ? WHEN t.`title` = ? THEN ? WHEN t.`title` = ? THEN ? ELSE ? END) AS `title` FROM `t_topic` t WHERE t.`title` LIKE ? ORDER BY t.`create_time` ASC,t.`id` ASC", statement.Text);
        Assert.Equal(["123", 1, "first1", "456", "first2", "789", "first3", "firstEnd", "%someTitle%"], statement.Parameters.Select(p => p.Value));
    }

    [Fact, Upstream("QueryTest3.java#propertyCaseWhen")]
    public async Task PropertyCaseWhen()
    {
        var query = Db.Query<Topic>().Where(t => t.Title!.Contains("someTitle"))
            .Select(t => new Topic { Title = t.Title == "123" ? "111" : t.Title == "456" ? "222" : "222", Id = t.Id });
        Assert.Equal("SELECT (CASE WHEN t.`title` = ? THEN ? WHEN t.`title` = ? THEN ? ELSE ? END) AS `title`,t.`id` FROM `t_topic` t WHERE t.`title` LIKE ?", query.ToSql());
        Assert.Empty(await query.ToListAsync(Ct));
    }

    // Java 断言能查到数据，依赖库里创建时间早于当前时间的旧数据；C# 的种子数据是写入时的未来时间，这里只执行、核对 SQL
    [Fact, Upstream("QueryTest3.java#testAndOr1", Deviation = "查询结果依赖库里已有数据，C# 只执行不断言行数")]
    public async Task TestAndOr1()
    {
        var now = DateTime.Now;
        var query1 = Db.Query<Topic>().Where(t => t.Id == "1" && (t.Title!.Contains("你好") || t.Title == "我是title" || t.CreateTime <= now));
        await query1.FirstOrDefaultAsync(Ct);
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? AND (`title` LIKE ? OR `title` = ? OR `create_time` <= ?)", query1.ToSql());
        var query2 = Db.Query<Topic>().Where(t => t.Title!.Contains("你好") || t.Title == "我是title" || t.CreateTime <= now);
        Assert.NotEmpty(await query2.ToListAsync(Ct));
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`title` LIKE ? OR `title` = ? OR `create_time` <= ?)", query2.ToSql());
        var query3 = Db.Query<Topic>().Where(t => t.Id == "1" || (t.Title!.Contains("你好") && t.Title == "我是title" && t.CreateTime <= now));
        await query3.FirstOrDefaultAsync(Ct);
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`id` = ? OR (`title` LIKE ? AND `title` = ? AND `create_time` <= ?))", query3.ToSql());
    }
}
