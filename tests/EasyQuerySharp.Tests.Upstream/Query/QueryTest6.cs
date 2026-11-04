using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;
using static EasyQuerySharp.Tests.Upstream.TopicJoinChain;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest6.java（RIGHT JOIN 链 + DISTINCT + 拦截器 / 逻辑删除 / 追踪开关）。</summary>
public sealed class QueryTest6(MySqlFixture fixture) : MySqlTest(fixture)
{
    // ---------- joinTest1..7：10~4 张表 RIGHT JOIN，全套条件（第三个主键条件 joinTest1 是 whereByIds，其余是 whereById）----------

    [Fact, Upstream("QueryTest6.java#joinTest1", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest1() =>
        Assert.Equal(Sql(10, join: "RIGHT", idsIn: true, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).RightJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).RightJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).RightJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id).RightJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).RightJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id).RightJoin<Topic>((t, _, _, _, _, _, _, _, _, t9) => t.Id == t9.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());

    [Fact, Upstream("QueryTest6.java#joinTest2", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest2() =>
        Assert.Equal(Sql(9, join: "RIGHT", idsIn: false, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).RightJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).RightJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).RightJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id).RightJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).RightJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());

    [Fact, Upstream("QueryTest6.java#joinTest3", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest3() =>
        Assert.Equal(Sql(8, join: "RIGHT", idsIn: false, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).RightJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).RightJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).RightJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id).RightJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());

    [Fact, Upstream("QueryTest6.java#joinTest4", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest4() =>
        Assert.Equal(Sql(7, join: "RIGHT", idsIn: false, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).RightJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).RightJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).RightJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());

    [Fact, Upstream("QueryTest6.java#joinTest5", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest5() =>
        Assert.Equal(Sql(6, join: "RIGHT", idsIn: false, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).RightJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id).RightJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());

    [Fact, Upstream("QueryTest6.java#joinTest6", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest6() =>
        Assert.Equal(Sql(5, join: "RIGHT", idsIn: false, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).RightJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());

    [Fact, Upstream("QueryTest6.java#joinTest7", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest7() =>
        Assert.Equal(Sql(4, join: "RIGHT", idsIn: false, distinct: true), Db.Query<Topic>()
            .RightJoin<Topic>((t, t1) => t.Id == t1.Id).RightJoin<Topic>((t, _, t2) => t.Id == t2.Id).RightJoin<Topic>((t, _, _, t3) => t.Id == t3.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereById("1").FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime).OrderBy(o => o.CreateTime).OrderByDescending(o => o.CreateTime)
            .GroupBy((t, _, _, _) => t.Id).Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .DisableInterceptors().EnableInterceptor("b").DisableInterceptor("b").DisableLogicDelete().AsTracking().AsNoTracking().Distinct().ToSql());
}
