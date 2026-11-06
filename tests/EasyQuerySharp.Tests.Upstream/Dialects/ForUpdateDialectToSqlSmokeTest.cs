namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>
/// 对应 Java：ForUpdateDialectToSQLSmokeTest.java。同一条分页查询加 FOR UPDATE 在各方言下只渲染、不连库：
/// 锁子句写在整条语句（含 Oracle 的 ROWNUM 外层）的末尾；不支持行锁的方言抛出 <see cref="NotSupportedException"/>。
/// 还没有对应方言的用例（H2、GaussDB、达梦、DB2、ClickHouse、DuckDB）随方言一起移植。
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
    // 人大金仓（PG 模式）在 C# 里用 PostgreSQL 方言
    [Upstream("ForUpdateDialectToSQLSmokeTest.java#kingbaseESForUpdateToSQLSmoke")]
    public void ForUpdateAppendsAtEnd(string dialect) => RowLockAssert.EndsWithSingleForUpdate(LockedPageSql(dialect));

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#oracleForUpdateToSQLSmoke")]
    public void OracleForUpdateToSqlSmoke()
    {
        var sql = LockedPageSql("oracle");
        Assert.Contains("ROWNUM", sql, StringComparison.OrdinalIgnoreCase);
        RowLockAssert.EndsWithSingleForUpdate(sql);
    }

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#msSQLForUpdateThrowsUnsupported")]
    public void SqlServerForUpdateThrowsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => LockedPageSql("mssql"));
        Assert.Contains("SQL Server 不支持 FOR UPDATE", ex.Message);
    }

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#sqliteForUpdateToSQLSmoke",
        Deviation = "SQLite 没有 FOR UPDATE 语法（写事务锁整个库），Java 照样拼出、执行时才报语法错误；C# 渲染时就抛出 NotSupportedException")]
    public void SqliteForUpdateThrowsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => LockedPageSql("sqlite"));
        Assert.Contains("SQLite 不支持 FOR UPDATE", ex.Message);
    }
}
