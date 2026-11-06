using Company = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.Company;

namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>
/// ClickHouse 方言自身的写法（Java 只有 <see cref="ClickHouseTest"/> 的 4 个更新 / 删除用例，这里是 C# 独有的）：
/// 分页、mutation 写入、连接、分组、函数表与建表。只渲染，不连库。
/// </summary>
public sealed class ClickHouseDialectTest
{
    private const string TopicColumns = "SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`";
    private const string Day2025 = "2025-01-01T00:00(LocalDateTime)";
    private static readonly DateTime Day = new(2025, 1, 1);

    private static EasyDb ClickHouse => SqlOnly.Db("clickhouse");

    [Fact]
    public void PagingUsesLimitOffset()
    {
        SqlOnly.AssertStatement(ClickHouse.Query<Topic>().Where(t => t.Title == "a").OrderBy(t => t.Id).Skip(10).Take(5).ToStatement(),
            TopicColumns + " WHERE `title` = ? ORDER BY `id` ASC LIMIT 5 OFFSET 10", "a(String)");
        Assert.Equal(TopicColumns + " LIMIT 3", ClickHouse.Query<Topic>().Take(3).ToSql());
    }

    // 插入是普通的 INSERT；更新、删除是 mutation，没有条件时补 WHERE 1 = 1（mutation 必须带条件）
    [Fact]
    public void WritesAreMutations()
    {
        SqlOnly.AssertStatement(ClickHouse.Insert(new Topic { Id = "1", Stars = 2, Title = "t", CreateTime = Day }).ToStatement(),
            "INSERT INTO `t_topic` (`id`,`stars`,`title`,`create_time`) VALUES (?,?,?,?)", "1(String),2(Integer),t(String)," + Day2025);
        SqlOnly.AssertStatement(ClickHouse.Update<Topic>().Set(t => t.Stars, 3).Where(t => t.Title == "a").ToStatement(),
            "ALTER TABLE `t_topic` UPDATE `stars` = ? WHERE `title` = ?", "3(Integer),a(String)");
        SqlOnly.AssertStatement(ClickHouse.Delete<Topic>().Where(t => t.CreateTime < Day).ToStatement()!,
            "ALTER TABLE `t_topic` DELETE WHERE `create_time` < ?", Day2025);
        Assert.Equal("ALTER TABLE `t_topic` DELETE WHERE 1 = 1", ClickHouse.Delete<Topic>().All().ToSql());
        // 驱动执行的文本：DateTime 参数写明 DateTime64(7)（驱动按值推断的 DateTime 只到秒），其余按 @p 由驱动推断类型
        Assert.Equal("ALTER TABLE `t_topic` DELETE WHERE `create_time` < {p0:DateTime64(7)} AND `stars` > @p1",
            ClickHouse.Delete<Topic>().Where(t => t.CreateTime < Day && t.Stars > 1).ToStatement()!.CommandText);
    }

    // ALTER TABLE 不能带别名、不能连接；也没有 upsert 与自增列
    [Fact]
    public void UnsupportedWrites()
    {
        Assert.Contains("不能带表别名", Assert.Throws<NotSupportedException>(() => ClickHouse.Delete<Company>().Where(c => c.Users!.Where(u => u.Age > 18).Any()).ToSql()).Message);
        Assert.Contains("upsert", Assert.Throws<NotSupportedException>(() => ClickHouse.Insert(new Topic { Id = "1" }).OnConflictUpdate(t => t.Title).ToSql()).Message);
        Assert.Contains("没有自增列", Assert.Throws<NotSupportedException>(() => ClickHouse.CodeFirst.CreateTableSql(typeof(TopicAuto))).Message);
    }

    [Fact]
    public void JoinAndGroupBy()
    {
        SqlOnly.AssertStatement(ClickHouse.Query<BlogEntity>().LeftJoin<Topic>((b, t) => b.Id == t.Id).Where((_, t) => t.Title == "x").Select((b, t) => new Draft<string?, string?>(b.Id, t.Title)).ToStatement(),
            "SELECT t.`id` AS `value1`,t1.`title` AS `value2` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t1.`title` = ?", "false(Boolean),x(String)");
        Assert.Equal("SELECT t.`title` AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t GROUP BY t.`title` LIMIT 2 OFFSET 2",
            ClickHouse.Query<Topic>().GroupBy(t => t.Title).Select(g => new Draft<string?, long>(g.Key, g.Count())).Skip(2).Take(2).ToSql());
        // 字符串聚合：groupArray 再拼接；带组内排序的没有对应写法
        Assert.Equal("SELECT t.`stars` AS `value1`,arrayStringConcat(groupArray(t.`title`),?) AS `value2` FROM `t_topic` t GROUP BY t.`stars`",
            ClickHouse.Query<Topic>().GroupBy(t => t.Stars).Select(g => new Draft<int?, string>(g.Key, g.StringJoin(x => x.Title, ","))).ToSql());
    }

    // 函数名区分大小写：按字符处理的用 UTF8 版本，日期用 toYear / addDays / dateDiff，格式化的分钟是 %i
    [Fact]
    public void FunctionTable()
    {
        Assert.Equal("SELECT CHAR_LENGTH(t.`title`) AS `value1`,substringUTF8(t.`title`,2,2) AS `value2`,(positionUTF8(t.`title`,?) - 1) AS `value3`,leftPadUTF8(t.`title`, 5, ?) AS `value4` FROM `t_topic` t",
            ClickHouse.Query<Topic>().Select(t => new Draft<int, string, int, string>(t.Title!.Length, t.Title!.Substring(1, 2), t.Title!.IndexOf("x"), t.Title!.PadLeft(5, '0'))).ToSql());
        Assert.Equal("SELECT addDays(t.`create_time`,?) AS `value1`,addMonths(t.`create_time`,?) AS `value2`,toYear(t.`create_time`) AS `value3`,(toDayOfWeek(t.`create_time`) % 7) AS `value4`,toDayOfWeek(t.`create_time`) AS `value5`,toDayOfYear(t.`create_time`) AS `value6` FROM `t_topic` t",
            ClickHouse.Query<Topic>().Select(t => new Draft<DateTime, DateTime, int, int, int, int>(t.CreateTime!.Value.AddDays(2), t.CreateTime!.Value.AddMonths(1), t.CreateTime!.Value.Year,
                (int)t.CreateTime!.Value.DayOfWeek, t.CreateTime!.Value.IsoDayOfWeek(), t.CreateTime!.Value.DayOfYear)).ToSql());
        Assert.Equal("SELECT formatDateTime(t.`create_time`,'%Y-%m-%d %H:%i:%S') AS `value1`,toDateTime(date_trunc('month',t.`create_time`)) AS `value2`,NOW() AS `value3`,NOW('UTC') AS `value4` FROM `t_topic` t",
            ClickHouse.Query<Topic>().Select(t => new Draft<string, DateTime?, DateTime, DateTime>(t.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss"), t.CreateTime.DateTrunc(DateTruncUnit.Month), Sql.Now(), Sql.UtcNow())).ToSql());
        // 相差按经过的秒数整除（dateDiff 本身数的是跨过的边界）
        Assert.Equal("SELECT intDiv(dateDiff('second',t.`create_time`,?),86400) AS `value1`,dateDiff('millisecond',t.`create_time`,?) AS `value2` FROM `t_topic` t",
            ClickHouse.Query<Topic>().Select(t => new Draft<long, long>((long)(Day - t.CreateTime!.Value).TotalDays, (long)(Day - t.CreateTime!.Value).TotalMilliseconds)).ToSql());
        Assert.Equal("SELECT (log(t.`stars`) / log(?)) AS `value1`,toString(t.`stars`) AS `value2`,CAST(t.`title` AS Int32) AS `value3`,GREATEST(t.`stars`,?,?) AS `value4` FROM `t_topic` t",
            ClickHouse.Query<Topic>().Select(t => new Draft<double, string, int, int?>(Math.Log((double)t.Stars!, 2), Sql.Cast<string>(t.Stars), Sql.Cast<int>(t.Title), Sql.MaxColumns(t.Stars, 1, 2))).ToSql());
    }

    // 值里含 % 或 _ 时转义后按字面匹配：反斜杠本来就是 LIKE 的转义符，不写 ESCAPE
    [Fact]
    public void LikeEscapesWildcardValues() =>
        SqlOnly.AssertStatement(ClickHouse.Query<Topic>().Where(t => t.Title!.Contains("30%") && !t.Title!.StartsWith("a_")).ToStatement(),
            TopicColumns + " WHERE `title` LIKE ? AND `title` NOT LIKE ?", "%30\\%%(String),a\\_%(String)");

    // MergeTree 的主键就是排序键；可空列用 NULL 修饰（即 Nullable）
    [Fact]
    public void CreateTableUsesMergeTree() =>
        Assert.Equal("CREATE TABLE IF NOT EXISTS `t_topic` (`id` String NOT NULL, `stars` Int32 NULL, `title` String NULL, `create_time` DateTime64(3) NULL) ENGINE = MergeTree ORDER BY (`id`)",
            ClickHouse.CodeFirst.CreateTableSql(typeof(Topic)));
}
