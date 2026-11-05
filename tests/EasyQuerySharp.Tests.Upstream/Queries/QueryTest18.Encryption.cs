namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>QueryTest18 里用到 SysUser 加密列（phone）的用例：条件值先加密，LIKE 值按片段加密后再加 %。</summary>
public sealed partial class QueryTest18
{
    private const string SysUserPagedInner = "SELECT t.`id`,t.`create_time`,t.`username`,t.`phone`,t.`id_card`,t.`address` FROM `easy-query-test`.`t_sys_user` t WHERE ";

    // Java 的 limitSelect(offset, limit, selector)：先分页再投影（分页的查询作为派生表）
    [Fact, Upstream("QueryTest18.java#testSubCountPage")]
    public async Task TestSubCountPage()
    {
        var q = Db.Query<SysUser>().Where(s => s.Phone!.StartsWith("133"));
        using (var listen = SqlCapture.Listen())
        {
            await q.CountAsync(Ct);
            listen.AssertSql("SELECT COUNT(*) FROM `easy-query-test`.`t_sys_user` WHERE `phone` LIKE ?");
            listen.AssertParams("3wApxVPL9GhKXR9/YKKGBg==%(String)");
        }
        using (var listen = SqlCapture.Listen())
        {
            await q.LimitSelect(2, 10, s => new Draft<string?, string?, long>(s.Id, s.Phone, s.Blogs!.Count())).ToListAsync(Ct);
            listen.AssertSql("SELECT t1.`id` AS `value1`,t1.`phone` AS `value2`,(SELECT COUNT(*) FROM `t_blog` t2 WHERE t2.`deleted` = ? AND t2.`title` = t1.`id`) AS `value3` FROM (" + SysUserPagedInner + "t.`phone` LIKE ? LIMIT 10 OFFSET 2) t1");
            listen.AssertParams("false(Boolean),3wApxVPL9GhKXR9/YKKGBg==%(String)");
        }
    }

    // Java 的 toPageSelectResult(q -> q.select(…), 2, 10)：C# 的 ToPageAsync(2, 10, selector)
    [Fact, Upstream("QueryTest18.java#testSubCountPage1")]
    public async Task TestSubCountPage1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => s.Phone != "123").OrderByDescending(s => s.CreateTime)
            .ToPageAsync(2, 10, s => new Draft<string?, string?, long>(s.Id, s.Phone, s.Blogs!.Count()), Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `easy-query-test`.`t_sys_user` WHERE `phone` <> ?");
        listen.AssertParams(0, "L/vVSy7H9DYkzz3srmSVCQ==(String)");
        listen.AssertSql(1, "SELECT t1.`id` AS `value1`,t1.`phone` AS `value2`,(SELECT COUNT(*) FROM `t_blog` t2 WHERE t2.`deleted` = ? AND t2.`title` = t1.`id`) AS `value3` FROM (" + SysUserPagedInner + "t.`phone` <> ? ORDER BY t.`create_time` DESC LIMIT 10 OFFSET 10) t1");
        listen.AssertParams(1, "false(Boolean),L/vVSy7H9DYkzz3srmSVCQ==(String)");
    }

    [Fact, Upstream("QueryTest18.java#testSubCountLimitSelect1")]
    public async Task TestSubCountLimitSelect1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysUser>().Where(s => s.Phone != "123").OrderByDescending(s => s.CreateTime)
            .LimitSelect(20, 10, s => new Draft<string?, string?, long>(s.Id, s.Phone, s.Blogs!.Count())).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id` AS `value1`,t1.`phone` AS `value2`,(SELECT COUNT(*) FROM `t_blog` t2 WHERE t2.`deleted` = ? AND t2.`title` = t1.`id`) AS `value3` FROM (" + SysUserPagedInner + "t.`phone` <> ? ORDER BY t.`create_time` DESC LIMIT 10 OFFSET 20) t1");
        listen.AssertParams("false(Boolean),L/vVSy7H9DYkzz3srmSVCQ==(String)");
    }
}
