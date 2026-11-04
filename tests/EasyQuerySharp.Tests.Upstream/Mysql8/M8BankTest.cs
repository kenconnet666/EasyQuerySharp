using EasyQuerySharp.Tests.Upstream.Mysql8.Entities.Bank;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8BankTest.java（银行、银行卡、用户的导航用例）。</summary>
public sealed class M8BankTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    /// <summary>MyUserVO：投影目标（Java 的 vo/MyUserVO 的标量部分）。</summary>
    public sealed class MyUserVO
    {
        public string? Vo1 { get; set; }

        public string? Vo2 { get; set; }

        public string? Vo3 { get; set; }

        public long? BankCardCount { get; set; }
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test1")]
    public async Task Test1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().Where(c => c.User!.Name == "小明").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t LEFT JOIN `t_sys_user` t1 ON t1.`id` = t.`uid` WHERE t1.`name` = ?");
        listen.AssertParams("小明(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#test10", Deviation = "Java 的 filterConfigure(NotNullOrEmptyValueFilter) 在 C# 里用 WhereIf 表达，这里条件都有值，SQL 相同")]
    public async Task Test10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>()
            .LeftJoin<SysBank>((c, b) => c.BankId == b.Id)
            .Where((c, b) => c.User!.Name == "小明")
            .Select((c, b) => new Draft<string?, string?, string?>(c.Code, c.User!.Name, b.Name))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`code` AS `value1`,t2.`name` AS `value2`,t1.`name` AS `value3` FROM `t_bank_card` t LEFT JOIN `t_bank` t1 ON t.`bank_id` = t1.`id` LEFT JOIN `t_sys_user` t2 ON t2.`id` = t.`uid` WHERE t2.`name` = ?");
        listen.AssertParams("小明(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#fromSubQuery1")]
    public async Task FromSubQuery1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Where(u => u.Name!.Contains("123"))
            .Select(u => new Draft<string?, string?, string?>(u.Name, u.Id, u.Phone))
            .Where(u => u.Value3 == "1234567")
            .OrderBy(u => u.Value1)
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`value1` AS `value1`,t1.`value2` AS `value2`,t1.`value3` AS `value3` FROM (SELECT t.`name` AS `value1`,t.`id` AS `value2`,t.`phone` AS `value3` FROM `t_sys_user` t WHERE t.`name` LIKE ?) t1 WHERE t1.`value3` = ? ORDER BY t1.`value1` ASC");
        listen.AssertParams("%123%(String),1234567(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#fromSubQuery2")]
    public async Task FromSubQuery2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Where(u => u.Name!.Contains("123"))
            .Select(u => new MyUserVO { Vo1 = u.Name, Vo2 = u.Id, Vo3 = u.Phone, BankCardCount = u.BankCards!.Count() })
            .Where(u => u.BankCardCount > 1L)
            .OrderBy(u => u.BankCardCount)
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`vo1` AS `vo1`,t2.`vo2` AS `vo2`,t2.`vo3` AS `vo3`,t2.`bank_card_count` AS `bank_card_count` FROM (SELECT t.`name` AS `vo1`,t.`id` AS `vo2`,t.`phone` AS `vo3`,(SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`uid` = t.`id`) AS `bank_card_count` FROM `t_sys_user` t WHERE t.`name` LIKE ?) t2 WHERE t2.`bank_card_count` > ? ORDER BY t2.`bank_card_count` ASC");
        listen.AssertParams("%123%(String),1(Long)");
    }
}
