// 条件在数据库里执行：字符串比较不涉及区域性，不适用 CA1309 / CA1310
#pragma warning disable CA1309, CA1310
// 条件在数据库里执行：大小写转换后比较是要翻译的写法本身，不适用 CA1862
#pragma warning disable CA1862

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest17.java（按属性名取列、原生 SQL 片段用在条件 / 排序 / 投影、字符串函数、分组拼接投影到 VO、
/// 多层导航的 Any 与 flatElement）。Java 的 flatElement() 写成 Any。
/// </summary>
public sealed class QueryTest17(MySqlFixture fixture) : MySqlTest(fixture)
{
    // 按属性名取列；只选部分列 / 再追加一列（Java 的 _concat）
    [Fact, Upstream("QueryTest17.java#test11")]
    public async Task Test11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => Sql.Column<string?>(t, "title") == "123").Select(t => new Topic { Id = t.Id, Title = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title` FROM `t_topic` t WHERE t.`title` = ?");
        listen.AssertParams("123(String)");
    }

    [Fact, Upstream("QueryTest17.java#textLikeFunc")]
    public async Task TextLikeFunc()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains(Sql.Constant("ABc").ToLower())).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE CONCAT('%',LOWER(?),'%')");
        listen.AssertParams("false(Boolean),ABc(String)");
    }

    // 原生排序：不带方向的 sql(…) 原样写；sqlSegment(…).asc() 加方向
    [Fact, Upstream("QueryTest17.java#testNativeSQL")]
    public async Task TestNativeSQL()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(b => b.Id == "123").OrderByRaw("RAND()").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? ORDER BY RAND()");
        listen.AssertParams("123(String)");
        await Db.Query<Topic>().Where(b => b.Id == "123").OrderBy(t => Sql.Raw<double>("RAND()")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? ORDER BY RAND() ASC");
        listen.AssertParams("123(String)");
    }

    [Fact, Upstream("QueryTest17.java#testNativeSQL1")]
    public async Task TestNativeSQL1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(b => b.Id == "123").OrderByRaw(t => Sql.Raw<object>("IFNULL({0},{1}) DESC", t.Stars, 1)).OrderByRaw("RAND()").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? ORDER BY IFNULL(`stars`,?) DESC,RAND()");
        listen.AssertParams("123(String),1(Integer)");
    }

    [Fact, Upstream("QueryTest17.java#testNativeSQL2")]
    public async Task TestNativeSQL2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(b => b.Id == "123" && Sql.Raw<bool>("{0}!={1}", b.Stars, b.CreateTime))
            .OrderByRaw(t => Sql.Raw<object>("IFNULL({0},{1}) DESC", t.Stars, 1)).OrderByRaw("RAND()").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? AND `stars`!=`create_time` ORDER BY IFNULL(`stars`,?) DESC,RAND()");
        listen.AssertParams("123(String),1(Integer)");
    }

    // 投影里写原生片段
    [Fact, Upstream("QueryTest17.java#testNativeSQL3")]
    public async Task TestNativeSQL3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(b => b.Id == "123")
            .Select(t => new Draft<double, int>(Sql.Raw<double>("RAND()"), Sql.Raw<int>("IFNULL({0},{1})", t.Stars, 1))).ToListAsync(Ct);
        listen.AssertSql("SELECT RAND() AS `value1`,IFNULL(t.`stars`,?) AS `value2` FROM `t_topic` t WHERE t.`id` = ?");
        listen.AssertParams("1(Integer),123(String)");
    }

    // 各字符串函数（Java 的 subString / concat / toUpper / trim / replace / leftPad / length / compareTo）
    // 写法与 Java 不同、SQL 一致：Java 用动态 API（fx.subString(\"id\", …)）写同样的条件；C# 只有表达式写法，SQL 相同
    [Fact, Upstream("QueryTest17.java#test1"), Upstream("QueryTest17.java#test3")]
    public async Task Test1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => (b.Id ?? "123") == "123"
                                                && b.Id!.Substring(1, 20) == "456"
                                                && string.Concat(b.Content, "123", b.Id) == "789"
                                                && b.Content!.ToUpper() == "abc"
                                                && b.Content!.ToLower() == "def"
                                                && b.Content!.Trim() == "a"
                                                && b.Content!.TrimStart() == "b"
                                                && b.Content!.TrimEnd() == "c"
                                                && b.Content!.Replace("123", "456") == "aaa"
                                                && b.Content!.PadLeft(2, 'a') == "aa"
                                                && b.Content!.PadRight(2, 'a') == "aa"
                                                && b.Content!.Length == 1
                                                && b.Content!.CompareTo("aaaa") >= 0).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND IFNULL(`id`,?) = ? AND SUBSTR(`id`,2,20) = ? AND CONCAT(`content`,?,`id`) = ? AND UPPER(`content`) = ? AND LOWER(`content`) = ? AND TRIM(`content`) = ? AND LTRIM(`content`) = ? AND RTRIM(`content`) = ? AND REPLACE(`content`,?,?) = ? AND LPAD(`content`, 2, ?) = ? AND RPAD(`content`, 2, ?) = ? AND CHAR_LENGTH(`content`) = ? AND STRCMP(`content`,?) >= ?");
        listen.AssertParams("false(Boolean),123(String),123(String),456(String),123(String),789(String),abc(String),def(String),a(String),b(String),c(String),123(String),456(String),aaa(String),a(String),aa(String),a(String),aa(String),1(Integer),aaaa(String),0(Integer)");
    }

    /// <summary>Java 的 ProvinceVO。</summary>
    public sealed class ProvinceVO
    {
        public string? MyCode { get; set; }

        public string? MyName { get; set; }
    }

    // 分组后把连接表的标题拼起来投影到 VO
    [Fact, Upstream("QueryTest17.java#xxx123")]
    public async Task Xxx123()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).GroupBy((t1, b2) => t1.Id)
            .Select(g => new ProvinceVO { MyCode = g.Key, MyName = g.StringJoin(x => x.Item2.Title, ",") }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `my_code`,GROUP_CONCAT(t1.`title` SEPARATOR ?) AS `my_name` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` GROUP BY t.`id`");
        listen.AssertParams(",(String),false(Boolean)");
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).GroupBy((t1, b2) => t1.Id)
            .Select(g => new ProvinceVO { MyCode = g.Key, MyName = g.Distinct().StringJoin(x => x.Item2.Title, ",") }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `my_code`,GROUP_CONCAT(DISTINCT t1.`title` SEPARATOR ?) AS `my_name` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` GROUP BY t.`id`");
        listen.AssertParams(",(String),false(Boolean)");
    }

    private static readonly EasyDb MySqlOnly = TestKit.SqlOnly.Db("mysql");

    // 对一导航之后的集合导航
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("QueryTest17.java#testFlatElement1")]
    public void TestFlatElement1()
    {
        TestKit.SqlOnly.AssertStatement(MySqlOnly.Query<MyObject>().Where(m => m.Parent!.Children!.Where(o => string.Compare(o.Name, "3") > 0).Any()).ToStatement(),
            "SELECT t.`id`,t.`f1`,t.`f2`,t.`parent_id` FROM `MyObject` t LEFT JOIN `MyObjectParent` t1 ON t1.`id` = t.`parent_id` WHERE EXISTS(SELECT 1 FROM `MyObjectParentChildren` t2 WHERE t2.`parent_id` = t1.`id` AND t2.`name` > ? LIMIT 1)", "3(String)");
        TestKit.SqlOnly.AssertStatement(MySqlOnly.Query<MyObject>().Where(m => m.Parent!.Children!.Any(o => string.Compare(o.Name, "3") > 0)).ToStatement(),
            "SELECT t.`id`,t.`f1`,t.`f2`,t.`parent_id` FROM `MyObject` t LEFT JOIN `MyObjectParent` t1 ON t1.`id` = t.`parent_id` WHERE EXISTS(SELECT 1 FROM `MyObjectParentChildren` t2 WHERE t2.`parent_id` = t1.`id` AND t2.`name` > ? LIMIT 1)", "3(String)");
    }

    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("QueryTest17.java#testFlatElement2")]
    public void TestFlatElement2()
    {
        TestKit.SqlOnly.AssertStatement(MySqlOnly.Query<MyObject>()
                .Where(m => m.Parent!.Children!.Where(c => c.MyChildren!.Where(x => string.Compare(x.Name, "3") > 0).Any()).Any()).ToStatement(),
            "SELECT t.`id`,t.`f1`,t.`f2`,t.`parent_id` FROM `MyObject` t LEFT JOIN `MyObjectParent` t1 ON t1.`id` = t.`parent_id` WHERE EXISTS(SELECT 1 FROM `MyObjectParentChildren` t2 WHERE t2.`parent_id` = t1.`id` AND EXISTS(SELECT 1 FROM `MyChild` t3 WHERE t3.`children_parent_id` = t2.`id` AND t3.`name` > ? LIMIT 1) LIMIT 1)", "3(String)");
        TestKit.SqlOnly.AssertStatement(MySqlOnly.Query<MyObject>()
                .Where(m => m.Parent!.Children!.Any(c => c.MyChildren!.Any(x => string.Compare(x.Name, "3") > 0))).ToStatement(),
            "SELECT t.`id`,t.`f1`,t.`f2`,t.`parent_id` FROM `MyObject` t LEFT JOIN `MyObjectParent` t1 ON t1.`id` = t.`parent_id` WHERE EXISTS(SELECT 1 FROM `MyObjectParentChildren` t2 WHERE t2.`parent_id` = t1.`id` AND EXISTS(SELECT 1 FROM `MyChild` t3 WHERE t3.`children_parent_id` = t2.`id` AND t3.`name` > ? LIMIT 1) LIMIT 1)", "3(String)");
    }

    // 带导航条件的查询投影后再连接、分组
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("QueryTest17.java#testNop2")]
    public void TestNop2()
    {
        TestKit.SqlOnly.AssertStatement(MySqlOnly.Query<MyObject>()
                .Where(m => m.Parent!.Children!.Where(c => c.MyChildren!.Where(x => string.Compare(x.Name, "3") > 0).Any()).Any())
                .Select(m => new Draft<string?, string?>(m.F1, m.F1))
                .InnerJoin<OtherTable>((a, b) => a.Value1 == b.Type)
                .GroupBy((a, b) => a.Value1)
                .Select(g => new Draft<string?, decimal?, long>(g.Key, g.Sum(x => (decimal?)(object?)x.Item1.Value2), g.Count(x => x.Item2.Type))).ToStatement(),
            "SELECT t4.`value1` AS `value1`,SUM(t4.`value2`) AS `value2`,COUNT(t5.`type`) AS `value3` FROM (SELECT t.`f1` AS `value1`,t.`f1` AS `value2` FROM `MyObject` t LEFT JOIN `MyObjectParent` t1 ON t1.`id` = t.`parent_id` WHERE EXISTS(SELECT 1 FROM `MyObjectParentChildren` t2 WHERE t2.`parent_id` = t1.`id` AND EXISTS(SELECT 1 FROM `MyChild` t3 WHERE t3.`children_parent_id` = t2.`id` AND t3.`name` > ? LIMIT 1) LIMIT 1)) t4 INNER JOIN `OtherTable` t5 ON t4.`value1` = t5.`type` GROUP BY t4.`value1`", "3(String)");
    }
    [Fact, Upstream("QueryTest17.java#testNopQuery", NotApplicable = "Java 用例体全部注释掉了"), Upstream("QueryTest17.java#testFlatElement5", NotApplicable = "Java 用例体全部注释掉了")]
    public void TestNopQuery()
    {
    }

    [Fact, Upstream("QueryTest17.java#testaaa", NotApplicable = "测的是 Java 代理对象的导航路径工具（EasyProxyUtil.getFullNavValue）"),
     Upstream("QueryTest17.java#test12234567", NotApplicable = "Java 自建 Hikari 数据源与客户端配置，没有断言"),
     Upstream("QueryTest17.java#testxxx9", NotApplicable = "测的是 Java 代理管理器创建代理对象"),
     Upstream("QueryTest17.java#xxx", NotApplicable = "Java 的几种实体继承写法（MyTopic / MyTopicx / MyTopic4 / MyTopic5）只跑不断言；C# 的实体就是普通类")]
    public void TestJavaInternals()
    {
    }

    // Java 用例没有断言：只选部分列；投影到 VO 后再按 VO 的列过滤
    [Fact, Upstream("QueryTest17.java#testSubFrom"), Upstream("QueryTest17.java#testSubFrom1")]
    public async Task TestSubFrom()
    {
        await Db.Query<Province>().Where(p => p.Code == "123").Select(p => new Province { Code = p.Code, Name = p.Name }).ToListAsync(Ct);
        await Db.Query<Province>().Where(p => p.Code == "123").Select(p => new ProvinceVO { MyName = p.Code, MyCode = p.Name }).Where(p => p.MyCode!.Contains("123")).ToListAsync(Ct);
    }

    // Java 用例没有断言：时间加一天后比较与投影；同样的原生片段写法
    [Fact, Upstream("QueryTest17.java#aaaa")]
    public async Task Aaaa()
    {
        var now = DateTime.Now;
        await Db.Query<Topic>().Where(t => t.CreateTime!.Value.AddDays(1) < now).Select(t => new Draft<DateTime?>(t.CreateTime!.Value.AddDays(1))).ToListAsync(Ct);
        await Db.Query<Topic>().Where(t => Sql.Raw<bool>("({0} + interval 1 day) < {1}", t.CreateTime, now))
            .Select(t => new Draft<DateTime?>(Sql.Raw<DateTime?>("({0} + interval 1 day)", t.CreateTime))).ToListAsync(Ct);
    }

    // Java 的 streamBy(s -> s.findFirst())：C# 的 FirstOrDefaultAsync；字段名校验是 Java 的内部工具
    [Fact, Upstream("QueryTest17.java#testOptional1"), Upstream("QueryTest17.java#testOptional", Deviation = "Java 另外测了 EasyFieldCheckUtil 的字段名校验与 Stream 收集；C# 用 FirstOrDefaultAsync / ToListAsync / ToDictionary")]
    public async Task TestOptional1()
    {
        string[] ids = ["1", "2", "3"];
        await Db.Query<Topic>().Where(t => t.Id == "1" && (t.Id ?? "1").Contains("123") && ids.Contains(t.Id ?? "2")).FirstOrDefaultAsync(Ct);
        var first = await Db.Query<Topic>().Where(t => t.Id == "1").FirstOrDefaultAsync(Ct);
        var byId = (await Db.Query<Topic>().Where(t => t.Id == "1").ToListAsync(Ct)).ToDictionary(t => t.Id!);
        Assert.Equal(first is not null, byId.ContainsKey("1"));
    }
}
