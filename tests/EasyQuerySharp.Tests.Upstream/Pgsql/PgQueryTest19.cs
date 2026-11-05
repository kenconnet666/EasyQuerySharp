namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>
/// 对应 Java：pgsql/QueryTest19.java 的树查询部分（asTreeCTE / toTreeList，C# 的 AsTreeCte / ToTreeAsync）。
/// 分类数据：1 → 3 → 5、6；1 → 4。
/// </summary>
public sealed class PgQueryTest19(PgFixture fixture) : PgTest(fixture)
{
    // 1 的下面有 3（3 下面有 5、6）和 4
    private static void AssertSubTreeOf1(IReadOnlyList<MyCategory> children)
    {
        Assert.Equal(2, children.Count);
        foreach (var child in children)
        {
            if (child.Id == "3") Assert.All(child.Children!, c => { Assert.Contains(c.Id, new[] { "5", "6" }); Assert.Empty(c.Children!); });
            else Assert.Equal("4", child.Id);
            Assert.Equal(child.Id == "3" ? 2 : 0, child.Children!.Count);
        }
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testToTreeList")]
    public async Task TestToTreeList()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte().ToTreeAsync(Ct);
        var root = Assert.Single(tree);
        Assert.Equal("1", root.Id);
        AssertSubTreeOf1(root.Children!);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\") ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t.\"cte_deep\" FROM \"as_tree_cte\" t");
        listen.AssertParams("1(String)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testToTreeList2")]
    public async Task TestToTreeList2()
    {
        using var listen = SqlCapture.Listen();
        AssertSubTreeOf1(await Db.Query<MyCategory>().Where(m => m.ParentId == "1").AsTreeCte().ToTreeAsync(Ct));
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"parent_id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\") ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t.\"cte_deep\" FROM \"as_tree_cte\" t");
        listen.AssertParams("1(String)");
    }

    // Java 的 setChildFilter：AsTreeCte 的 childFilter 只作用于递归走到的行；后半段是与树无关的聚合
    [Fact, Upstream("pgsql/QueryTest19.java#testToTreeList3")]
    public async Task TestToTreeList3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte(childFilter: child => child.Name!.Contains("123")).ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\" WHERE t3.\"name\" LIKE ?) ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t.\"cte_deep\" FROM \"as_tree_cte\" t");
        listen.AssertParams("1(String),%123%(String)");
        var max = await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("123")).MaxAsync(b => b.CreateTime, Ct);
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("123")).Select(b => new Draft<long, DateTime?>(Sql.Count(b.Id), Sql.Max(b.CreateTime))).ToListAsync(Ct);
        await Db.Query<BlogEntity>().Where(b => b.Content!.Contains("123")).GroupBy(b => b.Star).Having(g => g.Max(x => x.CreateTime) < DateTime.Now).Keys().ToListAsync(Ct);
        Assert.True(max is null || max > DateTime.MinValue);
    }

    // 树查询之后联表、分组：外层引用 CTE，之后的表从 t6 起编号
    [Fact, Upstream("pgsql/QueryTest19.java#tree9")]
    public async Task Tree9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte().LeftJoin<BlogEntity>((m, b2) => m.Id == b2.Id)
            .GroupBy((m1, _) => m1.Name).Select(g => new Draft<string?, int?>(g.Key, g.Sum(e => e.Item2.Star))).ToListAsync(Ct);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\") ) SELECT t.\"name\" AS \"value1\",SUM(t6.\"star\") AS \"value2\" FROM \"as_tree_cte\" t LEFT JOIN \"t_blog\" t6 ON t6.\"deleted\" = ? AND t.\"id\" = t6.\"id\" GROUP BY t.\"name\"");
        listen.AssertParams("1(String),false(Boolean)");
    }

    // 启用了默认关闭的拦截器 aaa：基础部分、递归部分的两张表、外层都带上 1=1（Java 把 CTE 引用也当实体表）
    [Fact, Upstream("pgsql/QueryTest19.java#tree10")]
    public async Task Tree10()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MyCategory>().Where(m => m.Id == "1").EnableInterceptor("aaa").AsTreeCte().OrderByDescending(m => m.Id).ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE 1=1 AND t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON 1=1 AND t3.\"parent_id\" = t2.\"id\" WHERE 1=1) ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t.\"cte_deep\" FROM \"as_tree_cte\" t WHERE 1=1 ORDER BY t.\"id\" DESC");
        listen.AssertParams("1(String)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#tree10_1"), Upstream("pgsql/QueryTest19.java#tree11")]
    public async Task Tree10_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MyCategory>().Where(m => m.Id == "1").EnableInterceptor("aaa").DisableInterceptors().AsTreeCte().OrderByDescending(m => m.Id).ToTreeAsync(Ct);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\") ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t.\"cte_deep\" FROM \"as_tree_cte\" t ORDER BY t.\"id\" DESC");
        listen.AssertParams("1(String)");
    }

    // 树查询作为 IN 子查询：WITH 提到整条语句最前面
    [Fact, Upstream("pgsql/QueryTest19.java#tree12")]
    public async Task Tree12()
    {
        using var listen = SqlCapture.Listen();
        var ids = Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte().Select(m => m.Id);
        await Db.Query<BlogEntity>().Where(b => Sql.In(b.Id, ids)).ToListAsync(Ct);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t2.\"id\",t2.\"parent_id\",t2.\"name\" FROM \"category\" t2 WHERE t2.\"id\" = ?)  UNION ALL  (SELECT t3.\"cte_deep\" + 1 AS \"cte_deep\",t4.\"id\",t4.\"parent_id\",t4.\"name\" FROM \"as_tree_cte\" t3 INNER JOIN \"category\" t4 ON t4.\"parent_id\" = t3.\"id\") ) SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"id\" IN (SELECT t1.\"id\" FROM \"as_tree_cte\" t1)");
        listen.AssertParams("1(String),false(Boolean)");
    }

    // 树结果投影成 DTO：DTO 上指向自身的导航决定父子，层级列跟在投影之后
    [Fact, Upstream("pgsql/QueryTest19.java#testToTreeListDTO")]
    public async Task TestToTreeListDTO()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte()
            .Select(m => new MyCategoryDTO { Id = m.Id, ParentId = m.ParentId, Name = m.Name }).ToTreeAsync(Ct);
        var root = Assert.Single(tree);
        Assert.Equal("1", root.Id);
        Assert.Equal(2, root.Children!.Count);
        Assert.All(root.Children, c => Assert.Equal(c.Id == "3" ? 2 : 0, c.Children!.Count));
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"cte_deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\") ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t.\"cte_deep\" FROM \"as_tree_cte\" t");
        listen.AssertParams("1(String)");
    }

    private const string NoDeepInProjection = "Java 的 DTO 属性名与层级列同名时自动带上层级；C# 的投影里引用不到层级列，DTO 的 Deep1 不赋值，SQL 相同";

    // 树结果再连接一张表，投影成 DTO 后拼树
    [Fact, Upstream("pgsql/QueryTest19.java#tree7_1"), Upstream("pgsql/QueryTest19.java#tree7_2", Deviation = NoDeepInProjection)]
    public async Task Tree7_1()
    {
        using var listen = SqlCapture.Listen();
        var tree = await Db.Query<MyCategory>().Where(m => m.Id == "1").AsTreeCte(o => o.DeepColumnName = "deep")
            .LeftJoin<MyCategory>((m, b2) => m.Id == b2.Id)
            .Select((m1, m2) => new MyCategoryVO4 { Id = m1.Id, ParentId = m1.ParentId, Name = m1.Name, JoinName = m2.Name }).ToTreeAsync(Ct);
        Assert.Single(tree);
        listen.AssertSql("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0 AS \"deep\",t1.\"id\",t1.\"parent_id\",t1.\"name\" FROM \"category\" t1 WHERE t1.\"id\" = ?)  UNION ALL  (SELECT t2.\"deep\" + 1 AS \"deep\",t3.\"id\",t3.\"parent_id\",t3.\"name\" FROM \"as_tree_cte\" t2 INNER JOIN \"category\" t3 ON t3.\"parent_id\" = t2.\"id\") ) SELECT t.\"id\",t.\"parent_id\",t.\"name\",t6.\"name\" AS \"join_name\",t.\"deep\" FROM \"as_tree_cte\" t LEFT JOIN \"category\" t6 ON t.\"id\" = t6.\"id\"");
        listen.AssertParams("1(String)");
    }
}
