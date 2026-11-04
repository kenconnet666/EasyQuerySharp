using EasyQuerySharp.Tests.Upstream.Infrastructure;
using EasyQuerySharp.Tests.Upstream.Mysql8.Entities.Bank;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/ForUpdateTest.java。行锁只能在事务里、对单表调用一次，只写在最外层查询上。</summary>
public sealed class ForUpdateTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private Task<SysUser?> LockU1() => Db.Query<SysUser>().Where(u => u.Id == "u1").ForUpdate().FirstOrDefaultAsync(Ct);

    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateSqlAppendInTransaction")]
    public async Task TestForUpdateSqlAppendInTransaction()
    {
        using var listen = SqlCapture.Listen();
        await using (var tx = Db.BeginTransaction())
        {
            await LockU1();
            await tx.CommitAsync(Ct);
        }
        listen.AssertSql("SELECT `id`,`name`,`phone`,`age`,`create_time` FROM `t_sys_user` WHERE `id` = ? LIMIT 1 FOR UPDATE");
    }

    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateWithoutTransactionThrows")]
    public void TestForUpdateWithoutTransactionThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<SysUser>().Where(u => u.Id == "u1").ForUpdate());
        Assert.Contains("BeginTransaction", ex.Message);
    }

    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateJoinNotSupported")]
    public async Task TestForUpdateJoinNotSupported()
    {
        await using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<SysUser>().LeftJoin<SysBankCard>((u, c) => u.Id == c.Uid).ForUpdate());
        Assert.Contains("single-table", ex.Message);
    }

    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateDuplicateCallThrows")]
    public async Task TestForUpdateDuplicateCallThrows()
    {
        await using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<SysUser>().Where(u => u.Id == "u1").ForUpdate().ForUpdate());
        Assert.Contains("repeated", ex.Message);
    }

    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateBlocksConcurrentTransaction")]
    public Task TestForUpdateBlocksConcurrentTransaction() => RowLockAssert.SecondTransactionWaitsAsync(Db, LockU1);

    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery")]
    public async Task TestForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery()
    {
        await using var tx = Db.BeginTransaction();
        var existsSql = Db.Query<SysUser>().Where(u => Sql.Exists(Db.Query<SysBankCard>().Where(c => c.Uid == u.Id))).ForUpdate().ToSql();
        RowLockAssert.EndsWithSingleForUpdate(existsSql);

        var uids = Db.Query<SysBankCard>().Select(c => c.Uid);
        var inSql = Db.Query<SysUser>().Where(u => u.Id.In(uids)).ForUpdate().ToSql();
        RowLockAssert.EndsWithSingleForUpdate(inSql);
    }

    // Java 用 cloneQueryable().selectCount() / limit(1).select(" 1 ") 渲染计数与存在判断；C# 直接执行 CountAsync / AnyAsync 后看捕获的 SQL
    [Fact, Upstream("mysql8/ForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent")]
    public async Task TestForUpdateDerivedCountAndExistsSqlBehaviorConsistent()
    {
        using var listen = SqlCapture.Listen();
        await using var tx = Db.BeginTransaction();
        var locked = Db.Query<SysUser>().Where(u => u.Id == "u1").ForUpdate();
        await locked.CountAsync(Ct);
        await locked.AnyAsync(Ct);
        var sqls = listen.Events.Select(e => e.Statement.Text).ToList();
        Assert.Equal(2, sqls.Count);
        Assert.All(sqls, RowLockAssert.EndsWithSingleForUpdate);
        await tx.CommitAsync(Ct);
    }
}
