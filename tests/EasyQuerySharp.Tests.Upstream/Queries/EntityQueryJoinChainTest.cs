namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：EntityQueryAggregateTest2 / 4 / 5 / 7.java（1 到 10 张表的 LEFT JOIN 上的条件、排序、分组键、投影、去重与分页）。
/// Java 的 testN 用不同张数的表与各种等价写法（whereMerge / where / 带 false 的不生效重载、asTable(o -> o) 这类不改变 SQL 的调用），
/// C# 按表数写出对应元数的 lambda，不生效的调用不写；Test2 与 Test4 的 SQL 逐字相同。
/// </summary>
public sealed class EntityQueryJoinChainTest(MySqlFixture fixture) : MySqlTest(fixture)
{

    [Fact, Upstream("EntityQueryAggregateTest2.java#test1"), Upstream("EntityQueryAggregateTest4.java#test1")]
    public async Task WhereOrder1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, _, t10) => t1.Id == t10.Id)
            .WhereById("1").Where((t1, _, _, _, _, _, _, _, _, _) => t1.Id == "1").Where((_, t2, _, _, _, _, _, _, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _, _, _, _, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4, _, _, _, _, _, _) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test2"), Upstream("EntityQueryAggregateTest4.java#test2")]
    public async Task WhereOrder2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id)
            .WhereById("1").Where((t1, _, _, _, _, _, _, _, _) => t1.Id == "1").Where((_, t2, _, _, _, _, _, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _, _, _, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4, _, _, _, _, _) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test3"), Upstream("EntityQueryAggregateTest4.java#test3")]
    public async Task WhereOrder3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id)
            .WhereById("1").Where((t1, _, _, _, _, _, _, _) => t1.Id == "1").Where((_, t2, _, _, _, _, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _, _, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4, _, _, _, _) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test4"), Upstream("EntityQueryAggregateTest4.java#test4")]
    public async Task WhereOrder4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id)
            .WhereById("1").Where((t1, _, _, _, _, _, _) => t1.Id == "1").Where((_, t2, _, _, _, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4, _, _, _) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test5"), Upstream("EntityQueryAggregateTest4.java#test5")]
    public async Task WhereOrder5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id)
            .WhereById("1").Where((t1, _, _, _, _, _) => t1.Id == "1").Where((_, t2, _, _, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4, _, _) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test6"), Upstream("EntityQueryAggregateTest4.java#test6")]
    public async Task WhereOrder6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id)
            .WhereById("1").Where((t1, _, _, _, _) => t1.Id == "1").Where((_, t2, _, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4, _) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test7"), Upstream("EntityQueryAggregateTest4.java#test7")]
    public async Task WhereOrder7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id)
            .WhereById("1").Where((t1, _, _, _) => t1.Id == "1").Where((_, t2, _, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _, _) => t1.CreateTime).OrderByDescending((_, _, _, t4) => t4.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t3.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test8"), Upstream("EntityQueryAggregateTest4.java#test8")]
    public async Task WhereOrder8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id)
            .WhereById("1").Where((t1, _, _) => t1.Id == "1").Where((_, t2, _) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _, _) => t1.CreateTime).OrderByDescending((_, _, t3) => t3.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t2.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest2.java#test9"), Upstream("EntityQueryAggregateTest4.java#test9")]
    public async Task WhereOrder9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .WhereById("1").Where((t1, _) => t1.Id == "1").Where((_, t2) => t2.Title!.Contains("xx"))
            .OrderBy((t1, _) => t1.CreateTime).OrderByDescending((_, t2) => t2.CreateTime).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`id` = ? AND t.`id` = ? AND t1.`title` LIKE ? ORDER BY t.`create_time` ASC,t1.`create_time` DESC");
        listen.AssertParams("1(String),1(String),%xx%(String)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test1")]
    public async Task SelectDraft1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, _, t10) => t1.Id == t10.Id)
            .Select((t1, _, _, _, _, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id`");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test2")]
    public async Task SelectDraft2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, _, t10) => t1.Id == t10.Id)
            .Select((t1, _, _, _, _, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id`");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test3")]
    public async Task SelectDraft3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, _, t10) => t1.Id == t10.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _, _, _, _, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _, _, _, _, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _, _, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test4")]
    public async Task SelectDraft4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _, _, _, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _, _, _, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test5")]
    public async Task SelectDraft5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _, _, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _, _, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test6")]
    public async Task SelectDraft6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test7")]
    public async Task SelectDraft7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test8")]
    public async Task SelectDraft8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test9")]
    public async Task SelectDraft9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _, _) => t1.Stars == 1).OrderBy((t1, _, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest7.java#test10")]
    public async Task SelectDraft10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id)
            .WhereById("123").WhereByIds(new[] { "123" }).Where((t1, _, _) => t1.Stars == 1).OrderBy((t1, _, _) => t1.Id).Skip(2).Take(3)
            .Select((t1, _, _) => new Draft<string?, int?, DateTime?>(t1.Id, t1.Stars, t1.CreateTime)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`id` AS `value1`,t.`stars` AS `value2`,t.`create_time` AS `value3` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` WHERE t.`id` = ? AND t.`id` IN (?) AND t.`stars` = ? ORDER BY t.`id` ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test1")]
    public async Task GroupKeys1()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, _, t10) => t1.Id == t10.Id)
            .GroupBy((t1, t2, _, _, _, _, _, _, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` LEFT JOIN `t_topic` t9 ON t.`id` = t9.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test2")]
    public async Task GroupKeys2()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, _, t9) => t1.Id == t9.Id)
            .GroupBy((t1, t2, _, _, _, _, _, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` LEFT JOIN `t_topic` t8 ON t.`id` = t8.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test3")]
    public async Task GroupKeys3()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id).LeftJoin<Topic>((t1, _, _, _, _, _, _, t8) => t1.Id == t8.Id)
            .GroupBy((t1, t2, _, _, _, _, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` LEFT JOIN `t_topic` t7 ON t.`id` = t7.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test4")]
    public async Task GroupKeys4()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id).LeftJoin<Topic>((t1, _, _, _, _, _, t7) => t1.Id == t7.Id)
            .GroupBy((t1, t2, _, _, _, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` LEFT JOIN `t_topic` t6 ON t.`id` = t6.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test5")]
    public async Task GroupKeys5()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id).LeftJoin<Topic>((t1, _, _, _, _, t6) => t1.Id == t6.Id)
            .GroupBy((t1, t2, _, _, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` LEFT JOIN `t_topic` t5 ON t.`id` = t5.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test6")]
    public async Task GroupKeys6()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id).LeftJoin<Topic>((t1, _, _, _, t5) => t1.Id == t5.Id)
            .GroupBy((t1, t2, _, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` LEFT JOIN `t_topic` t4 ON t.`id` = t4.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test7")]
    public async Task GroupKeys7()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id).LeftJoin<Topic>((t1, _, _, t4) => t1.Id == t4.Id)
            .GroupBy((t1, t2, _, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` LEFT JOIN `t_topic` t3 ON t.`id` = t3.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test8")]
    public async Task GroupKeys8()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id).LeftJoin<Topic>((t1, _, t3) => t1.Id == t3.Id)
            .GroupBy((t1, t2, _) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` LEFT JOIN `t_topic` t2 ON t.`id` = t2.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }

    [Fact, Upstream("EntityQueryAggregateTest5.java#test9")]
    public async Task GroupKeys9()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<Topic>().LeftJoin<Topic>((t1, t2) => t1.Id == t2.Id)
            .GroupBy((t1, t2) => new { Id1 = t1.Id, Id2 = t2.Id });
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` GROUP BY t.`id`,t1.`id`");
        await grouped.Having(g => g.Count() > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(*) > ?");
        listen.AssertParams("0(Long)");
        await grouped.Having(g => g.Count(e => e.Item1.Id) > 0L).Keys().ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t1.`id` FROM `t_topic` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` GROUP BY t.`id`,t1.`id` HAVING COUNT(t.`id`) > ?");
        listen.AssertParams("0(Long)");
    }
}
