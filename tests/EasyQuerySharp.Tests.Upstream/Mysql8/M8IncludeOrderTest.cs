namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8IncludeOrderTest.java（带出的子数据按导航声明的排序）。</summary>
public sealed class M8IncludeOrderTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // 一对多：导航上声明 OrderBy = Order
    [Fact, Upstream("mysql8/M8IncludeOrderTest.java#testOrder")]
    public async Task TestOrder()
    {
        var list = await Db.Query<M8Parent>().Include(m => m.Children).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, p => Assert.Equal(p.Children!.Select(c => c.Order).Order(), p.Children!.Select(c => c.Order)));
    }

    // 多对多：同样按导航声明的排序
    [Fact, Upstream("mysql8/M8IncludeOrderTest.java#testOrde2r")]
    public async Task TestOrde2r()
    {
        var list = await Db.Query<M8Parent>().Include(m => m.Children2).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, p => Assert.Equal(p.Children2!.Select(c => c.Order).Order(), p.Children2!.Select(c => c.Order)));
    }
}
