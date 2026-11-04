using EasyQuerySharp.Commands;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>对应 Java：UpdateTest.java（按条件更新、按实体更新、追踪差异更新、按字典更新）。</summary>
public sealed class UpdateTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 里大量用例故意写到不存在的表，只为拿到出错的 SQL
    private static async Task<string> FailedSql(Func<Task> action) =>
        (await Assert.ThrowsAsync<EasyQueryException>(action)).Sql!;

    private static string NewTitle() => "test123" + Random.Shared.Next(100000);

    [Fact, Upstream("UpdateTest.java#updateTest1")]
    public async Task UpdateTest1()
    {
        Assert.Equal(1, await Db.Update<Topic>().Set(t => t.Stars, 12).Where(o => o.Id == "2").ExecuteAsync(Ct));

        using var listen = SqlCapture.Listen();
        Assert.Equal(0, await Db.Update<Topic>().Set(t => t.Stars, 12).Where(o => o.Id == "2").Where(o => o.Title == "2c").ExecuteAsync(Ct));
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ? WHERE `id` = ? AND `title` = ?");
        listen.AssertParams("12(Integer),2(String),2c(String)");

        Assert.Equal(0, await Db.Update<Topic>().Set(t => t.Stars, 12).Where(o => o.Id == "2" && o.Title == "2c").ExecuteAsync(Ct));
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ? WHERE `id` = ? AND `title` = ?");
        listen.AssertParams("12(Integer),2(String),2c(String)");

        Assert.Equal(0, await Db.Update<Topic>().Set(t => t.Stars, 12)
            .Where(o => o.Id == "2" && o.Title == "21" && o.CreateTime == new DateTime(2021, 3, 4, 5, 1, 0) && o.Title == "2c").ExecuteAsync(Ct));
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ? WHERE `id` = ? AND `title` = ? AND `create_time` = ? AND `title` = ?");
        listen.AssertParams("12(Integer),2(String),21(String),2021-03-04T05:01(LocalDateTime),2c(String)");
    }

    [Fact, Upstream("UpdateTest.java#updateTest2")]
    public async Task UpdateTest2() =>
        Assert.Equal(0, await Db.Update<Topic>().Set(t => t.Stars, 12).Where(o => o.Id == "2x").ExecuteAsync(Ct));

    [Fact, Upstream("UpdateTest.java#updateTest3")]
    public async Task UpdateTest3()
    {
        var topic = await Db.Query<Topic>().WhereById("3").FirstAsync(Ct);
        // Java 的 stars().asStr()：C# 写 ToString()，翻译成 CAST(x AS CHAR)
        Assert.Equal(1, await Db.Update<Topic>().Set(t => t.Title, t => t.Stars.ToString()).Where(o => o.Id == topic.Id).ExecuteAsync(Ct));
        var topic2 = await Db.Query<Topic>().WhereById("3").FirstAsync(Ct);
        Assert.Equal(topic.Stars.ToString(), topic2.Title);
    }

    [Fact, Upstream("UpdateTest.java#updateTest4")]
    public async Task UpdateTest4()
    {
        async Task<int?> Stars() => (await Db.Query<Topic>().WhereById("5").FirstAsync(Ct)).Stars;
        Assert.Equal(1, await Db.Update<Topic>().Set(t => t.Stars, 2).WhereById("5").ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Update<Topic>().Increment(t => t.Stars, 1).WhereById("5").ExecuteAsync(Ct));
        Assert.Equal(3, await Stars());
        Assert.Equal(1, await Db.Update<Topic>().Increment(t => t.Stars, 2).Where(o => o.Id == "5").ExecuteAsync(Ct));
        Assert.Equal(5, await Stars());
        Assert.Equal(1, await Db.Update<Topic>().Decrement(t => t.Stars, 1).Where(o => o.Id == "5").ExecuteAsync(Ct));
        Assert.Equal(4, await Stars());
        Assert.Equal(1, await Db.Update<Topic>().Decrement(t => t.Stars, 2).Where(o => o.Id == "5").ExecuteAsync(Ct));
        Assert.Equal(2, await Stars());
    }

    // 追踪作用域里查询默认就追踪（Java 的默认行为），AsTracking 只是显式写出来；AddTracking 对已追踪的同一实例返回 true
    [Theory, Upstream("UpdateTest.java#updateTest5"), Upstream("UpdateTest.java#updateTest6"), Upstream("UpdateTest.java#updateTest7")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UpdateTest5(bool asTracking, bool addTracking)
    {
        using (Db.BeginTracking())
        {
            var query = Db.Query<Topic>().Where(o => o.Id == "7");
            var topic = await (asTracking ? query.AsTracking() : query).FirstAsync(Ct);
            if (addTracking) Assert.True(Db.AddTracking(topic));
            topic.Title = NewTitle();
            Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ?", Db.Update(topic).ToSql());
            Assert.Equal(1, await Db.Update(topic).ExecuteAsync(Ct));
        }
        Assert.False(Db.AddTracking(new Topic { Id = "7" }));
    }

    [Fact, Upstream("UpdateTest.java#updateTest8")]
    public async Task UpdateTest8()
    {
        Assert.Equal(1, await Db.Update<Topic>().SetNull(t => t.Title).WhereById("9").ExecuteAsync(Ct));
        var topic = await Db.Query<Topic>().WhereById("9").FirstAsync(Ct);
        Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ?", Db.Update(topic).Columns(ColumnStrategy.OnlyNull).ToSql());
        Assert.Equal(1, await Db.Update(topic).Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct));
    }

    [Fact, Upstream("UpdateTest.java#updateTest9")]
    public async Task UpdateTest9()
    {
        Assert.Equal(1, await Db.Update<Topic>().SetNull(t => t.Title).WhereById("10").ExecuteAsync(Ct));
        var topic = await Db.Query<Topic>().WhereById("10").FirstAsync(Ct);
        Assert.Equal("UPDATE `t_topic` SET `stars` = ?,`create_time` = ? WHERE `id` = ?", Db.Update(topic).Columns(ColumnStrategy.OnlyNotNull).ToSql());
        Assert.Equal(1, await Db.Update(topic).Columns(ColumnStrategy.OnlyNotNull).ExecuteAsync(Ct));
    }

    [Fact, Upstream("UpdateTest.java#updateTest10")]
    public async Task UpdateTest10()
    {
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            Db.Update<Topic>().Set(t => t.Stars, 12).Where(o => o.Id == Guid.NewGuid().ToString()).ExecuteAsync(1, "123", Ct));
        Assert.Equal("123", ex.Message);
        Assert.Equal(0, ex.ActualRows);
    }

    // C# 枚举按底层整数参数化，Java 的参数文本是枚举名
    [Fact, Upstream("UpdateTest.java#updateTest11", Deviation = "参数文本：C# 枚举按底层整数参数化为 9(Integer)，Java 输出 CLASSER(TopicTypeEnum)"),
     Upstream("UpdateTest.java#updateTest11_1", Deviation = "同 updateTest11")]
    public async Task UpdateTest11()
    {
        var existing = await Db.Query<TopicTypeTest1>().WhereById("123").FirstOrDefaultAsync(Ct);
        if (existing is not null) Assert.Equal(1, await Db.Delete(existing).ExecuteAsync(Ct));
        using var listen = SqlCapture.Listen();
        var command = Db.Update<TopicTypeTest1>().Set(t => t.Stars, 234).Where(o => o.TopicType == TopicTypeEnum.Classer);
        Assert.Equal("UPDATE `t_topic_type` SET `stars` = ? WHERE `topic_type` = ?", command.ToSql());
        await command.ExecuteAsync(Ct);
        listen.AssertParams("234(Integer),9(Integer)");
    }

    [Fact, Upstream("UpdateTest.java#updateTest12")]
    public async Task UpdateTest12()
    {
        var topic = await Db.Query<Topic>().WhereById("15").FirstAsync(Ct);
        Assert.Equal(1, await Db.Update(topic).SetColumns(o => o.CreateTime).WhereColumns(o => o.Stars).ExecuteAsync(Ct));
    }

    [Fact, Upstream("UpdateTest.java#updateTest12_1"), Upstream("UpdateTest.java#updateTest12_2")]
    public async Task UpdateTest12_1()
    {
        var topic = await Db.Query<Topic>().WhereById("15").FirstAsync(Ct);
        Assert.Equal("UPDATE `t_topic` SET `title` = ?,`create_time` = ? WHERE `id` = ? AND `stars` = ?",
            Db.Update(topic).WhereColumns(t => new { t.Id, t.Stars }).ToSql());
    }

    [Fact, Upstream("UpdateTest.java#updateTest16", Deviation = "Java 的 updatable(null) 串了一长串配置方法；C# 用空集合表达没有实体，配置方法只保留有对应的几个")]
    public async Task UpdateTest16() =>
        Assert.Equal(0, await Db.Update(Array.Empty<TopicAuto>()).DisableInterceptors().DisableLogicDelete()
            .Columns(ColumnStrategy.OnlyNotNull).AsTable("c").ExecuteAsync(Ct));

    // 显式追踪的新实体：原始值里 title 为 null，所以定位条件是 title IS NULL
    [Fact, Upstream("UpdateTest.java#updateTest17"), Upstream("UpdateTest.java#updateTest17_1")]
    public async Task UpdateTest17()
    {
        using (Db.BeginTracking())
        {
            var topic = new Topic { Id = "123xx" };
            Assert.True(Db.AddTracking(topic));
            topic.Title = NewTitle();
            Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ? AND `title` IS NULL", Db.Update(topic).WhereColumns(o => new { o.Id, o.Title }).ToSql());
            Assert.Equal(0, await Db.Update(topic).ExecuteAsync(Ct));
        }
    }

    [Fact, Upstream("UpdateTest.java#updateTest17_2", Deviation = "C# 的 Topic.Alias 标了 ColumnIgnore（Java 映射了 alias 列），条件里少一个 `alias` IS NULL")]
    public async Task UpdateTest17_2()
    {
        using (Db.BeginTracking())
        {
            var topic = new Topic { Id = "123xx" };
            Db.AddTracking(topic);
            topic.Title = NewTitle();
            Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ? AND `stars` IS NULL AND `create_time` IS NULL",
                Db.Update(topic).WhereColumns(o => new { o.Id, o.Stars, o.CreateTime }).ToSql());
            Assert.Equal(0, await Db.Update(topic).ExecuteAsync(Ct));
        }
    }

    [Fact, Upstream("UpdateTest.java#updateTest18_1")]
    public async Task UpdateTest18_1() =>
        Assert.Equal("UPDATE `xxxxx` SET `stars` = ifnull(`stars`,0)+? WHERE `id` = ?", await FailedSql(() =>
            Db.Update<Topic>().AsTable("xxxxx").Set(t => t.Stars, t => Sql.Raw<int?>("ifnull({0},0)+{1}", t.Stars, 1)).Where(o => o.Id == "2").ExecuteAsync(Ct)));

    [Fact, Upstream("UpdateTest.java#updateTest19"), Upstream("UpdateTest.java#updateTest19_1")]
    public async Task UpdateTest19() =>
        Assert.Equal("UPDATE `xxxxx` SET `stars` = ifnull(`stars`,0)+?,`title` = ?,`create_time` = ? WHERE `id` = ?", await FailedSql(() =>
            Db.Update(new Topic()).AsTable("xxxxx").ColumnSql(t => t.Stars, "ifnull({0},0)+{1}").ExecuteAsync(Ct)));

    [Fact, Upstream("UpdateTest.java#updateTest20")]
    public async Task UpdateTest20()
    {
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().Where(o => o.Id == "7").FirstAsync(Ct);
            Assert.True(Db.AddTracking(topic));
            topic.Title = NewTitle();
            Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ? AND `title` = ?", Db.Update(topic).WhereColumns(o => new { o.Id, o.Title }).ToSql());
            Assert.Equal(1, await Db.Update(topic).ExecuteAsync(Ct));
        }
    }

    // 追踪的实体：SET 只写变化的 title（即使它也是定位列），定位列取原始值
    [Theory, Upstream("UpdateTest.java#updateTest21"), Upstream("UpdateTest.java#updateTest21_1"), Upstream("UpdateTest.java#updateTest22"),
     Upstream("UpdateTest.java#updateTest22_1"), Upstream("UpdateTest.java#updateTest23"), Upstream("UpdateTest.java#updateTest23_1")]
    [InlineData("x1234", "id,title", "UPDATE `x1234` SET `title` = ? WHERE `id` = ? AND `title` = ?")]
    [InlineData("x123", "id", "UPDATE `x123` SET `title` = ? WHERE `id` = ?")]
    [InlineData("x123", "title", "UPDATE `x123` SET `title` = ? WHERE `title` = ?")]
    public async Task UpdateTest21(string table, string whereColumns, string expected)
    {
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().Where(o => o.Id == "7").FirstAsync(Ct);
            Assert.True(Db.AddTracking(topic));
            topic.Title = NewTitle();
            var command = Db.Update(topic).AsTable(table);
            command = whereColumns switch
            {
                "id,title" => command.WhereColumns(o => new { o.Id, o.Title }),
                "id" => command.WhereColumns(o => o.Id),
                _ => command.WhereColumns(o => o.Title),
            };
            Assert.Equal(expected, await FailedSql(() => command.ExecuteAsync(Ct)));
        }
    }

    // 不在追踪作用域里：AddTracking 返回 false，整行更新（定位列不写进 SET）
    [Fact, Upstream("UpdateTest.java#updateTest24"), Upstream("UpdateTest.java#updateTest24_1")]
    public async Task UpdateTest24()
    {
        var topic = await Db.Query<Topic>().Where(o => o.Id == "7").FirstAsync(Ct);
        Assert.False(Db.AddTracking(topic));
        topic.Title = NewTitle();
        Assert.Equal("UPDATE `x123` SET `stars` = ?,`create_time` = ? WHERE `title` = ?",
            await FailedSql(() => Db.Update(topic).AsTable("x123").WhereColumns(o => o.Title).ExecuteAsync(Ct)));
    }

    [Fact, Upstream("UpdateTest.java#updateTest28"), Upstream("UpdateTest.java#updateTest28_1")]
    public void UpdateTest28()
    {
        using var tracking = Db.BeginTracking();
        var topic = new TopicLarge { Id = "1", Stars = 1, Title = "1", Title1 = "2", CreateTime = DateTime.Now };
        Db.AddTracking(topic);
        topic.Title = "xx";
        Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ?", Db.Update(topic).ToSql());
    }

    [Fact, Upstream("UpdateTest.java#updateTest29"), Upstream("UpdateTest.java#updateTest29_1")]
    public void UpdateTest29()
    {
        var topic = new TopicLarge { Id = "1", Stars = 1, Title = "xx", Title1 = "2", CreateTime = DateTime.Now };
        Assert.Equal("UPDATE `t_topic` SET `stars` = ?,`create_time` = ? WHERE `id` = ?", Db.Update(topic).ToSql());
    }

    // 1.0 与 1 数值相等，不算变化
    [Fact, Upstream("UpdateTest.java#updateTest30"), Upstream("UpdateTest.java#updateTest31")]
    public async Task UpdateTest30()
    {
        using var tracking = Db.BeginTracking();
        var blog = new BlogEntity { Id = "123123123x", Content = "123", Score = 1.0m };
        Assert.True(Db.AddTracking(blog));
        blog.Content = "111";
        blog.Score = 1m;
        Assert.Equal("UPDATE `x1234` SET `content` = ? WHERE `deleted` = ? AND `id` = ?",
            await FailedSql(() => Db.Update(blog).AsTable("x1234").ExecuteAsync(Ct)));
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);

    [Fact, Upstream("UpdateTest.java#mapUpdateTest1")]
    public async Task MapUpdateTest1() =>
        Assert.Equal("UPDATE `aaa` SET `name` = ? WHERE `id` = ?",
            await FailedSql(() => Db.UpdateMap("aaa", Map(("id", "123"), ("name", "123"))).WhereColumns("id").ExecuteAsync(Ct)));

    [Fact, Upstream("UpdateTest.java#mapUpdateTest2")]
    public async Task MapUpdateTest2() =>
        Assert.Equal("UPDATE `aaa` SET `name` = ? WHERE `id` = ?", await FailedSql(() =>
            Db.UpdateMap("aaa", Map(("id", "123"), ("name", "123"), ("name1", null))).Columns(ColumnStrategy.OnlyNotNull).WhereColumns("id").ExecuteAsync(Ct)));

    [Fact, Upstream("UpdateTest.java#mapUpdateTest3")]
    public async Task MapUpdateTest3() =>
        Assert.Equal("UPDATE `aaa` SET `name` = ?,`name1` = ? WHERE `id` = ?", await FailedSql(() =>
            Db.UpdateMap("aaa", Map(("id", "123"), ("name", "123"), ("name1", null))).Columns(ColumnStrategy.All).WhereColumns("id").ExecuteAsync(Ct)));

    [Fact, Upstream("UpdateTest.java#mapUpdateTest4")]
    public async Task MapUpdateTest4()
    {
        Assert.Equal(1, await Db.Update<Topic>().Set(t => t.Stars, 12).Where(o => o.Id == "2").ExecuteAsync(Ct));
        Assert.Equal(2, await Db.UpdateMap("t_topic", Map(("id", "2"), ("stars", 12)), Map(("id", "2"), ("stars", 12))).WhereColumns("id").ExecuteAsync(Ct));
    }

    [Fact, Upstream("UpdateTest.java#mapUpdateTest5")]
    public async Task MapUpdateTest5()
    {
        // stars 不允许为 null，执行失败
        Assert.Equal("UPDATE `t_topic` SET `stars` = ? WHERE `id` = ?",
            await FailedSql(() => Db.UpdateMap("t_topic", Map(("id", "2"), ("stars", null))).WhereColumns("id").ExecuteAsync(Ct)));
        using var listen = SqlCapture.Listen();
        await Db.UpdateMap("t_topic", Map(("id", "2"), ("stars", 12))).WhereColumns("id").ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ? WHERE `id` = ?");
        listen.AssertParams("12(Integer),2(String)");
    }

    [Fact, Upstream("UpdateTest.java#mapUpdateTest511")]
    public async Task MapUpdateTest511()
    {
        // 键区分大小写：字典里是 ID，定位列是 id
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.UpdateMap("t_topic", Map(("ID", "2"), ("stars", null))).WhereColumns("id").ExecuteAsync(Ct));
        Assert.Equal("WHERE 列 id 在字典里不存在", ex.Message);
    }

    private static List<Topic> BatchTopics(bool titleWithIndex) => Enumerable.Range(0, 100)
        .Select(i => new Topic { Id = "1234567890x" + i, Stars = 1, Title = titleWithIndex ? "1234567890x" + i : "1234567890x", CreateTime = null })
        .ToList();

    // Java 的 batch() 是一条语句多组参数；这里每个实体一条语句，逐条核对参数
    [Fact, Upstream("UpdateTest.java#updateTestBatch", Deviation = "批量：Java 用 JDBC batch 一条语句多组参数，C# 每个实体一条语句；Java 的 ignoreVersion 留到版本号（M5.3）")]
    public async Task UpdateTestBatch()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update(BatchTopics(false)).ExecuteAsync(Ct);
        for (var i = 0; i < 100; i++) listen.AssertParams(i, $"1(Integer),1234567890x(String),null(null),1234567890x{i}(String)");
    }

    [Fact, Upstream("UpdateTest.java#updateTestBatch1", Deviation = "同 updateTestBatch")]
    public async Task UpdateTestBatch1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update(BatchTopics(false)).SetColumns(o => o.Stars).ExecuteAsync(Ct);
        for (var i = 0; i < 100; i++) listen.AssertParams(i, $"1(Integer),1234567890x{i}(String)");
    }

    [Fact, Upstream("UpdateTest.java#updateTestBatch2", Deviation = "同 updateTestBatch")]
    public async Task UpdateTestBatch2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update(BatchTopics(true)).SetColumns(o => o.Title).ExecuteAsync(Ct);
        await Db.Update(BatchTopics(true)).SetColumns(o => new { o.Title }).ExecuteAsync(Ct);
        for (var i = 0; i < 200; i++) listen.AssertParams(i, $"1234567890x{i % 100}(String),1234567890x{i % 100}(String)");
    }

    [Fact, Upstream("UpdateTest.java#updateSet1")]
    public async Task UpdateSet1()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Update<Topic>()
            .Set(t => t.Stars, 1)
            .Set(t => t.Stars, t => int.Parse(t.Title!))
            .Set(t => t.Title, "1")
            .Set(t => t.Title, t => t.CreateTime!.Value.ToString("yyyy/MM/dd"))
            .Set(t => t.Title, t => t.Title ?? "x")
            .Set(t => t.Title, t => Sql.Raw<string>("IFNULL({0},{1})", t.Title, "p"))
            .Where(o => o.Id == "2" && o.Title == "2c")
            .ExecuteAsync(Ct);
        Assert.Equal(0, rows);
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ?,`stars` = CAST(`title` AS SIGNED),`title` = ?,`title` = DATE_FORMAT(`create_time`,'%Y/%m/%d'),`title` = IFNULL(`title`,?),`title` = IFNULL(`title`,?) WHERE `id` = ? AND `title` = ?");
        listen.AssertParams("1(Integer),1(String),x(String),p(String),2(String),2c(String)");
    }

    [Fact, Upstream("UpdateTest.java#updateTrackParameterTest1")]
    public async Task UpdateTrackParameterTest1()
    {
        var newTitle = NewTitle();
        string? oldTitle;
        using var listen = SqlCapture.Listen();
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().AsTracking().Where(o => o.Id == "7").FirstAsync(Ct);
            oldTitle = topic.Title;
            topic.Title = newTitle;
            await Assert.ThrowsAsync<EasyQueryException>(() => Db.Update(topic).AsTable("aaaxxx").WhereColumns(o => new { o.Id, o.Title }).ExecuteAsync(Ct));
        }
        listen.AssertSql("UPDATE `aaaxxx` SET `title` = ? WHERE `id` = ? AND `title` = ?");
        listen.AssertParams($"{newTitle}(String),7(String),{oldTitle}(String)");
    }

    // 追踪的与不追踪的混在一起：追踪的只写 title，不追踪的整行更新（title 是定位列，不写进 SET）
    [Theory, Upstream("UpdateTest.java#updateTrackParameterTest2"), Upstream("UpdateTest.java#updateTrackParameterTest3")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateTrackParameterTest2(bool trackedFirst)
    {
        var newTitle = NewTitle();
        string? oldTitle;
        using var listen = SqlCapture.Listen();
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().AsTracking().Where(o => o.Id == "7").FirstAsync(Ct);
            oldTitle = topic.Title;
            topic.Title = newTitle;
            var topic1 = await Db.Query<Topic>().AsNoTracking().Where(o => o.Id == "8").FirstAsync(Ct);
            topic1.Title = newTitle;
            Topic[] entities = trackedFirst ? [topic, topic1] : [topic1, topic];
            await Assert.ThrowsAsync<EasyQueryException>(() => Db.Update(entities).AsTable("aaaxxx").WhereColumns(o => new { o.Id, o.Title }).ExecuteAsync(Ct));
        }
        // 第一条更新就失败，之后的不再执行
        Assert.Equal(3, listen.Events.Count);
        if (trackedFirst)
        {
            listen.AssertSql("UPDATE `aaaxxx` SET `title` = ? WHERE `id` = ? AND `title` = ?");
            listen.AssertParams($"{newTitle}(String),7(String),{oldTitle}(String)");
        }
        else
        {
            listen.AssertSql("UPDATE `aaaxxx` SET `stars` = ?,`create_time` = ? WHERE `id` = ? AND `title` = ?");
            Assert.StartsWith("108(Integer),", JavaParams.Format(listen.Last.Statement), StringComparison.Ordinal);
            Assert.EndsWith($",8(String),{newTitle}(String)", JavaParams.Format(listen.Last.Statement), StringComparison.Ordinal);
        }
    }

    [Theory, Upstream("UpdateTest.java#updateSetFuncTest1"), Upstream("UpdateTest.java#updateSetFuncTest1_1"),
     Upstream("UpdateTest.java#updateSetFuncTest2"), Upstream("UpdateTest.java#updateSetFuncTest3")]
    [InlineData(1, "UPDATE `a123123` SET `title` = CONCAT(SUBSTR(`title`,2,10),`id`) WHERE `id` = ?", "123zzzxxx(String)")]
    [InlineData(2, "UPDATE `a123123` SET `title` = CONCAT(`id`,SUBSTR(`title`,2,10)) WHERE `id` = ?", "123zzzxxx(String)")]
    [InlineData(3, "UPDATE `a123123` SET `title` = CONCAT(SUBSTR(`title`,2,10),?) WHERE `id` = ?", "123(String),123zzzxxx(String)")]
    [InlineData(4, "UPDATE `a123123` SET `title` = CONCAT(SUBSTR(`title`,2,10),LOWER(`id`)) WHERE `id` = ?", "123zzzxxx(String)")]
    public async Task UpdateSetFuncTest1(int variant, string expectedSql, string expectedParams)
    {
        using var listen = SqlCapture.Listen();
        var command = Db.Update<Topic>().AsTable("a123123").WhereById("123zzzxxx");
        command = variant switch
        {
            1 => command.Set(t => t.Title, t => t.Title!.Substring(1, 10) + t.Id),
            2 => command.Set(t => t.Title, t => t.Id + t.Title!.Substring(1, 10)),
            3 => command.Set(t => t.Title, t => t.Title!.Substring(1, 10) + "123"),
            _ => command.Set(t => t.Title, t => t.Title!.Substring(1, 10) + t.Id!.ToLower()),
        };
        await Assert.ThrowsAsync<EasyQueryException>(() => command.ExecuteAsync(Ct));
        listen.AssertSql(expectedSql);
        listen.AssertParams(expectedParams);
    }

    [Fact, Upstream("UpdateTest.java#testUpdateIdNull1", Deviation = "C# 的主键为 null 时按 C# 语义写成 IS NULL，Java 写 `id` = ? 并传 null")]
    public async Task TestUpdateIdNull1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<Topic>().Set(t => t.Title, t => t.Title!.Substring(1, 10) + t.Id!.ToLower()).Where(t => t.Id == null).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `title` = CONCAT(SUBSTR(`title`,2,10),LOWER(`id`)) WHERE `id` IS NULL");
    }

    [Fact, Upstream("UpdateTest.java#testColumnConfigure1")]
    public async Task TestColumnConfigure1() =>
        Assert.Equal("INSERT INTO `xxxxx` (`id`,`stars`,`title`,`create_time`) VALUES (?,ifnull(`stars`,0)+?,?,?)", await FailedSql(() =>
            Db.Insert(new Topic()).AsTable("xxxxx").Columns(ColumnStrategy.All).ColumnSql(t => t.Stars, "ifnull({0},0)+{1}").ExecuteAsync(Ct)));

    [Fact, Upstream("UpdateTest.java#testColumnConfigure2")]
    public async Task TestColumnConfigure2()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal("UPDATE `xxxxx` SET `stars` = ifnull(`stars`,0)+? WHERE `id` = ?", await FailedSql(() =>
            Db.UpdateMap("xxxxx", Map(("id", "1"), ("stars", "2"))).ColumnSql("stars", "ifnull({0},0)+{1}").WhereColumns("id").ExecuteAsync(Ct)));
        listen.AssertParams("2(String),1(String)");
    }

    [Fact, Upstream("UpdateTest.java#testColumnConfigure3")]
    public async Task TestColumnConfigure3()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal("UPDATE `xxxxx` SET `id` = ?,`stars` = ? WHERE `id` = ?", await FailedSql(() =>
            Db.UpdateMap("xxxxx", Map(("id", new MapValue("123", "1")), ("stars", "2"))).WhereColumns("id").ExecuteAsync(Ct)));
        listen.AssertParams("123(String),2(String),1(String)");
    }

    [Fact, Upstream("UpdateTest.java#aaaa", Deviation = "Java 把 BlogEntity 全部列映射到 SysMenuDto（类型并不匹配）；C# 只投影用到的两列")]
    public async Task Aaaa()
    {
        var list = await Db.Query<BlogEntity>()
            .LeftJoin<BlogEntity>((a, b) => a.Id == b.Id)
            .LeftJoin<BlogEntity>((a, b, c) => a.Id == b.Id)
            .LeftJoin<BlogEntity>((a, b, c, d) => a.Id == b.Id)
            .Where((a, b, c, d) => a.Star == 1)
            .Select((a, b, c, d) => new { a.Id, SourceName = "1" + d.Title + "1" })
            .ToListAsync(Ct);
        Assert.NotNull(list);
    }

    [Fact, Upstream("UpdateTest.java#updateEntityWhere")]
    public async Task UpdateEntityWhere()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update(new BlogEntity { Id = "123xoiuy", Title = "title", Content = "content", Status = 1 }).Where(t => t.Status == 2).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_blog` SET `create_time` = ?,`update_time` = ?,`create_by` = ?,`update_by` = ?,`title` = ?,`content` = ?,`url` = ?,`star` = ?,`publish_time` = ?,`score` = ?,`status` = ?,`order` = ?,`is_top` = ?,`top` = ? WHERE `deleted` = ? AND `id` = ? AND `status` = ?");
        listen.AssertParams("null(null),null(null),null(null),null(null),title(String),content(String),null(null),null(null),null(null),null(null),1(Integer),null(null),null(null),null(null),false(Boolean),123xoiuy(String),2(Integer)");
    }
}
