namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：InsertTest / UpdateTest 里 SysUserSQLEncryption 的用例（列的 SQL 读写转换：手机号写入时 <c>to_base64(AES_ENCRYPT(?,?))</c>，
/// 查询时 <c>AES_DECRYPT(from_base64(列),?)</c>，条件里与它比较的值同样加密）。
/// </summary>
public sealed class SqlConversionTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static async Task<SqlStatement> Failed(Func<Task> action) =>
        (await Assert.ThrowsAsync<EasyQueryException>(action)).Statement!;

    private static SysUserSQLEncryption User(string id = "123", DateTime? createTime = null) => new()
    {
        Id = id, Username = "username", Phone = "13232456789", IdCard = "12345678", Address = "xxxxxxx", CreateTime = createTime ?? DateTime.Now,
    };

    [Fact, Upstream("InsertTest.java#insertSQLConvert1")]
    public async Task InsertSQLConvert1() =>
        Assert.Equal("INSERT INTO `xxxxx` (`id`,`username`,`phone`,`id_card`,`address`,`create_time`) VALUES (?,?,to_base64(AES_ENCRYPT(?,?)),?,?,?)",
            (await Failed(() => Db.Insert(User()).AsTable("xxxxx").ExecuteAsync(Ct))).Text);

    // 7 个参数：6 列的值加上密钥
    [Fact, Upstream("InsertTest.java#insertSQLConvert2")]
    public async Task InsertSQLConvert2()
    {
        var now = DateTime.Now;
        var parameters = (await Failed(() => Db.Insert(User(createTime: now)).AsTable("xxxxx").ExecuteAsync(Ct))).Parameters;
        Assert.Equal(["123", "username", "13232456789", MySqlAesEncryptConversion.Secret, "12345678", "xxxxxxx", now], parameters.Select(p => p.Value));
    }

    // 写入、读回（解密）、按实体更新、按条件更新，再按加密后的值查回来
    [Fact, Upstream("InsertTest.java#insertSQLConvert3")]
    public async Task InsertSQLConvert3()
    {
        await Db.Delete<SysUserSQLEncryption>().WhereById("12345").ExecuteAsync(Ct);
        var user = User("12345");
        Assert.Equal(1, await Db.Insert(user).ExecuteAsync(Ct));

        var read = await Db.Query<SysUserSQLEncryption>().WhereById("12345").FirstAsync(Ct);
        Assert.Equal(("12345", "username", "13232456789", "12345678", "xxxxxxx"), (read.Id, read.Username, read.Phone, read.IdCard, read.Address));
        // DATETIME 存到秒（MySQL 四舍五入小数秒），按分钟比较在整分附近会跨分钟，改为允许 1 秒误差
        Assert.InRange((read.CreateTime!.Value - user.CreateTime!.Value).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(1));

        read.Phone = "111123456";
        Assert.Equal(1, await Db.Update(read).ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Update<SysUserSQLEncryption>().Set(s => s.Phone, "1111234").WhereById("12345").ExecuteAsync(Ct));

        var found = await Db.Query<SysUserSQLEncryption>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).Where((t1, t2) => t1.Phone == "1111234")
            .Select((t1, t2) => t1).FirstOrDefaultAsync(Ct);
        Assert.Equal("1111234", found!.Phone);
        await Db.Delete<SysUserSQLEncryption>().WhereById("12345").ExecuteAsync(Ct);
    }

    [Fact, Upstream("UpdateTest.java#updateTest25"), Upstream("UpdateTest.java#updateTest25_1")]
    public async Task UpdateTest25() =>
        Assert.Equal("UPDATE `xxxxx` SET `username` = ?,`phone` = to_base64(AES_ENCRYPT(?,?)),`id_card` = ?,`address` = ?,`create_time` = ? WHERE `id` = ?",
            (await Failed(() => Db.Update(User()).AsTable("xxxxx").ExecuteAsync(Ct))).Text);

    [Fact, Upstream("UpdateTest.java#updateTest27"), Upstream("UpdateTest.java#updateTest27_1")]
    public async Task UpdateTest27() =>
        Assert.Equal("UPDATE `x123` SET `phone` = to_base64(AES_ENCRYPT(?,?)) WHERE `id` = ?",
            (await Failed(() => Db.Update<SysUserSQLEncryption>().AsTable("x123").Set(s => s.Phone, "123123").Where(o => o.Id == "7").ExecuteAsync(Ct))).Text);
}
