namespace EasyQuerySharp.Tests.Upstream.Functions;

/// <summary>
/// 对应 Java：NumberFunctionsTest.java。按方言的用例写成 Theory，每种方言一行数据；只生成 SQL、不连库。
/// Java 的 asNumber(Integer.class) 只改变结果类型、不改变 SQL，C# 里用强制转换表达。
/// </summary>
public sealed class NumberFunctionsTest
{
    [Theory, Upstream("NumberFunctionsTest.java#addMySQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#addMSSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#addPGSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#addORACLE", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）")]
    [InlineData("mysql", "SELECT (t.`score` + ?) AS `value1`,(t.`score` + ?) AS `value2`,(t.`score` + ?) AS `value3`,(t.`score` + t.`score`) AS `value4` FROM `t_blog` t WHERE t.`deleted` = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    [InlineData("mssql", "SELECT (t.[score] + ?) AS [value1],(t.[score] + ?) AS [value2],(t.[score] + ?) AS [value3],(t.[score] + t.[score]) AS [value4] FROM [t_blog] t WHERE t.[deleted] = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    [InlineData("pgsql", "SELECT (t.\"score\" + ?) AS \"value1\",(t.\"score\" + ?) AS \"value2\",(t.\"score\" + ?) AS \"value3\",(t.\"score\" + t.\"score\") AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    [InlineData("oracle", "SELECT (t.\"score\" + ?) AS \"value1\",(t.\"score\" + ?) AS \"value2\",(t.\"score\" + ?) AS \"value3\",(t.\"score\" + t.\"score\") AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    public void Add(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Select(b => new Draft<decimal?, int?, long?, decimal?>(b.Score + 1, (int?)(b.Score + 1), (long?)(b.Score + 1), b.Score + b.Score)).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("NumberFunctionsTest.java#subtractMySQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#subtractMSSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#subtractPGSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#subtractORACLE", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）")]
    [InlineData("mysql", "SELECT (t.`score` - ?) AS `value1`,(t.`score` - ?) AS `value2`,(t.`score` - ?) AS `value3`,(t.`score` - t.`score`) AS `value4` FROM `t_blog` t WHERE t.`deleted` = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    [InlineData("mssql", "SELECT (t.[score] - ?) AS [value1],(t.[score] - ?) AS [value2],(t.[score] - ?) AS [value3],(t.[score] - t.[score]) AS [value4] FROM [t_blog] t WHERE t.[deleted] = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    [InlineData("pgsql", "SELECT (t.\"score\" - ?) AS \"value1\",(t.\"score\" - ?) AS \"value2\",(t.\"score\" - ?) AS \"value3\",(t.\"score\" - t.\"score\") AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    [InlineData("oracle", "SELECT (t.\"score\" - ?) AS \"value1\",(t.\"score\" - ?) AS \"value2\",(t.\"score\" - ?) AS \"value3\",(t.\"score\" - t.\"score\") AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "1(BigDecimal),1(BigDecimal),1(BigDecimal),false(Boolean)")]
    public void Subtract(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Select(b => new Draft<decimal?, int?, long?, decimal?>(b.Score - 1, (int?)(b.Score - 1), (long?)(b.Score - 1), b.Score - b.Score)).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("NumberFunctionsTest.java#multiplyMySQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#multiplyMSSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#multiplyPGSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#multiplyORACLE", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）")]
    [InlineData("mysql", "SELECT (t.`score` * ?) AS `value1`,(t.`score` * t.`score`) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?", "2(BigDecimal),false(Boolean)")]
    [InlineData("mssql", "SELECT (t.[score] * ?) AS [value1],(t.[score] * t.[score]) AS [value2] FROM [t_blog] t WHERE t.[deleted] = ?", "2(BigDecimal),false(Boolean)")]
    [InlineData("pgsql", "SELECT (t.\"score\" * ?) AS \"value1\",(t.\"score\" * t.\"score\") AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "2(BigDecimal),false(Boolean)")]
    [InlineData("oracle", "SELECT (t.\"score\" * ?) AS \"value1\",(t.\"score\" * t.\"score\") AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "2(BigDecimal),false(Boolean)")]
    public void Multiply(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Select(b => new Draft<decimal?, decimal?>(b.Score * 2, b.Score * b.Score)).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("NumberFunctionsTest.java#divideMySQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#divideMSSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#dividePGSQL", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）"), Upstream("NumberFunctionsTest.java#divideORACLE", Deviation = "C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同）")]
    [InlineData("mysql", "SELECT (t.`score` / ?) AS `value1`,(t.`score` / t.`score`) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?", "2(BigDecimal),false(Boolean)")]
    [InlineData("mssql", "SELECT (t.[score] / ?) AS [value1],(t.[score] / t.[score]) AS [value2] FROM [t_blog] t WHERE t.[deleted] = ?", "2(BigDecimal),false(Boolean)")]
    [InlineData("pgsql", "SELECT (t.\"score\" / ?) AS \"value1\",(t.\"score\" / t.\"score\") AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "2(BigDecimal),false(Boolean)")]
    [InlineData("oracle", "SELECT (t.\"score\" / ?) AS \"value1\",(t.\"score\" / t.\"score\") AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "2(BigDecimal),false(Boolean)")]
    public void Divide(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Select(b => new Draft<decimal?, decimal?>(b.Score / 2, b.Score / b.Score)).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("NumberFunctionsTest.java#avgMySQL"), Upstream("NumberFunctionsTest.java#avgMSSQL"), Upstream("NumberFunctionsTest.java#avgPGSQL"), Upstream("NumberFunctionsTest.java#avgORACLE")]
    [InlineData("mysql", "SELECT AVG(t.`score`) AS `value1`,AVG(DISTINCT t.`score`) AS `value2`,AVG(DISTINCT (CASE WHEN t.`title` LIKE ? THEN t.`score` ELSE NULL END)) AS `value3` FROM `t_blog` t WHERE t.`deleted` = ?", "%小说%(String),false(Boolean)")]
    [InlineData("mssql", "SELECT AVG(t.[score]) AS [value1],AVG(DISTINCT t.[score]) AS [value2],AVG(DISTINCT (CASE WHEN t.[title] LIKE ? THEN t.[score] ELSE NULL END)) AS [value3] FROM [t_blog] t WHERE t.[deleted] = ?", "%小说%(String),false(Boolean)")]
    [InlineData("pgsql", "SELECT AVG(t.\"score\") AS \"value1\",AVG(DISTINCT t.\"score\") AS \"value2\",AVG(DISTINCT (CASE WHEN t.\"title\" LIKE ? THEN t.\"score\" ELSE NULL END)) AS \"value3\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "%小说%(String),false(Boolean)")]
    [InlineData("oracle", "SELECT AVG(t.\"score\") AS \"value1\",AVG(DISTINCT t.\"score\") AS \"value2\",AVG(DISTINCT (CASE WHEN t.\"title\" LIKE ? THEN t.\"score\" ELSE NULL END)) AS \"value3\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "%小说%(String),false(Boolean)")]
    public void Avg(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Select(b => new Draft<decimal?, decimal?, decimal?>(Sql.Avg(b.Score), Sql.AvgDistinct(b.Score), Sql.AvgDistinct(b.Score, b.Title!.Contains("小说")))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("NumberFunctionsTest.java#sumMySQL"), Upstream("NumberFunctionsTest.java#sumMSSQL"), Upstream("NumberFunctionsTest.java#sumPGSQL"), Upstream("NumberFunctionsTest.java#sumORACLE")]
    [InlineData("mysql", "SELECT SUM(t.`score`) AS `value1`,SUM(DISTINCT t.`score`) AS `value2`,SUM(DISTINCT t.`score`) AS `value3`,SUM(DISTINCT (CASE WHEN t.`title` LIKE ? THEN t.`score` ELSE ? END)) AS `value4` FROM `t_blog` t WHERE t.`deleted` = ?", "%小说%(String),0(Long),false(Boolean)")]
    [InlineData("mssql", "SELECT SUM(t.[score]) AS [value1],SUM(DISTINCT t.[score]) AS [value2],SUM(DISTINCT t.[score]) AS [value3],SUM(DISTINCT (CASE WHEN t.[title] LIKE ? THEN t.[score] ELSE ? END)) AS [value4] FROM [t_blog] t WHERE t.[deleted] = ?", "%小说%(String),0(Long),false(Boolean)")]
    [InlineData("pgsql", "SELECT SUM(t.\"score\") AS \"value1\",SUM(DISTINCT t.\"score\") AS \"value2\",SUM(DISTINCT t.\"score\") AS \"value3\",SUM(DISTINCT (CASE WHEN t.\"title\" LIKE ? THEN t.\"score\" ELSE ? END)) AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "%小说%(String),0(Long),false(Boolean)")]
    [InlineData("oracle", "SELECT SUM(t.\"score\") AS \"value1\",SUM(DISTINCT t.\"score\") AS \"value2\",SUM(DISTINCT t.\"score\") AS \"value3\",SUM(DISTINCT (CASE WHEN t.\"title\" LIKE ? THEN t.\"score\" ELSE ? END)) AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?", "%小说%(String),0(Long),false(Boolean)")]
    public void Sum(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Select(b => new Draft<decimal?, int?, long?, decimal?>(Sql.Sum(b.Score), (int?)Sql.SumDistinct(b.Score), (long?)Sql.SumDistinct(b.Score), Sql.SumDistinct(b.Score, b.Title!.Contains("小说")))).ToStatement(), sql, parameters);
    }

    // 合并成分组 JOIN 的各种计数与 any / none：同样的条件只算一列（where(..).any() 与 any(..) 共用 __any4__）
    [Fact, Upstream("NumberFunctionsTest.java#subQueryMySQL")]
    public void SubQueryMySQL() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mysql").Query<Mysql8.SysUser>().GroupJoin(o => o.BankCards)
                .Where(u => u.BankCards!.Select(s => s.Id).LongCount() > 1L
                            && u.BankCards!.Any()
                            && u.BankCards!.Where(bc => bc.Uid == null).Any()
                            && u.BankCards!.Any(bc => bc.Uid == null)
                            && !u.BankCards!.Any()
                            && !u.BankCards!.Where(bc => bc.Uid == null).Any()
                            && !u.BankCards!.Any(bc => bc.Uid == null))
                .Select(u => new Draft<bool>(u.BankCards!.Where(x => x.Id != "1").Any()))
                .ToStatement(),
            "SELECT IFNULL(t2.`__any7__`,?) AS `value1` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(t1.`id`) AS `__count2__`,(COUNT(*) > 0) AS `__any3__`,(COUNT((CASE WHEN t1.`uid` IS NULL THEN ? ELSE NULL END)) > 0) AS `__any4__`,(COUNT(*) <= 0) AS `__none5__`,(COUNT((CASE WHEN t1.`uid` IS NULL THEN ? ELSE NULL END)) <= 0) AS `__none6__`,(COUNT((CASE WHEN t1.`id` <> ? THEN ? ELSE NULL END)) > 0) AS `__any7__` FROM `t_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) > ? AND IFNULL(t2.`__any3__`,?) = ? AND IFNULL(t2.`__any4__`,?) = ? AND IFNULL(t2.`__any4__`,?) = ? AND IFNULL(t2.`__none5__`,?) = ? AND IFNULL(t2.`__none6__`,?) = ? AND IFNULL(t2.`__none6__`,?) = ?",
            "false(Boolean),1(Integer),1(Integer),1(String),1(Integer),1(Long),false(Boolean),true(Boolean),false(Boolean),true(Boolean),false(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean)");

    // 同 subQueryMySQL：Oracle 没有布尔列，any / none 写成 CASE WHEN … THEN true ELSE false END
    [Fact, Upstream("NumberFunctionsTest.java#subQueryORACLE")]
    public void SubQueryORACLE() =>
        SqlOnly.AssertStatement(SqlOnly.Db("oracle").Query<Mysql8.SysUser>().GroupJoin(o => o.BankCards)
                .Where(u => u.BankCards!.Select(s => s.Id).LongCount() > 1L
                            && u.BankCards!.Any()
                            && u.BankCards!.Where(bc => bc.Uid == null).Any()
                            && u.BankCards!.Any(bc => bc.Uid == null)
                            && !u.BankCards!.Any()
                            && !u.BankCards!.Where(bc => bc.Uid == null).Any()
                            && !u.BankCards!.Any(bc => bc.Uid == null))
                .Select(u => new Draft<bool>(u.BankCards!.Where(x => x.Id != "1").Any()))
                .ToStatement(),
            "SELECT NVL(t2.\"__any7__\",?) AS \"value1\" FROM \"t_sys_user\" t LEFT JOIN (SELECT t1.\"uid\" AS \"__group_key1__\",COUNT(t1.\"id\") AS \"__count2__\",(CASE WHEN (COUNT(*) > 0) THEN ? ELSE ? END) AS \"__any3__\",(CASE WHEN (COUNT((CASE WHEN t1.\"uid\" IS NULL THEN ? ELSE NULL END)) > 0) THEN ? ELSE ? END) AS \"__any4__\",(CASE WHEN (COUNT(*) <= 0) THEN ? ELSE ? END) AS \"__none5__\",(CASE WHEN (COUNT((CASE WHEN t1.\"uid\" IS NULL THEN ? ELSE NULL END)) <= 0) THEN ? ELSE ? END) AS \"__none6__\",(CASE WHEN (COUNT((CASE WHEN t1.\"id\" <> ? THEN ? ELSE NULL END)) > 0) THEN ? ELSE ? END) AS \"__any7__\" FROM \"t_bank_card\" t1 GROUP BY t1.\"uid\") t2 ON t2.\"__group_key1__\" = t.\"id\" WHERE NVL(t2.\"__count2__\",0) > ? AND NVL(t2.\"__any3__\",?) = ? AND NVL(t2.\"__any4__\",?) = ? AND NVL(t2.\"__any4__\",?) = ? AND NVL(t2.\"__none5__\",?) = ? AND NVL(t2.\"__none6__\",?) = ? AND NVL(t2.\"__none6__\",?) = ?",
            "false(Boolean),true(Boolean),false(Boolean),1(Integer),true(Boolean),false(Boolean),true(Boolean),false(Boolean),1(Integer),true(Boolean),false(Boolean),1(String),1(Integer),true(Boolean),false(Boolean),1(Long),false(Boolean),true(Boolean),false(Boolean),true(Boolean),false(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean)");
}
