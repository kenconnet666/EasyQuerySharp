using EasyQuerySharp.Metadata;
using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/MySQL8Test3.java（多层分组合并、主表键条件下推进分组子查询、偏移类窗口函数、取第一张卡的导航）。
/// Java 的 flatElement() 写成 Any；ALL_SUB_QUERY_GROUP_JOIN 对应 GroupJoinAll()。
/// </summary>
public sealed class MySQL8Test3(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // Java 用例没有断言：值全为空时条件都去掉
    [Fact, Upstream("mysql8/MySQL8Test3.java#testDynamicJoin")]
    public async Task TestDynamicJoin()
    {
        string bankCardCode = "", bankCardType = "", bankName = "", userName = "", userPhone = "";
        await Db.Query<SysBankCard>().UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true)
            .Where(c => c.Code.LikeContains(bankCardCode) && c.Type.LikeContains(bankCardType) && c.Bank!.Name.LikeContains(bankName)
                        && c.User!.Name.LikeContains(userName) && c.User!.Phone.LikeContains(userPhone))
            .ToPageAsync(1, 2, Ct);
    }

    // Java 的 thisConfigure 把一组配置写成方法复用；C# 直接写成接收并返回查询的方法
    private static Query<T> ThisConfigure<T>(Query<T> query) =>
        query.UseValueFilter(ValueFilter.NotNullOrBlank, includeNavigations: true).GroupJoinAll();

    [Fact, Upstream("mysql8/MySQL8Test3.java#thisConfigureTest")]
    public async Task ThisConfigureTest()
    {
        using var listen = SqlCapture.Listen();
        await ThisConfigure(Db.Query<SysUser>()).Where(u => u.Name == "")
            .OrderBy(u => Sql.NullsLast(u.BankCards!.Where(x => x.Type == "123").Max(s => s.OpenTime)))
            .Select(u => new Draft<string?>(u.Name)).ToPageAsync(1, 20, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `t_sys_user` t");
        listen.AssertSql(1, "SELECT t.`name` AS `value1` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX(t1.`open_time`) AS `__max2__` FROM `t_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY CASE WHEN t2.`__max2__` IS NULL THEN 1 ELSE 0 END ASC,t2.`__max2__` ASC LIMIT 2");
        listen.AssertParams(1, "123(String)");
    }

    private const string ProvinceCityAreaSql = "SELECT t.`id`,t.`name` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_city` t1 LEFT JOIN (SELECT t3.`cid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_area` t3 WHERE t3.`name` = ? GROUP BY t3.`cid`) t4 ON t4.`__group_key1__` = t1.`id` WHERE IFNULL(t4.`__any2__`,?) = ? GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?";

    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testFlatElement"), Upstream("mysql8/MySQL8Test3.java#testFlatElement1")]
    public async Task TestFlatElement()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().GroupJoinAll().Where(m => m.Cities!.Any(c => c.Areas!.Any(a => a.Name == "123"))).ToListAsync(Ct);
        listen.AssertSql(ProvinceCityAreaSql);
        listen.AssertParams("1(Integer),1(Integer),123(String),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    // 不合并时是两层 EXISTS
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testFlatElement2")]
    public async Task TestFlatElement2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().Where(m => m.Cities!.Any(c => c.Areas!.Any(a => a.Name == "123"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_province` t WHERE EXISTS(SELECT 1 FROM `m8_city` t1 WHERE t1.`pid` = t.`id` AND EXISTS(SELECT 1 FROM `m8_area` t2 WHERE t2.`cid` = t1.`id` AND t2.`name` = ? LIMIT 1) LIMIT 1)");
        listen.AssertParams("123(String)");
    }

    // 三层分组合并：省 → 市 → 区 → 楼
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testFlatElement3")]
    public async Task TestFlatElement3()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<M8Province>().GroupJoinAll().Where(m => m.Cities!.Any(c => c.Areas!.Any(a => a.Builds!.Any(b => b.Name == "科创楼")))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_city` t1 LEFT JOIN (SELECT t3.`cid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_area` t3 LEFT JOIN (SELECT t5.`aid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_build` t5 WHERE t5.`name` = ? GROUP BY t5.`aid`) t6 ON t6.`__group_key1__` = t3.`id` WHERE IFNULL(t6.`__any2__`,?) = ? GROUP BY t3.`cid`) t4 ON t4.`__group_key1__` = t1.`id` WHERE IFNULL(t4.`__any2__`,?) = ? GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("1(Integer),1(Integer),1(Integer),科创楼(String),false(Boolean),true(Boolean),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
        Assert.NotEmpty(list);
    }

    // 主表主键的等值条件下推进分组子查询（先过滤再分组）
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testGroupJoin")]
    public async Task TestGroupJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().GroupJoinAll().Where(m => m.Cities!.Any(c => c.Name == "杭州") && m.Id == "p1").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_city` t1 WHERE t1.`name` = ? AND t1.`pid` = ? GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND t.`id` = ?");
        listen.AssertParams("1(Integer),杭州(String),p1(String),false(Boolean),true(Boolean),p1(String)");
    }

    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testGroupJoin2")]
    public async Task TestGroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        string[] ids = ["p1", "p2"];
        await Db.Query<M8Province>().GroupJoinAll().Where(m => m.Cities!.Any(c => c.Name == "杭州") && ids.Contains(m.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_city` t1 WHERE t1.`name` = ? AND t1.`pid` IN (?,?) GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND t.`id` IN (?,?)");
        listen.AssertParams("1(Integer),杭州(String),p1(String),p2(String),false(Boolean),true(Boolean),p1(String),p2(String)");
    }

    // 全由主键条件组成的 OR 整组下推
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testGroupJoin3")]
    public async Task TestGroupJoin3()
    {
        using var listen = SqlCapture.Listen();
        string[] ids = ["p3", "p4"];
        await Db.Query<M8Province>().GroupJoinAll()
            .Where(m => m.Cities!.Any(c => c.Name == "杭州") && (m.Id == "p1" || m.Id == "p2" || ids.Contains(m.Id))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_city` t1 WHERE t1.`name` = ? AND (t1.`pid` = ? OR t1.`pid` = ? OR t1.`pid` IN (?,?)) GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND (t.`id` = ? OR t.`id` = ? OR t.`id` IN (?,?))");
        listen.AssertParams("1(Integer),杭州(String),p1(String),p2(String),p3(String),p4(String),false(Boolean),true(Boolean),p1(String),p2(String),p3(String),p4(String)");
    }

    // 两层都下推：区的分组子查询按市的 id、市的分组子查询按省的 id
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testGroupJoin4")]
    public async Task TestGroupJoin4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().GroupJoinAll()
            .Where(m => m.Cities!.Any(c => c.Id == "c1" && c.Name == "杭州" && c.Areas!.Any(a => a.Name == "上城区")) && m.Id == "p1").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `m8_province` t LEFT JOIN (SELECT t1.`pid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_city` t1 LEFT JOIN (SELECT t3.`cid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_area` t3 WHERE t3.`name` = ? AND t3.`cid` = ? GROUP BY t3.`cid`) t4 ON t4.`__group_key1__` = t1.`id` WHERE t1.`id` = ? AND t1.`name` = ? AND IFNULL(t4.`__any2__`,?) = ? AND t1.`pid` = ? GROUP BY t1.`pid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND t.`id` = ?");
        listen.AssertParams("1(Integer),1(Integer),上城区(String),c1(String),c1(String),杭州(String),false(Boolean),true(Boolean),p1(String),false(Boolean),true(Boolean),p1(String)");
    }

    // 多对多：下推到中间表的列
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement() 链式取元素属性；C# 写成嵌套 Any，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testMany2Many")]
    public async Task TestMany2Many()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Parent>().GroupJoinAll().Where(m => m.Children2!.Any(c => c.Name == "123") && m.Id == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`order` FROM `m8_parent` t LEFT JOIN (SELECT t2.`parent_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `m8_child` t1 INNER JOIN `m8_parent_child` t2 ON t1.`id` = t2.`child_id` WHERE t1.`name` = ? AND t2.`parent_id` = ? GROUP BY t2.`parent_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__any2__`,?) = ? AND t.`id` = ?");
        listen.AssertParams("1(Integer),123(String),123(String),false(Boolean),true(Boolean),123(String)");
    }

    // 批量插入乱序的行，自增主键回填后与查回的行一一对应
    [Fact, Upstream("mysql8/MySQL8Test3.java#batchTest")]
    public async Task BatchTest()
    {
        var rows = Enumerable.Range(0, 10).Select(i => new BatchInsert { Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            .OrderBy(_ => Guid.NewGuid()).ToList();
        await Db.Insert<BatchInsert>(rows).Batch().FillGeneratedKeys().ExecuteAsync(Ct);
        var saved = await Db.Query<BatchInsert>().Where(b => rows.Select(r => r.Id).Contains(b.Id)).ToListAsync(Ct);
        Assert.Equal(rows.Count, saved.Count);
        foreach (var row in saved) Assert.Equal(rows.Single(r => r.Name == row.Name).Id, row.Id);
    }

    /// <summary>Java 的 UserBankDTO：用户、卡数、最早开户的卡号。</summary>
    public sealed class UserBankDto
    {
        public string? UserId { get; set; }

        public long BankCardCount { get; set; }

        public string? FirstBankCardCode { get; set; }
    }

    // Java 用例没有断言：分组合并的计数与取第一张卡一起投影到 DTO
    [Fact, Upstream("mysql8/MySQL8Test3.java#testDTO1")]
    public async Task TestDTO1()
    {
        await Db.Query<SysUser>().GroupJoinAll()
            .Select(u => new UserBankDto { UserId = u.Id, BankCardCount = u.BankCards!.Count(), FirstBankCardCode = u.BankCards!.OrderBy(c => c.OpenTime).First().Code })
            .ToListAsync(Ct);
    }

    // Java 用例没有断言：两张表的一列 UNION ALL
    [Fact, Upstream("mysql8/MySQL8Test3.java#testOneIsNull")]
    public async Task TestOneIsNull()
    {
        await Db.Query<SysBankCard>().Select(c => c.Code).UnionAll(Db.Query<SysUser>().Select(u => u.Phone)).ToListAsync(Ct);
    }

    // 偏移类窗口函数：Java 的 offset(…).prev / next 对应 Sql.Over.Lag / Lead
    [Fact, Upstream("mysql8/MySQL8Test3.java#nextTest")]
    public async Task NextTest()
    {
        var time = new DateTime(2025, 1, 1);
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>()
            .Select(c => new Draft<string?, string?, DateTime?, DateTime?, DateTime?, DateTime?, DateTime?>(
                c.Type,
                Sql.Over.Lag(c.Type, 1, w => w.OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.Lead(c.OpenTime, 1, w => w.OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.Lead(c.OpenTime, 1, w => w.PartitionBy(c.BankId).OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.Lead(c.OpenTime, 1, time, w => w.PartitionBy(c.BankId).OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.Lead(c.OpenTime, 1, w => w.PartitionBy(c.BankId).OrderBy(c.Type).OrderByDescending(c.Code)) ?? time,
                Sql.Over.Lead(c.OpenTime, 1, c.OpenTime, w => w.PartitionBy(c.BankId).OrderBy(c.Type).OrderByDescending(c.Code)) ?? time))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`type` AS `value1`,LAG(t.`type`, 1) OVER (ORDER BY t.`type` ASC,t.`code` DESC) AS `value2`,LEAD(t.`open_time`, 1) OVER (ORDER BY t.`type` ASC,t.`code` DESC) AS `value3`,LEAD(t.`open_time`, 1) OVER (PARTITION BY t.`bank_id` ORDER BY t.`type` ASC,t.`code` DESC) AS `value4`,LEAD(t.`open_time`, 1, ?) OVER (PARTITION BY t.`bank_id` ORDER BY t.`type` ASC,t.`code` DESC) AS `value5`,IFNULL(LEAD(t.`open_time`, 1) OVER (PARTITION BY t.`bank_id` ORDER BY t.`type` ASC,t.`code` DESC),?) AS `value6`,IFNULL(LEAD(t.`open_time`, 1, t.`open_time`) OVER (PARTITION BY t.`bank_id` ORDER BY t.`type` ASC,t.`code` DESC),?) AS `value7` FROM `t_bank_card` t");
        listen.AssertParams("2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime),2025-01-01T00:00(LocalDateTime)");
    }

    [Fact, Upstream("mysql8/MySQL8Test3.java#nextTest1")]
    public async Task NextTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>()
            .Select(c => new Draft<string?, string?, DateTime?, DateTime?, DateTime?>(
                c.Type,
                Sql.Over.Lag(c.Type, 1, w => w.OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.FirstValue(c.OpenTime, w => w.OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.LastValue(c.OpenTime, w => w.PartitionBy(c.BankId).OrderBy(c.Type).OrderByDescending(c.Code)),
                Sql.Over.NthValue(c.OpenTime, 2, w => w.PartitionBy(c.BankId).OrderBy(c.Type).OrderByDescending(c.Code))))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`type` AS `value1`,LAG(t.`type`, 1) OVER (ORDER BY t.`type` ASC,t.`code` DESC) AS `value2`,FIRST_VALUE(t.`open_time`) OVER (ORDER BY t.`type` ASC,t.`code` DESC) AS `value3`,LAST_VALUE(t.`open_time`) OVER (PARTITION BY t.`bank_id` ORDER BY t.`type` ASC,t.`code` DESC) AS `value4`,NTH_VALUE(t.`open_time`, 2) OVER (PARTITION BY t.`bank_id` ORDER BY t.`type` ASC,t.`code` DESC) AS `value5` FROM `t_bank_card` t");
    }

    [Fact, Upstream("mysql8/MySQL8Test3.java#nextTest2", NotApplicable = "Java 的 offset() 可以不写窗口、沿用查询的排序，没有排序时运行期报错；C# 的 Sql.Over.Lag / Lead 必须传窗口，编译期就要求写出排序")]
    public void NextTest2()
    {
    }

    // Java 的 offset() 不写窗口时沿用查询的 ORDER BY；C# 显式写出同样的窗口
    [Fact, Upstream("mysql8/MySQL8Test3.java#nextTest3", Deviation = "Java 的 offset() 不写窗口时沿用查询的排序；C# 的 Sql.Over.Lag 显式传同样的窗口，SQL 相同")]
    public async Task NextTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().OrderBy(c => c.OpenTime)
            .Select(c => new Draft<string?, string?>(c.Type, Sql.Over.Lag(c.Type, 1, w => w.OrderBy(c.OpenTime)))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`type` AS `value1`,LAG(t.`type`, 1) OVER (ORDER BY t.`open_time` ASC) AS `value2` FROM `t_bank_card` t ORDER BY t.`open_time` ASC");
    }

    // 写法与 Java 不同、SQL 一致：Java 的 firstCard 是 OneToOne + limit 1 + orderByProps 的导航；C# 写成 BankCards.OrderBy(c => c.OpenTime).First()，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testQueryFirstLimit3")]
    public async Task TestQueryFirstLimit3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards!.OrderBy(c => c.OpenTime).First().Code == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t3.`code` = ?");
        listen.AssertParams("1(Integer),123(String)");
    }

    // 写法与 Java 不同、SQL 一致：Java 的 firstCard 是 OneToOne + limit 1 + orderByProps 的导航；C# 写成 BankCards.OrderBy(c => c.OpenTime).First()，SQL 相同
    [Fact, Upstream("mysql8/MySQL8Test3.java#testQueryFirstLimit4")]
    public async Task TestQueryFirstLimit4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards!.OrderBy(c => c.OpenTime).First().Bank!.Name == "建设银行").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) INNER JOIN `t_bank` t4 ON t4.`id` = t3.`bank_id` WHERE t4.`name` = ?");
        listen.AssertParams("1(Integer),建设银行(String)");
    }

    // ---------- 树：Java 的 asTreeCTE / toTreeList，C# 的 AsTreeCte / ToTreeAsync ----------

    private const string C0122 = "01225d2f-e1a5-46d8-8ef9-535b7b1b7754", C03ab = "03abe9c8-adf1-4934-ae53-3b52c7c3eb2d", C1bcc = "1bccba2c-7cff-43af-b117-2e518be4422a";

    // 每一行都是起点（层级 0），各自挂上走到的下级
    [Fact, Upstream("mysql8/MySQL8Test3.java#testTree")]
    public async Task TestTree()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<Comment>().AsTreeCte().ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`parent_id`,t1.`content`,t1.`user_id`,t1.`post_id`,t1.`create_at` FROM `t_comment` t1)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`parent_id`,t3.`content`,t3.`user_id`,t3.`post_id`,t3.`create_at` FROM `as_tree_cte` t2 INNER JOIN `t_comment` t3 ON t3.`parent_id` = t2.`id`) ) SELECT t.`id`,t.`parent_id`,t.`content`,t.`user_id`,t.`post_id`,t.`create_at`,t.`cte_deep` FROM `as_tree_cte` t");
        Assert.Equal(3, tree.Count);
        Assert.Equal(C0122, tree[0].Id);
        Assert.Equal(C03ab, tree[0].ParentId);
        Assert.Equal(C1bcc, Assert.Single(tree[0].Children!).Id);
        Assert.Empty(tree[0].Children![0].Children!);
        Assert.Equal(C03ab, tree[1].Id);
        Assert.Equal("0", tree[1].ParentId);
        var child = Assert.Single(tree[1].Children!);
        Assert.Equal(C0122, child.Id);
        var grandChild = Assert.Single(child.Children!);
        Assert.Equal(C1bcc, grandChild.Id);
        Assert.Empty(grandChild.Children!);
    }

    [Fact, Upstream("mysql8/MySQL8Test3.java#testTree_1")]
    public async Task TestTree_1()
    {
        using var listen = SqlCapture.Listen();
        Assert.Empty(await Db.Query<Comment>().Where(c => c.Id == null).AsTreeCte().ToTreeAsync(Ct));
        listen.AssertSql("WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`parent_id`,t1.`content`,t1.`user_id`,t1.`post_id`,t1.`create_at` FROM `t_comment` t1 WHERE t1.`id` IS NULL)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`parent_id`,t3.`content`,t3.`user_id`,t3.`post_id`,t3.`create_at` FROM `as_tree_cte` t2 INNER JOIN `t_comment` t3 ON t3.`parent_id` = t2.`id`) ) SELECT t.`id`,t.`parent_id`,t.`content`,t.`user_id`,t.`post_id`,t.`create_at`,t.`cte_deep` FROM `as_tree_cte` t");
    }

    // 自动映射成 DTO 再拼树：DTO 上声明了指向自身的导航，层级列叫 deep 时 DTO 的 Deep 有值
    [Fact, Upstream("mysql8/MySQL8Test3.java#testTree2")]
    public async Task TestTree2()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<Comment>().AsTreeCte(o => o.DeepColumnName = "deep").Select<MyComment>().ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `deep`,t1.`id`,t1.`parent_id`,t1.`content`,t1.`user_id`,t1.`post_id`,t1.`create_at` FROM `t_comment` t1)  UNION ALL  (SELECT t2.`deep` + 1 AS `deep`,t3.`id`,t3.`parent_id`,t3.`content`,t3.`user_id`,t3.`post_id`,t3.`create_at` FROM `as_tree_cte` t2 INNER JOIN `t_comment` t3 ON t3.`parent_id` = t2.`id`) ) SELECT t.`id`,t.`parent_id`,t.`content`,t.`user_id`,t.`post_id`,t.`create_at`,t.`deep` FROM `as_tree_cte` t");
        Assert.Equal(3, tree.Count);
        Assert.Equal((C0122, C03ab), (tree[0].Id, tree[0].ParentId));
        Assert.Equal(C1bcc, Assert.Single(tree[0].Children!).Id);
        Assert.Empty(tree[0].Children![0].Children!);
        Assert.Equal((C03ab, "0"), (tree[1].Id, tree[1].ParentId));
        var child = Assert.Single(tree[1].Children!);
        Assert.Equal(C0122, child.Id);
        Assert.Equal(C1bcc, Assert.Single(child.Children!).Id);
        Assert.Equal((C1bcc, C0122), (tree[2].Id, tree[2].ParentId));
        Assert.Empty(tree[2].Children!);
        Assert.All(tree, n => Assert.Equal(0L, n.Deep));
    }

    // 往上走：每个起点沿上级挂到最上面，根是最上面那一级（层级最大）
    private static void AssertUpTree<TNode>(List<TNode> tree, Func<TNode, string?> id, Func<TNode, long?> deep, Func<TNode, List<TNode>?> children)
    {
        Assert.Equal(3, tree.Count);
        Assert.All(tree, n => Assert.Equal(C03ab, id(n)));
        Assert.Equal([1L, 0L, 2L], tree.Select(n => deep(n)!.Value));
        Assert.Equal(C0122, id(Assert.Single(children(tree[0])!)));
        Assert.Empty(children(tree[1])!);
        var middle = Assert.Single(children(tree[2])!);
        Assert.Equal((C0122, 1L), (id(middle), deep(middle)));
        Assert.Equal(C1bcc, id(Assert.Single(children(middle)!)));
    }

    [Fact, Upstream("mysql8/MySQL8Test3.java#testTree3")]
    public async Task TestTree3()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<Comment>().AsTreeCte(o => { o.Up = true; o.DeepColumnName = "deep"; }).Select<MyComment>().ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `deep`,t1.`id`,t1.`parent_id`,t1.`content`,t1.`user_id`,t1.`post_id`,t1.`create_at` FROM `t_comment` t1)  UNION ALL  (SELECT t2.`deep` + 1 AS `deep`,t3.`id`,t3.`parent_id`,t3.`content`,t3.`user_id`,t3.`post_id`,t3.`create_at` FROM `as_tree_cte` t2 INNER JOIN `t_comment` t3 ON t3.`id` = t2.`parent_id`) ) SELECT t.`id`,t.`parent_id`,t.`content`,t.`user_id`,t.`post_id`,t.`create_at`,t.`deep` FROM `as_tree_cte` t");
        AssertUpTree(tree, n => n.Id, n => n.Deep, n => n.Children);
    }

    // DTO 上没有声明导航：按源实体的 Children 导航，在 DTO 上找同名的键与子节点
    [Fact, Upstream("mysql8/MySQL8Test3.java#testTree4")]
    public async Task TestTree4()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<Comment>().AsTreeCte(o => { o.Up = true; o.DeepColumnName = "deep"; }).Select<MyComment3>().ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `deep`,t1.`id`,t1.`parent_id`,t1.`content`,t1.`user_id`,t1.`post_id`,t1.`create_at` FROM `t_comment` t1)  UNION ALL  (SELECT t2.`deep` + 1 AS `deep`,t3.`id`,t3.`parent_id`,t3.`content`,t3.`user_id`,t3.`post_id`,t3.`create_at` FROM `as_tree_cte` t2 INNER JOIN `t_comment` t3 ON t3.`id` = t2.`parent_id`) ) SELECT t.`id`,t.`parent_id`,t.`content`,t.`user_id`,t.`post_id`,t.`create_at`,t.`deep` FROM `as_tree_cte` t");
        AssertUpTree(tree, n => n.Id, n => n.Deep, n => n.Children);
    }

    // DTO 缺上级键：拼树前报错
    [Fact, Upstream("mysql8/MySQL8Test3.java#testTree5", Deviation = "异常信息用中文，说明缺的是哪个属性")]
    public async Task TestTree5()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Query<Comment>().Where(c => c.ParentId != null).FilterBy(new Comment()).AsTreeCte().Select<MyComment4>().ToTreeAsync(Ct));
        Assert.Equal("MyComment4 上没有属性 ParentId，拼树时取不到父子键", ex.Message);
    }

    /// <summary>对应 Java：mysql8/vo/UserBankDTO2。</summary>
    private sealed class UserBankDTO2
    {
        public string? Name { get; set; }

        public string? Phone { get; set; }

        public string? Code2 { get; set; }
    }

    private Query<UserBankDTO2> UserBanks(System.Linq.Expressions.Expression<Func<SysUser, SysBankCard, bool>> where) =>
        Db.Query<SysUser>().SmartPredicate().InnerJoin<SysBankCard>((user, card) => user.Id == card.Uid).Where(where)
            .Select((user, card) => new UserBankDTO2 { Name = user.Name, Phone = user.Phone, Code2 = card.Code });

    // 条件下推（SMART_PREDICATE）：只涉及银行卡的条件推进它的 ON，只涉及用户的推进内层 WHERE
    [Fact, Upstream("mysql8/MySQL8Test3.java#testDTOWhereToTableWhere")]
    public async Task TestDTOWhereToTableWhere()
    {
        using var listen = SqlCapture.Listen();
        await UserBanks((user, _) => user.Age == 18).Where(u => u.Code2 == "123" && (u.Name == "name" || u.Phone == "phone")).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`name` AS `name`,t2.`phone` AS `phone`,t2.`code2` AS `code2` FROM (SELECT t.`name` AS `name`,t.`phone` AS `phone`,t1.`code` AS `code2` FROM `t_sys_user` t INNER JOIN `t_bank_card` t1 ON t.`id` = t1.`uid` AND t1.`code` = ? WHERE t.`age` = ? AND (t.`name` = ? OR t.`phone` = ?)) t2");
        listen.AssertParams("123(String),18(Integer),name(String),phone(String)");
    }

    // OR 里混了两张表的列：整组条件留在外层
    [Fact, Upstream("mysql8/MySQL8Test3.java#testDTOWhereToTableWhere1")]
    public async Task TestDTOWhereToTableWhere1()
    {
        using var listen = SqlCapture.Listen();
        await UserBanks((_, card) => card.Code == "456").Where(u => u.Code2 == "123" && (u.Name == "name" || u.Code2 == "phone")).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`name` AS `name`,t2.`phone` AS `phone`,t2.`code2` AS `code2` FROM (SELECT t.`name` AS `name`,t.`phone` AS `phone`,t1.`code` AS `code2` FROM `t_sys_user` t INNER JOIN `t_bank_card` t1 ON t.`id` = t1.`uid` WHERE t1.`code` = ?) t2 WHERE t2.`code2` = ? AND (t2.`name` = ? OR t2.`code2` = ?)");
        listen.AssertParams("456(String),123(String),name(String),phone(String)");
    }

    // CROSS JOIN 也能带 ON（MySQL 等同 INNER JOIN）
    [Fact, Upstream("mysql8/CrossJoinTest.java#crossJoin1")]
    public async Task CrossJoin1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBankCard>().CrossJoin<SysBankCard>((c1, c2) => c1.Id == c2.Id).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t CROSS JOIN `t_bank_card` t1 ON t.`id` = t1.`id`");
    }

    private const string FirstCardRanged = " FROM (SELECT t1.`id` AS `id`,t1.`uid` AS `uid`,t1.`code` AS `code`,t1.`type` AS `type`,t1.`bank_id` AS `bank_id`,t1.`open_time` AS `open_time` FROM (SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t.`uid` ORDER BY t.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t WHERE t.`uid` IN (?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2";

    // 一对一带 Limit 的导航：每个用户按开户时间取第一张卡
    [Fact, Upstream("mysql8/MySQL8Test3.java#testQueryFirstLimit1")]
    public async Task TestQueryFirstLimit1()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysUser>().Include(s => s.FirstCard).ToListAsync(Ct);
        listen.AssertSql(1, "SELECT t2.`id` AS `id`,t2.`uid` AS `uid`,t2.`code` AS `code`,t2.`type` AS `type`,t2.`bank_id` AS `bank_id`,t2.`open_time` AS `open_time`" + FirstCardRanged);
        listen.AssertParams(1, "u1(String),u2(String),1(Long),1(Long)");
        Assert.All(list.Where(u => u.FirstCard is not null), u => Assert.Equal(u.Id, u.FirstCard!.Uid));
    }

    /// <summary>对应 Java：mysql8/vo/SysUserFirstCardDTO（平铺第一张卡的卡号、类型、开户时间）。</summary>
    public sealed class SysUserFirstCardDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Phone { get; set; }

        public int? Age { get; set; }

        public DateTime? CreateTime { get; set; }

        [MapPath("FirstCard.Code")]
        public string? CardCode { get; set; }

        [MapPath("FirstCard.Type")]
        public string? Type { get; set; }

        [MapPath("FirstCard.OpenTime")]
        public DateTime? OpenTime { get; set; }
    }

    // 平铺同样按导航的 Limit 取段，外层只取平铺的列（不写别名）
    [Fact, Upstream("mysql8/MySQL8Test3.java#testQueryFirstLimit2")]
    public async Task TestQueryFirstLimit2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Select<SysUserFirstCardDTO>().ToListAsync(Ct);
        listen.AssertSql(1, "SELECT t2.`code`,t2.`type`,t2.`open_time`,t2.`uid` AS `__relation__uid`" + FirstCardRanged);
        listen.AssertParams(1, "u1(String),u2(String),1(Long),1(Long)");
    }
}
