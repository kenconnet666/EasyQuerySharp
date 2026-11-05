using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// 按 DTO 结构自动带出（Java 的 selectAutoInclude，C# 的 SelectAutoInclude）
public sealed partial class M8BankTest
{
    /// <summary>对应 Java：mysql8/vo/SysUserDTO（用户、储蓄卡与卡所属的银行）。</summary>
    public sealed class SysUserDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Phone { get; set; }

        public int? Age { get; set; }

        public DateTime? CreateTime { get; set; }

        public List<InternalBankCards>? BankCards { get; set; }

        /// <summary>SysBank 的投影。</summary>
        public sealed class InternalBank
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public DateTime? CreateTime { get; set; }
        }

        /// <summary>SysBankCard 的投影：只带出储蓄卡（Java 的 EXTRA_AUTO_INCLUDE_CONFIGURE）。</summary>
        public sealed class InternalBankCards : IAutoIncludeConfigure<SysBankCard>
        {
            public string? Id { get; set; }

            public string? Uid { get; set; }

            public string? Code { get; set; }

            public string? Type { get; set; }

            public string? BankId { get; set; }

            public DateTime? OpenTime { get; set; }

            public InternalBank? Bank { get; set; }

            public static Query<SysBankCard> Configure(Query<SysBankCard> query) => query.Where(c => c.Type == "储蓄卡");
        }
    }

    // 用户 → 储蓄卡（带出查询带上条件）→ 卡所属的银行（再一次带出）
    [Fact, Upstream("mysql8/M8BankTest.java#testExtraFilter")]
    public async Task TestExtraFilter()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysUser>().SelectAutoInclude<SysUserDTO>().ToListAsync(Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t");
        listen.AssertSql(1, "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t WHERE t.`type` = ? AND t.`uid` IN (?,?)");
        listen.AssertParams(1, "储蓄卡(String),u1(String),u2(String)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE t.`id` IN (?,?)");
        listen.AssertParams(2, "1(String),2(String)");
        Assert.All(list.SelectMany(u => u.BankCards!), c => Assert.Equal(c.BankId, c.Bank!.Id));
    }

    /// <summary>对应 Java：mysql8/vo/SysUserDTO2（根查询合并卡的分组并多取卡数；卡上多取银行成立日期）。</summary>
    public sealed class SysUserDTO2 : IAutoIncludeConfigure<SysUser>
    {
        public static Query<SysUser> Configure(Query<SysUser> query) => query.GroupJoin(u => u.BankCards);

        private static Expression<Func<SysUser, int?>> CardCountSql => u => u.BankCards!.Count();

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Phone { get; set; }

        public int? Age { get; set; }

        public DateTime? CreateTime { get; set; }

        [MapExpression(nameof(CardCountSql))]
        public int? CardCount { get; set; }

        public List<InternalBankCards>? BankCards { get; set; }

        /// <summary>SysBank 的投影。</summary>
        public sealed class InternalBank
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public DateTime? CreateTime { get; set; }
        }

        /// <summary>SysBankCard 的投影：只带出储蓄卡，多取银行成立日期。</summary>
        public sealed class InternalBankCards : IAutoIncludeConfigure<SysBankCard>
        {
            public static Query<SysBankCard> Configure(Query<SysBankCard> query) => query.Where(c => c.Type == "储蓄卡");

            private static Expression<Func<SysBankCard, string?>> BankCreateDay => c => c.Bank!.CreateTime!.Value.ToString("yyyy-MM-dd");

            [MapExpression(nameof(BankCreateDay))]
            public string? BankNameOr123 { get; set; }

            public string? Id { get; set; }

            public string? Uid { get; set; }

            public string? Code { get; set; }

            public string? Type { get; set; }

            public string? BankId { get; set; }

            public DateTime? OpenTime { get; set; }

            public InternalBank? Bank { get; set; }
        }
    }

    // 额外的列：根查询写在最前面（分组合并的卡数），带出查询写在最后面（连银行取日期）
    [Fact, Upstream("mysql8/M8BankTest.java#testExtraFilter2")]
    public async Task TestExtraFilter2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().SelectAutoInclude<SysUserDTO2>().ToListAsync(Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT IFNULL(t2.`__count2__`,0) AS `card_count`,t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`");
        listen.AssertSql(1, "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time`,DATE_FORMAT(t1.`create_time`,'%Y-%m-%d') AS `bank_name_or123` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t.`type` = ? AND t.`uid` IN (?,?)");
        listen.AssertParams(1, "储蓄卡(String),u1(String),u2(String)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE t.`id` IN (?,?)");
        listen.AssertParams(2, "1(String),2(String)");
    }
}
