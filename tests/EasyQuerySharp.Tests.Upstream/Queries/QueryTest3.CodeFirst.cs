namespace EasyQuerySharp.Tests.Upstream.Queries;

// QueryTest3.java 里先 syncTable 再读写的用例
public sealed partial class QueryTest3
{
    // 同步建 user_book_encrypt，写入两个用户和各自的书，按用户地址 LIKE 联表查询：手机号由库端解密读出
    [Fact, Upstream("QueryTest3.java#userBookTest",
         Deviation = "C# 的 SysUserEncrypt 地址列没有 Java 端加密（Java 用 JavaEncryptionStrategy，VO 上再声明一次解密），按明文写入、LIKE 与读出；"
                     + "Java 只 syncTable 了 UserBookEncrypt，这里连 SysUserEncrypt 一起同步，用例不依赖别的用例先建表")]
    public async Task UserBookTest()
    {
        await Db.CodeFirst.SyncTablesAsync([typeof(UserBookEncrypt), typeof(SysUserEncrypt)], Ct);
        await CleanUserBooksAsync();
        try
        {
            await Db.Insert(new SysUserEncrypt { Id = "1", Name = "用户1", Phone = "12345678901", Address = "浙江省绍兴市越城区城市广场1234号", CreateTime = DateTime.Now }).ExecuteAsync(Ct);
            await Db.Insert(new[] { new UserBookEncrypt { Id = "1", UserId = "1", Name = "语文" }, new UserBookEncrypt { Id = "2", UserId = "1", Name = "数学" } }).ExecuteAsync(Ct);
            await Db.Insert(new SysUserEncrypt { Id = "2", Name = "用户2", Phone = "19012345678", Address = "浙江省杭州市上城区武林广场1234号", CreateTime = DateTime.Now }).ExecuteAsync(Ct);
            await Db.Insert(new[] { new UserBookEncrypt { Id = "3", UserId = "2", Name = "语文" }, new UserBookEncrypt { Id = "4", UserId = "2", Name = "英语" } }).ExecuteAsync(Ct);

            var userBooks = await Db.Query<UserBookEncrypt>().LeftJoin<SysUserEncrypt>((t, t1) => t.UserId == t1.Id)
                .Where((t, t1) => t1.Address!.Contains("越城区"))
                .Select((t, t1) => new UserBookEncryptVO { Id = t.Id, UserId = t.UserId, Name = t.Name, UserName = t1.Name, UserPhone = t1.Phone, UserAddress = t1.Address })
                .ToListAsync(Ct);
            Assert.Equal(2, userBooks.Count);
            Assert.All(userBooks, b => Assert.Equal(("12345678901", "浙江省绍兴市越城区城市广场1234号"), (b.UserPhone, b.UserAddress)));
        }
        finally
        {
            await CleanUserBooksAsync();
        }
    }

    private async Task CleanUserBooksAsync()
    {
        await Db.Delete<SysUserEncrypt>().DisableLogicDelete().Where(s => s.Id == "1" || s.Id == "2").ExecuteAsync(Ct);
        await Db.Delete<UserBookEncrypt>().DisableLogicDelete().Where(b => b.Id == "1" || b.Id == "2" || b.Id == "3" || b.Id == "4").ExecuteAsync(Ct);
    }
}
