using EasyQuerySharp.Tests.Upstream.Pgsql;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：mysql8/JsonTest.java（JSON 存成文本列，按键取值）。Java 的 asJSONObject().getField / getBoolean / getInteger、
/// asJSONArray().getJSONObject(0).getString 在 C# 写成 <c>Sql.Json(列).Text / Value&lt;T&gt; / Get(0).Text</c>。
/// </summary>
public sealed class JsonColumnTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private async Task SeedAsync()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(TopicJson)], Ct);
        await Db.CodeFirst.CreateTablesAsync([typeof(TopicJson)], Ct);
        await Db.Insert(
            new TopicJson
            {
                Id = "1", Name = "名称", ExtraJson = "{\"success\":true,\"code\":\"200\",\"age\":18}",
                ExtraJsonArray = "[{\"name\":\"Jack\",\"age\":18,\"success\":true,\"code\":\"200\"},{\"name\":\"Tom\",\"age\":20,\"success\":false,\"code\":\"200\"}]",
            },
            new TopicJson
            {
                Id = "2", Name = "名称2", ExtraJson = "{\"success\":false,\"code\":\"100\",\"msg\":\"存在错误信息\"}",
                ExtraJsonArray = "[{\"name\":\"JackSon\",\"age\":18,\"success\":true,\"code\":\"200\"},{\"name\":\"Tom\",\"age\":20,\"success\":false,\"code\":\"200\"}]",
            }).ExecuteAsync(Ct);
    }

    [Fact, Upstream("mysql8/JsonTest.java#testJsonField")]
    public async Task TestJsonField()
    {
        await SeedAsync();
        var list = await Db.Query<TopicJson>().Where(t => Sql.Json(t.ExtraJson).Text("code") == "200" && Sql.Json(t.ExtraJson).Text("success") == "true").ToListAsync(Ct);
        Assert.Equal("1", Assert.Single(list).Id);
        var flags = await Db.Query<TopicJson>().Select(t => new Draft<bool?>(Sql.Json(t.ExtraJson).Value<bool?>("success"))).ToListAsync(Ct);
        Assert.Equal([false, true], flags.Select(f => f.Value1!.Value).Order());
    }

    [Fact, Upstream("mysql8/JsonTest.java#testJsonField2")]
    public async Task TestJsonField2()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<TopicJson>().Where(t => Sql.Json(t.ExtraJson).Text("code") == "200").ToListAsync(Ct);
        Assert.Contains("\"code\":\"200\"", Assert.Single(list).ExtraJson);
        listen.AssertSql("SELECT `id`,`name`,`extra_json`,`extra_json_array` FROM `t_test_json` WHERE JSON_UNQUOTE(JSON_EXTRACT(`extra_json`, '$.code')) = ?");
        listen.AssertParams("200(String)");
    }

    [Fact, Upstream("mysql8/JsonTest.java#testJsonField4")]
    public async Task TestJsonField4()
    {
        await SeedAsync();
        var list = await Db.Query<TopicJson>().Where(t => Sql.Json(t.ExtraJson).Value<bool?>("success") == true)
            .Select(t => new Draft<bool?>(Sql.Json(t.ExtraJson).Value<bool?>("success"))).ToListAsync(Ct);
        Assert.True(Assert.Single(list).Value1);
    }

    [Fact, Upstream("mysql8/JsonTest.java#testJsonField5")]
    public async Task TestJsonField5()
    {
        await SeedAsync();
        Assert.Single(await Db.Query<TopicJson>().Where(t => Sql.Json(t.ExtraJson).Value<int?>("age") == 18).ToListAsync(Ct));
    }

    [Fact, Upstream("mysql8/JsonTest.java#testJsonField6")]
    public async Task TestJsonField6()
    {
        await SeedAsync();
        Assert.Single(await Db.Query<TopicJson>().Where(t => Sql.Json(t.ExtraJsonArray).Get(0).Text("name") == "Jack").ToListAsync(Ct));
    }
}

/// <summary>对应 Java：pgsql/JsonTest.java 与 pgsql/Json2Test.java（jsonb 列：文本属性与对象属性两种写法）。</summary>
public sealed class PgJsonColumnTest(PgFixture fixture) : PgTest(fixture)
{
    private const string ArrayJson = "[{\"name\":\"Jack\",\"age\":18,\"success\":true,\"code\":\"200\"},{\"name\":\"Tom\",\"age\":20,\"success\":false,\"code\":\"200\"}]";

    private async Task SeedTextAsync()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(PgTopicJson)], Ct);
        await Db.CodeFirst.CreateTablesAsync([typeof(PgTopicJson)], Ct);
        await Db.Insert(
            new PgTopicJson { Id = "1", Name = "名称", ExtraJson = "{\"success\":true,\"code\":\"200\",\"age\":18}", ExtraJsonArray = ArrayJson },
            new PgTopicJson
            {
                Id = "2", Name = "名称2", ExtraJson = "{\"success\":false,\"code\":\"100\",\"msg\":\"存在错误信息\"}",
                ExtraJsonArray = ArrayJson.Replace("\"Jack\"", "\"JackSon\"", StringComparison.Ordinal),
            }).ExecuteAsync(Ct);
    }

    private async Task SeedObjectAsync()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(PgTopicJson2)], Ct);
        await Db.CodeFirst.CreateTablesAsync([typeof(PgTopicJson2)], Ct);
        await Db.Insert(new PgTopicJson2
        {
            Id = "1", Name = "名称",
            ExtraJson = new TopicExtraJson { Success = true, Code = "200", Age = 18 },
            ExtraJsonArray =
            [
                new TopicExtraJson { Success = true, Name = "Jack", Code = "202", Age = 18 },
                new TopicExtraJson { Success = false, Name = "Tom", Code = "200", Age = 20 },
            ],
        }).ExecuteAsync(Ct);
    }

    [Fact, Upstream("pgsql/JsonTest.java#testJsonField4")]
    public async Task TestJsonField4()
    {
        await SeedTextAsync();
        var list = await Db.Query<PgTopicJson>().Where(t => Sql.Json(t.ExtraJson).Value<bool?>("success") == true)
            .Select(t => new Draft<bool?>(Sql.Json(t.ExtraJson).Value<bool?>("success"))).ToListAsync(Ct);
        Assert.True(Assert.Single(list).Value1);
    }

    [Fact, Upstream("pgsql/JsonTest.java#testJsonField5")]
    public async Task TestJsonField5()
    {
        await SeedTextAsync();
        Assert.Single(await Db.Query<PgTopicJson>().Where(t => Sql.Json(t.ExtraJson).Value<int?>("age") == 18).ToListAsync(Ct));
    }

    [Fact, Upstream("pgsql/JsonTest.java#testJsonField6")]
    public async Task TestJsonField6()
    {
        await SeedTextAsync();
        Assert.Single(await Db.Query<PgTopicJson>().Where(t => Sql.Json(t.ExtraJsonArray).Get(0).Text("name") == "Jack").ToListAsync(Ct));
    }

    [Fact, Upstream("pgsql/Json2Test.java#testJsonField4")]
    public async Task Json2TestJsonField4()
    {
        await SeedObjectAsync();
        var list = await Db.Query<PgTopicJson2>().Where(t => Sql.Json(t.ExtraJson).Value<bool?>("success") == true)
            .Select(t => new Draft<bool?>(Sql.Json(t.ExtraJson).Value<bool?>("success"))).ToListAsync(Ct);
        Assert.True(Assert.Single(list).Value1);
    }

    [Fact, Upstream("pgsql/Json2Test.java#testJsonField5")]
    public async Task Json2TestJsonField5()
    {
        await SeedObjectAsync();
        Assert.Single(await Db.Query<PgTopicJson2>().Where(t => Sql.Json(t.ExtraJson).Value<int?>("age") == 18).ToListAsync(Ct));
    }

    [Fact, Upstream("pgsql/Json2Test.java#testJsonField6")]
    public async Task Json2TestJsonField6()
    {
        await SeedObjectAsync();
        var row = Assert.Single(await Db.Query<PgTopicJson2>().Where(t => Sql.Json(t.ExtraJsonArray).Get(0).Text("name") == "Jack").ToListAsync(Ct));
        Assert.Equal(2, row.ExtraJsonArray!.Count);
        Assert.Equal("Jack", row.ExtraJsonArray[0].Name);
    }

    [Fact, Upstream("pgsql/Json2Test.java#testJsonField7")]
    public async Task Json2TestJsonField7()
    {
        await SeedObjectAsync();
        var vo = Assert.Single(await Db.Query<PgTopicJson2>().Where(t => Sql.Json(t.ExtraJsonArray).Get(0).Text("name") == "Jack").Select<TopicJson2VO>().ToListAsync(Ct));
        Assert.Equal("200", vo.ExtraJson!.Code);
        Assert.Equal(2, vo.ExtraJsonArray!.Count);
    }
}
