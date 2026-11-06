using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8QueryTest.java（条件对象里的导航路径）。</summary>
public sealed class M8QueryTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    /// <summary>对应 Java：mysql8/dto/BankCardQueryDTO。</summary>
    public sealed class BankCardQueryDTO
    {
        [Filter(Property = "Bank.Name")]
        public string? BankName { get; set; }
    }

    /// <summary>对应 Java：mysql8/dto/SysUserQueryDTO（集合导航上的多个条件合成一个 EXISTS）。</summary>
    public sealed class SysUserQueryDTO
    {
        [Filter(Property = "BankCards.Code")]
        public string? BankCardCode { get; set; }

        [Filter(Property = "BankCards.Type")]
        public string? BankCardType { get; set; }

        [Filter(FilterOp.In, Property = "BankCards.Bank.Name")]
        public List<string>? BankCardBankNames { get; set; }
    }

    /// <summary>对应 Java：mysql8/dto/SysUserQueryDTO2（开头 / 包含 / 结尾：值里的 % 与 _ 按字面匹配）。</summary>
    public sealed class SysUserQueryDTO2
    {
        [Filter(FilterOp.StartsWith, Property = "BankCards.Code")]
        public string? BankCardCode { get; set; }

        [Filter(FilterOp.In, Property = "BankCards.Bank.Name")]
        public List<string>? BankCardBankNames { get; set; }

        [Filter(FilterOp.Contains, Property = "BankCards.Type")]
        public string? BankCardType { get; set; }

        [Filter(FilterOp.EndsWith, Property = "BankCards.Type")]
        public string? BankCardType2 { get; set; }

        [Filter(FilterOp.Contains, Property = "BankCards.Type")]
        public string? BankCardType3 { get; set; }
    }

    private static SysUserQueryDTO Cards() => new() { BankCardCode = "123", BankCardType = "储蓄卡", BankCardBankNames = ["工商银行", "建设银行"] };

    private const string GroupedCards = "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,";

    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject1")]
    public async Task WhereObject1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().FilterBy(new BankCardQueryDTO { BankName = "工商银行" }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%工商银行%(String)");
    }

    // 全部集合导航合并成分组 JOIN：条件对象里同一集合导航的条件只有一个聚合用到，提到分组子查询的 WHERE
    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject2")]
    public async Task WhereObject2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().FilterBy(Cards()).ToListAsync(Ct);
        listen.AssertSql(GroupedCards + "(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t3 ON t3.`id` = t1.`bank_id` WHERE t1.`code` LIKE ? AND t1.`type` LIKE ? AND t3.`name` IN (?,?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),%123%(String),%储蓄卡%(String),工商银行(String),建设银行(String),false(Boolean),true(Boolean)");
    }

    // 再加一个手写的 Any：两个聚合各自写成 CASE WHEN
    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject4")]
    public async Task WhereObject4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll().FilterBy(Cards()).Where(u => u.BankCards!.Any(x => x.Code!.LikeContains("123"))).ToListAsync(Ct);
        listen.AssertSql(GroupedCards + "(COUNT((CASE WHEN t1.`code` LIKE ? AND t1.`type` LIKE ? AND t3.`name` IN (?,?) THEN ? ELSE NULL END)) > 0) AS `__any2__`,(COUNT((CASE WHEN t1.`code` LIKE CONCAT('%',?,'%') THEN ? ELSE NULL END)) > 0) AS `__any3__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t3 ON t3.`id` = t1.`bank_id` GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__any3__`,?) = ?");
        listen.AssertParams("%123%(String),%储蓄卡%(String),工商银行(String),建设银行(String),1(Integer),123(String),1(Integer),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    // 开头 / 包含 / 结尾：值在 SQL 里拼 %，值里有 % 或 _ 时转义并写 ESCAPE
    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject6")]
    public async Task WhereObject6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .FilterBy(new SysUserQueryDTO2 { BankCardCode = "123%", BankCardType = "储蓄_卡", BankCardType2 = "储蓄卡", BankCardType3 = "储蓄卡", BankCardBankNames = ["工商银行", "建设银行"] })
            .ToListAsync(Ct);
        listen.AssertSql(GroupedCards + "(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t3 ON t3.`id` = t1.`bank_id` WHERE t1.`code` LIKE CONCAT(?,'%') ESCAPE '\\\\' AND t3.`name` IN (?,?) AND t1.`type` LIKE CONCAT('%',?,'%') ESCAPE '\\\\' AND t1.`type` LIKE CONCAT('%',?) AND t1.`type` LIKE CONCAT('%',?,'%') GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),123\\%(String),工商银行(String),建设银行(String),储蓄\\_卡(String),储蓄卡(String),储蓄卡(String),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject3")]
    public async Task WhereObject3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().FilterBy(new SysUserQueryDTO { BankCardCode = "123", BankCardType = "储蓄卡", BankCardBankNames = ["工商银行", "建设银行"] }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 INNER JOIN `t_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`uid` = t.`id` AND t1.`code` LIKE ? AND t1.`type` LIKE ? AND t2.`name` IN (?,?) LIMIT 1)");
        listen.AssertParams("%123%(String),%储蓄卡%(String),工商银行(String),建设银行(String)");
    }

    // 全部集合导航合并成分组 JOIN：任一张卡满足（卡号、类型模糊匹配，所属银行在列表里），银行在分组子查询里 INNER JOIN
    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject5")]
    public async Task WhereObject5()
    {
        using var listen = SqlCapture.Listen();
        var names = new List<string> { "工商银行", "建设银行" };
        await Db.Query<SysUser>().GroupJoinAll()
            .Where(u => u.BankCards!.Any(s => s.Code!.Contains("123") && s.Type!.Contains("储蓄卡") && names.Contains(s.Bank!.Name!)))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t3 ON t3.`id` = t1.`bank_id` WHERE t1.`code` LIKE ? AND t1.`type` LIKE ? AND t3.`name` IN (?,?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),%123%(String),%储蓄卡%(String),工商银行(String),建设银行(String),false(Boolean),true(Boolean)");
    }
}
