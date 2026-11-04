using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：vo/BlogGroupIdAndName。</summary>
public sealed class BlogGroupIdAndName
{
    public string? Id { get; set; }

    public long? IdCount { get; set; }
}

/// <summary>
/// 对应 Java：MyTest1.java 与 MyTest2.java（两者只差 Draft / Tuple，C# 都是 Draft；日期加减、取部分、相差，投影后再分组、分组后再排序等多层派生表）。
/// Java 的 a.duration(b).toDays() 是 b - a 的天数，C# 写 (b - a).TotalDays。
/// </summary>
public sealed class MyTest1(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string TopicWhere = " FROM `t_topic` t WHERE t.`title` IS NOT NULL AND t.`create_time` <= ?";
    private static readonly DateTime Le = new(2021, 3, 4, 5, 6, 0);

    // 插入一篇指定时间的博客，执行完删除
    private async Task WithBlogAsync(string id, DateTime createTime, DateTime updateTime, Func<Task> body)
    {
        await Db.Delete<BlogEntity>().WhereById(id).DisableLogicDelete().AllowDeleteStatement(true).ExecuteAsync(Ct);
        await Db.Insert(new BlogEntity
        {
            Id = id, CreateBy = "z", CreateTime = createTime, UpdateBy = "z", UpdateTime = updateTime, Title = "titlez", Content = "contentz",
            Url = "http://blog.easy-query.com/z", Star = 1, Score = 1.2m, Status = 1, Order = 1.2m, IsTop = false, Top = true, Deleted = false,
        }).ExecuteAsync(Ct);
        try
        {
            await body();
        }
        finally
        {
            await Db.Delete<BlogEntity>().WhereById(id).DisableLogicDelete().AllowDeleteStatement(true).ExecuteAsync(Ct);
        }
    }

    // 分组后组内非聚合的列：g.Column(x => …)
    [Fact, Upstream("MyTest1.java#testDraft1"), Upstream("MyTest2.java#testTuple1"), Upstream("MyTest1.java#testDraft1_1"), Upstream("MyTest2.java#testTuple1_1")]
    public async Task TestDraft1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Title!.Length == 123).GroupBy(o => o.Content)
            .Select(g => new Draft<string?, int>(g.Key, g.Column(x => x.Content!.Length))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`content` AS `value1`,CHAR_LENGTH(t.`content`) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? AND CHAR_LENGTH(t.`title`) = ? GROUP BY t.`content`");
        listen.AssertParams("false(Boolean),123(Integer)");
    }

    [Fact, Upstream("MyTest1.java#testDraft2"), Upstream("MyTest2.java#testTuple2")]
    public async Task TestDraft2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => int.Parse(o.Title!) == 123).GroupBy(o => o.Content)
            .Select(g => new Draft<string?, int>(g.Key, g.Column(x => x.Content!.Length))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`content` AS `value1`,CHAR_LENGTH(t.`content`) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? AND CAST(t.`title` AS SIGNED) = ? GROUP BY t.`content`");
    }

    [Fact, Upstream("MyTest1.java#testDraft3"), Upstream("MyTest2.java#testTuple3"), Upstream("MyTest1.java#testDraft4"), Upstream("MyTest2.java#testTuple4"), Upstream("MyTest1.java#testDraft5"), Upstream("MyTest2.java#testTuple5")]
    public async Task TestDraft3()
    {
        var first = await Db.Query<BlogEntity>().Select(o => new Draft<DateTime?>(o.CreateTime)).FirstOrDefaultAsync(Ct);
        Assert.Equal((2020, 1, 1), (first!.Value1!.Value.Year, first.Value1.Value.Month, first.Value1.Value.Day));
        using var listen = SqlCapture.Listen();
        var plus2 = await Db.Query<BlogEntity>().Select(o => new Draft<DateTime>(o.CreateTime!.Value.AddDays(2))).FirstOrDefaultAsync(Ct);
        Assert.Equal(new DateTime(2020, 1, 3), plus2!.Value1.Date);
        listen.AssertSql("SELECT date_add(t.`create_time`, interval (?) day) AS `value1` FROM `t_blog` t WHERE t.`deleted` = ? LIMIT 1");
        listen.AssertParams("2(Long),false(Boolean)");
        var months = await Db.Query<BlogEntity>().Select(o => new Draft<DateTime>(o.CreateTime!.Value.AddMonths(2))).FirstOrDefaultAsync(Ct);
        Assert.Equal(new DateTime(2020, 3, 1), months!.Value1.Date);
        listen.AssertSql("SELECT date_add(t.`create_time`, interval (?) month) AS `value1` FROM `t_blog` t WHERE t.`deleted` = ? LIMIT 1");
        var both = await Db.Query<BlogEntity>().Select(o => new Draft<DateTime>(o.CreateTime!.Value.AddMonths(12).AddDays(1))).FirstOrDefaultAsync(Ct);
        Assert.Equal(new DateTime(2021, 1, 2), both!.Value1.Date);
        listen.AssertSql("SELECT date_add(date_add(t.`create_time`, interval (?) month), interval (?) day) AS `value1` FROM `t_blog` t WHERE t.`deleted` = ? LIMIT 1");
        listen.AssertParams("12(Long),1(Long),false(Boolean)");
    }

    [Fact, Upstream("MyTest1.java#testDraft6"), Upstream("MyTest2.java#testTuple6")]
    public Task TestDraft6() => WithBlogAsync("123456zz", new DateTime(2022, 1, 1), new DateTime(2022, 1, 1), async () =>
    {
        using var listen = SqlCapture.Listen();
        var draft = await Db.Query<BlogEntity>().WhereById("123456zz").Where(o => o.CreateTime!.Value.AddYears(1) <= new DateTime(2023, 1, 1))
            .Select(o => new Draft<DateTime, DateTime, DateTime>(o.CreateTime!.Value.AddMonths(12).AddDays(1), o.CreateTime!.Value.AddYears(1), o.CreateTime!.Value.AddSeconds(3)))
            .FirstOrDefaultAsync(Ct);
        Assert.Equal((new DateTime(2023, 1, 2), new DateTime(2023, 1, 1), new DateTime(2022, 1, 1, 0, 0, 3)), (draft!.Value1, draft.Value2, draft.Value3));
        listen.AssertSql("SELECT date_add(date_add(t.`create_time`, interval (?) month), interval (?) day) AS `value1`,date_add(t.`create_time`, interval (?) year) AS `value2`,date_add(t.`create_time`, interval (?) second) AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? AND date_add(t.`create_time`, interval (?) year) <= ? LIMIT 1");
        listen.AssertParams("12(Long),1(Long),1(Long),3(Long),false(Boolean),123456zz(String),1(Long),2023-01-01T00:00(LocalDateTime)");
    });

    // 取部分：星期几周日为 0（C# 的 DayOfWeek 同样如此）
    [Theory, Upstream("MyTest1.java#testDraft7"), Upstream("MyTest2.java#testTuple7"), Upstream("MyTest1.java#testDraft8"), Upstream("MyTest2.java#testTuple8")]
    [InlineData("123456zz7", 3)]
    [InlineData("123456zz8", 13)]
    public Task TestDraft7(string id, int hour) => WithBlogAsync(id, new DateTime(2022, 1, 2, hour, 4, 5), new DateTime(2022, 1, 1), async () =>
    {
        using var listen = SqlCapture.Listen();
        var d = await Db.Query<BlogEntity>().WhereById(id).Where(o => o.CreateTime!.Value.AddYears(1) <= new DateTime(2023, 2, 1))
            .Select(o => new Draft<int, int, int, int, int, int, int>(o.CreateTime!.Value.Year, o.CreateTime!.Value.Month, o.CreateTime!.Value.Day,
                o.CreateTime!.Value.Hour, o.CreateTime!.Value.Minute, o.CreateTime!.Value.Second, (int)o.CreateTime!.Value.DayOfWeek))
            .FirstOrDefaultAsync(Ct);
        Assert.Equal((2022, 1, 2, hour, 4, 5, 0), (d!.Value1, d.Value2, d.Value3, d.Value4, d.Value5, d.Value6, d.Value7));
        listen.AssertSql("SELECT YEAR(t.`create_time`) AS `value1`,MONTH(t.`create_time`) AS `value2`,DAYOFMONTH(t.`create_time`) AS `value3`,HOUR(t.`create_time`) AS `value4`,MINUTE(t.`create_time`) AS `value5`,SECOND(t.`create_time`) AS `value6`,(DAYOFWEEK(t.`create_time`)-1) AS `value7` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? AND date_add(t.`create_time`, interval (?) year) <= ? LIMIT 1");
        listen.AssertParams("false(Boolean)," + id + "(String),1(Long),2023-02-01T00:00(LocalDateTime)");
    });

    // Java 的 update.duration(create)：C# 的 (create - update)
    [Fact, Upstream("MyTest1.java#testDraft9"), Upstream("MyTest2.java#testTuple9")]
    public Task TestDraft9() => WithBlogAsync("123456zz9", new DateTime(2022, 1, 2, 3, 4, 5), new DateTime(2022, 2, 3, 4, 5, 6), async () =>
    {
        using var listen = SqlCapture.Listen();
        var d = await Db.Query<BlogEntity>().WhereById("123456zz9")
            .Select(o => new Draft<long, long, long, long, long, long, long>(
                (long)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalDays, (long)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalHours,
                (long)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalMinutes, (long)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalSeconds,
                (long)(o.CreateTime!.Value - o.CreateTime!.Value.AddDays(1)).TotalDays, (long)(o.CreateTime!.Value - o.CreateTime!.Value.AddSeconds(2)).TotalSeconds,
                (long)(o.CreateTime!.Value - o.CreateTime!.Value.AddMinutes(3)).TotalMinutes))
            .FirstOrDefaultAsync(Ct);
        Assert.Equal((-32L, -769L, -46141L, -2768461L, -1L, -2L, -3L), (d!.Value1, d.Value2, d.Value3, d.Value4, d.Value5, d.Value6, d.Value7));
        listen.AssertSql("SELECT timestampdiff(DAY, t.`update_time`, t.`create_time`) AS `value1`,timestampdiff(HOUR, t.`update_time`, t.`create_time`) AS `value2`,timestampdiff(MINUTE, t.`update_time`, t.`create_time`) AS `value3`,timestampdiff(SECOND, t.`update_time`, t.`create_time`) AS `value4`,timestampdiff(DAY, date_add(t.`create_time`, interval (?) day), t.`create_time`) AS `value5`,timestampdiff(SECOND, date_add(t.`create_time`, interval (?) second), t.`create_time`) AS `value6`,timestampdiff(MINUTE, date_add(t.`create_time`, interval (?) minute), t.`create_time`) AS `value7` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? LIMIT 1");
        listen.AssertParams("1(Long),2(Long),3(Long),false(Boolean),123456zz9(String)");
    });

    [Fact, Upstream("MyTest1.java#testDraft11"), Upstream("MyTest2.java#testTuple11")]
    public Task TestDraft11() => WithBlogAsync("123456zz9xx1", new DateTime(2022, 1, 2, 3, 4, 5), new DateTime(2022, 2, 3, 4, 5, 6), async () =>
    {
        using var listen = SqlCapture.Listen();
        var d = await Db.Query<BlogEntity>().WhereById("123456zz9xx1")
            .Select(o => new Draft<long, long, long, long, long, long, long>(
                (long)(o.UpdateTime!.Value - o.CreateTime!.Value).TotalDays, (long)(o.UpdateTime!.Value - o.CreateTime!.Value).TotalHours,
                (long)(o.UpdateTime!.Value - o.CreateTime!.Value).TotalMinutes, (long)(o.UpdateTime!.Value - o.CreateTime!.Value).TotalSeconds,
                (long)(o.CreateTime!.Value.AddDays(1) - o.CreateTime!.Value).TotalDays, (long)(o.CreateTime!.Value.AddSeconds(2) - o.CreateTime!.Value).TotalSeconds,
                (long)(o.CreateTime!.Value.AddMinutes(3) - o.CreateTime!.Value).TotalMinutes))
            .FirstOrDefaultAsync(Ct);
        Assert.Equal((32L, 769L, 46141L, 2768461L, 1L, 2L, 3L), (d!.Value1, d.Value2, d.Value3, d.Value4, d.Value5, d.Value6, d.Value7));
        listen.AssertSql("SELECT timestampdiff(DAY, t.`create_time`, t.`update_time`) AS `value1`,timestampdiff(HOUR, t.`create_time`, t.`update_time`) AS `value2`,timestampdiff(MINUTE, t.`create_time`, t.`update_time`) AS `value3`,timestampdiff(SECOND, t.`create_time`, t.`update_time`) AS `value4`,timestampdiff(DAY, t.`create_time`, date_add(t.`create_time`, interval (?) day)) AS `value5`,timestampdiff(SECOND, t.`create_time`, date_add(t.`create_time`, interval (?) second)) AS `value6`,timestampdiff(MINUTE, t.`create_time`, date_add(t.`create_time`, interval (?) minute)) AS `value7` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? LIMIT 1");
    });

    [Fact, Upstream("MyTest1.java#testExists"), Upstream("MyTest2.java#testExists")]
    public async Task TestExists()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(o => o.Title!.Contains("123") && Sql.Exists(Db.Query<BlogEntity>().Where(x => x.Id == o.Id))
                        && (long)(new DateTime(2021, 1, 1, 1, 1, 0) - o.CreateTime!.Value).TotalDays <= 0L)
            .Select(o => new Draft<string?, string?, DateTime?, string?>(o.Id, o.Title, o.CreateTime,
                Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => x.Title))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`title` AS `value2`,t.`create_time` AS `value3`,(SELECT t2.`title` FROM `t_blog` t2 WHERE t2.`deleted` = ? AND t2.`id` = t.`id`) AS `value4` FROM `t_topic` t WHERE t.`title` LIKE ? AND EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AND timestampdiff(DAY, t.`create_time`, ?) <= ?");
        listen.AssertParams("false(Boolean),%123%(String),false(Boolean),2021-01-01T01:01(LocalDateTime),0(Long)");
    }

    [Fact, Upstream("MyTest1.java#testSelect1"), Upstream("MyTest2.java#testSelect1")]
    public async Task TestSelect1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).Select(o => new BlogEntity { Title = "1", Id = o.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT ? AS `title`,t.`title` AS `id` FROM `t_topic` t WHERE t.`title` IS NOT NULL");
        listen.AssertParams("1(String)");
    }

    [Fact, Upstream("MyTest1.java#testSelect2"), Upstream("MyTest2.java#testSelect2")]
    public async Task TestSelect2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).Select(o => new BlogEntity { Id = o.Title, Title = Sql.Raw<string>("1+1+{0}", o.Stars) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` AS `id`,1+1+t.`stars` AS `title` FROM `t_topic` t WHERE t.`title` IS NOT NULL");
    }

    [Fact, Upstream("MyTest1.java#testSelect3", NotApplicable = "Java 对同一个属性 id 赋值两次，生成两个 AS `id`；C# 的对象初始化器不允许重复成员"), Upstream("MyTest2.java#testSelect3", NotApplicable = "Java 对同一个属性 id 赋值两次，生成两个 AS `id`；C# 的对象初始化器不允许重复成员"),
     Upstream("MyTest1.java#testSelect4", NotApplicable = "Java 的 selectExpression 产出两个 `title` 列；C# 的投影成员不能重名"), Upstream("MyTest2.java#testSelect4", NotApplicable = "Java 的 selectExpression 产出两个 `title` 列；C# 的投影成员不能重名")]
    public void TestSelect3() { }

    [Fact, Upstream("MyTest1.java#testSelect5"), Upstream("MyTest2.java#testSelect5")]
    public async Task TestSelect5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).Select(o => new BlogEntity { Id = o.Title, Title = o.Title!.Substring(1, 2) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` AS `id`,SUBSTR(t.`title`,2,2) AS `title` FROM `t_topic` t WHERE t.`title` IS NOT NULL");
    }

    private Query<BlogEntity> TitleSubstr() => Db.Query<Topic>().Where(o => o.Title != null && o.CreateTime <= Le)
        .Select(o => new BlogEntity { Id = o.Title, Title = o.Title!.Substring(1, 2) });

    private const string TitleSubstrSql = "SELECT t.`title` AS `id`,SUBSTR(t.`title`,2,2) AS `title`" + TopicWhere;

    // 投影 → 分组 → 投影 → 排序：每一步都包一层派生表
    [Fact, Upstream("MyTest1.java#testSelect6"), Upstream("MyTest2.java#testSelect6")]
    public async Task TestSelect6()
    {
        using var listen = SqlCapture.Listen();
        await TitleSubstr().GroupBy(o => o.Id).Select(g => new BlogGroupIdAndName { Id = g.Key, IdCount = g.Count() }).OrderByDescending(o => o.IdCount).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`id` AS `id`,t2.`id_count` AS `id_count` FROM (SELECT t1.`id` AS `id`,COUNT(*) AS `id_count` FROM (" + TitleSubstrSql + ") t1 GROUP BY t1.`id`) t2 ORDER BY t2.`id_count` DESC");
        listen.AssertParams("2021-03-04T05:06(LocalDateTime)");
    }

    [Fact, Upstream("MyTest1.java#testSelect6_1"), Upstream("MyTest2.java#testSelect6_1")]
    public async Task TestSelect6_1()
    {
        using var listen = SqlCapture.Listen();
        await TitleSubstr().GroupBy(o => o.Id).Select(g => new BlogGroupIdAndName { Id = g.Key, IdCount = g.Count() }).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id` AS `id`,COUNT(*) AS `id_count` FROM (" + TitleSubstrSql + ") t1 GROUP BY t1.`id`");
    }

    [Fact, Upstream("MyTest1.java#testSelect6_2"), Upstream("MyTest2.java#testSelect6_2")]
    public async Task TestSelect6_2()
    {
        using var listen = SqlCapture.Listen();
        await TitleSubstr().GroupBy(o => o.Id).Select(g => new { Id = g.Key, IdCount = g.Min(x => x.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id` AS `id`,MIN(t1.`id`) AS `id_count` FROM (" + TitleSubstrSql + ") t1 GROUP BY t1.`id`");
    }

    [Fact, Upstream("MyTest1.java#testSelect6_3"), Upstream("MyTest2.java#testSelect6_3")]
    public async Task TestSelect6_3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().AsTracking().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).Where((t, b2) => t.Title != null && t.CreateTime <= Le)
            .GroupBy((t1, b2) => new { t1.Id, b2.Star })
            .Select(g => new BlogGroupIdAndName { Id = g.Key.Id, IdCount = g.Count(e => e.Item2.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `id`,COUNT(t1.`id`) AS `id_count` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`title` IS NOT NULL AND t.`create_time` <= ? GROUP BY t.`id`,t1.`star`");
        listen.AssertParams("false(Boolean),2021-03-04T05:06(LocalDateTime)");
    }

    private Query<Draft<string?, string>> TitleDraft() => Db.Query<Topic>().Where(o => o.Title != null && o.CreateTime <= Le)
        .Select(o => new Draft<string?, string>(o.Title, o.Title!.Substring(1, 2)));

    private const string TitleDraftSql = "SELECT t.`title` AS `value1`,SUBSTR(t.`title`,2,2) AS `value2`" + TopicWhere;

    [Fact, Upstream("MyTest1.java#testSelect7"), Upstream("MyTest2.java#testSelect7")]
    public async Task TestSelect7()
    {
        using var listen = SqlCapture.Listen();
        await TitleDraft().GroupBy(o => o.Value1).Select(g => new Draft<string?, long>(g.Key, g.Count())).OrderByDescending(o => o.Value1).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`value1` AS `value1`,t2.`value2` AS `value2` FROM (SELECT t1.`value1` AS `value1`,COUNT(*) AS `value2` FROM (" + TitleDraftSql + ") t1 GROUP BY t1.`value1`) t2 ORDER BY t2.`value1` DESC");
    }

    [Fact, Upstream("MyTest1.java#testSelect8"), Upstream("MyTest2.java#testSelect8")]
    public async Task TestSelect8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null && o.CreateTime <= Le).Select(o => o.Title).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title`" + TopicWhere);
        listen.AssertParams("2021-03-04T05:06(LocalDateTime)");
        await Db.Query<Topic>().Where(o => o.Title != null && o.CreateTime <= Le).Select(o => decimal.Parse(o.Title!)).ToListAsync(Ct);
    }

    [Fact, Upstream("MyTest1.java#testSelect9"), Upstream("MyTest2.java#testSelect9")]
    public async Task TestSelect9()
    {
        using var listen = SqlCapture.Listen();
        await TitleDraft().GroupBy(o => o.Value1).OrderByDescending(g => g.Key).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`value1` AS `value1`,COUNT(*) AS `value2` FROM (" + TitleDraftSql + ") t1 GROUP BY t1.`value1` ORDER BY t1.`value1` DESC");
    }

    [Fact, Upstream("MyTest1.java#testSelect10"), Upstream("MyTest2.java#testSelect10")]
    public async Task TestSelect10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null)
            .Select(o => new TopicSubQueryBlog { Id = o.Id, Stars = o.Stars, Title = o.Title, CreateTime = o.CreateTime,
                BlogCount = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => Sql.Count())) })
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`,(SELECT COUNT(*) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("MyTest1.java#testGroup11"), Upstream("MyTest2.java#testGroup11")]
    public async Task TestGroup11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null && o.CreateTime <= Le).GroupBy(o => o.Title!.Substring(1, 2))
            .Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(x => x.Title!.Substring(1, 2)) }).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`title`,2,2) AS `id`,COUNT(SUBSTR(t.`title`,2,2)) AS `star`" + TopicWhere + " GROUP BY SUBSTR(t.`title`,2,2)");
    }

    [Fact, Upstream("MyTest1.java#testGroup12"), Upstream("MyTest2.java#testGroup12"), Upstream("MyTest1.java#testGroup13"), Upstream("MyTest2.java#testGroup13")]
    public async Task TestGroup12()
    {
        const string sql = "SELECT t1.`value1` AS `id`,COUNT(t1.`value2`) AS `star` FROM (" + TitleDraftSql + ") t1 GROUP BY t1.`value1`";
        using var listen = SqlCapture.Listen();
        await TitleDraft().GroupBy(o => o.Value1).Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(x => x.Value2) }).ToListAsync(Ct);
        listen.AssertSql(sql);
        await TitleDraft().GroupBy(o => o.Value1).OrderBy(g => g.Key).Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(x => x.Value2) }).ToListAsync(Ct);
        listen.AssertSql(sql + " ORDER BY t1.`value1` ASC");
    }

    [Fact, Upstream("MyTest1.java#testGroup14"), Upstream("MyTest2.java#testGroup14")]
    public async Task TestGroup14()
    {
        using var listen = SqlCapture.Listen();
        await TitleDraft().GroupBy(o => o.Value1).Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(x => x.Value2) }).OrderBy(o => o.Star).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`id` AS `id`,t2.`star` AS `star` FROM (SELECT t1.`value1` AS `id`,COUNT(t1.`value2`) AS `star` FROM (" + TitleDraftSql + ") t1 GROUP BY t1.`value1`) t2 ORDER BY t2.`star` ASC");
    }

    // Java 的 toNumber(BigDecimal)：C# 的 Convert.ToDecimal；joining：g.StringJoin
    [Fact, Upstream("MyTest1.java#testGroup15"), Upstream("MyTest2.java#testGroup15")]
    public async Task TestGroup15()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null && o.CreateTime <= Le).GroupBy(o => o.Title!.Substring(1, 2))
            .Select(g => new BlogEntity { Id = g.Key, Title = g.Max(x => x.Title), Score = g.Min(x => Convert.ToDecimal(x.Stars)), Content = g.StringJoin(x => x.Id, ",") })
            .ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`title`,2,2) AS `id`,MAX(t.`title`) AS `title`,MIN(CAST(t.`stars` AS DECIMAL(36,18))) AS `score`,GROUP_CONCAT(t.`id` SEPARATOR ?) AS `content`" + TopicWhere + " GROUP BY SUBSTR(t.`title`,2,2)");
        listen.AssertParams(",(String),2021-03-04T05:06(LocalDateTime)");
    }

    // 分组后直接取键（Java 的 groupBy(…).toList()）：C# 的 Keys()
    [Fact, Upstream("MyTest1.java#testGroup16"), Upstream("MyTest2.java#testGroup16"), Upstream("MyTest1.java#testGroup17"), Upstream("MyTest2.java#testGroup17")]
    public async Task TestGroup16()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).GroupBy(o => o.Title!.Substring(1, 2)).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(`title`,2,2) FROM `t_topic` WHERE `title` IS NOT NULL GROUP BY SUBSTR(`title`,2,2)");
        await Db.Query<Topic>().Where(o => o.Title != null).GroupBy(o => o.Title!.Substring(1, 2)).Having(g => g.Count() >= 1L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(`title`,2,2) FROM `t_topic` WHERE `title` IS NOT NULL GROUP BY SUBSTR(`title`,2,2) HAVING COUNT(*) >= ?");
        listen.AssertParams("1(Long)");
    }

    [Fact, Upstream("MyTest1.java#testGroup18"), Upstream("MyTest2.java#testGroup18")]
    public async Task TestGroup18()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).GroupBy(o => o.Title!.Substring(1, 2)).Having(g => g.Count() >= 1L)
            .Select(g => new Draft<string, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`title`,2,2) AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t WHERE t.`title` IS NOT NULL GROUP BY SUBSTR(t.`title`,2,2) HAVING COUNT(*) >= ?");
    }

    private const string TopicBlogJoin = " FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` IS NOT NULL AND t1.`score` IS NOT NULL";

    [Fact, Upstream("MyTest1.java#testGroup19"), Upstream("MyTest2.java#testGroup19")]
    public async Task TestGroup19()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id != null && t1.Score != null)
            .GroupBy((t, t1) => new { Title = t.Title!.Substring(1, 2), Score = t1.Score ?? 0m }).Having(g => g.Count() >= 1L)
            .Select(g => new Draft<string, decimal, long>(g.Key.Title, g.Key.Score, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`title`,2,2) AS `value1`,IFNULL(t1.`score`,?) AS `value2`,COUNT(*) AS `value3`" + TopicBlogJoin + " GROUP BY SUBSTR(t.`title`,2,2),IFNULL(t1.`score`,?) HAVING COUNT(*) >= ?");
        listen.AssertParams("0(BigDecimal),false(Boolean),0(BigDecimal),1(Long)");
    }

    [Fact, Upstream("MyTest1.java#testGroup20"), Upstream("MyTest2.java#testGroup20")]
    public async Task TestGroup20()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id != null && t1.Score != null)
            .GroupBy((t, t1) => new { Title = Sql.Raw<string>("SUBSTR({0},2,2)", t.Title), Score = t1.Score ?? 0m }).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`title`,2,2),IFNULL(t1.`score`,?)" + TopicBlogJoin + " GROUP BY SUBSTR(t.`title`,2,2),IFNULL(t1.`score`,?)");
        listen.AssertParams("0(BigDecimal),false(Boolean),0(BigDecimal)");
    }

    [Fact, Upstream("MyTest1.java#testGroup21"), Upstream("MyTest2.java#testGroup21")]
    public async Task TestGroup21()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id != null && t1.Score != null)
            .GroupBy((t, t1) => new { Title = Sql.Raw<string>("SUBSTR({0},2,2)", t.Title), Score = t1.Score ?? 0m })
            .Select(g => new Draft<string, long>(g.Key.Title, g.Count(e => e.Item1.Title))).ToListAsync(Ct);
        listen.AssertSql("SELECT SUBSTR(t.`title`,2,2) AS `value1`,COUNT(t.`title`) AS `value2`" + TopicBlogJoin + " GROUP BY SUBSTR(t.`title`,2,2),IFNULL(t1.`score`,?)");
        listen.AssertParams("false(Boolean),0(BigDecimal)");
    }

    [Fact, Upstream("MyTest1.java#testGroup22"), Upstream("MyTest2.java#testGroup22")]
    public async Task TestGroup22()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id != null && t1.Score != null)
            .GroupBy((t, t1) => new { Time = Sql.Raw<DateTime?>("IFNULL({0},{1})", t.CreateTime, new DateTime(2022, 1, 1, 1, 2, 0)), Score = t1.Score ?? 0m })
            .Select(g => new Draft<DateTime?, long>(g.Key.Time, g.Count(e => e.Item1.Title))).ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL(t.`create_time`,?) AS `value1`,COUNT(t.`title`) AS `value2`" + TopicBlogJoin + " GROUP BY IFNULL(t.`create_time`,?),IFNULL(t1.`score`,?)");
        listen.AssertParams("2022-01-01T01:02(LocalDateTime),false(Boolean),2022-01-01T01:02(LocalDateTime),0(BigDecimal)");
    }
}
