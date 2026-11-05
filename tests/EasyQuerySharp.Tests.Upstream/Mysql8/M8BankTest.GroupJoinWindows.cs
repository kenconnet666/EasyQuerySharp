namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// M8BankTest 里分组合并前先取一段行的用例：Java 的 orderBy(…).elements(i, j) 是 C# 的 OrderBy(…).Skip(i).Take(j - i + 1)。
// 同样取法的聚合合并到同一张派生表，派生表里按 ROW_NUMBER() 编组内行号再取段
public sealed partial class M8BankTest
{
    private static readonly DateTime Y2000 = new(2000, 1, 1);

    [Fact, Upstream("mysql8/M8BankTest.java#test11")]
    public async Task Test11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.OrderBy(c => c.OpenTime).Take(2).Any(c => c.Bank!.Name == "杭州银行"))
            .Select(u => new Draft<string?, string>(u.Name, string.Join(",", u.BankCards!.OrderBy(c => c.OpenTime).Take(2).Select(c => c.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t4.`__joining3__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t5.`name` = ? THEN ? ELSE NULL END)) <= 0) AS `__none2__`,GROUP_CONCAT(t3.`type` SEPARATOR ?) AS `__joining3__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("杭州银行(String),1(Integer),,(String),1(Long),2(Long),%小明%(String),true(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test12")]
    public async Task Test12()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.Where(c => c.Type == "123").OrderBy(c => c.OpenTime).Take(2).Any(c => c.Bank!.Name == "杭州银行"))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT(?) <= 0) AS `__none2__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1 WHERE t1.`type` = ?) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` WHERE t5.`name` = ? GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("1(Integer),123(String),1(Long),2(Long),杭州银行(String),%小明%(String),true(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test13")]
    public async Task Test13()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Skip(1).Take(2).Any(c => c.Bank!.Name == "杭州银行"))
            .Select(u => new Draft<string?, string>(u.Name,
                string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Skip(1).Take(2).Select(c => c.Bank!.Id))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t4.`__joining3__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t5.`name` = ? THEN ? ELSE NULL END)) <= 0) AS `__none2__`,GROUP_CONCAT(t5.`id` SEPARATOR ?) AS `__joining3__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1 WHERE t1.`type` = ?) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("杭州银行(String),1(Integer),,(String),储蓄卡(String),2(Long),3(Long),%小明%(String),true(Boolean),true(Boolean)");
    }

    // Java 的 subQueryConfigure(…, q -> q.where(…).orderBy(…))：C# 的 GroupJoin(…, q => q.Where(…).OrderBy(…))，链上不写排序时按它编行号
    [Fact, Upstream("mysql8/M8BankTest.java#test14")]
    public async Task Test14()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards, q => q.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime))
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.Skip(1).Take(2).Any(c => c.Bank!.CreateTime >= Y2000))
            .Select(u => new Draft<string?, string>(u.Name, string.Join(",", u.BankCards!.Skip(1).Take(2).Select(c => c.Bank!.Name))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t4.`__joining3__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t5.`create_time` >= ? THEN ? ELSE NULL END)) <= 0) AS `__none2__`,GROUP_CONCAT(t5.`name` SEPARATOR ?) AS `__joining3__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1 WHERE t1.`type` = ?) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("2000-01-01T00:00(LocalDateTime),1(Integer),,(String),储蓄卡(String),2(Long),3(Long),%小明%(String),true(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test15")]
    public async Task Test15()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Skip(1).Take(2).Any(c => c.Bank!.CreateTime >= Y2000))
            .Select(u => new Draft<string?, string>(u.Name,
                string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Skip(1).Take(2).Select(c => c.Bank!.Name))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t4.`__joining3__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t5.`create_time` >= ? THEN ? ELSE NULL END)) <= 0) AS `__none2__`,GROUP_CONCAT(t5.`name` SEPARATOR ?) AS `__joining3__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1 WHERE t1.`type` = ?) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("2000-01-01T00:00(LocalDateTime),1(Integer),,(String),储蓄卡(String),2(Long),3(Long),%小明%(String),true(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test16")]
    public async Task Test16()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Take(3).Any(c => c.Bank!.CreateTime >= Y2000))
            .Select(u => new Draft<string?, string>(u.Name,
                string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Take(3).Select(c => c.Bank!.Name))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t4.`__joining3__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t5.`create_time` >= ? THEN ? ELSE NULL END)) <= 0) AS `__none2__`,GROUP_CONCAT(t5.`name` SEPARATOR ?) AS `__joining3__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1 WHERE t1.`type` = ?) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("2000-01-01T00:00(LocalDateTime),1(Integer),,(String),储蓄卡(String),1(Long),3(Long),%小明%(String),true(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test17")]
    public async Task Test17()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => u.Name!.Contains("小明") && !u.BankCards!.OrderBy(c => c.OpenTime).Take(3).Any(c => c.Bank!.CreateTime >= Y2000 && c.Type == "储蓄卡"))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t3.`uid` AS `__group_key1__`,(COUNT(?) <= 0) AS `__none2__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 INNER JOIN `t_bank` t5 ON t5.`id` = t3.`bank_id` WHERE t5.`create_time` >= ? AND t3.`type` = ? GROUP BY t3.`uid`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ? AND IFNULL(t4.`__none2__`,?) = ?");
        listen.AssertParams("1(Integer),1(Long),3(Long),2000-01-01T00:00(LocalDateTime),储蓄卡(String),%小明%(String),true(Boolean),true(Boolean)");
    }
}
