using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// M8BankTest 的导航用例：对一导航的隐式 JOIN、集合导航的子查询、取一段行（Java 的 elements(i, j) = C# 的 Skip / Take）
public sealed partial class M8BankTest
{
    [Fact, Upstream("mysql8/M8BankTest.java#test2")]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().Where(c => c.User!.Phone!.Contains("1234") && c.Bank!.Name == "工商银行").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t LEFT JOIN `t_sys_user` t1 ON t1.`id` = t.`uid` INNER JOIN `t_bank` t2 ON t2.`id` = t.`bank_id` WHERE t1.`phone` LIKE ? AND t2.`name` = ?");
        listen.AssertParams("%1234%(String),工商银行(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test3")]
    public async Task Test3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().Where(c => c.User!.Name == "小明" && c.Bank!.Name == "工商银行").OrderBy(c => c.Code)
            .Select(c => new Draft<string?, string?, string?>(c.User!.Name, c.Bank!.Name, c.Code)).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`name` AS `value1`,t2.`name` AS `value2`,t.`code` AS `value3` FROM `t_bank_card` t LEFT JOIN `t_sys_user` t1 ON t1.`id` = t.`uid` INNER JOIN `t_bank` t2 ON t2.`id` = t.`bank_id` WHERE t1.`name` = ? AND t2.`name` = ? ORDER BY t.`code` ASC");
        listen.AssertParams("小明(String),工商银行(String)");
    }

    // Java 的 subQueryConfigure(…, filterConfigure(NotNullOrEmptyValueFilter)) 只影响值为空的条件，这里条件都有值
    [Fact, Upstream("mysql8/M8BankTest.java#test4", Deviation = "Java 给子查询单独配了值过滤；C# 的 UseValueFilter 作用于整条查询，这里条件都有值，SQL 相同")]
    public async Task Test4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(u => u.BankCards!.Where(c => c.Bank!.Name == "工商银行").Count() >= 2L && !u.BankCards!.Any(c => c.Bank!.Name == "建设银行"))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE (SELECT COUNT(*) FROM `t_bank_card` t1 INNER JOIN `t_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`uid` = t.`id` AND t2.`name` = ?) >= ? AND NOT (EXISTS(SELECT 1 FROM `t_bank_card` t3 INNER JOIN `t_bank` t4 ON t4.`id` = t3.`bank_id` WHERE t3.`uid` = t.`id` AND t4.`name` = ? LIMIT 1))");
        listen.AssertParams("工商银行(String),2(Long),建设银行(String)");
    }

    // Java 的 subQueryToGroupJoin：C# 的 GroupJoin
    [Fact, Upstream("mysql8/M8BankTest.java#test5")]
    public async Task Test5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Bank!.Name == "工商银行").Count() >= 2L && !u.BankCards!.Any(c => c.Bank!.Name == "建设银行"))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT((CASE WHEN t3.`name` = ? THEN ? ELSE NULL END)) AS `__count2__`,(COUNT((CASE WHEN t3.`name` = ? THEN ? ELSE NULL END)) <= 0) AS `__none3__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t3 ON t3.`id` = t1.`bank_id` GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) >= ? AND IFNULL(t2.`__none3__`,?) = ?");
        listen.AssertParams("工商银行(String),1(Integer),建设银行(String),1(Integer),2(Long),true(Boolean),true(Boolean)");
    }

    // 用户的银行卡里第一张开户的（按开户时间）是工商银行
    [Fact, Upstream("mysql8/M8BankTest.java#test6")]
    public async Task Test6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards!.OrderBy(c => c.OpenTime).First().Bank!.Name == "工商银行").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) INNER JOIN `t_bank` t4 ON t4.`id` = t3.`bank_id` WHERE t4.`name` = ?");
        listen.AssertParams("1(Integer),工商银行(String)");
    }

    // 用户最早开户的两张卡的类型：ORDER BY / LIMIT 留在字符串聚合的子查询里
    [Fact, Upstream("mysql8/M8BankTest.java#test7")]
    public async Task Test7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.Name!.Contains("小明"))
            .Select(u => new Draft<string?, string>(u.Name, string.Join(",", u.BankCards!.OrderBy(c => c.OpenTime).Take(2).Select(c => c.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,(SELECT GROUP_CONCAT(t1.`type` SEPARATOR ?) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` ORDER BY t1.`open_time` ASC LIMIT 2) AS `value2` FROM `t_sys_user` t WHERE t.`name` LIKE ?");
        listen.AssertParams(",(String),%小明%(String)");
    }

    // 取了两张之后再按银行过滤：先把这两张包成派生表
    [Fact, Upstream("mysql8/M8BankTest.java#test7_1")]
    public async Task Test7_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.Name!.Contains("小明"))
            .Select(u => new Draft<string?, string>(u.Name,
                string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Take(2).Where(c => c.Bank!.Name == "建设银行").Select(c => c.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,(SELECT GROUP_CONCAT(t2.`type` SEPARATOR ?) FROM (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time` FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? ORDER BY t1.`open_time` ASC LIMIT 2) t2 INNER JOIN `t_bank` t3 ON t3.`id` = t2.`bank_id` WHERE t3.`name` = ?) AS `value2` FROM `t_sys_user` t WHERE t.`name` LIKE ?");
        listen.AssertParams(",(String),储蓄卡(String),建设银行(String),%小明%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test7_2")]
    public async Task Test7_2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.Name!.Contains("小明"))
            .Select(u => new Draft<string?, string>(u.Name, string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Take(2).Select(c => c.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,(SELECT GROUP_CONCAT(t1.`type` SEPARATOR ?) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? ORDER BY t1.`open_time` ASC LIMIT 2) AS `value2` FROM `t_sys_user` t WHERE t.`name` LIKE ?");
        listen.AssertParams(",(String),储蓄卡(String),%小明%(String)");
    }

    // 最早开户的两张卡里没有杭州银行的：EXISTS 作用在包好的派生表上
    [Fact, Upstream("mysql8/M8BankTest.java#test8")]
    public async Task Test8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.OrderBy(c => c.OpenTime).Take(2).Any(c => c.Bank!.Name == "杭州银行"))
            .Select(u => new Draft<string?, string>(u.Name, string.Join(",", u.BankCards!.OrderBy(c => c.OpenTime).Take(2).Select(c => c.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,(SELECT GROUP_CONCAT(t4.`type` SEPARATOR ?) FROM `t_bank_card` t4 WHERE t4.`uid` = t.`id` ORDER BY t4.`open_time` ASC LIMIT 2) AS `value2` FROM `t_sys_user` t WHERE t.`name` LIKE ? AND NOT (EXISTS(SELECT 1 FROM (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time` FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` ORDER BY t1.`open_time` ASC LIMIT 2) t2 INNER JOIN `t_bank` t3 ON t3.`id` = t2.`bank_id` WHERE t3.`name` = ? LIMIT 1))");
        listen.AssertParams(",(String),%小明%(String),杭州银行(String)");
    }

    // 值为 null 的条件被值过滤去掉；条件里用过导航，主表照样带别名 t
    [Fact, Upstream("mysql8/M8BankTest.java#test9")]
    public async Task Test9()
    {
        string? queryName = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(c => c.User!.Name == queryName).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t");
    }
}
