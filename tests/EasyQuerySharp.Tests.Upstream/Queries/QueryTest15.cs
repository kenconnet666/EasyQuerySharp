namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest15.java（计算列：全名、年龄、学生数；按属性名取列；CASE WHEN；日期格式与加减；Map 投影分页）。
/// Java 的 @Column(sqlConversion = …) 计算列对应 C# 的 <c>[Computed(nameof(静态表达式))]</c>。
/// </summary>
public sealed class QueryTest15(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 的学生数子查询写成 select(Long.class, s -> s.columnCount("id"))，COUNT 列多了 AS `id`；C# 的计算列是普通的计数子查询
    private const string CountAlias = "Java 的学生数子查询里 COUNT 列带 AS `id`、且多占一个别名编号（selectColumn 建的匿名表）；C# 的计算列写成 Select(s => s.Id).Count()，不写别名、不跳号";

    // 年龄计算列：C# 两个整数相除向零截断，要得到 Java 在 MySQL 上的小数除法要先转 double，365 随之是 Double 参数
    private const string AgeDivisor = "年龄计算列写成 (double)天数 / 365（C# 两个整数相除向零截断，Java 的 / 在 MySQL 上是小数除法），参数 365 是 Double，Java 是 Integer";

    // Java 的子查询多建了一张匿名表：学生表是 t2、外层派生表是 t3；C# 依次是 t1、t2
    private static string WithoutCountAlias(string javaSql) => javaSql
        .Replace("(SELECT COUNT(t2.`id`) AS `id` FROM `school_student` t2 WHERE t2.`class_id`", "(SELECT COUNT(t1.`id`) FROM `school_student` t1 WHERE t1.`class_id`", StringComparison.Ordinal)
        .Replace("COUNT(t1.`id`) AS `id`", "COUNT(t1.`id`)", StringComparison.Ordinal)
        .Replace("t3.", "t2.", StringComparison.Ordinal).Replace(") t3", ") t2", StringComparison.Ordinal);

    /// <summary>Java 的 UserExtraDTO：与 UserExtra 同名的属性。</summary>
    public sealed class UserExtraDto
    {
        public string? Id { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public DateTime? Birthday { get; set; }

        public string? FullName { get; set; }

        public int? Age { get; set; }
    }

    /// <summary>Java 的 SchoolClassAggregateDTO：班级与学生数。</summary>
    public sealed class SchoolClassAggregateDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public long? StudentSize { get; set; }
    }

    // 计算列在 SELECT 里写成表达式 AS 列名，条件里展开成表达式
    [Fact, Upstream("QueryTest15.java#test1", Deviation = CountAlias + "；" + AgeDivisor)]
    public async Task Test1()
    {
        string[] names = ["1", "2"];
        using var listen = SqlCapture.Listen();
        await Db.Query<UserExtra>().Where(u => u.FullName!.Contains("123") && names.Contains(u.FullName) && u.Age > 12).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`first_name`,`last_name`,`birthday`,CONCAT(`first_name`,`last_name`) AS `full_name`,CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) AS `age` FROM `t_user_extra` WHERE CONCAT(`first_name`,`last_name`) LIKE ? AND CONCAT(`first_name`,`last_name`) IN (?,?) AND CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) > ?");
        listen.AssertParams("365(Double),%123%(String),1(String),2(String),365(Double),12(Integer)");
        await Db.Query<UserExtra>().Where(u => u.FullName!.Contains("123") && names.Contains(u.FullName) && u.Age > 12)
            .Select(u => new UserExtraDto { Id = u.Id, FirstName = u.FirstName, LastName = u.LastName, Birthday = u.Birthday, FullName = u.FullName, Age = u.Age })
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`first_name`,t.`last_name`,t.`birthday`,CONCAT(t.`first_name`,t.`last_name`) AS `full_name`,CEILING((timestampdiff(DAY, t.`birthday`, NOW()) / ?)) AS `age` FROM `t_user_extra` t WHERE CONCAT(t.`first_name`,t.`last_name`) LIKE ? AND CONCAT(t.`first_name`,t.`last_name`) IN (?,?) AND CEILING((timestampdiff(DAY, t.`birthday`, NOW()) / ?)) > ?");
        listen.AssertParams("365(Double),%123%(String),1(String),2(String),365(Double),12(Integer)");
        await Db.Query<SchoolClassAggregate>().Where(u => u.Name!.Contains("123"))
            .Select(u => new SchoolClassAggregateDto { Id = u.Id, Name = u.Name, StudentSize = u.StudentSize }).ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t.`id`,t.`name`,(SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) AS `student_size` FROM `school_class` t WHERE t.`name` LIKE ?"));
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("QueryTest15.java#testSimple", Deviation = AgeDivisor)]
    public async Task TestSimple()
    {
        string[] names = ["1", "2"];
        using var listen = SqlCapture.Listen();
        await Db.Query<UserExtra>().Where(u => u.FullName!.Contains("123") && names.Contains(u.FullName) && u.Age > 12).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`first_name`,`last_name`,`birthday`,CONCAT(`first_name`,`last_name`) AS `full_name`,CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) AS `age` FROM `t_user_extra` WHERE CONCAT(`first_name`,`last_name`) LIKE ? AND CONCAT(`first_name`,`last_name`) IN (?,?) AND CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) > ?");
        listen.AssertParams("365(Double),%123%(String),1(String),2(String),365(Double),12(Integer)");
    }

    [Fact, Upstream("QueryTest15.java#test1_1", Deviation = CountAlias)]
    public async Task Test1_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClassAggregate>().Where(u => u.Name!.Contains("123")).ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t.`id`,t.`name`,(SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) AS `student_size` FROM `school_class` t WHERE t.`name` LIKE ?"));
        listen.AssertParams("%123%(String)");
        await Db.Query<SchoolClassAggregateProp>().Select(s => new SchoolClassAggregateProp { Id = s.Id, Name = s.Name, StudentSize = s.StudentSize }).ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t.`id`,t.`name`,(SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) AS `student_size` FROM `school_class` t"));
    }

    // 默认不选出的计算列：整实体查询不带它，显式投影才带
    [Fact, Upstream("QueryTest15.java#testCaseSubAggregate", Deviation = CountAlias)]
    public async Task TestCaseSubAggregate()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClassAggregateProp>().ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`name` FROM `school_class`");
        await Db.Query<SchoolClassAggregateProp>().Select(s => new SchoolClassAggregateProp { Id = s.Id, Name = s.Name }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t");
        await Db.Query<SchoolClassAggregateProp>().Select(s => new SchoolClassAggregateProp { Id = s.Id, Name = s.Name, StudentSize = s.StudentSize }).ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t.`id`,t.`name`,(SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) AS `student_size` FROM `school_class` t"));
    }

    // 投影成 Map 后按键过滤（Java 的 anyColumn("sc")）
    [Fact, Upstream("QueryTest15.java#testSubqueryAnonymous", Deviation = CountAlias)]
    public async Task TestSubqueryAnonymous()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClassAggregateProp>()
            .Select(s => new Dictionary<string, object?> { { "a", s.Id }, { "b", s.Name }, { "sc", s.StudentSize } })
            .Where(m => (int?)m["sc"] > 1).ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t3.`a` AS `a`,t3.`b` AS `b`,t3.`sc` AS `sc` FROM (SELECT t.`id` AS `a`,t.`name` AS `b`,(SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) AS `sc` FROM `school_class` t) t3 WHERE t3.`sc` > ?"));
        listen.AssertParams("1(Integer)");
    }

    // 计算列写入时跳过，查回时算出
    [Fact, Upstream("QueryTest15.java#test2", Deviation = AgeDivisor)]
    public async Task Test2()
    {
        await Db.Delete<UserExtra>().WhereById("test2").ExecuteAsync(Ct);
        try
        {
            await Db.Insert(new UserExtra { Id = "test2", FirstName = "孙", LastName = "悟空", Birthday = new DateTime(2020, 1, 1) }).ExecuteAsync(Ct);
            using var listen = SqlCapture.Listen();
            var list = await Db.Query<UserExtra>().Where(u => u.Id == "test2" && u.FullName!.Contains("悟")).ToListAsync(Ct);
            listen.AssertSql("SELECT `id`,`first_name`,`last_name`,`birthday`,CONCAT(`first_name`,`last_name`) AS `full_name`,CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) AS `age` FROM `t_user_extra` WHERE `id` = ? AND CONCAT(`first_name`,`last_name`) LIKE ?");
            listen.AssertParams("365(Double),test2(String),%悟%(String)");
            var user = Assert.Single(list);
            Assert.Equal("孙悟空", user.FullName);
            Assert.True(user.Age > 4);
        }
        finally
        {
            await Db.Delete<UserExtra>().WhereById("test2").ExecuteAsync(Ct);
        }
    }

    [Fact, Upstream("QueryTest15.java#test3", Deviation = AgeDivisor)]
    public async Task Test3()
    {
        await Db.Delete<UserExtra>().WhereById("test3").ExecuteAsync(Ct);
        try
        {
            await Db.Insert(new UserExtra { Id = "test3", FirstName = "孙", LastName = "悟空", Birthday = new DateTime(2020, 1, 1) }).ExecuteAsync(Ct);
            using var listen = SqlCapture.Listen();
            var list = await Db.Query<UserExtra>().Where(u => u.Id == "test3" && u.FullName!.Contains("悟"))
                .OrderBy(x => x.FullName).ThenBy(x => x.Age).ThenBy(x => Sql.NullsLast(x.FullName)).ToListAsync(Ct);
            listen.AssertSql("SELECT `id`,`first_name`,`last_name`,`birthday`,CONCAT(`first_name`,`last_name`) AS `full_name`,CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) AS `age` FROM `t_user_extra` WHERE `id` = ? AND CONCAT(`first_name`,`last_name`) LIKE ? ORDER BY CONCAT(`first_name`,`last_name`) ASC,CEILING((timestampdiff(DAY, `birthday`, NOW()) / ?)) ASC,CASE WHEN CONCAT(`first_name`,`last_name`) IS NULL THEN 1 ELSE 0 END ASC,CONCAT(`first_name`,`last_name`) ASC");
            listen.AssertParams("365(Double),test3(String),%悟%(String),365(Double)");
            var user = Assert.Single(list);
            Assert.Equal("孙悟空", user.FullName);
            Assert.True(user.Age > 4);
        }
        finally
        {
            await Db.Delete<UserExtra>().WhereById("test3").ExecuteAsync(Ct);
        }
    }

    // 计算列作分组键与聚合的值
    [Fact, Upstream("QueryTest15.java#test4", Deviation = AgeDivisor)]
    public async Task Test4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<UserExtra>().Where(u => u.Id == "test3" && u.FullName!.Contains("悟"))
            .GroupBy(u => new { u.Age, u.FullName })
            .Select(g => new Draft<int?, string?, string?>(g.Key.Age, g.Key.FullName, g.Max(x => x.FullName))).ToListAsync(Ct);
        listen.AssertSql("SELECT CEILING((timestampdiff(DAY, t.`birthday`, NOW()) / ?)) AS `value1`,CONCAT(t.`first_name`,t.`last_name`) AS `value2`,MAX(CONCAT(t.`first_name`,t.`last_name`)) AS `value3` FROM `t_user_extra` t WHERE t.`id` = ? AND CONCAT(t.`first_name`,t.`last_name`) LIKE ? GROUP BY CEILING((timestampdiff(DAY, t.`birthday`, NOW()) / ?)),CONCAT(t.`first_name`,t.`last_name`)");
        listen.AssertParams("365(Double),test3(String),%悟%(String),365(Double)");
    }

    // 计算列用在 JOIN 的 ON、条件、排序里
    [Fact, Upstream("QueryTest15.java#test5", Deviation = AgeDivisor)]
    public async Task Test5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<UserExtra>().LeftJoin<BlogEntity>((u, b2) => u.FullName == b2.Id).Where((u1, b2) => u1.FullName == "123").OrderBy((u1, b2) => u1.FullName).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`first_name`,t.`last_name`,t.`birthday`,CONCAT(t.`first_name`,t.`last_name`) AS `full_name`,CEILING((timestampdiff(DAY, t.`birthday`, NOW()) / ?)) AS `age` FROM `t_user_extra` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND CONCAT(t.`first_name`,t.`last_name`) = t1.`id` WHERE CONCAT(t.`first_name`,t.`last_name`) = ? ORDER BY CONCAT(t.`first_name`,t.`last_name`) ASC");
        listen.AssertParams("365(Double),false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest15.java#test6")]
    public async Task Test6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().LeftJoin<UserExtra>((u, b2) => u.Id == b2.FullName).Where((u1, b2) => b2.FullName == "123").OrderBy((u1, b2) => b2.FullName).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `t_user_extra` t1 ON t.`id` = CONCAT(t1.`first_name`,t1.`last_name`) WHERE t.`deleted` = ? AND CONCAT(t1.`first_name`,t1.`last_name`) = ? ORDER BY CONCAT(t1.`first_name`,t1.`last_name`) ASC");
        listen.AssertParams("false(Boolean),123(String)");
    }

    // 按属性名取列（Java 的 Filter.eq(table, "createTime", 值)）
    [Fact, Upstream("QueryTest15.java#test7")]
    public async Task Test7()
    {
        var of = new DateTime(2020, 1, 2, 3, 4, 0);
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => Sql.Column<DateTime?>(t, "createTime") == of).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `create_time` = ?");
        listen.AssertParams("2020-01-02T03:04(LocalDateTime)");
    }

    [Fact, Upstream("QueryTest15.java#testColumn1")]
    public async Task TestColumn1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Select(t => t.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`create_time` FROM `t_topic` t");
    }

    [Fact, Upstream("QueryTest15.java#testColumn2")]
    public async Task TestColumn2()
    {
        using var listen = SqlCapture.Listen();
        Assert.NotNull(await Db.Query<Topic>().Select(t => t.Stars).FirstOrDefaultAsync(Ct));
        listen.AssertSql("SELECT t.`stars` FROM `t_topic` t LIMIT 1");
    }

    // Java 用例没有断言：整实体投影成 Map 后分页
    [Fact, Upstream("QueryTest15.java#aaa"), Upstream("QueryTest15.java#aaa1")]
    public async Task Aaa()
    {
        await Db.Query<Topic>().Where(t => t.Id == "1").Select(t => new Dictionary<string, object?>(Sql.Columns(t))).ToPageAsync(1, 20, Ct);
    }

    [Fact, Upstream("QueryTest15.java#testCaseWhenLazy")]
    public async Task TestCaseWhenLazy()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Select(t => new Draft<object>(t.Title == "123" ? "1" : "2")).ToListAsync(Ct);
        listen.AssertSql("SELECT (CASE WHEN t.`title` = ? THEN ? ELSE ? END) AS `value1` FROM `t_topic` t");
        listen.AssertParams("123(String),1(String),2(String)");
    }

    [Fact, Upstream("QueryTest15.java#testCaseWhenLazy2")]
    public async Task TestCaseWhenLazy2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Select(t => new Topic { Title = t.Title == "123" ? "1" : "2" }).ToListAsync(Ct);
        listen.AssertSql("SELECT (CASE WHEN t.`title` = ? THEN ? ELSE ? END) AS `title` FROM `t_topic` t");
        listen.AssertParams("123(String),1(String),2(String)");
    }

    [Fact, Upstream("QueryTest15.java#testOrderCase")]
    public async Task TestOrderCase()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.Id == "123").OrderBy(t => t.Stars >= 1 ? 2 : t.Stars <= 2 ? 3 : 4).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? ORDER BY (CASE WHEN `stars` >= ? THEN ? WHEN `stars` <= ? THEN ? ELSE ? END) ASC");
        listen.AssertParams("123(String),1(Integer),2(Integer),2(Integer),3(Integer),4(Integer)");
    }

    // Java 用例没有断言：字符串常量转时间再加一天
    [Fact, Upstream("QueryTest15.java#tes123txxx")]
    public async Task Tes123txxx()
    {
        var format = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        await Db.Query<Topic>().Where(t => t.CreateTime < DateTime.Parse(format, System.Globalization.CultureInfo.InvariantCulture).AddDays(1)).ToListAsync(Ct);
    }

    // 各库的中文日期格式（Java 的 Kingbase 是 PG 模式，C# 用 PG 方言比对）
    // 写法与 Java 不同、SQL 一致：Java 用人大金仓（PG 模式）方言；C# 用 PostgreSQL 方言比对，SQL 相同
    [Fact, Upstream("QueryTest15.java#test122345")]
    public void Test122345()
    {
        Assert.Equal("SELECT \"id\",\"stars\",\"title\",\"create_time\" FROM \"t_topic\" WHERE TO_CHAR((\"create_time\")::TIMESTAMP,'YYYY年MM月DD日') = ?", TestKit.SqlOnly.Db("pgsql").Query<Topic>().Where(m => m.CreateTime!.Value.ToString("yyyy年MM月dd日") == "2022年01月01日").ToSql());
    }

    [Fact, Upstream("QueryTest15.java#test1223456")]
    public void Test1223456()
    {
        Assert.Equal("SELECT \"id\",\"stars\",\"title\",\"create_time\" FROM \"t_topic\" WHERE strftime('%Y年%m月%d日', \"create_time\") = ?", TestKit.SqlOnly.Db("sqlite").Query<Topic>().Where(m => m.CreateTime!.Value.ToString("yyyy年MM月dd日") == "2022年01月01日").ToSql());
    }

    [Fact, Upstream("QueryTest15.java#test123")]
    public async Task Test123()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(m => m.CreateTime!.Value.ToString("yyyy年MM月dd日") == "2022年01月01日").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE DATE_FORMAT(`create_time`,'%Y年%m月%d日') = ?");
        listen.AssertParams("2022年01月01日(String)");
    }

    // 当前时间加减几个月后格式化：参数是月数
    [Fact, Upstream("QueryTest15.java#testColumn")]
    public async Task TestColumn()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(t => t.Id == "6261" && string.Compare(t.CreateTime!.Value.ToString("yyyy-MM"), Sql.Now().AddMonths(-3).ToString("yyyy-MM")) >= 0
                        && string.Compare(t.CreateTime!.Value.ToString("yyyy-MM"), Sql.Now().AddMonths(-1).ToString("yyyy-MM")) <= 0)
            .Select(t => t.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`create_time` FROM `t_topic` t WHERE t.`id` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m') >= DATE_FORMAT(date_add(NOW(), interval (?) month),'%Y-%m') AND DATE_FORMAT(t.`create_time`,'%Y-%m') <= DATE_FORMAT(date_add(NOW(), interval (?) month),'%Y-%m')");
        listen.AssertParams("6261(String),-3(Long),-1(Long)");
    }

    [Fact, Upstream("QueryTest15.java#testFETCHER")]
    public async Task TestFETCHER()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(t => t.Id == "6261" && string.Compare(t.CreateTime!.Value.ToString("yyyy-MM"), Sql.Now().AddMonths(-3).ToString("yyyy-MM")) >= 0
                        && string.Compare(t.CreateTime!.Value.ToString("yyyy-MM"), Sql.Now().AddMonths(-1).ToString("yyyy-MM")) <= 0)
            .Select(t => new Topic { Id = t.Id, CreateTime = t.CreateTime, Title = t.Title }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`title` FROM `t_topic` t WHERE t.`id` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m') >= DATE_FORMAT(date_add(NOW(), interval (?) month),'%Y-%m') AND DATE_FORMAT(t.`create_time`,'%Y-%m') <= DATE_FORMAT(date_add(NOW(), interval (?) month),'%Y-%m')");
        listen.AssertParams("6261(String),-3(Long),-1(Long)");
    }

    [Fact, Upstream("QueryTest15.java#testa")]
    public async Task Testa()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Where(t => t.Id == "6261" && string.Compare(t.CreateTime!.Value.ToString("yyyy-MM"), Sql.Now().AddMonths(-3).ToString("yyyy-MM")) >= 0
                        && string.Compare(t.CreateTime!.Value.ToString("yyyy-MM"), Sql.Now().AddMonths(-1).ToString("yyyy-MM")) <= 0)
            .GroupBy(t => t.CreateTime!.Value.ToString("yyyy-MM"))
            .Select(g => new Draft<string, long>(g.Key, g.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT DATE_FORMAT(t.`create_time`,'%Y-%m') AS `value1`,COUNT(*) AS `value2` FROM `t_topic` t WHERE t.`id` = ? AND DATE_FORMAT(t.`create_time`,'%Y-%m') >= DATE_FORMAT(date_add(NOW(), interval (?) month),'%Y-%m') AND DATE_FORMAT(t.`create_time`,'%Y-%m') <= DATE_FORMAT(date_add(NOW(), interval (?) month),'%Y-%m') GROUP BY DATE_FORMAT(t.`create_time`,'%Y-%m')");
        listen.AssertParams("6261(String),-3(Long),-1(Long)");
    }

    [Fact, Upstream("QueryTest15.java#test12234", NotApplicable = "Java 自建 Hikari 数据源与客户端配置，没有断言；C# 的连接与配置由 EasyDbOptions 覆盖")]
    public void Test12234()
    {
    }

    // ---------- 无实体的表（Java 的 Map 查询）----------

    [Fact, Upstream("QueryTest15.java#test1223")]
    public void Test1223() =>
        Assert.Equal("SELECT * FROM `123` WHERE `name` = ?", Db.Table("123").Where(x => x.Get<string>("name") == "123").ToSql());

    // Java 的 join(INNER_JOIN, ..).asTable("table2")：InnerJoin<DynamicRow>(..).AsTable(..)，不投影时 SELECT *
    [Fact, Upstream("QueryTest15.java#testxxx")]
    public async Task Testxxx()
    {
        using var listen = SqlCapture.Listen();
        await Assert.ThrowsAsync<EasyQueryException>(() =>
            Db.Table("table1").InnerJoin<DynamicRow>((t1, t2) => t1.Get<string>("id1") == t2.Get<string>("id2")).AsTable("table2").ToListAsync(Ct));
        listen.AssertSql("SELECT * FROM `table1` t INNER JOIN `table2` t1 ON t.`id1` = t1.`id2`");
    }

    // Java 用例没有断言：两个无实体查询各自选列后再 JOIN（派生表套派生表），投影里带 SUM(CASE WHEN ..)；再跑两条实体查询
    [Fact, Upstream("QueryTest15.java#test8")]
    public async Task Test8()
    {
        var language = Db.Table("language").SelectColumns("language_id", "name");
        var query1 = Db.Table("film").LeftJoin(language, (f, l) => f.Get<object>("language_id") == l.Get<object>("language_id"))
            .Select((f, l) => new DynamicRow { { "film_id", f.Get<object>("film_id") }, { "title", f.Get<object>("title") }, { "name", l.Get<object>("name") } });
        var query2 = Db.Table("film").LeftJoin(language, (f, l) => f.Get<object>("film_id") == l.Get<object>("film_id"))
            .Select((f, _) => new DynamicRow
            {
                { "film_id", f.Get<object>("film_id") }, { "text", f.Get<object>("text") },
                { "sum", Sql.Sum(Sql.Raw<int?>("(CASE WHEN {0} = '1' THEN 1 ELSE NULL END)", f.Get<object>("id"))) },
            });
        Assert.NotEmpty(query1.LeftJoin(query2, (a, b) => a.Get<object>("film_id") == b.Get<object>("film_id")).ToSql());
        await Db.Query<Topic>().Where(t => Sql.Raw<bool>("{0} in ({1})", t.Id, new[] { "1", "2" })).ToListAsync(Ct);
        await Db.Query<Topic>().Select(t => Sql.Sum(t.Stars)).ToListAsync(Ct);
    }

    // Java 用例没有断言：日期格式、OR 分组、nullOrDefault、原生条件混在一起；投影排除两列后把 id 当标题，或用原生片段赋值
    [Fact, Upstream("QueryTest15.java#testxxx1")]
    public async Task Testxxx1()
    {
        await Db.Query<Topic>()
            .Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01"
                        && (o.Stars != 1 || o.CreateTime <= new DateTime(2024, 1, 1, 1, 1, 0) || !o.Title!.Contains("abc"))
                        && o.CreateTime.Value.ToString("yyyy/MM/dd") == "2023/01/01"
                        && (o.Id ?? "yyyy/MM/dd") == "xxx"
                        && Sql.Raw<bool>("{0} != {1}", o.Stars, o.CreateTime)
                        && (o.CreateTime.Value.ToString("yyyy/MM/dd") == "2023/01/01" || (o.Id ?? "yyyy/MM/dd") == "xxx" || Sql.Raw<bool>("{0} != {1}", o.Stars, o.CreateTime))
                        && o.CreateTime.Value.ToString("yyyy/MM/dd") == "2023/01/02"
                        && (o.Id ?? "yyyy/MM/dd2") == "xxx1")
            .Select(o => new Topic { Stars = o.Stars, CreateTime = o.CreateTime, Title = o.Id, Id = o.Id })
            .ToListAsync(Ct);
        await Db.Query<Topic>().Where(o => o.CreateTime!.Value.ToString("yyyy/MM/dd") == "2023/01/01")
            .Select(o => new Topic { Title = (o.Stars ?? 0).ToString(), Id = Sql.Raw<string>("IFNULL({0},'')", o.Id) })
            .ToListAsync(Ct);
    }
}
