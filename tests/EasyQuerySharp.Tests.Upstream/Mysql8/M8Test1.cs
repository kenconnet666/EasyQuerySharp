namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/M8Test1.java（实体加导航列的 Part 投影、多条件组合、集合导航的 Any、没有主键的表建删）。
/// </summary>
public sealed class M8Test1(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // Java 的 selectIgnores(id)：Sql.Without(实体, 排除的列)，读出后 Id 保持默认值
    [Fact, Upstream("mysql8/M8Test1.java#testQuery")]
    public async Task TestQuery()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysBankCard>().Where(c => c.Id != null).Select(c => new Part<SysBankCard, string?>(Sql.Without(c, c.Id), c.Bank!.Name)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time`,t1.`name` AS `__part__column1` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t.`id` IS NOT NULL");
        Assert.All(list, p => Assert.Null(p.Entity!.Id));
    }

    // 整个实体再加一列导航上的列：Part 投影
    [Fact, Upstream("mysql8/M8Test1.java#testQuery2")]
    public async Task TestQuery2()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysBankCard>().Where(c => c.Id != null).Select(c => new Part<SysBankCard, string?>(c, c.Bank!.Name)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time`,t1.`name` AS `__part__column1` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t.`id` IS NOT NULL");
        Assert.All(list, p => Assert.NotNull(p.Entity));
    }

    // Java 的 like(条件, 值)：条件为真才加，C# 的 WhereIf
    [Fact, Upstream("mysql8/M8Test1.java#userTest")]
    public async Task UserTest()
    {
        const string userName = "小明";
        int[] ages = [18, 19, 20];
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserTest>().Where(u => u.Id >= 100L && (u.Sex == 1 || u.Sex == 2) && ages.Contains(u.Age!.Value))
            .WhereIf(!string.IsNullOrWhiteSpace(userName), u => u.UserName!.Contains(userName)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`sex`,`age`,`user_name` FROM `m8_user_test` WHERE `id` >= ? AND (`sex` = ? OR `sex` = ?) AND `age` IN (?,?,?) AND `user_name` LIKE ?");
        listen.AssertParams("100(Long),1(Integer),2(Integer),18(Integer),19(Integer),20(Integer),%小明%(String)");
    }

    // Java 用例没有断言：集合导航上的 Any
    [Fact, Upstream("mysql8/M8Test1.java#testMany")]
    public async Task TestMany() => await Db.Query<M8User2>().Where(m => m.Books!.Any(x => x.BookPrice > 1m)).ToListAsync(Ct);

    // Java 的 syncTable / dropTable：没有主键的表同样能建、删
    [Fact, Upstream("mysql8/M8Test1.java#testNoKeyCodeFirst")]
    public async Task TestNoKeyCodeFirst()
    {
        await Db.CodeFirst.CreateTablesAsync([typeof(TableNoKey)], Ct);
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(TableNoKey)], Ct);
        await Db.CodeFirst.CreateTablesAsync([typeof(TableNoKey)], Ct);
        Assert.Empty(await Db.Query<TableNoKey>().ToListAsync(Ct));
    }

    // 分组结果投影成 DTO，DTO 上声明了导航：外层按导航过滤时隐式 JOIN（对应 Java 的 supportNonEntity 导航）
    [Fact, Upstream("mysql8/M8Test1.java#test23")]
    public async Task Test23()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().GroupBy(c => c.Uid).Select(g => new BankCardGroupBO { Uid = g.Key, Count = g.Count() })
            .Where(b => Sql.LikeContains(b.User!.Name, "123")).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`uid` AS `uid`,t1.`count` AS `count` FROM (SELECT t.`uid` AS `uid`,COUNT(*) AS `count` FROM `t_bank_card` t GROUP BY t.`uid`) t1 LEFT JOIN `t_sys_user` t2 ON t2.`id` = t1.`uid` WHERE t2.`name` LIKE CONCAT('%',?,'%')");
        listen.AssertParams("123(String)");
    }

    // 本表键是字符串、目标键是整数：父键转成目标列类型时报错，不会悄悄查不到
    [Fact, Upstream("mysql8/M8Test1.java#testSelfTargetTypeDiff",
        Deviation = "Java 的异常来自 MySQL 驱动（Cannot determine value type from string 'u1'）；C# 在把父键转成目标列的类型时就抛出 FormatException")]
    public async Task TestSelfTargetTypeDiff() =>
        await Assert.ThrowsAnyAsync<FormatException>(() => Db.Query<SysUser>().Include(u => u.BankCardIntList).ToListAsync(Ct));

    /// <summary>对应 Java：mysql8/dto/BankCardSelectAutoInclude（分组结果上带出用户）。</summary>
    public sealed class BankCardSelectAutoInclude
    {
        public string? Uid { get; set; }

        public long Count { get; set; }

        public InternalUser? User { get; set; }

        /// <summary>SysUser 的投影。</summary>
        public sealed class InternalUser
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public string? Phone { get; set; }

            public int? Age { get; set; }
        }
    }

    private const string GroupedCards = "SELECT t1.`uid` AS `uid`,t1.`count` AS `count` FROM (SELECT t.`uid` AS `uid`,COUNT(*) AS `count` FROM `t_bank_card` t GROUP BY t.`uid`) t1";
    private const string UsersById = "SELECT t.`id`,t.`name`,t.`phone`,t.`age` FROM `t_sys_user` t WHERE t.`id` IN (?,?)";

    // 分组结果（DTO 上声明了导航）再按结构带出用户；外层先按导航过滤
    [Fact, Upstream("mysql8/M8Test1.java#test24")]
    public async Task Test24()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysBankCard>().GroupBy(c => c.Uid).Select(g => new BankCardGroupBO { Uid = g.Key, Count = g.Count() })
            .Where(b => b.User!.Name != null).SelectAutoInclude<BankCardSelectAutoInclude>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, GroupedCards + " LEFT JOIN `t_sys_user` t2 ON t2.`id` = t1.`uid` WHERE t2.`name` IS NOT NULL");
        listen.AssertSql(1, UsersById);
        listen.AssertParams(1, "u1(String),u2(String)");
        Assert.All(list, b => Assert.Equal(b.Uid, b.User!.Id));
    }

    [Fact, Upstream("mysql8/M8Test1.java#test25")]
    public async Task Test25()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().GroupBy(c => c.Uid).Select(g => new BankCardGroupBO { Uid = g.Key, Count = g.Count() })
            .SelectAutoInclude<BankCardSelectAutoInclude>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, GroupedCards);
        listen.AssertSql(1, UsersById);
        listen.AssertParams(1, "u1(String),u2(String)");
    }
}
