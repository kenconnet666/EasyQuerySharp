using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：DateTimeFunctionsTest.java（MySQL 实库）。日期函数直接写 BCL 成员：
/// <c>x.ToString("yyyy-MM")</c>、<c>x.Year</c>、<c>x.DayOfYear</c>、<c>(end - start).TotalDays</c>；
/// ISO 星期与数据库当前时间用 <c>Sql.IsoDayOfWeek</c> / <c>Sql.Now()</c>。
/// </summary>
public sealed class DateTimeFunctionsTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("DateTimeFunctionsTest.java#formatMySQL")]
    public async Task FormatMySQL()
    {
        const string format1 = "yyyy年MM-01 HH:mm分ss秒", format2 = "yyyy-MM-dd HH:mm:ss", format3 = "yyyy/MM-/01 HH时mm分ss秒";
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>()
            .Select(b => new Draft<DateTime?, string, string, string>(b.CreateTime, b.CreateTime!.Value.ToString(format1), b.CreateTime!.Value.ToString(format2), b.CreateTime!.Value.ToString(format3)))
            .ToListAsync(Ct);
        Assert.NotEmpty(list);
        foreach (var item in list)
        {
            // C# 格式串里的 / 是日期分隔符占位，按固定文化格式化才与 SQL 一致
            Assert.Equal(item.Value1!.Value.ToString(format1, System.Globalization.CultureInfo.InvariantCulture), item.Value2);
            Assert.Equal(item.Value1!.Value.ToString(format2, System.Globalization.CultureInfo.InvariantCulture), item.Value3);
            Assert.Equal(item.Value1!.Value.ToString(format3, System.Globalization.CultureInfo.InvariantCulture), item.Value4);
        }
        listen.AssertSql("SELECT t.`create_time` AS `value1`,DATE_FORMAT(t.`create_time`,'%Y年%m-01 %H:%i分%s秒') AS `value2`,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') AS `value3`,DATE_FORMAT(t.`create_time`,'%Y/%m-/01 %H时%i分%s秒') AS `value4` FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("DateTimeFunctionsTest.java#dayOfYearMySQL")]
    public async Task DayOfYearMySQL()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(b => new Draft<DateTime?, int>(b.CreateTime, b.CreateTime!.Value.DayOfYear)).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, item => Assert.Equal(item.Value1!.Value.DayOfYear, item.Value2));
        listen.AssertSql("SELECT t.`create_time` AS `value1`,DAYOFYEAR(t.`create_time`) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    // Java 的 dayOfWeekSunDayIsLastDay：周一 1 … 周日 7
    [Fact, Upstream("DateTimeFunctionsTest.java#dayOfWeekSunDayIsEndDayMySQL")]
    public async Task DayOfWeekSunDayIsEndDayMySQL()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(b => new Draft<DateTime?, int>(b.CreateTime, b.CreateTime!.Value.IsoDayOfWeek())).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, item => Assert.Equal(item.Value1!.Value.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)item.Value1!.Value.DayOfWeek, item.Value2));
        listen.AssertSql("SELECT t.`create_time` AS `value1`,(WEEKDAY(t.`create_time`)+1) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    // Java 的 dateTimeProp(DateTimeUnitEnum.Xxx) 与 year() 等写法生成同一条 SQL
    [Fact, Upstream("DateTimeFunctionsTest.java#dateTimePropMySQL"), Upstream("DateTimeFunctionsTest.java#dateTimeProp2MySQL")]
    public async Task DateTimePropMySQL()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>()
            .Select(b => new Draft<DateTime?, int, int, int, int, int, int>(b.CreateTime, b.CreateTime!.Value.Year, b.CreateTime!.Value.Month, b.CreateTime!.Value.Day,
                b.CreateTime!.Value.Hour, b.CreateTime!.Value.Minute, b.CreateTime!.Value.Second))
            .ToListAsync(Ct);
        Assert.NotEmpty(list);
        foreach (var item in list)
        {
            var v = item.Value1!.Value;
            Assert.Equal((v.Year, v.Month, v.Day, v.Hour, v.Minute, v.Second), (item.Value2, item.Value3, item.Value4, item.Value5, item.Value6, item.Value7));
        }
        listen.AssertSql("SELECT t.`create_time` AS `value1`,YEAR(t.`create_time`) AS `value2`,MONTH(t.`create_time`) AS `value3`,DAYOFMONTH(t.`create_time`) AS `value4`,HOUR(t.`create_time`) AS `value5`,MINUTE(t.`create_time`) AS `value6`,SECOND(t.`create_time`) AS `value7` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    // Java 的 a.duration(b).toDays()：C# 的 (b - a).TotalDays，整数相差（截断）
    [Fact, Upstream("DateTimeFunctionsTest.java#durationMySQL")]
    public async Task DurationMySQL()
    {
        var time2025 = new DateTime(2025, 1, 1);
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>()
            .Select(b => new Draft<DateTime?, long, long, long, long, long>(b.CreateTime, (long)(time2025 - b.CreateTime!.Value).TotalDays, (long)(time2025 - b.CreateTime!.Value).TotalHours,
                (long)(time2025 - b.CreateTime!.Value).TotalMinutes, (long)(time2025 - b.CreateTime!.Value).TotalSeconds, (time2025 - b.CreateTime!.Value).Days))
            .ToListAsync(Ct);
        Assert.NotEmpty(list);
        foreach (var item in list)
        {
            var between = time2025 - item.Value1!.Value;
            Assert.Equal(((long)between.TotalDays, (long)between.TotalHours, (long)between.TotalMinutes, (long)between.TotalSeconds, (long)between.Days),
                (item.Value2, item.Value3, item.Value4, item.Value5, item.Value6));
        }
        listen.AssertSql("SELECT t.`create_time` AS `value1`,timestampdiff(DAY, t.`create_time`, ?) AS `value2`,timestampdiff(HOUR, t.`create_time`, ?) AS `value3`,timestampdiff(MINUTE, t.`create_time`, ?) AS `value4`,timestampdiff(SECOND, t.`create_time`, ?) AS `value5`,timestampdiff(DAY, t.`create_time`, ?) AS `value6` FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime),false(Boolean)");
    }

    [Fact, Upstream("DateTimeFunctionsTest.java#booleanMySQL")]
    public async Task BooleanMySQL()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(b => new Draft<bool?, bool?>(b.Deleted, !b.Deleted)).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, item => Assert.Equal(item.Value1, !item.Value2));
        listen.AssertSql("SELECT t.`deleted` AS `value1`,(NOT (t.`deleted`)) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    // Java 的 expression().now() / utcNow()：数据库的当前时间（DateTime.Now 是客户端的值，作为参数）
    [Fact, Upstream("DateTimeFunctionsTest.java#testConstant")]
    public async Task TestConstant()
    {
        using var listen = SqlCapture.Listen();
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(b => new Draft<DateTime, DateTime>(Sql.Now(), Sql.UtcNow())).ToListAsync(Ct));
        listen.AssertSql("SELECT NOW() AS `value1`,UTC_TIMESTAMP() AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }
}
