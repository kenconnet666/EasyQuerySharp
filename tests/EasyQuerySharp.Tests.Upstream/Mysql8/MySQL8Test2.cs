using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/MySQL8Test2.java（投影成 Part 后再连接、导航判空、值过滤是否传播到导航、导航的组内排序、分组拼接）。
/// Java 的 NotNullOrEmptyValueFilter.DEFAULT 对应 <c>UseValueFilter(ValueFilter.NotNullOrBlank)</c>，
/// DEFAULT_PROPAGATION_SUPPORTS 对应 <c>UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true)</c>。
/// </summary>
public sealed class MySQL8Test2(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // Java 的 Part 列与银行卡类型直接比较（Long 与 String）；C# 先转成 object 再比较，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test2.java#test1")]
    public async Task Test1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long>(b, b.BankCards!.Count()))
            .LeftJoin<SysBankCard>((p, c) => p.Entity.Id == c.BankId)
            .Where((p, c) => (object)p.Value1 == (object?)c.Type && p.Entity.BankCards!.Any())
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`id`,t2.`name`,t2.`create_time`,t2.`__part__column1` AS `__part__column1` FROM (SELECT t.`id`,t.`name`,t.`create_time`,(SELECT COUNT(*) FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id`) AS `__part__column1` FROM `t_bank` t WHERE t.`name` LIKE ?) t2 LEFT JOIN `t_bank_card` t3 ON t2.`id` = t3.`bank_id` WHERE t2.`__part__column1` = t3.`type` AND EXISTS(SELECT 1 FROM `t_bank_card` t4 WHERE t4.`bank_id` = t2.`id` LIMIT 1)");
        listen.AssertParams("%银行%(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#test2")]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoinAll().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, long>(b, b.BankCards!.Count()))
            .LeftJoin<SysBankCard>((p, c) => p.Entity.Id == c.BankId)
            .Where((p, c) => (object)p.Value1 == (object?)c.Type && p.Entity.BankCards!.Any())
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t3.`id`,t3.`name`,t3.`create_time`,t3.`__part__column1` AS `__part__column1` FROM (SELECT t.`id`,t.`name`,t.`create_time`,IFNULL(t2.`__count2__`,0) AS `__part__column1` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE ?) t3 LEFT JOIN `t_bank_card` t4 ON t3.`id` = t4.`bank_id` LEFT JOIN (SELECT t5.`bank_id` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__` FROM `t_bank_card` t5 GROUP BY t5.`bank_id`) t6 ON t6.`__group_key1__` = t3.`id` WHERE t3.`__part__column1` = t4.`type` AND IFNULL(t6.`__any2__`,?) = ?");
        listen.AssertParams("%银行%(String),false(Boolean),true(Boolean)");
    }

    private Query<Part<SysBank, long>> BankCardCounts(bool groupJoinAll)
    {
        var banks = Db.Query<SysBank>();
        if (groupJoinAll) banks = banks.GroupJoinAll();
        return banks.Where(b => b.Name!.Contains("银行")).Select(b => new Part<SysBank, long>(b, b.BankCards!.Count()));
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#test3")]
    public async Task Test3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().LeftJoin(BankCardCounts(false), (a, b) => a.BankId == b.Entity.Id)
            .Where((a, b) => b.Entity.BankCards!.Any() && (object)b.Value1 == (object?)a.Type).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t LEFT JOIN (SELECT t1.`id`,t1.`name`,t1.`create_time`,(SELECT COUNT(*) FROM `t_bank_card` t2 WHERE t2.`bank_id` = t1.`id`) AS `__part__column1` FROM `t_bank` t1 WHERE t1.`name` LIKE ?) t4 ON t.`bank_id` = t4.`id` WHERE EXISTS(SELECT 1 FROM `t_bank_card` t5 WHERE t5.`bank_id` = t4.`id` LIMIT 1) AND t4.`__part__column1` = t.`type`");
        listen.AssertParams("%银行%(String)");
    }

    // 外层开了 GroupJoinAll：连接来源上的导航也合并成分组表
    [Fact, Upstream("mysql8/MySQL8Test2.java#test4")]
    public async Task Test4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().GroupJoinAll().LeftJoin(BankCardCounts(false), (a, b) => a.BankId == b.Entity.Id)
            .Where((a, b) => b.Entity.BankCards!.Any() && (object)b.Value1 == (object?)a.Type).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t LEFT JOIN (SELECT t1.`id`,t1.`name`,t1.`create_time`,(SELECT COUNT(*) FROM `t_bank_card` t2 WHERE t2.`bank_id` = t1.`id`) AS `__part__column1` FROM `t_bank` t1 WHERE t1.`name` LIKE ?) t4 ON t.`bank_id` = t4.`id` LEFT JOIN (SELECT t5.`bank_id` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__` FROM `t_bank_card` t5 GROUP BY t5.`bank_id`) t6 ON t6.`__group_key1__` = t4.`id` WHERE IFNULL(t6.`__any2__`,?) = ? AND t4.`__part__column1` = t.`type`");
        listen.AssertParams("%银行%(String),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#test5")]
    public async Task Test5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().GroupJoinAll().LeftJoin(BankCardCounts(true), (a, b) => a.BankId == b.Entity.Id)
            .Where((a, b) => b.Entity.BankCards!.Any() && (object)b.Value1 == (object?)a.Type).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t LEFT JOIN (SELECT t1.`id`,t1.`name`,t1.`create_time`,IFNULL(t3.`__count2__`,0) AS `__part__column1` FROM `t_bank` t1 LEFT JOIN (SELECT t2.`bank_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_bank_card` t2 GROUP BY t2.`bank_id`) t3 ON t3.`__group_key1__` = t1.`id` WHERE t1.`name` LIKE ?) t5 ON t.`bank_id` = t5.`id` LEFT JOIN (SELECT t6.`bank_id` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__` FROM `t_bank_card` t6 GROUP BY t6.`bank_id`) t7 ON t7.`__group_key1__` = t5.`id` WHERE IFNULL(t7.`__any2__`,?) = ? AND t5.`__part__column1` = t.`type`");
        listen.AssertParams("%银行%(String),false(Boolean),true(Boolean)");
    }

    private const string JoinSql = "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` LEFT JOIN `t_sys_user` t2 ON t2.`id` = t.`uid` WHERE t1.`name` = ? AND t2.`name` = ?";

    [Fact, Upstream("mysql8/MySQL8Test2.java#testJoin")]
    public async Task TestJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().Where(c => c.Bank!.Name == "ICBC" && c.User!.Name == "小明").ToListAsync(Ct);
        listen.AssertSql(JoinSql);
        listen.AssertParams("ICBC(String),小明(String)");
    }

    // 先删一个不存在的银行，再查（删除不影响之后的查询）
    [Fact, Upstream("mysql8/MySQL8Test2.java#testDelete")]
    public async Task TestDelete()
    {
        await Db.Delete<SysBank>().Where(b => b.Name == "123").ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().Where(c => c.Bank!.Name == "ICBC" && c.User!.Name == "小明").ToListAsync(Ct);
        listen.AssertSql(JoinSql);
        listen.AssertParams("ICBC(String),小明(String)");
    }

    // 导航声明了 PartitionOrder.None（Java 的 IGNORE）：取第一个时组内不排序
    [Fact, Upstream("mysql8/MySQL8Test2.java#testOrderCaseWhen1")]
    public async Task TestOrderCaseWhen1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().OrderBy(u => u.BankCards!.First().Code).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid`)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) ORDER BY t3.`code` ASC");
        listen.AssertParams("1(Integer)");
    }

    // 导航上声明的默认排序（Java 的 orderByProps + NAVIGATE）
    [Fact, Upstream("mysql8/MySQL8Test2.java#testOrderCaseWhen2")]
    public async Task TestOrderCaseWhen2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().OrderBy(u => u.BankCard4s!.First().Code).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC,CASE WHEN t1.`code` IS NULL THEN 0 ELSE 1 END ASC,t1.`code` DESC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) ORDER BY t3.`code` ASC");
        listen.AssertParams("1(Integer)");
    }

    // 对一导航判空：比较目标表的主键
    [Fact, Upstream("mysql8/MySQL8Test2.java#testParentNull")]
    public async Task TestParentNull()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().Where(c => c.Bank == null && c.BankId == null).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t INNER JOIN `t_bank` t1 ON t1.`id` = t.`bank_id` WHERE t1.`id` IS NULL AND t.`bank_id` IS NULL");
    }

    // Java 用例没有断言：CASE WHEN 里用分组合并的聚合
    [Fact, Upstream("mysql8/MySQL8Test2.java#abc")]
    public async Task Abc()
    {
        await Db.Query<SysBank>().GroupJoinAll()
            .Where(b => (!b.BankCards!.Any() ? null : 123m / b.BankCards!.Count()) == 123m).ToListAsync(Ct);
    }

    // 计数查询去掉了投影，主表仍带别名 t；总数只有 3 条，数据查询的 LIMIT 收到 3
    [Fact, Upstream("mysql8/MySQL8Test2.java#testPageSelectSubQueryCount")]
    public async Task TestPageSelectSubQueryCount()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoin(b => b.BankCards).Select(b => new Draft<string?, long>(b.Id, b.BankCards!.Count())).ToPageAsync(1, 10, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `t_bank` t");
        listen.AssertSql(1, "SELECT t.`id` AS `value1`,IFNULL(t2.`__count2__`,0) AS `value2` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` LIMIT 3");
    }

    // 默认的值过滤只管本查询的条件，导航里的条件照写
    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure1")]
    public async Task TestFilterConfigure1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(b => b.Name == "" && b.BankCards!.Any(s => s.Type == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id` AND t1.`type` = ? LIMIT 1)");
        listen.AssertParams("(String)");
    }

    // 传播到导航：Any 里空值的条件也去掉
    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure2")]
    public async Task TestFilterConfigure2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).Where(b => b.Name == "" && b.BankCards!.Any(s => s.Type == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id` LIMIT 1)");
    }

    // 分组合并时条件被去掉，计数仍计 1
    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure3")]
    public async Task TestFilterConfigure3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Any(s => s.Type == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure3_1")]
    public async Task TestFilterConfigure3_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Any(s => s.Type == "" && s.Code == "123") && b.BankCards!.Any(s => s.Type == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,(COUNT((CASE WHEN t1.`code` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,(COUNT(?) > 0) AS `__any3__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__any3__`,?) = ?");
        listen.AssertParams("123(String),1(Integer),1(Integer),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure4")]
    public async Task TestFilterConfigure4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Any(s => s.Type == "")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),(String),false(Boolean),true(Boolean)");
    }

    // 与空值比较的条件整个去掉，取元素的 JOIN 也不再需要；Java 构造时已为导航建表，主表带别名 t
    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure5")]
    public async Task TestFilterConfigure5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.OrderBy(s => s.Type).First().Type == "").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure6")]
    public async Task TestFilterConfigure6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Where(x => x.Id == "").OrderBy(s => s.Type).First().Type == "").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure7")]
    public async Task TestFilterConfigure7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Where(x => x.Id == "1").OrderBy(s => s.Type).First().Type == "").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t");
    }

    // 默认的值过滤不管导航链上的 Where
    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure8")]
    public async Task TestFilterConfigure8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Where(x => x.Id == "").OrderBy(s => s.Type).First().Type == "1").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`bank_id` ORDER BY t1.`type` ASC)) AS `__row__` FROM `t_bank_card` t1 WHERE t1.`id` = ?) t3 ON (t3.`bank_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`type` = ?");
        listen.AssertParams("(String),1(Integer),1(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure9")]
    public async Task TestFilterConfigure9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(b => b.Name == "" && b.BankCards!.Where(x => x.Id == "").OrderBy(s => s.Type).First().Type == "1").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`bank_id` ORDER BY t1.`type` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`bank_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`type` = ?");
        listen.AssertParams("1(Integer),1(String)");
    }

    // 传播的值过滤同样作用于条件里手写的子查询
    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure10")]
    public async Task TestFilterConfigure10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true)
            .Where(b => b.Name == "" && Sql.Exists(Db.Query<SysBankCard>().Where(c => c.BankId == b.Id && c.Type == ""))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id`)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testFilterConfigure11")]
    public async Task TestFilterConfigure11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(b => b.Name == "" && Sql.Exists(Db.Query<SysBankCard>().Where(c => c.BankId == b.Id && c.Type == ""))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE EXISTS(SELECT 1 FROM `t_bank_card` t1 WHERE t1.`bank_id` = t.`id` AND t1.`type` = ?)");
        listen.AssertParams("(String)");
    }

    // 分组内去重拼接（Java 的 joining(",", true)）
    [Fact, Upstream("mysql8/MySQL8Test2.java#testGroupConcat1")]
    public void TestGroupConcat1()
    {
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().GroupBy(b => b.Id)
                .Select(g => new Draft<string?, string, string>(g.Key, g.Distinct().StringJoin(x => x.Title, ","), g.Distinct().StringJoin(x => x.Content, ",")))
                .ToStatement(),
            "SELECT t.`id` AS `value1`,GROUP_CONCAT(DISTINCT t.`title` SEPARATOR ?) AS `value2`,GROUP_CONCAT(DISTINCT t.`content` SEPARATOR ?) AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            ",(String),,(String),false(Boolean)");
    }

    // 原生片段写聚合
    [Fact, Upstream("mysql8/MySQL8Test2.java#testGroupConcat2")]
    public void TestGroupConcat2()
    {
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().GroupBy(b => b.Id)
                .Select(g => new Draft<string?, string>(g.Key, Sql.Raw<string>("GROUP_CONCAT(DISTINCT {0} SEPARATOR {1})", g.Column(x => x.Content), ",")))
                .ToStatement(),
            "SELECT t.`id` AS `value1`,GROUP_CONCAT(DISTINCT t.`content` SEPARATOR ?) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? GROUP BY t.`id`",
            ",(String),false(Boolean)");
    }

    // Java 用例没有断言：导航级条件 + 两个 Any 共用分组表
    [Fact, Upstream("mysql8/MySQL8Test2.java#testAA")]
    public async Task TestAA()
    {
        await Db.Query<SysUser>().GroupJoinAll().GroupJoin(u => u.BankCards, q => q.Where(x => x.Code == "1"))
            .Where(u => u.BankCards!.Any(b => b.Type == "11") && u.BankCards!.Any(b => b.Type == "22")).ToListAsync(Ct);
    }

    // 两层分组合并：市的分组表里再按区分组；两个求和共用
    [Fact, Upstream("mysql8/MySQL8Test2.java#testSumCount")]
    public async Task TestSumCount()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(m => m.Name.LikeContains("浙江"))
            .Select(m => new Draft<long, long>(
                m.Cities!.Sum(c => c.Areas!.Where(a => a.Id.LikeContains("123") && a.Name.LikeContains("绍兴")).Count()),
                m.Cities!.Sum(c => c.Areas!.Where(a => a.Id.LikeContains("123") && a.Name.LikeContains("绍兴1")).Count())))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL(t2.`__sum2__`,0) AS `value1`,IFNULL(t2.`__sum3__`,0) AS `value2` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,SUM(IFNULL(t4.`__count2__`,0)) AS `__sum2__`,SUM(IFNULL(t4.`__count3__`,0)) AS `__sum3__` FROM `m8_city` t1 LEFT JOIN (SELECT t3.`cid` AS `__group_key1__`,COUNT((CASE WHEN t3.`name` LIKE CONCAT('%',?,'%') THEN ? ELSE NULL END)) AS `__count2__`,COUNT((CASE WHEN t3.`name` LIKE CONCAT('%',?,'%') THEN ? ELSE NULL END)) AS `__count3__` FROM `m8_area` t3 WHERE t3.`id` LIKE CONCAT('%',?,'%') GROUP BY t3.`cid`) t4 ON t4.`__group_key1__` = t1.`id` GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE CONCAT('%',?,'%')");
        listen.AssertParams("绍兴(String),1(Integer),绍兴1(String),1(Integer),123(String),浙江(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testMerge")]
    public async Task TestMerge()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(m => m.Name.LikeContains("浙江"))
            .Select(m => new Draft<long, long>(
                m.Cities!.Where(c => c.Name!.Contains("绍兴") && c.Name.Contains("绍兴1")).Count(),
                m.Cities!.Where(c => c.Name!.Contains("绍兴") && c.Name.Contains("绍兴2")).Count()))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT IFNULL(t2.`__count2__`,0) AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,COUNT((CASE WHEN t1.`name` LIKE ? THEN ? ELSE NULL END)) AS `__count2__`,COUNT((CASE WHEN t1.`name` LIKE ? THEN ? ELSE NULL END)) AS `__count3__` FROM `m8_city` t1 WHERE t1.`name` LIKE ? GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE CONCAT('%',?,'%')");
        listen.AssertParams("%绍兴1%(String),1(Integer),%绍兴2%(String),1(Integer),%绍兴%(String),浙江(String)");
    }

    private const string CityCountJoin = "LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `m8_city` t1 WHERE t1.`name` IS NOT NULL AND t1.`id` IS NOT NULL GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id`";

    // Java 的 valueOf(() -> 条件) 投影成布尔值，C# 直接投影比较表达式；Java 的字符串 id 直接与计数比较，C# 用 (long)(object) 只改类型
    [Fact, Upstream("mysql8/MySQL8Test2.java#testMerge2")]
    public async Task TestMerge2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(m => m.Name.LikeContains("浙江") && (long)(object)m.Id! <= m.Cities!.Where(c => c.Name != null && c.Id != null).Count())
            .Select(m => new Draft<bool>((long)(object)m.Id! <= m.Cities!.Where(c => c.Name != null && c.Id != null).Count()))
            .ToPageAsync(1, 2, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `m8_province` t " + CityCountJoin + " WHERE t.`name` LIKE CONCAT('%',?,'%') AND  t.`id` <= IFNULL(t2.`__count2__`,0)");
        listen.AssertParams(0, "1(Integer),浙江(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test2.java#testMerge3")]
    public async Task TestMerge3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(m => m.Name.LikeContains("浙江") && (long)(object)m.Id! <= m.Cities!.Where(c => c.Name != null && c.Id != null).Count())
            .Select(m => new Draft<string?>(m.Name))
            .ToPageAsync(1, 2, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `m8_province` t " + CityCountJoin + " WHERE t.`name` LIKE CONCAT('%',?,'%') AND  t.`id` <= IFNULL(t2.`__count2__`,0)");
        listen.AssertParams(0, "1(Integer),浙江(String)");
    }

    // 两个计数共有 id 非空：提到 WHERE
    [Fact, Upstream("mysql8/MySQL8Test2.java#testMerge5")]
    public async Task TestMerge5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(m => m.Name.LikeContains("浙江")
                        && (long)(object)m.Id! <= m.Cities!.Where(c => c.Name != null && c.Id != null).Count()
                        && (long)(object)m.Id! >= m.Cities!.Where(c => c.Name == null && c.Id != null).Count())
            .Select(m => new Draft<string?>(m.Name))
            .ToPageAsync(1, 2, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,COUNT((CASE WHEN t1.`name` IS NOT NULL THEN ? ELSE NULL END)) AS `__count2__`,COUNT((CASE WHEN t1.`name` IS NULL THEN ? ELSE NULL END)) AS `__count3__` FROM `m8_city` t1 WHERE t1.`id` IS NOT NULL GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE CONCAT('%',?,'%') AND  t.`id` <= IFNULL(t2.`__count2__`,0) AND  t.`id` >= IFNULL(t2.`__count3__`,0)");
        listen.AssertParams(0, "1(Integer),1(Integer),浙江(String)");
    }

    // 投影里的比较（Java 的 valueOf）：分页先计数，再取这一页（总数只有 1 时 LIMIT 1）
    [Fact, Upstream("mysql8/MySQL8Test2.java#testMerge4")]
    public async Task TestMerge4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll()
            .Where(m => m.Name.LikeContains("浙江"))
            .Select(m => new Draft<bool>((long)(object)m.Id! <= m.Cities!.Where(c => c.Name != null && c.Id != null).Count()))
            .ToPageAsync(1, 2, Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `m8_province` t WHERE t.`name` LIKE CONCAT('%',?,'%')");
        listen.AssertParams(0, "浙江(String)");
        listen.AssertSql(1, "SELECT ( t.`id` <= IFNULL(t2.`__count2__`,0)) AS `value1` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `m8_city` t1 WHERE t1.`name` IS NOT NULL AND t1.`id` IS NOT NULL GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE CONCAT('%',?,'%') LIMIT 1");
        listen.AssertParams(1, "1(Integer),浙江(String)");
    }

    // 视图实体上的树查询（Java 只执行不断言）
    [Fact, Upstream("mysql8/MySQL8Test2.java#cteViewTree1")]
    public async Task CteViewTree1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TreeC>().AsTreeCte().ToListAsync(Ct);
        // 视图的 CTE 在递归 CTE 之前定义（递归部分要引用它）
        Assert.StartsWith("WITH RECURSIVE `m8_treec` AS (", listen.Last.Statement.Text, StringComparison.Ordinal);
        Assert.Contains(",`as_tree_cte` AS ( (", listen.Last.Statement.Text, StringComparison.Ordinal);
    }

    // 投影出的查询按指定的键列成树
    [Fact, Upstream("mysql8/MySQL8Test2.java#cteViewTree2")]
    public async Task CteViewTree2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<TreeA>().LeftJoin<TreeB>((a, b) => a.Id == b.Aid)
            .Select((a, b) => new TreeC { Id = a.Id, Name = a.Name, Pid = b.Aid })
            .AsTreeCte(s => s.Id, s => s.Pid).ToListAsync(Ct);
        Assert.StartsWith("WITH RECURSIVE `as_tree_cte` AS (", listen.Last.Statement.Text, StringComparison.Ordinal);
    }
}
