namespace EasyQuerySharp.Tests.Upstream.Sharding;

/// <summary>
/// 对应 Java：ShardingTest.java（48 个用例，文件里另有 9 个被注释掉）。固件见 <see cref="ShardingFixture"/>：
/// 同一数据源里的分表默认在库内 <c>UNION ALL</c>（MaxUnionTables = 64），跨数据源的在内存里归并。
/// 这个文件是取模分表、元数据与 Map 投影的用例；按时间分表、分库的在 ShardingTest.Time.cs。
/// 改数据的用例在结束时删掉自己写的行，种子数据保持不变。
/// </summary>
public sealed partial class ShardingTest(ShardingFixture fixture) : IClassFixture<ShardingFixture>
{
    private static readonly DateTime Begin = ShardingFixture.Begin;
    private static readonly int Days = ShardingFixture.Days;

    private EasyDb Db => fixture.Db;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Upstream("ShardingTest.java#test111", NotApplicable = "只打印实体元数据，没有断言；分片规则的元数据见 shardingTest48 / 49")]
    public void Test111()
    {
    }

    // "123" 按 Java 的 String.hashCode 取模落在 t_topic_sharding_0，只查这一张表
    [Fact, Upstream("ShardingTest.java#sharding1")]
    public async Task Sharding1()
    {
        using var listen = SqlCapture.Listen();
        Assert.NotNull(await Db.Query<TopicSharding>().WhereById("123").FirstOrDefaultAsync(Ct));
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic_sharding_0` WHERE `id` = ? LIMIT 1");
    }

    [Fact, Upstream("ShardingTest.java#sharding2")]
    public async Task Sharding2()
    {
        const string id = "999999";
        var existing = await Db.Query<TopicSharding>().WhereById(id).FirstOrDefaultAsync(Ct);
        if (existing is not null) Assert.Equal(1, await Db.Delete(existing).ExecuteAsync(Ct));

        await Db.Insert(new TopicSharding { Id = id, Title = "title" + id, Stars = 999999, CreateTime = DateTime.Now }).ExecuteAsync(Ct);
        Assert.NotNull(await Db.Query<TopicSharding>().WhereById(id).FirstOrDefaultAsync(Ct));
        Assert.Equal(1, await Db.Delete<TopicSharding>().WhereById(id).ExecuteAsync(Ct));
    }

    [Fact, Upstream("ShardingTest.java#sharding3")]
    public async Task Sharding3()
    {
        var topics = Enumerable.Range(10000, 3).Select(i => new TopicSharding { Id = Text(i), Title = "title" + i, Stars = i, CreateTime = DateTime.Now }).ToList();
        var ids = topics.Select(t => t.Id).ToList();
        await Db.Delete<TopicSharding>().Where(o => ids.Contains(o.Id)).ExecuteAsync(Ct);
        try
        {
            await Db.Insert(topics).ExecuteAsync(Ct);
            Assert.Equal(3, (await Db.Query<TopicSharding>().Where(o => ids.Contains(o.Id)).ToListAsync(Ct)).Count);
            var ordered = await Db.Query<TopicSharding>().Where(o => ids.Contains(o.Id)).OrderBy(o => o.Stars).ToListAsync(Ct);
            Assert.Equal([10000, 10001, 10002], ordered.Select(t => t.Stars));
        }
        finally
        {
            await Db.Delete<TopicSharding>().Where(o => ids.Contains(o.Id)).ExecuteAsync(Ct);
        }
    }

    [Fact, Upstream("ShardingTest.java#sharding4")]
    public async Task Sharding4()
    {
        var list = await Db.Query<TopicSharding>().Where(o => o.Stars <= 1000).OrderByDescending(o => o.Stars).ToListAsync(Ct);
        Assert.Equal(Enumerable.Range(0, 500).Reverse().Cast<int?>(), list.Select(t => t.Stars));
    }

    [Fact, Upstream("ShardingTest.java#sharding5")]
    public async Task Sharding5()
    {
        var query = Db.Query<TopicSharding>().Where(o => o.Stars <= 1000).OrderByDescending(o => o.Stars);
        AssertStars(await query.ToPageAsync(1, 20, Ct), 499);
        AssertStars(await query.ToPageAsync(2, 20, Ct), 479);
        AssertStars(await query.ToPageAsync(7, 7, Ct), 457);

        static void AssertStars(Page<TopicSharding> page, int first)
        {
            Assert.Equal(500, page.Total);
            Assert.Equal(Enumerable.Range(0, (int)page.PageSize).Select(i => (int?)(first - i)), page.Rows.Select(t => t.Stars));
        }
    }

    // 三张表在同一数据源里，库内 UNION ALL 之后由数据库分组
    [Fact, Upstream("ShardingTest.java#sharding6")]
    public async Task Sharding6()
    {
        await Db.Delete<TopicSharding>().Where(o => o.Stars >= 20000 && o.Stars <= 20100).ExecuteAsync(Ct);
        try
        {
            Assert.Equal(100, await Db.Insert(Hundred(20000)).ExecuteAsync(Ct));
            var list = await Db.Query<TopicSharding>().Where(o => o.Stars >= 20000 && o.Stars <= 20100).OrderBy(o => o.Stars).ToListAsync(Ct);
            Assert.Equal(Enumerable.Range(20000, 100).Select(Text), list.Select(t => t.Id));

            var ordered = await Db.Query<TopicSharding>().Where(o => o.Stars >= 20000 && o.Stars <= 20100).GroupBy(t => t.Title).OrderBy(g => g.Key)
                .Select(g => new TopicShardingGroup { Title = g.Key, IdCount = (int)g.Count(s => s.Id) }).ToListAsync(Ct);
            AssertTitles(ordered);
            var unordered = await Db.Query<TopicSharding>().Where(o => o.Stars >= 20000 && o.Stars <= 20100).GroupBy(t => t.Title)
                .Select(g => new TopicShardingGroup { Title = g.Key, IdCount = (int)g.Count(s => s.Id) }).ToListAsync(Ct);
            AssertTitles(unordered);
        }
        finally
        {
            await Db.Delete<TopicSharding>().Where(o => o.Stars >= 20000 && o.Stars <= 20100).ExecuteAsync(Ct);
        }

        // 偶数行的标题都是 title1，奇数行是 title + i（i = 1 时也是 title1）
        static void AssertTitles(List<TopicShardingGroup> groups)
        {
            Assert.Equal(50, groups.Count);
            Assert.Equal(51, groups.Single(g => g.Title == "title1").IdCount);
        }
    }

    [Fact, Upstream("ShardingTest.java#sharding7")]
    public async Task Sharding7()
    {
        await Db.Delete<TopicSharding>().Where(o => o.Stars >= 30000 && o.Stars <= 30100).ExecuteAsync(Ct);
        try
        {
            Assert.Equal(100, await Db.Insert(Hundred(30000)).ExecuteAsync(Ct));
            Assert.Equal(100, await Db.Query<TopicSharding>().Where(o => o.Stars >= 30000 && o.Stars <= 30100).CountAsync(Ct));
        }
        finally
        {
            await Db.Delete<TopicSharding>().Where(o => o.Stars >= 30000 && o.Stars <= 30100).ExecuteAsync(Ct);
        }
    }

    [Fact, Upstream("ShardingTest.java#sharding8")]
    public async Task Sharding8()
    {
        await Db.Delete<TopicSharding>().Where(o => o.Stars >= 40000 && o.Stars <= 40100).ExecuteAsync(Ct);
        try
        {
            Assert.Equal(100, await Db.Insert(Hundred(40000)).ExecuteAsync(Ct));
            Assert.Equal(50, await Db.Query<TopicSharding>().Where(o => o.Stars >= 40000 && o.Stars < 40050).CountAsync(Ct));
        }
        finally
        {
            await Db.Delete<TopicSharding>().Where(o => o.Stars >= 40000 && o.Stars <= 40100).ExecuteAsync(Ct);
        }
    }

    // Java 的 toShardingPageResult：总数是各节点（这里三张表）的行数之和
    [Fact, Upstream("ShardingTest.java#sharding28")]
    public async Task Sharding28()
    {
        var query = Db.Query<TopicSharding>().Where(o => o.Stars <= 1000).OrderByDescending(o => o.Stars);
        AssertStars(await query.ToShardingPageAsync(1, 20, null, Ct), 499);
        AssertStars(await query.ToShardingPageAsync(2, 20, null, Ct), 479);
        AssertStars(await query.ToShardingPageAsync(7, 7, null, Ct), 457);

        static void AssertStars(ShardingPage<TopicSharding> page, int first)
        {
            Assert.Equal(500, page.Total);
            Assert.Equal(3, page.TotalLines.Count);
            Assert.Equal(Enumerable.Range(0, (int)page.PageSize).Select(i => (int?)(first - i)), page.Rows.Select(t => t.Stars));
        }
    }

    // Java 读 EntityMetadata 的分表 / 分库属性；C# 的分片信息在规则上：分片键是 id，全部节点在默认数据源（不分库）
    [Fact, Upstream("ShardingTest.java#shardingTest48")]
    public void ShardingTest48()
    {
        var rule = Db.GetShardRule<TopicSharding>()!;
        Assert.Equal(["Id"], rule.KeyProperties);
        Assert.All(rule.Nodes, n => Assert.Equal("ds2020", n.DataSource));
        Assert.Equal(["t_topic_sharding_0", "t_topic_sharding_1", "t_topic_sharding_2"], rule.Nodes.Select(n => n.Table));
    }

    // 只分库：分片键是 createTime，各库里表名不变
    [Fact, Upstream("ShardingTest.java#shardingTest49")]
    public void ShardingTest49()
    {
        var rule = Db.GetShardRule<TopicShardingDataSource>()!;
        Assert.Equal(["CreateTime"], rule.KeyProperties);
        Assert.All(rule.Nodes, n => Assert.Equal("t_topic_sharding_ds", n.Table));
        Assert.Equal(["ds2020", "ds2021", "ds2022", "ds2023"], rule.Nodes.Select(n => n.DataSource));
    }

    // Java 只执行、不断言
    [Fact, Upstream("ShardingTest.java#shardingTest50")]
    public async Task ShardingTest50() =>
        await Db.Query<TopicSharding>().Where(o => o.Id == "20000").GroupBy(o => o.Title).OrderBy(g => g.Key)
            .Select(g => new TopicShardingGroup { Title = g.Key, IdCount = (int)g.Count(s => s.Id) }).ToListAsync(Ct);

    // Java 只执行、不断言：投影之后再排序（包成派生表，分片表在里面）
    [Fact, Upstream("ShardingTest.java#shardingTest51")]
    public async Task ShardingTest51() =>
        await Db.Query<TopicSharding>().Where(o => o.Id == "20000").GroupBy(o => o.Title)
            .Select(g => new TopicShardingGroup { Title = g.Key, IdCount = (int)g.Count(s => s.Id) }).OrderBy(t => t.Title).ToListAsync(Ct);

    // 显式换表的查询不走分片
    [Fact, Upstream("ShardingTest.java#shardingTest51_1")]
    public async Task ShardingTest51_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicSharding>().AsTable(o => o + "_1").Where(o => o.Id == "20000").CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) FROM `t_topic_sharding_1` WHERE `id` = ?");
    }

    private const string MapSql = "SELECT t.`create_time` AS `createTime`,COUNT(t.`id`) AS `idCount` FROM (SELECT * FROM `t_topic_sharding_0` UNION ALL SELECT * FROM `t_topic_sharding_2`) t WHERE t.`id` IN (?,?) GROUP BY t.`create_time`";

    private static readonly string[] MapIds = ["20000", "20001"];

    // Java 用 SHARDING_UNION_ALL 把两张分表合成一条语句（C# 的默认做法）："20000" 落在 _2，"20001" 落在 _0
    private async Task AssertMapQuery()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicSharding>().Where(t => MapIds.Contains(t.Id)).GroupBy(t => t.CreateTime)
            .Select(g => new DynamicRow { { "createTime", g.Key }, { "idCount", g.Count(t => t.Id) } }).ToListAsync(Ct);
        listen.AssertSql(MapSql);
        listen.AssertParams("20000(String),20001(String)");
    }

    [Fact, Upstream("ShardingTest.java#shardingTestMap")]
    public Task ShardingTestMap() => AssertMapQuery();

    // Java 用强类型的 MapKey 放值，SQL 相同
    [Fact, Upstream("ShardingTest.java#shardingTestMap2")]
    public Task ShardingTestMap2() => AssertMapQuery();

    [Fact, Upstream("ShardingTest.java#shardingTestMap3")]
    public Task ShardingTestMap3() => AssertMapQuery();

    // Java 的 asLocalDate() 只改读出来的类型，SQL 相同
    [Fact, Upstream("ShardingTest.java#shardingTestMap1")]
    public Task ShardingTestMap1() => AssertMapQuery();

    // 100 行：id 与 stars 从 start 起，偶数行标题是 title1，奇数行是 title + i
    private static List<TopicSharding> Hundred(int start) =>
        [.. Enumerable.Range(0, 100).Select(i => new TopicSharding { Id = Text(i + start), Title = "title" + (i % 2 == 0 ? "1" : Text(i)), Stars = i + start, CreateTime = DateTime.Now })];

    private static string Text(int i) => i.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
