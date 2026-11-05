namespace EasyQuerySharp.Tests.Upstream.Features;

/// <summary>
/// 对应 Java：InterceptorTest.java。<see cref="MyEntityInterceptor"/> 填创建 / 修改人与时间（按条件更新时追加 SET），
/// <see cref="MyTenantInterceptor"/> 有当前用户时按租户过滤，可以按名字在单条语句上关闭。
/// </summary>
public sealed class InterceptorTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string Select = "SELECT `id`,`stars`,`title`,`create_time`,`create_by`,`update_time`,`update_by`,`tenant_id` FROM `t_topic_interceptor`";

    // 三个用例共同的部分：插入时填人与租户，按实体更新时刷新修改人。
    // 当前用户是 AsyncLocal，这里改的值不会带回调用方，所以调用前由用例设好、返回后用例再切到 xiaoming1
    private async Task<TopicInterceptor> InsertThenUpdateByEntityAsync(string id, int stars)
    {
        Assert.Equal(1, await Db.Insert(new TopicInterceptor { Id = id, Title = id, Stars = stars }).ExecuteAsync(Ct));
        Assert.Equal(Select + " WHERE `id` = ?", Db.Query<TopicInterceptor>().Where(t => t.Id == id).DisableInterceptor("MyTenantInterceptor").ToSql());

        var inserted = await Db.Query<TopicInterceptor>().Where(t => t.Id == id).FirstOrDefaultAsync(Ct);
        Assert.NotNull(inserted);
        Assert.Equal(CurrentUserHelper.UserId, inserted.CreateBy);
        CurrentUserHelper.UserId = "xiaoming1";
        Assert.Equal(1, await Db.Update(inserted).ExecuteAsync(Ct));

        var updated = await Db.Query<TopicInterceptor>().Where(t => t.Id == id).FirstOrDefaultAsync(Ct);
        Assert.NotNull(updated);
        Assert.NotEqual(inserted.UpdateBy, inserted.CreateBy);
        Assert.NotEqual(CurrentUserHelper.UserId, inserted.CreateBy);
        Assert.Equal(CurrentUserHelper.UserId, updated.UpdateBy);
        return updated;
    }

    [Fact, Upstream("InterceptorTest.java#test1")]
    public async Task Test1()
    {
        CurrentUserHelper.UserId = "xiaoming";
        CurrentUserHelper.TenantId = "abc";
        await Db.Delete<TopicInterceptor>().WhereById("123xx").DisableLogicDelete().AllowDeleteStatement(true).ExecuteAsync(Ct);
        var updated = await InsertThenUpdateByEntityAsync("123xx", 123);
        CurrentUserHelper.UserId = "xiaoming1";
        Assert.Equal(1, await Db.Update<TopicInterceptor>().Set(t => t.Title, updated.Title).WhereById(updated.Id!).ExecuteAsync(Ct));
        await Db.Delete<TopicInterceptor>().WhereById("123xx").DisableLogicDelete().AllowDeleteStatement(true).ExecuteAsync(Ct);
    }

    [Fact, Upstream("InterceptorTest.java#test2")]
    public async Task Test2()
    {
        if (await Db.Query<TopicInterceptor>().Where(t => t.Id == "1234").AnyAsync(Ct))
            await Db.Delete<TopicInterceptor>().WhereById("1234").ExecuteAsync(Ct);
        CurrentUserHelper.UserId = "xiaoming";
        CurrentUserHelper.TenantId = "abc";
        var updated = await InsertThenUpdateByEntityAsync("1234", 1234);
        CurrentUserHelper.UserId = "xiaoming1";
        // 关闭租户过滤后条件里没有 tenant_id；按条件更新追加修改人与时间
        Assert.Equal("UPDATE `t_topic_interceptor` SET `title` = ?,`update_by` = ?,`update_time` = ? WHERE `id` = ?",
            Db.Update<TopicInterceptor>().DisableInterceptor("MyTenantInterceptor").Set(t => t.Title, updated.Title).WhereById(updated.Id!).ToSql());
        Assert.Equal(1, await Db.Update<TopicInterceptor>().Set(t => t.Title, updated.Title).WhereById(updated.Id!).ExecuteAsync(Ct));
    }

    [Fact, Upstream("InterceptorTest.java#test3")]
    public async Task Test3()
    {
        if (await Db.Query<TopicInterceptor>().Where(t => t.Id == "12345").AnyAsync(Ct))
            Assert.Equal(1, await Db.Delete<TopicInterceptor>().WhereById("12345").ExecuteAsync(Ct));
        CurrentUserHelper.UserId = "xiaoming";
        CurrentUserHelper.TenantId = "abc";
        Assert.Equal(Select + " WHERE `tenant_id` = ? AND `id` = ?", Db.Query<TopicInterceptor>().Where(t => t.Id == "12345").ToSql());
        var updated = await InsertThenUpdateByEntityAsync("12345", 12345);
        CurrentUserHelper.UserId = "xiaoming1";

        Assert.Equal("UPDATE `t_topic_interceptor` SET `title` = ?,`update_by` = ?,`update_time` = ? WHERE `tenant_id` = ? AND `id` = ?",
            Db.Update<TopicInterceptor>().Set(t => t.Title, updated.Title).WhereById(updated.Id!).ToSql());
        Assert.Equal(1, await Db.Update<TopicInterceptor>().Set(t => t.Title, updated.Title).WhereById(updated.Id!).ExecuteAsync(Ct));
        Assert.Equal("DELETE FROM `t_topic_interceptor` WHERE `tenant_id` = ? AND `id` = ?", Db.Delete<TopicInterceptor>().WhereById(updated.Id!).ToSql());
        Assert.Equal("DELETE FROM `t_topic_interceptor` WHERE `tenant_id` = ? AND `id` = ?", Db.Delete(updated).ToSql());
        Assert.Equal(1, await Db.Delete<TopicInterceptor>().WhereById(updated.Id!).ExecuteAsync(Ct));
        Assert.Equal(0, await Db.Delete(updated).ExecuteAsync(Ct));
    }
}
