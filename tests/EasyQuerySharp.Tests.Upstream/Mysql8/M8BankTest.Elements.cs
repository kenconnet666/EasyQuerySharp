using EasyQuerySharp.Interceptors;
using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// M8BankTest：集合导航里取某一张（First）、默认分组合并的导航（BankCards2 / BankCards3）、分组结果作为 JOIN 来源
public sealed partial class M8BankTest
{
    // 每个用户最早开户的那张卡的卡号，和用户实体一起返回
    [Fact, Upstream("mysql8/M8BankTest.java#testFirst")]
    public async Task TestFirst()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.Age > 18)
            .Select(u => new Part<SysUser, string?>(u, u.BankCards!.OrderBy(c => c.OpenTime).First().Code)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time`,t3.`code` AS `__part__column1` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t.`age` > ?");
        listen.AssertParams("1(Integer),18(Integer)");
    }

    [Fact, Upstream("mysql8/M8BankTest.java#testFirst2")]
    public async Task TestFirst2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.Age > 18)
            .Select(u => new Draft<string?, string?>(u.Name, u.BankCards!.OrderBy(c => c.OpenTime).First().Code)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t3.`code` AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t.`age` > ?");
        listen.AssertParams("1(Integer),18(Integer)");
    }

    // 必有的导航（Required）：取某一张时 INNER JOIN
    [Fact, Upstream("mysql8/M8BankTest.java#testFirstElementInnerJoin")]
    public async Task TestFirstElementInnerJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(u => u.BankCards3!.OrderBy(c => c.OpenTime).First().Type == "储蓄卡").ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t INNER JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) WHERE t3.`type` = ?");
        listen.AssertParams("1(Integer),储蓄卡(String)");
    }

    // 同样的取法只 JOIN 一次，第二个条件再经由它连银行
    [Fact, Upstream("mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin2")]
    public async Task TestUserAutoSubQueryToGroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .Where(u => u.BankCards2!.OrderBy(c => c.OpenTime).First().Type == "储蓄卡" && u.BankCards2!.OrderBy(c => c.OpenTime).First().Bank!.Name == "工商银行")
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`id`,t1.`uid`,t1.`code`,t1.`type`,t1.`bank_id`,t1.`open_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`uid` ORDER BY t1.`open_time` ASC)) AS `__row__` FROM `t_bank_card` t1) t3 ON (t3.`uid` = t.`id` AND t3.`__row__` = ?) INNER JOIN `t_bank` t4 ON t4.`id` = t3.`bank_id` WHERE t3.`type` = ? AND t4.`name` = ?");
        listen.AssertParams("1(Integer),储蓄卡(String),工商银行(String)");
    }

    // 分组里按条件计数，条件用到默认分组合并的导航
    [Fact, Upstream("mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin3")]
    public async Task TestUserAutoSubQueryToGroupJoin3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupBy(u => u.Name)
            .Select(g => new Draft<string?, long>(g.Key, g.Count(u => u.BankCards2!.Any(c => c.Type == "123" || c.Code == "456")))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,COUNT((CASE WHEN IFNULL(t2.`__any2__`,?) = ? THEN ? ELSE NULL END)) AS `value2` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE (t1.`type` = ? OR t1.`code` = ?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` GROUP BY t.`name`");
        listen.AssertParams("false(Boolean),true(Boolean),1(Integer),1(Integer),123(String),456(String)");
    }

    // 必有的导航：INNER JOIN，聚合结果不会是 NULL，不包 IFNULL
    [Fact, Upstream("mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin3_1")]
    public async Task TestUserAutoSubQueryToGroupJoin3_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().GroupBy(u => u.Name)
            .Select(g => new Draft<string?, long>(g.Key, g.Count(u => u.BankCards3!.Any(c => c.Type == "123" || c.Code == "456")))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,COUNT((CASE WHEN t2.`__any2__` = ? THEN ? ELSE NULL END)) AS `value2` FROM `t_sys_user` t INNER JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_bank_card` t1 WHERE (t1.`type` = ? OR t1.`code` = ?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` GROUP BY t.`name`");
        listen.AssertParams("true(Boolean),1(Integer),1(Integer),123(String),456(String)");
    }

    // Java 的 flatElement() 是"任一元素满足"，C# 写 Any；ALL_SUB_QUERY_GROUP_JOIN 对应显式 GroupJoin
    [Fact, Upstream("mysql8/M8BankTest.java#testFlatTest", Deviation = "Java 用 ALL_SUB_QUERY_GROUP_JOIN 行为让全部集合导航合并成分组 JOIN；C# 在查询上显式 GroupJoin(导航)，flatElement 写成 Any")]
    public async Task TestFlatTest()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().GroupJoin(b => b.BankCards)
            .Where(b => b.BankCards!.Any(c => c.Code == "123") && b.BankCards!.Any(c => c.Type == "储蓄卡")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `__group_key1__`,(COUNT((CASE WHEN t1.`code` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,(COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) > 0) AS `__any3__` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__any3__`,?) = ?");
        listen.AssertParams("123(String),1(Integer),储蓄卡(String),1(Integer),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    // Java 用例没有断言，只验证条件分支里写导航能执行；这里分支不成立，只剩名字条件
    [Fact, Upstream("mysql8/M8BankTest.java#dynamicPartition")]
    public async Task DynamicPartition()
    {
        var likeIcbc = false;
        var name = "小明";
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>()
            .WhereIf(!string.IsNullOrWhiteSpace(name), u => u.Name!.Contains(name))
            .WhereIf(likeIcbc, u => u.BankCards!.OrderBy(c => c.OpenTime).First().Bank!.Name == "工商银行")
            .ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`name`,`phone`,`age`,`create_time` FROM `t_sys_user` WHERE `name` LIKE ?");
        listen.AssertParams("%小明%(String)");
    }

    // 分组统计作为派生表 LEFT JOIN
    [Fact, Upstream("mysql8/M8BankTest.java#testExtraFilter3")]
    public async Task TestExtraFilter3()
    {
        var cardCounts = Db.Query<SysBankCard>().GroupBy(c => c.BankId).Select(g => new Draft<string?, long>(g.Key, g.Count()));
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().LeftJoin(cardCounts, (b, g) => b.Id == g.Value1)
            .Select((b, g) => new Draft<string?, long>(b.Name, g.Value2)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,t3.`value2` AS `value2` FROM `t_bank` t LEFT JOIN (SELECT t1.`bank_id` AS `value1`,COUNT(*) AS `value2` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) t3 ON t.`id` = t3.`value1`");
    }

    // 同上，分组统计写成 CTE
    [Fact, Upstream("mysql8/M8BankTest.java#testExtraFilter4")]
    public async Task TestExtraFilter4()
    {
        var cardCounts = Db.Query<SysBankCard>().GroupBy(c => c.BankId).Select(g => new Draft<string?, long>(g.Key, g.Count())).AsCte();
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().LeftJoin(cardCounts, (b, g) => b.Id == g.Value1)
            .Select((b, g) => new Draft<string?, long>(b.Name, g.Value2)).ToListAsync(Ct);
        listen.AssertSql("WITH `with_Draft2` AS (SELECT t1.`bank_id` AS `value1`,COUNT(*) AS `value2` FROM `t_bank_card` t1 GROUP BY t1.`bank_id`) SELECT t.`name` AS `value1`,t3.`value2` AS `value2` FROM `t_bank` t LEFT JOIN `with_Draft2` t3 ON t.`id` = t3.`value1`");
    }

    // 查询过滤拦截器在一次更新、一次删除里各只调用一次
    [Fact, Upstream("mysql8/M8BankTest.java#testaa1")]
    public async Task Testaa1()
    {
        _ = Db; // 没有 MySQL 时跳过
        var counter = new CountingCardFilter();
        var db = new EasyDb(o => o.UseMySql(new MySqlDataSource(TestDatabases.MySql.TryConnectionString(Mysql8Fixture.Database)!)).AddInterceptor(counter));

        await db.Update<SysBankCard>().Set(c => c.Id, "123123").Where(c => c.Id == "1" && c.Id == null).ExecuteAsync(Ct);
        Assert.Equal(1, counter.Calls);

        counter.Calls = 0;
        await db.Delete<SysBankCard>().Where(c => c.Id == "1" && c.Id == null).ExecuteAsync(Ct);
        Assert.Equal(1, counter.Calls);
    }

    // 对应 Java 的 M8Interceptor：只计数，不加条件
    private sealed class CountingCardFilter : QueryFilter<SysBankCard>
    {
        public int Calls { get; set; }

        public override System.Linq.Expressions.Expression<Func<SysBankCard, bool>>? Filter()
        {
            Calls++;
            return null;
        }
    }
}
