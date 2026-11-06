using Company = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.Company;
using SysUser = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.SysUser;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest16.java（blogtest 实体上的对一 / 集合导航条件、投影里的隐式子查询、多对多的分组合并、导航别名）。
/// 库里没有 blogtest 的表（Java 用 try/catch 吞掉执行错误），这里只生成语句比对。
/// Java 的 flatElement().x() 在 C# 里写成 Any(e => e.x …)。
/// </summary>
public sealed class QueryTest16(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static void AssertStatement(SqlStatement statement, string sql, string parameters)
    {
        Assert.Equal(sql, statement.Text);
        Assert.Equal(parameters, JavaParams.Format(statement));
    }

    [Fact, Upstream("QueryTest16.java#test14")]
    public void Test14() =>
        AssertStatement(Db.Query<SysUser>().Where(user => user.Name!.Contains("小明") || user.Company!.Name!.Contains("JAVA企业")).ToStatement(),
            "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE (t.`name` LIKE ? OR t1.`name` LIKE ?)", "%小明%(String),%JAVA企业%(String)");

    [Fact, Upstream("QueryTest16.java#test15", Deviation = "Java 的 flatElement 在 C# 里写成 Any")]
    public void Test15() =>
        AssertStatement(Db.Query<SysUser>().Where(user => user.Name!.Contains("小明") || user.Roles!.Any(r => r.Name!.Contains("管理员"))).ToStatement(),
            "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t WHERE (t.`name` LIKE ? OR EXISTS(SELECT 1 FROM `t_role` t1 WHERE EXISTS(SELECT 1 FROM `t_user_role` t2 WHERE t2.`role_id` = t1.`id` AND t2.`user_id` = t.`id` LIMIT 1) AND t1.`name` LIKE ? LIMIT 1))", "%小明%(String),%管理员%(String)");

    [Fact, Upstream("QueryTest16.java#test8")]
    public async Task Test8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().DisableLogicDelete().Where(s => s.Title == "杭州市" || s.Title == "绍兴市").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE (`title` = ? OR `title` = ?)");
        listen.AssertParams("杭州市(String),绍兴市(String)");
    }

    // Java 用例没有断言：排序、投影里的原生片段
    [Fact, Upstream("QueryTest16.java#testxxc")]
    public async Task Testxxc()
    {
        await Db.Query<Topic>().Where(b => b.Id == "123").OrderByRaw("RAND()").ToListAsync(Ct);
        await Db.Query<Topic>().Where(b => b.Id == "123").Select(t => new Draft<double, int>(Sql.Raw<double>("RAND()"), Sql.Raw<int>("IFNULL({0},{1})", t.Stars ?? 1, 2))).ToListAsync(Ct);
    }

    [Fact, Upstream("QueryTest16.java#test1", Deviation = "Java 的 flatElement 在 C# 里写成 Any")]
    public void Test1() =>
        AssertStatement(Db.Query<SysUser>().Where(s => s.Roles!.Any(r => r.Menus!.Any(menu => menu.Route == "/admin"))).ToStatement(), "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_role` t1 WHERE EXISTS(SELECT 1 FROM `t_user_role` t2 WHERE t2.`role_id` = t1.`id` AND t2.`user_id` = t.`id` LIMIT 1) AND EXISTS(SELECT 1 FROM `t_menu` t3 WHERE EXISTS(SELECT 1 FROM `t_role_menu` t4 WHERE t4.`menu_id` = t3.`id` AND t4.`role_id` = t1.`id` LIMIT 1) AND t3.`route` = ? LIMIT 1) LIMIT 1)", "/admin(String)");

    // Java 的 roles().mode(GROUP_JOIN)：GroupJoin(s => s.Roles)
    [Fact, Upstream("QueryTest16.java#test2", Deviation = "Java 的 flatElement 在 C# 里写成 Any")]
    public void Test2() =>
        AssertStatement(Db.Query<SysUser>().GroupJoin(s => s.Roles).Where(s => s.Roles!.Any(r => r.Menus!.Any(menu => menu.Route == "/admin"))).ToStatement(),
            "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_role` t1 INNER JOIN `t_user_role` t2 ON t1.`id` = t2.`role_id` WHERE EXISTS(SELECT 1 FROM `t_menu` t5 WHERE EXISTS(SELECT 1 FROM `t_role_menu` t6 WHERE t6.`menu_id` = t5.`id` AND t6.`role_id` = t1.`id` LIMIT 1) AND t5.`route` = ? LIMIT 1) GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__any2__`,?) = ?", "1(Integer),/admin(String),false(Boolean),true(Boolean)");

    // Java 的 ALL_SUB_QUERY_GROUP_JOIN：GroupJoinAll()，分组子查询里的集合导航同样合并
    [Fact, Upstream("QueryTest16.java#test3", Deviation = "Java 的 flatElement 在 C# 里写成 Any")]
    public void Test3() =>
        AssertStatement(Db.Query<SysUser>().GroupJoinAll().Where(s => s.Roles!.Any(r => r.Menus!.Any(menu => menu.Route == "/admin"))).ToStatement(),
            "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_role` t1 INNER JOIN `t_user_role` t2 ON t1.`id` = t2.`role_id` LEFT JOIN (SELECT t6.`role_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_menu` t5 INNER JOIN `t_role_menu` t6 ON t5.`id` = t6.`menu_id` WHERE t5.`route` = ? GROUP BY t6.`role_id`) t8 ON t8.`__group_key1__` = t1.`id` WHERE IFNULL(t8.`__any2__`,?) = ? GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__any2__`,?) = ?", "1(Integer),1(Integer),/admin(String),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");

    [Fact, Upstream("QueryTest16.java#testDocSub", Deviation = "Java 的 flatElement 在 C# 里写成 Any")]
    public void TestDocSub()
    {
        AssertStatement(Db.Query<Company>().Where(com => com.Users!.Where(u => u.Age > 18).Any()).ToStatement(), "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_company` t WHERE EXISTS(SELECT 1 FROM `t_user` t1 WHERE t1.`company_id` = t.`id` AND t1.`age` > ? LIMIT 1)", "18(Integer)");
        AssertStatement(Db.Query<Company>().Where(com => com.Users!.Any(u => u.Age > 18)).ToStatement(), "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_company` t WHERE EXISTS(SELECT 1 FROM `t_user` t1 WHERE t1.`company_id` = t.`id` AND t1.`age` > ? LIMIT 1)", "18(Integer)");
        AssertStatement(Db.Query<Company>().Where(com => com.Users!.Average(u => (decimal?)u.Age) > 18m).ToStatement(), "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_company` t WHERE IFNULL((SELECT AVG(t1.`age`) FROM `t_user` t1 WHERE t1.`company_id` = t.`id`),0) > ?", "18(BigDecimal)");
    }

    // Java 的 noneValue() / anyValue()：投影里的 !Any() / Any()
    [Fact, Upstream("QueryTest16.java#docSub")]
    public void DocSub()
    {
        var companies = Db.Query<Company>().Where(com => com.Name!.Contains("xx公司"));
        AssertStatement(companies.Select(com => new Draft<string?, bool>(com.Id, !com.Users!.Any())).ToStatement(), "SELECT t.`id` AS `value1`,(NOT (EXISTS(SELECT 1 FROM `t_user` t1 WHERE t1.`company_id` = t.`id` LIMIT 1))) AS `value2` FROM `t_company` t WHERE t.`name` LIKE ?", "%xx公司%(String)");
        AssertStatement(companies.Select(com => new Draft<string?, bool>(com.Id, com.Users!.Any())).ToStatement(), "SELECT t.`id` AS `value1`,(EXISTS(SELECT 1 FROM `t_user` t1 WHERE t1.`company_id` = t.`id` LIMIT 1)) AS `value2` FROM `t_company` t WHERE t.`name` LIKE ?", "%xx公司%(String)");
        AssertStatement(companies.Select(com => new Draft<string?, int?>(com.Id, com.Users!.Where(user => user.Name!.StartsWith("李")).Sum(x => x.Age))).ToStatement(),
            "SELECT t.`id` AS `value1`,IFNULL((SELECT SUM(t1.`age`) FROM `t_user` t1 WHERE t1.`company_id` = t.`id` AND t1.`name` LIKE ?),0) AS `value2` FROM `t_company` t WHERE t.`name` LIKE ?", "李%(String),%xx公司%(String)");
        AssertStatement(companies.Select(com => new Draft<string?, long>(com.Id, com.Users!.LongCount())).ToStatement(), "SELECT t.`id` AS `value1`,(SELECT COUNT(*) FROM `t_user` t1 WHERE t1.`company_id` = t.`id`) AS `value2` FROM `t_company` t WHERE t.`name` LIKE ?", "%xx公司%(String)");
        AssertStatement(companies.Select(com => new Draft<string?, long>(com.Id, com.Users!.LongCount())).ToStatement(), "SELECT t.`id` AS `value1`,(SELECT COUNT(*) FROM `t_user` t1 WHERE t1.`company_id` = t.`id`) AS `value2` FROM `t_company` t WHERE t.`name` LIKE ?", "%xx公司%(String)");
        AssertStatement(companies.Select(com => new Draft<string?, long>(com.Id, com.Users!.Where(user => user.Name!.StartsWith("李")).LongCount())).ToStatement(), "SELECT t.`id` AS `value1`,(SELECT COUNT(*) FROM `t_user` t1 WHERE t1.`company_id` = t.`id` AND t1.`name` LIKE ?) AS `value2` FROM `t_company` t WHERE t.`name` LIKE ?", "李%(String),%xx公司%(String)");
    }

    // Java 的 roles().configure(r -> r.asAlias("myRole"))：Roles.AsAlias("myRole")；rangeClosed 的两个条件都为真
    [Fact, Upstream("QueryTest16.java#test19")]
    public void Test19()
    {
        DateTime? begin = new DateTime(2020, 1, 1, 1, 2, 0);
        DateTime? end = new DateTime(2026, 1, 1, 1, 2, 0);
        AssertStatement(Db.Query<SysUser>().Where(user => user.Roles!.AsAlias("myRole").Any(role => role.Name!.Contains("查询的角色名") && role.CreateTime.RangeClosed(begin, end))).ToStatement(),
            "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_role` myRole WHERE EXISTS(SELECT 1 FROM `t_user_role` t2 WHERE t2.`role_id` = myRole.`id` AND t2.`user_id` = t.`id` LIMIT 1) AND myRole.`name` LIKE ? AND (myRole.`create_time` >= ? AND myRole.`create_time` <= ?) LIMIT 1)", "%查询的角色名%(String),2020-01-01T01:02(LocalDateTime),2026-01-01T01:02(LocalDateTime)");
    }

    // Java 先 limit 再 select(com -> com) 包成派生表（C# 写 Skip / Take 后 AsSubQuery() 再 Select），外层投影里的集合聚合引用派生表的列
    [Fact, Upstream("QueryTest16.java#testSubSum")]
    public void TestSubSum() =>
        AssertStatement(Db.Query<Company>().Where(com => com.Name!.Contains("xx公司")).Skip(10).Take(20).AsSubQuery().Select(com => new Draft<string?, int?>(com.Id, com.Users!.Sum(x => x.Age))).ToStatement(),
            "SELECT t1.`id` AS `value1`,IFNULL((SELECT SUM(t2.`age`) FROM `t_user` t2 WHERE t2.`company_id` = t1.`id`),0) AS `value2` FROM (SELECT t.`id`,t.`name`,t.`create_time` FROM `t_company` t WHERE t.`name` LIKE ? LIMIT 20 OFFSET 10) t1", "%xx公司%(String)");

    // 子查询的表起别名（Java 的 configure(r -> r.asAlias("myRole"))）；时间区间两端都有值时都写上
    [Fact, Upstream("QueryTest16.java#test20")]
    public void Test20()
    {
        var begin = new DateTime(2020, 1, 1, 1, 2, 0);
        var end = new DateTime(2026, 1, 1, 1, 2, 0);
        AssertStatement(Db.Query<SysUser>().Where(user => user.Company!.Name!.Contains("123")
                && user.Roles!.AsAlias("myRole").Any(role => role.Name!.Contains("查询的角色名") && role.CreateTime.RangeClosed(begin, end))).ToStatement(),
            "SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t1.`name` LIKE ? AND EXISTS(SELECT 1 FROM `t_role` myRole WHERE EXISTS(SELECT 1 FROM `t_user_role` t3 WHERE t3.`role_id` = myRole.`id` AND t3.`user_id` = t.`id` LIMIT 1) AND myRole.`name` LIKE ? AND (myRole.`create_time` >= ? AND myRole.`create_time` <= ?) LIMIT 1)",
            "%123%(String),%查询的角色名%(String),2020-01-01T01:02(LocalDateTime),2026-01-01T01:02(LocalDateTime)");
    }

    /// <summary>对应 Java：entity/navf/UVO（只有平铺属性：角色的权限、角色的资源标识、角色的路由）。</summary>
    public sealed class UVO
    {
        [MapPath("Roles.Permission")]
        public List<string?>? Roles { get; set; }

        [MapPath("Roles.Resources.PermissionMark")]
        public List<string?>? Resources { get; set; }

        [MapPath("Roles.Routes")]
        public List<NavfRoute>? Routes { get; set; }
    }

    // 只有平铺属性时主查询只取关联键；表不存在，执行到这一条就报错（与 Java 一致）
    [Fact, Upstream("QueryTest16.java#test111")]
    public async Task Test111()
    {
        using var listen = SqlCapture.Listen();
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<NavfUser>().Select<UVO>().ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id` AS `__relation__id` FROM `sys_user` t WHERE t.`deleted` = ?");
        listen.AssertParams("0(Integer)");
    }

    // Oracle：先按 ROWNUM 取段再在外层算子查询的合计（NVL 兜底）
    [Fact, Upstream("QueryTest16.java#testSubSumOracle")]
    public void TestSubSumOracle() =>
        SqlOnly.AssertStatement(SqlOnly.Db("oracle").Query<Company>().Where(com => com.Name!.Contains("xx公司")).Skip(10).Take(20).AsSubQuery().Select(com => new Draft<string?, int?>(com.Id, com.Users!.Sum(x => x.Age))).ToStatement(),
            "SELECT t1.\"id\" AS \"value1\",NVL((SELECT SUM(t2.\"age\") FROM \"t_user\" t2 WHERE t2.\"company_id\" = t1.\"id\"),0) AS \"value2\" FROM (SELECT rt.* FROM(SELECT t.\"id\",t.\"name\",t.\"create_time\", ROWNUM AS \"__rownum__\" FROM \"t_company\" t WHERE t.\"name\" LIKE ? AND ROWNUM < 31) rt WHERE rt.\"__rownum__\" > 10) t1", "%xx公司%(String)");

    /// <summary>对应 Java：vo/CompanyVO。</summary>
    private sealed class CompanyVO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        public int? UseTotalAge { get; set; }
    }

    // Oracle 上先分页（ROWNUM 两层）再在外层投影集合的求和；总数单独一条 COUNT
    [Fact, Upstream("QueryTest16.java#testSubSumOracle2")]
    public void TestSubSumOracle2()
    {
        var db = SqlOnly.Db("oracle");
        const long pageIndex = 10, pageSize = 20;
        var query = db.Query<Company>().Where(com => com.Name!.Contains("xx公司"));
        AssertStatement(query.ToCountStatement(), "SELECT COUNT(*) FROM \"t_company\" WHERE \"name\" LIKE ?", "%xx公司%(String)");
        AssertStatement(query.Skip((pageIndex - 1) * pageSize).Take(pageSize).AsSubQuery().Select(com => new CompanyVO { Id = com.Id, Name = com.Name, CreateTime = com.CreateTime, UseTotalAge = com.Users!.Sum(u => u.Age) }).ToStatement(),
            "SELECT t1.\"id\",t1.\"name\",t1.\"create_time\",NVL((SELECT SUM(t2.\"age\") FROM \"t_user\" t2 WHERE t2.\"company_id\" = t1.\"id\"),0) AS \"use_total_age\" FROM (SELECT rt.* FROM(SELECT t.\"id\",t.\"name\",t.\"create_time\", ROWNUM AS \"__rownum__\" FROM \"t_company\" t WHERE t.\"name\" LIKE ? AND ROWNUM < 201) rt WHERE rt.\"__rownum__\" > 180) t1",
            "%xx公司%(String)");
    }

    // 无实体的表各自选列后 UNION / UNION ALL，外层包一层派生表
    [Fact, Upstream("QueryTest16.java#test16", Deviation = "Java 的 Map 查询 select 之后每个 UNION 分支多占一个别名编号（t、t2，外层 t4）；C# 按实际出现的表连续编号")]
    public void Test16()
    {
        Query<DynamicRow> Users(string table) => Db.Table(table).SelectColumns("a", "b");
        Assert.Equal("SELECT t2.`a` AS `a`,t2.`b` AS `b` FROM ( (SELECT t.`a` AS `a`,t.`b` AS `b` FROM `t_user` t)  UNION  (SELECT t1.`a` AS `a`,t1.`b` AS `b` FROM `t_user1` t1) ) t2",
            Users("t_user").Union(Users("t_user1")).ToSql());
        Assert.Equal("SELECT t2.`a` AS `a`,t2.`b` AS `b` FROM ( (SELECT t.`a` AS `a`,t.`b` AS `b` FROM `t_user` t)  UNION ALL  (SELECT t1.`a` AS `a`,t1.`b` AS `b` FROM `t_user1` t1) ) t2",
            Users("t_user").UnionAll(Users("t_user1")).ToSql());
    }
}
