namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：mapping/TableNameMappingSqlTest（按实体换表，主表、隐式 JOIN、集合导航的子查询都跟着换）。
/// Java 的映射是一个作用域（进入时生效、退出时恢复，可以嵌套）；C# 写成查询上的 <c>MapTable&lt;T&gt;(..)</c>，查询对象不可变，
/// 加了映射的是一条新查询，原来的不受影响，所以"嵌套与恢复"就是在已映射的查询上再映射、原查询照旧。
/// </summary>
public sealed class TableNameMappingSqlTest
{
    private static EasyDb Db => SqlOnly.Db("mysql");

    private const string UserColumns = "`id`,`name`,`age`,`company_id`,`attrs`,`created_at`";
    private const string AliasedUserColumns = "t.`id`,t.`name`,t.`age`,t.`company_id`,t.`attrs`,t.`created_at`";

    // Java 传入空映射；C# 没有调用 MapTable 就是空映射，映射查询里用不到的实体同样不改变 SQL
    [Fact, Upstream("fork/mapping/TableNameMappingSqlTest.java#emptyMappingChangesNothing")]
    public void EmptyMappingChangesNothing()
    {
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user`", Db.Query<ForkUser>().ToSql());
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user`", Db.Query<ForkUser>().MapTable<ForkPost>("t_fork_post_archive").ToSql());
    }

    // 显式的 AsTable 优先于映射，与调用的先后无关
    [Fact, Upstream("fork/mapping/TableNameMappingSqlTest.java#explicitAsTableWins")]
    public void ExplicitAsTableWins()
    {
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user_2025`",
            Db.Query<ForkUser>().MapTable<ForkUser>("t_fork_user_archive").AsTable("t_fork_user_2025").ToSql());
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user_2025`",
            Db.Query<ForkUser>().AsTable("t_fork_user_2025").MapTable<ForkUser>("t_fork_user_archive").ToSql());
    }

    [Fact, Upstream("fork/mapping/TableNameMappingSqlTest.java#mainTableJoinAndSubqueriesFollowTheMapping")]
    public void MainTableJoinAndSubqueriesFollowTheMapping() =>
        SqlOnly.AssertStatement(
            Db.Query<ForkUser>().MapTable<ForkUser>("t_fork_user_archive").MapTable<ForkCompany>("t_fork_company_archive").MapTable<ForkPost>("t_fork_post_archive")
                .Where(u => u.Company!.Name == "acme" && u.Posts!.Any(p => p.Title == "hello"))
                .Select(u => new Draft<string?, int>(u.Id, u.Posts!.Count())).ToStatement(),
            "SELECT t.`id` AS `value1`,(SELECT COUNT(*) FROM `t_fork_post_archive` t3 WHERE t3.`user_id` = t.`id`) AS `value2` FROM `t_fork_user_archive` t LEFT JOIN `t_fork_company_archive` t1 ON t1.`id` = t.`company_id` WHERE t1.`name` = ? AND EXISTS(SELECT 1 FROM `t_fork_post_archive` t2 WHERE t2.`user_id` = t.`id` AND t2.`title` = ? LIMIT 1)",
            "acme(String),hello(String)");

    [Fact, Upstream("fork/mapping/TableNameMappingSqlTest.java#mappedTableNameIsReadable",
        Deviation = "Java 在映射作用域里读出当前生效的表名；C# 的映射是查询对象上的不可变状态，没有作用域可读，映射后的表名从 ToSql() 体现，实体元数据里的表名不变")]
    public void MappedTableNameIsReadable()
    {
        var mapped = Db.Query<ForkUser>().MapTable<ForkUser>(name => name + "_archive");
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user_archive`", mapped.ToSql());
        Assert.Equal("t_fork_user", Db.Metadata.Get<ForkUser>().TableName);
    }

    [Fact, Upstream("fork/mapping/TableNameMappingSqlTest.java#onlyMappedEntitiesChange")]
    public void OnlyMappedEntitiesChange() =>
        SqlOnly.AssertStatement(
            Db.Query<ForkUser>().MapTable<ForkPost>("t_fork_post_archive").Where(u => u.Posts!.Any(p => p.Title == "hello")).ToStatement(),
            $"SELECT {AliasedUserColumns} FROM `t_fork_user` t WHERE EXISTS(SELECT 1 FROM `t_fork_post_archive` t1 WHERE t1.`user_id` = t.`id` AND t1.`title` = ? LIMIT 1)",
            "hello(String)");

    // 外层映射帖子、内层再映射用户：内层两者都换；外层与原查询不受内层影响
    [Fact, Upstream("fork/mapping/TableNameMappingSqlTest.java#scopesNestAndRestore")]
    public void ScopesNestAndRestore()
    {
        var query = Db.Query<ForkUser>().Where(u => u.Posts!.Any());
        var outer = query.MapTable<ForkPost>("t_fork_post_archive");
        var inner = outer.MapTable<ForkUser>("t_fork_user_other");
        Assert.Equal($"SELECT {AliasedUserColumns} FROM `t_fork_user_other` t WHERE EXISTS(SELECT 1 FROM `t_fork_post_archive` t1 WHERE t1.`user_id` = t.`id` LIMIT 1)", inner.ToSql());
        Assert.Equal($"SELECT {AliasedUserColumns} FROM `t_fork_user` t WHERE EXISTS(SELECT 1 FROM `t_fork_post_archive` t1 WHERE t1.`user_id` = t.`id` LIMIT 1)", outer.ToSql());
        Assert.Equal($"SELECT {AliasedUserColumns} FROM `t_fork_user` t WHERE EXISTS(SELECT 1 FROM `t_fork_post` t1 WHERE t1.`user_id` = t.`id` LIMIT 1)", query.ToSql());
        // 内层的映射覆盖外层同一实体的映射
        Assert.Equal($"SELECT {AliasedUserColumns} FROM `t_fork_user` t WHERE EXISTS(SELECT 1 FROM `t_fork_post_2025` t1 WHERE t1.`user_id` = t.`id` LIMIT 1)",
            outer.MapTable<ForkPost>("t_fork_post_2025").ToSql());
    }
}
