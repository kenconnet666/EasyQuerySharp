using System.Linq.Expressions;
using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest4.java（标量子查询比较、JSON 列、函数、first / single、多表的全套条件）。</summary>
public sealed class QueryTest4(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 的 asStr() 只改变类型、不生成 SQL；C# 用 (string)(object) 表达
    [Fact, Upstream("QueryTest4.java#query1233")]
    public void Query1233()
    {
        const string head = "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE t.`create_time` ";
        const string tail = " (SELECT MAX(t1.`create_time`) FROM `t_topic` t1) AND t.`create_time` = ?";
        var max = Db.Query<Topic>().Select(x => Sql.Max(x.CreateTime));
        Assert.Equal(head + "=" + tail, Db.Query<Topic>().Where(o => o.CreateTime == Sql.SubQuery(max) && (string)(object)o.CreateTime! == "1").ToSql());
        Assert.Equal(head + ">=" + tail, Db.Query<Topic>().Where(o => o.CreateTime >= Sql.SubQuery(max) && (string)(object)o.CreateTime! == "1").ToSql());
        Assert.Equal(head + ">" + tail, Db.Query<Topic>().Where(o => o.CreateTime > Sql.SubQuery(max) && (string)(object)o.CreateTime! == "1").ToSql());
        Assert.Equal(head + "<>" + tail, Db.Query<Topic>().Where(o => o.CreateTime != Sql.SubQuery(max) && (string)(object)o.CreateTime! == "1").ToSql());
        Assert.Equal(head + "<=" + tail, Db.Query<Topic>().Where(o => o.CreateTime <= Sql.SubQuery(max) && (string)(object)o.CreateTime! == "1").ToSql());
        Assert.Equal(head + "<" + tail, Db.Query<Topic>().Where(o => o.CreateTime < Sql.SubQuery(max) && (string)(object)o.CreateTime! == "1").ToSql());
    }

    private async Task<TopicTypeArrayJson> SaveArrayJson(string id, params (string Name, int Age)[] items)
    {
        var existing = await Db.Query<TopicTypeArrayJson>().WhereById(id).FirstOrDefaultAsync(Ct);
        if (existing is not null) Assert.Equal(1, await Db.Delete(existing).ExecuteAsync(Ct));
        Assert.Null(await Db.Query<TopicTypeArrayJson>().WhereById(id).FirstOrDefaultAsync(Ct));
        var row = new TopicTypeArrayJson
        {
            Id = id, Stars = 123, Title = new TopicTypeJsonValue { Name = "123", Age = 456 },
            Title2 = items.Select(i => new TopicTypeJsonValue { Name = i.Name, Age = i.Age }).ToList(),
            TopicType = (int)TopicTypeEnum.Classer, CreateTime = DateTime.Now,
        };
        Assert.Equal(1, await Db.Insert(row).ExecuteAsync(Ct));
        var read = await Db.Query<TopicTypeArrayJson>().WhereById(id).FirstAsync(Ct);
        Assert.Equal("123", read.Title!.Name);
        Assert.Equal(456, read.Title.Age);
        Assert.Equal((int)TopicTypeEnum.Classer, read.TopicType);
        return read;
    }

    [Fact, Upstream("QueryTest4.java#testJson1")]
    public async Task TestJson1()
    {
        var read = await SaveArrayJson("1231", ("1234", 4565));
        var item = Assert.Single(read.Title2!);
        Assert.Equal("1234", item.Name);
        Assert.Equal(4565, item.Age);
    }

    // Jackson 与 fastjson 两种序列化器在 C# 里都是 System.Text.Json
    [Fact, Upstream("QueryTest4.java#testJson2Jackson")]
    public async Task TestJson2Jackson()
    {
        var read = await SaveArrayJson("1231", ("1234", 4565), ("12345", 45655));
        Assert.Equal(2, read.Title2!.Count);
        Assert.Equal(("1234", 4565), (read.Title2[0].Name, read.Title2[0].Age!.Value));
        Assert.Equal(("12345", 45655), (read.Title2[1].Name, read.Title2[1].Age!.Value));
    }

    [Fact, Upstream("QueryTest4.java#testSQLFunc1"), Upstream("QueryTest4.java#testSQLFunc2"), Upstream("QueryTest4.java#testSQLFunc2_2")]
    public void TestSQLFunc1() =>
        Assert.Equal("SELECT IFNULL(t.`id`,?) FROM `t_topic` t WHERE t.`id` = ?", Db.Query<Topic>().Where(o => o.Id == "1").Select(o => o.Id ?? "1").ToSql());

    [Fact, Upstream("QueryTest4.java#testSQLFunc2_1", Deviation = "Java 把三个参数直接写进 IFNULL（MySQL 并不接受）；C# 的 a ?? b ?? c 嵌套成 IFNULL(a,IFNULL(b,c))")]
    public void TestSQLFunc2_1() =>
        Assert.Equal("SELECT IFNULL(t.`id`,IFNULL(t.`title`,?)) FROM `t_topic` t WHERE t.`id` = ?",
            Db.Query<Topic>().Where(o => o.Id == "1").Select(o => o.Id ?? o.Title ?? "1").ToSql());

    [Theory, Upstream("QueryTest4.java#testSQLFunc3"), Upstream("QueryTest4.java#testSQLFunc4")]
    [InlineData("yyyy/MM/dd", "%Y/%m/%d")]
    [InlineData("yyyy-MM-dd", "%Y-%m-%d")]
    public void TestSQLFunc3(string format, string mysqlFormat) =>
        Assert.Equal($"SELECT DATE_FORMAT(t.`create_time`,'{mysqlFormat}') FROM `t_topic` t WHERE t.`id` = ?",
            Db.Query<Topic>().Where(o => o.Id == "1").Select(o => o.CreateTime!.Value.ToString(format)).ToSql());

    // Java 断言具体日期（种子数据写入那天）；C# 的固件每次重建数据，这里断言格式与本地格式化一致
    [Fact, Upstream("QueryTest4.java#testSQLFunc5", Deviation = "Java 断言固定日期 2023-05-25（种子数据写入日）；C# 断言与客户端格式化结果一致"),
     Upstream("QueryTest4.java#testSQLFunc6", Deviation = "同 testSQLFunc5")]
    public async Task TestSQLFunc5()
    {
        var expected = (await Db.Query<Topic>().WhereById("1").FirstAsync(Ct)).CreateTime!.Value.ToString("yyyy-MM-dd");
        var list = await Db.Query<Topic>().Where(o => o.Id == "1").OrderByDescending(o => o.CreateTime).Select(o => o.CreateTime!.Value.ToString("yyyy-MM-dd")).ToListAsync(Ct);
        Assert.All(list, s => Assert.Equal(expected, s));
    }

    [Fact, Upstream("QueryTest4.java#testSQLFunc7", Deviation = "同 testSQLFunc5")]
    public async Task TestSQLFunc7()
    {
        var query = Db.Query<Topic>().Where(o => o.Id == "1").OrderByDescending(o => o.CreateTime).Select(o => new Topic { Title = o.CreateTime!.Value.ToString("yyyy-MM-dd") });
        Assert.Equal("SELECT DATE_FORMAT(t.`create_time`,'%Y-%m-%d') AS `title` FROM `t_topic` t WHERE t.`id` = ? ORDER BY t.`create_time` DESC", query.ToSql());
        var expected = (await Db.Query<Topic>().WhereById("1").FirstAsync(Ct)).CreateTime!.Value.ToString("yyyy-MM-dd");
        Assert.All(await query.ToListAsync(Ct), t => Assert.Equal(expected, t.Title));
    }

    [Fact, Upstream("QueryTest4.java#testSQLFunc8", NotApplicable = "对日期列取 ABS 在 C# 里写不出来（Math.Abs 不接受 DateTime）")]
    public void TestSQLFunc8() { }

    [Fact, Upstream("QueryTest4.java#testSQLFunc9", Deviation = "Java 客户端 API 的 fx().round 把位数写成字面量；C# 与 Java 实体 API 的 round(n) 一致，位数参数化")]
    public void TestSQLFunc9() =>
        Assert.Equal("SELECT ROUND(t.`stars`,?) AS `title` FROM `t_topic` t WHERE t.`id` = ? ORDER BY t.`create_time` DESC",
            Db.Query<Topic>().Where(o => o.Id == "1").OrderByDescending(o => o.CreateTime).Select(o => new { Title = Math.Round((decimal)o.Stars!, 1) }).ToSql());

    [Fact, Upstream("QueryTest4.java#testSQLFunc10")]
    public void TestSQLFunc10() =>
        Assert.Equal("SELECT CONCAT(t.`stars`,t.`id`) AS `title` FROM `t_topic` t WHERE t.`id` = ? ORDER BY t.`create_time` DESC",
            Db.Query<Topic>().Where(o => o.Id == "1").OrderByDescending(o => o.CreateTime).Select(o => new Topic { Title = o.Stars + o.Id }).ToSql());

    [Fact, Upstream("QueryTest4.java#testSQLFunc12")]
    public void TestSQLFunc12() =>
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE IFNULL(`id`,?) = IFNULL(`create_time`,?)",
            Db.Query<Topic>().Where(t => (object)(t.Id ?? "123") == (object?)(t.CreateTime ?? (DateTime?)null)).ToSql());

    [Fact, Upstream("QueryTest4.java#testSQLFunc12_1")]
    public void TestSQLFunc12_1() =>
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE IFNULL(`id`,?) = DATE_FORMAT(`create_time`,'%Y/%m/%d %H:%i:%s')",
            Db.Query<Topic>().Where(t => (t.Id ?? "123") == t.CreateTime!.Value.ToString("yyyy/MM/dd HH:mm:ss")).ToSql());

    [Fact, Upstream("QueryTest4.java#queryFirstSingle")]
    public async Task QueryFirstSingle()
    {
        const string sql = "SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`sys_123` WHERE `id` = ?";
        var query = Db.Query<SysUser>().AsTable("sys_123").Where(o => o.Id == "123xxx");
        Assert.Equal(sql + " LIMIT 1", (await Assert.ThrowsAsync<EasyQueryException>(() => query.FirstOrDefaultAsync(Ct))).Sql);
        Assert.Equal(sql, (await Assert.ThrowsAsync<EasyQueryException>(() => query.SingleOrDefaultAsync(Ct))).Sql);
        // 多于一行时 SingleOrDefault 报错（Java 的 EasyQuerySingleMoreElementException）
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<SysUser>().SingleOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest4.java#queryTest7")]
    public async Task QueryTest7()
    {
        Assert.Null(await Db.Query<Topic>().Where(o => o.Id == "123").FirstOrDefaultAsync(Ct));
        var topic = await Db.Query<Topic>().Where(o => o.Id == "1").Select(o => new Topic { Id = o.Id, Title = o.Title }).FirstOrDefaultAsync(Ct);
        Assert.NotNull(topic);
        Assert.NotNull(topic.Id);
        Assert.NotNull(topic.Title);
        Assert.Null(topic.Stars);
        Assert.Null(topic.CreateTime);
        var joined = await Db.Query<Topic>().Where(o => o.Id == "1").Select(o => new Topic { Id = o.Id, Title = o.Title })
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id)
            .Where((t, t1) => t1.Id == "123" && t.Id == "456")
            .ToListAsync(Ct);
        Assert.Empty(joined);
        Assert.Null(await Db.Query<Topic>().Where(o => o.Id == "123").SingleOrDefaultAsync(Ct));
        // Java 的 firstNotNull / singleNotNull：没有数据时抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<Topic>().Where(o => o.Id == "123").FirstAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Query<Topic>().Where(o => o.Id == "123").SingleAsync(Ct));
        Assert.NotNull(await Db.Query<Topic>().Where(o => o.Id == "1").SingleAsync(Ct));
    }

    // ---------- joinTest：多表上的全套条件、排序、分组 ----------

    // Java 的 whereMerge 里对第一张表的全部比较 / LIKE 变体（带 false 的那一半不生效）
    private static readonly Expression<Func<Topic, bool>> AllComparisons = o =>
        o.Id == "1" && o.Id != "1" && string.Compare(o.Id, "1") >= 0 && string.Compare(o.Id, "1") > 0
        && string.Compare(o.Id, "1") <= 0 && string.Compare(o.Id, "1") < 0
        && o.Id!.Contains("1") && !o.Id.Contains("1") && o.Id.StartsWith("1") && o.Id.EndsWith("1")
        && !o.Id.StartsWith("1") && !o.Id.EndsWith("1");

    private static string JoinSql(int tables) =>
        "SELECT t.`id`,COUNT(t.`id`) AS `stars` FROM `t_topic` t"
        + string.Concat(Enumerable.Range(1, tables - 1).Select(i => $" LEFT JOIN `t_topic` t{i} ON t.`id` = t{i}.`id`"))
        + " WHERE t.`id` = ? AND t.`id` = ? AND t.`id` = ? AND t.`create_time` > ? AND t.`id` = ? AND t.`id` <> ? AND t.`id` >= ? AND t.`id` > ? AND t.`id` <= ? AND t.`id` < ?"
        + " AND t.`id` LIKE ? AND t.`id` NOT LIKE ? AND t.`id` LIKE ? AND t.`id` LIKE ? AND t.`id` NOT LIKE ? AND t.`id` NOT LIKE ?"
        + " GROUP BY t.`id` HAVING COUNT(t.`id`) >= ? ORDER BY t.`create_time` ASC,t.`create_time` DESC,t.`create_time` ASC,t.`create_time` DESC LIMIT 2 OFFSET 1";

    private static TopicRequest Request() => new() { CreateTimeBegin = DateTime.Now };

    [Fact, Upstream("QueryTest4.java#joinTest1", Deviation = "Java 的 id 是字符串，ge / gt 等比较在 C# 里写成 string.Compare(..) >= 0")]
    public void JoinTest1() =>
        Assert.Equal(JoinSql(10), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).LeftJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, _, _, t9) => t.Id == t9.Id)
            .Where(o => o.Id == "1").WhereIf(false, o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" })
            .FilterBy(Request()).FilterByIf(false, Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _, _, _, _) => t.CreateTime)
            .OrderByIf(false, (t, _, _, _, _, _, _, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L).HavingIf(false, g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());

    [Fact, Upstream("QueryTest4.java#joinTest2", Deviation = "同 joinTest1")]
    public void JoinTest2() =>
        Assert.Equal(JoinSql(9), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id).LeftJoin<Topic>((t, _, _, _, _, _, _, _, t8) => t.Id == t8.Id)
            .Where(o => o.Id == "1").WhereIf(false, o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" })
            .FilterBy(Request()).FilterByIf(false, Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());

    [Fact, Upstream("QueryTest4.java#joinTest3", Deviation = "同 joinTest1")]
    public void JoinTest3() =>
        Assert.Equal(JoinSql(8), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .LeftJoin<Topic>((t, _, _, _, _, _, _, t7) => t.Id == t7.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _, _, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());

    [Fact, Upstream("QueryTest4.java#joinTest4", Deviation = "同 joinTest1")]
    public void JoinTest4() =>
        Assert.Equal(JoinSql(7), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id).LeftJoin<Topic>((t, _, _, _, _, _, t6) => t.Id == t6.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());

    [Fact, Upstream("QueryTest4.java#joinTest5", Deviation = "同 joinTest1")]
    public void JoinTest5() =>
        Assert.Equal(JoinSql(6), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .LeftJoin<Topic>((t, _, _, _, _, t5) => t.Id == t5.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());

    [Fact, Upstream("QueryTest4.java#joinTest6", Deviation = "同 joinTest1")]
    public void JoinTest6() =>
        Assert.Equal(JoinSql(5), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id).LeftJoin<Topic>((t, _, _, _, t4) => t.Id == t4.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());

    [Fact, Upstream("QueryTest4.java#joinTest7", Deviation = "同 joinTest1")]
    public void JoinTest7() =>
        Assert.Equal(JoinSql(4), Db.Query<Topic>()
            .LeftJoin<Topic>((t, t1) => t.Id == t1.Id).LeftJoin<Topic>((t, t1, t2) => t.Id == t2.Id)
            .LeftJoin<Topic>((t, _, _, t3) => t.Id == t3.Id)
            .Where(o => o.Id == "1").WhereById("1").WhereByIds(new[] { "1" }).FilterBy(Request()).Where(AllComparisons)
            .Limit(1, 2)
            .OrderBy((t, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _) => t.CreateTime)
            .OrderBy((t, _, _, _) => t.CreateTime).OrderByDescending((t, _, _, _) => t.CreateTime)
            .GroupBy((t, _, _, _) => t.Id)
            .Having(g => g.Count(e => e.Item1.Id) >= 1L)
            .Select(g => new Topic { Id = g.Column(e => e.Item1.Id), Stars = (int)g.Count(e => e.Item1.Id) })
            .ToSql());
}
