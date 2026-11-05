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

    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject1")]
    public async Task WhereObject1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().FilterBy(new BankCardQueryDTO { BankName = "工商银行" }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%工商银行%(String)");
    }

    [Fact, Upstream("mysql8/M8QueryTest.java#whereObject3")]
    public async Task WhereObject3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().FilterBy(new SysUserQueryDTO { BankCardCode = "123", BankCardType = "储蓄卡", BankCardBankNames = ["工商银行", "建设银行"] }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 INNER JOIN `t_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`uid` = t.`id` AND t1.`code` LIKE ? AND t1.`type` LIKE ? AND t2.`name` IN (?,?) LIMIT 1)");
        listen.AssertParams("%123%(String),%储蓄卡%(String),工商银行(String),建设银行(String)");
    }
}
