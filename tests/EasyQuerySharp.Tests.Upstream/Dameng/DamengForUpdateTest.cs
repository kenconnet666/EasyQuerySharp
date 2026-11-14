using EasyQuerySharp.Metadata;
using EasyQuerySharp.TestKit.Databases;
using SysBankCard = EasyQuerySharp.Tests.Upstream.Mysql8.SysBankCard;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>
/// 对应 Java：dameng/DamengForUpdateTest.java，与 mysql8/ForUpdateTest 相同的用例在达梦上：锁子句写在整条语句（含 ROWNUM）的末尾，
/// 只能在事务里、单表查询上用一次。
/// </summary>
public sealed class DamengForUpdateTest : DamengTest
{
    private static Query<SysUser> U1() => Db.Query<SysUser>().Where(u => u.Id == "u1");

    // Java 在事务里执行 firstOrNull 后看捕获的 SQL；C# 渲染同一条语句
    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateSqlAppendInTransaction")]
    public void TestForUpdateSqlAppendInTransaction()
    {
        using var tx = Db.BeginTransaction();
        var sql = U1().ForUpdate().Take(1).ToSql();
        Assert.Equal("SELECT \"ID\",\"NAME\",\"PHONE\",\"AGE\",\"CREATE_TIME\" FROM \"t_sys_user\" WHERE \"ID\" = ? AND ROWNUM < 2 FOR UPDATE", sql);
        RowLockAssert.EndsWithSingleForUpdate(sql);
    }

    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateWithoutTransactionThrows")]
    public void TestForUpdateWithoutTransactionThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => U1().ForUpdate());
        Assert.Contains("BeginTransaction", ex.Message);
    }

    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateJoinNotSupported")]
    public void TestForUpdateJoinNotSupported()
    {
        using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<SysUser>().LeftJoin<SysBankCard>((u, c) => u.Id == c.Uid).ForUpdate());
        Assert.Contains("single-table", ex.Message);
    }

    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateDuplicateCallThrows")]
    public void TestForUpdateDuplicateCallThrows()
    {
        using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => U1().ForUpdate().ForUpdate());
        Assert.Contains("repeated", ex.Message);
    }

    // 两个事务争同一行锁，连实库的达梦（EQS_DAMENG_CS，没有配置时跳过），表建在独立的模式 EQS_UPSTREAM 里。
    // Java 的基类建表后没有写入 u1（锁不到行也就挡不住第二个事务），这里先写入 u1
    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateBlocksConcurrentTransaction")]
    public async Task TestForUpdateBlocksConcurrentTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        const string schema = "EQS_UPSTREAM";
        var dameng = TestDatabases.Dameng;
        await using (var server = await dameng.OpenAsync(ct))
        await using (var command = server.CreateCommand())
        {
            command.CommandText = $"SELECT 1 FROM SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = '{schema}'";
            if (await command.ExecuteScalarAsync(ct) is null)
            {
                command.CommandText = $"CREATE SCHEMA {schema}";
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        var db = new EasyDb(o =>
        {
            o.WithJavaNulls();
            o.UseDameng(dameng.CreateDataSource(schema)).AddListener(SqlCapture.Instance);
            o.NameConversion = NameConversion.UpperUnderlined;
        });
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(SysUser)], ct);
        await db.CodeFirst.CreateTablesAsync([typeof(SysUser)], ct);
        await db.Insert(new SysUser { Id = "u1", Name = "u1" }).ExecuteAsync(ct);

        await RowLockAssert.SecondTransactionWaitsAsync(db, () => db.Query<SysUser>().Where(u => u.Id == "u1").ForUpdate().FirstOrDefaultAsync(ct));
    }

    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery")]
    public void TestForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery()
    {
        using var tx = Db.BeginTransaction();
        var existsSql = Db.Query<SysUser>().Where(u => Sql.Exists(Db.Query<SysBankCard>().Where(c => c.Uid == u.Id))).ForUpdate().ToSql();
        RowLockAssert.EndsWithSingleForUpdate(existsSql);

        var uids = Db.Query<SysBankCard>().Select(c => c.Uid);
        var inSql = Db.Query<SysUser>().Where(u => u.Id.In(uids)).ForUpdate().ToSql();
        RowLockAssert.EndsWithSingleForUpdate(inSql);
    }

    // Java 的 cloneQueryable().selectCount() / limit(1).select(" 1 ")：C# 的计数语句与只取一行的常量投影
    [Fact, Upstream("dameng/DamengForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent")]
    public void TestForUpdateDerivedCountAndExistsSqlBehaviorConsistent()
    {
        using var tx = Db.BeginTransaction();
        var locked = U1().ForUpdate();
        var countSql = locked.ToCountStatement().Text;
        var existsSql = locked.Take(1).Select(_ => Sql.Raw<int>(" 1 ")).ToSql();
        Assert.Equal("SELECT COUNT(*) FROM \"t_sys_user\" WHERE \"ID\" = ? FOR UPDATE", countSql);
        Assert.Equal("SELECT  1  FROM \"t_sys_user\" t WHERE t.\"ID\" = ? AND ROWNUM < 2 FOR UPDATE", existsSql);
        RowLockAssert.EndsWithSingleForUpdate(countSql);
        RowLockAssert.EndsWithSingleForUpdate(existsSql);
    }
}
