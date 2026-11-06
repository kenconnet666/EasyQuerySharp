using SysUser = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 沿集合导航展开 <c>SelectMany</c>（C# 的写法，对应 Java 的 <c>toList(x -&gt; x.a().flatElement()…)</c>）：INNER JOIN，多对多经中间表，保留重复。
/// </summary>
public sealed class SelectManyTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact]
    public void OneToManyJoinsTheTarget() =>
        Assert.Equal("SELECT t1.`id`,t1.`user_id`,t1.`name`,t1.`create_time`,t1.`author` FROM `t_user` t INNER JOIN `t_user_book` t1 ON t1.`user_id` = t.`id` WHERE t.`name` = ?",
            Db.Query<SysUser>().Where(u => u.Name == "a").SelectMany(u => u.Books).ToSql());

    // 连着写的 SelectMany、之后的条件、排序、投影都直接作用在最后连接的表上，不包派生表
    [Fact]
    public void ManyToManyJoinsThroughTheMappingTable() =>
        Assert.Equal("SELECT DISTINCT t4.`route` FROM `t_user` t INNER JOIN `t_user_role` t1 ON t1.`user_id` = t.`id` INNER JOIN `t_role` t2 ON t2.`id` = t1.`role_id` INNER JOIN `t_role_menu` t3 ON t3.`role_id` = t2.`id` INNER JOIN `t_menu` t4 ON t4.`id` = t3.`menu_id` WHERE t4.`route` LIKE ? ORDER BY t4.`route` ASC",
            Db.Query<SysUser>().SelectMany(u => u.Roles).SelectMany(r => r.Menus).Where(m => m.Route!.StartsWith("/a")).OrderBy(m => m.Route).Select(m => m.Route).Distinct().ToSql());

    // 分页之后再展开：先把分页的查询包成派生表
    [Fact]
    public void PagedOwnersAreWrappedBeforeJoining() =>
        Assert.Equal("SELECT t2.`id`,t2.`user_id`,t2.`name`,t2.`create_time`,t2.`author` FROM (SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time` FROM `t_user` t ORDER BY t.`id` ASC LIMIT 10) t1 INNER JOIN `t_user_book` t2 ON t2.`user_id` = t1.`id`",
            Db.Query<SysUser>().OrderBy(u => u.Id).Take(10).SelectMany(u => u.Books).ToSql());

    [Fact]
    public void QuerySyntaxProjectsOwnerAndElement()
    {
        var query = from u in Db.Query<SysUser>()
                    from r in u.Roles!
                    where r.Name != null
                    select new { User = u.Name, Role = r.Name };
        // 查询语法经过透明标识符（new { u, r }），之后的条件包一层派生表
        Assert.Equal("SELECT t3.`name` AS `user`,t3.`name1` AS `role` FROM (SELECT t.`id`,t.`company_id`,t.`name`,t.`age`,t.`create_time`,t2.`id` AS `id1`,t2.`name` AS `name1`,t2.`create_time` AS `create_time1` FROM `t_user` t INNER JOIN `t_user_role` t1 ON t1.`user_id` = t.`id` INNER JOIN `t_role` t2 ON t2.`id` = t1.`role_id`) t3 WHERE t3.`name1` IS NOT NULL",
            query.ToSql());
    }
}
