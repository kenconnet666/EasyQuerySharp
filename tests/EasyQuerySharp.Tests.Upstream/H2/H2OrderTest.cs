namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/H2OrderTest.java（按 id 取模分成 5 张表的 t_order：IN 条件路由到几张分表、跨表合并后倒序）。
/// 改在 SQLite 上跑（见 <see cref="H2Fixture"/>），与 Java 的分片执行器一样每张表一条语句、在内存里归并。
/// </summary>
public sealed class H2OrderTest(H2Fixture fixture) : H2Test(fixture)
{
    private static readonly int?[] Ids = [1, 2, 6, 7];

    // 1、6 落在 t_order_4，2、7 落在 t_order_0（Java 的 String.hashCode 取模），其余三张表不查
    private static readonly string[] Routed =
    [
        "SELECT id,status,created FROM t_order_0 WHERE id IN (?,?,?,?)",
        "SELECT id,status,created FROM t_order_4 WHERE id IN (?,?,?,?)",
    ];

    [Fact, Upstream("h2/H2OrderTest.java#test1")]
    public async Task Test1()
    {
        using var listen = SqlCapture.Listen();
        var list = await OrderDb.Query<H2Order>().Where(o => Ids.Contains(o.Id)).ToListAsync(Ct);
        Assert.Equal(4, list.Count);
        Assert.Equal(Routed, listen.Events.Select(e => e.Statement.Text).Order());
    }

    [Fact, Upstream("h2/H2OrderTest.java#test2")]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        var list = await OrderDb.Query<H2Order>().Where(o => Ids.Contains(o.Id)).OrderByDescending(o => o.Id).ToListAsync(Ct);
        Assert.Equal([7, 6, 2, 1], list.Select(o => o.Id));
        Assert.Equal(Routed.Select(sql => sql + " ORDER BY id DESC"), listen.Events.Select(e => e.Statement.Text).Order());
    }
}
