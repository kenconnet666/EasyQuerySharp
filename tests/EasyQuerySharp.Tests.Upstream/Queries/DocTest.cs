namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：doc/DocTest.java（文档示例：计数、连接后挑列、按组计数、分页、日期格式条件、空集合 IN、投影里的标量子查询）。
/// Java 的 FETCHER.id().title() / allFieldsExclude 在 C# 写成成员初始化或 <c>Sql.Without</c>。
/// </summary>
public sealed class DocTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static readonly DateTime Jan2021 = new(2021, 1, 1, 1, 1, 0);
    private static readonly DateTime Feb2022 = new(2022, 2, 1, 3, 4, 0);
    private const string JoinWhere = " FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`title` LIKE ? AND t1.`create_time` <= ?";
    private const string FilterOrder = " FROM `t_topic` t WHERE t.`title` LIKE ? AND t.`create_time` >= ? ORDER BY t.`id` ASC,t.`create_time` DESC";
    private const string BlogExceptTitleTop = "t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`";

    private static async Task<SqlStatement> Failed(Func<Task> action) => (await Assert.ThrowsAsync<EasyQueryException>(action)).Statement!;

    [Fact, Upstream("doc/DocTest.java#testDoc3x", NotApplicable = "Java 用例体全部被注释掉，没有可移植的内容")]
    public void TestDoc3x() { }

    [Fact, Upstream("doc/DocTest.java#testTopic", Deviation = "最后一段 Java 用 stars().as(o.stars()) 给同名列强加别名（t.`stars` AS `stars`）；C# 列与成员同名时不写 AS")]
    public async Task TestTopic()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(o => o.Title!.Contains("11") && o.CreateTime <= Jan2021).CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) FROM `t_topic` WHERE `title` LIKE ? AND `create_time` <= ?");
        listen.AssertParams("%11%(String),2021-01-01T01:01(LocalDateTime)");

        var joined = Db.Query<Topic>().LeftJoin<Topic>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Title!.Contains("11") && t1.CreateTime <= Jan2021);
        await joined.Select((t, t1) => new Topic { Id = t.Id, Stars = t.Stars, Title = t1.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t1.`id` AS `title`" + JoinWhere);
        listen.AssertParams("%11%(String),2021-01-01T01:01(LocalDateTime)");
        await joined.Select((t, t1) => new Topic { Id = t.Id, Stars = t1.Stars, Title = t1.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`stars`,t1.`id` AS `title`" + JoinWhere);

        // 整表去掉 title、id 两列，id 改取连接表的 title
        await Db.Query<Topic>().LeftJoin<Topic>((t, t1) => t.Id == t1.Id).OrderBy((t, t1) => t.Id).ThenByDescending((t, t1) => t1.CreateTime)
            .Select((t, t1) => new Topic { Stars = t.Stars, CreateTime = t.CreateTime, Id = t1.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`create_time`,t1.`title` AS `id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` ORDER BY t.`id` ASC,t1.`create_time` DESC");

        var filtered = Db.Query<Topic>().Where(o => o.Title!.Contains("123") && o.CreateTime >= Feb2022).OrderBy(o => o.Id).ThenByDescending(o => o.CreateTime);
        await filtered.Select(o => new Topic { Id = o.Id, Title = o.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title`" + FilterOrder);
        listen.AssertParams("%123%(String),2022-02-01T03:04(LocalDateTime)");
        await filtered.Select(o => Sql.Without(o, o.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`stars`,t.`title`,t.`create_time`" + FilterOrder);

        await Db.Query<Topic>().Where(o => o.Title!.Contains("123") && o.CreateTime >= Feb2022).GroupBy(o => o.Id)
            .Select(g => new Topic { Id = g.Key, Stars = (int)g.Count(o => o.Id) }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `stars` FROM `t_topic` t WHERE t.`title` LIKE ? AND t.`create_time` >= ? GROUP BY t.`id`");

        // 分页的数据查询（总数为 0 时 C# 不发数据查询，这里直接渲染第一页）
        Assert.Equal("SELECT t.`id`,t.`title`" + FilterOrder + " LIMIT 20", filtered.Select(o => new Topic { Id = o.Id, Title = o.Title }).Take(20).ToSql());

        await filtered.Select(o => new Topic { Id = o.Id, Title = o.Title, Stars = o.Stars }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`title`,t.`stars`" + FilterOrder);
    }

    [Fact, Upstream("doc/DocTest.java#test2")]
    public async Task Test2()
    {
        var statement = await Failed(() => Db.Query<DocSysUser>().AsTable("a222")
            .Where(o => o.Id == "1" && o.Id == o.CreateTime!.Value.ToString("yyyy-MM-dd") && o.CreateTime.Value.ToString("yyyy-MM-dd") == "2023-01-02"
                        && (o.Name ?? "unknown").Contains("123") && !string.IsNullOrWhiteSpace(o.Phone))
            .Select(o => new DocSysUser { Id = o.Id, Name = o.Name, Phone = o.Phone, DepartName = o.DepartName }).ToListAsync(Ct));
        Assert.Equal("SELECT t.`id`,t.`name`,t.`phone`,t.`depart_name` FROM `a222` t WHERE t.`id` = ? AND  t.`id` = DATE_FORMAT(t.`create_time`,'%Y-%m-%d') AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') = ? AND IFNULL(t.`name`,?) LIKE ? AND (t.`phone` IS NOT NULL AND t.`phone` <> '' AND LTRIM(t.`phone`) <> '')",
            statement.Text);

        // 空集合：IN 恒假、NOT IN 恒真
        using var listen = SqlCapture.Listen();
        var days = new List<string> { "2023-01-02", "2023-01-03" };
        var none = new List<string>();
        await Db.Query<BlogEntity>().Where(o => days.Contains(o.CreateTime!.Value.ToString("yyyy-MM-dd")) && !days.Contains(o.CreateTime.Value.ToString("yyyy-MM-dd"))
                                                && none.Contains(o.CreateTime.Value.ToString("yyyy-MM-dd")) && !none.Contains(o.CreateTime.Value.ToString("yyyy-MM-dd")))
            .Select(o => Sql.Without(o, o.Title, o.Top)).ToListAsync(Ct);
        listen.AssertSql($"SELECT {BlogExceptTitleTop} FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') IN (?,?) AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') NOT IN (?,?) AND 1 = 2 AND 1 = 1");
        listen.AssertParams("false(Boolean),2023-01-02(String),2023-01-03(String),2023-01-02(String),2023-01-03(String)");

        // 分组后把计数转成字符串放进 phone（Java 的 count().toStr()）
        var grouped = await Failed(() => Db.Query<DocSysUser>().AsTable("a222").Where(o => o.Id == "1" && o.Id!.Contains("123")).GroupBy(o => o.Id)
            .Select(g => new DocSysUser { Id = g.Key, Phone = g.Count().ToString() }).ToListAsync(Ct));
        Assert.Equal("SELECT t.`id` AS `id`,CAST(COUNT(*) AS CHAR) AS `phone` FROM `a222` t WHERE t.`id` = ? AND t.`id` LIKE ? GROUP BY t.`id`", grouped.Text);
    }

    [Fact, Upstream("doc/DocTest.java#testLikeOr")]
    public async Task TestLikeOr()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>()
            .Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2020") || o.CreateTime.Value.ToString("yyyy-MM-dd").StartsWith("2021")
                        || o.CreateTime.Value.ToString("yyyy-MM-dd").StartsWith("2022"))
            .Select(o => Sql.Without(o, o.Title, o.Top)).ToListAsync(Ct);
        listen.AssertSql($"SELECT {BlogExceptTitleTop} FROM `t_blog` t WHERE t.`deleted` = ? AND (DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ? OR DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ? OR DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ?)");
        listen.AssertParams("false(Boolean),2020%(String),2021%(String),2022%(String)");
    }

    private const string SubQueryOverride = "Java 先选 allFieldsExclude 的列（含 create_time），再把子查询 AS `create_time` 追加在后，读取时后者覆盖前者；C# 的成员初始化每个成员只写一次，create_time 直接取子查询";

    // 投影里的标量子查询覆盖 create_time
    [Fact, Upstream("doc/DocTest.java#testSubQuerySelect", Deviation = SubQueryOverride)]
    public async Task TestSubQuerySelect()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2023"))
            .Select(o => new BlogEntity
            {
                Id = o.Id, CreateTime = Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => x.CreateTime)),
                UpdateTime = o.UpdateTime, CreateBy = o.CreateBy, UpdateBy = o.UpdateBy, Deleted = o.Deleted, Content = o.Content, Url = o.Url, Star = o.Star,
                PublishTime = o.PublishTime, Score = o.Score, Status = o.Status, Order = o.Order, IsTop = o.IsTop,
            }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,(SELECT t1.`create_time` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top` FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ?");
        listen.AssertParams("false(Boolean),false(Boolean),2023%(String)");
    }

    [Fact, Upstream("doc/DocTest.java#testSubQuerySelect2", Deviation = SubQueryOverride)]
    public async Task TestSubQuerySelect2()
    {
        using var listen = SqlCapture.Listen();
        var counts = await Db.Query<BlogEntity>().Where(o => o.CreateTime!.Value.ToString("yyyy-MM-dd").StartsWith("2023"))
            .Select(o => new Draft<string?, long>(o.Id, Sql.SubQuery(Db.Query<BlogEntity>().Where(x => x.Id == o.Id).Select(x => Sql.Count(x.Id))))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,(SELECT COUNT(t1.`id`) FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = t.`id`) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m-%d') LIKE ?");
        listen.AssertParams("false(Boolean),false(Boolean),2023%(String)");
        Assert.All(counts, c => Assert.Equal(1, c.Value2));
    }
}
