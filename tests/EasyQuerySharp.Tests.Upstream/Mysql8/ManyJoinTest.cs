namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/ManyJoinTest.java（集合导航取第几个元素、多对多的分组合并与取元素、嵌套分组合并、几个聚合共用一张分组表）。
/// Java 的 element(n) 对应 C# 的 ElementAt(n)、firstElement() / first() 对应 First()，
/// flatElement() 是"任一元素满足"，写成 Any；ALL_SUB_QUERY_GROUP_JOIN 对应 GroupJoinAll()。
/// </summary>
public sealed class ManyJoinTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private const string FlatElement = "Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同";

    // Java 用例没有断言，只验证能执行
    [Fact, Upstream("mysql8/ManyJoinTest.java#manyGroupTest1")]
    public async Task ManyGroupTest1()
    {
        await Db.Query<DocUser>().Where(u => u.BankCards!.Any()).ToListAsync(Ct);
    }

    // 两种取法（带条件的第 1 张、不带条件的第 3 张）各 JOIN 一次，同一取法上的两个条件共用一张
    [Fact, Upstream("mysql8/ManyJoinTest.java#testElement4")]
    public async Task TestElement4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>()
            .Where(u => u.BankCards!.Where(o => o.Type == "123").ElementAt(0).BankId == "123"
                        && u.BankCards!.Where(o => o.Type == "123").ElementAt(0).Type == "123"
                        && u.BankCards!.ElementAt(2).Type == "123")
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age` FROM `doc_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`id` ASC)) AS `__row__` FROM `doc_bank_card` t1 WHERE t1.`type` = ?) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) LEFT JOIN (SELECT t4.`id`,t4.`uid`,t4.`code`,t4.`type`,t4.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t4.`uid` ORDER BY t4.`id` ASC)) AS `__row__` FROM `doc_bank_card` t4) t6 ON (t6.`uid` = t.`id` AND t6.`__row__` = ?) WHERE t3.`bank_id` = ? AND t3.`type` = ? AND t6.`type` = ?");
        listen.AssertParams("123(String),1(Integer),3(Integer),123(String),123(String),123(String)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#testElement5")]
    public async Task TestElement5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>().Where(u => u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Type == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age` FROM `doc_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY IFNULL(t1.`code`,?) DESC)) AS `__row__` FROM `doc_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t3.`type` = ?");
        listen.AssertParams("x(String),3(Integer),123(String)");
    }

    // 条件与排序里同样的取法共用一张
    [Fact, Upstream("mysql8/ManyJoinTest.java#testElement6")]
    public async Task TestElement6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>()
            .Where(u => u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Type == "123")
            .OrderBy(u => u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Type)
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age` FROM `doc_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY IFNULL(t1.`code`,?) DESC)) AS `__row__` FROM `doc_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t3.`type` = ? ORDER BY t3.`type` ASC");
        listen.AssertParams("x(String),3(Integer),123(String)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#testElement7")]
    public async Task TestElement7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>()
            .Where(u => u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Type == "123")
            .OrderBy(u => u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Type)
            .Select(u => new Draft<string?, string?>(
                u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Type,
                u.BankCards!.OrderByDescending(x => x.Code ?? "x").ElementAt(2).Code))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t3.`type` AS `value1`,t3.`code` AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY IFNULL(t1.`code`,?) DESC)) AS `__row__` FROM `doc_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t3.`type` = ? ORDER BY t3.`type` ASC");
        listen.AssertParams("x(String),3(Integer),123(String)");
    }

    // 声明了分组合并：聚合进分组表，取第几个元素仍是单独的编号派生表
    [Fact, Upstream("mysql8/ManyJoinTest.java#testElement3")]
    public async Task TestElement3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>().GroupJoin(u => u.BankCards)
            .Select(u => new Draft<string?, string?>(
                u.BankCards!.Where(o => o.Type == "123").Max(x => x.Code),
                u.BankCards!.Where(o => o.Type == "123").First().Type))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`__max2__` AS `value1`,t5.`type` AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX(t1.`code`) AS `__max2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`id`,t3.`uid`,t3.`code`,t3.`type`,t3.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t3.`uid` ORDER BY t3.`id` ASC)) AS `__row__` FROM `doc_bank_card` t3 WHERE t3.`type` = ?) t5 ON (t5.`uid` = t.`id` AND t5.`__row__` = ?)");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    // 先取第 4~6 张再求最大值（Java 的 elements(3, 5)）
    [Fact, Upstream("mysql8/ManyJoinTest.java#testElement8")]
    public async Task TestElement8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>().GroupJoin(u => u.BankCards)
            .Select(u => new Draft<string?, string?>(
                u.BankCards!.Where(o => o.Type == "123").Skip(3).Take(3).Max(x => x.Code),
                u.BankCards!.Where(o => o.Type == "123").First().Type))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t4.`__max2__` AS `value1`,t7.`type` AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,MAX(t3.`code`) AS `__max2__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`id` ASC)) AS `__row__` FROM `doc_bank_card` t1 WHERE t1.`type` = ?) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` LEFT JOIN (SELECT t5.`id`,t5.`uid`,t5.`code`,t5.`type`,t5.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t5.`uid` ORDER BY t5.`id` ASC)) AS `__row__` FROM `doc_bank_card` t5 WHERE t5.`type` = ?) t7 ON (t7.`uid` = t.`id` AND t7.`__row__` = ?)");
        listen.AssertParams("123(String),4(Long),6(Long),123(String),1(Integer)");
    }

    // 多对多取第一个：经中间表编号，分区列以 __列名__ 选出
    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many2")]
    public async Task ManyJoinMany2Many2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().GroupJoin(s => s.SchoolTeachers)
            .Where(s => s.SchoolTeachers!.Where(x => x.Name!.Contains("小明")).OrderBy(x => x.Name).First().Name!.Contains("123123"))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t LEFT JOIN (SELECT t2.`class_id` AS `__class_id__`,t1.`id`,t1.`name`,(ROW_NUMBER() OVER (PARTITION BY t2.`class_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `school_teacher` t1 INNER JOIN `school_class_teacher` t2 ON t1.`id` = t2.`teacher_id` WHERE t1.`name` LIKE ?) t5 ON (t5.`__class_id__` = t.`id` AND t5.`__row__` = ?) WHERE t5.`name` LIKE ?");
        listen.AssertParams("%小明%(String),1(Integer),%123123%(String)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many3")]
    public async Task ManyJoinMany2Many3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoin(u => u.Roles).Where(s => s.Roles!.Where(x => x.Name!.Contains("小明角色")).Count() == 123L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`name` LIKE ? GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__count2__`,0) = ?");
        listen.AssertParams("1(Integer),%小明角色%(String),123(Long)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many3_1")]
    public async Task ManyJoinMany2Many3_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User2>().GroupJoin(u => u.Roles).Where(s => s.Roles!.Where(x => x.RoleName!.Contains("小明角色")).Count() == 123L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`user_id`,t.`user_name`,t.`user_age`,t.`create_time` FROM `m8_user2` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `m8_role2` t1 INNER JOIN `m8_user_role2` t2 ON t1.`role_id` = t2.`role_id` WHERE t1.`role_name` LIKE ? GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`user_id` WHERE IFNULL(t4.`__count2__`,0) = ?");
        listen.AssertParams("1(Integer),%小明角色%(String),123(Long)");
    }

    // Java 的 toNumber(Long.class) 对应 Sql.Cast<long>
    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many3_2")]
    public async Task ManyJoinMany2Many3_2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User2>().GroupJoin(u => u.Roles)
            .Where(s => s.Roles!.Where(x => x.RoleName!.Contains("小明角色")).Sum(x => Sql.Cast<long>(x.RoleName)) == 123L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`user_id`,t.`user_name`,t.`user_age`,t.`create_time` FROM `m8_user2` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,SUM(CAST(t1.`role_name` AS SIGNED)) AS `__sum2__` FROM `m8_role2` t1 INNER JOIN `m8_user_role2` t2 ON t1.`role_id` = t2.`role_id` WHERE t1.`role_name` LIKE ? GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`user_id` WHERE IFNULL(t4.`__sum2__`,0) = ?");
        listen.AssertParams("%小明角色%(String),123(Long)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many4")]
    public async Task ManyJoinMany2Many4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoin(u => u.Roles)
            .Where(s => s.Roles!.Where(x => x.Name!.Contains("小明角色")).OrderBy(x => x.Name).First().Name == "123123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__user_id__`,t1.`id`,t1.`name`,t1.`create_time`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`name` LIKE ?) t5 ON (t5.`__user_id__` = t.`id` AND t5.`__row__` = ?) WHERE t5.`name` = ?");
        listen.AssertParams("%小明角色%(String),1(Integer),123123(String)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many4_1")]
    public async Task ManyJoinMany2Many4_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User2>().GroupJoin(u => u.Roles)
            .Where(s => s.Roles!.Where(x => x.RoleName!.Contains("小明角色")).OrderBy(x => x.RoleName).First().RoleName == "123123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`user_id`,t.`user_name`,t.`user_age`,t.`create_time` FROM `m8_user2` t LEFT JOIN (SELECT t2.`user_id` AS `__user_id__`,t1.`role_id`,t1.`role_name`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`role_name` ASC)) AS `__row__` FROM `m8_role2` t1 INNER JOIN `m8_user_role2` t2 ON t1.`role_id` = t2.`role_id` WHERE t1.`role_name` LIKE ?) t5 ON (t5.`__user_id__` = t.`user_id` AND t5.`__row__` = ?) WHERE t5.`role_name` = ?");
        listen.AssertParams("%小明角色%(String),1(Integer),123123(String)");
    }

    // 组内排序 NULL 排最后（Java 的 desc(NULLS_LAST)）
    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many9")]
    public async Task ManyJoinMany2Many9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User2>()
            .Where(s => s.Books!.Where(x => x.BookPrice > 100m).OrderByDescending(x => Sql.NullsLast(x.BookPrice)).First().BookPrice == 1m).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`user_id`,t.`user_name`,t.`user_age`,t.`create_time` FROM `m8_user2` t LEFT JOIN (SELECT t1.`book_id`,t1.`user_id`,t1.`book_name`,t1.`book_price`,(ROW_NUMBER() OVER (PARTITION BY t1.`user_id` ORDER BY CASE WHEN t1.`book_price` IS NULL THEN 1 ELSE 0 END ASC,t1.`book_price` DESC)) AS `__row__` FROM `m8_user_book` t1 WHERE t1.`book_price` > ?) t3 ON (t3.`user_id` = t.`user_id` AND t3.`__row__` = ?) WHERE t3.`book_price` = ?");
        listen.AssertParams("100(BigDecimal),1(Integer),1(BigDecimal)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#manyJoinMany2Many10")]
    public async Task ManyJoinMany2Many10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User2>().GroupJoin(u => u.Books).Where(s => s.Books!.Where(x => x.BookPrice > 100m).Sum(x => x.BookPrice) == 1m).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`user_id`,t.`user_name`,t.`user_age`,t.`create_time` FROM `m8_user2` t LEFT JOIN (SELECT t1.`user_id` AS `__group_key1__`,SUM(t1.`book_price`) AS `__sum2__` FROM `m8_user_book` t1 WHERE t1.`book_price` > ? GROUP BY t1.`user_id`) t2 ON t2.`__group_key1__` = t.`user_id` WHERE IFNULL(t2.`__sum2__`,0) = ?");
        listen.AssertParams("100(BigDecimal),1(BigDecimal)");
    }

    // Java 用例没有断言：分组拼接后再模糊匹配
    [Fact, Upstream("mysql8/ManyJoinTest.java#testManyJoinGroup")]
    public async Task TestManyJoinGroup()
    {
        await Db.Query<M8User2>().GroupJoin(u => u.Books).Where(m => string.Join(",", m.Books!.Select(x => x.BookName)).Contains("123")).ToListAsync(Ct);
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#testManyJoinGroup1")]
    public async Task TestManyJoinGroup1()
    {
        var list = await Db.Query<M8User2>().Include(x => x.Roles).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.NotEmpty(list[0].Roles!);
    }

    // 嵌套的分组合并：角色的分组子查询里再按菜单分组（Java 的 subQueryConfigure(roles, s -> s.subQueryToGroupJoin(menus))）
    [Fact, Upstream("mysql8/ManyJoinTest.java#testMany2Many2GroupJoin")]
    public async Task TestMany2Many2GroupJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoin(u => u.Roles, q => q.GroupJoin(r => r.Menus))
            .Where(m => m.Roles!.Any(r => r.Menus!.Any(menu => menu.Name == "admin"))).ToListAsync(Ct);
        listen.AssertSql(NestedAnySql);
        listen.AssertParams("1(Integer),1(Integer),admin(String),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    private const string NestedAnySql = "SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` LEFT JOIN (SELECT t6.`role_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_menu` t5 INNER JOIN `m8_role_menu` t6 ON t5.`id` = t6.`menu_id` WHERE t5.`name` = ? GROUP BY t6.`role_id`) t8 ON t8.`__group_key1__` = t1.`id` WHERE IFNULL(t8.`__any2__`,?) = ? GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__any2__`,?) = ?";

    [Fact, Upstream("mysql8/ManyJoinTest.java#testMany2Many2GroupJoin2", Deviation = FlatElement)]
    public async Task TestMany2Many2GroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoin(u => u.Roles, q => q.GroupJoin(r => r.Menus))
            .Where(m => m.Roles!.Any(r => r.Menus!.Any(menu => menu.Name == "admin"))).ToListAsync(Ct);
        listen.AssertSql(NestedAnySql);
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#testMany2Many2GroupJoin24", Deviation = FlatElement)]
    public async Task TestMany2Many2GroupJoin24()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoin(u => u.Roles, q => q.GroupJoin(r => r.Menus)).Where(m => m.Roles!.Any(r => r.Id == "123")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`id` = ? GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),123(String),false(Boolean),true(Boolean)");
    }

    // GroupJoinAll：子查询里的集合导航同样合并
    [Fact, Upstream("mysql8/ManyJoinTest.java#testMany2Many2GroupJoin3")]
    public async Task TestMany2Many2GroupJoin3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll().Where(m => m.Roles!.Any(r => r.Menus!.Any(menu => menu.Name == "admin"))).ToListAsync(Ct);
        listen.AssertSql(NestedAnySql);
        listen.AssertParams("1(Integer),1(Integer),admin(String),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#testMany2Many2GroupJoin4", Deviation = FlatElement)]
    public async Task TestMany2Many2GroupJoin4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll().Where(m => m.Roles!.Any(r => r.Menus!.Any(menu => menu.Name == "admin"))).ToListAsync(Ct);
        listen.AssertSql(NestedAnySql);
    }

    // 两个条件各是一个聚合，两层分组表里各有两列
    [Fact, Upstream("mysql8/ManyJoinTest.java#testMany2Many2GroupJoin5", Deviation = FlatElement)]
    public async Task TestMany2Many2GroupJoin5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll()
            .Where(m => m.Roles!.Any(r => r.Menus!.Any(menu => menu.Name == "admin")) && m.Roles!.Any(r => r.Menus!.Any(menu => menu.Path!.StartsWith("/admin"))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,(COUNT((CASE WHEN IFNULL(t8.`__any2__`,?) = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,(COUNT((CASE WHEN IFNULL(t8.`__any3__`,?) = ? THEN ? ELSE NULL END)) > 0) AS `__any3__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` LEFT JOIN (SELECT t6.`role_id` AS `__group_key1__`,(COUNT((CASE WHEN t5.`name` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,(COUNT((CASE WHEN t5.`path` LIKE ? THEN ? ELSE NULL END)) > 0) AS `__any3__` FROM `m8_menu` t5 INNER JOIN `m8_role_menu` t6 ON t5.`id` = t6.`menu_id` GROUP BY t6.`role_id`) t8 ON t8.`__group_key1__` = t1.`id` GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__any2__`,?) = ? AND IFNULL(t4.`__any3__`,?) = ?");
        listen.AssertParams("false(Boolean),true(Boolean),1(Integer),false(Boolean),true(Boolean),1(Integer),admin(String),1(Integer),/admin%(String),1(Integer),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    // 多对多取第一个（没有分组合并）
    [Fact, Upstream("mysql8/ManyJoinTest.java#partitionByFirst")]
    public async Task PartitionByFirst()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().Where(m => m.Roles!.OrderBy(s => s.Name).First().Name == "管理员").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__user_id__`,t1.`id`,t1.`name`,t1.`create_time`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id`) t5 ON (t5.`__user_id__` = t.`id` AND t5.`__row__` = ?) WHERE t5.`name` = ?");
        listen.AssertParams("1(Integer),管理员(String)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#partitionByFirst2")]
    public async Task PartitionByFirst2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().Where(m => m.Roles!.OrderBy(s => s.Name).First().CreateTime <= new DateTime(2020, 1, 1, 1, 1, 0)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__user_id__`,t1.`id`,t1.`name`,t1.`create_time`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id`) t5 ON (t5.`__user_id__` = t.`id` AND t5.`__row__` = ?) WHERE t5.`create_time` <= ?");
        listen.AssertParams("1(Integer),2020-01-01T01:01(LocalDateTime)");
    }

    // 同一导航上的条件聚合与计数共用一张分组表；条件相同，提到 WHERE
    [Fact, Upstream("mysql8/ManyJoinTest.java#testGroupJoinCombine")]
    public async Task TestGroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().Where(u => u.BankCards!.Any(o => o.Code == "123"))
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => o.Code == "123").Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__`,COUNT(?) AS `__count3__` FROM `t_bank_card` t1 WHERE t1.`code` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),1(Integer),123(String),false(Boolean),true(Boolean)");
    }

    // UPDATE 的条件里同样可以合并分组
    [Fact, Upstream("mysql8/ManyJoinTest.java#test2GroupJoinCombine")]
    public async Task Test2GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<SysUser>().GroupJoinAll().Set(u => u.Id, (string?)null).Where(u => u.BankCards!.Any(o => o.Code == "123") && u.Id == null).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE t1.`code` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` SET t.`id` = ? WHERE IFNULL(t2.`__any2__`,?) = ? AND t.`id` IS NULL");
        listen.AssertParams("1(Integer),123(String),null(null),false(Boolean),true(Boolean)");
    }

    // 两个聚合共有的条件（code、用户名）提到 WHERE，不同的部分留在各自的 CASE 里
    [Fact, Upstream("mysql8/ManyJoinTest.java#test3GroupJoinCombine")]
    public async Task Test3GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => u.BankCards!.Any(o => o.Type == "456" && o.Code == "123" && o.User!.Name == "123"))
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => o.Code == "123" && o.Type == "4567" && o.User!.Name == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count3__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` WHERE t1.`code` = ? AND t3.`name` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("456(String),1(Integer),4567(String),1(Integer),123(String),123(String),false(Boolean),true(Boolean)");
    }

    // OR 里的顺序不同算不同的条件，只有用户名是共有的
    [Fact, Upstream("mysql8/ManyJoinTest.java#test4GroupJoinCombine")]
    public async Task Test4GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => u.BankCards!.Any(o => (o.Type == "456" || o.Code == "123") && o.User!.Name == "123"))
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => (o.Code == "123" || o.Type == "4567") && o.User!.Name == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT((CASE WHEN (t1.`type` = ? OR t1.`code` = ?) THEN ? ELSE NULL END)) > 0) AS `__any2__`,COUNT((CASE WHEN (t1.`code` = ? OR t1.`type` = ?) THEN ? ELSE NULL END)) AS `__count3__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` WHERE t3.`name` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("456(String),123(String),1(Integer),123(String),4567(String),1(Integer),123(String),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#test5GroupJoinCombine")]
    public async Task Test5GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => u.BankCards!.Any(o => (o.Code == "123" || o.Type == "4567") && o.User!.Name == "123"))
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => (o.Code == "123" || o.Type == "4567") && o.User!.Name == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__`,COUNT(?) AS `__count3__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` WHERE (t1.`code` = ? OR t1.`type` = ?) AND t3.`name` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),1(Integer),123(String),4567(String),123(String),false(Boolean),true(Boolean)");
    }

    // Java 的 bankCards().filter(…) 是导航级条件：C# 的 FilterNavigation；用户表的 JOIN 先于分组表编号
    [Fact, Upstream("mysql8/ManyJoinTest.java#test6GroupJoinCombine")]
    public async Task Test6GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().FilterNavigation(u => u.BankCards, o => (o.Code == "123" || o.Type == "4567") && o.User!.Name == "123")
            .Where(u => u.BankCards!.Any())
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t3.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,COUNT(*) AS `__count3__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t2 ON t2.`id` = t1.`uid` WHERE (t1.`code` = ? OR t1.`type` = ?) AND t2.`name` = ? GROUP BY t1.`uid`) t3 ON t3.`__group_key1__` = t.`id` WHERE IFNULL(t3.`__any2__`,?) = ?");
        listen.AssertParams("123(String),4567(String),123(String),false(Boolean),true(Boolean)");
    }

    // 计数没有条件：不提取，Any 的条件留在 CASE 里
    [Fact, Upstream("mysql8/ManyJoinTest.java#test7GroupJoinCombine")]
    public async Task Test7GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => u.BankCards!.Any(o => (o.Code == "123" || o.Type == "4567") && o.User!.Name == "123"))
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT((CASE WHEN (t1.`code` = ? OR t1.`type` = ?) AND t3.`name` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,COUNT(*) AS `__count3__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("123(String),4567(String),123(String),1(Integer),false(Boolean),true(Boolean)");
    }

    private const string AnyThenFilteredCountSql = "SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,COUNT((CASE WHEN (t1.`code` = ? OR t1.`type` = ?) AND t3.`name` = ? THEN ? ELSE NULL END)) AS `__count3__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?";

    [Fact, Upstream("mysql8/ManyJoinTest.java#test8GroupJoinCombine"), Upstream("mysql8/ManyJoinTest.java#test9GroupJoinCombine")]
    public async Task Test8GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().Where(u => u.BankCards!.Any())
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => (o.Code == "123" || o.Type == "4567") && o.User!.Name == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql(AnyThenFilteredCountSql);
        listen.AssertParams("123(String),4567(String),123(String),1(Integer),false(Boolean),true(Boolean)");
    }

    // 排序里的聚合在投影之前翻译，编号在前
    [Fact, Upstream("mysql8/ManyJoinTest.java#test10GroupJoinCombine")]
    public async Task Test10GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().Where(u => u.BankCards!.Any())
            .OrderBy(u => u.BankCards!.Where(x => x.Type == "123").Count())
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => o.Code == "123" && o.User!.Name == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count4__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count3__`,COUNT((CASE WHEN t1.`code` = ? AND t3.`name` = ? THEN ? ELSE NULL END)) AS `__count4__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? ORDER BY IFNULL(t2.`__count3__`,0) ASC");
        listen.AssertParams("123(String),1(Integer),123(String),123(String),1(Integer),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#test11GroupJoinCombine")]
    public async Task Test11GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().Where(u => u.BankCards!.Where(x => x.Type == "123").Any())
            .OrderBy(u => u.BankCards!.Where(x => x.Type == "123").Count())
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => o.Code == "123" && o.User!.Name == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count4__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count3__`,COUNT((CASE WHEN t1.`code` = ? AND t3.`name` = ? THEN ? ELSE NULL END)) AS `__count4__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? ORDER BY IFNULL(t2.`__count3__`,0) ASC");
        listen.AssertParams("123(String),1(Integer),123(String),1(Integer),123(String),123(String),1(Integer),false(Boolean),true(Boolean)");
    }

    private const string SharedTypeSql = "SELECT t.`name` AS `value1`,IFNULL(t2.`__count4__`,0) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__`,COUNT(?) AS `__count3__`,COUNT((CASE WHEN t1.`code` = ? AND t3.`name` = ? THEN ? ELSE NULL END)) AS `__count4__` FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t3 ON t3.`id` = t1.`uid` WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? ORDER BY ";

    // 三个聚合都有 type 条件：提到 WHERE，第三个聚合的 CASE 里只剩其余两项
    [Fact, Upstream("mysql8/ManyJoinTest.java#test12GroupJoinCombine")]
    public async Task Test12GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().Where(u => u.BankCards!.Where(x => x.Type == "123").Any())
            .OrderBy(u => u.BankCards!.Where(x => x.Type == "123").Count())
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => o.Code == "123" && o.User!.Name == "123" && o.Type == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql(SharedTypeSql + "IFNULL(t2.`__count3__`,0) ASC");
        listen.AssertParams("1(Integer),1(Integer),123(String),123(String),1(Integer),123(String),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#test13GroupJoinCombine")]
    public async Task Test13GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().Where(u => u.BankCards!.Where(x => x.Type == "123").Any())
            .OrderBy(u => Sql.NullsLast(u.BankCards!.Where(x => x.Type == "123").Count()))
            .Select(u => new Draft<string?, long>(u.Name, u.BankCards!.Where(o => o.Code == "123" && o.User!.Name == "123" && o.Type == "123").Count()))
            .ToListAsync(Ct);
        listen.AssertSql(SharedTypeSql + "CASE WHEN IFNULL(t2.`__count3__`,0) IS NULL THEN 1 ELSE 0 END ASC,IFNULL(t2.`__count3__`,0) ASC");
        listen.AssertParams("1(Integer),1(Integer),123(String),123(String),1(Integer),123(String),false(Boolean),true(Boolean)");
    }

    // 只在排序里用到导航聚合
    [Fact, Upstream("mysql8/ManyJoinTest.java#test14GroupJoinCombine")]
    public async Task Test14GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().OrderBy(u => Sql.NullsLast(u.BankCards!.Where(x => x.Type == "123").Count()))
            .Select(u => new Draft<string?>(u.Name)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY CASE WHEN IFNULL(t2.`__count2__`,0) IS NULL THEN 1 ELSE 0 END ASC,IFNULL(t2.`__count2__`,0) ASC");
        listen.AssertParams("1(Integer),123(String)");
    }

    private const string MaxOpenTimeSql = "SELECT t.`name` AS `value1` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX(t1.`open_time`) AS `__max2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY CASE WHEN t2.`__max2__` IS NULL THEN 1 ELSE 0 END ASC,t2.`__max2__` ASC";

    [Fact, Upstream("mysql8/ManyJoinTest.java#test15GroupJoinCombine")]
    public async Task Test15GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().OrderBy(u => Sql.NullsLast(u.BankCards!.Where(x => x.Type == "123").Max(s => s.OpenTime)))
            .Select(u => new Draft<string?>(u.Name)).ToListAsync(Ct);
        listen.AssertSql(MaxOpenTimeSql);
        listen.AssertParams("123(String)");
    }

    // 不提取共有条件：唯一的聚合也写成 CASE WHEN
    [Fact, Upstream("mysql8/ManyJoinTest.java#test16GroupJoinCombine")]
    public async Task Test16GroupJoinCombine()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().GroupJoinNoMerge().OrderBy(u => Sql.NullsLast(u.BankCards!.Where(x => x.Type == "123").Max(s => s.OpenTime)))
            .Select(u => new Draft<string?>(u.Name)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX((CASE WHEN t1.`type` = ? THEN t1.`open_time` ELSE NULL END)) AS `__max2__` FROM `t_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY CASE WHEN t2.`__max2__` IS NULL THEN 1 ELSE 0 END ASC,t2.`__max2__` ASC");
        listen.AssertParams("123(String)");
    }

    // 分页的计数查询去掉只为排序服务的分组表；总数只有 2 条，数据查询的 LIMIT 收到 2
    [Fact, Upstream("mysql8/ManyJoinTest.java#test16GroupJoinCombinePage")]
    public async Task Test16GroupJoinCombinePage()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().OrderBy(u => Sql.NullsLast(u.BankCards!.Where(x => x.Type == "123").Max(s => s.OpenTime)))
            .Select(u => new Draft<string?>(u.Name)).ToPageAsync(1, 20, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `t_sys_user` t");
        listen.AssertSql(1, MaxOpenTimeSql + " LIMIT 2");
        listen.AssertParams(1, "123(String)");
    }

    // Java 用例没有断言：最后开户的那张卡的几个列与它的银行，同一取法只 JOIN 一次
    [Fact, Upstream("mysql8/ManyJoinTest.java#testLastCard")]
    public async Task TestLastCard()
    {
        await Db.Query<SysUser>()
            .Select(u => new Draft<string?, string?, string?, string?, string?>(
                u.Id, u.Name,
                u.BankCards!.OrderByDescending(c => c.OpenTime).First().Code,
                u.BankCards!.OrderByDescending(c => c.OpenTime).First().Type,
                u.BankCards!.OrderByDescending(c => c.OpenTime).First().Bank!.Name))
            .ToListAsync(Ct);
    }
}
