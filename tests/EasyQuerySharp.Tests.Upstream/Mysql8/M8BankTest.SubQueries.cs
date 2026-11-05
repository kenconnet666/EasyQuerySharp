using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// M8BankTest 里的计数与条件子查询：隐式（集合导航）与显式（Sql.SubQuery / Sql.Exists）两种写法，以及分组合并后的同样查询
public sealed partial class M8BankTest
{
    private static readonly DateTime Y2002 = new(2002, 1, 1);

    // 种子数据：工商银行 3 张卡（1 张储蓄卡、2 张信用卡），建设银行 2 张储蓄卡，招商银行没有卡
    private static void AssertCounts(IReadOnlyList<Part<SysBank, long>> rows)
    {
        Assert.NotEmpty(rows);
        Assert.Equal(3, rows.Single(r => r.Entity.Name == "工商银行").Value1);
        Assert.Equal(2, rows.Single(r => r.Entity.Name == "建设银行").Value1);
        Assert.Equal(0, rows.Single(r => r.Entity.Name == "招商银行").Value1);
    }

    private static void AssertTypeCounts(IReadOnlyList<Part<SysBank, long, long>> rows)
    {
        Assert.NotEmpty(rows);
        Assert.Equal((1L, 2L), rows.Where(r => r.Entity.Name == "工商银行").Select(r => (r.Value1, r.Value2)).Single());
        Assert.Equal((2L, 0L), rows.Where(r => r.Entity.Name == "建设银行").Select(r => (r.Value1, r.Value2)).Single());
        Assert.Equal((0L, 0L), rows.Where(r => r.Entity.Name == "招商银行").Select(r => (r.Value1, r.Value2)).Single());
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest")]
    public async Task TestQueryBankAndCardImplicitCountTest()
    {
        using var listen = SqlCapture.Listen();
        AssertCounts(await Db.Query<SysBank>().Where(b => b.Name!.Contains("银行")).Select(b => new Part<SysBank, long>(b, b.BankCards!.Count())).ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,(SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id`) AS `__part__column1` FROM `t_bank` t WHERE t.`name` LIKE ?");
        listen.AssertParams("%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardExplicitCountTest")]
    public async Task TestQueryBankAndCardExplicitCountTest()
    {
        using var listen = SqlCapture.Listen();
        AssertCounts(await Db.Query<SysBank>().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long>(b, Sql.SubQuery(Db.Query<SysBankCard>().Where(c => c.BankId == b.Id).Select(c => Sql.Count(c.Id)))))
            .ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,(SELECT COUNT(t1.`id`) FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id`) AS `__part__column1` FROM `t_bank` t WHERE t.`name` LIKE ?");
        listen.AssertParams("%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountToGroupTest")]
    public async Task TestQueryBankAndCardImplicitCountToGroupTest()
    {
        using var listen = SqlCapture.Listen();
        AssertCounts(await Db.Query<SysBank>().GroupJoin(b => b.BankCards).Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long>(b, b.BankCards!.Count())).ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,IFNULL(t2.`__count2__`,0) AS `__part__column1` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE ?");
        listen.AssertParams("%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest1")]
    public async Task TestQueryBankAndCardImplicitCountTest1()
    {
        using var listen = SqlCapture.Listen();
        AssertTypeCounts(await Db.Query<SysBank>().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long, long>(b, b.BankCards!.Where(c => c.Type == "储蓄卡").Count(), b.BankCards!.Where(c => c.Type == "信用卡").Count()))
            .ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,(SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id` AND t1.`type` = ?) AS `__part__column1`,(SELECT COUNT(*) FROM `t_bank_card` t2 WHERE t2.`bank_id` = t.`id` AND t2.`type` = ?) AS `__part__column2` FROM `t_bank` t WHERE t.`name` LIKE ?");
        listen.AssertParams("储蓄卡(String),信用卡(String),%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardExplicitCountTest1")]
    public async Task TestQueryBankAndCardExplicitCountTest1()
    {
        using var listen = SqlCapture.Listen();
        AssertTypeCounts(await Db.Query<SysBank>().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long, long>(b,
                Sql.SubQuery(Db.Query<SysBankCard>().Where(c => c.BankId == b.Id && c.Type == "储蓄卡").Select(c => Sql.Count(c.Id))),
                Sql.SubQuery(Db.Query<SysBankCard>().Where(c => c.BankId == b.Id && c.Type == "信用卡").Select(c => Sql.Count(c.Id)))))
            .ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,(SELECT COUNT(t1.`id`) FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id` AND t1.`type` = ?) AS `__part__column1`,(SELECT COUNT(t3.`id`) FROM `t_bank_card` t3 WHERE t3.`bank_id` = t.`id` AND t3.`type` = ?) AS `__part__column2` FROM `t_bank` t WHERE t.`name` LIKE ?");
        listen.AssertParams("储蓄卡(String),信用卡(String),%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountToGroupTest1")]
    public async Task TestQueryBankAndCardImplicitCountToGroupTest1()
    {
        using var listen = SqlCapture.Listen();
        AssertTypeCounts(await Db.Query<SysBank>().GroupJoin(b => b.BankCards).Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long, long>(b, b.BankCards!.Where(c => c.Type == "储蓄卡").Count(), b.BankCards!.Where(c => c.Type == "信用卡").Count()))
            .ToListAsync(Ct));
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,IFNULL(t2.`__count2__`,0) AS `__part__column1`,IFNULL(t2.`__count3__`,0) AS `__part__column2` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count2__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count3__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE ?");
        listen.AssertParams("储蓄卡(String),1(Integer),信用卡(String),1(Integer),%银行%(String)");
    }

    // Java 的 noneValue()：投影里的 !Any()
    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest2")]
    public async Task TestQueryBankAndCardImplicitCountTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, bool>(b, !b.BankCards!.OrderBy(c => c.OpenTime).Take(2).Where(c => c.OpenTime >= Y2002).Any()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,(NOT (EXISTS(SELECT 1 FROM (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time` FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id` ORDER BY t1.`open_time` ASC LIMIT 2) t2 WHERE t2.`open_time` >= ? LIMIT 1))) AS `__part__column1` FROM `t_bank` t WHERE t.`name` LIKE ?");
        listen.AssertParams("2002-01-01T00:00(LocalDateTime),%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest3")]
    public async Task TestQueryBankAndCardImplicitCountTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoin(b => b.BankCards).Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, bool>(b, !b.BankCards!.OrderBy(c => c.OpenTime).Take(2).Where(c => c.OpenTime >= Y2002).Any()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,IFNULL(t4.`__none2__`,?) AS `__part__column1` FROM `t_bank` t LEFT JOIN (SELECT t3.`bank_id` AS `__group_key1__`,(COUNT(?) <= 0) AS `__none2__` FROM (SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`,t2.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`bank_id` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t2 WHERE t2.`__row__` >= ? AND t2.`__row__` <= ?) t3 WHERE t3.`open_time` >= ? GROUP BY t3.`bank_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`name` LIKE ?");
        listen.AssertParams("true(Boolean),1(Integer),1(Long),2(Long),2002-01-01T00:00(LocalDateTime),%银行%(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest4")]
    public async Task TestQueryBankAndCardImplicitCountTest4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.Age == 18)
            .Select(u => new Draft<string?, string?, string?>(u.Name, u.BankCards!.OrderBy(c => c.OpenTime).First().Code, u.BankCards!.OrderBy(c => c.OpenTime).First().Bank!.Name))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t3.`code` AS `value2`,t4.`name` AS `value3` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) INNER JOIN `t_bank` t4 ON t4.`id` = t3.`bank_id` WHERE t.`age` = ?");
        listen.AssertParams("1(Integer),18(Integer)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery1")]
    public async Task WhereSubQuery1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards!.Any(c => c.Type == "储蓄卡")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ? LIMIT 1)");
        listen.AssertParams("储蓄卡(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery2")]
    public async Task WhereSubQuery2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards).Where(u => u.BankCards!.Any(c => c.Type == "储蓄卡")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),储蓄卡(String),false(Boolean),true(Boolean)");
    }

    // 导航上配的值过滤：值为 null 的区间下界被去掉（主查询的值过滤管不到导航里，见 whereSubQuery2_2）
    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery2_1")]
    public async Task WhereSubQuery2_1()
    {
        string? left = null, right = "2";
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards, q => q.UseValueFilter(ValueFilter.NotNullOrBlank)).UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(u => u.BankCards!.Any(c => c.Type == "储蓄卡" && c.Code.RangeClosed(left, right))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? AND t1.`code` <= ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),储蓄卡(String),2(String),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery2_2")]
    public async Task WhereSubQuery2_2()
    {
        string? left = null, right = "2";
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards).UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(u => u.BankCards!.Any(c => c.Type == "储蓄卡" && c.Code.RangeClosed(left, right))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? AND (t1.`code` >= ? AND t1.`code` <= ?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),储蓄卡(String),null(null),2(String),false(Boolean),true(Boolean)");
    }

    // Java 把手写的 exists(subQueryable(…)) 也合并进了声明的分组；C# 的 Sql.Exists 照写成 EXISTS
    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery3", Deviation = "Java 把与导航同表的显式 EXISTS 也改写进分组合并；C# 只合并集合导航上的聚合，显式 Sql.Exists 照写成 EXISTS 子查询")]
    public async Task WhereSubQuery3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards)
            .Where(u => Sql.Exists(Db.Query<SysBankCard>().Where(c => c.Uid == u.Id && c.Type == "储蓄卡"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?)");
        listen.AssertParams("储蓄卡(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery4")]
    public async Task WhereSubQuery4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoin(u => u.BankCards).Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").Count() == 2L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) = ?");
        listen.AssertParams("1(Integer),储蓄卡(String),2(Long)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery5")]
    public async Task WhereSubQuery5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").Count() == 2L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE (SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?) = ?");
        listen.AssertParams("储蓄卡(String),2(Long)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery6"), Upstream("mysql8/M8BankTest.java#whereSubQuery6_1")]
    public async Task WhereSubQuery6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => Sql.SubQuery(Db.Query<SysBankCard>().Where(c => c.Uid == u.Id && c.Type == "储蓄卡").Select(c => Sql.Count())) == 2L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE (SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?) = ?");
        listen.AssertParams("储蓄卡(String),2(Long)");
    }

    // 常量写在左边
    [Fact, Upstream("mysql8/M8BankTest.java#whereSubQuery7")]
    public async Task WhereSubQuery7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => 2L == Sql.SubQuery(Db.Query<SysBankCard>().Where(c => c.Uid == u.Id && c.Type == "储蓄卡").Select(c => Sql.Count()))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE ? = (SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?)");
        listen.AssertParams("2(Long),储蓄卡(String)");
    }
}
