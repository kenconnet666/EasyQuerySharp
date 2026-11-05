// 条件在数据库里执行：字符串包含是要翻译的写法本身
#pragma warning disable CA1307, CA1310

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest14.java（列与列的 LIKE、常量转日期再加减、动态表名、Oracle 的格式化与 ROWNUM 分页、原生片段）。
/// Java 的 like(列) 对应 <c>a.Contains(b)</c>（b 是列时值在 SQL 里拼 %），asAnyType 只改类型：C# 经 object 强转。
/// </summary>
public sealed class QueryTest14(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 经 getClientQueryable() 用属性名字符串加条件：C# 的 Sql.Column(t, "Id")
    [Fact, Upstream("QueryTest14.java#test111")]
    public async Task Test111() =>
        await Db.Query<Topic>().Where(t => Sql.Column<string>(t, nameof(Topic.Id)) == "123").OrderByDescending(x => x.CreateTime).ToListAsync(Ct);

    // Java 用例没有断言：Map 投影全部列再加一列、原生条件与原生投影
    [Fact, Upstream("QueryTest14.java#test1119")]
    public async Task Test1119()
    {
        var joined = Db.Query<SysUser>().LeftJoin<Topic>((s, t2) => s.Id == t2.Id).Where((s1, _) => s1.Id == "1");
        await joined.Select((s1, t2) => new Dictionary<string, object?>(Sql.Columns(s1)) { { "abc", t2.Title } }).ToListAsync(Ct);
        var raw = joined.Where((s1, t2) => Sql.Raw<bool>("{0} = IFNULL({1},1)", s1.Id, t2.Title));
        await raw.Select((s1, _) => new Draft<string>(Sql.Raw<string>("IFNULL({0},2)", s1.IdCard))).ToListAsync(Ct);
        await raw.Select((s1, _) => new TopicTypeVO { Id = Sql.Raw<string>("IFNULL({0},2)", s1.IdCard) }).ToListAsync(Ct);
    }

    // Java 的 constant(..).toDateTime(..).plus(1, DAYS)：Sql.Cast<DateTime>(Sql.Constant(..)).AddDays(1)
    [Fact, Upstream("QueryTest14.java#testPlus"), Upstream("QueryTest14.java#testPlus_1")]
    public async Task TestPlus()
    {
        var format = new DateTime(2020, 1, 1, 1, 1, 0).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.CreateTime < Sql.Cast<DateTime>(Sql.Constant(format)).AddDays(1)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE  `create_time` < date_add(CAST(? AS DATETIME), interval (?) day)");
        listen.AssertParams("2020-01-01 01:01:00(String),1(Long)");
    }

    // 后半段是无实体的表（Java 的 queryable(Map.class).asTable(..)）：常量写成 SQL 字面量用 Sql.Raw
    [Fact, Upstream("QueryTest14.java#testPropertyFunc")]
    public async Task TestPropertyFunc()
    {
        var format = new DateTime(2020, 1, 1, 1, 1, 0).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.CreateTime < Sql.Cast<DateTime>(Sql.Constant(format))).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE  `create_time` < CAST(? AS DATETIME)");
        listen.AssertParams("2020-01-01 01:01:00(String)");
        Assert.Equal("SELECT * FROM `my_table` WHERE  `create_time` < CAST('2020-01-01 01:01:00' AS DATETIME)",
            Db.Table("my_table").Where(t => t.Get<DateTime?>("create_time") < Sql.Cast<DateTime>(Sql.Raw<string>("'" + format + "'"))).ToSql());
    }

    // 无实体的表：选列并改名、格式化、计数；按列分组拼接；按列过滤读成行
    [Fact, Upstream("QueryTest14.java#testMapQuery")]
    public async Task TestMapQuery()
    {
        Assert.Equal("SELECT `col001` AS `姓名`,`col002` AS `年龄`,DATE_FORMAT(`create_time`,'%Y-%m-%d') AS `创建时间`,COUNT(1) AS `c1`,COUNT(`id`) AS `c2` FROM `t_topic`",
            Db.Table("t_topic").Select(t => new DynamicRow
            {
                { "姓名", t.Get<object>("col001") }, { "年龄", t.Get<object>("col002") }, { "创建时间", t.Get<DateTime>("create_time").ToString("yyyy-MM-dd") },
                { "c1", Sql.Count(Sql.Raw<int>("1")) }, { "c2", Sql.Count(t.Get<object>("id")) },
            }).ToSql());
        Assert.Equal("SELECT `col001` AS `姓名`,GROUP_CONCAT(`col002` SEPARATOR ?) AS `年龄集合` FROM `t_topic` GROUP BY `col001`",
            Db.Table("t_topic").GroupBy(t => t.Get<object>("col001"))
                .Select(g => new DynamicRow { { "姓名", g.Key }, { "年龄集合", g.StringJoin(x => x.Get<string>("col002"), ",") } }).ToSql());
        await Db.Query<Topic>().GroupBy(t => t.Title).Select(g => new Draft<string?, string>(g.Key, g.StringJoin(x => x.Id, ","))).ToListAsync(Ct);

        using var listen = SqlCapture.Listen();
        await Db.Table("t_topic").Where(m => m.Get<int>("id") == 1).ToListAsync(Ct);
        listen.AssertSql("SELECT * FROM `t_topic` WHERE `id` = ?");
        listen.AssertParams("1(Integer)");
    }

    // 先选列再 JOIN 另一个无实体的查询：两边都作为派生表
    [Fact, Upstream("QueryTest14.java#testDynamicTable2")]
    public void TestDynamicTable2() =>
        Assert.Equal("SELECT t1.`id` AS `id` FROM (SELECT t.`id` AS `id` FROM `aaa` t) t1 LEFT JOIN (SELECT * FROM `bbb` t2) t3 ON t1.`id` = t3.`id`",
            Db.Table("aaa").SelectColumns("id").LeftJoin(Db.Table("bbb"), (a, b) => a.Get<string>("id") == b.Get<string>("id")).ToSql());

    private Query<BlogEntity, Topic> BlogWithTopic => Db.Query<BlogEntity>().LeftJoin<Topic>((b, t2) => b.Id == t2.Id);

    [Fact, Upstream("QueryTest14.java#testLikeColumn")]
    public async Task TestLikeColumn()
    {
        using var listen = SqlCapture.Listen();
        await BlogWithTopic.Where((b1, t2) => b1.Id!.Contains(t2.Id!)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t.`id` LIKE CONCAT('%',t1.`id`,'%')");
        listen.AssertParams("false(Boolean)");
        await BlogWithTopic.Where((b1, t2) => !b1.Id!.Contains(t2.Id!)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND (NOT (t.`id` LIKE CONCAT('%',t1.`id`,'%')))");
        listen.AssertParams("false(Boolean)");
        await BlogWithTopic.Where((b1, t2) => b1.Id!.Contains(t2.Id!.Substring(1, 2))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t.`id` LIKE CONCAT('%',SUBSTR(t1.`id`,2,2),'%')");
        listen.AssertParams("false(Boolean)");
        await BlogWithTopic.Where((b1, t2) => (b1.Id ?? "123").Contains(t2.Id!.Substring(1, 2))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND IFNULL(t.`id`,?) LIKE CONCAT('%',SUBSTR(t1.`id`,2,2),'%')");
        listen.AssertParams("false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest14.java#testLikeColumn2")]
    public async Task TestLikeColumn2()
    {
        using var listen = SqlCapture.Listen();
        await BlogWithTopic.Where((b1, t2) => b1.Id!.Contains((string)(object)t2.Stars!)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t.`id` LIKE CONCAT('%',t1.`stars`,'%')");
        listen.AssertParams("false(Boolean)");
        await BlogWithTopic.Where((b1, t2) => b1.Id!.Contains(t2.Stars.ToString()!) && b1.Id!.Contains("123")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t.`id` LIKE CONCAT('%',CAST(t1.`stars` AS CHAR),'%') AND t.`id` LIKE ?");
        listen.AssertParams("false(Boolean),%123%(String)");
    }

    // Java 的 orderByObject 传了空的排序请求：不加排序；Topic 的列在 VO 里没有对应属性，只剩 order → top
    [Fact, Upstream("QueryTest14.java#testClose")]
    public async Task TestClose()
    {
        Assert.NotEmpty(await Db.SqlQueryAsync<Dictionary<string, object?>>("select * from t_topic", cancellationToken: Ct));
        SqlOnly.AssertStatement(BlogWithTopic.Select((b1, _) => new BlogEntityVO1 { Top = (bool?)(object)b1.Order! }).ToStatement(), "SELECT t.`order` AS `top` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ?", "false(Boolean)");
    }

    // Java 的 asTable(o -> o + tail)：AsTable(name => ..)；toDateTime 是 CAST(.. AS DATETIME)；select("*")：SelectRaw("*")
    [Fact, Upstream("QueryTest14.java#testDynamicTable")]
    public void TestDynamicTable()
    {
        DateTime? star = new DateTime(2022, 1, 1, 1, 1, 0);
        DateTime? end = new DateTime(2023, 1, 1, 1, 1, 0);
        var tableTail = "_hot";
        SqlOnly.AssertStatement(Db.Query<MultiColumnEntity>().AsTable(o => o + tableTail)
                .WhereIf(star is not null, m => Sql.Cast<DateTime>(m.Col8) > star)
                .WhereIf(end is not null, m => Sql.Cast<DateTime>(m.Col9) < end)
                .SelectRaw("*").ToStatement(),
            "SELECT * FROM `multi_hot` WHERE CAST(`col8` AS DATETIME) > ? AND CAST(`col9` AS DATETIME) < ?", "2022-01-01T01:01(LocalDateTime),2023-01-01T01:01(LocalDateTime)");
    }

    // Oracle：日期格式化成 TO_CHAR，分页用两层 ROWNUM
    [Fact, Upstream("QueryTest14.java#testOracle")]
    public void TestOracle()
    {
        var db = SqlOnly.Db("oracle");
        SqlOnly.AssertStatement(db.Query<BlogEntity>().Where(b => b.CreateTime!.Value.ToString("yyyy年MM月dd日 HH时mm分ss秒") == "2022年01月01日 01时01分01秒").ToStatement(),
            "SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND TO_CHAR(\"create_time\",'YYYY\"年\"MM\"月\"DD\"日\" HH24\"时\"MI\"分\"SS\"秒\"') = ?", "false(Boolean),2022年01月01日 01时01分01秒(String)");
        SqlOnly.AssertStatement(db.Query<BlogEntity>().Where(b => b.Id == "123").GroupBy(b => b.Title)
                .Select(g => new BlogEntity { Title = g.Key, Star = (int)g.Count(x => x.Id) }).Limit(20, 10).ToStatement(),
            "SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT t.\"title\" AS \"title\",COUNT(t.\"id\") AS \"star\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"id\" = ? GROUP BY t.\"title\") rt WHERE ROWNUM < 31) rt1 WHERE rt1.\"__rownum__\" > 20", "false(Boolean),123(String)");
        SqlOnly.AssertStatement(db.Query<BlogEntity>().Where(b => b.Id == "123").OrderBy(b => b.Star).Limit(20, 10).ToStatement(), "SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ? ORDER BY \"star\" ASC) rt WHERE ROWNUM < 31) rt1 WHERE rt1.\"__rownum__\" > 20", "false(Boolean),123(String)");
        SqlOnly.AssertStatement(db.Query<BlogEntity>().Where(b => b.Id == "123").OrderBy(b => b.Star).Take(10).ToStatement(), "SELECT rt.* FROM (SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ? ORDER BY \"star\" ASC) rt WHERE ROWNUM < 11", "false(Boolean),123(String)");
    }
}
