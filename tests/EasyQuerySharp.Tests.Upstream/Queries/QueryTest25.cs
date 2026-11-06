using EasyQuerySharp.Caching;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest25.java（排序里的 CASE、元组投影、原生片段列、Map 投影后再过滤、Part 投影、部分列加别名列、排除列）。
/// Java 的 Select.TUPLE.of(..) 与 Draft 写法生成的 SQL 相同，C# 都用 <c>Draft&lt;…&gt;</c>。
/// 二级缓存的四个用例（Java 的 easyCacheClient，见 0007 §9）在文件末尾。
/// </summary>
public sealed class QueryTest25(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns = "SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ?";

    private const string TopicColumns = "SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` IN ";

    /// <summary>
    /// Java 的 easyCacheClient：同一个库另建一个接上缓存的 EasyDb（写入监听在创建时登记），带 Java BaseTest 里的 BlogPredicateInterceptor；
    /// 存储是每个用例新建的 HybridCache（只有本机层，Java 是 Caffeine + Redis），用例之间互不影响。
    /// </summary>
    private CacheClient NewCache()
    {
        var cache = new EasyCache(CacheSupport.NewHybridCache(), o => o.Cache<BlogEntity>().Cache<Topic>().Cache<TopicInteger>());
        var db = new EasyDb(o => o.UseMySql(Db.Options.DataSource).AddListener(SqlCapture.Instance).AddInterceptor(new BlogPredicateInterceptor()).UseCache(cache));
        return cache.Of(db);
    }

    // Java 只跑不断言：排序里多分支的 CASE
    [Fact, Upstream("QueryTest25.java#testOrderCaseWhen")]
    public async Task TestOrderCaseWhen() =>
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == b.Id).LeftJoin<BlogEntity>((t, b, b1) => t.Id == b.Id)
            .OrderBy((t, b, b1) => b.Title!.Contains("123") ? 1 : b.Content!.Contains("456") ? 2 : b.Title!.Contains("4561") ? 3 : b.Title!.Contains("4561") ? 4 : 5)
            .Select((t, b, b1) => t).ToListAsync(Ct);

    // Java 只跑不断言：投影到 VO 的部分列
    [Fact, Upstream("QueryTest25.java#test11")]
    public async Task Test11() =>
        await Db.Query<Topic>().Where(t => t.Title!.Contains("123")).Select(t => new TopicTypeVO { Id = t.Id, Stars = t.Stars }).ToListAsync(Ct);

    [Fact, Upstream("QueryTest25.java#test12", Deviation = "Java 按表名反查实体元数据（getEntityMetadataList）；C# 的元数据按类型取，这里核对表名")]
    public async Task Test12()
    {
        await Db.Query<Topic>().Where(t => t.Title!.Contains("123")).ToListAsync(Ct);
        Assert.Equal("t_topic", Db.Metadata.Get<Topic>().TableName);
    }

    // 库里没有 test_a 等表：两边都执行到 SQL 为止，报表不存在
    [Fact, Upstream("QueryTest25.java#testToListFlat1", Deviation = "Java 的 toList(flatElement…) 先只查关联键、再逐层带出并在内存里去重；C# 的 SelectMany 按 LINQ 语义写成一条 INNER JOIN（保留重复，去重加 Distinct）")]
    public async Task TestToListFlat1()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<TestA>().Where(t => t.Aname!.Contains("123"))
            .SelectMany(t => t.BList).SelectMany(b => b.CList).Select(c => c.Cname).ToListAsync(Ct));
        Assert.Contains("test_a' doesn't exist", ex.Message);
    }

    [Fact, Upstream("QueryTest25.java#tupleTest1")]
    public async Task TupleTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?>(g.Key)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest2")]
    public async Task TupleTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => new { t.Id, t.Title }).Select(g => new Draft<string?>(g.Key.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`,t.`title`");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest3")]
    public async Task TupleTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?>(g.Key)).Where(d => d.Value1 == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`value1` AS `value1` FROM (SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`) t1 WHERE t1.`value1` = ?");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest4")]
    public async Task TupleTest4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t GROUP BY t.`id`");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest5")]
    public async Task TupleTest5()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long, int?>(g.Key, g.Count(), g.Sum(s => s.Stars))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2`,SUM(t.`stars`) AS `value3` FROM `t_topic` t GROUP BY t.`id`");
        Assert.NotEmpty(list);
    }

    [Fact, Upstream("QueryTest25.java#tupleTest7")]
    public async Task TupleTest7()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?>(t.Id, t.CreateTime)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
        Assert.NotNull(list[0].Value2);
    }

    // 原生片段作为一列：数字 1 按字符串读回
    [Fact, Upstream("QueryTest25.java#tupleTest8")]
    public async Task TupleTest8()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?, string>(t.Id, t.CreateTime, Sql.Raw<string>("1"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2`,1 AS `value3` FROM `t_blog` t WHERE t.`deleted` = ?");
        Assert.Equal("1", list[0].Value3);
    }

    [Fact, Upstream("QueryTest25.java#tupleTest9")]
    public async Task TupleTest9()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Where(t => t.Id == "0")
            .Select(t => new Draft<string?, DateTime?, string>(t.Id, t.CreateTime, Sql.Raw<string>("IFNULL({0},'1')", t.Title))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2`,IFNULL(t.`title`,'1') AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ?");
        Assert.Equal("title0", Assert.Single(list).Value3);
    }

    // Map 投影后再过滤：(string?)d["aa"] 就是派生表的 aa 列（Java 的 o.get("aa") / o.get(MapKey)）
    [Fact, Upstream("QueryTest25.java#testMap"), Upstream("QueryTest25.java#testMap1")]
    public async Task TestMap()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<BlogEntity>().Select(b => new Dictionary<string, object?> { { "aa", b.Id }, { "bb", b.Star } })
            .Where(d => (string?)d["aa"] == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`aa` AS `aa`,t1.`bb` AS `bb` FROM (SELECT t.`id` AS `aa`,t.`star` AS `bb` FROM `t_blog` t WHERE t.`deleted` = ?) t1 WHERE t1.`aa` = ?");
        listen.AssertParams("false(Boolean),123(String)");
        Assert.All(rows, r => Assert.Equal(2, r.Count));
        // Java 的 Select.PART.of(t_blog, title.concat("123")) 后按计算列和实体列过滤
        await Db.Query<BlogEntity>().Select(b => new Part<BlogEntity, string>(b, b.Title + "123")).Where(p => p.Value1 == "123" && p.Entity.Star == 1).ToListAsync(Ct);
    }

    // 部分列 + 别名列：同名不写 AS，content 来自另一张表写 AS
    [Fact, Upstream("QueryTest25.java#testDraft"), Upstream("QueryTest25.java#testDraft2")]
    public async Task TestDraft()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == (string?)(object?)b.Star).Select((t, b) => new BlogEntity { Id = b.Id, Star = b.Star, Content = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id`,t1.`star`,t.`title` AS `content` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`star`");
        listen.AssertParams("false(Boolean)");
    }

    // Java 的 toList(t -> t.id()) 与 selectColumn(..).toList() 结果一致
    [Fact, Upstream("QueryTest25.java#testList")]
    public async Task TestList()
    {
        var list1 = await Db.Query<Topic>().OrderBy(t => t.Id).Select(t => t.Id).ToListAsync(Ct);
        var list2 = (await Db.Query<Topic>().OrderBy(t => t.Id).ToListAsync(Ct)).Select(t => t.Id).ToList();
        Assert.NotEmpty(list1);
        Assert.Equal(list1, list2);
    }

    // Java 的 t_topic.selectIgnores(t_topic.id())：C# 的 SelectExcept
    [Fact, Upstream("QueryTest25.java#testOnlyIgnore"), Upstream("QueryTest25.java#testOnlyIgnore2")]
    public async Task TestOnlyIgnore()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().SelectExcept<Topic>(t => t.Id).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t");
    }

    // Java 的 new TopicProxy().selectIgnores(id)：空投影上排除不影响，等于全部列
    [Fact, Upstream("QueryTest25.java#testOnlyIgnore3")]
    public async Task TestOnlyIgnore3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Select(t => t).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t");
    }

    [Fact, Upstream("QueryTest25.java#xxx22", NotApplicable = "用 Hikari 连本机 3306、替换多个内部服务另建客户端，属于 Java 的启动配置")]
    public void Xxx22() => Assert.NotNull(Db.Metadata.Get<Topic>().Columns.Single(c => c.Name == "id"));

    // ---------- 二级缓存（Java 的 easyCacheClient） ----------

    // kv：没命中时 WHERE 条件 AND id IN (…)，命中不发 SQL；拦截器开关与条件各缓存各的，filter 在内存里筛
    [Fact, Upstream("QueryTest25.java#test11a", Deviation = "SQL 与参数逐字搬；Java 读 Caffeine 里的条件键、行 JSON 与关联条件缓存键的 MD5，是 Java 私有的存储布局，换成行为断言：两种条件下各自缓存了行与查无此行，再查不发 SQL")]
    public async Task Test11a()
    {
        var cache = NewCache();
        await cache.InvalidateAsync<BlogEntity>("1", Ct);
        using (var listen = SqlCapture.Listen())
        {
            Assert.NotNull(await cache.Kv<BlogEntity>().FindAsync("1", Ct));
            listen.AssertSql(BlogColumns + " AND `id` IN (?)");
            listen.AssertParams("false(Boolean),1(String)");
        }
        using (var listen = SqlCapture.Listen())
        {
            Assert.NotNull(await cache.Kv<BlogEntity>().FindAsync("1", Ct));
            Assert.Empty(listen.Events);
        }
        using (var listen = SqlCapture.Listen())
        {
            Assert.Null(await cache.Kv<BlogEntity>().EnableInterceptor("blog-cache").FindAsync("1", Ct));
            listen.AssertSql(BlogColumns + " AND `content` = ? AND `id` IN (?)");
            listen.AssertParams("false(Boolean),123(String),1(String)");
        }
        // Java 读 Caffeine 的 CACHE:BlogEntity:1：无条件的那一项是 id 为 1 的行，带 blog-cache 条件的那一项是查无此行
        using (var listen = SqlCapture.Listen())
        {
            Assert.Equal("1", (await cache.Kv<BlogEntity>().FindAsync("1", Ct))!.Id);
            Assert.Null(await cache.Kv<BlogEntity>().EnableInterceptor("blog-cache").FindAsync("1", Ct));
            Assert.Null(await cache.Kv<BlogEntity>().Filter(b => b.Id != "1").FindAsync("1", Ct));
            Assert.Empty(listen.Events);
        }
        using (var listen = SqlCapture.Listen())
        {
            Assert.Null(await cache.Kv<BlogEntity>().Where(b => b.Content.LikeContains("123xx") && b.Content.LikeContains("123xx4")).FindAsync("2", Ct));
            listen.AssertSql(BlogColumns + " AND `content` LIKE CONCAT('%',?,'%') AND `content` LIKE CONCAT('%',?,'%') AND `id` IN (?)");
            listen.AssertParams("false(Boolean),123xx(String),123xx4(String),2(String)");
        }
        using (var listen = SqlCapture.Listen())
        {
            Assert.Null(await cache.Kv<BlogEntity>().Where(b => b.Content.LikeContains("123xx")).FindAsync("2", Ct));
            listen.AssertSql(BlogColumns + " AND `content` LIKE CONCAT('%',?,'%') AND `id` IN (?)");
            listen.AssertParams("false(Boolean),123xx(String),2(String)");
        }
        // 条件走导航（Java 的 users().flatElement()）：条件的 SQL 与参数同样是缓存键的一部分（Java 对这种条件取整条 SQL 的 MD5 作键）
        using (var listen = SqlCapture.Listen())
        {
            Assert.Null(await cache.Kv<BlogEntity>().Where(b => b.Users!.Any(u => u.Phone == "133333")).FindAsync("2", Ct));
            listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ? AND EXISTS(SELECT 1 FROM `easy-query-test`.`t_sys_user` t1 WHERE t1.`id` = t.`title` AND t1.`phone` = ? LIMIT 1) AND t.`id` IN (?)");
            listen.AssertParams("false(Boolean),Nun4XeChdyK43Ss/oLE6eQ==TAIxYGF+bNgNUIbsBsV2LA==TAIxYGF+bNgNUIbsBsV2LA==(String),2(String)");
        }
        using (var listen = SqlCapture.Listen())
        {
            Assert.Null(await cache.Kv<BlogEntity>().Where(b => b.Users!.Any(u => u.Phone == "133333")).FindAsync("2", Ct));
            Assert.Empty(listen.Events);
        }
    }

    // all：取一行只查这一行；toList 先查主键索引，再一条 IN 查询取行
    [Fact, Upstream("QueryTest25.java#test11a1", Deviation = "IN 的参数是索引的顺序（Java 是 HashSet 的顺序），断言个数与集合相等（Java 的库里多一条 id 为 995 的主题，共 101 个）；Java 读 Caffeine 里的行与索引 JSON 是 Java 私有的存储布局，换成行为断言：再取行与主键都不发 SQL")]
    public async Task Test11a1()
    {
        var cache = NewCache();
        await cache.InvalidateAsync<Topic>("1", Ct);
        using (var listen = SqlCapture.Listen())
        {
            Assert.NotNull(await cache.All<Topic>().FindAsync("1", Ct));
            listen.AssertSql(TopicColumns + "(?)");
            listen.AssertParams("1(String)");
        }

        // Java 删掉 CACHE:Topic:1 与 CACHE:Topic:INDEX：按主键失效时这一行与索引一起删
        await cache.InvalidateAsync<Topic>("1", Ct);
        var ids = await Db.Query<Topic>().Select(t => t.Id!).ToListAsync(Ct);
        using (var listen = SqlCapture.Listen())
        {
            var list = await cache.All<Topic>().ToListAsync(Ct);
            Assert.Equal(2, listen.Events.Count);
            listen.AssertSql(0, "SELECT t.`id` FROM `t_topic` t");
            var rows = listen.Events[1].Statement;
            Assert.Equal(TopicColumns + "(" + string.Join(",", ids.Select(_ => "?")) + ")", rows.Text);
            Assert.Equal(ids.Order(StringComparer.Ordinal), rows.Parameters.Select(p => (string)p.Value!).Order(StringComparer.Ordinal));
            Assert.Equal(ids.Count, list.Count);
        }

        // Java 读 Caffeine 里的行（CACHE:Topic:1）与索引（CACHE:Topic:INDEX）；之后的 filter 不查库
        using (var listen = SqlCapture.Listen())
        {
            Assert.Equal("1", (await cache.All<Topic>().FindAsync("1", Ct))!.Id);
            Assert.Equal(ids.Order(StringComparer.Ordinal), (await cache.All<Topic>().ToIdsAsync(Ct)).Cast<string>().Order(StringComparer.Ordinal));
            Assert.NotNull(await cache.All<Topic>().Filter(t => t.Id == "1").FindAsync("1", Ct));
            Assert.Null(await cache.All<Topic>().Filter(t => t.Id != "1").FindAsync("1", Ct));
            Assert.NotNull(await cache.All<Topic>().Filter(t => t.Id == "1").FindAsync("1", Ct));
            Assert.Empty(listen.Events);
        }
    }

    // 同表 t_topic、Long 主键的实体：索引里的主键转成 long，字符串 "88" 也按主键类型转换
    [Fact, Upstream("QueryTest25.java#topicIntegerTest")]
    public async Task TopicIntegerTest()
    {
        var cache = NewCache();
        var list = await cache.All<TopicInteger>().ToListAsync(Ct);
        Assert.Equal(await Db.Query<Topic>().CountAsync(Ct), list.Count);
        Assert.Equal(88L, (await cache.All<TopicInteger>().FindAsync("88", Ct))!.Id);
    }

    // Java 只跑不断言：all 的分页（带条件与不带条件），以及一条无关的 EXISTS 投影
    [Fact, Upstream("QueryTest25.java#xxx")]
    public async Task Xxx()
    {
        var cache = NewCache();
        var page = await cache.All<Topic>().Where(o => o.Title.LikeContains("123")).ToPageAsync(1, 2, Ct);
        var page1 = await cache.All<Topic>().ToPageAsync(1, 2, Ct);
        Assert.Empty(page.Rows);
        Assert.Equal(2, page1.Rows.Count);
        var list = await Db.Query<Topic>().Select(t => new Draft<bool>(Sql.Exists(Db.Query<BlogEntity>().Where(b => b.Title == t.Title)))).ToListAsync(Ct);
        Assert.NotEmpty(list);
    }
}
