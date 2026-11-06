namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>
/// 对应 Java：ForUpdateDialectToSQLSmokeTest.java。同一条分页查询加 FOR UPDATE 在各方言下只渲染、不连库：
/// 锁子句写在整条语句（含 Oracle 的 ROWNUM 外层）的末尾；不支持行锁的方言抛出 <see cref="NotSupportedException"/>。
/// DuckDB 的用例在 DuckDb/DuckDbDialectTest。
/// </summary>
public sealed class ForUpdateDialectToSqlSmokeTest
{
    private static string LockedPageSql(string dialect)
    {
        var db = SqlOnly.Db(dialect);
        using var tx = db.BeginTransaction();
        return db.Query<DocBankCard>().Where(c => c.Id != null).OrderBy(c => c.Id).Skip(1).Take(2).ForUpdate().ToSql();
    }

    [Theory]
    [InlineData("mysql"), Upstream("ForUpdateDialectToSQLSmokeTest.java#mysqlForUpdateToSQLSmoke")]
    [InlineData("pgsql"), Upstream("ForUpdateDialectToSQLSmokeTest.java#pgsqlForUpdateToSQLSmoke")]
    [InlineData("kingbase"), Upstream("ForUpdateDialectToSQLSmokeTest.java#kingbaseESForUpdateToSQLSmoke")]
    [InlineData("gauss"), Upstream("ForUpdateDialectToSQLSmokeTest.java#gaussDBForUpdateToSQLSmoke")]
    public void ForUpdateAppendsAtEnd(string dialect) => RowLockAssert.EndsWithSingleForUpdate(LockedPageSql(dialect));

    // 达梦与 Oracle 一样用 ROWNUM 分页，锁子句写在最外层之后
    [Theory]
    [InlineData("oracle"), Upstream("ForUpdateDialectToSQLSmokeTest.java#oracleForUpdateToSQLSmoke")]
    [InlineData("dameng"), Upstream("ForUpdateDialectToSQLSmokeTest.java#damengForUpdateToSQLSmoke")]
    public void RowNumForUpdateToSqlSmoke(string dialect)
    {
        var sql = LockedPageSql(dialect);
        Assert.Contains("ROWNUM", sql, StringComparison.OrdinalIgnoreCase);
        RowLockAssert.EndsWithSingleForUpdate(sql);
    }

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#msSQLForUpdateThrowsUnsupported")]
    public void SqlServerForUpdateThrowsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => LockedPageSql("mssql"));
        Assert.Contains("SQL Server 不支持 FOR UPDATE", ex.Message);
    }

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#clickHouseForUpdateThrowsUnsupported")]
    public void ClickHouseForUpdateThrowsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => LockedPageSql("clickhouse"));
        Assert.Contains("ClickHouse 不支持 FOR UPDATE", ex.Message);
    }

    // Db2 写 FOR UPDATE WITH RS（锁到事务结束），Java 的断言只检查到 FOR UPDATE 为止
    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#db2ForUpdateToSQLSmoke")]
    public void Db2ForUpdateToSqlSmoke()
    {
        var sql = LockedPageSql("db2");
        Assert.Contains("OFFSET 1 ROWS FETCH NEXT 2 ROWS ONLY FOR UPDATE", sql.ToUpperInvariant());
        Assert.Equal(1, sql.Split("FOR UPDATE").Length - 1);
    }

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#h2ForUpdateToSQLSmoke",
        NotApplicable = "C# 没有 H2 方言（H2 没有 .NET 驱动，H2 的用例在 SQLite 上跑）；SQLite 没有行锁语法，见 sqliteForUpdateToSQLSmoke")]
    public void H2ForUpdateToSqlSmoke()
    {
    }

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#sqliteForUpdateToSQLSmoke",
        Deviation = "SQLite 没有 FOR UPDATE 语法（写事务锁整个库），Java 照样拼出、执行时才报语法错误；C# 渲染时就抛出 NotSupportedException")]
    public void SqliteForUpdateThrowsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => LockedPageSql("sqlite"));
        Assert.Contains("SQLite 不支持 FOR UPDATE", ex.Message);
    }
}
