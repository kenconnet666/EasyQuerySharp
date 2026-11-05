// 条件在数据库里执行：大小写转换后比较、字符串比较是要翻译的写法本身，不适用 CA1862 / CA1309 / CA1310
#pragma warning disable CA1862, CA1309, CA1310

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest10.java（AND / OR 嵌套、分组投影、字符串函数链、LIKE 的几种匹配、日期格式后的区间与 IN、投影到 VO、HAVING 里的比较）。
/// Java 的 likeMatchLeft / likeMatchRight 对应 <c>StartsWith</c> / <c>EndsWith</c>，subString(i, n) 对应 <c>Substring(i, n)</c>。
/// </summary>
public sealed class QueryTest10(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("QueryTest10.java#test223")]
    public async Task Test223()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != "title0"
                                           && (o.Title == "1" || o.Title == "2" || o.Title == "3"
                                               || (o.Title == "4" && o.Title == "5" && o.Title == "6" && (o.Title == "7" || o.Title == "8" || o.Title == "9")
                                                   && o.Title == "10" && o.Title == "11" && o.Title == "12")
                                               || o.Title == "13" || o.Title == "14" || o.Title == "15")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `title` <> ? AND (`title` = ? OR `title` = ? OR `title` = ? OR (`title` = ? AND `title` = ? AND `title` = ? AND (`title` = ? OR `title` = ? OR `title` = ?) AND `title` = ? AND `title` = ? AND `title` = ?) OR `title` = ? OR `title` = ? OR `title` = ?)");
        listen.AssertParams("title0(String),1(String),2(String),3(String),4(String),5(String),6(String),7(String),8(String),9(String),10(String),11(String),12(String),13(String),14(String),15(String)");
    }

    // 分组投影；投影后再按投影列过滤（包一层）；原生片段投影
    [Fact, Upstream("QueryTest10.java#test6")]
    public async Task Test6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?>(g.Key)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`");
        await Db.Query<Topic>().GroupBy(t => new { t.Id, t.Title }).Select(g => new Draft<string?>(g.Key.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`,t.`title`");
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?>(g.Key)).Where(o => o.Value1 == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`value1` AS `value1` FROM (SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`) t1 WHERE t1.`value1` = ?");
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t GROUP BY t.`id`");
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long, int?>(g.Key, g.Count(), g.Sum(s => s.Stars))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2`,SUM(t.`stars`) AS `value3` FROM `t_topic` t GROUP BY t.`id`");
        await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?>(t.Id, t.CreateTime)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
        var raw = await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?, string>(t.Id, t.CreateTime, Sql.Raw<string>("1"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2`,1 AS `value3` FROM `t_blog` t WHERE t.`deleted` = ?");
        Assert.Equal("1", raw[0].Value3);
        var title = await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?, string>(t.Id, t.CreateTime, Sql.Raw<string>("IFNULL({0},'1')", t.Title))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2`,IFNULL(t.`title`,'1') AS `value3` FROM `t_blog` t WHERE t.`deleted` = ?");
        Assert.Equal("title0", title[0].Value3);
    }

    // Java 用例没有断言：按格式化后的日期排序；按截取的内容分组
    [Fact, Upstream("QueryTest10.java#test222")]
    public async Task Test222()
    {
        await Db.Query<BlogEntity>().OrderBy(o => o.CreateTime!.Value.ToString("yyyy-MM-dd")).ToListAsync(Ct);
        await Db.Query<BlogEntity>().GroupBy(o => o.Content!.Substring(0, 8)).Select(o => new Draft<string>(o.Key)).ToListAsync(Ct);
    }

    [Fact, Upstream("QueryTest10.java#testDraft12")]
    public async Task TestDraft12()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(o => o.Title!.Substring(1, 2) == "123" && o.Title.ToLower().Substring(1, 2) == "123" && o.Title.ToLower().ToUpper().ToLower().Substring(1, 2) == "123"
                        && o.CreateTime!.Value.ToString("yyyy-MM").ToLower().Substring(1, 10).Contains("023-01"))
            .Select(o => new Draft<string?, string, string, string>(o.Id, o.Title!.ToLower().Replace("123", "456"), o.Title.ToUpper(), o.Title.ToLower().Substring(1, 2)))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,REPLACE(LOWER(t.`title`),?,?) AS `value2`,UPPER(t.`title`) AS `value3`,SUBSTR(LOWER(t.`title`),2,2) AS `value4` FROM `t_topic` t WHERE SUBSTR(t.`title`,2,2) = ? AND SUBSTR(LOWER(t.`title`),2,2) = ? AND SUBSTR(LOWER(UPPER(LOWER(t.`title`))),2,2) = ? AND SUBSTR(LOWER(DATE_FORMAT(t.`create_time`,'%Y-%m')),2,10) LIKE ?");
        listen.AssertParams("123(String),456(String),123(String),123(String),123(String),%023-01%(String)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft13")]
    public async Task TestDraft13()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(o => o.Title!.Trim().Substring(1, 2) == "123" && o.Title.Trim().ToLower().Substring(1, 2) == "123" && o.Title.ToLower().Trim().ToUpper().ToLower().Substring(1, 2) == "123"
                        && o.CreateTime!.Value.ToString("yyyy-MM").ToLower().Substring(1, 10).Trim().Contains("023-01"))
            .Select(o => new Draft<string?, string, string, string>(o.Id, o.Title!.ToLower().Trim(), o.Title.Trim().ToUpper(), o.Title.ToLower().Trim().Substring(1, 2)))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,TRIM(LOWER(t.`title`)) AS `value2`,UPPER(TRIM(t.`title`)) AS `value3`,SUBSTR(TRIM(LOWER(t.`title`)),2,2) AS `value4` FROM `t_topic` t WHERE SUBSTR(TRIM(t.`title`),2,2) = ? AND SUBSTR(LOWER(TRIM(t.`title`)),2,2) = ? AND SUBSTR(LOWER(UPPER(TRIM(LOWER(t.`title`)))),2,2) = ? AND TRIM(SUBSTR(LOWER(DATE_FORMAT(t.`create_time`,'%Y-%m')),2,10)) LIKE ?");
        listen.AssertParams("123(String),123(String),123(String),%023-01%(String)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft14")]
    public async Task TestDraft14()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(o => o.Title!.TrimStart().Substring(1, 2) == "123" && o.Title.TrimStart().ToLower().Substring(1, 2) == "123" && o.Title.ToLower().TrimStart().ToUpper().ToLower().Substring(1, 2) == "123"
                        && o.CreateTime!.Value.ToString("yyyy-MM").ToLower().Substring(1, 10).TrimStart().Contains("023-01"))
            .Select(o => new Draft<string?, string, string, string>(o.Id, o.Title!.ToLower().TrimStart(), o.Title.TrimStart().ToUpper(), o.Title.ToLower().TrimStart().Substring(1, 2)))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,LTRIM(LOWER(t.`title`)) AS `value2`,UPPER(LTRIM(t.`title`)) AS `value3`,SUBSTR(LTRIM(LOWER(t.`title`)),2,2) AS `value4` FROM `t_topic` t WHERE SUBSTR(LTRIM(t.`title`),2,2) = ? AND SUBSTR(LOWER(LTRIM(t.`title`)),2,2) = ? AND SUBSTR(LOWER(UPPER(LTRIM(LOWER(t.`title`)))),2,2) = ? AND LTRIM(SUBSTR(LOWER(DATE_FORMAT(t.`create_time`,'%Y-%m')),2,10)) LIKE ?");
        listen.AssertParams("123(String),123(String),123(String),%023-01%(String)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft15")]
    public async Task TestDraft15()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(o => o.Title!.TrimEnd().Substring(1, 2) == "123" && o.Title.TrimEnd().ToLower().Substring(1, 2) == "123" && o.Title.ToLower().TrimEnd().ToUpper().ToLower().Substring(1, 2) == "123"
                        && o.CreateTime!.Value.ToString("yyyy-MM").ToLower().Substring(1, 10).TrimEnd().Contains("023-01"))
            .Select(o => new Draft<string?, string, string, string>(o.Id, o.Title!.ToLower().TrimEnd(), o.Title.TrimEnd().ToUpper(), o.Title.ToLower().TrimEnd().Substring(1, 2)))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,RTRIM(LOWER(t.`title`)) AS `value2`,UPPER(RTRIM(t.`title`)) AS `value3`,SUBSTR(RTRIM(LOWER(t.`title`)),2,2) AS `value4` FROM `t_topic` t WHERE SUBSTR(RTRIM(t.`title`),2,2) = ? AND SUBSTR(LOWER(RTRIM(t.`title`)),2,2) = ? AND SUBSTR(LOWER(UPPER(RTRIM(LOWER(t.`title`)))),2,2) = ? AND RTRIM(SUBSTR(LOWER(DATE_FORMAT(t.`create_time`,'%Y-%m')),2,10)) LIKE ?");
        listen.AssertParams("123(String),123(String),123(String),%023-01%(String)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft16")]
    public async Task TestDraft16()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title!.TrimEnd().TrimStart() == o.Id!.TrimStart() && o.CreateTime!.Value.ToString("yyyy-MM-dd").Substring(0, 4) == "2021")
            .Select(o => new Draft<string?, string>(o.Id, o.Title!.ToLower())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,LOWER(t.`title`) AS `value2` FROM `t_topic` t WHERE LTRIM(RTRIM(t.`title`)) = LTRIM(t.`id`) AND SUBSTR(DATE_FORMAT(t.`create_time`,'%Y-%m-%d'),1,4) = ?");
        listen.AssertParams("2021(String)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft17")]
    public async Task TestDraft17()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title!.TrimEnd().TrimStart().Replace("title", "abc").Contains("abc"))
            .Select(o => new Draft<string?, string>(o.Id, o.Title!.ToLower().Replace("title", "abc"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,REPLACE(LOWER(t.`title`),?,?) AS `value2` FROM `t_topic` t WHERE REPLACE(LTRIM(RTRIM(t.`title`)),?,?) LIKE ?");
        listen.AssertParams("title(String),abc(String),title(String),abc(String),%abc%(String)");
    }

    // Java 的 compareTo 对应实例方法 CompareTo（STRCMP）
    [Fact, Upstream("QueryTest10.java#testDraft18")]
    public async Task TestDraft18()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title!.CompareTo(o.Id) == 0 && o.Title.Trim().CompareTo(o.Id!.ToLower().Substring(1, 10)) >= 2 && o.Id.CompareTo(o.Title) <= 1)
            .Select(o => new Draft<string?, int>(o.Id, o.Title!.CompareTo(o.Id))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,STRCMP(t.`title`,t.`id`) AS `value2` FROM `t_topic` t WHERE STRCMP(t.`title`,t.`id`) = ? AND STRCMP(TRIM(t.`title`),SUBSTR(LOWER(t.`id`),2,10)) >= ? AND STRCMP(t.`id`,t.`title`) <= ?");
        listen.AssertParams("0(Integer),2(Integer),1(Integer)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft19")]
    public async Task TestDraft19()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title!.PadLeft(5, '1') != "title0").Select(o => new Draft<string?, string>(o.Id, o.Title!.PadLeft(9, '0'))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,LPAD(t.`title`, 9, ?) AS `value2` FROM `t_topic` t WHERE LPAD(t.`title`, 5, ?) <> ?");
        listen.AssertParams("0(String),1(String),title0(String)");
        var padded = await Db.Query<Topic>().Where(o => o.Title!.PadLeft(5, '1') != "title0" && o.Id != "7").Select(o => new Draft<string?, string>(o.Id, o.Title!.PadLeft(9, '0'))).ToListAsync(Ct);
        Assert.All(padded, p => Assert.StartsWith("0", p.Value2, StringComparison.Ordinal));
    }

    [Fact, Upstream("QueryTest10.java#testDraft20")]
    public async Task TestDraft20()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().GroupBy(o => o.Content!.Substring(0, 8)).Select(o => new Draft<string, string>(o.Key, o.StringJoin(x => x.Id, ","))).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`content`,1,8) AS `value1`,GROUP_CONCAT(t.`id` SEPARATOR ?) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY SUBSTR(t.`content`,1,8)");
        listen.AssertParams(",(String),false(Boolean)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft11")]
    public async Task TestDraft11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(o => o.Title!.ToLower().Contains("123") && o.Title.ToLower() == o.Id!.ToUpper() && o.Title.ToLower() != o.Id.ToLower() && o.Title.ToLower() == o.Id.ToUpper()
                        && o.Title.ToUpper().Contains("123") && o.Title.ToUpper() == o.Id.ToUpper() && o.Title.ToUpper() != o.Id.ToLower())
            .Select(o => new Draft<string?, string, string>(o.Id, o.Title!.ToLower(), o.Title.ToUpper())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,LOWER(t.`title`) AS `value2`,UPPER(t.`title`) AS `value3` FROM `t_topic` t WHERE LOWER(t.`title`) LIKE ? AND LOWER(t.`title`) = UPPER(t.`id`) AND LOWER(t.`title`) <> LOWER(t.`id`) AND LOWER(t.`title`) = UPPER(t.`id`) AND UPPER(t.`title`) LIKE ? AND UPPER(t.`title`) = UPPER(t.`id`) AND UPPER(t.`title`) <> LOWER(t.`id`)");
    }

    [Fact, Upstream("QueryTest10.java#testDraft10")]
    public async Task TestDraft10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title!.Contains("123") && o.CreateTime >= new DateTime(2022, 2, 1, 3, 4, 0))
            .GroupBy(o => o.Id).Select(o => new Draft<string?, long>(o.Key, o.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t WHERE t.`title` LIKE ? AND t.`create_time` >= ? GROUP BY t.`id`");
    }

    // 空集合的 IN 写成 1 = 2；前缀、后缀匹配
    [Fact, Upstream("QueryTest10.java#testLike1", Deviation = "前两段 Java 把条件之后再 leftJoin 的查询包成派生表（SELECT t2.… FROM (SELECT … WHERE …) t2 LEFT JOIN …）；C# 的条件与连接写在同一层，结果相同")]
    public async Task TestLike1()
    {
        var tenantIds = new List<string?>();
        var roleIds = new List<string?>();
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => tenantIds.Contains(o.Id) && Sql.Exists(Db.Query<Topic>().Where(x => x.Id == o.Id).Where(x => roleIds.Contains(x.Id))))
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id && t1.Title!.Contains("123"))
            .Select((t, t1) => new BlogEntity { Id = t.Id, Content = t.Content, CreateTime = t.CreateTime, Star = t1.Stars }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`content`,t.`create_time`,t1.`stars` AS `star` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` AND t1.`title` LIKE ? WHERE t.`deleted` = ? AND 1 = 2 AND EXISTS(SELECT 1 FROM `t_topic` t2 WHERE t2.`id` = t.`id` AND 1 = 2)");
        listen.AssertParams("%123%(String),false(Boolean)");
        await Db.Query<BlogEntity>()
            .Where(o => tenantIds.Contains(o.Id) && o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("123") && o.CreateTime!.Value.ToString("yyyy-MM-dd").EndsWith("123")
                        && Sql.Exists(Db.Query<Topic>().Where(x => x.Id == o.Id).Where(x => roleIds.Contains(x.Id))))
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id && t1.Title!.Contains("123"))
            .Select((t, t1) => new BlogEntity { Id = t.Id, Content = t.Content, CreateTime = t.CreateTime, Star = t1.Stars }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`content`,t.`create_time`,t1.`stars` AS `star` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` AND t1.`title` LIKE ? WHERE t.`deleted` = ? AND 1 = 2 AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ? AND EXISTS(SELECT 1 FROM `t_topic` t2 WHERE t2.`id` = t.`id` AND 1 = 2)");
        listen.AssertParams("%123%(String),false(Boolean),123%(String),%123(String)");
        await Db.Query<BlogEntity>()
            .Where(o => o.Id == "123" && o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("123") && o.CreateTime!.Value.ToString("yyyy-MM-dd").EndsWith("123"))
            .Where(o => o.Title!.Contains("你好")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `id` = ? AND DATE_FORMAT(`create_time`,'%Y-%m-%d') LIKE ? AND DATE_FORMAT(`create_time`,'%Y-%m-%d') LIKE ? AND `title` LIKE ?");
        listen.AssertParams("false(Boolean),123(String),123%(String),%123(String),%你好%(String)");
    }

    // 日期格式化后的区间、判空、IN；Java 带 false 开关的写法不加条件，C# 直接不写
    [Fact, Upstream("QueryTest10.java#testQuery8")]
    public async Task TestQuery8()
    {
        var ids = new List<string>();
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>()
            .Where(o => ids.Contains(o.Id!) && !ids.Contains(o.Id!)
                        && o.CreateTime!.Value.ToString("yyyy-MM-dd").RangeClosed("2023-01-02", "2023-01-03")
                        && o.CreateTime!.Value.ToString("yyyy-MM-dd").RangeOpen("2023-01-04", "2023-01-06")
                        && o.CreateTime!.Value.ToString("yyyy-MM-dd").RangeOpenClosed("2023-01-07", "2023-01-08")
                        && o.CreateTime!.Value.ToString("yyyy-MM-dd").RangeClosedOpen("2023-01-09", "2023-01-10")
                        && o.CreateTime!.Value.ToString("yyyy-MM-dd") == null
                        && o.CreateTime!.Value.ToString("yyyy-MM-dd") != null
                        && ids.Contains(o.CreateTime!.Value.ToString("yyyy-MM-dd"))
                        && !ids.Contains(o.CreateTime!.Value.ToString("yyyy-MM-dd")))
            .SelectExcept<BlogEntity>(o => new { o.Title, o.Top }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top` FROM `t_blog` t WHERE t.`deleted` = ? AND 1 = 2 AND 1 = 1 AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') BETWEEN ? AND ? AND (DATE_FORMAT(t.`create_time`,'%Y-%m-%d') > ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') < ?) AND (DATE_FORMAT(t.`create_time`,'%Y-%m-%d') > ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') <= ?) AND (DATE_FORMAT(t.`create_time`,'%Y-%m-%d') >= ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') < ?) AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') IS NULL AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') IS NOT NULL AND 1 = 2 AND 1 = 1");
        listen.AssertParams("false(Boolean),2023-01-02(String),2023-01-03(String),2023-01-04(String),2023-01-06(String),2023-01-07(String),2023-01-08(String),2023-01-09(String),2023-01-10(String)");
    }

    /// <summary>Java 的 h2/vo/QueryVO。</summary>
    public sealed class QueryVO
    {
        public string? Id { get; set; }

        public string? Field1 { get; set; }

        public string? Field2 { get; set; }
    }

    // 投影到 VO；整实体去掉几列再加一个子查询列（Java 会同时选出同名的普通列和子查询列，C# 只有子查询列）
    [Fact, Upstream("QueryTest10.java#testVO1", Deviation = "Java 的 selectAll + selectIgnores + setSubQuery 会同时选出同名的普通列（t.`star`）和子查询列（AS `star`）；C# 的成员初始化每个属性只有一列")]
    public async Task TestVO1()
    {
        using var listen = SqlCapture.Listen();
        var query = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).LeftJoin<SysUser>((t, t1, t2) => t.Id == t2.Id)
            .Where((t, t1, t2) => t.Id == "123")
            .Where((t, t1, t2) => t.Id == "123" && t1.Title!.Contains("456") && t2.CreateTime == new DateTime(2021, 1, 1, 1, 1, 0));
        await query.Select((t, t1, t2) => new QueryVO { Id = t.Id, Field1 = t1.Title, Field2 = t2.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`title` AS `field1`,t2.`id` AS `field2` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LEFT JOIN `easy-query-test`.`t_sys_user` t2 ON t.`id` = t2.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? AND t2.`create_time` = ?");
        listen.AssertParams("false(Boolean),123(String),123(String),%456%(String),2021-01-01T01:01(LocalDateTime)");
        await query.Select((t, t1, t2) => new QueryVO { Id = t.Id, Field1 = t1.Title, Field2 = t2.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`title` AS `field1`,t2.`id` AS `field2` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LEFT JOIN `easy-query-test`.`t_sys_user` t2 ON t.`id` = t2.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? AND t2.`create_time` = ?");
        listen.AssertParams("false(Boolean),123(String),123(String),%456%(String),2021-01-01T01:01(LocalDateTime)");

        static string WithoutPlain(string javaSql, string column) => javaSql.Replace("t.`" + column + "`,", "", StringComparison.Ordinal);
        await Db.Query<BlogEntity>().Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2023"))
            .Select(o => new BlogEntity
            {
                Id = o.Id, CreateTime = o.CreateTime, UpdateTime = o.UpdateTime, CreateBy = o.CreateBy, UpdateBy = o.UpdateBy, Deleted = o.Deleted,
                Content = o.Content, Url = o.Url, PublishTime = o.PublishTime, Score = o.Score, Status = o.Status, Order = o.Order, IsTop = o.IsTop,
                Star = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => (int?)Sql.Count())),
            }).ToListAsync(Ct);
        listen.AssertSql(WithoutPlain("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,(SELECT COUNT(*) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `star` FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ?", "star"));
        listen.AssertParams("false(Boolean),false(Boolean),2023%(String)");
        await Db.Query<BlogEntity>().Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2023"))
            .Select(o => new BlogEntity
            {
                Id = o.Id, CreateTime = o.CreateTime, UpdateTime = o.UpdateTime, CreateBy = o.CreateBy, UpdateBy = o.UpdateBy, Deleted = o.Deleted,
                Content = o.Content, Url = o.Url, Star = o.Star, PublishTime = o.PublishTime, Status = o.Status, Order = o.Order, IsTop = o.IsTop,
                Score = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => (decimal?)Sql.Count())),
            }).ToListAsync(Ct);
        listen.AssertSql(WithoutPlain("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,(SELECT COUNT(*) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `score` FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ?", "score"));
        listen.AssertParams("false(Boolean),false(Boolean),2023%(String)");
        await Db.Query<BlogEntity>().Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2023") && Sql.Exists(Db.Query<Topic>().Where(x => x.Id == o.Id)))
            .Select(o => new BlogEntity
            {
                Id = o.Id, CreateTime = o.CreateTime, UpdateTime = o.UpdateTime, CreateBy = o.CreateBy, UpdateBy = o.UpdateBy, Deleted = o.Deleted,
                Content = o.Content, Url = o.Url, Star = o.Star, PublishTime = o.PublishTime, Status = o.Status, Order = o.Order, IsTop = o.IsTop,
                Score = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => (decimal?)Sql.Count())),
            }).ToListAsync(Ct);
        listen.AssertSql(WithoutPlain("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,(SELECT COUNT(*) FROM `t_blog` t2 WHERE t2.`deleted` = ? AND t2.`id` = t.`id`) AS `score` FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ? AND EXISTS(SELECT 1 FROM `t_topic` t1 WHERE t1.`id` = t.`id`)", "score"));
        listen.AssertParams("false(Boolean),false(Boolean),2023%(String)");
    }

    // 投影成 Map（键可以写表达式）；两张同名表连接后排序与投影
    [Fact, Upstream("QueryTest10.java#testDraft9")]
    public async Task TestDraft9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().OrderBy(t => t.Id).Select(o => new Dictionary<string, object?> { { "a", o.Title }, { "b", o.Id }, { "c", o.Stars } }).ToListAsync(Ct);
        await Db.Query<Topic>().OrderBy(t => t.Id).Select(o => new Dictionary<string, object?> { { "title", o.Title ?? "1" }, { "id", o.Id }, { "stars", o.Stars } }).ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL(t.`title`,?) AS `title`,t.`id` AS `id`,t.`stars` AS `stars` FROM `t_topic` t ORDER BY t.`id` ASC");
        await Db.Query<Topic>().LeftJoin<Topic>((t, t1) => t.Id == t1.Id).OrderBy((t, t1) => t.Id).ThenByDescending((t, t1) => t1.CreateTime)
            .Select((t, t1) => new Draft<int?, DateTime?, string?>(t.Stars, t.CreateTime, t1.Title)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`stars` AS `value1`,t.`create_time` AS `value2`,t1.`title` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` ORDER BY t.`id` ASC,t1.`create_time` DESC");
    }

    private const string KeyAlias = "Java 的 selectExpression(g.key1()) 把分组键按普通列选出，不写 AS；C# 的成员初始化 Id = g.Key 与 Java 的 .as(BlogEntity::getId) 一致，写 AS";

    private static string WithKeyAlias(string javaSql, string table) =>
        javaSql.Replace("SELECT " + table + ".`id` FROM", "SELECT " + table + ".`id` AS `id` FROM", StringComparison.Ordinal)
            .Replace("SELECT " + table + ".`id`,", "SELECT " + table + ".`id` AS `id`,", StringComparison.Ordinal);

    // HAVING 里的几种 LIKE；带 false 开关的不加
    [Fact, Upstream("QueryTest10.java#test2", Deviation = KeyAlias)]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(o => o.Id)
            .Having(g => g.Max(x => x.Id)!.Contains("1") && g.Max(x => x.Id)!.StartsWith("3") && g.Max(x => x.Id)!.EndsWith("5")
                         && !g.Max(x => x.Id)!.Contains("1") && !g.Max(x => x.Id)!.StartsWith("3") && !g.Max(x => x.Id)!.EndsWith("5"))
            .Select(g => new BlogEntity { Id = g.Key }).ToListAsync(Ct);
        listen.AssertSql(WithKeyAlias("SELECT t.`id` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY t.`id` HAVING MAX(t.`id`) LIKE ? AND MAX(t.`id`) LIKE ? AND MAX(t.`id`) LIKE ? AND MAX(t.`id`) NOT LIKE ? AND MAX(t.`id`) NOT LIKE ? AND MAX(t.`id`) NOT LIKE ?", "t"));
        listen.AssertParams("false(Boolean),123(String),%1%(String),3%(String),%5(String),%1%(String),3%(String),%5(String)");
    }

    [Fact, Upstream("QueryTest10.java#test3", Deviation = KeyAlias)]
    public async Task Test3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(o => o.Id)
            .Having(g => g.Max(x => x.Id) == "1" && string.Compare(g.Max(x => x.Id), "3") >= 0 && string.Compare(g.Max(x => x.Id), "5") > 0
                         && g.Max(x => x.Id) != "1" && string.Compare(g.Max(x => x.Id), "3") <= 0 && string.Compare(g.Max(x => x.Id), "5") < 0)
            .Select(g => new BlogEntity { Id = g.Key }).ToListAsync(Ct);
        listen.AssertSql(WithKeyAlias("SELECT t.`id` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY t.`id` HAVING MAX(t.`id`) = ? AND MAX(t.`id`) >= ? AND MAX(t.`id`) > ? AND MAX(t.`id`) <> ? AND MAX(t.`id`) <= ? AND MAX(t.`id`) < ?", "t"));
        listen.AssertParams("false(Boolean),123(String),1(String),3(String),5(String),1(String),3(String),5(String)");
    }

    [Fact, Upstream("QueryTest10.java#test4", Deviation = KeyAlias)]
    public async Task Test4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(o => o.Id)
            .Having(g => g.Max(x => x.Id) == g.Min(x => x.Id) && string.Compare(g.Max(x => x.Id), g.Min(x => x.Id)) >= 0 && string.Compare(g.Max(x => x.Id), g.Min(x => x.Id)) > 0
                         && g.Max(x => x.Id) != g.Min(x => x.Id) && string.Compare(g.Max(x => x.Id), g.Min(x => x.Id)) <= 0 && string.Compare(g.Max(x => x.Id), g.Min(x => x.Id)) < 0)
            .Select(g => new BlogEntity { Id = g.Key }).ToListAsync(Ct);
        listen.AssertSql(WithKeyAlias("SELECT t.`id` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY t.`id` HAVING MAX(t.`id`) = MIN(t.`id`) AND MAX(t.`id`) >= MIN(t.`id`) AND MAX(t.`id`) > MIN(t.`id`) AND MAX(t.`id`) <> MIN(t.`id`) AND MAX(t.`id`) <= MIN(t.`id`) AND MAX(t.`id`) < MIN(t.`id`)", "t"));
        listen.AssertParams("false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest10.java#test5", Deviation = KeyAlias)]
    public async Task Test5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t1.Title != null).GroupBy((t, t1) => t1.Id)
            .Select(g => new BlogEntity { Id = g.Key, Score = g.Sum(x => x.Item2.Score) }).ToListAsync(Ct);
        listen.AssertSql(WithKeyAlias("SELECT t1.`id`,SUM(t1.`score`) AS `score` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL GROUP BY t1.`id`", "t1"));
        listen.AssertParams("false(Boolean)");
    }
}
