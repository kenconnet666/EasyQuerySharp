using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>对应 Java：DeleteTest.java（按条件、按实体删除，逻辑删除改写成 UPDATE）。</summary>
public sealed class DeleteTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private async Task<Topic> EnsureTopic(string id, int stars)
    {
        var topic = await Db.Query<Topic>().WhereById(id).FirstOrDefaultAsync(Ct);
        if (topic is not null) return topic;
        topic = new Topic { Id = id, Title = "title" + stars, CreateTime = DateTime.Now, Stars = stars };
        Assert.Equal(1, await Db.Insert(topic).ExecuteAsync(Ct));
        return topic;
    }

    private static async Task<string> FailedSql(Func<Task> action) =>
        (await Assert.ThrowsAsync<EasyQueryException>(action)).Sql!;

    [Fact, Upstream("DeleteTest.java#deleteTest1")]
    public async Task DeleteTest1()
    {
        await EnsureTopic("999", 999);
        Assert.Equal("DELETE FROM `t_topic` WHERE `id` = ?", Db.Delete<Topic>().WhereById("999").ToSql());
        Assert.Equal(1, await Db.Delete<Topic>().WhereById("999").ExecuteAsync(Ct));
    }

    [Fact, Upstream("DeleteTest.java#deleteTest2")]
    public async Task DeleteTest2()
    {
        await EnsureTopic("998", 998);
        Assert.Equal("DELETE FROM `t_topic` WHERE `title` = ?", Db.Delete<Topic>().Where(o => o.Title == "title998").ToSql());
        Assert.Equal(1, await Db.Delete<Topic>().Where(o => o.Title == "title998").ExecuteAsync(Ct));
    }

    [Theory, Upstream("DeleteTest.java#deleteTest3"), Upstream("DeleteTest.java#deleteTest3_1"), Upstream("DeleteTest.java#deleteTest4")]
    [InlineData("997", 997)]
    [InlineData("997x", 997)]
    [InlineData("996", 996)]
    public async Task DeleteTest3(string id, int stars)
    {
        var topic = await EnsureTopic(id, stars);
        Assert.Equal("DELETE FROM `t_topic` WHERE `id` = ?", Db.Delete(topic).ToSql());
        Assert.Equal(1, await Db.Delete([topic]).ExecuteAsync(Ct));
    }

    [Fact, Upstream("DeleteTest.java#deleteTest6")]
    public async Task DeleteTest6()
    {
        await EnsureTopic("995", 995);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Delete<Topic>().WhereById("999").AllowDeleteStatement(false).ExecuteAsync(Ct));
    }

    [Fact, Upstream("DeleteTest.java#deleteTest13", NotApplicable = "检查的是 Java 表达式上下文里的行为标志位，C# 没有对应的内部状态")]
    public void DeleteTest13() { }

    [Fact, Upstream("DeleteTest.java#deleteTest14", Deviation = "Java 的 deletable(null) 用空集合表达；ToSql 同样返回 null")]
    public async Task DeleteTest14()
    {
        var empty = Array.Empty<Topic>();
        Assert.Null(Db.Delete(empty).ToSql());
        Assert.Null(Db.Delete(empty).DisableLogicDelete().DisableInterceptors().DisableInterceptor("1").EnableInterceptors().EnableInterceptor("1").AllowDeleteStatement(true).ToSql());
        Assert.Equal(0, await Db.Delete(empty).ExecuteAsync(Ct));
        Assert.Equal(0, await Db.Delete(empty).ExecuteAsync(0, "123", Ct));
    }

    [Theory, Upstream("DeleteTest.java#deleteTest5"), Upstream("DeleteTest.java#deleteTest19")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteTest5(bool withCreateTime)
    {
        Assert.Equal("UPDATE `t_blog` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ?", Db.Delete<BlogEntity>().Where(o => o.Id == "id123456").ToSql());
        if (withCreateTime)
            Assert.Equal("UPDATE `t_blog` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ? AND `create_time` <= ?",
                Db.Delete<BlogEntity>().Where(o => o.Id == "id123456" && o.CreateTime <= new DateTime(2023, 1, 1, 1, 1, 0)).ToSql());
        Assert.Equal("DELETE FROM `t_blog` WHERE `id` = ?", Db.Delete<BlogEntity>().Where(o => o.Id == "id123456").DisableLogicDelete().AllowDeleteStatement(true).ToSql());
        Assert.Equal(0, await Db.Delete<BlogEntity>().Where(o => o.Id == "id123456").ExecuteAsync(Ct));
        Assert.Equal(0, await Db.Delete<BlogEntity>().Where(o => o.Id == "id123456").DisableLogicDelete().AllowDeleteStatement(true).ExecuteAsync(Ct));
    }

    [Fact, Upstream("DeleteTest.java#deleteTest20")]
    public async Task DeleteTest20()
    {
        using var listen = SqlCapture.Listen();
        const string expected = "UPDATE `t_blog123abc` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ? AND `create_time` <= ?";
        Assert.Equal(expected, await FailedSql(() => Db.Delete<BlogEntity>().AsTable(o => o + "123abc")
            .Where(o => o.Id == "id123456" && o.CreateTime <= new DateTime(2023, 1, 1, 1, 1, 0)).ExecuteAsync(Ct)));
        listen.AssertSql(expected);
        listen.AssertParams("true(Boolean),false(Boolean),id123456(String),2023-01-01T01:01(LocalDateTime)");
    }

    [Fact, Upstream("DeleteTest.java#deleteTest21")]
    public async Task DeleteTest21()
    {
        var topic = new Topic { Id = "1", Stars = 101, Title = "标题1", CreateTime = new DateTime(2021, 1, 1, 1, 1, 1) };
        using var listen = SqlCapture.Listen();
        Assert.Equal("DELETE FROM `t_topic123abc` WHERE `id` = ?", await FailedSql(() => Db.Delete(topic).AsTable(o => o + "123abc").ExecuteAsync(Ct)));
        listen.AssertParams("1(String)");
    }

    [Fact, Upstream("DeleteTest.java#deleteTest22")]
    public async Task DeleteTest22()
    {
        var blog = new BlogEntity { Id = "1", Title = "标题1", CreateTime = new DateTime(2021, 1, 1, 1, 1, 1) };
        using var listen = SqlCapture.Listen();
        Assert.Equal("UPDATE `t_blog123abc` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ?", await FailedSql(() => Db.Delete(blog).AsTable(o => o + "123abc").ExecuteAsync(Ct)));
        listen.AssertParams("true(Boolean),false(Boolean),1(String)");
    }

    [Fact, Upstream("DeleteTest.java#testBatchDelete", Deviation = "批量：Java 一条语句两组参数，C# 每个实体一条语句")]
    public async Task TestBatchDelete()
    {
        List<Topic> topics = [new() { Id = "123pppxxx", Title = "123123", Stars = 2 }, new() { Id = "123pppxxx1", Title = "1231231", Stars = 1 }];
        using var listen = SqlCapture.Listen();
        await Db.Delete(topics).ExecuteAsync(Ct);
        listen.AssertSql(0, "DELETE FROM `t_topic` WHERE `id` = ?");
        listen.AssertParams(0, "123pppxxx(String)");
        listen.AssertParams(1, "123pppxxx1(String)");
    }

    [Fact, Upstream("DeleteTest.java#delete", NotApplicable = "检查的是 Java 删除命令的内部构建器，C# 没有对应的公开状态")]
    public void Delete() { }

    [Theory, Upstream("DeleteTest.java#deletex"), Upstream("DeleteTest.java#deletex1")]
    [InlineData(false, "DELETE FROM `t_blog` WHERE `id` = ?", "xxx111asdfgh123(String)")]
    [InlineData(true, "UPDATE `t_blog` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ?", "true(Boolean),false(Boolean),xxx111asdfgh123(String)")]
    public async Task Deletex(bool logicDelete, string expectedSql, string expectedParams)
    {
        var blog = new BlogEntity { Id = "xxx111asdfgh123", Title = "123", CreateTime = DateTime.Now };
        using var listen = SqlCapture.Listen();
        var command = Db.Delete(blog);
        await (logicDelete ? command : command.DisableLogicDelete()).ExecuteAsync(Ct);
        listen.AssertSql(expectedSql);
        listen.AssertParams(expectedParams);
    }

    [Fact, Upstream("DeleteTest.java#deletex2")]
    public async Task Deletex2()
    {
        using var listen = SqlCapture.Listen();
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Delete<BlogEntity>().DisableInterceptors().DisableInterceptor("x").EnableInterceptor("a")
            .AsSchema("asv").AsAlias("xx").Where(t => t.Id == "xxx111asdfgh123").ExecuteAsync(1, "2", Ct));
        listen.AssertSql("UPDATE `asv`.`t_blog` xx SET xx.`deleted` = ? WHERE xx.`deleted` = ? AND xx.`id` = ?");
        listen.AssertParams("true(Boolean),false(Boolean),xxx111asdfgh123(String)");
    }
}
