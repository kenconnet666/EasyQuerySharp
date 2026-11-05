namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>对应 Java：pgsql/PgSQLForUpdateTest.java，与 mysql8/ForUpdateTest 相同的用例跑在 PostgreSQL 上。</summary>
public sealed class PgSqlForUpdateTest(PgFixture fixture) : PgTest(fixture)
{
    private Task<DocUser?> LockXiaoMing() => Db.Query<DocUser>().Where(u => u.Id == "小明id").ForUpdate().FirstOrDefaultAsync(Ct);

    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateSqlAppendInTransaction")]
    public async Task TestForUpdateSqlAppendInTransaction()
    {
        using var listen = SqlCapture.Listen();
        await using (var tx = Db.BeginTransaction())
        {
            Assert.NotNull(await LockXiaoMing());
            await tx.CommitAsync(Ct);
        }
        listen.AssertSql("SELECT \"id\",\"name\",\"phone\",\"age\" FROM \"doc_user\" WHERE \"id\" = ? LIMIT 1 FOR UPDATE");
    }

    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateWithoutTransactionThrows")]
    public void TestForUpdateWithoutTransactionThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<DocUser>().Where(u => u.Id == "小明id").ForUpdate());
        Assert.Contains("BeginTransaction", ex.Message);
    }

    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateJoinNotSupported")]
    public async Task TestForUpdateJoinNotSupported()
    {
        await using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<DocUser>().LeftJoin<DocBankCard>((u, c) => u.Id == c.Uid).ForUpdate());
        Assert.Contains("single-table", ex.Message);
    }

    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateDuplicateCallThrows")]
    public async Task TestForUpdateDuplicateCallThrows()
    {
        await using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<DocUser>().Where(u => u.Id == "小明id").ForUpdate().ForUpdate());
        Assert.Contains("repeated", ex.Message);
    }

    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateBlocksConcurrentTransaction")]
    public Task TestForUpdateBlocksConcurrentTransaction() => RowLockAssert.SecondTransactionWaitsAsync(Db, LockXiaoMing);

    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery")]
    public async Task TestForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery()
    {
        await using var tx = Db.BeginTransaction();
        var existsSql = Db.Query<DocUser>().Where(u => Sql.Exists(Db.Query<DocBankCard>().Where(c => c.Uid == u.Id))).ForUpdate().ToSql();
        RowLockAssert.EndsWithSingleForUpdate(existsSql);

        var uids = Db.Query<DocBankCard>().Select(c => c.Uid);
        var inSql = Db.Query<DocUser>().Where(u => u.Id.In(uids)).ForUpdate().ToSql();
        RowLockAssert.EndsWithSingleForUpdate(inSql);
    }

    // Java 用 cloneQueryable().selectCount() / limit(1).select(" 1 ") 只渲染不执行；C# 执行 AnyAsync / CountAsync 后看捕获的 SQL。
    // PostgreSQL 不允许聚合查询加 FOR UPDATE，计数在执行时由数据库报错（SQL 与 Java 相同）
    [Fact, Upstream("pgsql/PgSQLForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent")]
    public async Task TestForUpdateDerivedCountAndExistsSqlBehaviorConsistent()
    {
        using var listen = SqlCapture.Listen();
        await using (var tx = Db.BeginTransaction())
            Assert.True(await Db.Query<DocUser>().Where(u => u.Id == "小明id").ForUpdate().AnyAsync(Ct));
        await using (var tx = Db.BeginTransaction())
            await Assert.ThrowsAsync<EasyQueryException>(() => Db.Query<DocUser>().Where(u => u.Id == "小明id").ForUpdate().CountAsync(Ct));
        var sqls = listen.Events.Select(e => e.Statement.Text).ToList();
        Assert.Equal(2, sqls.Count);
        Assert.All(sqls, RowLockAssert.EndsWithSingleForUpdate);
    }
}
