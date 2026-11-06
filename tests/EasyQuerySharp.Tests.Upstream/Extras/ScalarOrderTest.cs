namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 投影成一个值的查询作来源之后（合并、分组键），lambda 的参数本身就是那一列：<c>OrderBy(n =&gt; n)</c>、<c>Where(k =&gt; k &gt; 1)</c>。C# 独有。
/// </summary>
public sealed class ScalarOrderTest
{
    [Theory]
    [InlineData("mysql", "SELECT t2.`title` FROM ( (SELECT t.`title` FROM `t_topic` t)  UNION ALL  (SELECT t1.`title` FROM `t_topic` t1) ) t2 ORDER BY t2.`title` ASC")]
    [InlineData("oracle", "SELECT t2.\"title\" FROM ( (SELECT t.\"title\" FROM \"t_topic\" t)  UNION ALL  (SELECT t1.\"title\" FROM \"t_topic\" t1) ) t2 ORDER BY t2.\"title\" ASC")]
    public void OrdersAUnionOfValuesByTheValue(string dialect, string expected)
    {
        var db = SqlOnly.Db(dialect);
        Assert.Equal(expected, db.Query<Topic>().Select(t => t.Title).UnionAll(db.Query<Topic>().Select(t => t.Title)).OrderBy(n => n).ToSql());
    }

    [Theory]
    [InlineData("mysql", "SELECT t1.`stars` FROM (SELECT t.`stars` FROM `t_topic` t GROUP BY t.`stars`) t1 WHERE t1.`stars` > ? ORDER BY t1.`stars` DESC")]
    [InlineData("oracle", "SELECT t1.\"stars\" FROM (SELECT t.\"stars\" FROM \"t_topic\" t GROUP BY t.\"stars\") t1 WHERE t1.\"stars\" > ? ORDER BY t1.\"stars\" DESC")]
    [InlineData("dameng", "SELECT t1.\"stars\" FROM (SELECT t.\"stars\" FROM \"t_topic\" t GROUP BY t.\"stars\") t1 WHERE t1.\"stars\" > ? ORDER BY t1.\"stars\" DESC")]
    public void FiltersAndOrdersGroupKeysByTheKey(string dialect, string expected) =>
        Assert.Equal(expected, SqlOnly.Db(dialect).Query<Topic>().GroupBy(t => t.Stars).Keys().Where(k => k > 1).OrderByDescending(k => k).ToSql());

    // Oracle 的分页包在外层（ROWNUM），排序留在里面
    [Fact]
    public void PagesOrderedGroupKeysOnOracle() =>
        Assert.Equal(
            "SELECT rt.* FROM (SELECT t1.\"stars\" FROM (SELECT t.\"stars\" FROM \"t_topic\" t GROUP BY t.\"stars\") t1 ORDER BY t1.\"stars\" ASC) rt WHERE ROWNUM < 3",
            SqlOnly.Db("oracle").Query<Topic>().GroupBy(t => t.Stars).Keys().OrderBy(k => k).Take(2).ToSql());
}
