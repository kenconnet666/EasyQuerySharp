namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// UNION 的分支自带排序、分页（带出按父对象取段的 UNION ALL 模式就是这样）：默认与 Java 一致写成 <c>(q)</c>；
/// SQLite 的分支不能加括号、也不能带 ORDER BY / LIMIT，SQL Server 括号里不能带 ORDER BY，这两个方言包成派生表。C# 独有。
/// </summary>
public sealed class UnionBranchTest
{
    private static string UnionSql(string dialect)
    {
        var db = SqlOnly.Db(dialect);
        Query<Draft<string?>> Top(int stars) => db.Query<Topic>().Where(t => t.Stars > stars).OrderBy(t => t.Stars).Take(2).Select(t => new Draft<string?>(t.Title));
        return Top(1).UnionAll(Top(2), db.Query<Topic>().Select(t => new Draft<string?>(t.Title))).ToSql();
    }

    [Fact]
    public void ParenthesizesBranchesByDefault() =>
        Assert.Equal(
            "SELECT t3.`value1` AS `value1` FROM ( (SELECT t.`title` AS `value1` FROM `t_topic` t WHERE t.`stars` > ? ORDER BY t.`stars` ASC LIMIT 2)  UNION ALL  "
            + "(SELECT t1.`title` AS `value1` FROM `t_topic` t1 WHERE t1.`stars` > ? ORDER BY t1.`stars` ASC LIMIT 2)  UNION ALL  (SELECT t2.`title` AS `value1` FROM `t_topic` t2) ) t3",
            UnionSql("mysql"));

    [Fact]
    public void SqliteWrapsOrderedBranchesWithoutParentheses() =>
        Assert.Equal(
            "SELECT t3.\"value1\" AS \"value1\" FROM ( SELECT * FROM (SELECT t.\"title\" AS \"value1\" FROM \"t_topic\" t WHERE t.\"stars\" > ? ORDER BY t.\"stars\" ASC LIMIT 2)  UNION ALL  "
            + "SELECT * FROM (SELECT t1.\"title\" AS \"value1\" FROM \"t_topic\" t1 WHERE t1.\"stars\" > ? ORDER BY t1.\"stars\" ASC LIMIT 2)  UNION ALL  SELECT t2.\"title\" AS \"value1\" FROM \"t_topic\" t2 ) t3",
            UnionSql("sqlite"));

    [Fact]
    public void SqlServerWrapsOrderedBranchesIntoDerivedTables() =>
        Assert.Equal(
            "SELECT t3.[value1] AS [value1] FROM ( (SELECT * FROM (SELECT TOP 2 t.[title] AS [value1] FROM [t_topic] t WHERE t.[stars] > ? ORDER BY t.[stars] ASC) t)  UNION ALL  "
            + "(SELECT * FROM (SELECT TOP 2 t1.[title] AS [value1] FROM [t_topic] t1 WHERE t1.[stars] > ? ORDER BY t1.[stars] ASC) t)  UNION ALL  (SELECT t2.[title] AS [value1] FROM [t_topic] t2) ) t3",
            UnionSql("mssql"));
}
