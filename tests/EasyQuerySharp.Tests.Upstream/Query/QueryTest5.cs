using EasyQuerySharp.Querying;
using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;
using static EasyQuerySharp.Tests.Upstream.TopicJoinChain;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest5.java（10~4 张表的分组查询、分组后聚合、原生 SQL 来源、各种投影写法）。</summary>
public sealed class QueryTest5(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 的 baseQueryable1..7：n 张表自连接，第一张表上的全套条件，按第一张表的 id 分组
    private GroupQuery<string?, (Topic, Topic, Topic, Topic, Topic, Topic, Topic, Topic, Topic, Topic)> Base10(string? lastTable = null)
    {
        var q = Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).LeftJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, _, _, t9) => t.Id == t9.Id);
        if (lastTable is not null) q = q.AsTable(lastTable);
        return q.Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L);
    }

    private static readonly System.Linq.Expressions.Expression<Func<IGroup<string?, (Topic, Topic, Topic, Topic, Topic, Topic, Topic, Topic, Topic, Topic)>, Topic>> KeyAndCount10 =
        g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) };

    // Java 的 o.key1() 不带 as 时不写别名：C# 写成组内列 g.Column(..)
    [Fact, Upstream("QueryTest5.java#joinTest1")]
    public void JoinTest1() => Assert.Equal(Sql(10), Base10().Select(KeyAndCount10).ToSql());

    // Java 在分组查询上直接调用 sum / avg（结果取第一组）；C# 投影成每组一个值后读取
    [Theory, Upstream("QueryTest5.java#joinTest1_1", Deviation = "Java 在分组查询上直接聚合；C# 投影成每组一个聚合值再执行，SQL 相同"),
     Upstream("QueryTest5.java#joinTest1_2", Deviation = "同 joinTest1_1"), Upstream("QueryTest5.java#joinTest1_3", Deviation = "同 joinTest1_1"),
     Upstream("QueryTest5.java#joinTest1_4", Deviation = "同 joinTest1_1"), Upstream("QueryTest5.java#joinTest1_5", Deviation = "同 joinTest1_1"),
     Upstream("QueryTest5.java#joinTest1_6", Deviation = "同 joinTest1_1"), Upstream("QueryTest5.java#joinTest1_7", Deviation = "同 joinTest1_1"),
     Upstream("QueryTest5.java#joinTest1_8", Deviation = "同 joinTest1_1"), Upstream("QueryTest5.java#joinTest1_9", Deviation = "同 joinTest1_1")]
    [InlineData("SUM", 1)]
    [InlineData("AVG", 1)]
    [InlineData("AVG", 3)]
    public async Task JoinTest1_1(string function, int table)
    {
        var grouped = Base10("a123");
        var query = (function, table) switch
        {
            ("SUM", _) => grouped.Select(g => (decimal?)g.Sum(e => e.Item1.Stars)),
            ("AVG", 1) => grouped.Select(g => (decimal?)g.Average(e => e.Item1.Stars)),
            _ => grouped.Select(g => (decimal?)g.Average(e => e.Item3.Stars)),
        };
        var alias = table == 1 ? "t" : "t2";
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => query.ToListAsync(Ct));
        Assert.Equal(Sql(10, $"{function}({alias}.`stars`)", "a123"), ex.Sql);
    }

    [Fact, Upstream("QueryTest5.java#joinTest2")]
    public void JoinTest2() =>
        Assert.Equal(Sql(9), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).LeftJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) }).ToSql());

    [Fact, Upstream("QueryTest5.java#joinTest3")]
    public void JoinTest3() =>
        Assert.Equal(Sql(8), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) }).ToSql());

    [Fact, Upstream("QueryTest5.java#joinTest4")]
    public void JoinTest4() =>
        Assert.Equal(Sql(7), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) }).ToSql());

    [Fact, Upstream("QueryTest5.java#joinTest5")]
    public void JoinTest5() =>
        Assert.Equal(Sql(6), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) }).ToSql());

    [Fact, Upstream("QueryTest5.java#joinTest6")]
    public void JoinTest6() =>
        Assert.Equal(Sql(5), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) }).ToSql());

    [Fact, Upstream("QueryTest5.java#joinTest7")]
    public void JoinTest7() =>
        Assert.Equal(Sql(4), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, _, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) }).ToSql());

    [Fact, Upstream("QueryTest5.java#test1")]
    public async Task Test1()
    {
        Assert.Equal(1, await Db.QuerySql<int>("select 1").FirstOrDefaultAsync(Ct));
        Assert.Equal("SELECT * FROM (select 1) t", Db.QuerySql<int>("select 1").ToSql());
    }

    // Java 的 FETCHER.allFields().alias() 选出了 alias 列；C# 的 Topic.Alias 标了 ColumnIgnore，不是列
    [Fact, Upstream("QueryTest5.java#test2", Deviation = "Topic.Alias 在 C# 里不是列（ColumnIgnore），带 alias 的两段改为不含 alias；动态列清单用成员初始化表达")]
    public void Test2()
    {
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t", Db.Query<Topic>().Select(o => o).ToSql());
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`", Db.Query<Topic>().ToSql());
        Assert.Equal("SELECT ROW_NUMBER() OVER(ORDER BY t.`create_time`) AS `title`,t.`id`,t.`stars`,t.`create_time` FROM `t_topic` t",
            Db.Query<Topic>().Select(o => new TopicTypeVO
            {
                Title = Sql.Raw<string>("ROW_NUMBER() OVER(ORDER BY {0})", o.CreateTime), Id = o.Id, Stars = o.Stars, CreateTime = o.CreateTime,
            }).ToSql());
        Assert.Equal("SELECT t.`id`,t.`title`,t.`stars` FROM `t_topic` t",
            Db.Query<Topic>().Select(o => new TopicTypeVO { Id = o.Id, Title = o.Title, Stars = o.Stars }).ToSql());
        Assert.Equal("SELECT t.`id`,t.`title` FROM `t_topic` t", Db.Query<Topic>().Select(o => new TopicTypeVO { Id = o.Id, Title = o.Title }).ToSql());
    }

    [Fact, Upstream("QueryTest5.java#groupTest5_2")]
    public async Task GroupTest5_2() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<BlogEntity>().GroupBy(o => o.Id)
            .Select(g => new BlogEntity
            {
                Id = g.Key, Score = g.Distinct().Sum(s => s.Score ?? 0m), Order = g.Distinct().Count(s => s.Order ?? 1m), Status = g.Distinct().Average(s => s.Status ?? 3),
            }).SingleOrDefaultAsync(Ct));

    [Fact, Upstream("QueryTest5.java#joinTestx")]
    public async Task JoinTestx()
    {
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Query<Topic>().AsTable("a123")
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, b) => b.Title != null)
            .GroupBy((t, b) => b.Id ?? "")
            .Select(g => new BlogEntity { Id = g.Key, Score = g.Sum(s => s.Item2.Score) })
            .Distinct()
            .Select(s => Sql.Count(s.Id))
            .FirstOrDefaultAsync(Ct));
        Assert.Equal("SELECT COUNT(t2.`id`) FROM (SELECT DISTINCT IFNULL(t1.`id`,?) AS `id`,SUM(t1.`score`) AS `score` FROM `a123` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL GROUP BY IFNULL(t1.`id`,?)) t2 LIMIT 1", ex.Sql);
    }

    [Fact, Upstream("QueryTest5.java#joinTestx1")]
    public async Task JoinTestx1()
    {
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select<BlogEntityTest>().ToListAsync(Ct));
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Query<BlogEntity>().AsTable("a123").Select<BlogEntityTest>().ToListAsync(Ct));
        Assert.Equal("SELECT t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `a123` t WHERE t.`deleted` = ?", ex.Sql);
    }
}
