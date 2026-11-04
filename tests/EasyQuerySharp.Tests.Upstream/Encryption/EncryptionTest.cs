using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream.Encryption;

/// <summary>对应 Java：EncryptionTest.java。SysUser 的 phone / idCard / address 加密存储，读出解密，LIKE 作用在密文上。</summary>
public sealed class EncryptionTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static void AssertPlain(SysUser user)
    {
        Assert.Equal("18888888" + user.Id + user.Id, user.Phone);
        Assert.Equal("333333333333333" + user.Id + user.Id, user.IdCard);
        Assert.Equal(user.Phone + user.IdCard, user.Address);
    }

    [Fact, Upstream("EncryptionTest.java#phoneTest")]
    public async Task PhoneTest() => AssertPlain((await Db.Query<SysUser>().WhereById("1").FirstOrDefaultAsync(Ct))!);

    [Fact, Upstream("EncryptionTest.java#phoneTest1")]
    public async Task PhoneTest1()
    {
        var users = await Db.Query<SysUser>().ToListAsync(Ct);
        Assert.Equal(100, users.Count);
        Assert.All(users, AssertPlain);
    }

    // Java 的 likeMatchRight / likeMatchLeft / like：C# 的 EndsWith / StartsWith / Contains
    [Theory, Upstream("EncryptionTest.java#phoneTest2"), Upstream("EncryptionTest.java#phoneTest3"), Upstream("EncryptionTest.java#phoneTest4")]
    [InlineData("ends", "8800", 1)]
    [InlineData("starts", "188888881", 11)]
    [InlineData("contains", "1888888812", 1)]
    public async Task PhoneLike(string mode, string value, int expected)
    {
        var query = mode switch
        {
            "ends" => Db.Query<SysUser>().Where(o => o.Phone!.EndsWith(value)),
            "starts" => Db.Query<SysUser>().Where(o => o.Phone!.StartsWith(value)),
            _ => Db.Query<SysUser>().Where(o => o.Phone!.Contains(value)),
        };
        var users = await query.ToListAsync(Ct);
        Assert.Equal(expected, users.Count);
        Assert.All(users, AssertPlain);
    }

    // 不支持 LIKE 的加密列：等值能查到，LIKE 用明文查不到
    [Fact, Upstream("EncryptionTest.java#test5")]
    public async Task Test5()
    {
        await Db.Delete<SysUserEncryption>().WhereById("1").ExecuteAsync(Ct);
        var entity = new SysUserEncryption { Id = "1", Name = "name1", PhoneNotSupportLike = "12345678901", AddressNotSupportLike = "浙江省绍兴市越城区城市广场旁边2-102" };
        Assert.Equal(1, await Db.Insert(entity).ExecuteAsync(Ct));
        var read = await Db.Query<SysUserEncryption>().WhereById("1").FirstOrDefaultAsync(Ct);
        Assert.NotNull(read);
        Assert.Equal((entity.Id, entity.PhoneNotSupportLike, entity.AddressNotSupportLike), (read.Id, read.PhoneNotSupportLike, read.AddressNotSupportLike));
        Assert.Equal(read.Id, (await Db.Query<SysUserEncryption>().Where(o => o.PhoneNotSupportLike == "12345678901").FirstOrDefaultAsync(Ct))?.Id);
        Assert.Null(await Db.Query<SysUserEncryption>().Where(o => o.PhoneNotSupportLike!.Contains("12345678901")).FirstOrDefaultAsync(Ct));
    }

    // 支持 LIKE 的加密列：至少 4 个占位（中文算 2）的片段能用 LIKE 查到，更短的查不到
    [Fact, Upstream("EncryptionTest.java#test6")]
    public async Task Test6()
    {
        await Db.Delete<SysUserEncryption>().WhereById("2").ExecuteAsync(Ct);
        var entity = new SysUserEncryption { Id = "2", Name = "name2", PhoneSupportLike = "12345678901", AddressSupportLike = "浙江省绍兴市越城区城市广场旁边2-102" };
        Assert.Equal(1, await Db.Insert(entity).ExecuteAsync(Ct));
        var read = await Db.Query<SysUserEncryption>().WhereById("2").FirstOrDefaultAsync(Ct);
        Assert.NotNull(read);
        Assert.Equal((entity.PhoneSupportLike, entity.AddressSupportLike), (read.PhoneSupportLike, read.AddressSupportLike));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.PhoneSupportLike == "12345678901").FirstOrDefaultAsync(Ct));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.PhoneSupportLike!.Contains("34567")).FirstOrDefaultAsync(Ct));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.AddressSupportLike!.Contains("2-102")).FirstOrDefaultAsync(Ct));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.AddressSupportLike!.EndsWith("2-102")).FirstOrDefaultAsync(Ct));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.AddressSupportLike!.StartsWith("浙江省绍兴市")).FirstOrDefaultAsync(Ct));
        var found = await Db.Query<SysUserEncryption>().Where(o => o.AddressSupportLike!.Contains("绍兴")).FirstOrDefaultAsync(Ct);
        Assert.NotNull(found);
        // 中文单字不够一段，查不到
        Assert.Null(await Db.Query<SysUserEncryption>().Where(o => o.AddressSupportLike!.Contains("绍")).FirstOrDefaultAsync(Ct));

        found.PhoneSupportLike = "13232323321";
        Assert.Equal(1, await Db.Update(found).ExecuteAsync(Ct));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.PhoneSupportLike!.Contains("23233")).FirstOrDefaultAsync(Ct));
        Assert.Equal(1, await Db.Update<SysUserEncryption>().Set(s => s.PhoneSupportLike, "19876543210").Where(o => o.Id == "2").ExecuteAsync(Ct));
        Assert.NotNull(await Db.Query<SysUserEncryption>().Where(o => o.PhoneSupportLike!.Contains("9876")).FirstOrDefaultAsync(Ct));
        Assert.Null(await Db.Query<SysUserEncryption>().Where(o => o.PhoneSupportLike!.Contains("987")).FirstOrDefaultAsync(Ct));
        Assert.Equal("19876543210", await Db.Query<SysUserEncryption>().WhereById("2").Select(s => s.PhoneSupportLike).SingleAsync(Ct));
    }
}
