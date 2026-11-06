namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/H2OrderTest.java（按 id 取模分表的 t_order：IN 条件路由到几张分表、跨表合并后倒序）。
/// 依赖分库分表（ROADMAP 4.4），先留骨架。
/// </summary>
public sealed class H2OrderTest
{
    // Java：easyEntityQuery2.queryable(H2Order.class).where(o -> o.id().in(1, 2, 6, 7)).toList()，断言 4 行
    [Fact(Skip = "等 4.4 分库分表"), Upstream("h2/H2OrderTest.java#test1", Pending = true)]
    public Task Test1() => Task.CompletedTask;

    // Java：同上再 orderBy(id desc)，断言 7、6、2、1
    [Fact(Skip = "等 4.4 分库分表"), Upstream("h2/H2OrderTest.java#test2", Pending = true)]
    public Task Test2() => Task.CompletedTask;
}
