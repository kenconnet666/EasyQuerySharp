namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest25.java（排序里的 CASE、元组投影、原生片段列、Map 投影后再过滤、Part 投影、部分列加别名列、排除列）。
/// Java 的 Select.TUPLE.of(..) 与 Draft 写法生成的 SQL 相同，C# 都用 <c>Draft&lt;…&gt;</c>；缓存相关用例（easyCacheClient）随二级缓存（7.7）移植。
/// </summary>
public sealed class QueryTest25(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 只跑不断言：排序里多分支的 CASE
    [Fact, Upstream("QueryTest25.java#testOrderCaseWhen")]
    public async Task TestOrderCaseWhen() =>
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == b.Id).LeftJoin<BlogEntity>((t, b, b1) => t.Id == b.Id)
            .OrderBy((t, b, b1) => b.Title!.Contains("123") ? 1 : b.Content!.Contains("456") ? 2 : b.Title!.Contains("4561") ? 3 : b.Title!.Contains("4561") ? 4 : 5)
            .Select((t, b, b1) => t).ToListAsync(Ct);

    // Java 只跑不断言：投影到 VO 的部分列
    [Fact, Upstream("QueryTest25.java#test11")]
    public async Task Test11() =>
        await Db.Query<Topic>().Where(t => t.Title!.Contains("123")).Select(t => new TopicTypeVO { Id = t.Id, Stars = t.Stars }).ToListAsync(Ct);

    [Fact, Upstream("QueryTest25.java#test12", Deviation = "Java 按表名反查实体元数据（getEntityMetadataList）；C# 的元数据按类型取，这里核对表名")]
    public async Task Test12()
    {
        await Db.Query<Topic>().Where(t => t.Title!.Contains("123")).ToListAsync(Ct);
        Assert.Equal("t_topic", Db.Metadata.Get<Topic>().TableName);
    }

    // 库里没有 test_a 等表：两边都执行到 SQL 为止，报表不存在
    [Fact, Upstream("QueryTest25.java#testToListFlat1", Deviation = "Java 的 toList(flatElement…) 先只查关联键、再逐层带出并在内存里去重；C# 的 SelectMany 按 LINQ 语义写成一条 INNER JOIN（保留重复，去重加 Distinct）")]
    public async Task TestToListFlat1()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<TestA>().Where(t => t.Aname!.Contains("123"))
            .SelectMany(t => t.BList).SelectMany(b => b.CList).Select(c => c.Cname).ToListAsync(Ct));
        Assert.Contains("test_a' doesn't exist", ex.Message);
    }

    [Fact, Upstream("QueryTest25.java#tupleTest1")]
    public async Task TupleTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?>(g.Key)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest2")]
    public async Task TupleTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => new { t.Id, t.Title }).Select(g => new Draft<string?>(g.Key.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`,t.`title`");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest3")]
    public async Task TupleTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?>(g.Key)).Where(d => d.Value1 == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`value1` AS `value1` FROM (SELECT t.`id` AS `value1` FROM `t_topic` t GROUP BY t.`id`) t1 WHERE t1.`value1` = ?");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest4")]
    public async Task TupleTest4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t GROUP BY t.`id`");
    }

    [Fact, Upstream("QueryTest25.java#tupleTest5")]
    public async Task TupleTest5()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long, int?>(g.Key, g.Count(), g.Sum(s => s.Stars))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2`,SUM(t.`stars`) AS `value3` FROM `t_topic` t GROUP BY t.`id`");
        Assert.NotEmpty(list);
    }

    [Fact, Upstream("QueryTest25.java#tupleTest7")]
    public async Task TupleTest7()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?>(t.Id, t.CreateTime)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
        Assert.NotNull(list[0].Value2);
    }

    // 原生片段作为一列：数字 1 按字符串读回
    [Fact, Upstream("QueryTest25.java#tupleTest8")]
    public async Task TupleTest8()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(t => new Draft<string?, DateTime?, string>(t.Id, t.CreateTime, Sql.Raw<string>("1"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2`,1 AS `value3` FROM `t_blog` t WHERE t.`deleted` = ?");
        Assert.Equal("1", list[0].Value3);
    }

    [Fact, Upstream("QueryTest25.java#tupleTest9")]
    public async Task TupleTest9()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Where(t => t.Id == "0")
            .Select(t => new Draft<string?, DateTime?, string>(t.Id, t.CreateTime, Sql.Raw<string>("IFNULL({0},'1')", t.Title))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`create_time` AS `value2`,IFNULL(t.`title`,'1') AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ?");
        Assert.Equal("title0", Assert.Single(list).Value3);
    }

    // Map 投影后再过滤：(string?)d["aa"] 就是派生表的 aa 列（Java 的 o.get("aa") / o.get(MapKey)）
    [Fact, Upstream("QueryTest25.java#testMap"), Upstream("QueryTest25.java#testMap1")]
    public async Task TestMap()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<BlogEntity>().Select(b => new Dictionary<string, object?> { { "aa", b.Id }, { "bb", b.Star } })
            .Where(d => (string?)d["aa"] == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`aa` AS `aa`,t1.`bb` AS `bb` FROM (SELECT t.`id` AS `aa`,t.`star` AS `bb` FROM `t_blog` t WHERE t.`deleted` = ?) t1 WHERE t1.`aa` = ?");
        listen.AssertParams("false(Boolean),123(String)");
        Assert.All(rows, r => Assert.Equal(2, r.Count));
        // Java 的 Select.PART.of(t_blog, title.concat("123")) 后按计算列和实体列过滤
        await Db.Query<BlogEntity>().Select(b => new Part<BlogEntity, string>(b, b.Title + "123")).Where(p => p.Value1 == "123" && p.Entity.Star == 1).ToListAsync(Ct);
    }

    // 部分列 + 别名列：同名不写 AS，content 来自另一张表写 AS
    [Fact, Upstream("QueryTest25.java#testDraft"), Upstream("QueryTest25.java#testDraft2")]
    public async Task TestDraft()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == (string?)(object?)b.Star).Select((t, b) => new BlogEntity { Id = b.Id, Star = b.Star, Content = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id`,t1.`star`,t.`title` AS `content` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`star`");
        listen.AssertParams("false(Boolean)");
    }

    // Java 的 toList(t -> t.id()) 与 selectColumn(..).toList() 结果一致
    [Fact, Upstream("QueryTest25.java#testList")]
    public async Task TestList()
    {
        var list1 = await Db.Query<Topic>().OrderBy(t => t.Id).Select(t => t.Id).ToListAsync(Ct);
        var list2 = (await Db.Query<Topic>().OrderBy(t => t.Id).ToListAsync(Ct)).Select(t => t.Id).ToList();
        Assert.NotEmpty(list1);
        Assert.Equal(list1, list2);
    }

    // Java 的 t_topic.selectIgnores(t_topic.id())：C# 的 SelectExcept
    [Fact, Upstream("QueryTest25.java#testOnlyIgnore"), Upstream("QueryTest25.java#testOnlyIgnore2")]
    public async Task TestOnlyIgnore()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().SelectExcept<Topic>(t => t.Id).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t");
    }

    // Java 的 new TopicProxy().selectIgnores(id)：空投影上排除不影响，等于全部列
    [Fact, Upstream("QueryTest25.java#testOnlyIgnore3")]
    public async Task TestOnlyIgnore3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Select(t => t).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t");
    }

    [Fact, Upstream("QueryTest25.java#xxx22", NotApplicable = "用 Hikari 连本机 3306、替换多个内部服务另建客户端，属于 Java 的启动配置")]
    public void Xxx22() => Assert.NotNull(Db.Metadata.Get<Topic>().Columns.Single(c => c.Name == "id"));
}
