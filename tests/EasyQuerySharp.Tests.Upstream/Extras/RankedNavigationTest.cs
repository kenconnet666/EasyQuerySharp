using EasyQuerySharp.Metadata;
using SysBank = EasyQuerySharp.Tests.Upstream.Mysql8.SysBank;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 声明了排序与条数的对一导航（<c>[Navigate(OneToOne, OrderBy = …, Limit = 1)]</c>，Java 的 OneToOne + limit + orderByProps）在条件、投影、
/// 隐式子查询里都按"每个所属行取排序后的那一行"翻译（与 <c>BankCards.OrderBy(…).First()</c> 相同），不是普通的 LEFT JOIN。
/// </summary>
public sealed class RankedNavigationTest
{
    private static EasyDb Db => SqlOnly.Db("mysql");

    // 最早开户的那张卡：按开户时间编号的派生表，取第 1 行
    private static string FirstCard(string inner, string alias, string owner) =>
        $"LEFT JOIN (SELECT {inner}.`id`,{inner}.`uid`,{inner}.`code`,{inner}.`type`,{inner}.`bank_id`,{inner}.`open_time`,(ROW_NUMBER() OVER (PARTITION BY {inner}.`uid` ORDER BY {inner}.`open_time` ASC)) AS `__row__` FROM `t_bank_card` {inner}) {alias} ON ({alias}.`uid` = {owner}.`id` AND {alias}.`__row__` = ?)";

    [Fact]
    public void ProjectionTakesTheRankedRow() =>
        Assert.Equal("SELECT t.`id` AS `value1`,t3.`code` AS `value2` FROM `t_sys_user` t " + FirstCard("t1", "t3", "t"),
            Db.Query<SysUser>().Select(u => new Draft<string?, string?>(u.Id, u.FirstCard!.Code)).ToSql());

    [Fact]
    public void ImplicitSubQueriesJoinTheRankedRowInside() =>
        Assert.Equal("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t2 ON t2.`id` = t1.`uid` "
                     + FirstCard("t3", "t5", "t2") + " WHERE t1.`bank_id` = t.`id` AND t5.`type` = ? LIMIT 1)",
            Db.Query<SysBank>().Where(b => b.BankCards!.Any(c => c.User!.FirstCard!.Type == "x")).ToSql());

    // 与 null 比较看取到的那一行的主键，和普通的对一导航一样：条件、投影、隐式子查询
    [Fact]
    public void ComparesTheRankedRowWithNull()
    {
        Assert.Equal("SELECT t.`id` FROM `t_sys_user` t " + FirstCard("t1", "t3", "t") + " WHERE t3.`id` IS NOT NULL",
            Db.Query<SysUser>().Where(u => u.FirstCard != null).Select(u => u.Id).ToSql());
        Assert.Equal("SELECT t.`id` AS `value1`,(t3.`id` IS NULL) AS `value2` FROM `t_sys_user` t " + FirstCard("t1", "t3", "t"),
            Db.Query<SysUser>().Select(u => new Draft<string?, bool>(u.Id, u.FirstCard == null)).ToSql());
        Assert.Equal("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 LEFT JOIN `t_sys_user` t2 ON t2.`id` = t1.`uid` "
                     + FirstCard("t3", "t5", "t2") + " WHERE t1.`bank_id` = t.`id` AND t5.`id` IS NULL LIMIT 1)",
            Db.Query<SysBank>().Where(b => b.BankCards!.Any(c => c.User!.FirstCard == null)).ToSql());
    }

    // 导航声明了 OrderBy、没写 PartitionOrder：取那一行按声明的排序（Java 的默认 THROW 在这里报错）；写了 Key 才按主键
    [Fact]
    public void DeclaredOrderRanksTheRowByDefault()
    {
        Assert.Contains("ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` DESC)",
            Db.Query<RankedUser>().Select(u => u.LatestCard!.Code).ToSql());
        Assert.Contains("ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`id` ASC)",
            Db.Query<RankedUser>().Select(u => u.FirstCardById!.Code).ToSql());
    }

    [Table("t_sys_user")]
    public sealed class RankedUser
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Mysql8.SysBankCard.Uid)], OrderBy = "OpenTime desc", Limit = 1)]
        public Mysql8.SysBankCard? LatestCard { get; set; }

        [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Mysql8.SysBankCard.Uid)], OrderBy = "OpenTime desc", PartitionOrder = PartitionOrder.Key, Limit = 1)]
        public Mysql8.SysBankCard? FirstCardById { get; set; }
    }
}
