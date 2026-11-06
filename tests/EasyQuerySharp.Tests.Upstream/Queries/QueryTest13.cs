using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest13.java（Map 投影的分组结果作为 JOIN 来源、多张表一起投影）。
/// Java 用 MapKey 声明的 Map 投影，C# 写成 DTO，用 [Column] 给出与 Java 相同的列别名。
/// </summary>
public sealed class QueryTest13(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string MapDeviation = "Java 用 MapKey 拼 Map 投影；C# 写成 DTO，[Column] 给出相同的列别名，SQL 相同";

    // 分组计数作为派生表，再连接主题表
    [Fact, Upstream("QueryTest13.java#orderTest1", Deviation = MapDeviation), Upstream("QueryTest13.java#orderTest1_1", Deviation = MapDeviation)]
    public async Task OrderTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Star > 1).GroupBy(b => b.Id)
            .Select(g => new BlogIdCount { BlogId = g.Key, BlogCount = (int)g.Count() })
            .LeftJoin<Topic>((g, topic) => g.BlogId == topic.Id)
            .Where((g, _) => g.BlogCount <= 123)
            .Select((_, topic) => topic).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`id`,t2.`stars`,t2.`title`,t2.`create_time` FROM (SELECT t.`id` AS `blogId`,COUNT(*) AS `blogCount` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`star` > ? GROUP BY t.`id`) t1 LEFT JOIN `t_topic` t2 ON t1.`blogId` = t2.`id` WHERE t1.`blogCount` <= ?");
        listen.AssertParams("false(Boolean),1(Integer),123(Integer)");
    }

    // 读成字典时日期列的值仍是 DateTime
    [Fact, Upstream("QueryTest13.java#orderTest2", Deviation = MapDeviation)]
    public async Task OrderTest2()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<BlogEntity>().Where(b => b.Star > 1).Select(b => new BlogIdTime { BlogId = b.Id, DateTime = b.CreateTime }).ToRowsAsync(Ct);
        Assert.All(rows, r => Assert.True(r["dateTime"] is null or DateTime));
        listen.AssertSql("SELECT t.`id` AS `blogId`,t.`create_time` AS `dateTime` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`star` > ?");
        listen.AssertParams("false(Boolean),1(Integer)");
    }

    // 两张表各取几列；后半段的各种字符串条件只执行
    [Fact, Upstream("QueryTest13.java#testMultiSelect", Deviation = "Java 的 as(\"createTime\") 强制写了 AS `create_time`；C# 列名与别名相同时不写 AS")]
    public async Task TestMultiSelect()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().LeftJoin<SysUser>((b, s2) => b.Id == s2.Id)
            .Select((b1, s2) => new { b1.Id, b1.Content, b1.CreateTime, s2.Address, s2.IdCard }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`content`,t.`create_time`,t1.`address`,t1.`id_card` FROM `t_blog` t LEFT JOIN `easy-query-test`.`t_sys_user` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
        await Db.Query<BlogEntity>().Where(b => b.CreateTime!.Value.ToString("yyyy-MM-dd") == "2023-01-01" && (b.Title ?? "我的title") == "title"
            && b.Title!.Substring(1, 2).LikeContains("你好") && string.IsNullOrWhiteSpace(b.Title) && !string.IsNullOrWhiteSpace(b.Title)
            && string.IsNullOrEmpty(b.Title) && !string.IsNullOrEmpty(b.Title)).ToListAsync(Ct);
    }

    /// <summary>Java 的 Map 投影 blogId / blogCount。</summary>
    public sealed class BlogIdCount
    {
        [Column("blogId")]
        public string? BlogId { get; set; }

        [Column("blogCount")]
        public int BlogCount { get; set; }
    }

    /// <summary>Java 的 Map 投影 blogId / dateTime。</summary>
    public sealed class BlogIdTime
    {
        [Column("blogId")]
        public string? BlogId { get; set; }

        [Column("dateTime")]
        public DateTime? DateTime { get; set; }
    }
}
