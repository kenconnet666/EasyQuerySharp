using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;
using static EasyQuerySharp.Tests.Upstream.TopicJoinChain;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest7.java（INNER JOIN 链、CTE、forEach 等）。</summary>
public sealed class QueryTest7(MySqlFixture fixture) : MySqlTest(fixture)
{
    // ---------- joinTest1..7：10~4 张表 INNER JOIN，全套条件（第三个主键条件 joinTest1 是 whereByIds，其余是 whereById）----------

    [Fact, Upstream("QueryTest7.java#joinTest1", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest1() =>
        Assert.Equal(Sql(10, join: "INNER", idsIn: true, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).InnerJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).InnerJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).InnerJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id).InnerJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).InnerJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id).InnerJoin<Topic>((t, _, _, _, _, _, _, _, _, t9) => t.Id == t9.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#joinTest2", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest2() =>
        Assert.Equal(Sql(9, join: "INNER", idsIn: false, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).InnerJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).InnerJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).InnerJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id).InnerJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).InnerJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#joinTest3", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest3() =>
        Assert.Equal(Sql(8, join: "INNER", idsIn: false, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).InnerJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).InnerJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).InnerJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id).InnerJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#joinTest4", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest4() =>
        Assert.Equal(Sql(7, join: "INNER", idsIn: false, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).InnerJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).InnerJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).InnerJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#joinTest5", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest5() =>
        Assert.Equal(Sql(6, join: "INNER", idsIn: false, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).InnerJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).InnerJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#joinTest6", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest6() =>
        Assert.Equal(Sql(5, join: "INNER", idsIn: false, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).InnerJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#joinTest7", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest7() =>
        Assert.Equal(Sql(4, join: "INNER", idsIn: false, distinct: false), Db.Query<Topic>()
            .InnerJoin<Topic>((t, t1) => t.Id == t1.Id).InnerJoin<Topic>((t, _, t2) => t.Id == t2.Id).InnerJoin<Topic>((t, _, _, t3) => t.Id == t3.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().ToSql());

    [Fact, Upstream("QueryTest7.java#forEachTest1")]
    public async Task ForEachTest1()
    {
        var topic = await Db.Query<Topic>().WhereById("1").FirstOrDefaultAsync(Ct);
        Assert.NotNull(topic);
        Assert.Null(topic.Alias);
    }

    private const string TopicSelect = "SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE ";

    // Java 的 isBlank / isNotBlank / isEmpty / isNotEmpty：C# 的 string.IsNullOrWhiteSpace / IsNullOrEmpty 及取反
    [Fact, Upstream("QueryTest7.java#testBank1")]
    public void TestBank1() =>
        Assert.Equal(TopicSelect + "(`id` IS NOT NULL AND `id` <> '' AND LTRIM(`id`) <> '')", Db.Query<Topic>().Where(o => !string.IsNullOrWhiteSpace(o.Id)).ToSql());

    [Fact, Upstream("QueryTest7.java#testBank2")]
    public void TestBank2() =>
        Assert.Equal(TopicSelect + "(`id` IS NULL OR `id` = '' OR LTRIM(`id`) = '')", Db.Query<Topic>().Where(o => string.IsNullOrWhiteSpace(o.Id)).ToSql());

    [Fact, Upstream("QueryTest7.java#testEmpty1")]
    public void TestEmpty1() =>
        Assert.Equal(TopicSelect + "(`id` IS NOT NULL AND `id` <> '')", Db.Query<Topic>().Where(o => !string.IsNullOrEmpty(o.Id)).ToSql());

    [Fact, Upstream("QueryTest7.java#testEmpty2")]
    public void TestEmpty2() =>
        Assert.Equal(TopicSelect + "(`id` IS NULL OR `id` = '')", Db.Query<Topic>().Where(o => string.IsNullOrEmpty(o.Id)).ToSql());

    // Java 循环里每个值一个 or 组：C# 写成客户端集合的 All，展开成 AND 连接的 OR 组
    [Fact, Upstream("QueryTest7.java#testQuery9")]
    public void TestQuery9()
    {
        string[] searchValues = ["1", "小明", "小红"];
        Assert.Equal(TopicSelect + "(`id` IS NULL OR `id` = '' OR LTRIM(`id`) = '') AND (`id` LIKE ? OR `title` LIKE ?) AND (`id` LIKE ? OR `title` LIKE ?) AND (`id` LIKE ? OR `title` LIKE ?)",
            Db.Query<Topic>().Where(o => string.IsNullOrWhiteSpace(o.Id)).Where(o => searchValues.All(v => o.Id!.Contains(v) || o.Title!.Contains(v))).ToSql());
    }

    private const string GroupedBlogs = "SELECT t1.`id`,SUM(t1.`score`) AS `score` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL GROUP BY t1.`id` HAVING ";

    private Querying.GroupQuery<string?, (Topic, BlogEntity)> GroupedByBlog() =>
        Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t1.Title != null).GroupBy((t, t1) => t1.Id);

    // Java 的 key1() 不带 as 时不写别名：C# 写成组内列 g.Column(..)
    private static readonly System.Linq.Expressions.Expression<Func<IGroup<string?, (Topic, BlogEntity)>, BlogEntity>> KeyAndScore =
        g => new BlogEntity { Id = g.Column(e => e.Item2.Id), Score = g.Sum(e => e.Item2.Score) };

    [Fact, Upstream("QueryTest7.java#testQuery10"), Upstream("QueryTest7.java#testQuery11")]
    public void TestQuery10() =>
        Assert.Equal(GroupedBlogs + "COUNT(t1.`id`) >= ?",
            GroupedByBlog().HavingIf(true, g => g.Count(e => e.Item2.Id) >= 1L).HavingIf(false, g => g.Count(e => e.Item2.Id) >= 1L).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery12")]
    public void TestQuery12() =>
        Assert.Equal(GroupedBlogs + "AVG(t1.`score`) >= ?", GroupedByBlog().HavingIf(true, g => g.Average(e => e.Item2.Score) >= 1m).HavingIf(false, g => g.Average(e => e.Item2.Score) >= 1m).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery13")]
    public void TestQuery13() =>
        Assert.Equal(GroupedBlogs + "AVG(DISTINCT t1.`score`) >= ?", GroupedByBlog().HavingIf(true, g => g.Distinct().Average(e => e.Item2.Score) >= 1m).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery14")]
    public void TestQuery14() =>
        Assert.Equal(GroupedBlogs + "SUM(t1.`score`) = ?", GroupedByBlog().HavingIf(true, g => g.Sum(e => e.Item2.Score) == 1m).HavingIf(false, g => g.Sum(e => e.Item2.Score) >= 1m).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery15")]
    public void TestQuery15() =>
        Assert.Equal(GroupedBlogs + "SUM(DISTINCT t1.`score`) = ?", GroupedByBlog().HavingIf(true, g => g.Distinct().Sum(e => e.Item2.Score) == 1m).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery16")]
    public void TestQuery16() =>
        Assert.Equal(GroupedBlogs + "COUNT(DISTINCT t1.`id`) >= ?", GroupedByBlog().HavingIf(true, g => g.Distinct().Count(e => e.Item2.Id) >= 1L).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery17")]
    public void TestQuery17() =>
        Assert.Equal(GroupedBlogs + "MIN(t1.`id`) >= ?", GroupedByBlog().HavingIf(true, g => string.Compare(g.Min(e => e.Item2.Id), "1") >= 0).Select(KeyAndScore).ToSql());

    [Fact, Upstream("QueryTest7.java#testQuery18")]
    public void TestQuery18() =>
        Assert.Equal(GroupedBlogs + "MAX(t1.`id`) >= ?", GroupedByBlog().HavingIf(true, g => string.Compare(g.Max(e => e.Item2.Id), "1") >= 0).Select(KeyAndScore).ToSql());
}
