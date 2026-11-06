namespace EasyQuerySharp.Tests.Live;

public abstract partial class LiveSuite
{
    // 值对象在库里是普通的列：写入、按成员过滤、整体投影、判空、部分更新都在各库上往返一遍
    [Fact]
    public async Task RoundTripsValueObjects()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(Shop)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(Shop)], Ct);
        await db.Insert(
            new Shop { Name = "a", Address = new ShopAddress { Province = "zj", City = "hz", Street = "x", Geo = new GeoPoint { Lat = 30.25, Lng = 120.5 } }, Contact = new ShopContact { Phone = "1" } },
            new Shop { Name = "b", Address = new ShopAddress { Province = "js", City = "nj", Geo = new GeoPoint { Lat = 32, Lng = 118.75 } } }).ExecuteAsync(Ct);

        var a = await db.Query<Shop>().Where(s => s.Address.City == "hz").SingleAsync(Ct);
        Assert.Equal(("zj", "x", 30.25, 120.5, "1"), (a.Address.Province, a.Address.Street, a.Address.Geo.Lat, a.Address.Geo.Lng, a.Contact?.Phone));
        // 列全为 NULL 的可空值对象读成 null
        Assert.Null((await db.Query<Shop>().Where(s => s.Name == "b").SingleAsync(Ct)).Contact);
        Assert.Equal(["b"], await db.Query<Shop>().Where(s => s.Contact == null).Select(s => s.Name).ToListAsync(Ct));

        var projected = await db.Query<Shop>().OrderBy(s => s.Name).Select(s => new { s.Name, s.Address.Geo, s.Contact }).ToListAsync(Ct);
        Assert.Equal(118.75, projected[1].Geo.Lng);
        Assert.Null(projected[1].Contact);

        await db.Update<Shop>().Set(s => new Shop { Address = new ShopAddress { City = "sz" }, Contact = null }).Where(s => s.Name == "a").ExecuteAsync(Ct);
        var updated = await db.Query<Shop>().Where(s => s.Name == "a").SingleAsync(Ct);
        Assert.Equal(("zj", "sz"), (updated.Address.Province, updated.Address.City));
        Assert.Null(updated.Contact);
    }
}
