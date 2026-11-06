using System.Linq.Expressions;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：SubJoinTest.java（一对多集合上的聚合经过多对一导航：子查询里 LEFT JOIN 导航表）。
/// Java 的 books().sum(b -> b.author().age()) 对应 <c>Books.Sum(b => b.Author.Age)</c>；distinct() 对应 <c>Distinct()</c>；
/// sumBigDecimal 只改变 Java 的结果类型，C# 把元素转成 decimal。每个用例的 "2" 版本是 selectColumn（单列投影，不带别名）。
/// </summary>
public sealed class SubJoinTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private Query<SubJoinUser> User123 => Db.Query<SubJoinUser>().Where(s => s.Id == "123");

    private async Task AssertSql<TResult>(Expression<Func<SubJoinUser, TResult>> value, string draftSql, string columnSql)
    {
        using var listen = SqlCapture.Listen();
        // 同一个表达式包成 Draft 投影（带别名 value1）
        var draft = Expression.Lambda<Func<SubJoinUser, Draft<TResult>>>(
            Expression.New(typeof(Draft<TResult>).GetConstructor([typeof(TResult)])!, value.Body), value.Parameters);
        await User123.Select(draft).ToListAsync(Ct);
        listen.AssertSql(draftSql);
        listen.AssertParams("123(String)");
        await User123.Select(value).ToListAsync(Ct);
        listen.AssertSql(columnSql);
        listen.AssertParams("123(String)");
    }

    [Fact, Upstream("SubJoinTest.java#testSubJoinSum"), Upstream("SubJoinTest.java#testSubJoinSum2")]
    public Task TestSubJoinSum() => AssertSql(s => s.Books!.Sum(b => b.Author!.Age), "SELECT IFNULL((SELECT SUM(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT IFNULL((SELECT SUM(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinSumDistinct"), Upstream("SubJoinTest.java#testSubJoinSumDistinct2")]
    public Task TestSubJoinSumDistinct() => AssertSql(s => s.Books!.Distinct().Sum(b => b.Author!.Age), "SELECT IFNULL((SELECT SUM(DISTINCT t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT IFNULL((SELECT SUM(DISTINCT t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinSumBigDecimal"), Upstream("SubJoinTest.java#testSubJoinSumBigDecimal2")]
    public Task TestSubJoinSumBigDecimal() => AssertSql(s => s.Books!.Sum(b => (decimal?)b.Author!.Age), "SELECT IFNULL((SELECT SUM(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT IFNULL((SELECT SUM(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinSumBigDecimalDistinct"), Upstream("SubJoinTest.java#testSubJoinSumBigDecimalDistinct2")]
    public Task TestSubJoinSumBigDecimalDistinct() => AssertSql(s => s.Books!.Distinct().Sum(b => (decimal?)b.Author!.Age), "SELECT IFNULL((SELECT SUM(DISTINCT t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT IFNULL((SELECT SUM(DISTINCT t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinAvg"), Upstream("SubJoinTest.java#testSubJoinAvg2")]
    public Task TestSubJoinAvg() => AssertSql(s => s.Books!.Average(b => b.Author!.Age), "SELECT IFNULL((SELECT AVG(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT IFNULL((SELECT AVG(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinAvgDistinct"), Upstream("SubJoinTest.java#testSubJoinAvgDistinct2")]
    public Task TestSubJoinAvgDistinct() => AssertSql(s => s.Books!.Distinct().Average(b => b.Author!.Age), "SELECT IFNULL((SELECT AVG(DISTINCT t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT IFNULL((SELECT AVG(DISTINCT t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`),0) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinMax"), Upstream("SubJoinTest.java#testSubJoinMax2")]
    public Task TestSubJoinMax() => AssertSql(s => s.Books!.Max(b => b.Author!.Age), "SELECT (SELECT MAX(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT (SELECT MAX(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`) FROM `sub_join_user` t WHERE t.`id` = ?");

    [Fact, Upstream("SubJoinTest.java#testSubJoinMin"), Upstream("SubJoinTest.java#testSubJoinMin2")]
    public Task TestSubJoinMin() => AssertSql(s => s.Books!.Min(b => b.Author!.Age), "SELECT (SELECT MIN(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ?", "SELECT (SELECT MIN(t2.`age`) FROM `sub_join_book` t1 LEFT JOIN `sub_join_author` t2 ON t2.`id` = t1.`author_id` WHERE t1.`uid` = t.`id`) FROM `sub_join_user` t WHERE t.`id` = ?");

    // 排序里的 CASE WHEN 判断集合里是否存在；Java 的 flatElement().name().eq(..) 与 any(..) 同样渲染成 EXISTS
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement 在 C# 里写成 Any
    [Fact, Upstream("SubJoinTest.java#testSubJoinCaseWhen1"), Upstream("SubJoinTest.java#testSubJoinCaseWhen1_1"), Upstream("SubJoinTest.java#testSubJoinCaseWhen2")]
    public async Task TestSubJoinCaseWhen()
    {
        using var listen = SqlCapture.Listen();
        await User123.OrderBy(s => s.Books!.Any(x => x.Name == "123") ? 1 : 2)
            .Select(s => new Draft<int?>(s.Books!.Sum(b => b.Author!.Age))).ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL((SELECT SUM(t3.`age`) FROM `sub_join_book` t2 LEFT JOIN `sub_join_author` t3 ON t3.`id` = t2.`author_id` WHERE t2.`uid` = t.`id`),0) AS `value1` FROM `sub_join_user` t WHERE t.`id` = ? ORDER BY (CASE WHEN EXISTS(SELECT 1 FROM `sub_join_book` t1 WHERE t1.`uid` = t.`id` AND t1.`name` = ? LIMIT 1) THEN ? ELSE ? END) ASC");
        listen.AssertParams("123(String),123(String),1(Integer),2(Integer)");
    }
}
