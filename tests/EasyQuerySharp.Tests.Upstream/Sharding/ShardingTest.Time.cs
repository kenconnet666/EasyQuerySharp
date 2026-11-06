namespace EasyQuerySharp.Tests.Upstream.Sharding;

// 按时间分表（TopicShardingTime：同一数据源 40 张表，库内 UNION ALL）、按年分库（TopicShardingDataSource：四个库各一张表）、
// 分库加分表（TopicShardingDataSourceTime：四个库、40 张表，各库内 UNION ALL、跨库在内存里归并）
public sealed partial class ShardingTest
{
    private const string Sharding40To42 =
        "Java 对两个分片查询做 union 时把两边的路由做笛卡尔积，行数是 (2n−1) × 正确行数（90 / 290 / 15972）；C# 每个分支只展开自己的节点，期望用正确语义：UNION 去重后 30 / 58 / 484 行，UNION ALL 是两倍（Java 第二个断言误写成了第一个列表）";

    // 第 skip 天起连续 count 天，每天一行
    private static void AssertDays(IEnumerable<DateTime?> times, int skip, int count) =>
        Assert.Equal(Enumerable.Range(skip, count).Select(i => (DateTime?)Begin.AddDays(i)), times);

    [Fact, Upstream("ShardingTest.java#sharding9")]
    public async Task Sharding9()
    {
        var list = await Db.Query<TopicShardingTime>().OrderBy(o => o.CreateTime).ToListAsync(Ct);
        AssertDays(list.Select(t => t.CreateTime), 0, Days);
    }

    [Fact, Upstream("ShardingTest.java#sharding10")]
    public Task Sharding10() => AssertTimePage(1);

    [Fact, Upstream("ShardingTest.java#sharding11")]
    public Task Sharding11() => AssertTimePage(2);

    [Fact, Upstream("ShardingTest.java#sharding12")]
    public Task Sharding12() => AssertTimePage(3);

    private async Task AssertTimePage(long page)
    {
        var result = await Db.Query<TopicShardingTime>().OrderBy(o => o.CreateTime).ToPageAsync(page, 33, Ct);
        Assert.Equal(Days, result.Total);
        AssertDays(result.Rows.Select(t => t.CreateTime), (int)(page - 1) * 33, 33);
    }

    // 四个库并行各查一条（库内 12 / 12 / 12 / 4 张表 UNION ALL），按 create_time 归并
    [Fact, Upstream("ShardingTest.java#sharding18")]
    public async Task Sharding18()
    {
        var list = await Db.Query<TopicShardingDataSourceTime>().OrderBy(o => o.CreateTime).ToListAsync(Ct);
        AssertDays(list.Select(t => t.CreateTime), 0, Days);
    }

    [Fact, Upstream("ShardingTest.java#sharding19")]
    public Task Sharding19() => AssertDataSourceTimePage(1);

    [Fact, Upstream("ShardingTest.java#sharding20")]
    public Task Sharding20() => AssertDataSourceTimePage(2);

    [Fact, Upstream("ShardingTest.java#sharding21")]
    public Task Sharding21() => AssertDataSourceTimePage(3);

    // 计数相加；每个库取前 offset + 33 行，归并后跳过 offset 行
    private async Task AssertDataSourceTimePage(long page)
    {
        var result = await Db.Query<TopicShardingDataSourceTime>().OrderBy(o => o.CreateTime).ToPageAsync(page, 33, Ct);
        Assert.Equal(Days, result.Total);
        AssertDays(result.Rows.Select(t => t.CreateTime), (int)(page - 1) * 33, 33);
    }

    // 最新的 10 行；再按 id 查（id 末 6 位是月份，额外分片键只查 202304 这一张表）
    [Fact, Upstream("ShardingTest.java#sharding26")]
    public async Task Sharding26()
    {
        var list = await Db.Query<TopicShardingTime>().OrderByDescending(o => o.CreateTime).Take(10).ToListAsync(Ct);
        await AssertLatestTen(list);
    }

    // 没写排序但带了 Take、命中 40 张表：补上规则的默认排序（create_time 降序），结果同上
    [Fact, Upstream("ShardingTest.java#sharding27")]
    public async Task Sharding27()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<TopicShardingTime>().Take(10).ToListAsync(Ct);
        Assert.EndsWith(") t ORDER BY t.`create_time` DESC LIMIT 10", listen.Last.Statement.Text);
        await AssertLatestTen(list);
    }

    private async Task AssertLatestTen(List<TopicShardingTime> list)
    {
        Assert.Equal(Enumerable.Range(1, 10).Select(i => (DateTime?)ShardingFixture.End.AddDays(-i)), list.Select(t => t.CreateTime));
        using var listen = SqlCapture.Listen();
        var found = await Db.Query<TopicShardingTime>().WhereById(list[0].Id!).FirstOrDefaultAsync(Ct);
        Assert.Equal(list[0].Id, found?.Id);
        Assert.Equal(list[0].CreateTime, found?.CreateTime);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic_sharding_time_202304` WHERE `id` = ? LIMIT 1");
    }

    [Fact, Upstream("ShardingTest.java#sharding29")]
    public Task Sharding29() => AssertShardingPage(q => q.OrderBy(o => o.CreateTime), 2);

    [Fact, Upstream("ShardingTest.java#sharding30")]
    public Task Sharding30() => AssertShardingPage(q => q.OrderBy(o => o.CreateTime), 3);

    // stars 就是第几天，按 stars 排序与按时间排序结果相同
    [Fact, Upstream("ShardingTest.java#sharding31")]
    public Task Sharding31() => AssertShardingPage(q => q.OrderBy(o => o.Stars), 2);

    // 偏移量过半（957 > 1216 / 2）时反向排序从另一头取，再在内存里倒序
    [Fact, Upstream("ShardingTest.java#sharding32")]
    public Task Sharding32() => AssertShardingPage(q => q.OrderBy(o => o.Stars), 30);

    [Fact, Upstream("ShardingTest.java#sharding33")]
    public Task Sharding33() => AssertShardingPage(q => q.OrderBy(o => o.Stars), 3);

    private async Task<ShardingPage<TopicShardingTime>> AssertShardingPage(Func<Query<TopicShardingTime>, Query<TopicShardingTime>> order, long page,
        IReadOnlyList<long>? totalLines = null)
    {
        var result = await order(Db.Query<TopicShardingTime>()).ToShardingPageAsync(page, 33, totalLines, Ct);
        Assert.Equal(Days, result.Total);
        Assert.Equal(40, result.TotalLines.Count);
        AssertDays(result.Rows.Select(t => t.CreateTime), (int)(page - 1) * 33, 33);
        return result;
    }

    // 把第一张表的行数改成 -1 传回：只重算这一张，其余沿用
    [Fact, Upstream("ShardingTest.java#sharding34")]
    public async Task Sharding34()
    {
        var first = await AssertShardingPage(q => q.OrderBy(o => o.CreateTime), 30);
        var lines = first.TotalLines.ToArray();
        lines[0] = -1;
        using var listen = SqlCapture.Listen();
        var second = await AssertShardingPage(q => q.OrderBy(o => o.CreateTime), 30, lines);
        Assert.Equal(first.TotalLines, second.TotalLines);
        Assert.Single(listen.Events, e => e.Statement.Text.StartsWith("SELECT COUNT(*)", StringComparison.Ordinal));
    }

    // 偶数位置的行数改成 -1：只重算这 20 张表
    [Fact, Upstream("ShardingTest.java#sharding35")]
    public async Task Sharding35()
    {
        var first = await AssertShardingPage(q => q.OrderBy(o => o.CreateTime), 3);
        var lines = first.TotalLines.Select((line, i) => i % 2 == 0 ? -1 : line).ToArray();
        using var listen = SqlCapture.Listen();
        var second = await AssertShardingPage(q => q.OrderBy(o => o.CreateTime), 3, lines);
        Assert.Equal(first.TotalLines, second.TotalLines);
        Assert.Equal(20, listen.Events.Count(e => e.Statement.Text.StartsWith("SELECT COUNT(*)", StringComparison.Ordinal)));
    }

    // 条件互相矛盾：空路由，不访问数据库
    [Fact, Upstream("ShardingTest.java#sharding36")]
    public async Task Sharding36()
    {
        using var listen = SqlCapture.Listen();
        var page = await Db.Query<TopicShardingTime>().Where(o => o.CreateTime <= Begin).Where(o => o.CreateTime >= ShardingFixture.End).OrderBy(o => o.CreateTime)
            .ToShardingPageAsync(1, 10, null, Ct);
        Assert.Equal(0, page.Total);
        Assert.Empty(page.Rows);
        Assert.Empty(listen.Events);
    }

    // 分片表联不分片的 t_topic（都在默认数据源）；id 末 6 位只查 202003 这一张表
    [Fact, Upstream("ShardingTest.java#sharding37")]
    public async Task Sharding37()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<TopicShardingTime>().LeftJoin<Topic>((t, t1) => t.Id == t1.Id)
            .Where((t, t1) => t.Id == "0de2e78681a64343a98b0aab3c039b3a202003" && t1.Id == "1").ToListAsync(Ct);
        Assert.Empty(list);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic_sharding_time_202003` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`id` = ? AND t1.`id` = ?");
    }

    [Fact, Upstream("ShardingTest.java#sharding38")]
    public async Task Sharding38()
    {
        var list = await Db.Query<TopicShardingTime>().LeftJoin<Topic>((t, t1) => t.Id == t1.Id)
            .Where((t, t1) => t.Title == "0de2e78681a64343a98b0aab3c039b3a202003" && t1.Id == "1").ToListAsync(Ct);
        Assert.Empty(list);
    }

    // OR 的一边（id = '123'，不够 6 位）不能裁剪，整体查全部表；EXISTS 里的分片表按子查询自己的条件裁剪、内联成 UNION ALL。ToSql 不分片
    [Fact, Upstream("ShardingTest.java#sharding39")]
    public async Task Sharding39()
    {
        DateTime? begin = new DateTime(2021, 1, 1, 1, 1, 0), end = new DateTime(2022, 5, 1, 1, 1, 0);
        await Db.Query<TopicShardingTime>().Where(o => Sql.RangeOpen(o.CreateTime, begin, end) || o.Id == "123").ToListAsync(Ct);

        var sharded = Db.Query<TopicShardingTime>().Where(o => Sql.RangeOpen(o.CreateTime, begin, end));
        var where = Db.Query<Topic>().Where(o => o.Id == "123" && Sql.Exists(sharded.Where(x => x.Id == o.Id)));
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE t.`id` = ? AND EXISTS(SELECT 1 FROM `t_topic_sharding_time` t1 WHERE (t1.`create_time` > ? AND t1.`create_time` < ?) AND t1.`id` = t.`id`)",
            where.ToSql());
        Assert.Empty(await where.ToListAsync(Ct));
    }

    [Fact, Upstream("ShardingTest.java#sharding40", Deviation = Sharding40To42)]
    public Task Sharding40() => AssertUnion(new DateTime(2021, 2, 1, 1, 1, 0), 30);

    [Fact, Upstream("ShardingTest.java#sharding41", Deviation = Sharding40To42)]
    public Task Sharding41() => AssertUnion(new DateTime(2021, 3, 1, 1, 1, 0), 58);

    [Fact, Upstream("ShardingTest.java#sharding42", Deviation = Sharding40To42)]
    public Task Sharding42() => AssertUnion(new DateTime(2022, 5, 1, 1, 1, 0), 484);

    // 两条相同的分片查询做 UNION / UNION ALL：分支是嵌套处，各自按自己的条件裁剪、内联成 UNION ALL
    private async Task AssertUnion(DateTime end, int expected)
    {
        DateTime? begin = new DateTime(2021, 1, 1, 1, 1, 0), upper = end;
        var query1 = Db.Query<TopicShardingTime>().Where(o => Sql.RangeOpen(o.CreateTime, begin, upper));
        var query2 = Db.Query<TopicShardingTime>().Where(o => Sql.RangeOpen(o.CreateTime, begin, upper));
        Assert.Equal(expected, (await query1.Union(query2).ToListAsync(Ct)).Count);
        Assert.Equal(expected * 2, (await query1.UnionAll(query2).ToListAsync(Ct)).Count);
    }

    // 四个库各一张表，并行查询后按 create_time 归并
    [Fact, Upstream("ShardingTest.java#sharding43")]
    public async Task Sharding43()
    {
        var list = await Db.Query<TopicShardingDataSource>().OrderBy(o => o.CreateTime).ToListAsync(Ct);
        AssertDays(list.Select(t => t.CreateTime), 0, Days);
    }

    [Fact, Upstream("ShardingTest.java#sharding44")]
    public async Task Sharding44()
    {
        var page = await Db.Query<TopicShardingDataSource>().OrderBy(o => o.CreateTime).ToPageAsync(1, 33, Ct);
        Assert.Equal(Days, page.Total);
        AssertDays(page.Rows.Select(t => t.CreateTime), 0, 33);
    }

    // 闭区间两端都含：2021-01-01 01:01 … 2021-05-02 01:01 共 122 天
    [Fact, Upstream("ShardingTest.java#sharding45")]
    public async Task Sharding45()
    {
        DateTime? begin = new DateTime(2021, 1, 1, 1, 1, 0), end = new DateTime(2021, 5, 2, 1, 1, 0);
        var list = await Db.Query<TopicShardingTime>().Where(o => Sql.RangeClosed(o.CreateTime, begin, end)).OrderBy(o => o.CreateTime).ToListAsync(Ct);
        Assert.Equal((int)(end - begin).Value.TotalDays + 1, list.Count);
    }

    // ToSql 不分片；Java 的执行注释掉了（"todo sharding 需要聚合"），C# 的标量子查询里分片表内联成 UNION ALL，可以执行：t_topic 的 id 都不在分表里
    [Fact, Upstream("ShardingTest.java#sharding46")]
    public async Task Sharding46()
    {
        DateTime? begin = new DateTime(2021, 1, 1, 1, 1, 0), end = new DateTime(2021, 5, 2, 1, 1, 0);
        var sharded = Db.Query<TopicShardingTime>().Where(o => Sql.RangeClosed(o.CreateTime, begin, end));
        var select = Db.Query<Topic>().Where(t => t.Title != null).Select(t => new TopicSubQueryBlog
        {
            Id = t.Id, Stars = t.Stars, Title = t.Title, BlogCount = Sql.SubQuery(sharded.Where(x => x.Id == t.Id).Select(x => Sql.Count(x.Id))),
        });
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,(SELECT COUNT(t1.`id`) FROM `t_topic_sharding_time` t1 WHERE (t1.`create_time` >= ? AND t1.`create_time` <= ?) AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL",
            select.ToSql());
        Assert.All(await select.ToListAsync(Ct), row => Assert.Equal(0, row.BlogCount));
    }

    // 同上，按 stars 关联：2020-01-01 … 2020-05-02 的分表里 stars 是 0 … 122，每个值一行
    [Fact, Upstream("ShardingTest.java#sharding47")]
    public async Task Sharding47()
    {
        DateTime? begin = new DateTime(2020, 1, 1, 1, 1, 0), end = new DateTime(2020, 5, 2, 1, 1, 0);
        var sharded = Db.Query<TopicShardingTime>().Where(o => Sql.RangeClosed(o.CreateTime, begin, end));
        var select = Db.Query<Topic>().Where(t => t.Title != null).Select(t => new TopicSubQueryBlog
        {
            Id = t.Id, Stars = t.Stars, Title = t.Title, BlogCount = Sql.SubQuery(sharded.Where(x => x.Stars == t.Stars).Select(x => Sql.Count(x.Id))),
        });
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,(SELECT COUNT(t1.`id`) FROM `t_topic_sharding_time` t1 WHERE (t1.`create_time` >= ? AND t1.`create_time` <= ?) AND t1.`stars` = t.`stars`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL",
            select.ToSql());
        var days = (int)(end - begin).Value.TotalDays;
        Assert.All(await select.ToListAsync(Ct), row => Assert.Equal(row.Stars >= 0 && row.Stars <= days ? 1 : 0, row.BlogCount));
    }
}
