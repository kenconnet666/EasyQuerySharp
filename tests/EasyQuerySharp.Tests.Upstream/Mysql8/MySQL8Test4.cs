using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/MySQL8Test4.java（投影里取第几张卡、与 null 比较时的值过滤、All / notEmptyAll、投影成实体后再按列过滤）。
/// Java 的 startsWith / contains 对应 <c>Sql.LikeStartsWith</c> / <c>Sql.LikeContains</c>（<c>LIKE CONCAT(…)</c>）。
/// </summary>
public sealed class MySQL8Test4(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // 同一取法（最早开户的卡）的几列与它的银行，只 JOIN 一次
    [Fact, Upstream("mysql8/MySQL8Test4.java#testPartition")]
    public async Task TestPartition()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Select(u => new Draft<string?, string?, string?, string?, DateTime?, string?>(
                u.Id, u.Name,
                u.BankCards!.OrderBy(c => c.OpenTime).First().Code,
                u.BankCards!.OrderBy(c => c.OpenTime).First().Type,
                u.BankCards!.OrderBy(c => c.OpenTime).First().OpenTime,
                u.BankCards!.OrderBy(c => c.OpenTime).First().Bank!.Name))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`name` AS `value2`,t3.`code` AS `value3`,t3.`type` AS `value4`,t3.`open_time` AS `value5`,t4.`name` AS `value6` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) INNER JOIN `t_bank` t4 ON t4.`id` = t3.`bank_id`");
        listen.AssertParams("1(Integer)");
    }

    /// <summary>Java 的 UserDTO2：用户与第 4 张卡（按开户时间）的信息。</summary>
    public sealed class UserDto2
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? ThirdCardType { get; set; }

        public string? ThirdCardCode { get; set; }

        public string? ThirdCardBankName { get; set; }
    }

    private const string DtoAlias = "Java 的 DTO Proxy 逐列 set 时同名列也写 AS（t.`id` AS `id`）；C# 的成员初始化与 Java 的 select(DTO.class) 一致，同名不写";

    // 投影里才建的取元素 JOIN 按创建顺序编号，排在条件里的子查询之后
    [Fact, Upstream("mysql8/MySQL8Test4.java#testPartition2", Deviation = DtoAlias)]
    public async Task TestPartition2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").Count() > 3L && !u.BankCards!.Where(c => c.Type == "信用卡").Any())
            .Select(u => new UserDto2
            {
                Id = u.Id,
                Name = u.Name,
                ThirdCardType = u.BankCards!.OrderBy(c => c.OpenTime).ElementAt(3).Type,
                ThirdCardCode = u.BankCards!.OrderBy(c => c.OpenTime).ElementAt(3).Code,
                ThirdCardBankName = u.BankCards!.OrderBy(c => c.OpenTime).ElementAt(3).Bank!.Name,
            })
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t5.`type` AS `third_card_type`,t5.`code` AS `third_card_code`,t6.`name` AS `third_card_bank_name` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`id`,t3.`uid`,t3.`code`,t3.`type`,t3.`bank_id`,t3.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t3.`uid` ORDER BY t3.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t3) t5 ON (t5.`uid` = t.`id` AND t5.`__row__` = ?) INNER JOIN `t_bank` t6 ON t6.`id` = t5.`bank_id` WHERE (SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?) > ? AND NOT (EXISTS(SELECT 1 FROM `t_bank_card` t2 WHERE t2.`uid` = t.`id` AND t2.`type` = ? LIMIT 1))");
        listen.AssertParams("4(Integer),储蓄卡(String),3(Long),信用卡(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testPartition3", Deviation = DtoAlias)]
    public async Task TestPartition3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").Count() > 4L && !u.BankCards!.Where(c => c.Type == "信用卡").Any())
            .Select(u => new UserDto2
            {
                Id = u.Id,
                Name = u.Name,
                ThirdCardType = u.BankCards!.OrderBy(c => c.OpenTime).ElementAt(3).Type,
                ThirdCardCode = u.BankCards!.OrderBy(c => c.OpenTime).ElementAt(3).Code,
                ThirdCardBankName = u.BankCards!.OrderBy(c => c.OpenTime).ElementAt(3).Bank!.Name,
            })
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t5.`type` AS `third_card_type`,t5.`code` AS `third_card_code`,t6.`name` AS `third_card_bank_name` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count2__`,(COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) <= 0) AS `__none3__` FROM `t_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`id`,t3.`uid`,t3.`code`,t3.`type`,t3.`bank_id`,t3.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t3.`uid` ORDER BY t3.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t3) t5 ON (t5.`uid` = t.`id` AND t5.`__row__` = ?) INNER JOIN `t_bank` t6 ON t6.`id` = t5.`bank_id` WHERE IFNULL(t2.`__count2__`,0) > ? AND IFNULL(t2.`__none3__`,?) = ?");
        listen.AssertParams("储蓄卡(String),1(Integer),信用卡(String),1(Integer),4(Integer),4(Long),true(Boolean),true(Boolean)");
    }

    // 没有值过滤时与 null 比较照写（参数是 null）
    [Fact, Upstream("mysql8/MySQL8Test4.java#testSubQueryCountCompareNull1")]
    public async Task TestSubQueryCountCompareNull1()
    {
        long? val = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards!.Count() > val).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE (SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id`) > ?");
        listen.AssertParams("null(null)");
    }

    // 去掉的条件里只有集合聚合（子查询），Java 没有为它建表，主表不带别名
    [Fact, Upstream("mysql8/MySQL8Test4.java#testSubQueryCountCompareNull2")]
    public async Task TestSubQueryCountCompareNull2()
    {
        long? val = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).Where(u => u.BankCards!.Count() > val).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`name`,`phone`,`age`,`create_time` FROM `t_sys_user`");
    }

    private const string UsersSql = "SELECT `id`,`name`,`phone`,`age`,`create_time` FROM `t_sys_user`";

    // Java 的 isAfter / isBefore 对应 > / <
    [Fact, Upstream("mysql8/MySQL8Test4.java#testTimeAfterNull1")]
    public async Task TestTimeAfterNull1()
    {
        DateTime? val = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.CreateTime > val).ToListAsync(Ct);
        listen.AssertSql(UsersSql + " WHERE `create_time` > ?");
        listen.AssertParams("null(null)");
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testTimeAfterNull2")]
    public async Task TestTimeAfterNull2()
    {
        DateTime? val = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).Where(u => u.CreateTime > val).ToListAsync(Ct);
        listen.AssertSql(UsersSql);
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testTimeBeforeNull1")]
    public async Task TestTimeBeforeNull1()
    {
        DateTime? val = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.CreateTime < val).ToListAsync(Ct);
        listen.AssertSql(UsersSql + " WHERE `create_time` < ?");
        listen.AssertParams("null(null)");
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testTimeBeforeNull2")]
    public async Task TestTimeBeforeNull2()
    {
        DateTime? val = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).Where(u => u.CreateTime < val).ToListAsync(Ct);
        listen.AssertSql(UsersSql);
    }

    private const string CardsOfU2 = "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t WHERE t.`uid` IN (?)";

    // 储蓄卡全部以 33123 开头（没有储蓄卡也算）：只有 u2；带出的银行卡与条件一致
    private static void AssertOnlyU2(IReadOnlyList<SysUser> users, Func<SysBankCard, bool> all)
    {
        Assert.Single(users);
        Assert.All(users, u => Assert.True(u.BankCards!.Where(c => c.Type == "储蓄卡").All(all)));
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testAll")]
    public async Task TestAll()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE NOT (EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? AND (NOT (t1.`code` LIKE CONCAT(?,'%'))) LIMIT 1))");
        listen.AssertParams(0, "储蓄卡(String),33123(String)");
        listen.AssertSql(1, CardsOfU2);
        listen.AssertParams(1, "u2(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testAll1")]
    public async Task TestAll1()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => (c.Code ?? "").LikeStartsWith("33123"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => (c.Code ?? "").StartsWith("33123", StringComparison.Ordinal));
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE NOT (EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? AND (NOT (IFNULL(t1.`code`,?) LIKE CONCAT(?,'%'))) LIMIT 1))");
        listen.AssertParams(0, "储蓄卡(String),(String),33123(String)");
        listen.AssertSql(1, CardsOfU2);
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testAll2")]
    public async Task TestAll2()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true && c.Code.StartsWith("45678", StringComparison.Ordinal));
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE NOT (EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? AND (NOT (t1.`code` LIKE CONCAT(?,'%') AND t1.`code` LIKE CONCAT(?,'%'))) LIMIT 1))");
        listen.AssertParams(0, "储蓄卡(String),33123(String),45678(String)");
        listen.AssertSql(1, CardsOfU2);
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testAllGroupJoin")]
    public async Task TestAllGroupJoin()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().GroupJoinAll().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) <= 0) AS `__none2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? AND (NOT (t1.`code` LIKE CONCAT(?,'%'))) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__none2__`,?) = ?");
        listen.AssertParams(0, "1(Integer),储蓄卡(String),33123(String),true(Boolean),true(Boolean)");
        listen.AssertSql(1, CardsOfU2);
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testAllGroupJoin2")]
    public async Task TestAllGroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().GroupJoinAll().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true && c.Code.StartsWith("45678", StringComparison.Ordinal));
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) <= 0) AS `__none2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? AND (NOT (t1.`code` LIKE CONCAT(?,'%') AND t1.`code` LIKE CONCAT(?,'%'))) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__none2__`,?) = ?");
        listen.AssertParams(0, "1(Integer),储蓄卡(String),33123(String),45678(String),true(Boolean),true(Boolean)");
        listen.AssertSql(1, CardsOfU2);
    }

    // Java 的 notEmptyAll：至少有一张、且全部满足；C# 写成分组的 Any && All
    [Fact, Upstream("mysql8/MySQL8Test4.java#testAll4", Deviation = "Java 的 notEmptyAll(…) 是一个方法；C# 写成 Sql.Group(Any() && All(…))，SQL 相同")]
    public async Task TestAll4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Where(u => Sql.Group(u.BankCards!.Where(c => c.Type == "储蓄卡").Any() && u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE (EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? LIMIT 1) AND NOT (EXISTS(SELECT 1 FROM `t_bank_card` t2 WHERE t2.`uid` = t.`id` AND t2.`type` = ? AND (NOT (t2.`code` LIKE CONCAT(?,'%'))) LIMIT 1)))");
        listen.AssertParams("储蓄卡(String),储蓄卡(String),33123(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test4.java#testAll5", Deviation = "Java 的 notEmptyAll(…) 是一个方法；C# 写成 Sql.Group(Any() && All(…))，SQL 相同")]
    public async Task TestAll5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => Sql.Group(u.BankCards!.Where(c => c.Type == "储蓄卡").Any() && u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__`,(COUNT((CASE WHEN (NOT (t1.`code` LIKE CONCAT(?,'%'))) THEN ? ELSE NULL END)) <= 0) AS `__none3__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE (IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__none3__`,?) = ?)");
        listen.AssertParams("1(Integer),33123(String),1(Integer),储蓄卡(String),false(Boolean),true(Boolean),true(Boolean),true(Boolean)");
    }

    private const string SelectAsPhoneSql = "SELECT t1.`id`,t1.`phone` AS `phone`,t1.`age` FROM (SELECT t.`id`,t.`name` AS `phone`,t.`age` FROM `t_sys_user` t) t1 WHERE t1.`phone` LIKE CONCAT('%',?,'%')";

    // 投影成实体（名字放进 Phone）再按这一列过滤：Java 的 ClassProxy.of(…).columns(…) 与 Select.of(…)
    [Fact, Upstream("mysql8/MySQL8Test4.java#testSelect"), Upstream("mysql8/MySQL8Test4.java#testSelect1")]
    public async Task TestSelect()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Select(u => new SysUser { Id = u.Id, Phone = u.Name, Age = u.Age }).Where(t => t.Phone.LikeContains("123")).ToListAsync(Ct);
        listen.AssertSql(SelectAsPhoneSql);
        listen.AssertParams("123(String)");
    }
}
