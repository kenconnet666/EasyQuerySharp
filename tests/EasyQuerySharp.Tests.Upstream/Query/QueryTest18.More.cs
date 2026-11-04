using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>QueryTest18 的其余用例：只执行不断言 SQL 的用例照样执行一遍，保证能翻译、能跑。</summary>
public sealed partial class QueryTest18
{
    // Java 的 o.expression().sqlSegment("IFNULL({0},'')", …) / setSQL：C# 的 Sql.Raw
    [Fact, Upstream("QueryTest18.java#test9"), Upstream("QueryTest18.java#test10")]
    public async Task Test9() =>
        Assert.NotNull(await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01")
            .Select(o => new Topic { Title = (o.Stars ?? 0).ToString(), Alias = Sql.Raw<string>("IFNULL({0},'')", o.Id) }).ToListAsync(Ct));

    // Java 用字符串列名动态选列；C# 是同一个投影 API
    [Fact, Upstream("QueryTest18.java#test15")]
    public async Task Test15() =>
        Assert.NotEmpty(await Db.Query<Topic>().Select(t => new Topic { Id = t.Id ?? "123", Stars = t.Stars, Title = t.Title }).ToListAsync(Ct));

    [Fact, Upstream("QueryTest18.java#test")]
    public async Task Test()
    {
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t2) => b.Id == t2.Id)
            .Select((b1, t2) => new Draft<string?, string?, decimal?>(b1.Id, t2.Title, b1.Score)).Distinct().ToListAsync(Ct));
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t2) => b.Id == t2.Id)
            .Select((b1, t2) => new BlogEntity { Id = b1.Id, Content = b1.Content, Order = b1.Order }).Distinct().ToListAsync(Ct));
        Assert.NotNull(await Db.Query<BlogEntity>().Where(b => b.Star == 1).Select(b => new BlogEntity { Content = b.Content, Order = b.Order }).Distinct().ToListAsync(Ct));
    }

    // Java 的 sumOrDefault(x, 默认值)：C# 的 SumAsync 没有行时返回 0（可空时 null，?? 默认值）
    [Fact, Upstream("QueryTest18.java#test1xa")]
    public async Task Test1xa()
    {
        Assert.True(await Db.Query<Topic>().SumAsync(t => (decimal?)(t.Stars ?? 0), Ct) is null or >= 0);
        Assert.True(await Db.Query<Topic>().SumAsync(t => t.Stars ?? 0, Ct) >= 0);
    }

    [Fact, Upstream("QueryTest18.java#testFETCHER")]
    public async Task TestFETCHER()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicFile>().Where(t => t.Id == "123").Select(t => new TopicFile { Id = t.Id, Stars = t.Stars }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars` FROM `t_topic` t WHERE t.`id` = ?");
        listen.AssertParams("123(String)");
    }

    // Java 的 subString(begin, length)：begin 是 0 基，SQL 里加 1
    [Fact, Upstream("QueryTest18.java#testSubString")]
    public async Task TestSubString()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Id!.Substring(b.Star!.Value, b.Title!.Length - 5) == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND SUBSTR(`id`,`star`+1,(CHAR_LENGTH(`title`) - ?)) = ?");
        listen.AssertParams("false(Boolean),5(Integer),123(String)");
    }

    [Fact, Upstream("QueryTest18.java#test1111")]
    public async Task Test1111() =>
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t2) => b.Id == t2.Id).Where((b1, t2) => b1.Star == 1)
            .GroupBy((b1, t2) => new { b1.Title, b1.Content })
            .Select(g => new MyVO { Title = g.Key.Title, Content = g.Key.Content, C = g.Count(e => e.Item2.Id) }).ToListAsync(Ct));

    [Fact, Upstream("QueryTest18.java#autoFalse")]
    public async Task AutoFalse() =>
        Assert.All(await Db.Query<Topic>().Select<TopicAutoFalse>().ToListAsync(Ct), t => Assert.Null(t.Alias));

    [Fact, Upstream("QueryTest18.java#testEnum")]
    public async Task TestEnum() =>
        await Db.Query<TopicTypeTest1>().Select(t => t.TopicType).FirstOrDefaultAsync(Ct);

    [Fact, Upstream("QueryTest18.java#testCount")]
    public async Task TestCount()
    {
        Assert.Equal(100, await Db.Query<BlogEntity>().Select(t => new Draft<string?>(t.Id)).Distinct().CountAsync(Ct));
        Assert.True(await Db.Query<BlogEntity>().Select(t => new Draft<string?>(t.Id)).Distinct().AnyAsync(Ct));
    }

    // Java 的 b.and(() -> …)：C# 的 Sql.Group(…)
    [Fact, Upstream("QueryTest18.java#andTest")]
    public async Task AndTest()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Star <= 1 && b.Title!.Contains("123")).ToListAsync(Ct);
        await Db.Query<BlogEntity>().Where(b => Sql.Group(b.Star <= 1 && b.Title!.Contains("123"))).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND (`star` <= ? AND `title` LIKE ?)");
    }

    [Fact, Upstream("QueryTest18.java#Sum")]
    public async Task Sum() =>
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin<BlogEntity>((b, b2) => b.CreateTime < b2.CreateTime)
            .GroupBy((b1, b2) => b1.CreateTime!.Value.ToString("yyyyMMdd"))
            .Select(g => new Draft<string, decimal?>(g.Key, g.Sum(e => e.Item2.Score))).ToListAsync(Ct));

    // 同一个分组查询作为两边做自连接：每月累计（月份小于等于自身的所有月份之和）
    [Fact, Upstream("QueryTest18.java#Sum1")]
    public async Task Sum1()
    {
        var monthQuery = Db.Query<BlogEntity>().GroupBy(b => int.Parse(b.CreateTime!.Value.ToString("yyyyMM")))
            .Select(g => new Draft<int, decimal?>(g.Key, g.Sum(e => e.Score)));
        Assert.NotNull(await monthQuery.LeftJoin(monthQuery, (b, b2) => b.Value1 < b2.Value1)
            .GroupBy((b1, b2) => b1.Value1)
            .Select(g => new Draft<int, decimal?>(g.Key, g.Sum(e => e.Item2.Value2))).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest18.java#easyQuery"), Upstream("QueryTest18.java#easyQueryPrint1")]
    public async Task EasyQuery()
    {
        Assert.NotNull(await Db.Query<BlogEntity>().Select(b => b.CreateTime!.Value.ToString("yyyy年MM约-dd HH小时mm分钟ss秒")).FirstOrDefaultAsync(Ct));
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(b => b.Id).ToListAsync(Ct));
    }

    // ClickHouse 的 FINAL：Java 的 asTableSegment，MySQL 上执行会报错（与 Java 一样只看 SQL）
    [Fact, Upstream("QueryTest18.java#testFinal")]
    public async Task TestFinal()
    {
        using var listen = SqlCapture.Listen();
        await Assert.ThrowsAsync<EasyQueryException>(() => Db.Query<BlogEntity>()
            .AsTableSegment((table, alias) => alias is null ? table + " FINAL" : table + " " + alias + " FINAL").AsAlias("a")
            .Where(b => b.Star == 123).ToListAsync(Ct));
        listen.AssertSql("SELECT a.`id`,a.`create_time`,a.`update_time`,a.`create_by`,a.`update_by`,a.`deleted`,a.`title`,a.`content`,a.`url`,a.`star`,a.`publish_time`,a.`score`,a.`status`,a.`order`,a.`is_top`,a.`top` FROM `t_blog` a FINAL WHERE a.`deleted` = ? AND a.`star` = ?");
        listen.AssertParams("false(Boolean),123(Integer)");
    }

    // Java 的 caseWhen(…).then(0).elseEnd(…)：C# 的三元运算
    [Fact, Upstream("QueryTest18.java#testUpdate")]
    public async Task TestUpdate()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<Topic>().Set(t => new Topic { Stars = (decimal?)(t.Stars - 5) < 0m ? 0 : t.Stars - 5 }).Where(t => t.Id == "123xxxa").ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_topic` SET `stars` = (CASE WHEN (`stars` - ?) < ? THEN ? ELSE (`stars` - ?) END) WHERE `id` = ?");
        listen.AssertParams("5(Integer),0(BigDecimal),0(Integer),5(Integer),123xxxa(String)");
    }

    // Java 的 setIgnoreColumns / whereColumns(columnKeys() …)：C# 的 IgnoreColumns / WhereColumns
    [Fact, Upstream("QueryTest18.java#test123")]
    public async Task Test123()
    {
        var blog = new BlogEntity { Id = "123" };
        Assert.Equal(0, await Db.Update(blog).ExecuteAsync(Ct));
        using var listen = SqlCapture.Listen();
        Assert.Equal(0, await Db.Update(blog).IgnoreColumns(b => new { b.CreateTime, b.CreateBy, b.UpdateTime, b.UpdateBy })
            .WhereColumns(b => new { b.Id, b.Order, b.Content }).ExecuteAsync(Ct));
        Assert.DoesNotContain("`create_time` = ?", listen.Last.Statement.Text, StringComparison.Ordinal);
        Assert.EndsWith("WHERE `deleted` = ? AND `id` = ? AND `content` IS NULL AND `order` IS NULL", listen.Last.Statement.Text, StringComparison.Ordinal);
    }

    // Java 的 sqlConversion（非真实列）：C# 的 [Computed] 计算列，SELECT / WHERE / ORDER BY 里就地展开
    [Fact, Upstream("QueryTest18.java#testStarStr")]
    public async Task TestStarStr()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Blog2Entity>().Where(b => b.Star == 1 && b.StarStr == "1").OrderBy(b => b.Star).ThenBy(b => b.StarStr).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`star`,CAST(`star` AS CHAR) AS `star_str` FROM `t_blog` WHERE `deleted` = ? AND `star` = ? AND CAST(`star` AS CHAR) = ? ORDER BY `star` ASC,CAST(`star` AS CHAR) ASC");
        listen.AssertParams("false(Boolean),1(Integer),1(String)");
        Assert.All(list, b => Assert.Equal("1", b.StarStr));
    }
}
