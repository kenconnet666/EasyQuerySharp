using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest24.java（LIKE 的转义、条件值过滤下的区间与 OR、条件作为值、NOT 分组等）。
/// Java 有两套 LIKE 写法：like() 写成 <c>LIKE ?</c>，contains()/startsWith() 写成 <c>LIKE CONCAT(…)</c>；
/// C# 的 string.Contains/StartsWith 对应前者，值里有 % 或 _ 时转义并写成后者的 ESCAPE 形式。
/// </summary>
public sealed class QueryTest24(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogSelect = "SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ?";
    private static readonly DateTime? Y2020 = new DateTime(2020, 1, 1);
    private static readonly DateTime? Y2021 = new DateTime(2021, 1, 1);

    // 值里含 %：转义后用 ESCAPE；(string)(object)x 只改类型不写 CAST（Java 的 asStr()）
    [Fact, Upstream("QueryTest24.java#testContains")]
    public async Task TestContains()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b =>
            ((string)(object)b.Star!).Contains("30%") && ((string)(object)(b.Star ?? 1)).StartsWith("30%") && b.Title!.Contains("30%")
            && (b.Title ?? "1").Contains("30%") && b.Title!.Substring(1, 6).Contains("30%") && b.Title!.Contains(b.Content!) && b.Title!.Contains(b.Content ?? "")).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND `star` LIKE CONCAT('%',?,'%') ESCAPE '\\\\' AND IFNULL(`star`,?) LIKE CONCAT(?,'%') ESCAPE '\\\\' AND `title` LIKE CONCAT('%',?,'%') ESCAPE '\\\\' AND IFNULL(`title`,?) LIKE CONCAT('%',?,'%') ESCAPE '\\\\' AND SUBSTR(`title`,2,6) LIKE CONCAT('%',?,'%') ESCAPE '\\\\' AND `title` LIKE CONCAT('%',`content`,'%') AND `title` LIKE CONCAT('%',IFNULL(`content`,?),'%')");
        listen.AssertParams("false(Boolean),30\\%(String),1(Integer),30\\%(String),30\\%(String),1(String),30\\%(String),30\\%(String),(String)");
    }

    // 空串被条件值过滤去掉；其余普通值写成 LIKE ?
    [Fact, Upstream("QueryTest24.java#testContains2", Deviation = "Java 的 contains/startsWith/endsWith 函数写成 LIKE CONCAT('%',?,'%')，C# 的 string.Contains 等写成 LIKE ?（值里带好 %），语义相同"),
     Upstream("QueryTest24.java#testContains3", Deviation = "同 testContains2；Java 的 asNumber/asStr 等只改类型，C# 用 (T)(object)x 同样不写 CAST")]
    public async Task TestContains2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(b =>
            ((string)(object)b.Star!).Contains("") && ((string)(object)b.Star!).Contains("1") && ((string)(object)b.Star!).StartsWith("")
            && ((string)(object)b.Star!).StartsWith("2") && ((string)(object)b.Star!).EndsWith("") && ((string)(object)b.Star!).EndsWith("3")
            && (string)(object)b.Title! == "123").ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND `star` LIKE ? AND `star` LIKE ? AND `star` LIKE ? AND `title` = ?");
        listen.AssertParams("false(Boolean),%1%(String),2%(String),%3(String),123(String)");
    }

    [Fact, Upstream("QueryTest24.java#testContains4")]
    public async Task TestContains4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new BlogEntity { Title = b.Title, Content = b.Content ?? "" }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title`,IFNULL(t.`content`,?) AS `content` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    [Fact, Upstream("QueryTest24.java#aaa")]
    public async Task Aaa()
    {
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == b.Id).Select((t, b) => Sql.Max(b.CreateTime)).SingleOrDefaultAsync(Ct);
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == b.Id).Select((t, b) => Sql.Max(b.CreateTime)).FirstOrDefaultAsync(Ct);
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == b.Id).OrderByDescending((t, b) => b.CreateTime).Select((t, b) => b.CreateTime).FirstOrDefaultAsync(Ct);
    }

    private static readonly string OrCreateTime = " AND (`create_time` >= ? OR `create_time` >= ?)";

    [Fact, Upstream("QueryTest24.java#test11"), Upstream("QueryTest24.java#test12")]
    public async Task Test11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => (b.CreateTime.RangeClosed(Y2020, Y2021) || b.Star.RangeClosed(1, 100)) && (b.CreateTime >= Y2020 || b.CreateTime >= Y2020)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND ((`create_time` >= ? AND `create_time` <= ?) OR (`star` >= ? AND `star` <= ?))" + OrCreateTime);
        listen.AssertParams("false(Boolean),2020-01-01T00:00(LocalDateTime),2021-01-01T00:00(LocalDateTime),1(Integer),100(Integer),2020-01-01T00:00(LocalDateTime),2020-01-01T00:00(LocalDateTime)");
    }

    [Fact, Upstream("QueryTest24.java#test13")]
    public async Task Test13()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => ((b.CreateTime ?? b.CreateTime).RangeClosedOpen(Y2020, Y2021) || (b.Star ?? b.Star).RangeClosedOpen(1, 100)) && (b.CreateTime >= Y2020 || b.CreateTime >= Y2020)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND ((IFNULL(`create_time`,`create_time`) >= ? AND IFNULL(`create_time`,`create_time`) < ?) OR (IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?))" + OrCreateTime);
    }

    // 条件值过滤下的区间：被拒绝的一端去掉，两端都去掉时整个区间去掉（OR 里只剩另一侧）
    [Theory, Upstream("QueryTest24.java#test14"), Upstream("QueryTest24.java#test15"), Upstream("QueryTest24.java#test16")]
    [InlineData(false, true, "(IFNULL(`create_time`,`create_time`) < ? OR (IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?))")]
    [InlineData(true, false, "(IFNULL(`create_time`,`create_time`) >= ? OR (IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?))")]
    [InlineData(false, false, "(IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?)")]
    public async Task Test14(bool hasBegin, bool hasEnd, string range)
    {
        var begin = hasBegin ? Y2020 : null;
        var end = hasEnd ? Y2021 : null;
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(b => ((b.CreateTime ?? b.CreateTime).RangeClosedOpen(begin, end) || (b.Star ?? b.Star).RangeClosedOpen(1, 100)) && (b.CreateTime >= Y2020 || b.CreateTime >= Y2020)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND " + range + OrCreateTime);
    }

    [Theory, Upstream("QueryTest24.java#test17"), Upstream("QueryTest24.java#test18"), Upstream("QueryTest24.java#test19")]
    [InlineData(false, true, "(`create_time` <= ? OR (`star` >= ? AND `star` <= ?))")]
    [InlineData(true, false, "(`create_time` >= ? OR (`star` >= ? AND `star` <= ?))")]
    [InlineData(false, false, "(`star` >= ? AND `star` <= ?)")]
    public async Task Test17(bool hasBegin, bool hasEnd, string range)
    {
        var begin = hasBegin ? Y2020 : null;
        var end = hasEnd ? Y2021 : null;
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(b => (b.CreateTime.RangeClosed(begin, end) || b.Star.RangeClosed(1, 100)) && (b.CreateTime >= Y2020 || b.CreateTime >= Y2020)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND " + range + OrCreateTime);
    }

    [Theory, Upstream("QueryTest24.java#test20"), Upstream("QueryTest24.java#test21"), Upstream("QueryTest24.java#test22")]
    [InlineData(false, true, "((`star` >= ? AND `star` <= ?) OR `create_time` <= ?)")]
    [InlineData(true, false, "((`star` >= ? AND `star` <= ?) OR `create_time` >= ?)")]
    [InlineData(false, false, "(`star` >= ? AND `star` <= ?)")]
    public async Task Test20(bool hasBegin, bool hasEnd, string range)
    {
        var begin = hasBegin ? Y2020 : null;
        var end = hasEnd ? Y2021 : null;
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(b => (b.Star.RangeClosed(1, 100) || b.CreateTime.RangeClosed(begin, end)) && (b.CreateTime >= Y2020 || b.CreateTime >= Y2020)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND " + range + OrCreateTime);
    }

    [Theory, Upstream("QueryTest24.java#test23"), Upstream("QueryTest24.java#test24"), Upstream("QueryTest24.java#test25")]
    [InlineData(false, true, "((IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?) OR IFNULL(`create_time`,`create_time`) < ?)")]
    [InlineData(true, false, "((IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?) OR IFNULL(`create_time`,`create_time`) >= ?)")]
    [InlineData(false, false, "(IFNULL(`star`,`star`) >= ? AND IFNULL(`star`,`star`) < ?)")]
    public async Task Test23(bool hasBegin, bool hasEnd, string range)
    {
        var begin = hasBegin ? Y2020 : null;
        var end = hasEnd ? Y2021 : null;
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(b => ((b.Star ?? b.Star).RangeClosedOpen(1, 100) || (b.CreateTime ?? b.CreateTime).RangeClosedOpen(begin, end)) && (b.CreateTime >= Y2020 || b.CreateTime >= Y2020)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND " + range + OrCreateTime);
    }

    // Java 的 valueOf(() -> 条件)：条件作为值时整体加括号；isBefore / isAfter 是 < / >
    [Fact, Upstream("QueryTest24.java#test26"), Upstream("QueryTest24.java#test29")]
    public async Task Test26()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(b => (b.CreateTime < Y2020) == true && (b.CreateTime ?? new DateTime(2022, 1, 1)) < Y2020 && (b.CreateTime ?? b.CreateTime) > Y2020 && b.CreateTime < Y2020
                        && b.CreateTime < b.CreateTime && (b.CreateTime ?? b.CreateTime) > b.CreateTime && b.CreateTime!.Value.AddDays(1) > Y2020)
            .Select(b => b.CreateTime < Y2020 && b.Title == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT (t.`create_time` < ? AND t.`title` = ?) FROM `t_blog` t WHERE t.`deleted` = ? AND (t.`create_time` < ?) = ? AND IFNULL(t.`create_time`,?) < ? AND IFNULL(t.`create_time`,t.`create_time`) > ? AND t.`create_time` < ? AND t.`create_time` < t.`create_time` AND IFNULL(t.`create_time`,t.`create_time`) > t.`create_time` AND date_add(t.`create_time`, interval (?) day) > ?");
        listen.AssertParams("2020-01-01T00:00(LocalDateTime),123(String),false(Boolean),2020-01-01T00:00(LocalDateTime),true(Boolean),2022-01-01T00:00(LocalDateTime),2020-01-01T00:00(LocalDateTime),2020-01-01T00:00(LocalDateTime),2020-01-01T00:00(LocalDateTime),1(Long),2020-01-01T00:00(LocalDateTime)");
    }

    // Java 的 startsWith 函数在 SQL 里拼 %：Sql.LikeStartsWith（C# 的 string.StartsWith 对应 likeMatchLeft，% 拼进参数）
    [Fact, Upstream("QueryTest24.java#test27")]
    public async Task Test27()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(b => new Draft<string, bool>(b.CreateTime!.Value.ToString("yyyy-MM"), Sql.LikeStartsWith(b.CreateTime!.Value.ToString("yyyy-MM"), "2020-02"))).ToListAsync(Ct);
        Assert.All(list, item => Assert.Equal(item.Value1.StartsWith("2020-02", StringComparison.Ordinal), item.Value2));
        listen.AssertSql("SELECT DATE_FORMAT(t.`create_time`,'%Y-%m') AS `value1`,(DATE_FORMAT(t.`create_time`,'%Y-%m') LIKE CONCAT(?,'%')) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    [Fact, Upstream("QueryTest24.java#test28")]
    public async Task Test28()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new Draft<string, bool>(b.CreateTime!.Value.ToString("yyyy-MM"), Sql.LikeStartsWith(b.CreateTime!.Value.ToString("yyyy-MM"), "2020-02") || Sql.LikeStartsWith(b.Title, "小明"))).ToListAsync(Ct);
        listen.AssertSql("SELECT DATE_FORMAT(t.`create_time`,'%Y-%m') AS `value1`,((DATE_FORMAT(t.`create_time`,'%Y-%m') LIKE CONCAT(?,'%') OR t.`title` LIKE CONCAT(?,'%'))) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    [Fact, Upstream("QueryTest24.java#testx13"), Upstream("QueryTest24.java#testx14"), Upstream("QueryTest24.java#testx15")]
    public async Task Testx13()
    {
        // Java 依赖别的用例插入的数据；这里先确保有一行，枚举按整数读回
        await Db.Delete<TopicTypeTest1>().WhereById("testx13").AllowDeleteStatement(true).ExecuteAsync(Ct);
        await Db.Insert(new TopicTypeTest1 { Id = "testx13", Stars = 1, Title = "x", TopicType = TopicTypeEnum.Classer, CreateTime = DateTime.Now }).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();
        Assert.NotNull(await Db.Query<TopicTypeTest1>().Select(t => t.TopicType).FirstOrDefaultAsync(Ct));
        listen.AssertSql("SELECT t.`topic_type` FROM `t_topic_type` t LIMIT 1");
        Assert.NotNull(await Db.Query<TopicTypeTest1>().Select(t => Sql.Max(t.TopicType)).FirstOrDefaultAsync(Ct));
        listen.AssertSql("SELECT MAX(t.`topic_type`) FROM `t_topic_type` t LIMIT 1");
        Assert.NotNull(await Db.Query<TopicTypeTest1>().Select(t => Sql.Min(t.TopicType)).FirstOrDefaultAsync(Ct));
        listen.AssertSql("SELECT MIN(t.`topic_type`) FROM `t_topic_type` t LIMIT 1");
    }

    // whereById(null)：`id` = ?，参数 null（永不成立）
    [Fact, Upstream("QueryTest24.java#testx16")]
    public async Task Testx16()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().WhereById(null!).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`topic_type`,`create_time` FROM `t_topic_type` WHERE `id` = ?");
        listen.AssertParams("null(null)");
    }

    // Java 的 expression().not(() -> …)：C# 的 !(…)
    [Fact, Upstream("QueryTest24.java#testx17"), Upstream("QueryTest24.java#testx18")]
    public async Task Testx17()
    {
        var after = new DateTime(2024, 1, 1);
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => !(Sql.LikeStartsWith(b.Title, "123") && b.CreateTime > after)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND (NOT (`title` LIKE CONCAT(?,'%') AND `create_time` > ?))");
        await Db.Query<BlogEntity>().Where(b => !(Sql.LikeStartsWith(b.Title, "123") || b.CreateTime > after)).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + " AND (NOT ((`title` LIKE CONCAT(?,'%') OR `create_time` > ?)))");
        await Db.Query<SysUser>().Where(s => s.Phone!.StartsWith("186")).Select(s => new Draft<string?, string>(s.Phone, s.Address!.Substring(1, 10))).ToListAsync(Ct);
    }

    [Fact, Upstream("QueryTest24.java#testEnum", Deviation = "Java 的 ClassProxy 总写 AS，C# 的 DTO 投影同名列不写 AS")]
    public async Task TestEnum()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<TopicTypeTest1>().Select(t => new TopicTypeTest1 { TopicType = t.TopicType }).ToListAsync(Ct);
        Assert.All(list, t => Assert.True(Enum.IsDefined(t.TopicType!.Value)));
        listen.AssertSql("SELECT t.`topic_type` FROM `t_topic_type` t");
    }

    // 导航上标了 GroupJoin = true：聚合总是合并成分组 JOIN
    [Fact, Upstream("QueryTest24.java#testSubCount")]
    public async Task TestSubCount()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(t => new Topic { Id = t.Id, CreateTime = t.CreateTime, Title = t.Title, Stars = (int)t.Users2!.LongCount() }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`title`,IFNULL(t2.`__count2__`,0) AS `stars` FROM `t_blog` t LEFT JOIN (SELECT t1.`id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `easy-query-test`.`t_sys_user` t1 GROUP BY t1.`id`) t2 ON t2.`__group_key1__` = t.`title` WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    // 先右连接、只取第一张表，再整体作为派生表左连接
    // 写法与 Java 不同、SQL 一致：Java 两参 lambda 的 leftJoin 把前面的连接收成第一张表的派生表；C# 显式 Select((t, b) => t) 再连接，SQL 相同
    [Fact, Upstream("QueryTest24.java#testJoinParameterCount")]
    public async Task TestJoinParameterCount()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().RightJoin<BlogEntity>((t, b) => t.Id == b.Id).Select((t, _) => t)
            .LeftJoin<Topic>((a, c) => a.Stars == c.Stars)
            .Where((_, b) => b.Stars != null)
            .Select((a, _) => new Draft<string?>(a.Title!.Substring(1, 2))).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t2.`title`,2,2) AS `value1` FROM (SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t RIGHT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id`) t2 LEFT JOIN `t_topic` t3 ON t2.`stars` = t3.`stars` WHERE t3.`stars` IS NOT NULL");
        listen.AssertParams("false(Boolean)");
    }

    // 写法与 Java 不同、SQL 一致：Java 两参 lambda 的 leftJoin 把前面的连接收成第一张表的派生表；C# 显式 Select((t, b) => t) 再连接，SQL 相同
    [Fact, Upstream("QueryTest24.java#testdraft1")]
    public async Task Testdraft1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().RightJoin<BlogEntity>((t, b) => t.Id == b.Id).Where((_, b) => b.Score == 0m).Select((t, _) => t)
            .LeftJoin<Topic>((a, c) => a.Stars == c.Stars)
            .Select((a, _) => new Draft<string?>(a.Title!.Substring(1, 2))).Take(1).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t2.`title`,2,2) AS `value1` FROM (SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t RIGHT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`score` = ?) t2 LEFT JOIN `t_topic` t3 ON t2.`stars` = t3.`stars` LIMIT 1");
        listen.AssertParams("false(Boolean),0(BigDecimal)");
    }

    // 实体更新只生成 SQL 不执行
    [Fact, Upstream("QueryTest24.java#test123")]
    public void Test123()
    {
        var topic = new Topic { Id = "123", Title = "title123", Stars = 1, CreateTime = DateTime.Now };
        Assert.Equal("UPDATE `t_topic` SET `stars` = ?,`title` = ?,`create_time` = ? WHERE `id` = ?", Db.Update(topic).ToSql());
    }
}
