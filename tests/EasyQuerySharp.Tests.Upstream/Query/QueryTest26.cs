using EasyQuerySharp.Dialects;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：QueryTest26.java（读出后在客户端换算、原生条件片段、按偏移分批读取、IN 列表按上限拆分等）。
/// Java 的 valueConvert 对应 C# 的 <c>Sql.ClientSide(值, 换算)</c>；offsetChunk 对应 <c>OffsetChunkAsync</c>；maxInClauseSize 对应 <c>EasyDbOptions.MaxInClauseSize</c>。
/// </summary>
public sealed class QueryTest26(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("QueryTest26.java#test1")]
    public async Task Test1()
    {
        var list = await Db.Query<Topic>().Select(t => new Topic
        {
            Stars = t.Stars,
            Title = Sql.ClientSide(t.Stars, s => s + "-"),
            CreateTime = t.CreateTime,
            Id = Sql.ClientSide(t.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss"), s => s + ".123"),
        }).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, t =>
        {
            Assert.Equal(t.Stars + "-", t.Title);
            Assert.Equal(t.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + ".123", t.Id);
        });
    }

    /// <summary>对应 Java 用例里的 UserABC。</summary>
    public sealed class UserABC
    {
        public string? CreateTimeStr { get; set; }

        public DateTime? CreateTime { get; set; }

        public string? StarsStr { get; set; }

        public int? Stars { get; set; }
    }

    private static string Month(DateTime? time) => time!.Value.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    [Fact, Upstream("QueryTest26.java#test2")]
    public async Task Test2()
    {
        var list = await Db.Query<Topic>().Select(t => new UserABC
        {
            CreateTimeStr = Sql.ClientSide(t.CreateTime, Month),
            StarsStr = Sql.ClientSide(t.Stars, d => d + 1 + "aaa"),
            CreateTime = t.CreateTime,
            Stars = t.Stars,
        }).ToListAsync(Ct);
        Assert.All(list, item =>
        {
            Assert.Equal(item.Stars + 1 + "aaa", item.StarsStr);
            Assert.Equal(Month(item.CreateTime), item.CreateTimeStr);
        });
    }

    // Java 的 Draft4 / Tuple4 写法相同
    [Fact, Upstream("QueryTest26.java#test3"), Upstream("QueryTest26.java#test4")]
    public async Task Test3()
    {
        var list = await Db.Query<Topic>().Select(t => new Draft<string, string, DateTime?, int?>(
            Sql.ClientSide(t.CreateTime, Month), Sql.ClientSide(t.Stars, d => d + 1 + "aaa"), t.CreateTime, t.Stars)).ToListAsync(Ct);
        Assert.All(list, item =>
        {
            Assert.Equal(item.Value4 + 1 + "aaa", item.Value2);
            Assert.Equal(Month(item.Value3), item.Value1);
        });
    }

    // 先在 SQL 里格式化，读出后再拼接
    [Fact, Upstream("QueryTest26.java#test5"), Upstream("QueryTest26.java#test6")]
    public async Task Test5()
    {
        var list = await Db.Query<Topic>().Select(t => new UserABC
        {
            CreateTimeStr = Sql.ClientSide(t.CreateTime!.Value.ToString("yyyy-MM"), d => d + "2"),
            StarsStr = Sql.ClientSide(t.Stars, d => d + 1 + "aaa"),
            CreateTime = t.CreateTime,
            Stars = t.Stars,
        }).ToListAsync(Ct);
        Assert.All(list, item => Assert.Equal(Month(item.CreateTime) + "2", item.CreateTimeStr));
    }

    // publish_time 为 NULL：Java 拼出 "null1"，C# 的 null 字符串拼接是 "1"
    [Fact, Upstream("QueryTest26.java#test7", Deviation = "NULL 值拼接：Java 得到 \"null1\"，C# 得到 \"1\"")]
    public async Task Test7()
    {
        var list = await Db.Query<BlogEntity>().Select(t => new Draft<string, string, DateTime?, DateTime?>(
            Sql.ClientSide(t.CreateTime!.Value.ToString("yyyy-MM"), d => d + "1"), Sql.ClientSide(t.PublishTime!.Value.ToString("yyyy-MM"), d => d + "1"),
            t.CreateTime, t.PublishTime)).ToListAsync(Ct);
        Assert.All(list, item =>
        {
            if (item.Value4 is null) Assert.Equal("1", item.Value2);
            Assert.Equal(Month(item.Value3) + "1", item.Value1);
        });
    }

    // Java 的 expression().sql("find_in_set({0},{1})", ..)：C# 的 Sql.Raw<bool>
    [Fact, Upstream("QueryTest26.java#testFindInSet")]
    public async Task TestFindInSet()
    {
        using var listen = SqlCapture.Listen();
        var ids = Db.Query<Topic>().Where(t => t.Id == "123" && Sql.Raw<bool>("find_in_set({0},{1})", 104, t.Title)).Select(t => t.Id);
        await Db.Query<BlogEntity>().Where(b => b.Id.In(ids)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` IN (SELECT t1.`id` FROM `t_topic` t1 WHERE t1.`id` = ? AND find_in_set(?,t1.`title`))");
        listen.AssertParams("false(Boolean),123(String),104(Integer)");
    }

    // Java 的 ClassProxy.field("title").set(topic.id())：成员互换
    [Fact, Upstream("QueryTest26.java#testaa1")]
    public async Task Testaa1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.Id != null).Select(t => new TopicTypeVO { Title = t.Id, Id = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `title`,t.`title` AS `id` FROM `t_topic` t WHERE t.`id` IS NOT NULL");
    }

    // 投影常量：参数化后原样读回
    [Fact, Upstream("QueryTest26.java#testSetConstant")]
    public async Task TestSetConstant()
    {
        var list = await Db.Query<Topic>().Select(t => new BlogEntity { Id = "123" }).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, b => Assert.Equal("123", b.Id));
    }

    // Java 只跑不断言：字符串聚合里的 CASE
    [Fact, Upstream("QueryTest26.java#joiningTest")]
    public async Task JoiningTest() =>
        await Db.Query<BlogEntity>().GroupBy(b => b.Title).Select(g => new Draft<string?, string, string>(g.Key,
            g.StringJoin(s => s.Score == 0m ? s.Content + s.Url : null, "|"), g.StringJoin(s => s.Score == 1m ? s.Content + s.Url : null, "|"))).ToListAsync(Ct);

    // Java 的 newMap().put(1, ..).put(2, ..).getOrDefault(content, null)：C# 写成条件运算符
    [Fact, Upstream("QueryTest26.java#switchCase", Deviation = "参数类型：Java 的 Map 键是整数 1、2，C# 与字符串列比较时写成字符串")]
    public async Task SwitchCase()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().OrderBy(b => b.Content == "1" ? b.Title : b.Content == "2" ? "3" : null).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? ORDER BY (CASE WHEN `content` = ? THEN `title` WHEN `content` = ? THEN ? ELSE NULL END) ASC");
        listen.AssertParams("false(Boolean),1(String),2(String),3(String)");
    }

    private Query<BlogEntity> OrderedBlogs() => Db.Query<BlogEntity>().OrderBy(b => b.CreateTime).OrderBy(b => b.Id);

    // 每批最多 3 行，偏移按读到的行数前进，读完 100 行
    [Fact, Upstream("QueryTest26.java#testChunk3"), Upstream("QueryTest26.java#testChunk4")]
    public async Task TestChunk3()
    {
        var ids = new HashSet<string>();
        await OrderedBlogs().OffsetChunkAsync(3, chunk =>
        {
            Assert.True(chunk.Count <= 3);
            foreach (var blog in chunk) Assert.True(ids.Add(blog.Id!), "id 重复：" + blog.Id);
            return chunk.Count;
        }, cancellationToken: Ct);
        Assert.Equal(await Db.Query<BlogEntity>().CountAsync(Ct), ids.Count);
    }

    // 偏移不前进时靠最大读取行数结束（默认 100000）
    [Fact, Upstream("QueryTest26.java#testChunk5")]
    public async Task TestChunk5()
    {
        var count = 0;
        await OrderedBlogs().OffsetChunkAsync(100, chunk =>
        {
            count += chunk.Count;
            return 0;
        }, cancellationToken: Ct);
        Assert.Equal(100_000, count);
    }

    [Fact, Upstream("QueryTest26.java#testChunk6"), Upstream("QueryTest26.java#testChunk7")]
    public async Task TestChunk6()
    {
        var count = 0;
        await OrderedBlogs().OffsetChunkAsync(100, chunk =>
        {
            count += chunk.Count;
            return 0;
        }, maxFetchSize: 100_001, cancellationToken: Ct);
        Assert.Equal(100_001, count);
        count = 0;
        await OrderedBlogs().Select(b => new Draft<string?>(b.Title)).OffsetChunkAsync(100, chunk =>
        {
            count += chunk.Count;
            return 0;
        }, maxFetchSize: 100_001, cancellationToken: Ct);
        Assert.Equal(100_001, count);
    }

    // IN 列表超过上限时按上限拆成 OR 连接的多个 IN（只比对 SQL，用 Oracle 方言）
    [Fact, Upstream("QueryTest26.java#testMaxInCaluse", Deviation = "Java 的 contains 在 Oracle 上写成 LIKE ('%'||TO_CHAR(?)||'%')，C# 的 string.Contains 写成 LIKE ?（值里带好 %）")]
    public void TestMaxInClause()
    {
        var db = new EasyDb(o =>
        {
            o.MaxInClauseSize = 10;
            o.Use(OracleDialect.Instance);
        });
        var ids = Enumerable.Range(0, 21).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        const string select = "SELECT \"id\",\"stars\",\"title\",\"create_time\" FROM \"t_topic\" WHERE ";
        const string idIn = "(\"id\" IN (?,?,?,?,?,?,?,?,?,?) OR \"id\" IN (?,?,?,?,?,?,?,?,?,?) OR \"id\" IN (?))";
        Assert.Equal(select + idIn, db.Query<Topic>().WhereByIds(ids).ToSql());
        Assert.Equal(select + idIn, db.Query<Topic>().Where(t => ids.Contains(t.Id!)).ToSql());
        Assert.Equal(select + "\"title\" LIKE ? AND " + idIn, db.Query<Topic>().Where(t => t.Title!.Contains("123") && ids.Contains(t.Id!)).ToSql());
        Assert.Equal(select + idIn + " AND (\"title\" LIKE ? OR " + idIn + " OR " + idIn + ")",
            db.Query<Topic>().Where(t => ids.Contains(t.Id!) && (t.Title!.Contains("123") || ids.Contains(t.Id!) || ids.Contains(t.Id!))).ToSql());
        Assert.Equal(select + "(" + idIn + " AND (\"title\" LIKE ? OR " + idIn + " OR " + idIn + "))",
            db.Query<Topic>().Where(t => Sql.Group(ids.Contains(t.Id!) && (t.Title!.Contains("123") || ids.Contains(t.Id!) || ids.Contains(t.Id!)))).ToSql());
    }

    [Fact, Upstream("QueryTest26.java#test", NotApplicable = "测的是 Java 的 Linq / Stream 在内存里分组，与查询无关")]
    public void Test() => Assert.Empty(Array.Empty<int>().GroupBy(x => x));
}
