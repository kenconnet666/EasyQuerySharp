namespace EasyQuerySharp.Tests.Upstream.Queries;

// QueryTest12.java 的 Map 投影、加密列条件、导航的固定条件（extraFilter）与中间表条件、带关联子查询的删除
public sealed partial class QueryTest12
{
    private const string BlogTopicJoin = " FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ?";

    // Java 的 MapProxy.selectAll(b) + put("xx", …)：C# 的 new Dictionary<string, object?>(Sql.Columns(b)) { { "xx", … } }
    [Fact, Upstream("QueryTest12.java#testMap1")]
    public async Task TestMap1()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t2) => b.Id == t2.Id)
            .Select((b1, t2) => new Dictionary<string, object?>(Sql.Columns(b1)) { { "xx", t2.CreateTime } }).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + ",t1.`create_time` AS `xx`" + BlogTopicJoin);
        listen.AssertParams("false(Boolean)");
        Assert.All(rows, r => Assert.Equal(17, r.Count));
    }

    [Fact, Upstream("QueryTest12.java#testMap2"), Upstream("QueryTest20.java#testMapIgnore"), Upstream("QueryTest26.java#testaa")]
    public async Task TestMap2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t2) => b.Id == t2.Id)
            .Select((b1, t2) => new Dictionary<string, object?>(Sql.ColumnsExcept(b1, b1.CreateTime)) { { "xx", t2.CreateTime } }).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns).Replace("t.`create_time`,", "") + ",t1.`create_time` AS `xx`" + BlogTopicJoin);
        listen.AssertParams("false(Boolean)");
    }

    // Java 只跑不断言：整数列按 decimal 读出
    [Fact, Upstream("QueryTest12.java#testDraftInteger"), Upstream("QueryTest12.java#testDraftInteger1")]
    public async Task TestDraftInteger()
    {
        var list = await Db.Query<BlogEntity2>().LeftJoin<SysUser>((b, s2) => b.Id == s2.Id).Select((b, s2) => new Draft<decimal?, string?>(b.Star, b.Id)).ToListAsync(Ct);
        Assert.NotEmpty(list);
        await Db.Query<BlogEntity2>().LeftJoin<SysUser>((b, s2) => b.Id == s2.Id).LeftJoin<SysUser>((b, s2, s3) => s3.Id == b.Id)
            .Select((b, s2, s3) => new Draft<decimal?, string?>(b.Star, b.Id)).ToListAsync(Ct);
    }

    private const string BlogUsersAny = " FROM `t_blog` t WHERE t.`deleted` = ? AND EXISTS(SELECT 1 FROM `easy-query-test`.`t_sys_user` t1 WHERE t1.`id` = t.`title` AND t1.`id_card` LIKE ? LIMIT 1)";

    // 加密列的 LIKE：值按加密策略切片加密后再拼 %
    [Fact, Upstream("QueryTest12.java#testDoc7"), Upstream("QueryTest12.java#testDoc8")]
    public async Task TestDoc7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(s => s.Users!.Any(u => u.IdCard!.Contains("123123"))).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + BlogUsersAny);
        listen.AssertParams("false(Boolean),%l46D3q3sDqT7sJprBhRRog==oP64xEZMF/9uOzl/BwZiHw==5iSDNEWwPdp8glBiaPFCWQ==%(String)");
    }

    private const string SysUserColumns = "SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t";

    [Fact, Upstream("QueryTest12.java#testDoc9")]
    public async Task TestDoc9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => s.Blogs!.Any(u => u.Title!.Contains("123123"))).ToListAsync(Ct);
        listen.AssertSql(SysUserColumns + " WHERE EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`title` = t.`id` AND t1.`title` LIKE ? LIMIT 1)");
        listen.AssertParams("false(Boolean),%123123%(String)");
    }

    // Java 的 blogs().disableLogicDelete().any(…)：C# 在导航集合上写 DisableLogicDelete()
    [Fact, Upstream("QueryTest12.java#testDoc10")]
    public async Task TestDoc10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => s.Blogs!.DisableLogicDelete().Any(u => u.Title!.Contains("123123"))).ToListAsync(Ct);
        listen.AssertSql(SysUserColumns + " WHERE EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`title` = t.`id` AND t1.`title` LIKE ? LIMIT 1)");
        listen.AssertParams("%123123%(String)");
    }

    [Fact, Upstream("QueryTest12.java#testDoc11")]
    public async Task TestDoc11()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => s.MyBlog!.Title!.Contains("123123")).ToListAsync(Ct);
        listen.AssertSql(SysUserColumns + " LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t1.`id` = t.`id` WHERE t1.`title` LIKE ?");
        listen.AssertParams("false(Boolean),%123123%(String)");
    }

    // ON 里的 OR 组与加密列 LIKE
    [Fact, Upstream("QueryTest12.java#testDoc15")]
    public async Task TestDoc15()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().InnerJoin<SysUser>((b, s2) => b.Id == "1" && b.Id == s2.Id && (b.Title!.Contains("123") || s2.IdCard!.Contains("456")))
            .Where((b1, s2) => b1.Title!.Contains("1233")).ToListAsync(Ct);
        listen.AssertSql("SELECT " + Prefixed("t", BlogColumns) + " FROM `t_blog` t INNER JOIN `easy-query-test`.`t_sys_user` t1 ON t.`id` = ? AND t.`id` = t1.`id` AND (t.`title` LIKE ? OR t1.`id_card` LIKE ?) WHERE t.`deleted` = ? AND t.`title` LIKE ?");
        listen.AssertParams("1(String),%123%(String),%AdUseaKyj6EpwMyX7l1RQw==%(String),false(Boolean),%1233%(String)");
    }

    private const string RelationUserSelect = "SELECT t.`id`,t.`name` FROM `relation_user` t WHERE ";

    // 导航的固定条件与关联条件合成一组：(关联 AND 固定条件)。Java 没有建 relation_* 表（执行失败后只看 SQL），这里只比对 SQL
    [Fact, Upstream("QueryTest12.java#testDoc16")]
    public void TestDoc16() =>
        Assert.Equal(RelationUserSelect + "EXISTS(SELECT 1 FROM `relation_book` t1 WHERE (t1.`user_id` = t.`id` AND t1.`create_time` <= ?) AND t1.`name` LIKE ? LIMIT 1)",
            Db.Query<RelationUser>().Where(r => r.HistoryBooks!.Any(book => book.Name!.Contains("小学"))).ToSql());

    private const string TeachersAny = "EXISTS(SELECT 1 FROM `relation_teacher` t1 WHERE (EXISTS(SELECT 1 FROM `relation_route` t2 WHERE t2.`second_id` = t1.`id` AND t2.`first_id` = t.`id` AND t2.`type` = ? LIMIT 1) AND t1.`name` = ?) AND t1.`name` LIKE ? LIMIT 1)";

    // 多对多：中间表的固定条件写在中间表的 EXISTS 里
    [Fact, Upstream("QueryTest12.java#testDoc17")]
    public async Task TestDoc17()
    {
        using var listen = SqlCapture.Listen();
        var query = Db.Query<RelationUser>().Where(r => r.Teachers!.Any(book => book.Name!.Contains("小学")));
        Assert.Equal(RelationUserSelect + TeachersAny, query.ToSql());
        await Assert.ThrowsAnyAsync<Exception>(() => query.ToListAsync(Ct));
        listen.AssertParams("1(Integer),12345(String),%小学%(String)");
    }

    // 动态固定条件关闭时，只剩中间表条件
    [Fact, Upstream("QueryTest12.java#testDoc22")]
    public void TestDoc22()
    {
        using var scope = DynamicExtraFilter.CreateScope(false);
        Assert.Equal(RelationUserSelect + "EXISTS(SELECT 1 FROM `relation_teacher` t1 WHERE EXISTS(SELECT 1 FROM `relation_route` t2 WHERE t2.`second_id` = t1.`id` AND t2.`first_id` = t.`id` AND t2.`type` = ? LIMIT 1) AND t1.`name` LIKE ? LIMIT 1)",
            Db.Query<RelationUser>().Where(r => r.Teachers!.Any(book => book.Name!.Contains("小学"))).ToSql());
    }

    // Java 的 stringFormat("{0}{1}{2}", …)：C# 的 string.Format
    [Fact, Upstream("QueryTest12.java#testDoc19")]
    public async Task TestDoc19()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => string.Format("{0}{1}{2}", ",", s.IdCard, ",").Contains(",2,")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE CONCAT(?,`id_card`,?) LIKE ?");
        listen.AssertParams(",(String),,(String),%,2,%(String)");
    }

    // Java 的 concat(constant(1), idCard.toNumber(Integer), constant(2))：C# 的 string.Concat 与 Sql.Cast<int>
    [Fact, Upstream("QueryTest12.java#testDoc20")]
    public async Task TestDoc20()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => string.Concat(1, Sql.Cast<int>(s.IdCard), 2).Contains(",2,")).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE CONCAT(?,CAST(`id_card` AS SIGNED),?) LIKE ?");
        listen.AssertParams("1(Integer),2(Integer),%,2,%(String)");
    }

    // 删除里的关联子查询：表带别名 t
    [Fact, Upstream("QueryTest12.java#testDoc23")]
    public void TestDoc23() =>
        Assert.Equal("DELETE FROM `relation_user` t WHERE EXISTS(SELECT 1 FROM `relation_teacher` t1 WHERE EXISTS(SELECT 1 FROM `relation_user` t2 WHERE EXISTS(SELECT 1 FROM `relation_route` t3 WHERE t3.`first_id` = t2.`id` AND t3.`second_id` = t1.`id` LIMIT 1) AND t2.`id` = t.`id` LIMIT 1) AND t1.`name` LIKE ?)",
            Db.Delete<RelationUser>().Where(user => Sql.Exists(Db.Query<RelationTeacher>().Where(teach => teach.Users!.Any(u => u.Id == user.Id) && teach.Name!.Contains("小明")))).ToSql());

    // Java 的 teachers().flatElement().name().like(…)：在 WHERE 里等同于 any
    [Fact, Upstream("QueryTest12.java#testDoc24")]
    public void TestDoc24() =>
        Assert.Equal("DELETE FROM `relation_user` t WHERE " + TeachersAny.Replace("小学", "小明"),
            Db.Delete<RelationUser>().Where(user => user.Teachers!.Any(t => t.Name!.Contains("小明"))).ToSql());
}
