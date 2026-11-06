namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// Kingbase、GaussDB / openGauss 方言与 PostgreSQL 不同的写法（逐字对应 Java 的 sql-kingbase-es、sql-gauss-db）。
/// Java 只有 testDoubleConcat、testKingbaseCte 两个用例，其余差异在这里按 Java 的函数模板核对；没有实库，只比对 SQL。
/// </summary>
public sealed class PgFamilyDialectTest
{
    private const string Cards = "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE ";

    // 值里含通配符：PG 写 LIKE CONCAT(…) ESCAPE，两者写 STRPOS，值不转义（Java 的 KingbaseESLikeSQLFunction / GaussDBLikeSQLFunction）
    [Theory]
    [InlineData("kingbase"), InlineData("gauss")]
    public void WildcardLikeUsesStrpos(string dialect)
    {
        var db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(db.Query<DocBankCard>().Where(b => b.Type!.Contains("30%")).ToStatement(), Cards + "STRPOS(\"type\",?) > 0", "30%(String)");
        SqlOnly.AssertStatement(db.Query<DocBankCard>().Where(b => b.Type!.StartsWith("30%")).ToStatement(), Cards + "STRPOS(\"type\",?) = 1", "30%(String)");
        SqlOnly.AssertStatement(db.Query<DocBankCard>().Where(b => b.Type!.EndsWith("30%")).ToStatement(),
            Cards + "STRPOS(\"type\",?) = (CHAR_LENGTH(\"type\") - CHAR_LENGTH(?) + 1)", "30%(String),30%(String)");
        SqlOnly.AssertStatement(db.Query<DocBankCard>().Where(b => !b.Type!.Contains("30%")).ToStatement(), Cards + "STRPOS(\"type\",?) = 0", "30%(String)");
        // 不含通配符时与 PG 一样是普通的 LIKE ?
        SqlOnly.AssertStatement(db.Query<DocBankCard>().Where(b => b.Type!.Contains("30")).ToStatement(), Cards + "\"type\" LIKE ?", "%30%(String)");
    }

    // 条件当值：PG 直接写条件，两者写 CASE WHEN（Java 的 KingbaseESBooleanSQLFunction / GaussDBBooleanSQLFunction）
    [Theory]
    [InlineData("pgsql", "SELECT (t.\"code\" = ?) AS \"value1\" FROM \"doc_bank_card\" t", "123(String)")]
    [InlineData("kingbase", "SELECT (CASE WHEN (t.\"code\" = ?) THEN ? ELSE ? END) AS \"value1\" FROM \"doc_bank_card\" t", "123(String),true(Boolean),false(Boolean)")]
    [InlineData("gauss", "SELECT (CASE WHEN (t.\"code\" = ?) THEN ? ELSE ? END) AS \"value1\" FROM \"doc_bank_card\" t", "123(String),true(Boolean),false(Boolean)")]
    public void PredicateAsValue(string dialect, string sql, string parameters) =>
        SqlOnly.AssertStatement(SqlOnly.Db(dialect).Query<DocBankCard>().Select(b => new Draft<bool>(b.Code == "123")).ToStatement(), sql, parameters);

    // 多个参数的 CONCAT：Kingbase 两两嵌套，GaussDB 与 PG 一样平铺
    [Theory]
    [InlineData("kingbase", "SELECT CONCAT(CONCAT(t.\"code\",t.\"type\"),?) AS \"value1\" FROM \"doc_bank_card\" t")]
    [InlineData("gauss", "SELECT CONCAT(t.\"code\",t.\"type\",?) AS \"value1\" FROM \"doc_bank_card\" t")]
    public void Concat(string dialect, string sql) =>
        SqlOnly.AssertStatement(SqlOnly.Db(dialect).Query<DocBankCard>().Select(b => new Draft<string>(b.Code + b.Type + "x")).ToStatement(), sql, "x(String)");

    // Kingbase 的日期加减用 CONCAT 拼间隔（Java 的 KingbaseESDateTime2PlusSQLFunction，加月是 plusMonths 的写法），相差用 timestampdiff
    [Fact]
    public void KingbaseDateFunctions()
    {
        var time2025 = new DateTime(2025, 1, 1);
        SqlOnly.AssertStatement(SqlOnly.Db("kingbase").Query<BlogEntity>()
                .Select(b => new Draft<DateTime, DateTime, DateTime, long, long>(b.CreateTime!.Value.AddDays(2), b.CreateTime!.Value.AddMonths(3), b.CreateTime!.Value.AddYears(4),
                    (long)(time2025 - b.CreateTime!.Value).TotalDays, (long)(time2025 - b.CreateTime!.Value).TotalSeconds)).ToStatement(),
            "SELECT ((t.\"create_time\")::TIMESTAMP + CONCAT((?), ' day')::INTERVAL) AS \"value1\",((t.\"create_time\")::TIMESTAMP + CONCAT((?),' month')::INTERVAL) AS \"value2\","
            + "((t.\"create_time\")::TIMESTAMP + CONCAT((?), ' year')::INTERVAL) AS \"value3\",timestampdiff('DAY', t.\"create_time\", ?) AS \"value4\",timestampdiff('SECOND', t.\"create_time\", ?) AS \"value5\" "
            + "FROM \"t_blog\" t WHERE t.\"deleted\" = ?",
            "2(Long),3(Long),4(Long),2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime),false(Boolean)");
    }

    // 其余与 PG 相同：分页、RETURNING、ON CONFLICT 都沿用
    [Theory]
    [InlineData("kingbase"), InlineData("gauss")]
    public void InheritsPostgreSql(string dialect)
    {
        var db = SqlOnly.Db(dialect);
        Assert.Equal(SqlOnly.Db("pgsql").Query<DocBankCard>().Where(c => c.Id != null).OrderBy(c => c.Id).Skip(1).Take(2).ToSql(),
            db.Query<DocBankCard>().Where(c => c.Id != null).OrderBy(c => c.Id).Skip(1).Take(2).ToSql());
        Assert.Equal(SqlOnly.Db("pgsql").Insert(new DocBankCard { Id = "1", Code = "c" }).OnConflictIgnore().ToSql(),
            db.Insert(new DocBankCard { Id = "1", Code = "c" }).OnConflictIgnore().ToSql());
    }
}
