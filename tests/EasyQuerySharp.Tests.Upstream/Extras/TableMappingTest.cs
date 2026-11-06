using Company = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.Company;
using SysRole = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.SysRole;
using SysUser = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.SysUser;
using UserBook = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.UserBook;
using UserRole = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.UserRole;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 查询级换表 <c>MapTable</c> / <c>InSchema</c> 与连接符改写 <c>WithTableLink</c> 在各种翻译路径上的效果（fork 用例之外的 C# 用例）：
/// 多对多的中间表、分组合并、取集合的某一行、显式子查询（外层的映射作用于子查询，子查询自己的优先）、派生表、导航上的 <c>Sql.AsTable</c>。
/// </summary>
public sealed class TableMappingTest
{
    private static EasyDb Db => SqlOnly.Db("mysql");

    [Fact]
    public void ManyToManyMappingTableFollowsTheMapping() =>
        Assert.Equal("SELECT t.`id` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_role_2025` t1 WHERE EXISTS(SELECT 1 FROM `t_user_role_2025` t2 WHERE t2.`role_id` = t1.`id` AND t2.`user_id` = t.`id` LIMIT 1) AND t1.`name` = ? LIMIT 1)",
            Db.Query<SysUser>().MapTable<UserRole>("t_user_role_2025").MapTable<SysRole>("t_role_2025").Where(u => u.Roles!.Any(r => r.Name == "admin")).Select(u => u.Id).ToSql());

    [Fact]
    public void GroupJoinsFollowTheMapping() =>
        Assert.Equal("SELECT t.`id` FROM `t_user` t LEFT JOIN (SELECT t1.`user_id` AS `__group_key1__`,COUNT(*) AS `__count2__`,(COUNT((CASE WHEN t1.`name` = ? THEN ? ELSE NULL END)) > 0) AS `__any3__` FROM `t_user_book_2025` t1 GROUP BY t1.`user_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) > ? AND IFNULL(t2.`__any3__`,?) = ?",
            Db.Query<SysUser>().GroupJoinAll().MapTable<UserBook>("t_user_book_2025").Where(u => u.Books!.Count() > 1 && u.Books!.Any(b => b.Name == "x")).Select(u => u.Id).ToSql());

    [Fact]
    public void ElementJoinsFollowTheMapping() =>
        Assert.Equal("SELECT t.`id` FROM `t_user` t LEFT JOIN (SELECT t1.`id`,t1.`user_id`,t1.`name`,t1.`create_time`,t1.`author`,(ROW_NUMBER() OVER (PARTITION BY t1.`user_id` ORDER BY t1.`create_time` ASC)) AS `__row__` FROM `t_user_book_2025` t1) t3 ON (t3.`user_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`name` = ?",
            Db.Query<SysUser>().MapTable<UserBook>("t_user_book_2025").Where(u => u.Books!.OrderBy(b => b.CreateTime).First().Name == "x").Select(u => u.Id).ToSql());

    // 显式子查询沿用外层的映射；子查询自己映射的同一实体以它的为准
    [Fact]
    public void ExplicitSubQueriesInheritTheMapping()
    {
        Assert.Equal("SELECT t.`id` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_user_book_2025` t1 WHERE t1.`user_id` = t.`id`)",
            Db.Query<SysUser>().MapTable<UserBook>("t_user_book_2025").Where(u => Sql.Exists(Db.Query<UserBook>().Where(b => b.UserId == u.Id))).Select(u => u.Id).ToSql());
        Assert.Equal("SELECT t.`id` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_user_book_2024` t1 WHERE t1.`user_id` = t.`id`)",
            Db.Query<SysUser>().MapTable<UserBook>("t_user_book_2025").Where(u => Sql.Exists(Db.Query<UserBook>().MapTable<UserBook>("t_user_book_2024").Where(b => b.UserId == u.Id))).Select(u => u.Id).ToSql());
    }

    // 投影之后再加条件：外层包成派生表，内外都换
    [Fact]
    public void WrappedQueriesKeepTheMapping() =>
        Assert.Equal("SELECT t1.`id`,t1.`name` FROM (SELECT t.`id`,t.`name` FROM `t_user_2025` t) t1 WHERE t1.`name` = ?",
            Db.Query<SysUser>().MapTable<SysUser>("t_user_2025").Select(u => new { u.Id, u.Name }).Where(x => x.Name == "a").ToSql());

    // 导航上显式写的 Sql.AsTable 优先于映射
    [Fact]
    public void NavigationAsTableWins() =>
        Assert.Equal("SELECT t.`id` FROM `t_user` t LEFT JOIN `t_company_x` t1 ON t1.`id` = t.`company_id` WHERE t1.`name` = ?",
            Db.Query<SysUser>().MapTable<Company>("t_company_2025").Where(u => Sql.AsTable(u.Company, "t_company_x")!.Name == "a").Select(u => u.Id).ToSql());

    // 整条查询换 schema：主表、隐式 JOIN、子查询、中间表都放到同一个 schema 下
    [Fact]
    public void InSchemaMovesEveryTable() =>
        Assert.Equal("SELECT t.`id` FROM `archive`.`t_user` t LEFT JOIN `archive`.`t_company` t1 ON t1.`id` = t.`company_id` WHERE t1.`name` = ? AND EXISTS(SELECT 1 FROM `archive`.`t_role` t2 WHERE EXISTS(SELECT 1 FROM `archive`.`t_user_role` t3 WHERE t3.`role_id` = t2.`id` AND t3.`user_id` = t.`id` LIMIT 1) LIMIT 1)",
            Db.Query<SysUser>().InSchema("archive").Where(u => u.Company!.Name == "a" && u.Roles!.Any()).Select(u => u.Id).ToSql());

    [Fact]
    public void TableLinkRewritesTheConnector()
    {
        Assert.Equal("SELECT t1.`name` FROM ONLY `t_user` t LEFT OUTER JOIN `t_user_book` t1 ON t.`id` = t1.`user_id`",
            Db.Query<SysUser>().WithTableLink(from => from + " ONLY").LeftJoin<UserBook>((u, b) => u.Id == b.UserId).WithTableLink("LEFT OUTER JOIN").Select((u, b) => b.Name).ToSql());
        Assert.Equal("INSERT INTO /*link*/ `t_user_book` (`id`,`name`) VALUES (?,?)", Db.Insert(new UserBook { Id = "1", Name = "a" }).WithTableLink("/*link*/ ").ToSql());
        Assert.Equal("UPDATE /*link*/ `t_user_book` SET `name` = ? WHERE `id` = ?", Db.Update<UserBook>().Set(b => b.Name, "a").Where(b => b.Id == "1").WithTableLink(_ => "/*link*/ ").ToSql());
        Assert.Equal("DELETE FROM /*link*/ `t_user_book` WHERE `id` = ?", Db.Delete<UserBook>().Where(b => b.Id == "1").WithTableLink("/*link*/ ").ToSql());
        // 走导航的更新改写成带连接的写法，目标表没有单独的位置放连接符：明确报不支持
        Assert.Throws<NotSupportedException>(() => Db.Update<SysUser>().Set(u => u.Name, "a").Where(u => u.Company!.Name == "x").WithTableLink("/*link*/ ").ToSql());
    }
}
