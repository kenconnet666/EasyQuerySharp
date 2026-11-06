// 条件在数据库里执行：字符串比较是要翻译的写法本身，不适用 CA1309 / CA1310
#pragma warning disable CA1309, CA1310

using System.Linq.Expressions;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest9.java（计数、要求有结果的查询及其异常、判空、IFNULL、日期格式、分组投影、投影成字典、HAVING）。
/// Java 的 firstNotNull / singleNotNull / findNotNull / required 对应 <c>FirstAsync</c> / <c>SingleAsync</c> / <c>FindOrThrowAsync</c> / <c>EnsureAnyAsync</c>，
/// 查不到时都抛 <see cref="NotFoundException"/>，默认消息取实体 <c>[Table(NotFoundMessage = …)]</c>（Java 的 @EasyAssertMessage）。
/// </summary>
public sealed class QueryTest9(MySqlFixture fixture) : MySqlTest(fixture)
{
    private sealed class MyAppException(string message) : Exception(message);

    [Fact, Upstream("QueryTest9.java#selectCount1")]
    public async Task SelectCount1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Id == "123").CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) FROM `t_topic` WHERE `id` = ?");
    }

    [Fact, Upstream("QueryTest9.java#selectCount2"), Upstream("QueryTest9.java#selectCount3")]
    public async Task SelectCount2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((o, _) => o.Id == "123").CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?");
    }

    private Query<Topic, BlogEntity> JoinedById(string id) =>
        Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((o, _) => o.Id == id);

    [Fact, Upstream("QueryTest9.java#selectThrow3")]
    public async Task SelectThrow3()
    {
        using var listen = SqlCapture.Listen();
        var ex = await Assert.ThrowsAsync<MyAppException>(() => JoinedById(Guid.NewGuid().ToString()).FirstAsync(() => new MyAppException("asv"), Ct));
        Assert.Equal("asv", ex.Message);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ? LIMIT 1");
    }

    [Fact, Upstream("QueryTest9.java#selectThrow4")]
    public async Task SelectThrow4()
    {
        using var listen = SqlCapture.Listen();
        var ex = await Assert.ThrowsAsync<MyAppException>(() => JoinedById(Guid.NewGuid().ToString()).SingleAsync(() => new MyAppException("asv"), Ct));
        Assert.Equal("asv", ex.Message);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?");
    }

    [Fact, Upstream("QueryTest9.java#selectThrow5")]
    public async Task SelectThrow5() =>
        Assert.Equal("1", (await Assert.ThrowsAsync<NotFoundException>(() => JoinedById(Guid.NewGuid().ToString()).SingleAsync("1", Ct))).Message);

    [Fact, Upstream("QueryTest9.java#selectThrow6")]
    public async Task SelectThrow6() =>
        Assert.Equal("1", (await Assert.ThrowsAsync<NotFoundException>(() => JoinedById(Guid.NewGuid().ToString()).FirstAsync("1", Ct))).Message);

    [Fact, Upstream("QueryTest9.java#selectThrow7", Deviation = "Java 抛 EasyQuerySingleMoreElementException；C# 多于一行时抛 InvalidOperationException（与 LINQ 的 Single 一致）")]
    public async Task SelectThrow7() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id).SingleOrDefaultAsync(Ct));

    [Fact, Upstream("QueryTest9.java#selectThrow9", Deviation = "Java 按方法分 First / Single / Find / Required 四种异常，C# 都是 NotFoundException；多于一行时 C# 抛 InvalidOperationException，消息不同")]
    public async Task SelectThrow9()
    {
        using var listen = SqlCapture.Listen();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => JoinedById(Guid.NewGuid().ToString()).FirstAsync(Ct));
        Assert.Equal("未找到主题信息", ex.Message);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ? LIMIT 1");

        ex = await Assert.ThrowsAsync<NotFoundException>(() => JoinedById(Guid.NewGuid().ToString()).SingleAsync(Ct));
        Assert.Equal("未找到主题信息", ex.Message);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<Topic>().SingleOrDefaultAsync(Ct));
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<Topic>().SingleAsync(Ct));
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`");

        ex = await Assert.ThrowsAsync<NotFoundException>(() => Db.Query<Topic>().FindOrThrowAsync("xaaaaa", cancellationToken: Ct));
        Assert.Equal("未找到主题信息", ex.Message);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ?");
        Assert.Null(await Db.Query<Topic>().FindAsync("xaaaaa", Ct));
        ex = await Assert.ThrowsAsync<NotFoundException>(() => Db.Query<Topic>().FindOrThrowAsync("xaaaaa", "aaaa", Ct));
        Assert.Equal("aaaa", ex.Message);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ?");

        ex = await Assert.ThrowsAsync<NotFoundException>(() => Db.Query<Topic>().WhereById("xxxaassd").EnsureAnyAsync(cancellationToken: Ct));
        Assert.Equal("未找到主题信息", ex.Message);
        listen.AssertSql("SELECT  1  FROM `t_topic` WHERE `id` = ? LIMIT 1");
        ex = await Assert.ThrowsAsync<NotFoundException>(() => Db.Query<Topic>().WhereById("xxxaassd").EnsureAnyAsync("xxxx", Ct));
        Assert.Equal("xxxx", ex.Message);
        listen.AssertSql("SELECT  1  FROM `t_topic` WHERE `id` = ? LIMIT 1");
        var custom = await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<Topic>().WhereById("xxxaassd").EnsureAnyAsync(() => new InvalidOperationException("xxxx1"), Ct));
        Assert.Equal("xxxx1", custom.Message);
        listen.AssertSql("SELECT  1  FROM `t_topic` WHERE `id` = ? LIMIT 1");
        await Db.Query<Topic>().EnsureAnyAsync(cancellationToken: Ct);
        listen.AssertSql("SELECT  1  FROM `t_topic` LIMIT 1");
    }

    // Java 用例没有断言：多次 where、联表后排序取第一条、联表投影、分组投影
    [Fact, Upstream("QueryTest9.java#entityQuery1")]
    public async Task EntityQuery1()
    {
        await Db.Query<Topic>().Where(o => o.Id == "1" && o.Title!.Contains("xxx"))
            .Where(o => o.Id == "1" && o.Title!.Contains("xxx") && o.CreateTime >= DateTime.Now)
            .Where(o => o.Id == "1" && o.Title!.Contains("xxx") && o.CreateTime >= DateTime.Now).ToListAsync(Ct);
        var joined = Db.Query<Topic>().LeftJoin<Topic>((a, b) => a.Id == b.Id)
            .Where((a, b) => a.Title == "1" && b.CreateTime >= new DateTime(2021, 1, 1, 1, 1, 0));
        await joined.OrderBy((a, _) => a.Title).OrderByDescending((a, _) => a.Id).FirstOrDefaultAsync(Ct);
        await joined.OrderBy((a, _) => a.Title).Select((a, _) => new Topic { Title = a.Title, Stars = a.Stars }).FirstOrDefaultAsync(Ct);
        await Db.Query<Topic>().Where(o => o.Title == "title" && o.Id == "1").GroupBy(o => o.Title)
            .Select(g => new Topic { Title = g.Key, Stars = (int)g.Count(x => x.Id) }).ToListAsync(Ct);
    }

    [Fact, Upstream("QueryTest9.java#testDsl1", Deviation = "Java 的 selectExpression 里不带别名的格式化列映射不到属性；C# 投影必须写出属性，这里映射到 Alias")]
    public async Task TestDsl1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Stars == 123).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `stars` = ?");
        listen.AssertParams("123(Integer)");

        var query = Db.Query<Topic>().Where(o => o.Title == "title" && o.Id == "1");
        await query.OrderBy(o => o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss"))
            .Select(o => new Topic { Title = o.Title, Id = o.Id, Alias = o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss") }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title`,t.`id`,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') AS `alias` FROM `t_topic` t WHERE t.`title` = ? AND t.`id` = ? ORDER BY DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') ASC");
        listen.AssertParams("title(String),1(String)");
        await query.OrderByDescending(o => o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss"))
            .Select(o => new Topic { Title = o.Title, Id = o.Id, Alias = o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss") }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title`,t.`id`,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') AS `alias` FROM `t_topic` t WHERE t.`title` = ? AND t.`id` = ? ORDER BY DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') DESC");
        listen.AssertParams("title(String),1(String)");
        await query.OrderByDescending(o => o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss")).OrderByRaw(o => Sql.Raw<object>("IFNULL({0},'') ASC", o.Stars))
            .Select(o => new Topic { Title = o.Title, Id = o.Id, Alias = o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss") }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title`,t.`id`,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') AS `alias` FROM `t_topic` t WHERE t.`title` = ? AND t.`id` = ? ORDER BY DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') DESC,IFNULL(t.`stars`,'') ASC");
        listen.AssertParams("title(String),1(String)");
    }

    [Fact, Upstream("QueryTest9.java#testDsl2")]
    public async Task TestDsl2()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01").GroupBy(o => o.CreateTime!.Value.ToString("yyyy/MM/dd"));
        await grouped.Select(g => new Topic { Stars = (int)g.Count(x => x.Id), Title = g.Key }).ToListAsync(Ct);
        listen.AssertSql("SELECT COUNT(t.`id`) AS `stars`,DATE_FORMAT(t.`create_time`,'%Y/%m/%d') AS `title` FROM `t_topic` t WHERE DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? GROUP BY DATE_FORMAT(t.`create_time`,'%Y/%m/%d')");
        listen.AssertParams("2023/01/01(String)");
        await grouped.Select(g => new Topic { Stars = (int)g.Count(x => x.Id), Title = g.Column(x => x.CreateTime!.Value.ToString("yyyy/MM/dd")), Id = g.Key }).ToListAsync(Ct);
        listen.AssertSql("SELECT COUNT(t.`id`) AS `stars`,DATE_FORMAT(t.`create_time`,'%Y/%m/%d') AS `title`,DATE_FORMAT(t.`create_time`,'%Y/%m/%d') AS `id` FROM `t_topic` t WHERE DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? GROUP BY DATE_FORMAT(t.`create_time`,'%Y/%m/%d')");
        listen.AssertParams("2023/01/01(String)");
    }

    // Java 的 toStr()：C# 的 ToString()；setSQL：Sql.Raw。Java 里三段写法相同
    [Fact, Upstream("QueryTest9.java#testDsl3")]
    public async Task TestDsl3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01")
            .Select(o => new Topic { Title = (o.Stars ?? 0).ToString(), Alias = Sql.Raw<string>("IFNULL({0},'')", o.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT CAST(IFNULL(t.`stars`,?) AS CHAR) AS `title`,IFNULL(t.`id`,'') AS `alias` FROM `t_topic` t WHERE DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ?");
        listen.AssertParams("0(Integer),2023/01/01(String)");
    }

    // Java 的 isEmpty / isBlank 及取反：string.IsNullOrEmpty / IsNullOrWhiteSpace；isEmpty(false) 这类不生效的条件不写
    [Fact, Upstream("QueryTest9.java#testDsl4")]
    public async Task TestDsl4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => string.IsNullOrEmpty(o.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`id` IS NULL OR `id` = '')");
        listen.AssertParams("");
        await Db.Query<Topic>().Where(o => string.IsNullOrWhiteSpace(o.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`id` IS NULL OR `id` = '' OR LTRIM(`id`) = '')");
        await Db.Query<Topic>().Where(o => !string.IsNullOrEmpty(o.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`id` IS NOT NULL AND `id` <> '')");
        await Db.Query<Topic>().Where(o => !string.IsNullOrWhiteSpace(o.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`id` IS NOT NULL AND `id` <> '' AND LTRIM(`id`) <> '')");
    }

    // Java 的 nullOrDefault：C# 的 ??；expression().now()：Sql.Now()；格式化字符串与 NOW() 比较用 string.Compare(.., Sql.Raw("NOW()"))
    [Fact, Upstream("QueryTest9.java#testDsl5")]
    public async Task TestDsl5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.CreateTime <= o.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `create_time` <= `create_time`");
        listen.AssertParams("");
        await Db.Query<Topic>().Where(o => string.Compare(o.CreateTime!.Value.ToString("yyyy/MM/dd"), Sql.Raw<string>("NOW()")) <= 0).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE DATE_FORMAT(`create_time`,'%Y/%m/%d') <= NOW()");
        await Db.Query<Topic>().Where(o => o.CreateTime <= Sql.Now()).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE  `create_time` <= NOW()");
        await Db.Query<Topic>().Where(o => o.CreateTime <= (o.CreateTime ?? new DateTime(2021, 1, 1, 1, 1, 0))).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE  `create_time` <= IFNULL(`create_time`,?)");
        listen.AssertParams("2021-01-01T01:01(LocalDateTime)");
        await Db.Query<Topic>().Where(o => o.CreateTime <= (o.CreateTime ?? new DateTime(2022, 1, 1, 1, 1, 0))
                                           && !string.IsNullOrWhiteSpace(o.Id)
                                           && (o.Id ?? "") == (o.Title ?? o.Id)
                                           && string.IsNullOrEmpty(o.Title)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE  `create_time` <= IFNULL(`create_time`,?) AND (`id` IS NOT NULL AND `id` <> '' AND LTRIM(`id`) <> '') AND IFNULL(`id`,?) = IFNULL(`title`,`id`) AND (`title` IS NULL OR `title` = '')");
        listen.AssertParams("2022-01-01T01:01(LocalDateTime),(String)");
        await Db.Query<Topic>().Where(NullOrDefaults).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE  `create_time` <= IFNULL(`create_time`,?) AND IFNULL(`id`,?) IS NULL AND IFNULL(`id`,?) = IFNULL(`title`,`id`) AND (`title` IS NULL OR `title` = '')");
        listen.AssertParams("2022-01-01T01:01(LocalDateTime),1(String),2(String)");
    }

    private static readonly Expression<Func<Topic, bool>> NullOrDefaults = o =>
        o.CreateTime <= (o.CreateTime ?? new DateTime(2022, 1, 1, 1, 1, 0))
        && (o.Id ?? "1") == null
        && (o.Id ?? "2") == (o.Title ?? o.Id)
        && string.IsNullOrEmpty(o.Title);

    [Fact, Upstream("QueryTest9.java#dslTest6", Deviation = "Java 的 selectExpression(key1()) 不写别名；C# 分组键投影到属性时写 AS（同 QueryTest2）")]
    public async Task DslTest6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(NullOrDefaults).Select(o => o.Title).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` FROM `t_topic` t WHERE  t.`create_time` <= IFNULL(t.`create_time`,?) AND IFNULL(t.`id`,?) IS NULL AND IFNULL(t.`id`,?) = IFNULL(t.`title`,t.`id`) AND (t.`title` IS NULL OR t.`title` = '')");
        listen.AssertParams("2022-01-01T01:01(LocalDateTime),1(String),2(String)");

        await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01").GroupBy(o => o.Title)
            .Select(g => new Topic { Title = g.Key, Stars = (int)g.Count(x => x.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` AS `title`,COUNT(t.`id`) AS `stars` FROM `t_topic` t WHERE DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? GROUP BY t.`title`");
        listen.AssertParams("2023/01/01(String)");

        await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01"
                                           && (o.Stars != 1 || o.CreateTime <= new DateTime(2024, 1, 1, 1, 1, 0) || !o.Title!.Contains("abc"))
                                           && o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01"
                                           && (o.Id ?? "yyyy/MM/dd") == "xxx"
                                           && Sql.Raw<bool>("{0} != {1}", o.Stars, o.CreateTime)
                                           && (o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01" || (o.Id ?? "yyyy/MM/dd") == "xxx" || Sql.Raw<bool>("{0} != {1}", o.Stars, o.CreateTime))
                                           && o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/02"
                                           && (o.Id ?? "yyyy/MM/dd2") == "xxx1")
            .Select(o => new Topic { Stars = o.Stars, CreateTime = o.CreateTime, Title = o.Id, Id = o.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`create_time`,t.`id` AS `title`,t.`id` FROM `t_topic` t WHERE DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? AND (t.`stars` <> ? OR t.`create_time` <= ? OR t.`title` NOT LIKE ?) AND DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? AND IFNULL(t.`id`,?) = ? AND t.`stars` != t.`create_time` AND (DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? OR IFNULL(t.`id`,?) = ? OR t.`stars` != t.`create_time`) AND DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ? AND IFNULL(t.`id`,?) = ?");
        listen.AssertParams("2023/01/01(String),1(Integer),2024-01-01T01:01(LocalDateTime),%abc%(String),2023/01/01(String),yyyy/MM/dd(String),xxx(String),2023/01/01(String),yyyy/MM/dd(String),xxx(String),2023/01/02(String),yyyy/MM/dd2(String),xxx1(String)");
    }

    // Java 的 MapProxy：C# 投影成匿名类型后 ToRowsAsync
    [Fact, Upstream("QueryTest9.java#dslTest7", Deviation = "Java 空的 MapProxy 渲染 SELECT *，C# 不投影时按实体的列读成字典；map.put(\"id\", id) 在 C# 匿名类型里成员名与列名相同，不写 AS")]
    public async Task DslTest7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01")
            .Select(o => new { o.Stars, o.CreateTime, abc = o.Id, o.Id }).ToRowsAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`create_time`,t.`id` AS `abc`,t.`id` FROM `t_topic` t WHERE DATE_FORMAT(t.`create_time`,'%Y/%m/%d') = ?");
        listen.AssertParams("2023/01/01(String)");
        await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01").ToRowsAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE DATE_FORMAT(`create_time`,'%Y/%m/%d') = ?");
        listen.AssertParams("2023/01/01(String)");

        var idQuery = Db.Query<BlogEntity>().Where(o => o.Id == "123").Select(o => o.Id);
        await Db.Query<Topic>().Where(o => Sql.In(o.Id, idQuery))
            .Select(o => new { o.Stars, o.CreateTime, abc = o.Id, o.Id }).ToRowsAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`create_time`,t.`id` AS `abc`,t.`id` FROM `t_topic` t WHERE t.`id` IN (SELECT t1.`id` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ?)");
        listen.AssertParams("false(Boolean),123(String)");
        await Db.Query<Topic>().LeftJoin<BlogEntity>((a, b) => a.Id == b.Id).Where((o, _) => Sql.In(o.Id, idQuery))
            .Select((a, _) => new { a.Stars, a.CreateTime, abc = a.Id, id = a.Id, efg = a.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss") }).ToRowsAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`create_time`,t.`id` AS `abc`,t.`id`,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') AS `efg` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` IN (SELECT t2.`id` FROM `t_blog` t2 WHERE t2.`deleted` = ? AND t2.`id` = ?)");
        listen.AssertParams("false(Boolean),false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest9.java#havingQueryTest1")]
    public async Task HavingQueryTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(o => o.Id)
            .Having(g => g.Count() != 1L && g.Sum(x => x.Star) >= 10)
            .Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(x => x.Id), Title = g.Max(x => x.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `star`,MAX(t.`id`) AS `title` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY t.`id` HAVING COUNT(*) <> ? AND SUM(t.`star`) >= ?");
        listen.AssertParams("false(Boolean),123(String),1(Long),10(Integer)");
    }

    // Java 的 likeMatchLeft / likeMatchRight：StartsWith / EndsWith；带 false 的不生效条件不写
    [Fact, Upstream("QueryTest9.java#testQuery5")]
    public async Task TestQuery5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "1"
                                                && o.Id == o.CreateTime!.Value.ToString("yyyy-MM-dd")
                                                && o.CreateTime!.Value.ToString("yyyy-MM-dd") == "2023-01-02"
                                                && (o.Title ?? "unknown").Contains("123")
                                                && (o.Title ?? "unknown").StartsWith("123")
                                                && (o.Title ?? "unknown").EndsWith("123")
                                                && (o.Star ?? 1) >= 101
                                                && (o.Star ?? 4) > 102
                                                && (o.Star ?? 3) <= 103
                                                && (o.Star ?? 2) < 104
                                                && (o.Star ?? 1) == 105
                                                && (o.Title ?? "unknown") == o.Content
                                                && !string.IsNullOrWhiteSpace(o.Content))
            .GroupBy(o => o.Id)
            .Having(g => g.Count(x => x.Id) != 1L && g.Sum(x => x.Star) >= 10)
            .Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(), Title = g.Max(x => x.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `id`,COUNT(*) AS `star`,MAX(t.`id`) AS `title` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? AND  t.`id` = DATE_FORMAT(t.`create_time`,'%Y-%m-%d') AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') = ? AND IFNULL(t.`title`,?) LIKE ? AND IFNULL(t.`title`,?) LIKE ? AND IFNULL(t.`title`,?) LIKE ? AND IFNULL(t.`star`,?) >= ? AND IFNULL(t.`star`,?) > ? AND IFNULL(t.`star`,?) <= ? AND IFNULL(t.`star`,?) < ? AND IFNULL(t.`star`,?) = ? AND IFNULL(t.`title`,?) = t.`content` AND (t.`content` IS NOT NULL AND t.`content` <> '' AND LTRIM(t.`content`) <> '') GROUP BY t.`id` HAVING COUNT(t.`id`) <> ? AND SUM(t.`star`) >= ?");
        listen.AssertParams("false(Boolean),1(String),2023-01-02(String),unknown(String),%123%(String),unknown(String),123%(String),unknown(String),%123(String),1(Integer),101(Integer),4(Integer),102(Integer),3(Integer),103(Integer),2(Integer),104(Integer),1(Integer),105(Integer),unknown(String),1(Long),10(Integer)");
    }

    [Fact, Upstream("QueryTest9.java#testQuery6", Deviation = "Java 两处引用同一个子查询对象，别名都是 t1；C# 每处子查询各自编号（标量子查询多保留一个别名，IN 子查询是 t3）。分组键投影到属性时写 AS")]
    public async Task TestQuery6()
    {
        using var listen = SqlCapture.Listen();
        var sss = Db.Query<BlogEntity>().Where(o => o.Id == "sss").Select(o => o.Id);
        await Db.Query<BlogEntity>().Where(o => o.Id == Sql.SubQuery(sss) && Sql.In(o.Title ?? "unknown", sss))
            .GroupBy(o => o.Id)
            .Having(g => g.Count(x => x.Id) != 1L && g.Sum(x => x.Star) >= 10)
            .Select(g => new BlogEntity { Id = g.Key, Star = (int)g.Count(x => x.Id), Title = g.Max(x => x.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `star`,MAX(t.`id`) AS `title` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = (SELECT t1.`id` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ?) AND IFNULL(t.`title`,?) IN (SELECT t3.`id` FROM `t_blog` t3 WHERE t3.`deleted` = ? AND t3.`id` = ?) GROUP BY t.`id` HAVING COUNT(t.`id`) <> ? AND SUM(t.`star`) >= ?");
        listen.AssertParams("false(Boolean),false(Boolean),sss(String),unknown(String),false(Boolean),sss(String),1(Long),10(Integer)");
    }

    [Fact, Upstream("QueryTest9.java#testQuery7")]
    public async Task TestQuery7()
    {
        using var listen = SqlCapture.Listen();
        var ids = new List<string>();
        await Db.Query<BlogEntity>().Where(o => ids.Contains(o.Id!) && !ids.Contains(o.Id!))
            .SelectExcept<BlogEntity>(o => new { o.Title, o.Top }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top` FROM `t_blog` t WHERE t.`deleted` = ? AND 1 = 2 AND 1 = 1");
        listen.AssertParams("false(Boolean)");
    }
}
