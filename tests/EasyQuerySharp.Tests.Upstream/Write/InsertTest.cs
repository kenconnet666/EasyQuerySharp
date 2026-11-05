using EasyQuerySharp.Commands;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>对应 Java：InsertTest.java（插入、回填自增键、换表、拦截器开关、冲突处理、按字典插入）。</summary>
public sealed class InsertTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static async Task<string> FailedSql(Func<Task> action) =>
        (await Assert.ThrowsAsync<EasyQueryException>(action)).Sql!;

    private static TopicAuto NewTopicAuto() => new() { Stars = 999, Title = "title999", CreateTime = DateTime.Now.AddDays(99) };

    [Fact, Upstream("InsertTest.java#insertTest")]
    public async Task InsertTest1()
    {
        var list = await Db.Query<TopicAuto>().Where(o => o.Stars < 999).ToListAsync(Ct);
        Assert.Equal(10, list.Count);
        Assert.Equal(Enumerable.Range(1, 10), list.Select(t => t.Id!.Value));
    }

    [Fact, Upstream("InsertTest.java#insertTest2")]
    public async Task InsertTest2()
    {
        var topic = NewTopicAuto();
        Assert.Equal(1, await Db.Insert(topic).FillGeneratedKeys().ExecuteAsync(Ct));
        Assert.NotNull(topic.Id);
    }

    [Fact, Upstream("InsertTest.java#insertTest3")]
    public void InsertTest3() =>
        Assert.Equal("INSERT INTO `xxx`.`aaa` (`stars`,`title`,`create_time`) VALUES (?,?,?)",
            Db.Insert(NewTopicAuto()).AsTable("aaa").AsSchema(_ => "xxx").ToSql());

    [Fact, Upstream("InsertTest.java#insertTest5")]
    public void InsertTest5() =>
        Assert.Equal("INSERT INTO `xxx`.`t_topic_autoaaa` (`stars`,`title`,`create_time`) VALUES (?,?,?)",
            Db.Insert(NewTopicAuto()).AsTable(o => o + "aaa").AsSchema("xxx").ToSql());

    // Topic1Interceptor 默认关闭：只有显式启用时才把标题清空；关闭全部之后再启用它，仍然生效
    [Theory, Upstream("InsertTest.java#insertTest6"), Upstream("InsertTest.java#insertTest7"), Upstream("InsertTest.java#insertTest8")]
    [InlineData("use", "INSERT INTO `xxx`.`t_topic_autoaaa` (`stars`,`create_time`) VALUES (?,?)")]
    [InlineData("use,none", "INSERT INTO `xxx`.`t_topic_autoaaa` (`stars`,`title`,`create_time`) VALUES (?,?,?)")]
    [InlineData("none,use", "INSERT INTO `xxx`.`t_topic_autoaaa` (`stars`,`create_time`) VALUES (?,?)")]
    public async Task InsertTest6(string steps, string expected)
    {
        var command = Db.Insert(NewTopicAuto());
        foreach (var step in steps.Split(','))
            command = step == "use" ? command.EnableInterceptor("Topic1Interceptor") : command.DisableInterceptors();
        Assert.Equal(expected, await FailedSql(() => command.AsTable(o => o + "aaa").AsSchema("xxx").ExecuteAsync(Ct)));
    }

    [Fact, Upstream("InsertTest.java#insertTest8_1")]
    public async Task InsertTest8_1() =>
        Assert.Equal("INSERT IGNORE INTO `xxx`.`t_topic_autoaaa` (`stars`,`create_time`) VALUES (?,?)", await FailedSql(() =>
            Db.Insert(NewTopicAuto()).OnConflictIgnore().DisableInterceptors().EnableInterceptor("Topic1Interceptor").AsTable(o => o + "aaa").AsSchema("xxx").ExecuteAsync(Ct)));

    [Fact, Upstream("InsertTest.java#insertTest9", Deviation = "Java 的 insertable(null) 串了一长串配置；C# 用空集合表达没有实体")]
    public async Task InsertTest9() =>
        Assert.Equal(0, await Db.Insert(Array.Empty<TopicAuto>()).DisableInterceptors().EnableInterceptor("11").EnableInterceptors()
            .AsSchema("b").AsTable("c").AsSchema(o => o + "ab").AsTable(o => o + "bb").ExecuteAsync(Ct));

    [Fact, Upstream("InsertTest.java#insertDuplicateKeyUpdate1")]
    public void InsertDuplicateKeyUpdate1() =>
        Assert.Equal("INSERT INTO `t_topic_auto` (`stars`,`title`,`create_time`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `stars` = VALUES(`stars`), `title` = VALUES(`title`), `create_time` = VALUES(`create_time`)",
            Db.Insert(NewTopicAuto()).OnConflictUpdate().ToSql());

    [Fact, Upstream("InsertTest.java#insertDuplicateKeyUpdate2")]
    public void InsertDuplicateKeyUpdate2() =>
        Assert.Equal("INSERT INTO `t_topic_auto` (`stars`,`title`,`create_time`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `stars` = VALUES(`stars`), `title` = VALUES(`title`)",
            Db.Insert(NewTopicAuto()).OnConflictUpdate(s => new { s.Stars, s.Title }).ToSql());

    private static BlogEntity Blog(string id, int star, bool withUrl = true)
    {
        var begin = new DateTime(2000, 1, 1, 1, 1, 1);
        return new BlogEntity
        {
            Id = id, CreateBy = id, CreateTime = begin.AddDays(1), UpdateBy = id, UpdateTime = begin.AddDays(1),
            Title = "title" + id, Content = "content" + id, Url = withUrl ? "http://blog.easy-query.com/" + id : null,
            Star = star, Score = 1.2m, Status = 1, Order = 1.2m, IsTop = false, Top = false, Deleted = false,
        };
    }

    // MySQL 冲突更新的受影响行数：插入计 1，确有更新计 2，值相同不算更新计 0
    [Fact, Upstream("InsertTest.java#insertTestx")]
    public async Task InsertTestx()
    {
        await Db.Delete<BlogEntity>().Hard().WhereById("200").ExecuteAsync(Ct);
        var blog = Blog("200", 1);
        Assert.Equal(1, await Db.Insert(blog).OnConflictIgnore().ExecuteAsync(Ct));
        Assert.Equal(1, await Db.Insert(blog).OnConflictUpdate(s => new { s.Star, s.Content }).ExecuteAsync(Ct));
        blog.Content = "xxx";
        Assert.Equal(2, await Db.Insert(blog).OnConflictUpdate(s => new { s.Star, s.Content }).ExecuteAsync(Ct));
        Assert.Equal(0, await Db.Insert(blog).OnConflictIgnore().ExecuteAsync(Ct));
        blog.Content = "abc";
        Assert.Equal(2, await Db.Insert(blog).OnConflictUpdate().ExecuteAsync(Ct));
        Assert.Equal("abc", (await Db.Query<BlogEntity>().WhereById("200").FirstAsync(Ct)).Content);
        await Db.Delete<BlogEntity>().WhereById("200").ExecuteAsync(Ct);
    }

    [Fact, Upstream("InsertTest.java#insertBatch", Deviation = "Java 的 batch() 返回 JDBC 的 SUCCESS_NO_INFO（-3）；C# 每个实体一条语句，返回真实行数")]
    public async Task InsertBatch()
    {
        string[] ids = ["500", "300", "400"];
        await Db.Delete<BlogEntity>().Hard().WhereByIds(ids).ExecuteAsync(Ct);
        List<BlogEntity> blogs = [Blog("500", 500, withUrl: false), Blog("300", 300), Blog("400", 400)];
        Assert.Equal(3, await Db.Insert(blogs).ExecuteAsync(Ct));
        await Db.Delete<BlogEntity>().Hard().WhereByIds(ids).ExecuteAsync(Ct);
        Assert.Equal(3, await Db.Insert(blogs).ExecuteAsync(Ct));
        await Db.Delete<BlogEntity>().Hard().WhereByIds(ids).ExecuteAsync(Ct);
    }

    // 模板里 {0} 是列、{1} 是值；Java 的 columnConfigure 只放了值，所以写 {0}
    [Fact, Upstream("InsertTest.java#insertCustom")]
    public async Task InsertCustom() =>
        Assert.Equal("INSERT INTO `xxxxx` (`stars`) VALUES (?)", await FailedSql(() =>
            Db.Insert(new TopicAutoNative { Stars = 1 }).AsTable("xxxxx").ColumnSql(t => t.Id, "sde.next_rowid('sde',{1})").ExecuteAsync(Ct)));

    [Fact, Upstream("InsertTest.java#insertCustom1")]
    public async Task InsertCustom1() =>
        Assert.Equal("INSERT INTO `xxxxx` (`id`,`stars`) VALUES (sde.next_rowid('sde',?),?)", await FailedSql(() =>
            Db.Insert(new TopicAutoNative { Stars = 1, Id = 0 }).AsTable("xxxxx").ColumnSql(t => t.Id, "sde.next_rowid('sde',{1})").ExecuteAsync(Ct)));

    [Theory, Upstream("InsertTest.java#incrementTest1"), Upstream("InsertTest.java#incrementTest2")]
    [InlineData(null)]
    [InlineData("id")]
    public async Task IncrementTest1(string? id) =>
        Assert.Equal("INSERT INTO `custom_increment` (`id`,`name`,`address`) VALUES (mysqlNextId(),?,?)", await FailedSql(() =>
            Db.Insert(new CustomIncrement { Id = id, Name = "name", Address = "address" }).ExecuteAsync(Ct)));

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);

    [Fact, Upstream("InsertTest.java#mapInsertTest1")]
    public async Task MapInsertTest1()
    {
        var row = Map(("id", 123), ("name", "小明"));
        Assert.Equal("INSERT INTO `aaaaa` (`id`,`name`) VALUES (?,?)", Db.InsertMap("aaaaa", row).ToSql());
        Assert.Equal("INSERT INTO `aaaaa` (`id`,`name`) VALUES (?,?)", await FailedSql(() => Db.InsertMap("aaaaa", [row]).ExecuteAsync(Ct)));
    }

    [Fact, Upstream("InsertTest.java#mapInsertTest2")]
    public async Task MapInsertTest2() =>
        Assert.Equal("INSERT INTO `aaaaa` (`id`,`name`,`name1`) VALUES (?,?,?)", await FailedSql(() =>
            Db.InsertMap("aaaaa", Map(("id", 123), ("name", "小明"), ("name1", "小明"), ("name2", null))).Columns(ColumnStrategy.OnlyNotNull).ExecuteAsync(Ct)));

    [Fact, Upstream("InsertTest.java#mapInsertTest3")]
    public async Task MapInsertTest3() =>
        Assert.Equal("INSERT INTO `aaaaa` (`id`,`name`,`name1`,`name2`) VALUES (?,?,?,?)", await FailedSql(() =>
            Db.InsertMap("aaaaa", Map(("id", 123), ("name", "小明"), ("name1", "小明"), ("name2", null))).Columns(ColumnStrategy.All).ExecuteAsync(Ct)));

    // 回填自增键：每批的键接着上一批递增
    [Fact, Upstream("InsertTest.java#testIncrement", Deviation = "批量规模缩小（Java 共 1.8 万行，验证的是自增键逐行回填）；C# 每行一条语句，批量写入见 M5.7")]
    public async Task TestIncrement()
    {
        await Db.ExecuteSqlAsync("truncate table t_test_inc;", cancellationToken: Ct);
        var expected = 1;
        foreach (var size in new[] { 100, 191, 299 })
        {
            var rows = Enumerable.Range(0, size).Select(_ => new TestInc { CreateTime = DateTime.Now, Stars = 1, Title = "" }).ToList();
            await Db.Insert(rows).FillGeneratedKeys().ExecuteAsync(Ct);
            foreach (var row in rows) Assert.Equal(expected++, row.Id);
        }
    }

    [Fact, Upstream("InsertTest.java#insertEmpty")]
    public async Task InsertEmpty()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal(0, await Db.Insert(new List<BlogEntity>()).AsSchema("asc").AsTable("123").DisableInterceptors().EnableInterceptors()
            .DisableInterceptor("x").EnableInterceptor("y").Columns(ColumnStrategy.All).ExecuteAsync(Ct));
        Assert.Empty(listen.Events);
    }
}
