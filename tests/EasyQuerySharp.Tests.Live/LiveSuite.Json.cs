using System.Text.Json;
using EasyQuerySharp.Dialects;

namespace EasyQuerySharp.Tests.Live;

// JSON 列：按声明的类型写入读回，null 保持 null，空列表写成 []
public abstract partial class LiveSuite
{
    // MySQL 系的列改成原生 JSON 类型（与 Java fork 的 8.0.18 用例一致），其余库用 code-first 的默认文本类型
    [Fact, Upstream("fork/live/JsonValueConverterLiveTest.java#mysql8018JsonColumn",
        Deviation = "C# 的实库套件在每个库上跑同一份往返（MySQL 系的列改成 JSON 类型）；Dictionary<string, object?> 的值按 System.Text.Json 读成 JsonElement")]
    public async Task RoundTripsJsonColumns()
    {
        var db = await SeededAsync();
        if (Dialect is MySqlDialect) await db.ExecuteSqlRawAsync("ALTER TABLE t_live_profile MODIFY addresses JSON, MODIFY extra JSON", cancellationToken: Ct);

        await db.Insert(new Profile
        {
            Id = "p1",
            Addresses = [new ProfileAddress { City = "Hangzhou", Zip = 310000 }],
            Extra = new() { ["vip"] = true, ["tags"] = new[] { "x", "y" } },
        }).ExecuteAsync(Ct);
        await db.Insert(new Profile { Id = "p2" }).ExecuteAsync(Ct);

        var read = await db.Query<Profile>().WhereById("p1").SingleAsync(Ct);
        Assert.Equal("Hangzhou", read.Addresses![0].City);
        Assert.Equal(310000, read.Addresses[0].Zip);
        Assert.True(((JsonElement)read.Extra!["vip"]!).GetBoolean());
        Assert.Equal(["x", "y"], ((JsonElement)read.Extra["tags"]!).EnumerateArray().Select(e => e.GetString()));

        var none = await db.Query<Profile>().WhereById("p2").SingleAsync(Ct);
        Assert.Null(none.Addresses);
        Assert.Null(none.Extra);

        read.Addresses = [];
        await db.Update(read).ExecuteAsync(Ct);
        Assert.Empty((await db.Query<Profile>().WhereById("p1").SingleAsync(Ct)).Addresses!);
    }
}
