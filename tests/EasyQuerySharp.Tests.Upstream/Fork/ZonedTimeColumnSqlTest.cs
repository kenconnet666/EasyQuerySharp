namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：types/ZonedTimeColumnSqlTest.java（带时区的时间列与普通日期列一样比较、加减、格式化、单独选出与求时长）。</summary>
public sealed class ZonedTimeColumnSqlTest
{
    private static EasyDb MySql => SqlOnly.Db("mysql");

    [Fact, Upstream("fork/types/ZonedTimeColumnSqlTest.java#proxyColumnsAreDateTimeColumns",
        NotApplicable = "测的是 Java 代理列的类型（SQLInstantTypeColumn 等）；C# 的 DateTimeOffset 属性直接用日期的成员和扩展方法，见本类其余用例")]
    public void ProxyColumnsAreDateTimeColumns()
    {
    }

    // 列与函数比较时上游在 AND 后面多输出一个空格，与 Java 逐字一致
    [Fact, Upstream("fork/types/ZonedTimeColumnSqlTest.java#compareAndDateArithmetic")]
    public void CompareAndDateArithmetic()
    {
        var since = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("SELECT `id`,`kind`,`created_at`,`completed_at`,`due_at`,`seen_at` FROM `t_fork_event`"
                     + " WHERE `created_at` >= ? AND  `completed_at` > date_add(`due_at`, interval (?) day) AND DATE_FORMAT(`seen_at`,'%Y-%m-%d') = ?",
            MySql.Query<ForkEvent>().Where(e => e.CreatedAt >= since && e.CompletedAt > e.DueAt!.Value.AddDays(1) && e.SeenAt!.Value.ToString("yyyy-MM-dd") == "2026-10-27").ToSql());
    }

    [Fact, Upstream("fork/types/ZonedTimeColumnSqlTest.java#selectSingleZonedColumn")]
    public void SelectSingleZonedColumn()
    {
        Assert.Equal("SELECT t.`created_at` FROM `t_fork_event` t ORDER BY t.`created_at` ASC",
            MySql.Query<ForkEvent>().OrderBy(e => e.CreatedAt).Select(e => e.CreatedAt).ToSql());
        Assert.Equal("SELECT t.`due_at` FROM `t_fork_event` t", MySql.Query<ForkEvent>().Select(e => e.DueAt).ToSql());
        Assert.Equal("SELECT t.`seen_at` FROM `t_fork_event` t", MySql.Query<ForkEvent>().Select(e => e.SeenAt).ToSql());
    }

    [Fact, Upstream("fork/types/ZonedTimeColumnSqlTest.java#durationAggregateInGroupSelect")]
    public void DurationAggregateInGroupSelect()
    {
        var sql = MySql.Query<ForkEvent>().GroupBy(e => e.Kind)
            .Select(g => new Draft<string?, double?>(g.Key, g.Average(e => (e.CompletedAt!.Value - e.CreatedAt!.Value).TotalSeconds))).ToSql();
        Assert.StartsWith("SELECT t.`kind` AS `value1`,AVG(", sql);
        Assert.Contains("t.`created_at`", sql);
        Assert.Contains("t.`completed_at`", sql);
        Assert.EndsWith("FROM `t_fork_event` t GROUP BY t.`kind`", sql);
    }
}
