namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>对应 Java：TrackTest.java（追踪作用域里按差异更新加密列；不在作用域里不能追踪）。</summary>
public sealed class TrackTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    // 删掉旧行再插一条新的，返回插入的实体
    private async Task<SysUserTrack> Reset(string id)
    {
        await Db.Delete<SysUserTrack>().WhereById(id).ExecuteAsync(Ct);
        var user = new SysUserTrack
        {
            Id = id, Username = "Username1", Phone = "13232323232", IdCard = "123456789000", Address = "浙江省绍兴市越城区城市广场", CreateTime = DateTime.Now,
        };
        Assert.Equal(1, await Db.Insert(user).ExecuteAsync(Ct));
        return user;
    }

    private async Task<string?> PhoneOf(string id) => (await Db.Query<SysUserTrack>().WhereById(id).FirstOrDefaultAsync(Ct))!.Phone;

    // 追踪查询出来的实体再显式追踪一次仍然成功；只改了手机号，按差异更新一行
    [Fact, Upstream("TrackTest.java#track1")]
    public async Task Track1()
    {
        await Reset("1");
        using (Db.BeginTracking())
        {
            var user = await Db.Query<SysUserTrack>().AsTracking().WhereById("1").FirstOrDefaultAsync(Ct);
            Assert.True(Db.AddTracking(user!));
            user!.Phone = "9876543210";
            Assert.Equal(1, await Db.Update(user).ExecuteAsync(Ct));
            Assert.Equal("9876543210", await PhoneOf("1"));
        }
    }

    // 普通查询出来的实体显式加入追踪
    [Fact, Upstream("TrackTest.java#track2")]
    public async Task Track2()
    {
        await Reset("2");
        using (Db.BeginTracking())
        {
            var user = await Db.Query<SysUserTrack>().WhereById("2").FirstOrDefaultAsync(Ct);
            Assert.True(Db.AddTracking(user!));
            user!.Phone = "9876543210";
            Assert.Equal(1, await Db.Update(user).ExecuteAsync(Ct));
            Assert.Equal("9876543210", await PhoneOf("2"));
        }
    }

    // 不在追踪作用域里：追踪不了
    [Fact, Upstream("TrackTest.java#track3")]
    public async Task Track3()
    {
        await Reset("3");
        var user = await Db.Query<SysUserTrack>().WhereById("3").FirstOrDefaultAsync(Ct);
        Assert.False(Db.AddTracking(user!));
    }

    // 追踪的实体没有改动：不发 UPDATE，影响 0 行
    [Fact, Upstream("TrackTest.java#track4")]
    public async Task Track4()
    {
        await Reset("5");
        using (Db.BeginTracking())
        {
            var user = await Db.Query<SysUserTrack>().AsTracking().WhereById("5").FirstOrDefaultAsync(Ct);
            Assert.True(Db.AddTracking(user!));
            Assert.Equal(0, await Db.Update(user!).ExecuteAsync(Ct));
        }
    }

    // 作用域里但实体没有追踪：整行更新
    [Fact, Upstream("TrackTest.java#track5")]
    public async Task Track5()
    {
        await Reset("6");
        using (Db.BeginTracking())
        {
            var user = await Db.Query<SysUserTrack>().WhereById("6").FirstOrDefaultAsync(Ct);
            user!.Phone = "9876543210";
            Assert.Equal(1, await Db.Update(user).ExecuteAsync(Ct));
            Assert.Equal("9876543210", await PhoneOf("6"));
        }
    }
}
