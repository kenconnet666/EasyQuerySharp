using EasyQuerySharp.Tests.Upstream.Mssql;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// <c>Sql.MaxColumns</c> / <c>Sql.MinColumns</c> 在各方言的写法（Java 的 maxColumns / minColumns）。SQL Server 的写法由 mssql/MsSQLQueryTest 覆盖，
/// 达梦沿用 Oracle 的写法。SQLite 没有 GREATEST，Java 版在这里写 GREATEST，C# 写多参数的 MAX。
/// </summary>
public sealed class MaxMinColumnsTest
{
    [Theory]
    [InlineData("mysql", "SELECT `id`,`column1`,`column2`,`column3` FROM `t_calc` WHERE GREATEST(`column1`,`column2`,`column3`) = ?")]
    [InlineData("pgsql", "SELECT \"id\",\"column1\",\"column2\",\"column3\" FROM \"t_calc\" WHERE GREATEST(\"column1\",\"column2\",\"column3\") = ?")]
    [InlineData("sqlite", "SELECT \"id\",\"column1\",\"column2\",\"column3\" FROM \"t_calc\" WHERE MAX(\"column1\",\"column2\",\"column3\") = ?")]
    [InlineData("oracle", "SELECT \"id\",\"column1\",\"column2\",\"column3\" FROM \"t_calc\" WHERE (SELECT MAX(__mmc.__col__) FROM (SELECT \"column1\" AS __col__ FROM DUAL UNION ALL SELECT \"column2\" AS __col__ FROM DUAL UNION ALL SELECT \"column3\" AS __col__ FROM DUAL) __mmc) = ?")]
    [InlineData("mssql", "SELECT [id],[column1],[column2],[column3] FROM [t_calc] WHERE (SELECT MAX(__col__) FROM (VALUES ([column1]), ([column2]), ([column3])) AS t(__col__)) = ?")]
    public void MaxColumns(string dialect, string sql) =>
        SqlOnly.AssertStatement(SqlOnly.Db(dialect).Query<MsSQLCalc>().Where(m => Sql.MaxColumns(m.Column1, m.Column2, m.Column3) == 1m).ToStatement(), sql, "1(BigDecimal)");

    [Theory]
    [InlineData("mysql", "SELECT LEAST(t.`column1`,t.`column2`) AS `value1` FROM `t_calc` t")]
    [InlineData("pgsql", "SELECT LEAST(t.\"column1\",t.\"column2\") AS \"value1\" FROM \"t_calc\" t")]
    [InlineData("sqlite", "SELECT MIN(t.\"column1\",t.\"column2\") AS \"value1\" FROM \"t_calc\" t")]
    [InlineData("oracle", "SELECT (SELECT MIN(__mmc.__col__) FROM (SELECT t.\"column1\" AS __col__ FROM DUAL UNION ALL SELECT t.\"column2\" AS __col__ FROM DUAL) __mmc) AS \"value1\" FROM \"t_calc\" t")]
    [InlineData("mssql", "SELECT (SELECT MIN(__col__) FROM (VALUES (t.[column1]), (t.[column2])) AS t(__col__)) AS [value1] FROM [t_calc] t")]
    public void MinColumns(string dialect, string sql) =>
        SqlOnly.AssertStatement(SqlOnly.Db(dialect).Query<MsSQLCalc>().Select(m => new Draft<decimal?>(Sql.MinColumns(m.Column1, m.Column2))).ToStatement(), sql);
}
