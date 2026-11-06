using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/MySQL8Test5.java（分组合并下的字符串聚合与组内排序、分页后投影、LIKE 转义、按偏移分批、受保护的拦截器）。
/// Java 的 joining(…) 对应 <c>string.Join(sep, nav.Select(…))</c>，elements(i, j) 对应 <c>Skip(i).Take(j - i + 1)</c>。
/// </summary>
public sealed class MySQL8Test5(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit2")]
    public async Task TestManyLimit2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll()
            .Select(m => new Draft<string?, string>(m.Id, string.Join(",", m.Roles!.Where(r => r.Name.LikeStartsWith("管理员")).Select(s => s.Name)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t4.`__joining2__` AS `value2` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,GROUP_CONCAT(t1.`name` SEPARATOR ?) AS `__joining2__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`name` LIKE CONCAT(?,'%') GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),管理员(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit4")]
    public async Task TestManyLimit4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Select(u => new Draft<string?, string>(u.Id, string.Join(",", u.BankCards!.Where(c => c.Code.LikeStartsWith("小")).Select(s => s.Type)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t2.`__joining2__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,GROUP_CONCAT(t1.`type` SEPARATOR ?) AS `__joining2__` FROM `t_bank_card` t1 WHERE t1.`code` LIKE CONCAT(?,'%') GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),小(String)");
    }

    // 组内按银行的创建时间排序后取前两张：编号子查询里先连银行
    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit5")]
    public async Task TestManyLimit5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Select(u => new Draft<string?, string>(u.Id,
                string.Join(",", u.BankCards!.Where(c => c.Code.LikeStartsWith("小")).OrderBy(s => s.Bank!.CreateTime).Take(3).Select(s => s.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t5.`__joining2__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t4.`uid` AS `__group_key1__`,GROUP_CONCAT(t4.`type` SEPARATOR ?) AS `__joining2__` FROM (SELECT t3.`id` AS `id`,t3.`uid` AS `uid`,t3.`code` AS `code`,t3.`type` AS `type`,t3.`bank_id` AS `bank_id`,t3.`open_time` AS `open_time`,t3.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t2.`create_time` ASC)) AS `__row__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`code` LIKE CONCAT(?,'%')) t3 WHERE t3.`__row__` >= ? AND t3.`__row__` <= ?) t4 GROUP BY t4.`uid`) t5 ON t5.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),小(String),1(Long),3(Long)");
    }

    // 拼接的值经由取出的这几张卡再连银行
    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit6")]
    public async Task TestManyLimit6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Select(u => new Draft<string?, string>(u.Id,
                string.Join(",", u.BankCards!.Where(c => c.Code.LikeStartsWith("小")).OrderBy(s => s.Bank!.CreateTime).Take(3).Select(s => s.Bank!.Name))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t5.`__joining2__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t4.`uid` AS `__group_key1__`,GROUP_CONCAT(t6.`name` SEPARATOR ?) AS `__joining2__` FROM (SELECT t3.`id` AS `id`,t3.`uid` AS `uid`,t3.`code` AS `code`,t3.`type` AS `type`,t3.`bank_id` AS `bank_id`,t3.`open_time` AS `open_time`,t3.`__row__` AS `__row__` FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t2.`create_time` ASC)) AS `__row__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`code` LIKE CONCAT(?,'%')) t3 WHERE t3.`__row__` >= ? AND t3.`__row__` <= ?) t4 INNER JOIN `t_bank` t6 ON t6.`id` = t4.`bank_id` GROUP BY t4.`uid`) t5 ON t5.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),小(String),1(Long),3(Long)");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit7")]
    public async Task TestManyLimit7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupJoinAll()
            .Select(u => new Draft<string?, string>(u.Id, string.Join(",", u.BankCards!.Where(c => c.Code.LikeStartsWith("小")).Select(s => s.Bank!.Name)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t2.`__joining2__` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,GROUP_CONCAT(t3.`name` SEPARATOR ?) AS `__joining2__` FROM `t_bank_card` t1 INNER JOIN `t_bank` t3 ON t3.`id` = t1.`bank_id` WHERE t1.`code` LIKE CONCAT(?,'%') GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),小(String)");
    }

    // 组内排序写进 GROUP_CONCAT（Java 的 joining 默认分隔符是逗号）
    [Fact, Upstream("mysql8/MySQL8Test5.java#testJoining1")]
    public async Task TestJoining1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoinAll().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, string>(b, string.Join(",", b.BankCards!.OrderBy(o => o.OpenTime).ThenByDescending(o => o.Type).Select(s => s.Type))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,t2.`__joining2__` AS `__part__column1` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,GROUP_CONCAT(t1.`type` ORDER BY t1.`open_time` ASC,t1.`type` DESC SEPARATOR ?) AS `__joining2__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE ?");
        listen.AssertParams(",(String),%银行%(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#testJoining2")]
    public async Task TestJoining2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoinAll().Where(b => b.Name!.Contains("银行"))
            .Select(b => new Part<SysBank, string>(b, string.Join(",", b.BankCards!.OrderBy(o => o.OpenTime).ThenByDescending(o => o.Type).Select(s => s.Type).Distinct())))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time`,t2.`__joining2__` AS `__part__column1` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,GROUP_CONCAT(DISTINCT t1.`type` ORDER BY t1.`open_time` ASC,t1.`type` DESC SEPARATOR ?) AS `__joining2__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t.`name` LIKE ?");
        listen.AssertParams(",(String),%银行%(String)");
    }

    private const string PagedBanks = "SELECT t1.`id`,t1.`name`,t1.`create_time`,t3.`__joining2__` AS `__part__column1` FROM (SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t WHERE t.`name` LIKE ? LIMIT 2) t1 LEFT JOIN (SELECT t2.`bank_id` AS `__group_key1__`,GROUP_CONCAT(t2.`type` SEPARATOR ?) AS `__joining2__` FROM `t_bank_card` t2 ";

    // 先分页再投影（Java 的 toPageSelectResult）：投影里的分组合并只作用于这一页；传播的值过滤去掉了空值条件
    [Fact, Upstream("mysql8/MySQL8Test5.java#testToSelectPageResult")]
    public async Task TestToSelectPageResult()
    {
        string? type = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll().Where(b => b.Name!.Contains("银行"))
            .ToPageAsync(1, 2, b => new Part<SysBank, string>(b, string.Join(",", b.BankCards!.Where(s => s.Type == type).Select(s => s.Type))), Ct);
        listen.AssertSql(1, PagedBanks + "GROUP BY t2.`bank_id`) t3 ON t3.`__group_key1__` = t1.`id`");
        listen.AssertParams(1, "%银行%(String),,(String)");
    }

    // 没有值过滤时条件照写；C# 与值为 null 的变量相等比较按 C# 语义写 IS NULL
    [Fact, Upstream("mysql8/MySQL8Test5.java#testToSelectPageResult2", Deviation = "Java 的 eq(null) 写 = ?（参数 null）；C# 与值为 null 的变量比较按 C# 语义写 IS NULL")]
    public async Task TestToSelectPageResult2()
    {
        string? type = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoinAll().Where(b => b.Name!.Contains("银行"))
            .ToPageAsync(1, 2, b => new Part<SysBank, string>(b, string.Join(",", b.BankCards!.Where(s => s.Type == type).Select(s => s.Type))), Ct);
        listen.AssertSql(1, PagedBanks + "WHERE t2.`type` IS NULL GROUP BY t2.`bank_id`) t3 ON t3.`__group_key1__` = t1.`id`");
        listen.AssertParams(1, "%银行%(String),,(String)");
    }

    // Java 在条件里写 bankCards().filter(…) 给导航加固定条件：C# 的 FilterNavigation
    [Fact, Upstream("mysql8/MySQL8Test5.java#testToSelectPageResult3")]
    public async Task TestToSelectPageResult3()
    {
        var type = "123";
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoinAll().FilterNavigation(b => b.BankCards, t => t.Type == type).Where(b => b.Name!.Contains("银行"))
            .ToPageAsync(1, 2, b => new Part<SysBank, string>(b, string.Join(",", b.BankCards!.Select(s => s.Type))), Ct);
        listen.AssertSql(1, PagedBanks + "WHERE t2.`type` = ? GROUP BY t2.`bank_id`) t3 ON t3.`__group_key1__` = t1.`id`");
        listen.AssertParams(1, "%银行%(String),,(String),123(String)");
    }

    // 值里有 % 或 _ 时转义后写成 LIKE CONCAT(…) ESCAPE '\\'
    [Fact, Upstream("mysql8/MySQL8Test5.java#testLikeStartsWith")]
    public async Task TestLikeStartsWith()
    {
        const string id = "testLikeStartsWith";
        await Db.Delete<TableNoKey>().Where(t => t.Column1 == id).ExecuteAsync(Ct);
        try
        {
            await Db.Insert(new TableNoKey { Column1 = id, Column2 = "小_明%在哪里" }).ExecuteAsync(Ct);
            using var listen = SqlCapture.Listen();
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2.LikeStartsWith("小_明%")).ToListAsync(Ct));
            listen.AssertSql("SELECT `column1`,`column2`,`column3` FROM `no_key_table` WHERE `column2` LIKE CONCAT(?,'%') ESCAPE '\\\\'");
            listen.AssertParams("小\\_明\\%(String)");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2.LikeContains("小_明%")).ToListAsync(Ct));
            listen.AssertSql("SELECT `column1`,`column2`,`column3` FROM `no_key_table` WHERE `column2` LIKE CONCAT('%',?,'%') ESCAPE '\\\\'");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2.LikeEndsWith("小_明%在哪里")).ToListAsync(Ct));
            listen.AssertSql("SELECT `column1`,`column2`,`column3` FROM `no_key_table` WHERE `column2` LIKE CONCAT('%',?) ESCAPE '\\\\'");
            listen.AssertParams("小\\_明\\%在哪里(String)");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2.LikeStartsWith("小_明%") && t.Column2.LikeContains("小_明%")).ToListAsync(Ct));
            listen.AssertSql("SELECT `column1`,`column2`,`column3` FROM `no_key_table` WHERE `column2` LIKE CONCAT(?,'%') ESCAPE '\\\\' AND `column2` LIKE CONCAT('%',?,'%') ESCAPE '\\\\'");
            listen.AssertParams("小\\_明\\%(String),小\\_明\\%(String)");
        }
        finally
        {
            await Db.Delete<TableNoKey>().Where(t => t.Column1 == id).ExecuteAsync(Ct);
        }
    }

    // Java 用例没有断言：先条件再连接
    [Fact, Upstream("mysql8/MySQL8Test5.java#testWhereJoin")]
    public async Task TestWhereJoin()
    {
        await Db.Query<M8User>().Where(m => m.Name!.Contains("123")).LeftJoin<M8User>((m, m2) => m.Id == m2.Id).ToListAsync(Ct);
    }

    // Java 用例没有断言：连接后投影回主表、去重，再带出银行卡
    [Fact, Upstream("mysql8/MySQL8Test5.java#selectJoinInclude")]
    public async Task SelectJoinInclude()
    {
        await Db.Query<SysUser>().LeftJoin<SysBankCard>((u, c) => u.Id == c.Uid).Select((u, c) => u).Include(u => u.BankCards).Distinct().ToListAsync(Ct);
    }

    // 按偏移分批：每批处理完就把它们标记掉，下一批的偏移不前进
    [Fact, Upstream("mysql8/MySQL8Test5.java#testOffsetChunk")]
    public async Task TestOffsetChunk()
    {
        var seen = new List<int>();
        await Db.Query<OffsetChunkTest>().Where(o => o.Status == null).OrderBy(o => o.Seq).OffsetChunkAsync(10, chunk =>
        {
            foreach (var row in chunk)
            {
                Assert.Equal(seen.Count, row.Seq);
                seen.Add(row.Seq!.Value);
                Db.Update<OffsetChunkTest>().Set(o => o.Status, row.Id).WhereById(row.Id!).ExecuteAsync(Ct).GetAwaiter().GetResult();
            }
            return 0;
        }, cancellationToken: Ct);
        Assert.Equal(Enumerable.Range(0, 1000), seen);
    }

    private const string ProtectedUsers = "SELECT `id`,`name`,`age`,`create_time` FROM `m8_user`";

    // 受保护的拦截器：关闭全部关不掉，按名字关闭才去掉
    [Fact, Upstream("mysql8/MySQL8Test5.java#interceptorTest1")]
    public async Task InterceptorTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserProtect>().ToListAsync(Ct);
        listen.AssertSql(ProtectedUsers + " WHERE 1=1");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#interceptorTest2")]
    public async Task InterceptorTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserProtect>().DisableInterceptors().ToListAsync(Ct);
        listen.AssertSql(ProtectedUsers + " WHERE 1=1");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#interceptorTest3")]
    public async Task InterceptorTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserProtect>().EnableInterceptor(nameof(MyProtectInterceptor)).DisableInterceptors().ToListAsync(Ct);
        listen.AssertSql(ProtectedUsers + " WHERE 1=1");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#interceptorTest4")]
    public async Task InterceptorTest4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserProtect>().DisableInterceptor(nameof(MyProtectInterceptor)).ToListAsync(Ct);
        listen.AssertSql(ProtectedUsers);
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#interceptorTest5")]
    public async Task InterceptorTest5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserProtect>().DisableInterceptors().DisableInterceptor(nameof(MyProtectInterceptor)).ToListAsync(Ct);
        listen.AssertSql(ProtectedUsers);
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#interceptorTest6")]
    public async Task InterceptorTest6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserProtect>().EnableInterceptors().DisableInterceptor(nameof(MyProtectInterceptor)).ToListAsync(Ct);
        listen.AssertSql(ProtectedUsers);
    }

    // 多对多取前六个后拼接（Java 的 elements(0, 5) 含两端）：先按中间表的外键编号再截取
    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit1")]
    public async Task TestManyLimit1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll()
            .Select(m => new Draft<string?, string>(m.Id,
                string.Join(",", m.Roles!.Where(r => r.Name.LikeStartsWith("管理员")).OrderBy(s => s.Name).Take(6).Select(s => s.Name)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t6.`__joining2__` AS `value2` FROM `m8_user` t LEFT JOIN (SELECT t4.`__group_key1__`,GROUP_CONCAT(t4.`name` SEPARATOR ?) AS `__joining2__` FROM (SELECT t3.`id` AS `id`,t3.`name` AS `name`,t3.`create_time` AS `create_time`,t3.`__group_key1__` AS `__group_key1__` FROM (SELECT t1.`id`,t1.`name`,t1.`create_time`,t2.`user_id` AS `__group_key1__`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`name` LIKE CONCAT(?,'%')) t3 WHERE t3.`__row__` >= ? AND t3.`__row__` <= ?) t4 GROUP BY t4.`__group_key1__`) t6 ON t6.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),管理员(String),1(Long),6(Long)");
    }

    // 不截取时组内排序写进 GROUP_CONCAT，拼接的值再连到角色的 M8SaveA
    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit1_1")]
    public async Task TestManyLimit1_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll()
            .Select(m => new Draft<string?, string>(m.Id,
                string.Join(",", m.Roles!.Where(r => r.Name.LikeStartsWith("管理员")).OrderBy(s => s.Name).Select(s => s.M8SaveA!.Name)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t4.`__joining2__` AS `value2` FROM `m8_user` t LEFT JOIN (SELECT t2.`user_id` AS `__group_key1__`,GROUP_CONCAT(t5.`name` ORDER BY t1.`name` ASC SEPARATOR ?) AS `__joining2__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` LEFT JOIN `m8_save_a` t5 ON t5.`id` = t1.`id` WHERE t1.`name` LIKE CONCAT(?,'%') GROUP BY t2.`user_id`) t4 ON t4.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),管理员(String)");
    }

    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit1_2")]
    public async Task TestManyLimit1_2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll()
            .Select(m => new Draft<string?, string>(m.Id,
                string.Join(",", m.Roles!.Where(r => r.Name.LikeStartsWith("管理员")).OrderBy(s => s.Name).Take(6).Select(s => s.M8SaveA!.Name)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t6.`__joining2__` AS `value2` FROM `m8_user` t LEFT JOIN (SELECT t4.`__group_key1__`,GROUP_CONCAT(t7.`name` SEPARATOR ?) AS `__joining2__` FROM (SELECT t3.`id` AS `id`,t3.`name` AS `name`,t3.`create_time` AS `create_time`,t3.`__group_key1__` AS `__group_key1__` FROM (SELECT t1.`id`,t1.`name`,t1.`create_time`,t2.`user_id` AS `__group_key1__`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`name` LIKE CONCAT(?,'%')) t3 WHERE t3.`__row__` >= ? AND t3.`__row__` <= ?) t4 LEFT JOIN `m8_save_a` t7 ON t7.`id` = t4.`id` GROUP BY t4.`__group_key1__`) t6 ON t6.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),管理员(String),1(Long),6(Long)");
    }

    // 两个相同的截取合并成一个分组
    [Fact, Upstream("mysql8/MySQL8Test5.java#testManyLimit1_3")]
    public async Task TestManyLimit1_3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().GroupJoinAll()
            .Select(m => new Draft<string?, string, string>(m.Id,
                string.Join(",", m.Roles!.Where(r => r.Name.LikeStartsWith("管理员")).OrderBy(s => s.Name).Take(6).Select(s => s.M8SaveA!.Name)),
                string.Join(",", m.Roles!.Where(r => r.Name.LikeStartsWith("管理员")).OrderBy(s => s.Name).Take(6).Select(s => s.M8SaveA!.Id)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t6.`__joining2__` AS `value2`,t6.`__joining3__` AS `value3` FROM `m8_user` t LEFT JOIN (SELECT t4.`__group_key1__`,GROUP_CONCAT(t7.`name` SEPARATOR ?) AS `__joining2__`,GROUP_CONCAT(t7.`id` SEPARATOR ?) AS `__joining3__` FROM (SELECT t3.`id` AS `id`,t3.`name` AS `name`,t3.`create_time` AS `create_time`,t3.`__group_key1__` AS `__group_key1__` FROM (SELECT t1.`id`,t1.`name`,t1.`create_time`,t2.`user_id` AS `__group_key1__`,(ROW_NUMBER() OVER (PARTITION BY t2.`user_id` ORDER BY t1.`name` ASC)) AS `__row__` FROM `m8_role` t1 INNER JOIN `m8_user_role` t2 ON t1.`id` = t2.`role_id` WHERE t1.`name` LIKE CONCAT(?,'%')) t3 WHERE t3.`__row__` >= ? AND t3.`__row__` <= ?) t4 LEFT JOIN `m8_save_a` t7 ON t7.`id` = t4.`id` GROUP BY t4.`__group_key1__`) t6 ON t6.`__group_key1__` = t.`id`");
        listen.AssertParams(",(String),,(String),管理员(String),1(Long),6(Long)");
    }

    // 逻辑删除由配置决定，实体上没有标注
    [Fact, Upstream("mysql8/MySQL8Test5.java#configurationLogicDelete")]
    public async Task ConfigurationLogicDelete()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MyConfigLogicDelete>().ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`name`,`deleted` FROM `my_config_logic` WHERE `deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    /// <summary>对应 Java：dto/SysDeptTreeResp（部门树，deep 与层级列同名）。</summary>
    public sealed class SysDeptTreeResp
    {
        public long? Id { get; set; }

        public long? Pid { get; set; }

        public string? Name { get; set; }

        public long? Deep { get; set; }

        public List<SysDeptTreeResp>? Children { get; set; }
    }

    // 往上找全部上级：每个起点各成一条到根的链，层级列 deep 填进 DTO
    [Fact, Upstream("mysql8/MySQL8Test5.java#testSysDeptCTE")]
    public async Task TestSysDeptCTE()
    {
        var tree = await Db.Query<SysDept>().Where(s => new[] { "abc-算法部", "abc-召回模型", "abc-渠道部" }.Contains(s.Name))
            .AsTreeCte(o => { o.Up = true; o.DeepColumnName = "deep"; })
            .OrderBy(s => s.Id)
            .Select<SysDeptTreeResp>().ToTreeAsync(Ct);
        static string Format(SysDeptTreeResp d) => $"SysDeptTreeResp(id={d.Id}, pid={d.Pid}, name={d.Name}, deep={d.Deep}, children=[{string.Join(", ", d.Children!.Select(Format))}])";
        Assert.Equal("[SysDeptTreeResp(id=2, pid=0, name=总部B, deep=2, children=[SysDeptTreeResp(id=21, pid=2, name=研发中心, deep=1, children=[SysDeptTreeResp(id=211, pid=21, name=abc-算法部, deep=0, children=[])])]), SysDeptTreeResp(id=2, pid=0, name=总部B, deep=2, children=[SysDeptTreeResp(id=22, pid=2, name=市场中心, deep=1, children=[SysDeptTreeResp(id=222, pid=22, name=abc-渠道部, deep=0, children=[])])]), SysDeptTreeResp(id=2, pid=0, name=总部B, deep=4, children=[SysDeptTreeResp(id=21, pid=2, name=研发中心, deep=3, children=[SysDeptTreeResp(id=211, pid=21, name=abc-算法部, deep=2, children=[SysDeptTreeResp(id=2111, pid=211, name=推荐算法, deep=1, children=[SysDeptTreeResp(id=21111, pid=2111, name=abc-召回模型, deep=0, children=[])])])])])]",
            "[" + string.Join(", ", tree.Select(Format)) + "]");
    }
}
