namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/TreeCTETest.java（递归 CTE 查子树，自动映射到 DTO 后拼成树）。改在 SQLite 上跑（见 <see cref="H2Fixture"/>），
/// 与 pgsql/QueryTest19 的 tree8 是同一个查询。
/// </summary>
public sealed class TreeCTETest(H2Fixture fixture) : H2Test(fixture)
{
    [Fact, Upstream("h2/TreeCTETest.java#tree8", Deviation = "SQLite 的复合查询分支不能加括号，递归 CTE 也不写列名清单；Java 的 H2 写成 as_tree_cte(cte_deep,id,parent_id,name) AS ( (…)  UNION ALL  (…) )")]
    public async Task Tree8()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte().Select<MyCategoryVO2>().ToTreeAsync(Ct);
        Assert.Equal(2, Assert.Single(tree).Children!.Count);
        listen.AssertSql("WITH RECURSIVE as_tree_cte AS ( SELECT 0 AS cte_deep,t1.id,t1.parent_id,t1.name FROM category t1 WHERE t1.id = ?  UNION ALL  SELECT t2.cte_deep + 1 AS cte_deep,t3.id,t3.parent_id,t3.name FROM as_tree_cte t2 INNER JOIN category t3 ON t3.parent_id = t2.id ) SELECT t.id,t.parent_id,t.name,t.cte_deep FROM as_tree_cte t");
        listen.AssertParams("1(String)");
    }
}
