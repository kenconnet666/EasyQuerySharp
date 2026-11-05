namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest8.java（多次投影与联表的链式组合、5 到 10 张表的 JOIN 后在最后一张表上加条件）。
/// Java 的 testJoinN 每个 ON 都写成 t1.id = t2.id（照抄原文），最后一张是 t_blog（带逻辑删除）。
/// </summary>
public sealed class QueryTest8(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 用例没有断言：原生 SQL 读成字典
    [Fact, Upstream("QueryTest8.java#test4x")]
    public async Task Test4x() => Assert.NotEmpty(await Db.SqlQueryAsync<Dictionary<string, object?>>("SHOW TABLES", cancellationToken: Ct));

    // Java 用例没有断言：分页后投影再联表、再投影再联表、最后投影到实体
    [Fact, Upstream("QueryTest8.java#test1")]
    public async Task Test1()
    {
        var join = Db.Query<Topic>().Take(100).Select(o => new Topic { Id = o.Id, Stars = o.Stars }).InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id);
        var join2 = join.Select((t, t1) => new BlogEntityTest { Url = t.Id, Title = t1.Title }).InnerJoin<BlogEntity>((t, t1) => t.Url == t1.Id);
        await join2.Select((t, _) => new BlogEntity { Url = t.Url, Content = t.Title }).ToListAsync(Ct);
    }

    [Fact, Upstream("QueryTest8.java#testJoin5")]
    public void TestJoin5() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t1.`id` LEFT JOIN `t_blog` t4 ON t4.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`stars` = ? AND t4.`order` = ?", Db.Query<Topic>()
            .LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _) => t1.Id == t2.Id)
            .LeftJoin<BlogEntity>((t1, t2, _, _, _) => t1.Id == t2.Id)
            .Where((t1, _, _, _, t5) => t1.Stars == 1 && t5.Order == 1m).ToSql());

    [Fact, Upstream("QueryTest8.java#testJoin6")]
    public void TestJoin6() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t1.`id` LEFT JOIN `t_blog` t5 ON t5.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`stars` = ? AND t5.`order` = ?", Db.Query<Topic>()
            .LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<BlogEntity>((t1, t2, _, _, _, _) => t1.Id == t2.Id)
            .Where((t1, _, _, _, _, t6) => t1.Stars == 1 && t6.Order == 1m).ToSql());

    [Fact, Upstream("QueryTest8.java#testJoin7")]
    public void TestJoin7() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t1.`id` LEFT JOIN `t_blog` t6 ON t6.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`stars` = ? AND t6.`order` = ?", Db.Query<Topic>()
            .LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<BlogEntity>((t1, t2, _, _, _, _, _) => t1.Id == t2.Id)
            .Where((t1, _, _, _, _, _, t7) => t1.Stars == 1 && t7.Order == 1m).ToSql());

    [Fact, Upstream("QueryTest8.java#testJoin8")]
    public void TestJoin8() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t1.`id` LEFT JOIN `t_blog` t7 ON t7.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`stars` = ? AND t7.`order` = ?", Db.Query<Topic>()
            .LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<BlogEntity>((t1, t2, _, _, _, _, _, _) => t1.Id == t2.Id)
            .Where((t1, _, _, _, _, _, _, t8) => t1.Stars == 1 && t8.Order == 1m).ToSql());

    [Fact, Upstream("QueryTest8.java#testJoin9")]
    public void TestJoin9() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t1.`id` LEFT JOIN `t_blog` t8 ON t8.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`stars` = ? AND t8.`order` = ?", Db.Query<Topic>()
            .LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<BlogEntity>((t1, t2, _, _, _, _, _, _, _) => t1.Id == t2.Id)
            .Where((t1, _, _, _, _, _, _, _, t9) => t1.Stars == 1 && t9.Order == 1m).ToSql());

    [Fact, Upstream("QueryTest8.java#testJoin10")]
    public void TestJoin10() =>
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t1.`id` LEFT JOIN `t_blog` t9 ON t9.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`stars` = ? AND t9.`order` = ?", Db.Query<Topic>()
            .LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<Topic>((t1, t2, _, _, _, _, _, _, _) => t1.Id == t2.Id)
            .LeftJoin<BlogEntity>((t1, t2, _, _, _, _, _, _, _, _) => t1.Id == t2.Id)
            .Where((t1, _, _, _, _, _, _, _, _, t10) => t1.Stars == 1 && t10.Order == 1m).ToSql());
}
