namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：DirectRelationTest.java。直达导航沿五张表的链逐级 JOIN（复合键的 ON 带括号）。
/// Java 用例执行前会建表造数；这里只比对 SQL，不连库。
/// </summary>
public sealed class DirectRelationTest
{
    private const string Chain = " FROM `direct1` t LEFT JOIN `direct2` t1 ON t1.`c7` = t.`c1` LEFT JOIN `direct3` t2 ON (t2.`c13` = t1.`c8` AND t2.`c14` = t1.`c9`) LEFT JOIN `direct4` t3 ON t3.`c20` = t2.`c15` LEFT JOIN `direct5` t4 ON (t4.`c22` = t3.`c16` AND t4.`c21` = t3.`c18`)";

    [Fact, Upstream("DirectRelationTest.java#test3")]
    public void Test3() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mysql").Query<Direct1>().Where(d => d.Direct4!.C16 == "123" && d.Direct5!.C21 == "1234").ToStatement(),
            "SELECT t.`c1`,t.`c2`,t.`c3`,t.`c4`,t.`c5`" + Chain + " WHERE t3.`c16` = ? AND t4.`c21` = ?", "123(String),1234(String)");

    // 条件里先用到 Direct5（建出整条链），投影里的 Direct4 复用已有的 JOIN
    [Fact, Upstream("DirectRelationTest.java#testJoin1")]
    public void TestJoin1() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mysql").Query<Direct1>().Where(d => d.Direct5!.C22 == "123").Select(d => new Draft<string?>(d.Direct4!.C18)).ToStatement(),
            "SELECT t3.`c18` AS `value1`" + Chain + " WHERE t4.`c22` = ?", "123(String)");
}
