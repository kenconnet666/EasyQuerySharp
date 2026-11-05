namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8BankTest.java（银行、银行卡、用户的导航用例）。</summary>
public sealed partial class M8BankTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    /// <summary>MyUserVO：投影目标（Java 的 vo/MyUserVO 的标量部分）。</summary>
    public sealed class MyUserVO
    {
        public string? Vo1 { get; set; }

        public string? Vo2 { get; set; }

        public string? Vo3 { get; set; }

        public long? BankCardCount { get; set; }

        public List<CardVO>? Cards { get; set; }
    }

    /// <summary>MyUserVO.cards 的元素（Java 里是同名属性的 VO）。</summary>
    public sealed class CardVO
    {
        public string? Type { get; set; }

        public string? Code { get; set; }
    }

    [Fact, Upstream("mysql8/M8BankTest.java#includeSubQuery2")]
    public async Task IncludeSubQuery2()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysUser>()
            .Select(u => new MyUserVO
            {
                Vo1 = u.Name, Vo2 = u.Id, Vo3 = u.Phone,
                Cards = u.BankCards!.Select(c => new CardVO { Type = c.Code, Code = c.Type }).ToList(),
            })
            .ToListAsync(Ct);
        listen.AssertSql(0, "SELECT t.`name` AS `vo1`,t.`id` AS `vo2`,t.`phone` AS `vo3`,t.`id` AS `__relation__id` FROM `t_sys_user` t");
        listen.AssertSql(1, "SELECT t.`code` AS `type`,t.`type` AS `code`,t.`uid` AS `__relation__uid` FROM `t_bank_card` t WHERE t.`uid` IN (?,?)");
        listen.AssertParams(1, "u1(String),u2(String)");
        Assert.Equal(3, list.Single(x => x.Vo2 == "u1").Cards!.Count);
    }

    [Fact, Upstream("mysql8/M8BankTest.java#includeSubQuery3")]
    public async Task IncludeSubQuery3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Select(u => new MyUserVO
            {
                Vo1 = u.Name, Vo2 = u.Id, Vo3 = u.Phone,
                Cards = u.BankCards!.Where(c => c.Type == "储蓄卡").Select(c => new CardVO { Type = c.Code, Code = c.Bank!.Name }).ToList(),
            })
            .ToListAsync(Ct);
        listen.AssertSql(0, "SELECT t.`name` AS `vo1`,t.`id` AS `vo2`,t.`phone` AS `vo3`,t.`id` AS `__relation__id` FROM `t_sys_user` t");
        listen.AssertSql(1, "SELECT t.`code` AS `type`,t1.`name` AS `code`,t.`uid` AS `__relation__uid` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t.`type` = ? AND t.`uid` IN (?,?)");
        listen.AssertParams(1, "储蓄卡(String),u1(String),u2(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#includeSubQuery4")]
    public async Task IncludeSubQuery4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Select(u => new MyUserVO
            {
                Vo1 = u.Name, Vo2 = u.Id, Vo3 = u.Phone,
                Cards = u.BankCards!.Where(c => c.Type == "储蓄卡")
                    .Select(c => new CardVO { Type = c.Code, Code = c.Bank!.BankCards!.Max(x => x.OpenTime!.Value.ToString("yyyy-MM-dd")) })
                    .ToList(),
            })
            .ToListAsync(Ct);
        listen.AssertSql(1, "SELECT t.`code` AS `type`,(SELECT MAX(DATE_FORMAT(t2.`open_time`,'%Y-%m-%d')) FROM `t_bank_card` t2 WHERE t2.`bank_id` = t1.`id`) AS `code`,t.`uid` AS `__relation__uid` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t.`type` = ? AND t.`uid` IN (?,?)");
        listen.AssertParams(1, "储蓄卡(String),u1(String),u2(String)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#includeSubQuery5")]
    public async Task IncludeSubQuery5()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysUser>()
            .Select(u => new SysUser
            {
                Id = u.Id, Name = u.Name, Phone = u.Phone, Age = u.Age, CreateTime = u.CreateTime,
                BankCards = u.BankCards!.Select(c => new SysBankCard
                {
                    Id = c.Id, Uid = c.Uid, Code = c.Code, Type = c.Type, BankId = c.BankId, OpenTime = c.OpenTime, Bank = c.Bank,
                }).ToList(),
            })
            .ToListAsync(Ct);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t");
        listen.AssertSql(1, "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t WHERE t.`uid` IN (?,?)");
        listen.AssertParams(1, "u1(String),u2(String)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE t.`id` IN (?,?)");
        listen.AssertParams(2, "1(String),2(String)");
        Assert.All(list.SelectMany(u => u.BankCards!), c => Assert.NotNull(c.Bank));
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
