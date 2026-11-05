namespace EasyQuerySharp.Tests.Upstream.Features;

/// <summary>
/// 对应 Java：LogicDeleteTest.java。布尔逻辑删除（<see cref="LogicDelTopic"/>）与自定义策略
/// （<see cref="LogicDelTopicCustom"/>：未删除是 <c>deleted_at IS NULL</c>，删除时写删除时间与删除人）。
/// </summary>
public sealed class LogicDeleteTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string LogicDelTopicSelect = "SELECT `id`,`stars`,`title`,`deleted`,`create_time` FROM `t_logic_del_topic`";
    private const string CustomSelect = "SELECT `id`,`stars`,`title`,`deleted_at`,`deleted_user`,`create_time` FROM `t_logic_del_topic_custom`";

    [Fact, Upstream("LogicDeleteTest.java#Test1")]
    public async Task Test1()
    {
        Assert.Equal(LogicDelTopicSelect + " WHERE `deleted` = ?", Db.Query<LogicDelTopic>().ToSql());
        Assert.Equal(100, (await Db.Query<LogicDelTopic>().ToListAsync(Ct)).Count);
    }

    [Fact, Upstream("LogicDeleteTest.java#Test2")]
    public async Task Test2()
    {
        Assert.Equal("UPDATE `t_logic_del_topic` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ?", Db.Delete<LogicDelTopic>().WhereById("111").ToSql());
        Assert.Equal(0, await Db.Delete<LogicDelTopic>().WhereById("111").ExecuteAsync(Ct));
    }

    [Fact, Upstream("LogicDeleteTest.java#Test3")]
    public async Task Test3()
    {
        var topic = new LogicDelTopic { Id = "111" };
        Assert.Equal("UPDATE `t_logic_del_topic` SET `deleted` = ? WHERE `deleted` = ? AND `id` = ?", Db.Delete(topic).ToSql());
        Assert.Equal(0, await Db.Delete(topic).ExecuteAsync(Ct));
    }

    [Fact, Upstream("LogicDeleteTest.java#Test4")]
    public async Task Test4()
    {
        Assert.Equal(LogicDelTopicSelect, Db.Query<LogicDelTopic>().DisableLogicDelete().ToSql());
        Assert.Equal(100, (await Db.Query<LogicDelTopic>().DisableLogicDelete().ToListAsync(Ct)).Count);
    }

    [Fact, Upstream("LogicDeleteTest.java#Test5")]
    public async Task Test5()
    {
        Assert.Equal("DELETE FROM `t_logic_del_topic` WHERE `id` = ?", Db.Delete<LogicDelTopic>().DisableLogicDelete().WhereById("111xx").ToSql());
        Assert.Equal(0, await Db.Delete<LogicDelTopic>().DisableLogicDelete().WhereById("111xx").ExecuteAsync(Ct));
    }

    [Fact, Upstream("LogicDeleteTest.java#Test6")]
    public async Task Test6()
    {
        var topic = await Db.Query<LogicDelTopic>().DisableLogicDelete().FirstOrDefaultAsync(Ct);
        Assert.NotNull(topic);
        var update = Db.Update<LogicDelTopic>().DisableLogicDelete().Set(l => l.Title, topic.Title).WhereById(topic.Id!);
        Assert.Equal("UPDATE `t_logic_del_topic` SET `title` = ? WHERE `id` = ?", update.ToSql());
        Assert.Equal(1, await update.ExecuteAsync(Ct));
    }

    [Fact, Upstream("LogicDeleteTest.java#Test7")]
    public async Task Test7()
    {
        Assert.Equal(CustomSelect + " WHERE `deleted_at` IS NULL", Db.Query<LogicDelTopicCustom>().ToSql());
        Assert.Equal(100, (await Db.Query<LogicDelTopicCustom>().ToListAsync(Ct)).Count);
    }

    // 按实体更新：生效的逻辑删除列在条件里，不写进 SET
    [Fact, Upstream("LogicDeleteTest.java#Test8")]
    public async Task Test8()
    {
        Assert.Equal(CustomSelect + " WHERE `deleted_at` IS NULL AND `id` = ?", Db.Query<LogicDelTopicCustom>().Where(o => o.Id == "1").ToSql());
        var topic = await Db.Query<LogicDelTopicCustom>().Where(o => o.Id == "1").FirstOrDefaultAsync(Ct);
        Assert.NotNull(topic);
        using var listen = SqlCapture.Listen();
        Assert.Equal(1, await Db.Update(topic).ExecuteAsync(Ct));
        listen.AssertSql("UPDATE `t_logic_del_topic_custom` SET `stars` = ?,`title` = ?,`deleted_user` = ?,`create_time` = ? WHERE `deleted_at` IS NULL AND `id` = ?");
    }

    // 删除改写成 UPDATE：SET 由自定义策略给出（删除时间、当前用户）
    [Fact, Upstream("LogicDeleteTest.java#Test9")]
    public async Task Test9()
    {
        var topic = await Db.Query<LogicDelTopicCustom>().Where(o => o.Id == "1").FirstOrDefaultAsync(Ct);
        Assert.NotNull(topic);
        topic.Id = "11xx";
        CurrentUserHelper.UserId = "easy-query";
        using var listen = SqlCapture.Listen();
        Assert.Equal(0, await Db.Delete(topic).ExecuteAsync(Ct));
        listen.AssertSql("UPDATE `t_logic_del_topic_custom` SET `deleted_at` = ?,`deleted_user` = ? WHERE `deleted_at` IS NULL AND `id` = ?");
        Assert.EndsWith(",easy-query(String),11xx(String)", JavaParams.Format(listen.Last.Statement));
    }

    [Fact, Upstream("LogicDeleteTest.java#Test10")]
    public async Task Test10()
    {
        Assert.Equal(CustomSelect + " WHERE `id` = ?", Db.Query<LogicDelTopicCustom>().DisableLogicDelete().Where(o => o.Id == "1").ToSql());
        Assert.NotNull(await Db.Query<LogicDelTopicCustom>().DisableLogicDelete().Where(o => o.Id == "1").FirstOrDefaultAsync(Ct));
    }
}
