using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>对应 Java：QueryTest18.java（单值默认、条件值过滤 + 导航、去重计数、枚举聚合）。</summary>
public sealed partial class QueryTest18(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("QueryTest18.java#test1_0")]
    public async Task Test1_0()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.Id == null).Select(t => new Topic { Id = t.Id ?? "1" }).ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL(t.`id`,?) AS `id` FROM `t_topic` t WHERE t.`id` IS NULL");
        listen.AssertParams("1(String)");
    }

    // Java 的 singleOrDefault(默认值)：C# 写 ?? 默认值
    [Fact, Upstream("QueryTest18.java#test1"), Upstream("QueryTest18.java#test2")]
    public async Task Test1()
    {
        using var listen = SqlCapture.Listen();
        var topic = await Db.Query<Topic>().Where(t => t.Id == null).SingleOrDefaultAsync(Ct) ?? new Topic();
        Assert.Null(topic.Id);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` IS NULL");
        Assert.NotNull(await Db.Query<SysUser>().Where(s => new[] { "1", "2" }.Contains(s.Id!)).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest18.java#test8", NotApplicable = "Java 代理对象的 __getMappingPath（导航路径字符串），C# 直接写属性路径字符串，没有对应 API"),
     Upstream("QueryTest18.java#test8_1", NotApplicable = "同 test8")]
    public void Test8() { }

    // 条件值过滤只作用于外层条件，不进入导航子查询的 lambda（与 Java 一致：子查询里的空串比较保留）
    [Fact, Upstream("QueryTest18.java#test11")]
    public async Task Test11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(s => s.Blogs!.Any(x => x.Content == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t WHERE EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`title` = t.`id` AND t1.`content` = ? LIMIT 1)");
    }

    [Fact, Upstream("QueryTest18.java#test13")]
    public async Task Test13()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(s => (s.Id == "123" ? s.Id : s.Id ?? "1234") == "123xx" && s.Blogs!.Any(x => x.Content == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t WHERE (CASE WHEN t.`id` = ? THEN t.`id` ELSE IFNULL(t.`id`,?) END) = ? AND EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`title` = t.`id` AND t1.`content` = ? LIMIT 1)");
        listen.AssertParams("123(String),1234(String),123xx(String),false(Boolean),(String)");
    }

    // 去重后投影只有一列时计数写成 COUNT(DISTINCT 列)；多列时包一层
    [Fact, Upstream("QueryTest18.java#test16")]
    public async Task Test16()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).Select((t1, b2) => new BlogEntityVO1 { Score = t1.Stars }).Distinct().ToPageAsync(1, 2, Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT COUNT(DISTINCT t.`stars`) FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id`");
        listen.AssertSql(1, "SELECT DISTINCT t.`stars` AS `score` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LIMIT 2");
    }

    [Fact, Upstream("QueryTest18.java#test17")]
    public async Task Test17()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).Select((t, b2) => t).Distinct().ToPageAsync(1, 2, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM (SELECT DISTINCT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id`) t2");
        listen.AssertSql(1, "SELECT DISTINCT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LIMIT 2");
    }

    [Fact, Upstream("QueryTest18.java#test18")]
    public async Task Test18()
    {
        var query = Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id);
        Assert.True(await query.Select((t, b2) => t.Id).Distinct().CountAsync(Ct) > 0);
        Assert.NotEmpty(await query.Select((t, b2) => t).Distinct().Skip(1).Take(20).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest18.java#testMaxEnum")]
    public async Task TestMaxEnum()
    {
        var existing = await Db.Query<TopicTypeTest1>().WhereById("123").FirstOrDefaultAsync(Ct);
        if (existing is null) await Db.Insert(new TopicTypeTest1 { Id = "123", Stars = 1, Title = "t", TopicType = TopicTypeEnum.Classer, CreateTime = DateTime.Now }).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();
        Assert.Equal(TopicTypeEnum.Classer, await Db.Query<TopicTypeTest1>().MaxAsync(x => x.TopicType, Ct));
        listen.AssertSql("SELECT MAX(t.`topic_type`) FROM `t_topic_type` t");
    }

    // Java 对不投影的去重计数写成 SELECT DISTINCT COUNT(*)（实际没有去重）；C# 包一层子查询，计的是去重后的行数
    [Fact, Upstream("QueryTest18.java#textDistinct1", Deviation = "Java 写 SELECT DISTINCT COUNT(*)，等于不去重计数；C# 包一层子查询保证去重后再计数"),
     Upstream("QueryTest18.java#textDistinct2", Deviation = "同 textDistinct1")]
    public async Task TextDistinct1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Distinct().CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) FROM (SELECT DISTINCT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t) t1");
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).Distinct().ToPageAsync(1, 2, Ct);
        listen.AssertSql("SELECT DISTINCT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LIMIT 2");
    }

    /// <summary>对应 Java：MyTestPrimaryKeyGenerator（UUID 字符串主键）。</summary>
    public sealed class GuidStringKey : Metadata.IKeyGenerator
    {
        public object NewKey(Metadata.ColumnMetadata column) => Guid.NewGuid().ToString();
    }

    /// <summary>对应 Java：entity/MyTestPrimaryKey。</summary>
    [Metadata.Table("t_test_p")]
    public sealed class MyTestPrimaryKey
    {
        [Metadata.Column(PrimaryKey = true, KeyGenerator = typeof(GuidStringKey))]
        public string? Id { get; set; }
    }

    // 主键在执行之前生成并写回实体（即使表不存在、插入失败）
    [Fact, Upstream("QueryTest18.java#test111")]
    public async Task Test111()
    {
        var entity = new MyTestPrimaryKey();
        Assert.Null(entity.Id);
        try
        {
            await Db.Insert(entity).ExecuteAsync(Ct);
        }
        catch (EasyQueryException)
        {
            // 表不存在时插入失败，主键仍已生成
        }
        Assert.Equal(36, entity.Id!.Length);
    }

    [Fact, Upstream("QueryTest18.java#test124")]
    public async Task Test124()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id)
            .Select((t, b2) => new BlogEntityVO2 { Id = t.Id, Title = t.Title, Content = b2.Id, Score = b2.Top == true ? 1m : 0m }).ToListAsync(Ct);
        Assert.StartsWith("SELECT t.`id`,t.`title`,t1.`id` AS `content`,", listen.Last.Statement.Text, StringComparison.Ordinal);
    }

    [Fact, Upstream("QueryTest18.java#testxx")]
    public async Task Testxx()
    {
        Assert.NotNull(await Db.FromSql<Topic>("select * from t_topic where id != ? ", "123").Where(t => t.Id != "456").ToPageAsync(1, 2, Ct));
        var joinTable = Db.FromSql<Topic>("select * from t_topic where id != ? ", "123");
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin(joinTable, (b, t2) => b.Id == t2.Id)
            .Where((b1, t2) => b1.CreateTime > DateTime.Now && t2.CreateTime!.Value.ToString("yyyy") == "2014")
            .Select((b1, t2) => new Draft<string?, string?>(b1.Id, t2.Id)).ToListAsync(Ct));
    }

    // Java 的 toChunk / toChunkIf（回调返回 false 时停止）：C# 的 await foreach + ToChunksAsync，break 即停止
    [Theory, Upstream("QueryTest18.java#testChunk1"), Upstream("QueryTest18.java#testChunk2"), Upstream("QueryTest18.java#testChunk3"), Upstream("QueryTest18.java#testChunk4")]
    [InlineData(20)]
    [InlineData(3)]
    public async Task TestChunk1(int size)
    {
        var ids = new HashSet<string>();
        await foreach (var blogs in Db.Query<BlogEntity>().OrderBy(b => b.CreateTime).ThenBy(b => b.Id).ToChunksAsync(size, Ct))
        {
            Assert.True(blogs.Count <= size);
            foreach (var blog in blogs) Assert.True(ids.Add(blog.Id!), "id 重复：" + blog.Id);
        }
        Assert.Equal(100, ids.Count);
    }

    // 流式读取再分块：.NET 10 自带的 AsyncEnumerable.Chunk
    [Fact, Upstream("QueryTest18.java#testStreamChunk1")]
    public async Task TestStreamChunk1()
    {
        var ids = new HashSet<string>();
        await foreach (var blogs in Db.Query<BlogEntity>().AsAsyncEnumerable(Ct).Chunk(20).WithCancellation(Ct))
        {
            Assert.True(blogs.Length <= 20);
            foreach (var blog in blogs) Assert.True(ids.Add(blog.Id!));
        }
        Assert.Equal(100, ids.Count);
    }

    [Fact, Upstream("QueryTest18.java#testResultSizeLimit1")]
    public async Task TestResultSizeLimit1()
    {
        var ex = await Assert.ThrowsAsync<ResultSizeLimitException>(() => Db.Query<BlogEntity>().MaxResultSize(10).ToListAsync(Ct));
        Assert.Equal(10, ex.Limit);
    }

    [Fact, Upstream("QueryTest18.java#testDraft")]
    public async Task TestDraft() =>
        Assert.NotEmpty(await Db.Query<Topic>().Take(100).Where(t => t.Id != "123").Select(t => new Draft<string?, string?>(t.Id, t.Title)).ToListAsync(Ct));
}
