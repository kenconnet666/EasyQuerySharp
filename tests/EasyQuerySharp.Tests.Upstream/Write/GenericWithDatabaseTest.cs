using EasyQuerySharp.Commands;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：GenericWithDatabaseTest.java（写入的列策略、换表名 / schema、自增自减、各种拒绝执行的情况）。
/// 大部分用例故意写到不存在的表，只为拿到出错的 SQL；驱动给出的错误消息与 JDBC 不同，只比对 SQL。
/// </summary>
public sealed class GenericWithDatabaseTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string JavaContext = "测的是 Java 表达式上下文（ExpressionContext）的克隆细节；C# 的查询状态是不可变 record，派生查询天然保留全部设置";

    private static async Task<string> FailedSql(Func<Task> action) =>
        (await Assert.ThrowsAsync<EasyQueryException>(action)).Sql!;

    private static QueryLargeColumnTestEntity Large(string? name = null, string? content = null) => new() { Id = "123", Name = name, Content = content };

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest1")]
    public void QueryLargeColumnTest1() =>
        Assert.Equal("SELECT `id`,`name`,`content` FROM `query_large_column_test`", Db.Query<QueryLargeColumnTestEntity>().ToSql());

    // 一列都不写：执行前就拒绝
    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest3")]
    public async Task QueryLargeColumnTest3() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Insert(new QueryLargeColumnTestEntity()).ExecuteAsync(Ct));

    // 插入默认只写非 null 列（Java 的 DEFAULT 同样如此）
    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest4"), Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest5")]
    public async Task QueryLargeColumnTest4() =>
        Assert.Equal("INSERT INTO `query_large_column_test` (`id`) VALUES (?)", await FailedSql(() => Db.Insert(Large()).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest6")]
    public async Task QueryLargeColumnTest6() =>
        Assert.Equal("INSERT INTO `query_large_column_test` (`id`,`name`,`content`) VALUES (?,?,?)",
            await FailedSql(() => Db.Insert(Large()).Columns(ColumnStrategy.All).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest7")]
    public async Task QueryLargeColumnTest7() =>
        Assert.Equal("INSERT INTO `query_large_column_test` (`name`,`content`) VALUES (?,?)",
            await FailedSql(() => Db.Insert(Large()).Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest8")]
    public async Task QueryLargeColumnTest8() =>
        Assert.Equal("UPDATE `query_large_column_test` SET `name` = ?,`content` = ? WHERE `id` = ?", await FailedSql(() => Db.Update(Large()).ExecuteAsync(Ct)));

    // 按列策略筛完没有要写的列：不发语句，0 行
    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest9")]
    public async Task QueryLargeColumnTest9()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal(0, await Db.Update(Large()).Columns(ColumnStrategy.OnlyNotNull).ExecuteAsync(Ct));
        Assert.Empty(listen.Events);
    }

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest10")]
    public async Task QueryLargeColumnTest10() =>
        Assert.Equal("UPDATE `query_large_column_test` SET `name` = ? WHERE `id` = ?",
            await FailedSql(() => Db.Update(Large("123")).Columns(ColumnStrategy.OnlyNotNull).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest11")]
    public async Task QueryLargeColumnTest11() =>
        Assert.Equal("UPDATE `query_large_column_test` SET `content` = ? WHERE `id` = ?",
            await FailedSql(() => Db.Update(Large("123")).Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest12")]
    public async Task QueryLargeColumnTest12() =>
        Assert.Equal(0, await Db.Update(Large("123", "123")).Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct));

    [Fact, Upstream("GenericWithDatabaseTest.java#deleteTest13")]
    public async Task DeleteTest13() =>
        Assert.Equal("DELETE FROM `query_large_column_test` WHERE `id` = ?", await FailedSql(() => Db.Delete(Large()).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#deleteTest14")]
    public async Task DeleteTest14() =>
        Assert.Equal("DELETE FROM `aaa` WHERE `id` = ?", await FailedSql(() => Db.Delete(Large()).AsTable("aaa").ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#deleteTest15"), Upstream("GenericWithDatabaseTest.java#deleteTest16")]
    public async Task DeleteTest15()
    {
        Assert.Equal("DELETE FROM `xxx`.`query_large_column_testaaa` WHERE `id` = ?",
            await FailedSql(() => Db.Delete(Large()).AsTable(o => o + "aaa").AsSchema("xxx").ExecuteAsync(Ct)));
        Assert.Equal("DELETE FROM `xxx`.`query_large_column_testaaa` WHERE `id` = ?",
            await FailedSql(() => Db.Delete(Large()).AsTable(o => o + "aaa").AsSchema(_ => "xxx").ExecuteAsync(Ct)));
    }

    // 不允许物理删除时拒绝执行
    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest14")]
    public async Task QueryLargeColumnTest14() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Delete(Large()).AllowDeleteStatement(false).ExecuteAsync(Ct));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest15")]
    public void QueryLargeColumnTest15() =>
        Assert.Equal("DELETE FROM `query_large_column_test` WHERE `id` = ?", Db.Delete(Large()).ToSql());

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest19")]
    public async Task QueryLargeColumnTest19() =>
        Assert.Equal("UPDATE `xxx`.`abc` SET `content` = ? WHERE `id` = ?",
            await FailedSql(() => Db.Update(Large("123")).AsTable("abc").AsSchema("xxx").Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest20")]
    public async Task QueryLargeColumnTest20() =>
        Assert.Equal("UPDATE `xcv`.`query_large_column_testabc` SET `content` = ? WHERE `id` = ?",
            await FailedSql(() => Db.Update(Large("123")).AsTable(o => o + "abc").AsSchema("xcv").Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest21")]
    public void QueryLargeColumnTest21() =>
        Assert.Throws<ArgumentException>(() => Db.Update(Large("123")).AsTable(""));

    // 按条件更新没有条件：拒绝执行
    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest22")]
    public async Task QueryLargeColumnTest22() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Update<QueryLargeColumnTestEntity>().AsTable(o => o + "abc").AsSchema("xcv")
            .Set(q => q.Id, "123").Set(q => q.Name, "123").Set(q => q.Content, "123").ExecuteAsync(Ct));

    [Fact, Upstream("GenericWithDatabaseTest.java#queryLargeColumnTest23")]
    public async Task QueryLargeColumnTest23() =>
        Assert.Equal("UPDATE `query_large_column_test` SET `id` = ?,`name` = ?,`content` = ? WHERE `id` = ?",
            await FailedSql(() => Db.Update<QueryLargeColumnTestEntity>().Set(q => q.Id, "123").Set(q => q.Name, "123").Set(q => q.Content, "123")
                .WhereById("123").ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#updateTest24"), Upstream("GenericWithDatabaseTest.java#updateTest25")]
    public async Task UpdateTest24()
    {
        Assert.Equal("UPDATE `x_t_blog` SET `star` = `star` + ? WHERE `deleted` = ? AND `id` = ?",
            await FailedSql(() => Db.Update<BlogEntity>().AsTable("x_t_blog").Increment(t => t.Star, 1).WhereById("123").ExecuteAsync(Ct)));
        Assert.Equal("UPDATE `x_t_blog` SET `star` = `star` + ? WHERE `deleted` = ? AND `id` = ?",
            await FailedSql(() => Db.Update<BlogEntity>().AsTable("x_t_blog").Increment(t => t.Star, 2).WhereById("123").ExecuteAsync(Ct)));
    }

    // Java 的 setColumns(false, …) 不生效：按条件追加
    [Fact, Upstream("GenericWithDatabaseTest.java#updateTest26")]
    public async Task UpdateTest26() =>
        Assert.Equal("UPDATE `x_t_blog` SET `score` = `score` + ? WHERE `deleted` = ? AND `id` = ?",
            await FailedSql(() => Db.Update<BlogEntity>().AsTable("x_t_blog").IncrementIf(false, t => t.Star, 2).Increment(t => t.Score, 2m).WhereById("123").ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#updateTest27")]
    public async Task UpdateTest27() =>
        Assert.Equal("UPDATE `x_t_blog` SET `star` = `star` - ? WHERE `deleted` = ? AND `id` = ?",
            await FailedSql(() => Db.Update<BlogEntity>().AsTable("x_t_blog").Decrement(t => t.Star, 2).WhereById("123").ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#updateTest28")]
    public async Task UpdateTest28() =>
        Assert.Equal("UPDATE `x_t_blog` SET `star` = `star` + ? WHERE `deleted` = ? AND `id` = ?",
            await FailedSql(() => Db.Update<BlogEntity>().AsTable("x_t_blog").IncrementIf(false, t => t.Status, 1).IncrementIf(true, t => t.Star, 2).WhereById("123").ExecuteAsync(Ct)));

    [Fact, Upstream("GenericWithDatabaseTest.java#updateTest29")]
    public async Task UpdateTest29() =>
        Assert.Equal("UPDATE `t_unknown` SET `money` = `money` + ? WHERE `id` = ?",
            await FailedSql(() => Db.Update<UnknownTable>().IncrementIf(false, u => u.Money1, 2L).IncrementIf(true, u => u.Money, 2L).WhereById("123").ExecuteAsync(Ct)));

    // 没有主键的实体不能按主键定位
    [Fact, Upstream("GenericWithDatabaseTest.java#updateTest30")]
    public void UpdateTest30() =>
        Assert.Throws<InvalidOperationException>(() => Db.Update<NoKeyEntity>().Set(n => n.Name, "123").WhereById("123"));

    // C# 里按名注册的只有拦截器与逻辑删除策略（加密、主键生成在特性上按类型引用，没有注册表），重复注册同样拒绝
    [Fact, Upstream("GenericWithDatabaseTest.java#repeatApply1", Deviation = "只有拦截器与自定义逻辑删除策略按名注册；加密与主键生成策略在特性上按类型引用，不存在重复注册")]
    public void RepeatApply1()
    {
        var options = new EasyDbOptions().AddInterceptor(new MyEntityInterceptor());
        Assert.Throws<InvalidOperationException>(() => options.AddInterceptor(new MyEntityInterceptor()));
        options.AddLogicDeleteStrategy(nameof(MyLogicDelStrategy), new MyLogicDelStrategy());
        Assert.Throws<InvalidOperationException>(() => options.AddLogicDeleteStrategy(nameof(MyLogicDelStrategy), new MyLogicDelStrategy()));
    }

    [Fact, Upstream("GenericWithDatabaseTest.java#testPrintSQL1", NotApplicable = JavaContext)] public void TestPrintSQL1() { }

    [Fact, Upstream("GenericWithDatabaseTest.java#testPrintSQL2", NotApplicable = JavaContext)] public void TestPrintSQL2() { }

    [Fact, Upstream("GenericWithDatabaseTest.java#testQueryLockClonePropagation", NotApplicable = JavaContext)] public void TestQueryLockClonePropagation() { }

    [Fact, Upstream("GenericWithDatabaseTest.java#testQueryLockExtendFromPropagation", NotApplicable = JavaContext)] public void TestQueryLockExtendFromPropagation() { }
}
