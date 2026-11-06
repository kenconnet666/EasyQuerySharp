using EasyQuerySharp.Encryption;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>对应 Java：Sm4EncryptionTest.java（国密 SM4 + 十六进制的分段加密：写入、解密读回、密文上的 LIKE、更新）。</summary>
public sealed class Sm4EncryptionTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    /// <summary>对应 Java：encryption/DefaultSafeSm4EasyEncryptionStrategy。</summary>
    public sealed class DefaultSafeSm4EasyEncryptionStrategy : Sm4HexEncryptionStrategy
    {
        protected override string Key => "sm4key1234567890";
    }

    /// <summary>对应 Java：entity/SysUserSm4Encryption。</summary>
    [Table("t_sys_user_sm4_encryption")]
    public sealed class SysUserSm4Encryption
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        public string? Name { get; set; }

        [Encryption(typeof(DefaultSafeSm4EasyEncryptionStrategy), SupportQueryLike = true), Column(DbType = "varchar(1024)")]
        public string? Phone { get; set; }

        [Encryption(typeof(DefaultSafeSm4EasyEncryptionStrategy), SupportQueryLike = true), Column(DbType = "varchar(2048)")]
        public string? Address { get; set; }
    }

    private async Task<string?> RawColumn(string column) =>
        (await Db.Table("t_sys_user_sm4_encryption").Where(r => r.Get<string>("id") == "1").SelectColumns(column).FirstOrDefaultAsync(Ct))!.Get<string>(column);

    private Task<SysUserSm4Encryption?> First(System.Linq.Expressions.Expression<Func<SysUserSm4Encryption, bool>> predicate) =>
        Db.Query<SysUserSm4Encryption>().Where(predicate).FirstOrDefaultAsync(Ct);

    [Fact, Upstream("Sm4EncryptionTest.java#sm4Test")]
    public async Task Sm4Test()
    {
        await Db.CodeFirst.CreateTablesAsync([typeof(SysUserSm4Encryption)], Ct);
        await Db.Delete<SysUserSm4Encryption>().WhereById("1").ExecuteAsync(Ct);
        var user = new SysUserSm4Encryption { Id = "1", Name = "name1", Phone = "12345678901", Address = "浙江省绍兴市越城区城市广场旁边2-102" };
        Assert.Equal(1, await Db.Insert(user).ExecuteAsync(Ct));

        // 库里存的是 32 个字符一组的小写十六进制
        var dbPhone = await RawColumn("phone");
        Assert.Matches("^[0-9a-f]+$", dbPhone);
        Assert.Equal(0, dbPhone!.Length % 32);
        Assert.Matches("^[0-9a-f]+$", await RawColumn("address"));

        var read = await First(o => o.Id == "1");
        Assert.Equal((user.Id, user.Phone, user.Address), (read!.Id, read.Phone, read.Address));
        Assert.Equal("1", (await First(o => o.Phone == "12345678901"))!.Id);
        Assert.NotNull(await First(o => o.Phone!.Contains("34567")));
        Assert.NotNull(await First(o => o.Phone!.LikeContains("34567")));
        Assert.NotNull(await First(o => o.Address!.Contains("2-102")));
        Assert.NotNull(await First(o => o.Address!.EndsWith("2-102")));
        Assert.NotNull(await First(o => o.Address!.StartsWith("浙江省绍兴市")));
        var shaoxing = await First(o => o.Address!.Contains("绍兴"));
        Assert.NotNull(shaoxing);
        // 中文单字不够一段，查不到
        Assert.Null(await First(o => o.Address!.Contains("绍")));

        shaoxing!.Phone = "13232323321";
        Assert.Equal(1, await Db.Update(shaoxing).ExecuteAsync(Ct));
        Assert.NotNull(await First(o => o.Phone!.Contains("23233")));

        Assert.Equal(1, await Db.Update<SysUserSm4Encryption>().Set(s => s.Phone, "19876543210").Where(o => o.Id == "1").ExecuteAsync(Ct));
        Assert.NotNull(await First(o => o.Phone!.Contains("9876")));
        Assert.Null(await First(o => o.Phone!.Contains("987")));
        Assert.Equal("19876543210", await Db.Query<SysUserSm4Encryption>().WhereById("1").Select(s => s.Phone).SingleAsync(Ct));
        var list = await Db.Query<SysUserSm4Encryption>().Where(o => o.Phone!.StartsWith("19876")).ToListAsync(Ct);
        Assert.Equal("19876543210", Assert.Single(list).Phone);
    }
}
