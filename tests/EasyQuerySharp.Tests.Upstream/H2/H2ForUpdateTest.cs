namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/H2ForUpdateTest.java（行锁）。调用 ForUpdate 时的三项检查（要在事务里、只能单表、不能重复）与方言无关，在 SQLite 上移植；
/// 其余用例断言 H2 渲染出的 FOR UPDATE 与真实的行锁等待，SQLite 没有行锁语法（C# 在渲染时拒绝 FOR UPDATE），
/// 同样的用例已在 mysql8/ForUpdateTest、pgsql/PgSQLForUpdateTest 上移植。
/// </summary>
public sealed class H2ForUpdateTest(H2Fixture fixture) : H2Test(fixture)
{
    private const string NoRowLock = "测 H2 渲染的 FOR UPDATE 与行锁；SQLite 没有行锁语法，同样的用例在 mysql8/ForUpdateTest、pgsql/PgSQLForUpdateTest 已移植";

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateSqlAppendInTransaction", NotApplicable = NoRowLock)]
    public void TestForUpdateSqlAppendInTransaction()
    {
    }

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateWithoutTransactionThrows")]
    public void TestForUpdateWithoutTransactionThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<DefTable>().Where(t => t.Id == "0").ForUpdate());
        Assert.Contains("BeginTransaction", ex.Message);
    }

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateJoinNotSupported")]
    public async Task TestForUpdateJoinNotSupported()
    {
        await using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).ForUpdate());
        Assert.Contains("single-table", ex.Message);
    }

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateDuplicateCallThrows")]
    public async Task TestForUpdateDuplicateCallThrows()
    {
        await using var tx = Db.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<DefTable>().Where(t => t.Id == "0").ForUpdate().ForUpdate());
        Assert.Contains("repeated", ex.Message);
    }

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateBlocksConcurrentTransaction", NotApplicable = NoRowLock)]
    public void TestForUpdateBlocksConcurrentTransaction()
    {
    }

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery", NotApplicable = NoRowLock)]
    public void TestForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery()
    {
    }

    [Fact, Upstream("h2/H2ForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent", NotApplicable = NoRowLock)]
    public void TestForUpdateDerivedCountAndExistsSqlBehaviorConsistent()
    {
    }
}
