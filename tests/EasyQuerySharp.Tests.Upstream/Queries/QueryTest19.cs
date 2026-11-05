using EasyQuerySharp.Filtering;
using BlogTest = EasyQuerySharp.Tests.Upstream.Entities.BlogTest;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest19.java（自定义值过滤、带固定条件的导航带出、多对多 any / none 与分组合并、CTE、取时间值）。
/// Java 的 toCteAs 对应 C# 的 <c>AsCte(name)</c>。
/// </summary>
public sealed class QueryTest19(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 自定义的值过滤让 Topic.title 的条件总是保留：C# 的值过滤是固定几种，把要保留的条件写在 UseValueFilter 之前
    [Fact, Upstream("QueryTest19.java#testMyFilter", Deviation = "Java 可以自定义 ValueFilter（按表和属性放行）；C# 的值过滤只有固定几种，需要保留的条件写在 UseValueFilter 之前")]
    public async Task TestMyFilter()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.Title!.Contains("")).UseValueFilter(ValueFilter.NotNullOrBlank).Where(t => t.Id!.Contains("")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `title` LIKE ?");
        listen.AssertParams("%%(String)");
    }

    /// <summary>对应 Java：entity/relation/MyRelationUserDTO。</summary>
    public sealed class MyRelationUserDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<InternalHistoryBooks>? HistoryBooks { get; set; }

        public sealed class InternalHistoryBooks
        {
            public string? Id { get; set; }

            public string? UserId { get; set; }

            public string? Name { get; set; }

            public int? BookType { get; set; }

            public DateTime? CreateTime { get; set; }
        }
    }

    /// <summary>对应 Java：entity/relation/MyRelationUserDTO1。</summary>
    public sealed class MyRelationUserDTO1
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<InternalTeachers>? Teachers { get; set; }

        public sealed class InternalTeachers
        {
            public string? Id { get; set; }

            public string? Name { get; set; }
        }
    }

    // Java 的 selectAutoInclude 按 DTO 结构带出；C# 写出嵌套投影。导航的固定条件与键条件合成一组；relation_* 表不存在，执行到带出时失败
    [Fact, Upstream("QueryTest19.java#testDoc99")]
    public async Task TestDoc99()
    {
        using var listen = SqlCapture.Listen();
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<MyRelationUser>().Select(u => new MyRelationUserDTO
        {
            Id = u.Id,
            Name = u.Name,
            HistoryBooks = u.HistoryBooks!.Select(b => new MyRelationUserDTO.InternalHistoryBooks { Id = b.Id, UserId = b.UserId, Name = b.Name, BookType = b.BookType, CreateTime = b.CreateTime }).ToList(),
        }).ToListAsync(Ct));
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`name` FROM `t_user` t");
        listen.AssertSql(1, "SELECT t.`id`,t.`user_id`,t.`name`,t.`book_type`,t.`create_time` FROM `relation_book` t WHERE (t.`user_id` IN (?) AND t.`create_time` <= ?)");
        listen.AssertParams(1, "1(String),2022-01-01T00:00(LocalDateTime)");
    }

    // 多对多：先按中间表的固定条件取 (用户, 老师) 对，再按老师的固定条件取老师
    [Fact, Upstream("QueryTest19.java#testDoc100")]
    public async Task TestDoc100()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MyRelationUser>().Select(u => new MyRelationUserDTO1
        {
            Id = u.Id,
            Name = u.Name,
            Teachers = u.Teachers!.Select(t => new MyRelationUserDTO1.InternalTeachers { Id = t.Id, Name = t.Name }).ToList(),
        }).ToListAsync(Ct).ContinueWith(_ => { }, Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`name` FROM `t_user` t");
        listen.AssertSql(1, "SELECT `first_id`,`second_id` FROM `relation_route` WHERE `first_id` IN (?) AND `type` = ?");
        listen.AssertParams(1, "1(String),1(Integer)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name` FROM `relation_teacher` t WHERE (t.`id` IN (?) AND t.`name` = ?)");
        listen.AssertParams(2, "2(String),12345(String)");
    }

    private const string UserSelect = "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t";

    // 值过滤去掉了用到导航的条件：不再 JOIN 地址表，主表仍带别名 t（与 Java 一致）
    [Fact, Upstream("QueryTest19.java#test1")]
    public void Test1()
    {
        var query = Db.Query<BlogTest.SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(u => u.Name == "小明" && u.Address!.City == "");
        Assert.Equal(UserSelect + " WHERE t.`name` = ?", query.ToSql());
    }


    private static string Roles(int role, int link) =>
        $"EXISTS(SELECT 1 FROM `t_role` t{role} WHERE EXISTS(SELECT 1 FROM `t_user_role` t{link} WHERE t{link}.`role_id` = t{role}.`id` AND t{link}.`user_id` = t.`id` LIMIT 1) AND t{role}.`name` LIKE ? LIMIT 1)";

    [Fact, Upstream("QueryTest19.java#test1_1")]
    public void Test1_1() =>
        Assert.Equal(UserSelect + " WHERE t.`name` = ? AND " + Roles(1, 2),
            Db.Query<BlogTest.SysUser>().Where(u => u.Name == "小明" && u.Roles!.Any(role => role.Name!.Contains("管理员"))).ToSql());

    [Fact, Upstream("QueryTest19.java#test1_2")]
    public void Test1_2() =>
        Assert.Equal(UserSelect + " WHERE t.`name` = ? AND NOT (" + Roles(1, 2) + ") AND " + Roles(3, 4),
            Db.Query<BlogTest.SysUser>().Where(u => u.Name == "小明" && !u.Roles!.Any(role => role.Name!.Contains("管理员")) && u.Roles!.Any(role => role.Name!.Contains("普通员工"))).ToSql());

    // 多对多的分组合并：目标表 INNER JOIN 中间表后按中间表的外键分组
    [Fact, Upstream("QueryTest19.java#test1_3")]
    public async Task Test1_3()
    {
        using var listen = SqlCapture.Listen();
        var query = Db.Query<BlogTest.SysUser>().GroupJoin(u => u.Roles)
            .Where(u => u.Name == "小明" && !u.Roles!.Any(role => role.Name!.Contains("管理员")) && u.Roles!.Any(role => role.Name!.Contains("普通员工")));
        await Assert.ThrowsAnyAsync<Exception>(() => query.ToListAsync(Ct));
        listen.AssertSql(UserSelect + " LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,(COUNT((CASE WHEN t1.`name` LIKE ? THEN ? ELSE NULL END)) <= 0) AS `__none2__`,(COUNT((CASE WHEN t1.`name` LIKE ? THEN ? ELSE NULL END)) > 0) AS `__any3__` FROM `t_role` t1 INNER JOIN `t_user_role` t2 ON t1.`id` = t2.`role_id` GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` = ? AND IFNULL(t4.`__none2__`,?) = ? AND IFNULL(t4.`__any3__`,?) = ?");
        listen.AssertParams("%管理员%(String),1(Integer),%普通员工%(String),1(Integer),小明(String),true(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    // 对一导航的条件：隐式 LEFT JOIN 地址表
    [Fact, Upstream("QueryTest19.java#test2")]
    public async Task Test2()
    {
        Assert.NotEmpty(await Db.Query<Topic>().Select(t => Sql.Now()).ToListAsync(Ct));
        Assert.Equal(UserSelect + " LEFT JOIN `t_user_address` t1 ON t1.`user_id` = t.`id` WHERE t.`name` = ? AND t1.`city` = ?",
            Db.Query<BlogTest.SysUser>().Where(u => u.Name == "小明" && u.Address!.City == "").ToSql());
    }

    [Fact, Upstream("QueryTest19.java#testTime")]
    public async Task TestTime()
    {
        using var listen = SqlCapture.Listen();
        var time = await Db.Query<Topic>().Where(t => t.Id == "1").Select(t => t.CreateTime).SingleAsync(Ct);
        // Java 断言种子数据的 2023-05-25 10:48:05；别的用例会改这一行，这里与整行读出的值比较
        Assert.Equal((await Db.Query<Topic>().Where(t => t.Id == "1").SingleAsync(Ct)).CreateTime, time);
        listen.AssertSql(0, "SELECT t.`create_time` FROM `t_topic` t WHERE t.`id` = ?");
        listen.AssertParams(0, "1(String)");
    }

    // 投影常量：参数化后原样读回
    [Fact, Upstream("QueryTest19.java#testTime1")]
    public async Task TestTime1()
    {
        using var listen = SqlCapture.Listen();
        var expected = new DateTime(2023, 5, 25, 10, 48, 6);
        var time = await Db.Query<Topic>().Where(t => t.Id == "1").Select(t => expected).SingleAsync(Ct);
        Assert.Equal(expected, time);
        listen.AssertSql("SELECT ? FROM `t_topic` t WHERE t.`id` = ?");
        listen.AssertParams("2023-05-25T10:48:06(LocalDateTime),1(String)");
    }

    // Java 只跑不断言：投影里的分组子查询
    [Fact, Upstream("QueryTest19.java#testaaa")]
    public async Task Testaaa()
    {
        var now = DateTime.Now;
        await Db.Query<Topic>().Select(t => new Draft<string?, string>(t.Id, Sql.SubQuery(Db.Query<BlogEntity>()
            .Where(b => b.CreateTime < now && b.Id == t.Id).GroupBy(b => b.Title).Select(g => g.StringJoin(e => e.Content, "->"))))).ToListAsync(Ct);
    }

    // Java 的 maxOrDefault(o -> o.id().toNumber(Long.class), 0L)
    [Fact, Upstream("QueryTest19.java#testNow", Deviation = "前半段无实体的 Map 查询取 NOW() 未移植；只比较两种取最大值的写法")]
    public async Task TestNow()
    {
        var max = await Db.Query<Topic>().MaxAsync(o => (long?)Sql.Cast<long>(o.Id), Ct) ?? 0L;
        var max1 = await Db.Query<Topic>().Select(t => (long?)Sql.Max(Sql.Cast<long>(t.Id))).SingleOrDefaultAsync(Ct) ?? 0L;
        Assert.Equal(max, max1);
    }


    private static string With(string name) => $"WITH `{name}` AS (SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time` FROM `t_topic` t1 WHERE t1.`id` = ?) ";

    [Fact, Upstream("QueryTest19.java#testToWithAs")]
    public async Task TestToWithAs()
    {
        using var listen = SqlCapture.Listen();
        var cte = Db.Query<Topic>().Where(t => t.Id == "456").AsCte("withTableTopic");
        await Db.Query<Topic>().LeftJoin(cte, (t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id == "123").Select((t, t1) => t).ToListAsync(Ct);
        listen.AssertSql(With("withTableTopic") + "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `withTableTopic` t2 ON t.`id` = t2.`id` WHERE t.`id` = ?");
        listen.AssertParams("456(String),123(String)");
    }

    // 同一个 CTE 引用两次：定义只写一次；Java 两次引用都编号为 t2（同一个表对象），C# 依次编号
    [Fact, Upstream("QueryTest19.java#testToWithAs2", Deviation = "Java 两次引用同一个 CTE 时别名都是 t2（SQL 有歧义）；C# 第二次引用编号为 t3"),
     Upstream("QueryTest19.java#testToWithAs3", Deviation = "同 testToWithAs2")]
    public async Task TestToWithAs2()
    {
        using var listen = SqlCapture.Listen();
        var cte = Db.Query<Topic>().Where(t => t.Id == "456").AsCte();
        await Db.Query<Topic>().LeftJoin(cte, (t, t1) => t.Id == t1.Id).LeftJoin(cte, (t, t1, t2) => t.Id == t2.Id)
            .Where((t, t1, t2) => t.Id == "123" && t2.Id == "t2123").Select((t, t1, t2) => t).ToListAsync(Ct);
        listen.AssertSql(With("with_Topic") + "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `with_Topic` t2 ON t.`id` = t2.`id` LEFT JOIN `with_Topic` t3 ON t.`id` = t3.`id` WHERE t.`id` = ? AND t3.`id` = ?");
        listen.AssertParams("456(String),123(String),t2123(String)");
    }

    [Fact, Upstream("QueryTest19.java#testToWithA43", Deviation = "同 testToWithAs2")]
    public async Task TestToWithA43()
    {
        using var listen = SqlCapture.Listen();
        var cte = Db.Query<Topic>().Where(t => t.Id == "456").AsCte();
        await Db.Query<Topic>().InnerJoin(cte, (t, t1) => t.Id == t1.Id).InnerJoin(cte, (t, t1, t2) => t.Id == t2.Id)
            .Where((t, t1, t2) => t.Id == "123" && t2.Id == "t2123").Select((t, t1, t2) => t).ToListAsync(Ct);
        listen.AssertSql(With("with_Topic") + "SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t INNER JOIN `with_Topic` t2 ON t.`id` = t2.`id` INNER JOIN `with_Topic` t3 ON t.`id` = t3.`id` WHERE t.`id` = ? AND t3.`id` = ?");
        listen.AssertParams("456(String),123(String),t2123(String)");
    }

    // Java 只跑不断言：字符串拼接后比较大小
    [Fact, Upstream("QueryTest19.java#testConcat")]
    public async Task TestConcat() =>
        await Db.Query<BlogEntity>().Where(b => string.Compare(b.UpdateTime + "," + b.Id, b.UpdateTime + "," + b.Id) >= 0).ToListAsync(Ct);
}
