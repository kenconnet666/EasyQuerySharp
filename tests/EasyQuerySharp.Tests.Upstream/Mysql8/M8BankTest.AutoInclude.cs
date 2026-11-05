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
}
