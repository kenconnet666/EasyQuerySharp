namespace EasyQuerySharp.Tests.Live;

// 显式连接：内 / 左 / 右连接、派生表、笛卡尔积、三表连接分页、连接表上的导航、分组聚合、自连接
public abstract partial class LiveSuite
{
    // carol 没有帖子，内连接里不出现
    [Fact]
    public async Task InnerJoinsTwoTablesAndSelectsFromBoth()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>()
            .InnerJoin<Post>((u, p) => p.UserId == u.Id)
            .OrderBy((u, p) => p.Id)
            .Select((u, p) => new Draft<string, string>(u.Name, p.Title))
            .ToListAsync(Ct);
        Assert.Equal([("alice", "orm tips"), ("alice", "sql 100%"), ("bob", "orm_v2"), ("bob", "hello")], rows.Select(r => (r.Value1, r.Value2)));
    }

    [Fact]
    public async Task LeftJoinsAndKeepsRowsWithoutAMatch()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>()
            .LeftJoin<Post>((u, p) => p.UserId == u.Id)
            .OrderBy((u, p) => u.Id).ThenBy((u, p) => p.Id)
            .Select((u, p) => new Draft<string, string?>(u.Name, p.Title))
            .ToListAsync(Ct);
        Assert.Equal(5, rows.Count);
        Assert.Equal(("carol", null), (rows[^1].Value1, rows[^1].Value2));
    }

    // globex 没有员工：右连接里照样出现，用户一侧为 null
    [Fact]
    public async Task RightJoinsAndKeepsEveryRowOfTheNewTable()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>()
            .RightJoin<Company>((u, c) => c.Id == u.CompanyId)
            .OrderBy((u, c) => c.Id).ThenBy((u, c) => u.Id)
            .Select((u, c) => new Draft<string?, string>(u.Name, c.Name))
            .ToListAsync(Ct);
        Assert.Equal([("alice", "acme"), ("bob", "acme"), (null, "globex")], rows.Select(r => (r.Value1, r.Value2)));
    }

    // 子查询作为派生表连接；没有帖子的 carol 读成 null
    [Fact]
    public async Task JoinsASubQueryAsADerivedTable()
    {
        var db = await SeededAsync();
        var stats = db.Query<Post>().GroupBy(p => p.UserId).Select(g => new Draft<long, long, decimal>(g.Key, g.Count(), g.Max(p => p.Amount)));
        var rows = await db.Query<User>()
            .LeftJoin(stats, (u, s) => s.Value1 == u.Id)
            .OrderBy((u, s) => u.Id)
            .Select((u, s) => new Draft<string, long?, decimal?>(u.Name, (long?)s.Value2, (decimal?)s.Value3))
            .ToListAsync(Ct);
        Assert.Equal([("alice", 2L, 2000m), ("bob", 2L, 3m), ("carol", null, null)], rows.Select(r => (r.Value1, r.Value2, r.Value3)));

        var many = await db.Query<User>()
            .InnerJoin(stats, (u, s) => s.Value1 == u.Id)
            .Where((u, s) => s.Value2 >= 2)
            .OrderBy((u, s) => u.Id)
            .Select((u, s) => u.Name)
            .ToListAsync(Ct);
        Assert.Equal(["alice", "bob"], many);
    }

    // 笛卡尔积：每一对，条件写在 WHERE 里
    [Fact]
    public async Task CrossJoinsEveryPair()
    {
        var db = await SeededAsync();
        var pairs = await db.Query<Company>().From<Company>().Where((a, b) => a.Id < b.Id).Select((a, b) => new Draft<string, string>(a.Name, b.Name)).ToListAsync(Ct);
        Assert.Equal([("acme", "globex")], pairs.Select(p => (p.Value1, p.Value2)));
    }

    // 'nice' 与 'thanks' 都含 n，都挂在 alice 的第 1 篇帖子上
    [Fact]
    public async Task JoinsThreeTablesFiltersOnJoinedTablesAndPages()
    {
        var db = await SeededAsync();
        var query = db.Query<User>()
            .InnerJoin<Post>((u, p) => p.UserId == u.Id)
            .InnerJoin<Comment>((u, p, c) => c.PostId == p.Id)
            .Where((u, p, c) => u.Name == "alice" && c.Body.Contains("n"))
            .OrderBy((u, p, c) => c.Id);
        var bodies = await query.Select((u, p, c) => c.Body).ToListAsync(Ct);
        Assert.Equal(["nice", "thanks"], bodies);
        Assert.Equal(2, await query.CountAsync(Ct));
        Assert.Equal(2, (await query.Select((u, p, c) => new Draft<long>(u.Id)).ToPageAsync(1, 1, Ct)).Total);
    }

    // p.User 是 Post 上的多对一导航，隐式 JOIN 排在显式 JOIN 之后
    [Fact]
    public async Task UsesNavigationsOfAJoinedTable()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>()
            .InnerJoin<Post>((u, p) => p.UserId == u.Id)
            .Where((u, p) => p.User!.Name == "bob")
            .Select((u, p) => new Draft<string, string?>(p.Title, p.User!.Name))
            .ToListAsync(Ct);
        Assert.Equal(["bob", "bob"], rows.Select(r => r.Value2));
    }

    [Fact]
    public async Task GroupsAndAggregatesAcrossJoinedTables()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>()
            .InnerJoin<Post>((u, p) => p.UserId == u.Id)
            .GroupBy((u, p) => u.Name)
            .OrderBy(g => g.Key)
            .Select(g => new Draft<string, long, decimal>(g.Key, g.Count(), g.Sum(e => e.Item2.Amount)))
            .ToListAsync(Ct);
        Assert.Equal([("alice", 2L, 2010.5m), ("bob", 2L, 4.25m)], rows.Select(r => (r.Value1, r.Value2, r.Value3)));

        var bobTotal = await db.Query<User>().InnerJoin<Post>((u, p) => p.UserId == u.Id).Where(u => u.Name == "bob").SumAsync((u, p) => p.Amount, Ct);
        Assert.Equal(4.25m, bobTotal);
    }

    // 自连接：同公司的不同用户配对
    [Fact]
    public async Task JoinsTheSameEntityTwice()
    {
        var db = await SeededAsync();
        var pairs = await db.Query<User>()
            .InnerJoin<User>((a, b) => b.CompanyId == a.CompanyId && b.Id != a.Id)
            .OrderBy((a, b) => a.Id).ThenBy((a, b) => b.Id)
            .Select((a, b) => new Draft<string, string>(a.Name, b.Name))
            .ToListAsync(Ct);
        Assert.Equal([("alice", "bob"), ("bob", "alice")], pairs.Select(p => (p.Value1, p.Value2)));
    }
}
