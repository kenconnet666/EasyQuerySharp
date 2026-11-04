using EasyQuerySharp.Metadata;
using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：QueryTest11.java（子查询作为条件 / 列、Map 投影、派生表 join、枚举 IN、日期加减、分组草稿、排序的 NULL 位置等）。
/// Java 的 MapProxy 在 C# 是 <c>Dictionary&lt;string, object?&gt;</c>：<c>new() { { "id", o.Id } }</c> 逐键投影，
/// <c>Select&lt;Dictionary&lt;string, object?&gt;&gt;()</c> 选全部列，<c>new Dictionary&lt;string, object?&gt;()</c> 是 <c>SELECT *</c>。
/// </summary>
public sealed class QueryTest11(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string TopicTypeSelect = "SELECT `id`,`stars`,`title`,`topic_type`,`create_time` FROM `t_topic_type` WHERE ";
    private const string TopicColumns = "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t";

    // Java 只跑不断言：子查询 exists / in、join 后分组分页、投影后再 join
    [Fact, Upstream("QueryTest11.java#testx")]
    public async Task Testx()
    {
        var where = Db.Query<BlogEntity>().Where(o => o.Id == "1");
        await Db.Query<Topic>().Where(o => Sql.Exists(where.Where(q => q.Id == o.Id))).ToListAsync(Ct);

        var idQuery = Db.Query<BlogEntity>().Where(o => o.Id == "1").Select(o => o.Id);
        await Db.Query<Topic>().Where(o => o.Id.In(idQuery)).ToListAsync(Ct);

        await Db.Query<Topic>().InnerJoin<BlogEntity>((t1, t2) => t1.Id == t2.Id).Where((t1, t2) => t2.Title != null)
            .GroupBy((t1, t2) => t2.Id)
            .Select(g => new BlogEntity { Id = g.Key, Score = g.Sum(e => e.Item2.Score) })
            .ToPageAsync(1, 20, Ct);

        var query = Db.Query<Topic>().Where(o => o.Id == "1").Select(o => new Topic { Id = o.Id, Title = o.Title });
        await query.LeftJoin<Topic>((t, t1) => t.Id == t1.Id).Where((t, t1) => t1.Id == "123" && t.Id == "456").Select((t, t1) => t).ToListAsync(Ct);
    }

    // Java 只跑不断言：截取后投影，再与表 join，再投影再 join
    [Fact, Upstream("QueryTest11.java#testx9")]
    public async Task Testx9()
    {
        var join = Db.Query<Topic>().Take(100).Select(o => new Topic { Id = o.Id, Stars = o.Stars })
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id);
        var join2 = join.Select((t, t1) => new BlogEntityTest { Url = t.Id, Title = t1.Title })
            .InnerJoin<BlogEntity>((t, t1) => t.Url == t1.Id);
        await join2.Select((t, t1) => new BlogEntity { Url = t.Url, Content = t.Title }).ToListAsync(Ct);

        await Db.Query<Topic>().Take(100).Select(o => new Draft<string?, int?>(o.Id, o.Stars))
            .LeftJoin<BlogEntity>((t, t1) => t.Value1 == t1.Id).Select((a, b) => new Draft<string?, string?>(a.Value1, b.Url))
            .InnerJoin<BlogEntity>((t, t1) => t.Value2 == t1.Id).Select((a, b) => new Draft<string?, string?>(a.Value1, b.Url))
            .ToListAsync(Ct);
    }

    // Java 的 sqlSegment("1").asAnyType(..)：C# 的 Sql.Raw<T>("1")；Map 投影见类注释
    [Fact, Upstream("QueryTest11.java#testx1", Deviation = "Map 逐键投影 Java 写 MapProxy.put，C# 写字典初始化 { \"id\", o.Id }；SQL 相同")]
    public async Task Testx1()
    {
        using (var listen = SqlCapture.Listen())
        {
            await Db.Query<BlogEntity>()
                .Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2023") && Sql.Exists(Db.Query<Topic>().Where(x => x.Id == o.Id)))
                .Select(o => new BlogEntity { Score = Sql.Raw<decimal?>("1") }).ToListAsync(Ct);
            listen.AssertSql("SELECT 1 AS `score` FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ? AND EXISTS(SELECT 1 FROM `t_topic` t1 WHERE t1.`id` = t.`id`)");
            listen.AssertParams("false(Boolean),2023%(String)");
        }
        using (var listen = SqlCapture.Listen())
        {
            var sql = Db.Query<Topic>().Where(o => o.Id == "3");
            await Db.Query<BlogEntity>().LeftJoin(sql, (a, b) => a.Id == b.Id).Where((a, b) => a.Id != null && b.Id != null).ToListAsync(Ct);
            listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN (SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time` FROM `t_topic` t1 WHERE t1.`id` = ?) t2 ON t.`id` = t2.`id` WHERE t.`deleted` = ? AND t.`id` IS NOT NULL AND t2.`id` IS NOT NULL");
            listen.AssertParams("3(String),false(Boolean)");
        }
        using (var listen = SqlCapture.Listen())
        {
            var rows = await Db.Query<Topic>().Where(f => f.Id == "1").Select(s => new Dictionary<string, object?> { { "id", s.Id }, { "name", s.Stars } }).ToListAsync(Ct);
            listen.AssertSql("SELECT t.`id` AS `id`,t.`stars` AS `name` FROM `t_topic` t WHERE t.`id` = ?");
            listen.AssertParams("1(String)");
            Assert.Equal("1", Assert.Single(rows)["id"]);
        }
        using (var listen = SqlCapture.Listen())
        {
            var rows = await Db.Query<Topic>().Where(f => f.Id == "1").Select<Dictionary<string, object?>>().ToListAsync(Ct);
            listen.AssertSql(TopicColumns + " WHERE t.`id` = ?");
            listen.AssertParams("1(String)");
            Assert.Equal("1", Assert.Single(rows)["id"]);
        }
        using (var listen = SqlCapture.Listen())
        {
            var rows = await Db.Query<Topic>().Where(f => f.Id == "1").Select(s => new Dictionary<string, object?>()).ToListAsync(Ct);
            listen.AssertSql("SELECT * FROM `t_topic` t WHERE t.`id` = ?");
            listen.AssertParams("1(String)");
            Assert.Equal(4, Assert.Single(rows).Count);
        }
    }

    [Fact, Upstream("QueryTest11.java#testx2", Deviation = "Java 先 selectAll 再 set(url) 会把 my_url 写两遍；C# 的成员初始化每个成员只出现一次")]
    public void Testx2()
    {
        Assert.Equal("SELECT t.`order` AS `score` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? LIMIT 1",
            Db.Query<BlogEntity>().Where(o => o.Id == "2").Select(o => new BlogEntityVO1 { Score = o.Order }).Take(1).ToSql());
        Assert.Equal("SELECT t.`content`,t.`url` AS `my_url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ?",
            Db.Query<BlogEntity>().SelectExcept<BlogEntityTest2>(o => o.Title).ToSql());
    }

    [Fact, Upstream("QueryTest11.java#testx3")]
    public async Task Testx3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "2" && o.Id == "3" && (o.Id == "4" || o.Id == "5")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `id` = ? AND `id` = ? AND (`id` = ? OR `id` = ?)");
        listen.AssertParams("false(Boolean),2(String),3(String),4(String),5(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx4")]
    public async Task Testx4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).Select(o => new TopicSubQueryBlog
        {
            Id = o.Id, Stars = o.Stars, Title = o.Title, CreateTime = o.CreateTime,
            BlogCount = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => Sql.Count())),
        }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`,(SELECT COUNT(*) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("QueryTest11.java#testx5")]
    public async Task Testx5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title != null).Select(o => new TopicSubQueryBlog
        {
            Id = o.Id, Stars = o.Stars, Title = o.Title, CreateTime = o.CreateTime,
            BlogCount = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => (long?)Sql.Sum(x.Star))),
        }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`,(SELECT SUM(t1.`star`) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `blog_count` FROM `t_topic` t WHERE t.`title` IS NOT NULL");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("QueryTest11.java#testx6")]
    public async Task Testx6()
    {
        using var listen = SqlCapture.Listen();
        var sub = Db.Query<BlogEntity>().Where(o => o.Id == "1");
        await Db.Query<Topic>().Where(o => Sql.Exists(sub.Where(q => q.Id == o.Id))).ToListAsync(Ct);
        listen.AssertSql(TopicColumns + " WHERE EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ? AND t1.`id` = t.`id`)");
        listen.AssertParams("false(Boolean),1(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx7")]
    public async Task Testx7()
    {
        using var listen = SqlCapture.Listen();
        var sub = Db.Query<BlogEntity>().Where(o => o.Id == "1");
        await Db.Query<Topic>().Where(o => Sql.NotExists(sub.Where(q => q.Id == o.Id))).ToListAsync(Ct);
        listen.AssertSql(TopicColumns + " WHERE NOT EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ? AND t1.`id` = t.`id`)");
        listen.AssertParams("false(Boolean),1(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx8")]
    public async Task Testx8()
    {
        using var listen = SqlCapture.Listen();
        var ids = Db.Query<BlogEntity>().Where(o => o.Id == "123").Select(o => o.Id);
        await Db.Query<Topic>().Where(o => o.Id.In(ids)).ToListAsync(Ct);
        listen.AssertSql(TopicColumns + " WHERE t.`id` IN (SELECT t1.`id` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ?)");
        listen.AssertParams("false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx10")]
    public async Task Testx10()
    {
        using var listen = SqlCapture.Listen();
        var ids = Db.Query<BlogEntity>().Where(o => o.Id == "123").Select(o => o.Id);
        await Db.Query<Topic>().Where(o => o.Id.NotIn(ids)).ToListAsync(Ct);
        listen.AssertSql(TopicColumns + " WHERE t.`id` NOT IN (SELECT t1.`id` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ?)");
        listen.AssertParams("false(Boolean),123(String)");
    }

    // Java 的 asAnyType(Integer.class) 只改类型：C# 的 (int?) 转换同样不写 CAST
    [Fact, Upstream("QueryTest11.java#testx11")]
    public async Task Testx11()
    {
        using var listen = SqlCapture.Listen();
        var query = Db.Query<TopicTypeTest1>().Where(o => o.Id == "123").Select(o => (int?)o.TopicType);
        await Db.Query<TopicTypeTest1>().Where(o => ((int?)o.TopicType).In(query)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`topic_type`,t.`create_time` FROM `t_topic_type` t WHERE t.`topic_type` IN (SELECT t1.`topic_type` FROM `t_topic_type` t1 WHERE t1.`id` = ?)");
        listen.AssertParams("123(String)");
    }

    // Java 的 toNumber(Integer.class)：C# 的 Sql.Cast<int>(..)
    [Fact, Upstream("QueryTest11.java#testx12")]
    public async Task Testx12()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Select(o => Sql.Cast<int>(o.TopicType)).ToListAsync(Ct);
        listen.AssertSql("SELECT CAST(t.`topic_type` AS SIGNED) FROM `t_topic_type` t");
    }

    [Fact, Upstream("QueryTest11.java#testx12_1")]
    public async Task Testx12_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Select(o => Sql.Cast<int>(o.TopicType)).FirstAsync(Ct);
        listen.AssertSql("SELECT CAST(t.`topic_type` AS SIGNED) FROM `t_topic_type` t LIMIT 1");
    }

    [Fact, Upstream("QueryTest11.java#testx13")]
    public async Task Testx13()
    {
        using var listen = SqlCapture.Listen();
        List<TopicTypeEnum?> types = [TopicTypeEnum.Classer, TopicTypeEnum.Student];
        await Db.Query<TopicTypeTest1>().Where(o => types.Contains(o.TopicType)).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "`topic_type` IN (?,?)");
        listen.AssertParams("9(Integer),1(Integer)");
    }

    // Java 的 isBlank / isNotBlank / isEmpty / isNotEmpty：C# 的 string.IsNullOrWhiteSpace / IsNullOrEmpty 及取反
    [Fact, Upstream("QueryTest11.java#testx14")]
    public async Task Testx14()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Where(o => o.Id!.Substring(1, 2) == "1" && o.Id.Substring(1, 2) != null
            && string.IsNullOrWhiteSpace(o.Id.Substring(1, 2)) && !string.IsNullOrWhiteSpace(o.Id.Substring(1, 2))
            && string.IsNullOrEmpty(o.Id.Substring(1, 2)) && !string.IsNullOrEmpty(o.Id.Substring(1, 2)) && string.IsNullOrWhiteSpace(o.Id)).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "SUBSTR(`id`,2,2) = ? AND SUBSTR(`id`,2,2) IS NOT NULL AND (SUBSTR(`id`,2,2) IS NULL OR SUBSTR(`id`,2,2) = '' OR LTRIM(SUBSTR(`id`,2,2)) = '') AND (SUBSTR(`id`,2,2) IS NOT NULL AND SUBSTR(`id`,2,2) <> '' AND LTRIM(SUBSTR(`id`,2,2)) <> '') AND (SUBSTR(`id`,2,2) IS NULL OR SUBSTR(`id`,2,2) = '') AND (SUBSTR(`id`,2,2) IS NOT NULL AND SUBSTR(`id`,2,2) <> '') AND (`id` IS NULL OR `id` = '' OR LTRIM(`id`) = '')");
        listen.AssertParams("1(String)");
    }

    // Java 的 now().plusMonths(-3)：C# 的 Sql.Now().AddMonths(-3)
    [Fact, Upstream("QueryTest11.java#testx15")]
    public async Task Testx15()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Where(o => o.CreateTime >= Sql.Now().AddMonths(-3)).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + " `create_time` >= date_add(NOW(), interval (?) month)");
        listen.AssertParams("-3(Long)");
    }

    // 只含常量的表达式（end.AddMonths(-3)）在 C# 里先在客户端算好，作为一个参数
    [Fact, Upstream("QueryTest11.java#testx16", Deviation = "Java 的 constant(end).plusMonths(-3) 写成 date_add(?, interval (?) month)；C# 在客户端算出结果，只传一个参数")]
    public async Task Testx16()
    {
        using var listen = SqlCapture.Listen();
        var begin = new DateTime(2020, 1, 1, 1, 1, 0);
        var end = new DateTime(2022, 1, 1, 1, 1, 0);
        await Db.Query<TopicTypeTest1>().Where(o => begin <= o.CreateTime && o.CreateTime <= end && o.CreateTime <= end.AddMonths(-3)).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "? <= `create_time` AND `create_time` <= ? AND `create_time` <= ?");
        listen.AssertParams("2020-01-01T01:01(LocalDateTime),2022-01-01T01:01(LocalDateTime),2021-10-01T01:01(LocalDateTime)");
    }

    [Fact, Upstream("QueryTest11.java#testx17")]
    public async Task Testx17()
    {
        var end = new DateTime(2022, 1, 1, 1, 1, 0);
        Assert.Empty(await Db.Query<TopicTypeTest1>().Where(o => end <= end.AddMonths(-3)).ToListAsync(Ct));
        // 恒真条件等于不过滤（Java 断言非空依赖其它用例写入的数据，这里与全表行数比较）
        Assert.Equal(await Db.Query<TopicTypeTest1>().CountAsync(Ct), (await Db.Query<TopicTypeTest1>().Where(o => end >= end.AddMonths(-3)).ToListAsync(Ct)).Count);
    }

    [Fact, Upstream("QueryTest11.java#testx18")]
    public async Task Testx18()
    {
        var end = new DateTime(2022, 1, 1, 1, 1, 0);
        Assert.Empty(await Db.Query<TopicTypeTest1>().Where(o => end <= end.AddMonths(-3)).ToListAsync(Ct));
        // 恒真条件等于不过滤（Java 断言非空依赖其它用例写入的数据，这里与全表行数比较）
        Assert.Equal(await Db.Query<TopicTypeTest1>().CountAsync(Ct), (await Db.Query<TopicTypeTest1>().Where(o => end >= end.AddMonths(-3)).ToListAsync(Ct)).Count);
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Where(o => string.Concat(o.Id, o.Title, o.Title!.Substring(1, 2)) == "123").ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "CONCAT(`id`,`title`,SUBSTR(`title`,2,2)) = ?");
        listen.AssertParams("123(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx20", Deviation = "别名编号：Java 的 union 分支与外层是 t、t2、t4、t6；C# 连续编号")]
    public async Task Testx20()
    {
        using var listen = SqlCapture.Listen();
        Query<Draft<string, long>> TypeCount(string type) =>
            Db.Query<Topic>().Where(o => o.Title == "123").Select(o => new Draft<string, long>(type, Sql.Count()));
        await TypeCount("type1").UnionAll(TypeCount("type2"), TypeCount("type3")).ToListAsync(Ct);
        listen.AssertSql("SELECT t3.`value1` AS `value1`,t3.`value2` AS `value2` FROM ( (SELECT ? AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t WHERE t.`title` = ?)  UNION ALL  (SELECT ? AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t1 WHERE t1.`title` = ?)  UNION ALL  (SELECT ? AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t2 WHERE t2.`title` = ?) ) t3");
        listen.AssertParams("type1(String),123(String),type2(String),123(String),type3(String),123(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx21")]
    public async Task Testx21()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.CreateTime > Sql.Now().AddMonths(-3))
            .GroupBy(o => new { o.CreateTime!.Value.Year, o.CreateTime.Value.Month })
            .OrderBy(g => g.Key.Year).OrderBy(g => g.Key.Month)
            .Select(g => new Draft<int, int, int?>(g.Key.Year, g.Key.Month, g.Sum(e => e.Star))).ToListAsync(Ct);
        listen.AssertSql("SELECT YEAR(t.`create_time`) AS `value1`,MONTH(t.`create_time`) AS `value2`,SUM(t.`star`) AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? AND  t.`create_time` > date_add(NOW(), interval (?) month) GROUP BY YEAR(t.`create_time`),MONTH(t.`create_time`) ORDER BY YEAR(t.`create_time`) ASC,MONTH(t.`create_time`) ASC");
        listen.AssertParams("false(Boolean),-3(Long)");
    }

    // Java 的 deleted().not().eq(false)：C# 的 !o.Deleted == false
    [Fact, Upstream("QueryTest11.java#testx22")]
    public async Task Testx22()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => !o.Deleted == false).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND (NOT (`deleted`)) = ?");
        listen.AssertParams("false(Boolean),false(Boolean)");
    }

    [Fact, Upstream("QueryTest11.java#testx23")]
    public async Task Testx23()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => 0L == Sql.SubQuery(Db.Query<Topic>().Where(x => x.Id == o.Id).Select(x => Sql.Count()))
                                                && o.Star == Sql.SubQuery(Db.Query<Topic>().Where(x => x.Id == o.Id).Select(x => (int?)Sql.Count())))
            .Select(o => new Draft<string?, string?>(o.Id, o.Url)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`url` AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? AND ? = (SELECT COUNT(*) FROM `t_topic` t1 WHERE t1.`id` = t.`id`) AND t.`star` = (SELECT COUNT(*) FROM `t_topic` t2 WHERE t2.`id` = t.`id`)");
        listen.AssertParams("false(Boolean),0(Long)");
    }

    [Fact, Upstream("QueryTest11.java#testx24")]
    public async Task Testx24()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "123")
            .Select(o => new Draft<string?, string?, long>(o.Id, o.Url, Sql.SubQuery(Db.Query<Topic>().Where(x => x.Id == o.Id).Select(x => Sql.Count()))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`url` AS `value2`,(SELECT COUNT(*) FROM `t_topic` t1 WHERE t1.`id` = t.`id`) AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ?");
        listen.AssertParams("false(Boolean),123(String)");
    }

    /// <summary>Java 给 is_top 赋了两次值，两列都输出：C# 用两个映射到同一列名的属性表达。</summary>
    private sealed class BlogTopTwice
    {
        public string? Id { get; set; }

        [Column("is_top")]
        public bool IsTop { get; set; }

        [Column("is_top")]
        public bool IsTopLower { get; set; }

        public string? Content { get; set; }
    }

    // Java 的 equalsWith：C# 的 Sql.EqualsWith，写成 CASE x WHEN y THEN 1 ELSE 0 END
    [Fact, Upstream("QueryTest11.java#testx26", Deviation = "Java 的 proxy set 同名列也写 AS（t.`id` AS `id`），C# 成员初始化同名列不写")]
    public async Task Testx26()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.Id == "123" && (o.Id ?? o.Title!.Substring(1, 2)) == "1")
            .Select(o => new BlogTopTwice
            {
                Id = o.Id,
                IsTop = Sql.EqualsWith(o.Id, "1"),
                IsTopLower = Sql.EqualsWith(o.Id!.Substring(1, 2), o.Title!.ToLower()),
                Content = o.Id ?? o.Title!.Substring(1, 2),
            }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,(CASE t.`id` WHEN ? THEN 1 ELSE 0 END) AS `is_top`,(CASE SUBSTR(t.`id`,2,2) WHEN LOWER(t.`title`) THEN 1 ELSE 0 END) AS `is_top`,IFNULL(t.`id`,SUBSTR(t.`title`,2,2)) AS `content` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? AND IFNULL(t.`id`,SUBSTR(t.`title`,2,2)) = ?");
        listen.AssertParams("1(String),false(Boolean),123(String),1(String)");
    }

    [Fact, Upstream("QueryTest11.java#testx27")]
    public async Task Testx27()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Where(o => (o.TopicType ?? TopicTypeEnum.Classer) == TopicTypeEnum.Student && o.TopicType == TopicTypeEnum.Student).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "IFNULL(`topic_type`,?) = ? AND `topic_type` = ?");
        listen.AssertParams("9(Integer),1(Integer),1(Integer)");
    }

    [Fact, Upstream("QueryTest11.java#testx28")]
    public async Task Testx28()
    {
        using var listen = SqlCapture.Listen();
        List<TopicTypeEnum> types = [TopicTypeEnum.Student, TopicTypeEnum.Classer, TopicTypeEnum.Teacher];
        await Db.Query<TopicTypeTest1>().Where(o => types.Contains(o.TopicType ?? TopicTypeEnum.Classer)).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "IFNULL(`topic_type`,?) IN (?,?,?)");
        listen.AssertParams("9(Integer),1(Integer),9(Integer),3(Integer)");
    }

    [Fact, Upstream("QueryTest11.java#testdraft")]
    public async Task TestDraft()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().GroupBy(t => t.Id).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t GROUP BY t.`id`");
    }

    private const string FormattedTopicSelect = "SELECT t.`title`,t.`id`,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') AS `create_time` FROM ";

    // Java 的 asc(NULLS_LAST) / desc(NULLS_FIRST)：C# 的 Sql.NullsLast / Sql.NullsFirst
    [Fact, Upstream("QueryTest11.java#testOrder", Deviation = "Java 的 selectExpression 里格式化列不写别名；C# 的投影列必须对应成员，写成 AS `create_time`")]
    public async Task TestOrder()
    {
        using (var listen = SqlCapture.Listen())
        {
            await Db.Query<Topic>().Where(o => o.Title == "title" && o.Id == "1")
                .OrderBy(o => o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss")).OrderBy(o => Sql.NullsLast(o.Stars))
                .OrderByDescending(o => o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss")).OrderByDescending(o => Sql.NullsFirst(o.Stars))
                .Select(o => new { o.Title, o.Id, CreateTime = o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss") }).ToListAsync(Ct);
            listen.AssertSql(FormattedTopicSelect + "`t_topic` t WHERE t.`title` = ? AND t.`id` = ? ORDER BY DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') ASC,CASE WHEN t.`stars` IS NULL THEN 1 ELSE 0 END ASC,t.`stars` ASC,DATE_FORMAT(t.`create_time`,'%Y-%m-%d %H:%i:%s') DESC,CASE WHEN t.`stars` IS NULL THEN 0 ELSE 1 END ASC,t.`stars` DESC");
            listen.AssertParams("title(String),1(String)");
        }
        await AssertJsonLike();
        await AssertEnumLike();
    }

    // JSON 列上的 LIKE：(string)(object)x 只改类型；JSON 列的等值比较按转换器序列化参数
    private async Task AssertJsonLike()
    {
        using var listen = SqlCapture.Listen();
        var single = new List<TopicTypeJsonValue> { new() { Name = "123" } };
        var value = new TopicTypeJsonValue { Age = 1, Name = "1" };
        await Db.Query<TopicTypeArrayJson>()
            .Where(o => ((string)(object)o.Title2!).Contains("123") && o.Title2 == single && ((string)(object)o.Title!).Contains("456") && o.Title == value && o.Id == "1")
            .OrderBy(o => o.Title2)
            .Select(o => new { Title = (string)(object)o.Title!, o.Id, CreateTime = o.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss") }).ToListAsync(Ct);
        listen.AssertSql(FormattedTopicSelect + "`t_topic_type_array` t WHERE t.`title2` LIKE ? AND t.`title2` = ? AND t.`title` LIKE ? AND t.`title` = ? AND t.`id` = ? ORDER BY t.`title2` ASC");
        listen.AssertParams("%123%(String),[{\"name\":\"123\",\"age\":null}](String),%456%(String),{\"name\":\"1\",\"age\":1}(String),1(String)");
    }

    private async Task AssertEnumLike()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Where(o => ((string)(object)o.TopicType!).Contains("1")).ToListAsync(Ct);
        listen.AssertSql(TopicTypeSelect + "`topic_type` LIKE ?");
        listen.AssertParams("%1%(String)");
    }

    [Fact, Upstream("QueryTest11.java#testOrder1", Deviation = "同 testOrder；JSON 参数按 System.Text.Json 的属性声明顺序序列化（name 在前），Java 的 Jackson 按字母序（age 在前）")]
    public Task TestOrder1() => AssertJsonLike();

    [Fact, Upstream("QueryTest11.java#testOrder2")]
    public Task TestOrder2() => AssertEnumLike();

    private sealed class MyTest
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    [Fact, Upstream("QueryTest11.java#testOrder3", Deviation = "Java 的 proxy set 同名列也写 AS（t.`id` AS `id`），C# 成员初始化同名列不写")]
    public async Task TestOrder3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TopicTypeTest1>().Where(o => ((string)(object)o.TopicType!).Contains("1")).Select(t => new MyTest { Id = t.Id, Name = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title` AS `name` FROM `t_topic_type` t WHERE t.`topic_type` LIKE ?");
        listen.AssertParams("%1%(String)");
    }
}
