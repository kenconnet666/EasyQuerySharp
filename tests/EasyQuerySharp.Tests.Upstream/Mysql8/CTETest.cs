namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/CTETest.java（toCteAs 与 JOIN、拦截器作用到 CTE 引用、由查询定义的实体 EntityCteViewer）。
/// Java 的 toCteAs 对应 <c>AsCte(name)</c>；EntityCteViewer 对应实现 <see cref="ICteView{TSelf}"/> 的实体。
/// </summary>
public sealed class CTETest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private Query<M8User> AgeCte(int age, string name) => Db.Query<M8User>().Where(m => m.Age == age).AsCte(name);

    [Fact, Upstream("mysql8/CTETest.java#cteTest1")]
    public async Task CteTest1()
    {
        using var listen = SqlCapture.Listen();
        await AgeCte(12, "ttt").Where(s => s.Name!.Contains("123")).Select(m => new Draft<string?, int?>(m.Name, m.Age)).ToListAsync(Ct);
        listen.AssertSql("WITH `ttt` AS (SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t WHERE t.`age` = ?) SELECT t1.`name` AS `value1`,t1.`age` AS `value2` FROM `ttt` t1 WHERE t1.`name` LIKE ?");
        listen.AssertParams("12(Integer),%123%(String)");
    }

    [Fact, Upstream("mysql8/CTETest.java#cteJoinTest2")]
    public async Task CteJoinTest2()
    {
        using var listen = SqlCapture.Listen();
        await AgeCte(12, "ttt").LeftJoin(AgeCte(13, "yyy"), (m, m2) => m.Age == m2.Age)
            .Where((m1, m2) => m1.Name!.Contains("123") && m2.Name!.Contains("456"))
            .Select((m1, m2) => new Draft<string?, string?>(m1.Name, m2.Name)).ToListAsync(Ct);
        listen.AssertSql("WITH `ttt` AS (SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t WHERE t.`age` = ?) ,`yyy` AS (SELECT t2.`id`,t2.`name`,t2.`age`,t2.`create_time` FROM `m8_user` t2 WHERE t2.`age` = ?) SELECT t1.`name` AS `value1`,t3.`name` AS `value2` FROM `ttt` t1 LEFT JOIN `yyy` t3 ON t1.`age` = t3.`age` WHERE t1.`name` LIKE ? AND t3.`name` LIKE ?");
        listen.AssertParams("12(Integer),13(Integer),%123%(String),%456%(String)");
    }

    // 定义 CTE 之前启用拦截器：定义里有它，外层对两个 CTE 的引用也都带上（Java 把实体 CTE 的引用当实体表）
    [Fact, Upstream("mysql8/CTETest.java#cteJoinTest7")]
    public async Task CteJoinTest7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().Where(m => m.Age == 12).EnableInterceptor("aaaaxx").AsCte("ttt").LeftJoin(AgeCte(13, "yyy"), (m, m2) => m.Age == m2.Age)
            .Where((m1, m2) => m1.Name!.Contains("123") && m2.Name!.Contains("456"))
            .Select((m1, m2) => new Draft<string?, string?>(m1.Name, m2.Name)).ToListAsync(Ct);
        listen.AssertSql("WITH `ttt` AS (SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t WHERE t.`name` = ? AND t.`age` = ?) ,`yyy` AS (SELECT t2.`id`,t2.`name`,t2.`age`,t2.`create_time` FROM `m8_user` t2 WHERE t2.`age` = ?) SELECT t1.`name` AS `value1`,t3.`name` AS `value2` FROM `ttt` t1 LEFT JOIN `yyy` t3 ON t3.`name` = ? AND t1.`age` = t3.`age` WHERE t1.`name` = ? AND t1.`name` LIKE ? AND t3.`name` LIKE ?");
        listen.AssertParams("123(String),12(Integer),13(Integer),123(String),123(String),%123%(String),%456%(String)");
    }

    // 定义 CTE 之后才启用：只作用于外层的引用
    [Fact, Upstream("mysql8/CTETest.java#cteJoinTest8")]
    public async Task CteJoinTest8()
    {
        using var listen = SqlCapture.Listen();
        await AgeCte(12, "ttt").EnableInterceptor("aaaaxx").LeftJoin(AgeCte(13, "yyy"), (m, m2) => m.Age == m2.Age)
            .Where((m1, m2) => m1.Name!.Contains("123") && m2.Name!.Contains("456"))
            .Select((m1, m2) => new Draft<string?, string?>(m1.Name, m2.Name)).ToListAsync(Ct);
        listen.AssertSql("WITH `ttt` AS (SELECT t.`id`,t.`name`,t.`age`,t.`create_time` FROM `m8_user` t WHERE t.`age` = ?) ,`yyy` AS (SELECT t2.`id`,t2.`name`,t2.`age`,t2.`create_time` FROM `m8_user` t2 WHERE t2.`age` = ?) SELECT t1.`name` AS `value1`,t3.`name` AS `value2` FROM `ttt` t1 LEFT JOIN `yyy` t3 ON t3.`name` = ? AND t1.`age` = t3.`age` WHERE t1.`name` = ? AND t1.`name` LIKE ? AND t3.`name` LIKE ?");
        listen.AssertParams("12(Integer),13(Integer),123(String),123(String),%123%(String),%456%(String)");
    }

    // 由查询定义的实体：查询它时写成 WITH `m8_user_temp` AS (视图查询)
    [Fact, Upstream("mysql8/CTETest.java#cteTest3")]
    public async Task CteTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserTemp>().Where(m => Sql.LikeContains(m.Name, "123")).ToListAsync(Ct);
        listen.AssertSql("WITH `m8_user_temp` AS (SELECT t.`id`,t.`name` FROM `m8_user` t WHERE t.`age` IS NULL) SELECT t2.`id`,t2.`name` FROM `m8_user_temp` t2 WHERE t2.`name` LIKE CONCAT('%',?,'%')");
        listen.AssertParams("123(String)");
    }

    // 视图实体上的多对多导航
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement 在 C# 里写成 Any
    [Fact, Upstream("mysql8/CTETest.java#cteTest4")]
    public async Task CteTest4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserTemp>().Where(m => m.Roles!.Any(r => Sql.LikeContains(r.Name, "123"))).ToListAsync(Ct);
        listen.AssertSql("WITH `m8_user_temp` AS (SELECT t.`id`,t.`name` FROM `m8_user` t WHERE t.`age` IS NULL) SELECT t2.`id`,t2.`name` FROM `m8_user_temp` t2 WHERE EXISTS(SELECT 1 FROM `m8_role` t3 WHERE EXISTS(SELECT 1 FROM `m8_user_role` t4 WHERE t4.`role_id` = t3.`id` AND t4.`user_id` = t2.`id` LIMIT 1) AND t3.`name` LIKE CONCAT('%',?,'%') LIMIT 1)");
        listen.AssertParams("123(String)");
    }

    // 视图实体 JOIN 自己，再取第二张表的第一个角色
    [Fact, Upstream("mysql8/CTETest.java#cteTest5")]
    public async Task CteTest5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserTemp>().LeftJoin<M8UserTemp>((m, m2) => m.Name == m2.Name)
            .Where((_, m2) => m2.Roles!.First().Name!.Contains("123"))
            .Select((m1, m2) => new Draft<string?, string?>(m1.Name, m2.Roles!.First().Name)).ToListAsync(Ct);
        listen.AssertSql("WITH `m8_user_temp` AS (SELECT t.`id`,t.`name` FROM `m8_user` t WHERE t.`age` IS NULL) SELECT t2.`name` AS `value1`,t10.`name` AS `value2` FROM `m8_user_temp` t2 LEFT JOIN `m8_user_temp` t5 ON t2.`name` = t5.`name` LEFT JOIN (SELECT t7.`user_id` AS `__user_id__`,t6.`id`,t6.`name`,t6.`create_time`,(ROW_NUMBER() OVER (PARTITION BY t7.`user_id`)) AS `__row__` FROM `m8_role` t6 INNER JOIN `m8_user_role` t7 ON t6.`id` = t7.`role_id`) t10 ON (t10.`__user_id__` = t5.`id` AND t10.`__row__` = ?) WHERE t10.`name` LIKE ?");
        listen.AssertParams("1(Integer),%123%(String)");
    }

    // 视图里带窗口函数列；前一段 GroupJoinAll 下的嵌套 Any 只执行不断言
    // 写法与 Java 不同、SQL 一致：Java 的 flatElement 在 C# 里写成 Any
    [Fact, Upstream("mysql8/CTETest.java#cteTest6")]
    public async Task CteTest6()
    {
        await Db.Query<M8User>().GroupJoinAll().Where(m => m.Roles!.Any(r => r.Menus!.Any(x => Sql.LikeContains(x.Path, "/admin")))).ToListAsync(Ct);
        using var listen = SqlCapture.Listen();
        await Db.Query<M8UserTemp2>().Where(m => m.RowNumber == 2L).ToListAsync(Ct);
        listen.AssertSql("WITH `m8_user_temp2` AS (SELECT t.`id`,t.`name`,t.`age`,(ROW_NUMBER() OVER (PARTITION BY t.`age` ORDER BY t.`create_time` DESC)) AS `row_number` FROM `m8_user` t WHERE t.`age` IS NULL) SELECT t2.`id`,t2.`name`,t2.`age`,t2.`row_number` FROM `m8_user_temp2` t2 WHERE t2.`row_number` = ?");
        listen.AssertParams("2(Long)");
    }
}
