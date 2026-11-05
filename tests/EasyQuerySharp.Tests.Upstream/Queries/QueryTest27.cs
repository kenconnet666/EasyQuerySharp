namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest27.java（随机排序、自定义模板函数、对一导航、Map 投影的值类型、裸查询作为 JOIN 来源、原生片段）。
/// Java 里用 rawSQLCommand 封装的静态函数，C# 写成标了 <c>[SqlFunction("…")]</c> 的静态方法。
/// </summary>
public sealed class QueryTest27(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogSelect = "SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ?";

    [Fact, Upstream("QueryTest27.java#testRandom")]
    public async Task TestRandom()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().OrderBy(t => Sql.Random()).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` ORDER BY RAND() ASC");
    }

    [Fact, Upstream("QueryTest27.java#testAAA1")]
    public async Task TestAAA1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(o => o.MyBlog!.Star == 1).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t1.`id` = t.`id` WHERE t1.`star` = ?");
        listen.AssertParams("false(Boolean),1(Integer)");
    }

    // Java 只跑不断言：带条件的查询再连接两次
    [Fact, Upstream("QueryTest27.java#testaaaa54"), Upstream("QueryTest27.java#testaaaa541")]
    public async Task Testaaaa54()
    {
        await Db.Query<BlogEntity>().Where(b => b.Title == "123").LeftJoin<BlogEntity>((a, b) => a.Id == b.Title)
            .LeftJoin<BlogEntity>((a, b, c) => c.Id == b.Title).Select((a, b, c) => a).ToListAsync(Ct);
        await Db.Query<BlogEntity>().LeftJoin<BlogEntity>((a, b) => a.Id == b.Title).LeftJoin<BlogEntity>((a, b, c) => c.Id == b.Title)
            .Where((a, b, c) => c.Id == b.Title).Select((a, b, c) => a).ToListAsync(Ct);
    }

    // 声明了分组合并的导航上计数：t.star <= IFNULL(计数列,0)
    [Fact, Upstream("QueryTest27.java#testCountSubQueryCount")]
    public async Task TestCountSubQueryCount()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().GroupJoin(b => b.Users).Where(b => b.Star <= b.Users!.Count).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN (SELECT t1.`id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `easy-query-test`.`t_sys_user` t1 GROUP BY t1.`id`) t2 ON t2.`__group_key1__` = t.`title` WHERE t.`deleted` = ? AND  t.`star` <= IFNULL(t2.`__count2__`,0)");
        listen.AssertParams("false(Boolean)");
    }

    /// <summary>对应 Java 用例里的 findInSet：返回条件的模板函数。</summary>
    [SqlFunction("FIND_IN_SET({0},{1})")]
    private static bool FindInSet(string value, string? set) => throw new InvalidOperationException("只能在查询里使用");

    /// <summary>对应 Java 用例里的 subStr：返回值的模板函数。</summary>
    [SqlFunction("SUBSTR({0},{1},{2})")]
    private static string SubStr(string? column, int begin, int end) => throw new InvalidOperationException("只能在查询里使用");

    [Fact, Upstream("QueryTest27.java#staticTestCustomFunction")]
    public async Task StaticTestCustomFunction()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => FindInSet("123", b.Content)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND FIND_IN_SET(?,`content`)");
        listen.AssertParams("false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest27.java#staticTestCustomFunction2")]
    public async Task StaticTestCustomFunction2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => FindInSet("123", SubStr(b.Title, 1, 2))).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND FIND_IN_SET(?,SUBSTR(`title`,?,?))");
        listen.AssertParams("false(Boolean),123(String),1(Integer),2(Integer)");
    }

    [Fact, Upstream("QueryTest27.java#staticTestCustomFunction3")]
    public async Task StaticTestCustomFunction3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => FindInSet("123", b.Title ?? "123")).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND FIND_IN_SET(?,IFNULL(`title`,?))");
        listen.AssertParams("false(Boolean),123(String),123(String)");
    }

    // Java 的 rawSQLStatement(..).asStr()：C# 的 Sql.Raw<string>
    [Fact, Upstream("QueryTest27.java#staticTestCustomFunction4")]
    public async Task StaticTestCustomFunction4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(it => Sql.Raw<string>("SUBSTR({0},{1},{2})", it.IdCard, 1, 2) == "312345").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE SUBSTR(`id_card`,?,?) = ?");
        listen.AssertParams("1(Integer),2(Integer),312345(String)");
    }

    // Map 里的值按列类型读出（Java 的 ValueTypeMode.TRY_TYPE）
    [Fact, Upstream("QueryTest27.java#testMap")]
    public async Task TestMap()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<Topic>().Select(t => new Dictionary<string, object?> { { "a1", t.CreateTime }, { "a2", t.CreateTime ?? new DateTime(2025, 1, 1) } }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`create_time` AS `a1`,IFNULL(t.`create_time`,?) AS `a2` FROM `t_topic` t");
        listen.AssertParams("2025-01-01T00:00(LocalDateTime)");
        Assert.All(rows, r => Assert.IsType<DateTime>(r["a2"]));
    }

    [Fact, Upstream("QueryTest27.java#testMap2")]
    public async Task TestMap2()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<Topic>().Select<Dictionary<string, object?>>().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t");
        Assert.All(rows.Where(r => r["create_time"] is not null), r => Assert.IsType<DateTime>(r["create_time"]));
    }

    // 没有任何条件的查询作为 JOIN 来源：直接连接表，不包派生表
    [Fact, Upstream("QueryTest27.java#testSave1")]
    public async Task TestSave1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin(Db.Query<SysUser>(), (t, u) => t.Id == u.Id).LeftJoin(Db.Query<SysUser>(), (a, v, c) => a.Id == c.Id)
            .Select((a, v, c) => a).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `easy-query-test`.`t_sys_user` t1 ON t.`id` = t1.`id` LEFT JOIN `easy-query-test`.`t_sys_user` t2 ON t.`id` = t2.`id`");
    }

    // Java 只跑不断言：排序、投影里的原生片段
    [Fact, Upstream("QueryTest27.java#updateTable")]
    public async Task UpdateTable()
    {
        await Db.Query<Topic>().Where(b => b.Id == "123").OrderByRaw("RAND()").ToListAsync(Ct);
        await Db.Query<Topic>().Where(b => b.Id == "123").OrderByRaw("IFNULL(`stars`,1) DESC").OrderByRaw("RAND()").ToListAsync(Ct);
        await Db.Query<Topic>().Where(b => b.Id == "123").Select(t => new Draft<double, int>(Sql.Raw<double>("RAND()"), Sql.Raw<int>("IFNULL({0},{1})", t.Stars, 1))).ToListAsync(Ct);
    }

    // Java 的 dbContext.topic() 是测试里对 queryable 的包装
    [Fact, Upstream("QueryTest27.java#testQuery")]
    public async Task TestQuery()
    {
        using (var listen = SqlCapture.Listen())
        {
            await Db.Query<Topic>().Where(s => s.Id == "123").ToListAsync(Ct);
            listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ?");
            listen.AssertParams("123(String)");
        }
        using (var listen = SqlCapture.Listen())
        {
            await Db.Query<Topic>().LeftJoin(Db.Query<BlogEntity>(), (a, b) => a.Id == b.Id).Where((t, b) => t.Id == "123").Select((t, b) => t).ToListAsync(Ct);
            listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?");
            listen.AssertParams("false(Boolean),123(String)");
        }
    }
}
