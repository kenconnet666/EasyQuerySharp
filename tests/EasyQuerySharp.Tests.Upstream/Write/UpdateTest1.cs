using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：UpdateTest1.java。换 schema / 表名后执行失败（库里没有 <c>_abc</c>），比对异常与监听器里的 SQL；
/// 以及 <see cref="Interceptors.TopicUpdateInterceptor"/> 在各种更新里补 <c>create_time</c>。
/// </summary>
public sealed class UpdateTest1(MySqlFixture fixture) : MySqlTest(fixture)
{
    // 执行失败：异常带着执行的 SQL，监听器也收到同一条
    private static async Task AssertFailedSqlAsync(Func<Task> execute, string sql, string parameters)
    {
        using var listen = SqlCapture.Listen();
        var ex = await Assert.ThrowsAsync<EasyQueryException>(execute);
        Assert.Equal(sql, ex.Sql);
        listen.AssertSql(sql);
        listen.AssertParams(parameters);
    }

    [Fact, Upstream("UpdateTest1.java#testUpdate1")]
    public Task TestUpdate1() => AssertFailedSqlAsync(
        () => Db.Update<Topic>().Set(o => o.Title, "title_abc").AsSchema(x => x + "_abc").AsTable(o => o + "_abc").WhereById("1").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_topic_abc` SET `title` = ? WHERE `id` = ?", "title_abc(String),1(String)");

    [Fact, Upstream("UpdateTest1.java#testUpdate1_1")]
    public Task TestUpdate1_1() => AssertFailedSqlAsync(
        () => Db.Update<Topic>().SetNull(o => o.Title).AsSchema(x => x + "_abc").AsTable(o => o + "_abc").WhereById("1").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_topic_abc` SET `title` = ? WHERE `id` = ?", "null(null),1(String)");

    // Java 的 toStr()：C# 的 ToString() 翻译成 CAST
    [Fact, Upstream("UpdateTest1.java#testUpdate1_2")]
    public Task TestUpdate1_2() => AssertFailedSqlAsync(
        () => Db.Update<Topic>().Set(o => new Topic { Title = o.Stars.ToString() }).AsSchema(x => x + "_abc").AsTable(o => o + "_abc").WhereById("1").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_topic_abc` SET `title` = CAST(`stars` AS CHAR) WHERE `id` = ?", "1(String)");

    // Java 的 asAnyType(String.class) 只改类型不转换：C# 经 object 强转，翻译时去掉转换
    [Fact, Upstream("UpdateTest1.java#testUpdate1_3")]
    public Task TestUpdate1_3() => AssertFailedSqlAsync(
        () => Db.Update<Topic>().Set(o => new Topic { Title = (string?)(object?)o.Stars }).AsSchema(x => x + "_abc").AsTable(o => o + "_abc").WhereById("1").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_topic_abc` SET `title` = `stars` WHERE `id` = ?", "1(String)");

    private static Topic Topic1() => new() { Id = "1", Stars = 101, Title = "标题1", CreateTime = new DateTime(2021, 1, 1, 1, 1, 1) };

    [Fact, Upstream("UpdateTest1.java#testUpdate2")]
    public Task TestUpdate2() => AssertFailedSqlAsync(
        () => Db.Update(Topic1()).AsSchema(x => x + "_abc").AsTable(o => o + "_abc").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_topic_abc` SET `stars` = ?,`title` = ?,`create_time` = ? WHERE `id` = ?",
        "101(Integer),标题1(String),2021-01-01T01:01:01(LocalDateTime),1(String)");

    // SET 按 SetColumns 的顺序；指定的列即使也是定位列也照常写
    [Fact, Upstream("UpdateTest1.java#testUpdate3")]
    public Task TestUpdate3() => AssertFailedSqlAsync(
        () => Db.Update(Topic1()).AsSchema(x => x + "_abc").AsTable(o => o + "_abc").SetColumns(o => new { o.Title, o.Stars }).WhereColumns(o => new { o.Id, o.Stars }).ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_topic_abc` SET `title` = ?,`stars` = ? WHERE `id` = ? AND `stars` = ?",
        "标题1(String),101(Integer),1(String),101(Integer)");

    [Fact, Upstream("UpdateTest1.java#testUpdate4")]
    public Task TestUpdate4() => AssertFailedSqlAsync(
        () => Db.Update<BlogEntity>().AsSchema(x => x + "_abc").AsTable(o => o + "_abc").Set(o => new BlogEntity { IsTop = !o.IsTop }).WhereById("2xxxa").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_blog_abc` SET `is_top` = (NOT (`is_top`)) WHERE `deleted` = ? AND `id` = ?", "false(Boolean),2xxxa(String)");

    [Fact, Upstream("UpdateTest1.java#testUpdate5")]
    public Task TestUpdate5() => AssertFailedSqlAsync(
        () => Db.Update<BlogEntity>().AsSchema(x => x + "_abc").AsTable(o => o + "_abc").Set(o => new BlogEntity { Title = o.Title == "123" ? "1" : "2" }).WhereById("2xxxa").ExecuteAsync(Ct),
        "UPDATE `_abc`.`t_blog_abc` SET `title` = (CASE WHEN `title` = ? THEN ? ELSE ? END) WHERE `deleted` = ? AND `id` = ?",
        "123(String),1(String),2(String),false(Boolean),2xxxa(String)");

    [Fact, Upstream("UpdateTest1.java#updateNoSet", Deviation = "Java 没有 SET 时只打日志、不执行、返回 0；C# 直接抛出 InvalidOperationException，避免把漏写 Set 的更新静默吞掉")]
    public async Task UpdateNoSet()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Update<Topic>().Where(t => t.Id == "xxxmmnbv").ExecuteAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Update<Topic>().Where(t => t.Id == "xxxmmnbv").ExecuteAsync(0, "123", Ct));
    }

    // 追踪的实体没有变化：不发语句，返回 0
    [Fact, Upstream("UpdateTest1.java#updateTest51")]
    public async Task UpdateTest51()
    {
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().AsTracking().Where(t => t.Id == "1").FirstAsync(Ct);
            Assert.Equal(0, await Db.Update(topic).ExecuteAsync(Ct));
        }
        Assert.False(Db.IsTracking);
    }

    // 按条件更新：拦截器追加 create_time（Java 只执行不断言）
    [Fact, Upstream("UpdateTest1.java#updateTestUpdate")]
    public async Task UpdateTestUpdate()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<TopicUpdate>().Set(t => t.Stars, 123).WhereById("1098765xxx").ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ?,`create_time` = ? WHERE `id` = ?");
    }

    private static TopicUpdate NewTopicUpdate() => new() { Id = "1098765xxx", CreateTime = new DateTime(2020, 1, 1, 1, 1, 0), Title = "123" };

    // 按实体更新前拦截器把 create_time 改成 2024-01-01
    [Fact, Upstream("UpdateTest1.java#updateTestUpdate1")]
    public async Task UpdateTestUpdate1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update(NewTopicUpdate()).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ?,`title` = ?,`create_time` = ? WHERE `id` = ?");
        listen.AssertParams("null(null),123(String),2024-01-01T01:01(LocalDateTime),1098765xxx(String)");
    }

    // 差异更新：标题改了，拦截器改的 create_time 也算变化
    [Fact, Upstream("UpdateTest1.java#updateTestUpdate2")]
    public async Task UpdateTestUpdate2()
    {
        using var listen = SqlCapture.Listen();
        using (Db.BeginTracking())
        {
            var topic = NewTopicUpdate();
            Db.AddTracking(topic);
            topic.Title = "456";
            await Db.Update(topic).ExecuteAsync(Ct);
        }
        listen.AssertSql("UPDATE `t_topic` SET `title` = ?,`create_time` = ? WHERE `id` = ?");
        listen.AssertParams("456(String),2024-01-01T01:01(LocalDateTime),1098765xxx(String)");
    }

    // 限定了列：拦截器补上 create_time
    [Fact, Upstream("UpdateTest1.java#updateTestUpdate3")]
    public async Task UpdateTestUpdate3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update(NewTopicUpdate()).SetColumns(t => t.Title).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `title` = ?,`create_time` = ? WHERE `id` = ?");
        listen.AssertParams("123(String),2024-01-01T01:01(LocalDateTime),1098765xxx(String)");
    }
}
