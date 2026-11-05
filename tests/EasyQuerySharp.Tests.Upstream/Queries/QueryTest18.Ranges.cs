using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>QueryTest18 的区间条件（Java 的 rangeClosed / rangeOpen 等）：C# 写 <c>x.RangeClosed(lower, upper)</c>。</summary>
public sealed partial class QueryTest18
{
    private const string BlogSelect = "SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ?";
    private static readonly DateTime? Time1 = new DateTime(2021, 1, 1, 1, 1, 0);
    private static readonly DateTime? Time2 = new DateTime(2022, 1, 1, 1, 1, 0);
    private static readonly DateTime? Def = new DateTime(2022, 5, 1, 1, 1, 0);

    private async Task AssertRange(System.Linq.Expressions.Expression<Func<BlogEntity, bool>> predicate, string where, string parameters, bool valueFilter = false)
    {
        using var listen = SqlCapture.Listen();
        var query = Db.Query<BlogEntity>();
        if (valueFilter) query = query.UseValueFilter(ValueFilter.NotNullOrBlank);
        await query.Where(predicate).ToListAsync(Ct);
        listen.AssertSql(BlogSelect + where);
        listen.AssertParams("false(Boolean)" + parameters);
    }

    // 区间右端是函数时 AND 后多一个空格（与 Java 一致）
    [Fact, Upstream("QueryTest18.java#rangeTest1")]
    public async Task RangeTest1()
    {
        await AssertRange(b => (b.CreateTime ?? Time1).RangeClosed(Time1, Time2),
            " AND IFNULL(`create_time`,?) BETWEEN ? AND ?", ",2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)");
        await AssertRange(b => b.CreateTime.RangeClosed(Time1, Time2),
            " AND (`create_time` >= ? AND `create_time` <= ?)", ",2021-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)");
        await AssertRange(b => b.CreateTime.RangeClosed(b.UpdateTime, b.UpdateTime ?? new DateTime(2023, 1, 1, 1, 1, 0)),
            " AND (`create_time` >= `update_time` AND  `create_time` <= IFNULL(`update_time`,?))", ",2023-01-01T01:01(LocalDateTime)");
        await AssertRange(b => Time1.RangeClosed(b.CreateTime, b.UpdateTime) && Time1.RangeClosed(b.CreateTime, b.UpdateTime ?? Time2),
            " AND (? >= `create_time` AND ? <= `update_time`) AND (? >= `create_time` AND ? <= IFNULL(`update_time`,?))",
            ",2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)");
    }

    [Fact, Upstream("QueryTest18.java#rangeTest1_1"), Upstream("QueryTest18.java#rangeTest1_2"), Upstream("QueryTest18.java#rangeTest1_3")]
    public async Task RangeTest1_1()
    {
        await AssertRange(b => b.CreateTime.RangeClosed(null, Time2), " AND `create_time` <= ?", ",2022-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeClosed(Time1, null), " AND `create_time` >= ?", ",2021-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeClosed(null, null), "", "", valueFilter: true);
    }

    [Fact, Upstream("QueryTest18.java#rangeTest1_4"), Upstream("QueryTest18.java#rangeTest1_5"), Upstream("QueryTest18.java#rangeTest1_6")]
    public async Task RangeTest1_4()
    {
        await AssertRange(b => (b.CreateTime ?? Def).RangeClosed(null, Time2), " AND IFNULL(`create_time`,?) <= ?",
            ",2022-05-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => (b.CreateTime ?? Def).RangeClosed(Time1, null), " AND IFNULL(`create_time`,?) >= ?",
            ",2022-05-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => (b.CreateTime ?? Def).RangeClosed(null, null), "", "", valueFilter: true);
    }

    [Fact, Upstream("QueryTest18.java#rangeTest2")]
    public Task RangeTest2() =>
        AssertRange(b => Time1.RangeClosed(b.CreateTime, b.UpdateTime) || Time1.RangeClosed(b.CreateTime, b.UpdateTime ?? Time2),
            " AND ((? >= `create_time` AND ? <= `update_time`) OR (? >= `create_time` AND ? <= IFNULL(`update_time`,?)))",
            ",2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2021-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)");

    [Fact, Upstream("QueryTest18.java#rangeTest3")]
    public async Task RangeTest3()
    {
        await AssertRange(b => (b.CreateTime ?? Time1).RangeClosed(null, Time2), " AND IFNULL(`create_time`,?) <= ?",
            ",2021-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => (b.CreateTime ?? Time1).RangeOpenClosed(null, Time2), " AND IFNULL(`create_time`,?) <= ?",
            ",2021-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeClosed(Time1, null), " AND `create_time` >= ?", ",2021-01-01T01:01(LocalDateTime)", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeClosed(null, null), "", "", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeOpen(null, null), "", "", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeOpenClosed(null, null), "", "", valueFilter: true);
        await AssertRange(b => b.CreateTime.RangeClosedOpen(null, null), "", "", valueFilter: true);
    }

    // Java 的 plus(n, TimeUnitEnum.HOURS)：C# 的 AddHours(n)
    [Fact, Upstream("QueryTest18.java#test125")]
    public Task Test125() =>
        AssertRange(b => b.CreateTime!.Value.AddHours(b.Star!.Value) > new DateTime(2024, 1, 1),
            " AND date_add(`create_time`, interval (`star`) hour) > ?", ",2024-01-01T00:00(LocalDateTime)");
}
