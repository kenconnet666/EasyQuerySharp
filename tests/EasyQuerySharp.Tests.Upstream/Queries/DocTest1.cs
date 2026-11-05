namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：doc/DocTest1.java（按主键查、窗口函数的 Part 投影与外层筛选、自关联导航的多级 JOIN）。
/// DocBlog / TestSelf 与库里的表结构不一致或表不存在，Java 只断言出错语句的 SQL。
/// </summary>
public sealed class DocTest1(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static async Task<SqlStatement> Failed(Func<Task> action) =>
        (await Assert.ThrowsAsync<EasyQueryException>(action)).Statement!;

    private const string BlogColumns = "`id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top`";

    [Fact, Upstream("doc/DocTest1.java#test1")]
    public async Task Test1()
    {
        await Db.Query<SysUser>().Select(s => new Draft<string?, bool>(s.Id, s.Blogs!.Any())).ToListAsync(Ct);

        using var listen = SqlCapture.Listen();
        Assert.NotNull(await Db.Query<BlogEntity>().FindAsync("1", Ct));
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ?");
        listen.AssertParams("false(Boolean),1(String)");
    }

    [Fact, Upstream("doc/DocTest1.java#test2")]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().WhereById("1").FirstAsync(Ct);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ? LIMIT 1");
        listen.AssertParams("false(Boolean),1(String)");
    }

    [Fact, Upstream("doc/DocTest1.java#testBlog1")]
    public async Task TestBlog1()
    {
        var statement = await Failed(() => Db.Query<DocBlog>().Where(d => d.PublishTime > new DateTime(2024, 1, 1))
            .Select(d => new Part<DocBlog, long>(d, Sql.Over.RowNumber(w => w.PartitionBy(d.Topic).OrderByDescending(d.Score)))).ToListAsync(Ct));
        SqlOnly.AssertStatement(statement,
            "SELECT t.`id`,t.`title`,t.`topic`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,(ROW_NUMBER() OVER (PARTITION BY t.`topic` ORDER BY t.`score` DESC)) AS `__part__column1` FROM `t_blog` t WHERE t.`publish_time` > ?",
            "2024-01-01T00:00(LocalDateTime)");
    }

    // 每个主题按评分取前 3、按点赞取前 5：窗口列在内层，外层按窗口列筛选后只取实体
    [Fact, Upstream("doc/DocTest1.java#testBlog2")]
    public async Task TestBlog2()
    {
        var statement = await Failed(() => Ranked().Select(p => p.Entity).ToListAsync(Ct));
        SqlOnly.AssertStatement(statement,
            "SELECT t1.`id`,t1.`title`,t1.`topic`,t1.`url`,t1.`star`,t1.`publish_time`,t1.`score`,t1.`status` FROM (SELECT t.`id`,t.`title`,t.`topic`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,(ROW_NUMBER() OVER (PARTITION BY t.`topic` ORDER BY IFNULL(t.`score`,?) DESC)) AS `__part__column1`,(ROW_NUMBER() OVER (PARTITION BY t.`topic` ORDER BY IFNULL(t.`star`,?) ASC)) AS `__part__column2` FROM `t_blog` t WHERE t.`publish_time` > ?) t1 WHERE t1.`__part__column1` <= ? AND t1.`__part__column2` <= ?",
            "0(BigDecimal),0(Integer),2024-01-01T00:00(LocalDateTime),3(Long),5(Long)");
    }

    [Fact, Upstream("doc/DocTest1.java#testBlog3")]
    public async Task TestBlog3()
    {
        var statement = await Failed(() => Ranked().ToListAsync(Ct));
        SqlOnly.AssertStatement(statement,
            "SELECT t1.`id`,t1.`title`,t1.`topic`,t1.`url`,t1.`star`,t1.`publish_time`,t1.`score`,t1.`status`,t1.`__part__column1` AS `__part__column1`,t1.`__part__column2` AS `__part__column2` FROM (SELECT t.`id`,t.`title`,t.`topic`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,(ROW_NUMBER() OVER (PARTITION BY t.`topic` ORDER BY IFNULL(t.`score`,?) DESC)) AS `__part__column1`,(ROW_NUMBER() OVER (PARTITION BY t.`topic` ORDER BY IFNULL(t.`star`,?) ASC)) AS `__part__column2` FROM `t_blog` t WHERE t.`publish_time` > ?) t1 WHERE t1.`__part__column1` <= ? AND t1.`__part__column2` <= ?",
            "0(BigDecimal),0(Integer),2024-01-01T00:00(LocalDateTime),3(Long),5(Long)");
    }

    private Querying.Query<Part<DocBlog, long, long>> Ranked() =>
        Db.Query<DocBlog>().Where(d => d.PublishTime > new DateTime(2024, 1, 1))
            .Select(d => new Part<DocBlog, long, long>(d,
                Sql.Over.RowNumber(w => w.PartitionBy(d.Topic).OrderByDescending(d.Score ?? 0m)),
                Sql.Over.RowNumber(w => w.PartitionBy(d.Topic).OrderBy(d.Star ?? 0))))
            .Where(p => p.Value1 <= 3L && p.Value2 <= 5L);

    // 自关联的上级再取它的用户：每段导航各 JOIN 一次
    [Fact, Upstream("doc/DocTest1.java#testBlog4")]
    public async Task TestBlog4()
    {
        var statement = await Failed(() => Db.Query<TestSelf>().Where(t => t.Uid == "1")
            .Select(t => new Draft<string?, string?>(t.MyUser!.Name, t.Parent!.MyUser!.Name)).ToListAsync(Ct));
        SqlOnly.AssertStatement(statement,
            "SELECT t1.`name` AS `value1`,t3.`name` AS `value2` FROM `test_self` t LEFT JOIN `my_user` t1 ON t1.`id` = t.`uid` LEFT JOIN `test_self` t2 ON t2.`id` = t.`pid` LEFT JOIN `my_user` t3 ON t3.`id` = t2.`uid` WHERE t.`uid` = ?",
            "1(String)");
    }

    // 平铺的两列在前（Java 的 @NavigateJoin），DTO 自身的列在后
    [Fact, Upstream("doc/DocTest1.java#testBlog5")]
    public async Task TestBlog5()
    {
        var statement = await Failed(() => Db.Query<TestSelf>().Where(t => t.Uid == "1").SelectAutoInclude<TopicSelfVO>().ToListAsync(Ct));
        SqlOnly.AssertStatement(statement,
            "SELECT t1.`name` AS `uname1`,t3.`name` AS `uname2`,t.`id`,t.`name` FROM `test_self` t LEFT JOIN `my_user` t1 ON t1.`id` = t.`uid` LEFT JOIN `test_self` t2 ON t2.`id` = t.`pid` LEFT JOIN `my_user` t3 ON t3.`id` = t2.`uid` WHERE t.`uid` = ?",
            "1(String)");
    }
}
