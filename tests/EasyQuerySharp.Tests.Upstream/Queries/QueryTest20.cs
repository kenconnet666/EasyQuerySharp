namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>对应 Java：QueryTest20.java（单表的各种投影、分组后排序、分组结果作为 JOIN 来源 / CTE、JOIN 的多个 ON 条件、Map 投影）。</summary>
public sealed class QueryTest20(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns =
        "t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top`";

    private const string MyBlogWhere = " FROM `t_blog` t WHERE t.`deleted` = ? AND t.`content` LIKE ?";

    // 分组投影之后再排序：包一层派生表，外层按草稿的成员排序
    [Fact, Upstream("QueryTest20.java#testFullSingleTable")]
    public async Task TestFullSingleTable()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog")).GroupBy(b => b.Title)
            .Having(g => g.Sum(e => e.Star) < 10)
            .Select(g => new Draft<string?, int?, DateTime?>(g.Key, g.Sum(e => e.Star), g.Max(e => e.CreateTime)))
            .OrderByDescending(d => d.Value3).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`value1` AS `value1`,t1.`value2` AS `value2`,t1.`value3` AS `value3` FROM (SELECT t.`title` AS `value1`,SUM(t.`star`) AS `value2`,MAX(t.`create_time`) AS `value3`" + MyBlogWhere + " GROUP BY t.`title` HAVING SUM(t.`star`) < ?) t1 ORDER BY t1.`value3` DESC");
        listen.AssertParams("false(Boolean),%my blog%(String),10(Integer)");
    }

    [Fact, Upstream("QueryTest20.java#testFullSingleAllColumns")]
    public async Task TestFullSingleAllColumns()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `content` LIKE ?");
        listen.AssertParams("false(Boolean),%my blog%(String)");
    }

    // 写了投影（哪怕是整个实体）就带别名 t；Java 的 new BlogEntityProxy() / selectAll(b) 也是整个实体
    [Fact, Upstream("QueryTest20.java#testFullSingleAllColumns1"), Upstream("QueryTest20.java#testFullSingleAllColumns2"), Upstream("QueryTest20.java#testFullSingleAllColumns3")]
    public async Task TestFullSingleAllColumns1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog")).Select(b => b).ToListAsync(Ct);
        listen.AssertSql("SELECT " + BlogColumns + MyBlogWhere);
        listen.AssertParams("false(Boolean),%my blog%(String)");
    }

    [Fact, Upstream("QueryTest20.java#testFullSingleSomeColumns1")]
    public async Task TestFullSingleSomeColumns1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog")).Select(b => new BlogEntity { Id = b.Id, Star = b.Star, Status = b.Status }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`star`,t.`status`" + MyBlogWhere);
    }

    // Java 的 FETCHER.allFieldsExclude(title, star)：C# 的 SelectExcept
    [Fact, Upstream("QueryTest20.java#testFullSingleSomeColumns2")]
    public async Task TestFullSingleSomeColumns2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog")).SelectExcept<BlogEntity>(b => new { b.Title, b.Star }).ToListAsync(Ct);
        listen.AssertSql("SELECT " + BlogColumns.Replace("t.`title`,", "").Replace("t.`star`,", "") + MyBlogWhere);
    }

    [Fact, Upstream("QueryTest20.java#testFullSingleSomeColumns3", Deviation = "Java 的 Select.of(b.id().as(..)) 同名列也写 AS；C# 成员初始化同名列不写")]
    public async Task TestFullSingleSomeColumns3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog") && b.Title == "123")
            .Select(b => new BlogEntity { Id = b.Id, Title = b.Title, Star = b.Star }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title`,t.`star`" + MyBlogWhere + " AND t.`title` = ?");
        listen.AssertParams("false(Boolean),%my blog%(String),123(String)");
    }

    // Java 的 select(b -> b.id()) / selectColumn(b -> b.id())：C# 的标量投影
    [Fact, Upstream("QueryTest20.java#testFullSingleColumns1"), Upstream("QueryTest20.java#testFullSingleColumns2")]
    public async Task TestFullSingleColumns1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog") && b.Title == "123").Select(b => b.Id).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`" + MyBlogWhere + " AND t.`title` = ?");
        listen.AssertParams("false(Boolean),%my blog%(String),123(String)");
    }

    [Fact, Upstream("QueryTest20.java#testFullSingleColumns3"), Upstream("QueryTest20.java#testFullSingleColumns4")]
    public async Task TestFullSingleColumns3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("my blog") && b.Title == "123").Select(b => b.Id ?? "123").ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL(t.`id`,?)" + MyBlogWhere + " AND t.`title` = ?");
        listen.AssertParams("123(String),false(Boolean),%my blog%(String),123(String)");
    }

    [Fact, Upstream("QueryTest20.java#testJoinMultiOn")]
    public async Task TestJoinMultiOn()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id && t1.Title!.Contains("234")).Where((t, t1) => t.Id == "3")
            .Select((t, t1) => t).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` AND t1.`title` LIKE ? WHERE t.`id` = ?");
        listen.AssertParams("false(Boolean),%234%(String),3(String)");
    }

    private Query<TopicGroupTestDTO> TopicGroup() => Db.Query<Topic>().Where(o => o.Id == "3").GroupBy(o => o.Id)
        .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(e => e.Id) });

    private const string TopicGroupSelect = "SELECT t1.`id` AS `id`,COUNT(t1.`id`) AS `id_count` FROM `t_topic` t1 WHERE t1.`id` = ? GROUP BY t1.`id`";

    private static string GroupJoinTail(string table) =>
        $" SELECT t.`id` AS `value1`,t3.`id_count` AS `value2` FROM `t_blog` t LEFT JOIN {table} t3 ON t.`id` = t3.`id` WHERE t.`deleted` = ? AND t.`id` IS NOT NULL AND t3.`id` = ?";

    private Task<List<Draft<string?, int?>>> JoinTopicGroup(Query<TopicGroupTestDTO> group) =>
        Db.Query<BlogEntity>().LeftJoin(group, (a, b) => a.Id == b.Id).Where((b1, t2) => b1.Id != null && t2.Id == "123")
            .Select((b1, t2) => new Draft<string?, int?>(b1.Id, t2.IdCount)).ToListAsync(Ct);

    // 分组投影作为 JOIN 来源：派生表别名跳过一个（t3，与 Java 一致）
    [Fact, Upstream("QueryTest20.java#testGroupJoin1")]
    public async Task TestGroupJoin1()
    {
        using var listen = SqlCapture.Listen();
        await JoinTopicGroup(TopicGroup());
        listen.AssertSql(GroupJoinTail("(" + TopicGroupSelect + ")").TrimStart());
        listen.AssertParams("3(String),false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest20.java#testGroupJoin2")]
    public async Task TestGroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        await JoinTopicGroup(TopicGroup().AsCte());
        listen.AssertSql("WITH `with_TopicGroupTestDTO` AS (" + TopicGroupSelect + ")" + GroupJoinTail("`with_TopicGroupTestDTO`"));
        listen.AssertParams("3(String),false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest20.java#testGroupJoin3")]
    public async Task TestGroupJoin3()
    {
        using var listen = SqlCapture.Listen();
        var sql = Db.Query<Topic>().Where(o => o.Id == "3");
        await Db.Query<BlogEntity>().LeftJoin(sql, (b, t2) => b.Id == t2.Id).Where((b, t) => b.Id == "345" && t.Id == "789").Select((b, t) => b).ToListAsync(Ct);
        listen.AssertSql("SELECT " + BlogColumns + " FROM `t_blog` t LEFT JOIN (SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time` FROM `t_topic` t1 WHERE t1.`id` = ?) t2 ON t.`id` = t2.`id` WHERE t.`deleted` = ? AND t.`id` = ? AND t2.`id` = ?");
        listen.AssertParams("3(String),false(Boolean),345(String),789(String)");
    }

    /// <summary>对应 Java：vo/TopicAndBlogVO。</summary>
    public sealed class TopicAndBlogVO
    {
        public string? Column1 { get; set; }

        public string? Column2 { get; set; }
    }

    [Fact, Upstream("QueryTest20.java#testJoinVo")]
    public async Task TestJoinVo()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t) => b.Id == t.Id).Select((b, t) => new TopicAndBlogVO { Column1 = b.Title, Column2 = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` AS `column1`,t1.`title` AS `column2` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("QueryTest20.java#testGroup")]
    public async Task TestGroup()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains("123")).GroupBy(b => b.Title)
            .Select(g => new Draft<string?, long, DateTime?, decimal?>(g.Key, g.Count(), g.Max(e => e.CreateTime), g.Sum(e => e.Score))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` AS `value1`,COUNT(*) AS `value2`,MAX(t.`create_time`) AS `value3`,SUM(t.`score`) AS `value4` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`title` LIKE ? GROUP BY t.`title`");
        listen.AssertParams("false(Boolean),%123%(String)");
    }

    /// <summary>对应 Java：vo/GroupVO。</summary>
    public sealed class GroupVO
    {
        public string? Key { get; set; }

        public long? IdCount { get; set; }

        public DateTime? CreateTimeMax { get; set; }

        public decimal? ScoreSum { get; set; }
    }

    // Java 的 star().asAny().like(..)：C# 的 ((string)(object)b.Star).Contains(..)，只改类型不写 CAST
    [Fact, Upstream("QueryTest20.java#testGroup1")]
    public async Task TestGroup1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains("123") && ((string)(object)b.Star!).Contains("1232")).GroupBy(b => b.Title)
            .Select(g => new GroupVO { Key = g.Key, IdCount = g.Count(), CreateTimeMax = g.Max(e => e.CreateTime), ScoreSum = g.Sum(e => e.Score) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`title` AS `key`,COUNT(*) AS `id_count`,MAX(t.`create_time`) AS `create_time_max`,SUM(t.`score`) AS `score_sum` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`title` LIKE ? AND t.`star` LIKE ? GROUP BY t.`title`");
        listen.AssertParams("false(Boolean),%123%(String),%1232%(String)");
    }

    [Fact, Upstream("QueryTest20.java#testWhere1")]
    public async Task TestWhere1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains("123") && b.Star > 1).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `title` LIKE ? AND `star` > ?");
        listen.AssertParams("false(Boolean),%123%(String),1(Integer)");
    }

    [Fact, Upstream("QueryTest20.java#testWhere2")]
    public async Task TestWhere2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.Title!.Contains("123") && t.Stars > 1).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `title` LIKE ? AND `stars` > ?");
        listen.AssertParams("%123%(String),1(Integer)");
    }

    private Query<Topic, BlogEntity> TopicBlog() => Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == b.Id);

    private const string TopicBlogJoin = " FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`title` LIKE ?";

    [Fact, Upstream("QueryTest20.java#testWhere3")]
    public async Task TestWhere3()
    {
        using var listen = SqlCapture.Listen();
        await TopicBlog().Where((t, b) => t.Title!.Contains("123") && b.Status == 1).Select((t, b) => t).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`" + TopicBlogJoin + " AND t1.`status` = ?");
        listen.AssertParams("false(Boolean),%123%(String),1(Integer)");
    }

    // Java 的 MapProxy.put / MapTypeProxy.put(MapKey, ..)：C# 都是字典初始化，键就是列别名
    [Fact, Upstream("QueryTest20.java#testMap1"), Upstream("QueryTest20.java#testMap2")]
    public async Task TestMap1()
    {
        using var listen = SqlCapture.Listen();
        var rows = await TopicBlog().Where((t, b) => t.Title!.Contains("123"))
            .Select((t, b) => new Dictionary<string, object?> { { "v1", t.Id }, { "v2", b.Star + 1 }, { "v3", t.CreateTime ?? new DateTime(2024, 1, 1) } }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `v1`,(t1.`star` + ?) AS `v2`,IFNULL(t.`create_time`,?) AS `v3`" + TopicBlogJoin);
        listen.AssertParams("1(Integer),2024-01-01T00:00(LocalDateTime),false(Boolean),%123%(String)");
        Assert.All(rows, r => Assert.Equal(3, r.Count));
    }

    [Fact, Upstream("QueryTest20.java#test1", NotApplicable = "测的是 Java 工具方法 EasyUtil.getQuarterStart 的格式化，与查询无关")]
    public void Test1() => Assert.Equal(1, (new DateTime(2025, 2, 3).Month + 2) / 3);
}
