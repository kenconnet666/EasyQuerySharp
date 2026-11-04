using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：VersionTest.java（乐观锁版本列）。条件顺序是 逻辑删除、版本、主键；SET 末尾写下一个版本。
/// 按条件写入必须 <c>WithVersion</c> 或 <c>IgnoreVersion</c>；Java 的 EasyQueryInvalidOperationException / NullPointerException 在 C# 都是 InvalidOperationException。
/// </summary>
public sealed class VersionTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static SysUserVersionLong NewUser(string id) => new()
    {
        Id = id,
        CreateTime = DateTime.Now,
        Version = 1L,
        Username = "username" + id,
        Phone = "13232323232",
        IdCard = "0000000000",
        Address = "浙江省绍兴市越城区城市广场",
    };

    private static SysUserVersionLongLogicDel NewLogicDelUser(string id) => new()
    {
        Id = id,
        CreateTime = DateTime.Now,
        Version = 1L,
        Username = "username" + id,
        Phone = "13232323232",
        IdCard = "0000000000",
        Address = "浙江省绍兴市越城区城市广场",
        Deleted = false,
    };

    [Fact, Upstream("VersionTest.java#test1")]
    public async Task Test1()
    {
        const string id = "1";
        await Db.Delete<SysUserVersionLong>().WhereById(id).IgnoreVersion().ExecuteAsync(Ct);
        Assert.Equal(1, await Db.Insert(NewUser(id)).ExecuteAsync(Ct));
        var user = await Db.Query<SysUserVersionLong>().Where(u => u.Id == id).FirstOrDefaultAsync(Ct);
        Assert.NotNull(user);
        Assert.Equal(1, await Db.Update(user).ExecuteAsync(Ct));
        // 写成功后新版本写回实体，同一个实例可以接着更新
        Assert.Equal(2L, user.Version);
        Assert.Equal(1, await Db.Update(user).ExecuteAsync(Ct));
    }

    [Fact, Upstream("VersionTest.java#test2")]
    public async Task Test2()
    {
        const string id = "2";
        await Db.Delete<SysUserVersionLong>().WhereById(id).IgnoreVersion().ExecuteAsync(Ct);
        Assert.Equal(1, await Db.Insert(NewUser(id)).ExecuteAsync(Ct));

        // 按条件更新没有给版本：报错
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WhereById(id).ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WhereById(id).IgnoreVersion().ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").Where(o => o.Id == id).WithVersion(1L).ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WithVersion(2L).Where(o => o.Id == id).ExecuteAsync(Ct));
        // 版本已是 3，再用旧版本更新不到任何行
        Assert.Equal(0, await Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WithVersion(2L).Where(o => o.Id == id).ExecuteAsync(Ct));
    }

    [Fact, Upstream("VersionTest.java#test3")]
    public void Test3()
    {
        const string id = "2";
        Assert.Equal("UPDATE `t_sys_user_version` SET `phone` = ? WHERE `id` = ?",
            Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WhereById(id).IgnoreVersion().ToSql());
        Assert.Equal("UPDATE `t_sys_user_version` SET `phone` = ? WHERE `id` = ?",
            Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").IgnoreVersion().Where(o => o.Id == id).ToSql());
        Assert.Equal("UPDATE `t_sys_user_version` SET `phone` = ?,`version` = ? WHERE `version` = ? AND `id` = ?",
            Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WithVersion(1L).IgnoreVersion(false).Where(o => o.Id == id).ToSql());
    }

    [Fact, Upstream("VersionTest.java#test3_1")]
    public void Test3_1() =>
        Assert.Equal("UPDATE `t_sys_user_version` SET `phone` = ?,`version` = ? WHERE `version` = ? AND `id` = ?",
            Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WithVersion(true, 1L).IgnoreVersion(false).Where(o => o.Id == "id").ToSql());

    [Fact, Upstream("VersionTest.java#test3_2")]
    public void Test3_2() =>
        Assert.Equal("UPDATE `t_sys_user_version` SET `phone` = ? WHERE `id` = ?",
            Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WithVersion(false, 1L).IgnoreVersion(true).Where(o => o.Id == "id").ToSql());

    [Fact, Upstream("VersionTest.java#test3_3")]
    public void Test3_3()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Db.Update<SysUserVersionLong>().Set(s => s.Phone, "123").WithVersion(false, 1L).IgnoreVersion(false).Where(o => o.Id == "id").ToSql());
        Assert.Contains("SysUserVersionLong 有版本列", ex.Message);
    }

    [Fact, Upstream("VersionTest.java#test4")]
    public async Task Test4()
    {
        const string id = "4";
        await Db.Delete<SysUserVersionLongLogicDel>().WhereById(id).IgnoreVersion().DisableLogicDelete().ExecuteAsync(Ct);
        var user = NewLogicDelUser(id);
        Assert.Equal(1, await Db.Insert(user).ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Delete(user).ExecuteAsync(Ct));
    }

    [Fact, Upstream("VersionTest.java#test5")]
    public async Task Test5()
    {
        const string id = "5";
        await Db.Delete<SysUserVersionLongLogicDel>().WhereById(id).IgnoreVersion().DisableLogicDelete().ExecuteAsync(Ct);
        Assert.Equal(1, await Db.Insert(NewLogicDelUser(id)).ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Delete<SysUserVersionLongLogicDel>().IgnoreVersion(false).WithVersion(1L).WhereById(id).ExecuteAsync(Ct));
    }

    [Fact, Upstream("VersionTest.java#test6")]
    public void Test6() =>
        Assert.Equal("UPDATE `t_sys_user_version_del` SET `deleted` = ?,`version` = ? WHERE `deleted` = ? AND `version` = ? AND `id` = ?",
            Db.Delete<SysUserVersionLongLogicDel>().WithVersion(1L).WhereById("56").ToSql());

    // 表 aaa 不存在，执行失败；异常带着执行的 SQL。生效的逻辑删除列在条件里，不写进 SET
    [Fact, Upstream("VersionTest.java#test7")]
    public async Task Test7()
    {
        var user = new SysUserVersionLongLogicDel { Id = "1x1", Username = "aaaa" };
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Update(user).AsTable("aaa").IgnoreVersion().ExecuteAsync(Ct));
        Assert.Equal("UPDATE `aaa` SET `username` = ?,`phone` = ?,`id_card` = ?,`address` = ?,`create_time` = ? WHERE `deleted` = ? AND `id` = ?", ex.Sql);
    }

    // 实体的版本为 null：无法做版本检查（Java 是 NullPointerException）
    [Fact, Upstream("VersionTest.java#test8")]
    public async Task Test8()
    {
        var user = new SysUserVersionLongLogicDel { Id = "1x1", Username = "aaaa" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Update(user).ExecuteAsync(Ct));
    }

    [Fact, Upstream("VersionTest.java#test9")]
    public async Task Test9()
    {
        var user = new SysUserVersionLongLogicDel { Id = "1x1", Username = "aaaa", Version = 1L };
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Update(user).SetColumns(o => new { o.Username }).AsTable("AXX").ExecuteAsync(Ct));
        Assert.Equal("UPDATE `AXX` SET `username` = ?,`version` = ? WHERE `deleted` = ? AND `version` = ? AND `id` = ?", ex.Sql);
        // 执行失败时不写回新版本
        Assert.Equal(1L, user.Version);
    }

    [Fact, Upstream("VersionTest.java#test10")]
    public async Task Test10()
    {
        using var listen = SqlCapture.Listen();
        var user = new SysUserVersionLongLogicDel { Id = "1x1", Username = "aaaa", Phone = "1333", Version = 1L };
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Update(user).SetColumns(o => new { o.Username }).AsTable("AXX").ExecuteAsync(Ct));
        const string sql = "UPDATE `AXX` SET `username` = ?,`version` = ? WHERE `deleted` = ? AND `version` = ? AND `id` = ?";
        Assert.Equal(sql, ex.Sql);
        listen.AssertSql(sql);
        listen.AssertParams("aaaa(String),2(Long),false(Boolean),1(Long),1x1(String)");
    }
}
