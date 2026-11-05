namespace EasyQuerySharp.Tests.Upstream.Queries;

// QueryTest.java 里的子查询列、NOT IN 子查询、union 的复杂分支、asTable 函数用例
public sealed partial class QueryTest
{
    [Fact, Upstream("QueryTest.java#query61")]
    public async Task Query61()
    {
        var count = await Db.Query<Topic>().CountAsync(Ct);
        var ids = Db.Query<BlogEntity>().Where(o => o.Id == "1").Select(o => o.Id);
        var list = await Db.Query<Topic>().Where(o => o.Id.NotIn(ids)).ToListAsync(Ct);
        Assert.Equal(count - 1, list.Count);
    }

    // Java 断言 99 行，依赖其它用例改过的库内数据；这里断言与 title 非空的行数一致
    private Task<long> TitledTopicCount() => Db.Query<Topic>().Where(t => t.Title != null).CountAsync(Ct);

    [Fact, Upstream("QueryTest.java#query63")]
    public async Task Query63()
    {
        var query = Db.Query<Topic>().Where(t => t.Title != null)
            .Select(o => new TopicSubQueryBlog
            {
                Id = o.Id, Stars = o.Stars, Title = o.Title, CreateTime = o.CreateTime,
                BlogCount = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => Sql.Count(x.Id))),
            });
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`,(SELECT COUNT(t1.`id`) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL", query.ToSql());
        var list = await query.ToListAsync(Ct);
        Assert.Equal(await TitledTopicCount(), list.Count);
        Assert.All(list, x => Assert.Equal(x.Stars < 200 ? 1 : 0, x.BlogCount));
    }

    [Fact, Upstream("QueryTest.java#query63_1")]
    public async Task Query63_1()
    {
        var query = Db.Query<Topic>().Where(t => t.Title != null)
            .Select(o => new TopicSubQueryBlog
            {
                Id = o.Id, Stars = o.Stars, Title = o.Title, CreateTime = o.CreateTime,
                BlogCount = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => (long?)Sql.Sum(x.Star))),
            });
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`,(SELECT SUM(t1.`star`) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL", query.ToSql());
        var list = await query.ToListAsync(Ct);
        Assert.Equal(await TitledTopicCount(), list.Count);
        Assert.All(list, x =>
        {
            if (x.Stars < 200) Assert.Equal(long.Parse(x.Id!), x.BlogCount);
            else Assert.Null(x.BlogCount);
        });
    }

    [Fact, Upstream("QueryTest.java#query64")]
    public async Task Query64()
    {
        var query = Db.Query<Topic>().Where(t => t.Title != null)
            .Select(o => new TopicSubQueryBlog
            {
                Id = o.Id, Stars = o.Stars, Title = o.Title,
                BlogCount = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => Sql.Count(x.Id))),
            });
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,(SELECT COUNT(t1.`id`) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL", query.ToSql());
        Assert.Equal(await TitledTopicCount(), (await query.ToListAsync(Ct)).Count);
    }

    // Java 在 toList 之后再 toSQL，同一批查询对象的别名编号会继续往后数；C# 的查询不可变，每次都从 t 开始连续编号
    [Fact, Upstream("QueryTest.java#query65", Deviation = "别名编号：Java 复用了执行过的查询对象，外层别名是 t7；C# 连续编号为 t4")]
    public async Task Query65()
    {
        var q1 = Db.Query<Topic>().Where(o => o.Id == "123");
        var q2 = Db.Query<Topic>().Where(o => o.CreateTime >= new DateTime(2020, 1, 1, 1, 1, 0));
        var q3 = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t1.Content != null && t.Stars != null).Select((t, t1) => t);
        var query = q1.Union(q2, q3).Where(o => o.Id == "123321");
        Assert.Empty(await query.ToListAsync(Ct));
        Assert.Equal("SELECT t4.`id`,t4.`stars`,t4.`title`,t4.`create_time` FROM ( (SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE t.`id` = ?)  UNION  (SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time` FROM `t_topic` t1 WHERE t1.`create_time` >= ?)  UNION  (SELECT t2.`id`,t2.`stars`,t2.`title`,t2.`create_time` FROM `t_topic` t2 LEFT JOIN `t_blog` t3 ON t3.`deleted` = ? AND t2.`id` = t3.`id` WHERE t3.`content` IS NOT NULL AND t2.`stars` IS NOT NULL) ) t4 WHERE t4.`id` = ?",
            query.ToSql());
    }

    [Fact, Upstream("QueryTest.java#query66", Deviation = "别名编号：Java 的分支与外层是 t、t2、t4/t5、t8；C# 连续编号为 t、t1、t2/t3、t4")]
    public async Task Query66()
    {
        const string id = "123";
        var q1 = Db.Query<Topic>().WhereIf(!string.IsNullOrWhiteSpace(id), t => t.Id == id).Select<TopicUnion>();
        var q2 = Db.Query<Topic>().Where(o => o.CreateTime >= new DateTime(2020, 1, 1, 1, 1, 0)).Select<TopicUnion>();
        var q3 = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t1.Content != null && t.Stars != null)
            .Select((t, t1) => new TopicUnion { Id = t.Id, Stars = t.Stars, Title = t.Title });
        var query = q1.Union(q2, q3).Where(o => o.Id == "123321");
        Assert.Empty(await query.ToListAsync(Ct));
        Assert.Equal("SELECT t4.`id`,t4.`stars`,t4.`abc` AS `abc` FROM ( (SELECT t.`id`,t.`stars`,t.`title` AS `abc` FROM `t_topic` t WHERE t.`id` = ?)  UNION  (SELECT t1.`id`,t1.`stars`,t1.`title` AS `abc` FROM `t_topic` t1 WHERE t1.`create_time` >= ?)  UNION  (SELECT t2.`id`,t2.`stars`,t2.`title` AS `abc` FROM `t_topic` t2 LEFT JOIN `t_blog` t3 ON t3.`deleted` = ? AND t2.`id` = t3.`id` WHERE t3.`content` IS NOT NULL AND t2.`stars` IS NOT NULL) ) t4 WHERE t4.`id` = ?",
            query.ToSql());
    }

    [Fact, Upstream("QueryTest.java#query67", Deviation = "别名编号：Java 的外层别名是 t5（中间跳号），C# 连续编号为 t2；Java 的 proxy set 同名列也写 AS，C# 成员初始化同名列不写")]
    public async Task Query67()
    {
        var q1 = Db.Query<Topic>().Where(o => o.Id == "123").Select<TopicUnion>();
        var q2 = Db.Query<BlogEntity>().Where(b => b.CreateTime >= new DateTime(2020, 1, 1, 1, 1, 0))
            .Select(b => new TopicUnion { Id = b.Id, Stars = b.Star, Title = b.Content });
        Assert.Empty(await q1.UnionAll(q2).Where(o => o.Id == "123321").ToListAsync(Ct));
        Assert.Equal("SELECT t2.`id`,t2.`stars`,t2.`abc` AS `abc` FROM ( (SELECT t.`id`,t.`stars`,t.`title` AS `abc` FROM `t_topic` t WHERE t.`id` = ?)  UNION  (SELECT t1.`id`,t1.`star` AS `stars`,t1.`content` AS `abc` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`create_time` >= ?) ) t2 WHERE t2.`id` = ?",
            q1.Union(q2).Where(o => o.Id == "123321").ToSql());
    }

    [Fact, Upstream("QueryTest.java#query24")]
    public void Query24() =>
        Assert.Equal("SELECT " + BlogColumns + " FROM `aa_bb_cc` WHERE `deleted` = ? AND `id` = ?",
            Db.Query<BlogEntity>().AsTable(_ => "aa_bb_cc").Where(o => o.Id == "123").ToSql());

    [Fact, Upstream("QueryTest.java#query25")]
    public void Query25() =>
        Assert.Equal("SELECT " + BlogColumns + " FROM `aa_bb_cc1` WHERE `deleted` = ? AND `id` = ?",
            Db.Query<BlogEntity>().AsTable(name => name == "t_blog" ? "aa_bb_cc1" : "xxx").Where(o => o.Id == "123").ToSql());

    [Fact, Upstream("QueryTest.java#query81")]
    public async Task Query81()
    {
        using var listen = SqlCapture.Listen();
        await Assert.ThrowsAsync<EasyQueryException>(() =>
            Db.Query<BlogEntity>().AsTable("t_12345").Where(o => o.Id == "2").Select<BlogEntity>().Distinct().CountAsync(Ct));
        listen.AssertSql("SELECT COUNT(*) FROM (SELECT DISTINCT " + Prefixed("t", BlogColumns) + " FROM `t_12345` t WHERE t.`deleted` = ? AND t.`id` = ?) t1");
    }

    [Fact, Upstream("QueryTest.java#query14")]
    public async Task Query14()
    {
        var page = await Db.Query<Topic>()
            .InnerJoin<BlogEntity>((t, b2) => t.Id == b2.Id)
            .Where((t, b2) => b2.Title != null)
            .GroupBy((t, b2) => b2.Id)
            .Select(g => new BlogEntity { Id = g.Key, Score = g.Sum(x => x.Item2.Score) })
            .ToPageAsync(1, 20, Ct);
        Assert.Equal(100, page.Total);
        Assert.Equal(20, page.Rows.Count);
    }
}
