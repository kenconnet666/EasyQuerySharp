namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 只跳过行数（<c>Skip(n)</c> 不带 <c>Take</c>）：OFFSET 前必须有 LIMIT 的方言写一个"不限条数"的 LIMIT，其余只写 OFFSET。
/// Java 的 limit 总带条数，没有对应用例。
/// </summary>
public sealed class SkipWithoutTakeTest
{
    [Theory]
    [InlineData("mysql", " ORDER BY `id` ASC LIMIT 18446744073709551615 OFFSET 10")]
    [InlineData("sqlite", " ORDER BY \"id\" ASC LIMIT -1 OFFSET 10")]
    [InlineData("hana", " ORDER BY \"id\" ASC LIMIT 2147483647 OFFSET 10")]
    [InlineData("pgsql", " ORDER BY \"id\" ASC OFFSET 10")]
    [InlineData("duckdb", " ORDER BY \"id\" ASC OFFSET 10")]
    public void WritesAnUnboundedLimitWhereOffsetNeedsOne(string dialect, string expectedTail) =>
        Assert.EndsWith(expectedTail, SqlOnly.Db(dialect).Query<Topic>().OrderBy(t => t.Id).Skip(10).ToSql());
}
