namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest28.java（按名字选列、查询过滤拦截器与关闭、JSON 列上的 LIKE、IFNULL 的字面量默认值、不分组的聚合与带条件的聚合）。
/// Java 的 nullOrDefaultZero() / nullOrDefaultFormat(1) 把默认值写成字面量：C# 的 <c>x ?? Sql.Raw&lt;int&gt;("0")</c>。
/// </summary>
public sealed class QueryTest28(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 按运行时的列名映射动态选列：C# 投影的属性在编译期写出，列按名字取用 Sql.Column
    [Fact, Upstream("QueryTest28.java#test", Deviation = "C# 投影的属性要在编译期写出，按名字取列用 Sql.Column")]
    public async Task Test()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new BlogEntity
        {
            Id = Sql.Column<string>(b, "id"), Content = Sql.Column<string>(b, "title"), Title = Sql.Column<string>(b, "content"),
        }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title` AS `content`,t.`content` AS `title` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    // TestInterceptor：m8_interceptor 的查询追加 1=1，m8_interceptor2 追加 2=2
    [Fact, Upstream("QueryTest28.java#testInterceptor1")]
    public async Task TestInterceptor1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<InterceptorEntity>().ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`name` FROM `m8_interceptor` WHERE 1=1");
    }

    // Java 的 noInterceptor()：DisableInterceptors()，同样作用于导航的子查询
    [Fact, Upstream("QueryTest28.java#testInterceptor2")]
    public async Task TestInterceptor2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<InterceptorEntity>().DisableInterceptors().Where(i => i.List!.Any()).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_interceptor` t WHERE EXISTS(SELECT 1 FROM `m8_interceptor2` t1 WHERE t1.`aid` = t.`id` LIMIT 1)");
    }

    [Fact, Upstream("QueryTest28.java#testInterceptor3")]
    public async Task TestInterceptor3()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<InterceptorEntity>().DisableInterceptors().Include(i => i.List).ToListAsync(Ct);
        listen.AssertSql(0, "SELECT `id`,`name` FROM `m8_interceptor`");
        listen.AssertSql(1, "SELECT t.`id`,t.`aid`,t.`name` FROM `m8_interceptor2` t WHERE t.`aid` IN (?)");
        listen.AssertParams(1, "1(String)");
        Assert.Single(Assert.Single(list).List!);
    }

    // JSON 列：(string)(object)x 只改类型；Java 的 contains / startsWith / endsWith 是在 SQL 里拼 % 的 Sql.LikeContains 等；
    // likeRaw 不经值转换器；toStr() 是 CAST(.. AS CHAR)
    [Fact, Upstream("QueryTest28.java#testNoValueConverter1", Deviation = "JSON 参数按 System.Text.Json 的属性声明顺序序列化（name 在前），Java 按字母序（age 在前）")]
    public async Task TestNoValueConverter1()
    {
        using var listen = SqlCapture.Listen();
        var single = new List<TopicTypeJsonValue> { new() { Name = "123" } };
        var value = new TopicTypeJsonValue { Age = 1, Name = "1" };
        await Db.Query<TopicTypeArrayJson>()
            .Where(o => ((string)(object)o.Title2!).Contains("123")
                        && Sql.LikeContains((string)(object)o.Title2!, "123")
                        && Sql.LikeStartsWith((string)(object)o.Title2!, "123")
                        && Sql.LikeEndsWith((string)(object)o.Title2!, "123")
                        && o.Title2 == single
                        && ((string)(object)o.Title!).Contains("456")
                        && o.Title == value
                        && Sql.LikeContains(Sql.Cast<string>(o.Title), "123"))
            .OrderBy(o => o.Title2).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`title2`,`topic_type`,`create_time` FROM `t_topic_type_array` WHERE `title2` LIKE ? AND `title2` LIKE CONCAT('%',?,'%') AND `title2` LIKE CONCAT(?,'%') AND `title2` LIKE CONCAT('%',?) AND `title2` = ? AND `title` LIKE ? AND `title` = ? AND CAST(`title` AS CHAR) LIKE CONCAT('%',?,'%') ORDER BY `title2` ASC");
        listen.AssertParams("%123%(String),123(String),123(String),123(String),[{\"name\":\"123\",\"age\":null}](String),%456%(String),{\"name\":\"1\",\"age\":1}(String),123(String)");
    }

    [Fact, Upstream("QueryTest28.java#testIncrementNullZero")]
    public async Task TestIncrementNullZero()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<Topic>().Set(t => new Topic { Stars = (t.Stars ?? 0) + 1 }).WhereById("5xxxx1").ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `stars` = (IFNULL(`stars`,?) + ?) WHERE `id` = ?");
        listen.AssertParams("0(Integer),1(Integer),5xxxx1(String)");
    }

    [Fact, Upstream("QueryTest28.java#testQueryNullOrDefault")]
    public async Task TestQueryNullOrDefault()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => (t.Stars ?? Sql.Raw<int>("0")) == 1).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE IFNULL(`stars`,0) = ?");
        listen.AssertParams("1(Integer)");
    }

    [Fact, Upstream("QueryTest28.java#testQueryNullOrDefault2")]
    public async Task TestQueryNullOrDefault2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => (t.Stars ?? Sql.Raw<int>("1")) == 1).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE IFNULL(`stars`,1) = ?");
        listen.AssertParams("1(Integer)");
    }

    // Java 的 groupBy() 不带键：整个结果作为一组，C# 直接在投影里写 Sql.Count() / Sql.Avg(..)
    [Fact, Upstream("QueryTest28.java#testQueryAggregate2")]
    public async Task TestQueryAggregate2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupJoinAll().Where(t => Sql.LikeContains(t.Title, "123"))
            .Select(t => new Draft<long, decimal?>(Sql.Count(), Sql.Avg((decimal?)t.Stars))).ToListAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) AS `value1`,AVG(t.`stars`) AS `value2` FROM `t_topic` t WHERE t.`title` LIKE CONCAT('%',?,'%')");
        listen.AssertParams("123(String)");
    }

    private const string DeviationElseZero = "Java 的 where(..).sumBigDecimal 里 ELSE 的 0 是 Integer，分组上的 sum(..).filter 是 Long；C# 只有 Sql.Sum(x, 条件) 一个入口，统一用 Long";

    // Java 的 where(..).sumBigDecimal(..)：带条件的聚合 Sql.Sum(x, 条件)
    [Fact, Upstream("QueryTest28.java#testQueryAggregate", Deviation = DeviationElseZero), Upstream("QueryTest28.java#testQueryAggregate1", Deviation = DeviationElseZero)]
    public async Task TestQueryAggregate()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupJoinAll().AsTracking().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id)
            .Where((t, _) => t.Title != null && t.CreateTime <= new DateTime(2021, 3, 4, 5, 6, 0))
            .Select((t, _) => new Draft<long, decimal?>(Sql.Count(), Sql.Sum((decimal?)t.Stars, Sql.LikeContains(t.Title, "123")))).ToListAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) AS `value1`,SUM((CASE WHEN t.`title` LIKE CONCAT('%',?,'%') THEN t.`stars` ELSE ? END)) AS `value2` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`title` IS NOT NULL AND t.`create_time` <= ?");
        listen.AssertParams("123(String),0(Long),false(Boolean),2021-03-04T05:06(LocalDateTime)");
    }

    // Java 的 mapQueryable("t_topic") / t.get("id")：db.Table(..) / Get<T>("id")
    [Fact, Upstream("QueryTest28.java#testQueryMap")]
    public async Task TestQueryMap()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Table("t_topic").Where(t => t.Get<string>("id") == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT * FROM `t_topic` WHERE `id` = ?");
        listen.AssertParams("123(String)");
        Assert.All(rows, row => Assert.Equal("123", row.Get<string>("id")));
    }

    /// <summary>对应 Java：QueryTest28.TypeA（没有 @Table 的类，用 asTable 指定表）。</summary>
    public sealed class TypeA
    {
        public string? Id { get; set; }
    }

    // Java 的 queryable(ClassProxy.of(TypeA.class)).asTable(..)：没有 [Table] 的类同样能查，AsTable 指定表名
    [Fact, Upstream("QueryTest28.java#testQueryMap2")]
    public async Task TestQueryMap2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TypeA>().AsTable("t_topic").Where(t => t.Id == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT `id` FROM `t_topic` WHERE `id` = ?");
        listen.AssertParams("123(String)");
    }
}
