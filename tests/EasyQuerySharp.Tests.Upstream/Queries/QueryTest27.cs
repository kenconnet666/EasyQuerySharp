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

    // ---------- 导航上的表配置：Java 的 nav().configure(r -> r.disableLogicDelete() / r.asTable(..)) ----------
    // 对一导航写成 Sql.DisableLogicDelete(nav) / Sql.AsTable(nav, ..)，集合导航写成 nav.DisableLogicDelete() / nav.AsTable(..)。
    // 改了表名的用例库里没有那张表（Java 用 try/catch 吞掉执行错误），这里只生成语句比对

    private static void AssertStatement(SqlStatement statement, string sql, string parameters)
    {
        Assert.Equal(sql, statement.Text);
        Assert.Equal(parameters, JavaParams.Format(statement));
    }

    [Fact, Upstream("QueryTest27.java#testAAA")]
    public async Task TestAAA()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(o => Sql.DisableLogicDelete(o.MyBlog)!.Star == 1).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t LEFT JOIN `t_blog` t1 ON t1.`id` = t.`id` WHERE t1.`star` = ?");
        listen.AssertParams("1(Integer)");
    }

    [Fact, Upstream("QueryTest27.java#testAAA2")]
    public void TestAAA2() =>
        AssertStatement(Db.Query<SysUser>().Where(o => Sql.AsTable(o.MyBlog, "BlogAbc")!.Star == 1).ToStatement(), "SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t LEFT JOIN `BlogAbc` t1 ON t1.`deleted` = ? AND t1.`id` = t.`id` WHERE t1.`star` = ?", "false(Boolean),1(Integer)");

    [Fact, Upstream("QueryTest27.java#testAAA4")]
    public void TestAAA4() =>
        AssertStatement(Db.Query<SysUser>().Where(o => Sql.AsTable(o.MyBlog, "BlogAbc")!.Star == 1 && o.Blogs!.AsTable("BInner").Any(x => x.Star == 1)).ToStatement(),
            "SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t LEFT JOIN `BlogAbc` t1 ON t1.`deleted` = ? AND t1.`id` = t.`id` WHERE t1.`star` = ? AND EXISTS(SELECT 1 FROM `BInner` t2 WHERE t2.`deleted` = ? AND t2.`title` = t.`id` AND t2.`star` = ? LIMIT 1)", "false(Boolean),1(Integer),false(Boolean),1(Integer)");

    // Java 的 ALL_SUB_QUERY_GROUP_JOIN：GroupJoinAll()；表配置带进分组合并的派生表
    [Fact, Upstream("QueryTest27.java#testAAA42"), Upstream("QueryTest27.java#testAAA43")]
    public void TestAAA42() =>
        AssertStatement(Db.Query<SysUser>().GroupJoinAll()
                .Where(o => Sql.AsTable(o.MyBlog, "BlogAbc")!.Star == 1 && o.Blogs!.AsTable("BInner").Any(x => x.Star == 1)).ToStatement(),
            "SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t LEFT JOIN `BlogAbc` t1 ON t1.`deleted` = ? AND t1.`id` = t.`id` LEFT JOIN (SELECT t2.`title` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `BInner` t2 WHERE t2.`deleted` = ? AND t2.`star` = ? GROUP BY t2.`title`) t3 ON t3.`__group_key1__` = t.`id` WHERE t1.`star` = ? AND IFNULL(t3.`__any2__`,?) = ?", "false(Boolean),1(Integer),false(Boolean),1(Integer),1(Integer),false(Boolean),true(Boolean)");

    // Java 的 blogs().mode(SUB_QUERY_ONLY)：GroupJoinIf(false, ..) 让这个导航不参与 GroupJoinAll
    [Fact, Upstream("QueryTest27.java#testAAA45")]
    public void TestAAA45() =>
        AssertStatement(Db.Query<SysUser>().GroupJoinAll().GroupJoinIf(false, o => o.Blogs)
                .Where(o => Sql.AsTable(o.MyBlog, "BlogAbc")!.Star == 1 && o.Blogs!.AsTable("BInner").Any(x => x.Star == 1)).ToStatement(),
            "SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t LEFT JOIN `BlogAbc` t1 ON t1.`deleted` = ? AND t1.`id` = t.`id` WHERE t1.`star` = ? AND EXISTS(SELECT 1 FROM `BInner` t2 WHERE t2.`deleted` = ? AND t2.`title` = t.`id` AND t2.`star` = ? LIMIT 1)", "false(Boolean),1(Integer),false(Boolean),1(Integer)");

    // 导航上标了 GroupJoin = true（Java 的 subQueryToGroupJoin）：多对多的 Any 自动合并成分组 JOIN，主表的键条件下推到分组里
    [Fact, Upstream("QueryTest27.java#autoMergeMany2Many1")]
    public void AutoMergeMany2Many1() =>
        AssertStatement(Db.Query<Station>().Where(s => s.StationId == "123" && s.Operators!.Any(x => x.TenantOperatorId == "1234")).ToStatement(),
            "SELECT t.`station_id`,t.`station_name` FROM `t_station` t LEFT JOIN (SELECT t2.`station_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_tenant_operator` t1 INNER JOIN `t_station_tenant` t2 ON t1.`tenant_operator_id` = t2.`tenant_id` WHERE t1.`tenant_operator_id` = ? AND t2.`station_id` = ? GROUP BY t2.`station_id`) t4 ON t4.`__group_key1__` = t.`station_id` WHERE t.`station_id` = ? AND IFNULL(t4.`__any2__`,?) = ?", "1(Integer),1234(String),123(String),123(String),false(Boolean),true(Boolean)");

    [Fact, Upstream("QueryTest27.java#autoMergeMany2Many2")]
    public void AutoMergeMany2Many2() =>
        AssertStatement(Db.Query<Station>().Where(s => s.Operators!.Any(x => x.TenantOperatorId == "1234")).ToStatement(),
            "SELECT t.`station_id`,t.`station_name` FROM `t_station` t LEFT JOIN (SELECT t2.`station_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_tenant_operator` t1 INNER JOIN `t_station_tenant` t2 ON t1.`tenant_operator_id` = t2.`tenant_id` WHERE t1.`tenant_operator_id` = ? GROUP BY t2.`station_id`) t4 ON t4.`__group_key1__` = t.`station_id` WHERE IFNULL(t4.`__any2__`,?) = ?", "1(Integer),1234(String),false(Boolean),true(Boolean)");

    // Java 的 MapKeyModeEnum.FIELD_NAME（Map 的键用属性名）：C# 的 Map 投影键是列名，要属性名作键时投影成 DTO
    [Fact, Upstream("QueryTest27.java#selectAllMap", Deviation = "C# 的 Sql.Columns 以列名作键，不写 AS；没有按属性名作键的模式")]
    public async Task SelectAllMap()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<BlogEntity>().OrderBy(o => Sql.Random()).Select(b => new Dictionary<string, object?>(Sql.Columns(b))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ? ORDER BY RAND() ASC");
        Assert.All(rows, row => Assert.NotNull(row["create_time"]));
    }

    // Java 的 getTrackEntityStateNotNull(..).getEntityValueState("title").isChanged()：GetTrackedChanges 列出变化的属性
    [Fact, Upstream("QueryTest27.java#testDiff")]
    public void TestDiff()
    {
        using var tracking = Db.BeginTracking();
        var topic = new Topic { Id = "123", Title = "456" };
        Db.AddTracking(topic);
        topic.Stars = 1;
        topic.Title = "567";
        Assert.Contains(nameof(Topic.Title), Db.GetTrackedChanges(topic));
    }

    [Fact, Upstream("QueryTest27.java#testDiff2")]
    public void TestDiff2()
    {
        using var tracking = Db.BeginTracking();
        var topic = new Topic { Id = "123", Title = "456" };
        Db.AddTracking(topic);
        topic.Stars = 1;
        topic.Title = "456";
        Assert.DoesNotContain(nameof(Topic.Title), Db.GetTrackedChanges(topic));
        Assert.Contains(nameof(Topic.Stars), Db.GetTrackedChanges(topic));
    }

    private static Query<BlogEntityVO1> Vo(EasyDb db) => db.Query<BlogEntity>().SmartPredicate()
        .Select(b => new BlogEntityVO1 { Score = b.Score, Status = b.Status, Order = b.Order, IsTop = b.IsTop, Top = b.Top });

    private static Query<BlogEntityVO1> JoinedVo(EasyDb db) => db.Query<BlogEntity>().SmartPredicate().LeftJoin<BlogEntity>((b, t) => b.Id == t.Id)
        .Select((b, t) => new BlogEntityVO1 { Score = b.Score, Status = t.Status, Order = b.Order, IsTop = b.IsTop, Top = b.Top });

    // 条件下推：派生表外层的条件改写成对内层表的条件
    [Fact, Upstream("QueryTest27.java#testDerivedTable1")]
    public async Task TestDerivedTable1()
    {
        using var listen = SqlCapture.Listen();
        await Vo(Db).Where(o => o.Status == 0 && o.Order == 1m).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`score` AS `score`,t1.`status1` AS `status1`,t1.`order` AS `order`,t1.`is_top` AS `is_top`,t1.`top` AS `top` FROM (SELECT t.`score` AS `score`,t.`status` AS `status1`,t.`order` AS `order`,t.`is_top` AS `is_top`,t.`top` AS `top` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`status` = ? AND t.`order` = ?) t1");
        listen.AssertParams("false(Boolean),0(Integer),1(BigDecimal)");
    }

    // 连接表的列推进它的 ON
    [Fact, Upstream("QueryTest27.java#testDerivedTable2")]
    public async Task TestDerivedTable2()
    {
        using var listen = SqlCapture.Listen();
        await JoinedVo(Db).Where(o => o.Status == 0 && o.Order == 1m).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`score` AS `score`,t2.`status1` AS `status1`,t2.`order` AS `order`,t2.`is_top` AS `is_top`,t2.`top` AS `top` FROM (SELECT t.`score` AS `score`,t1.`status` AS `status1`,t.`order` AS `order`,t.`is_top` AS `is_top`,t.`top` AS `top` FROM `t_blog` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` AND t1.`status` = ? WHERE t.`deleted` = ? AND t.`order` = ?) t2");
        listen.AssertParams("false(Boolean),0(Integer),false(Boolean),1(BigDecimal)");
    }

    // OR 用到两张表：留在外层
    [Fact, Upstream("QueryTest27.java#testDerivedTable3"), Upstream("QueryTest27.java#testDerivedTable4")]
    public async Task TestDerivedTable3()
    {
        using var listen = SqlCapture.Listen();
        await JoinedVo(Db).Where(o => o.Status == 0 || o.Order == 1m).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`score` AS `score`,t2.`status1` AS `status1`,t2.`order` AS `order`,t2.`is_top` AS `is_top`,t2.`top` AS `top` FROM (SELECT t.`score` AS `score`,t1.`status` AS `status1`,t.`order` AS `order`,t.`is_top` AS `is_top`,t.`top` AS `top` FROM `t_blog` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`deleted` = ?) t2 WHERE (t2.`status1` = ? OR t2.`order` = ?)");
        listen.AssertParams("false(Boolean),false(Boolean),0(Integer),1(BigDecimal)");
    }

    // 同一个 UUID 存成 BINARY(36)、VARCHAR、CHAR 三种列，读回相等（C# 用值转换器按 36 位文本存）
    [Fact, Upstream("QueryTest27.java#testUUId")]
    public async Task TestUUId()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(UUIDEntity2)], Ct);
        await Db.CodeFirst.CreateTablesAsync([typeof(UUIDEntity2)], Ct);
        var uuid = Guid.NewGuid();
        await Db.Insert(new UUIDEntity2 { Id1 = uuid, Id2 = uuid, Id3 = uuid }).ExecuteAsync(Ct);
        var read = await Db.Query<UUIDEntity2>().FirstAsync(Ct);
        Assert.Equal((uuid, uuid, uuid), (read.Id1, read.Id2, read.Id3));
    }
}
