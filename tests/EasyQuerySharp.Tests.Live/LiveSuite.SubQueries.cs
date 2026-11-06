namespace EasyQuerySharp.Tests.Live;

// 子查询、派生表、union 与 CTE：分组合并、取第 i 个元素 / 全部满足、对一取一、IN / 标量子查询、窗口结果的筛选、并集、CTE 的连接与链式、树
public abstract partial class LiveSuite
{
    // 集合导航上的聚合合并成分组 LEFT JOIN 后，结果与相关子查询一致
    [Fact]
    public async Task GroupJoinMatchesTheCorrelatedSubqueries()
    {
        var db = await SeededAsync();
        async Task<List<PostAggregates>> Select(bool grouped)
        {
            var query = db.Query<User>();
            if (grouped) query = query.GroupJoin(u => u.Posts);
            return await query.OrderBy(u => u.Id)
                .Select(u => new PostAggregates(u.Name, u.Posts!.Count(), u.Posts!.Count(p => p.Title.StartsWith("orm")), u.Posts!.Sum(p => p.Amount)))
                .ToListAsync(Ct);
        }

        var rows = await Select(true);
        Assert.Equal(await Select(false), rows);
        // 没有帖子时和是 0（与 Java 一致，不是 NULL）
        Assert.Equal([("alice", 2L, 1L, 2010.5m), ("bob", 2L, 1L, 4.25m), ("carol", 0L, 0L, 0m)], rows.Select(r => (r.Name, r.Posts, r.Orm, r.Total)));
    }

    private sealed record PostAggregates(string Name, long Posts, long Orm, decimal Total);

    // 全部满足（carol 没有帖子也算）、按排序取第 i 个、去重计数
    [Fact]
    public async Task UsesAllElementAtAndDistinctCountOnNavigations()
    {
        var db = await SeededAsync();
        var all = await db.Query<User>().Where(u => u.Posts!.All(p => p.Amount > 2m)).OrderBy(u => u.Id).Select(u => u.Name).ToListAsync(Ct);
        Assert.Equal(["alice", "carol"], all);

        var rows = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new Draft<string?, string?, long>(
                u.Posts!.OrderByDescending(p => p.Id).ElementAt(0).Title,
                u.Posts!.OrderBy(p => p.Id).ElementAt(1).Title,
                u.Posts!.Select(p => p.UserId).Distinct().Count()))
            .ToListAsync(Ct);
        Assert.Equal([("sql 100%", "sql 100%", 1L), ("hello", "hello", 1L), (null, null, 0L)], rows.Select(r => (r.Value1, r.Value2, r.Value3)));

        var byFirst = await db.Query<User>().Where(u => u.Posts!.OrderBy(p => p.Id).ElementAt(0).Title.StartsWith("orm")).Select(u => u.Name).ToListAsync(Ct);
        Assert.Equal(["alice", "bob"], byFirst.Order(StringComparer.Ordinal));
    }

    // 对一导航取"最新一条"（TS 的 oneOfMany）：带出时按导航上的排序与条数取
    [Fact]
    public async Task IncludesTheLatestRowOfAToManyRelation()
    {
        var db = await SeededAsync();
        var included = await db.Query<User>().OrderBy(u => u.Id).Include(u => u.LatestPost).ToListAsync(Ct);
        Assert.Equal(["sql 100%", "hello", null], included.Select(u => u.LatestPost?.Title));
    }

    // 同一个导航写在查询里（隐式 JOIN）：应当读起来像对一，每个用户一行
    [Fact]
    public async Task ReadsTheLatestRowOfAToManyRelationInQueries()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id).Select(u => new Draft<string, string?>(u.Name, u.LatestPost!.Title)).ToListAsync(Ct);
        Assert.Equal([("alice", "sql 100%"), ("bob", "hello"), ("carol", null)], rows.Select(r => (r.Value1, r.Value2)));
        Assert.Equal(["bob"], await db.Query<User>().Where(u => u.LatestPost!.Title == "hello").Select(u => u.Name).ToListAsync(Ct));

        // 与 null 比较：条件、投影、隐式子查询
        Assert.Equal(["carol"], await db.Query<User>().Where(u => u.LatestPost == null).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal([true, true, false], await db.Query<User>().OrderBy(u => u.Id).Select(u => u.LatestPost != null).ToListAsync(Ct));
        Assert.Equal(["acme"], await db.Query<Company>().Where(c => c.Users!.Any(u => u.LatestPost != null)).Select(c => c.Name).ToListAsync(Ct));
    }

    // IN / NOT IN 一个子查询，以及标量子查询（金额高于平均值 503.6875 的只有 2000.00 那篇）
    [Fact]
    public async Task UsesASubQueryInInAndAsAScalar()
    {
        var db = await SeededAsync();
        var users = db.Query<User>().OrderBy(u => u.Id);
        Assert.Equal(["alice", "bob"], await users.Where(u => u.Id.In(db.Query<Post>().Select(p => p.UserId))).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(["carol"], await users.Where(u => u.Id.NotIn(db.Query<Post>().Select(p => p.UserId))).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(["alice"], await users.Where(u => u.Id.In(db.Query<Post>().Where(p => p.Amount > 100m).Select(p => p.UserId))).Select(u => u.Name).ToListAsync(Ct));

        var ids = await db.Query<Post>().Where(p => p.Amount > Sql.SubQuery(db.Query<Post>().Select(x => Sql.Avg(x.Amount)))).Select(p => p.Id).ToListAsync(Ct);
        Assert.Equal([2L], ids);
    }

    // 窗口结果放进派生表再筛选（QUALIFY 的写法）：每个用户金额最高的一篇；派生表里先限条数再在外层排序
    [Fact]
    public async Task FiltersOnWindowResultsThroughADerivedTable()
    {
        var db = await SeededAsync();
        Query<Draft<long, long, long>> Top() => db.Query<Post>()
            .Select(p => new Draft<long, long, long>(p.Id, p.UserId, Sql.Over.RowNumber(w => w.PartitionBy(p.UserId).OrderByDescending(p.Amount))))
            .AsSubQuery();

        // alice 的 2000.00（id 2）、bob 的 3.00（id 4）
        Assert.Equal([2L, 4L], await Top().Where(t => t.Value3 == 1).OrderBy(t => t.Value1).Select(t => t.Value1).ToListAsync(Ct));
        Assert.Equal(2, await Top().Where(t => t.Value3 == 1).CountAsync(Ct));
        var page = await Top().OrderBy(t => t.Value1).Select(t => t.Value1).ToPageAsync(2, 3, Ct);
        Assert.Equal([4L], page.Rows);

        var firstTwo = await db.Query<Post>().OrderBy(p => p.Id).Take(2).Select(p => new Draft<long>(p.Id)).AsSubQuery().OrderByDescending(t => t.Value1).ToListAsync(Ct);
        Assert.Equal([2L, 1L], firstTwo.Select(t => t.Value1));
    }

    // 两个查询的列按位置对齐：UNION 去重、UNION ALL 保留重复；多个并起来后排序、跳过、限条数
    [Fact]
    public async Task UnionsAndUnionAllsQueries()
    {
        var db = await SeededAsync();
        var adults = db.Query<User>().Where(u => u.Age > 20).Select(u => new Draft<string, int?>(u.Name, u.Age));
        var minors = db.Query<User>().Where(u => u.Age < 20).Select(u => new Draft<string, int?>(u.Name, u.Age));

        var both = adults.Union(minors);
        Assert.Equal([("alice", 30), ("carol", 17)], (await both.OrderBy(r => r.Value1).ToListAsync(Ct)).Select(r => (r.Value1, r.Value2)));
        Assert.Equal(2, await both.CountAsync(Ct));
        Assert.Equal(1, await adults.Union(adults).CountAsync(Ct));
        Assert.Equal(2, await adults.UnionAll(adults).CountAsync(Ct));

        // alice、carol、alice 排序后跳过第一行再取两行
        var three = adults.UnionAll(minors).UnionAll(adults);
        var names = await three.OrderBy(r => r.Value1).Skip(1).Take(2).ToListAsync(Ct);
        Assert.Equal(["alice", "carol"], names.Select(r => r.Value1));
    }

    // 投影成一个值的查询合并、分组之后，按值本身过滤、排序、取段
    [Fact]
    public async Task OrdersScalarResultsByTheValueItself()
    {
        var db = await SeededAsync();
        var names = db.Query<User>().Select(u => u.Name);
        var titles = db.Query<Post>().Where(p => p.UserId == 2).Select(p => p.Title);
        Assert.Equal(["alice", "bob", "carol", "hello", "orm_v2"], await names.UnionAll(titles).OrderBy(n => n).ToListAsync(Ct));
        Assert.Equal([2L, 1L], await db.Query<Post>().GroupBy(p => p.UserId).Keys().Where(k => k > 0).OrderByDescending(k => k).Take(2).ToListAsync(Ct));
    }

    // 分支自己排序、取段（SQLite 不允许分支带 ORDER BY / LIMIT，包一层再合并）
    [Fact]
    public async Task UnionsBranchesThatAreOrderedAndLimited()
    {
        var db = await SeededAsync();
        var oldest = db.Query<User>().Where(u => u.Age != null).OrderByDescending(u => u.Age).Take(1).Select(u => new Draft<string>(u.Name));
        var first = db.Query<User>().OrderBy(u => u.Id).Take(1).Select(u => new Draft<string>(u.Name));
        Assert.Equal(["alice", "alice"], (await oldest.UnionAll(first).ToListAsync(Ct)).Select(r => r.Value1));

        // 带出按父对象取段的 UNION ALL 模式（Java 的 includeLimitMode）：每个父对象一个分支
        var unionAll = await SeededAsync(o => o.IncludeLimitMode = IncludeLimitMode.UnionAll);
        var users = await unionAll.Query<User>().OrderBy(u => u.Id)
            .Include(u => u.Posts, q => q.OrderByDescending(p => p.Amount).Take(1)).ToListAsync(Ct);
        Assert.Equal([["sql 100%"], ["hello"], []], users.Select(u => u.Posts!.Select(p => p.Title).ToArray()));
    }

    // CTE 和真实的表连接，第二个 CTE 建立在第一个之上
    [Fact]
    public async Task RegistersCtesAndJoinsThemChainingOneOnAnother()
    {
        var db = await SeededAsync();
        var big = db.Query<Post>().Where(p => p.Amount > 1m).Select(p => new Draft<long, decimal>(p.UserId, p.Amount)).AsCte("big");
        var totals = big.GroupBy(b => b.Value1).Select(g => new Draft<long, decimal>(g.Key, g.Sum(b => b.Value2))).AsCte("totals");
        var rows = await db.Query<User>()
            .InnerJoin(totals, (u, t) => t.Value1 == u.Id)
            .Where((u, t) => u.Name != "nobody")
            .OrderBy((u, t) => u.Id)
            .Select((u, t) => new Draft<string, decimal>(u.Name, t.Value2))
            .ToListAsync(Ct);
        Assert.Equal([("alice", 2010.5m), ("bob", 4.25m)], rows.Select(r => (r.Value1, r.Value2)));
    }

    // 自关联的树：1 总部 ─┬─ 2 研发 ── 4 前端；└─ 3 销售；5 子公司（另一棵树）
    [Fact]
    public async Task WalksASelfReferencingTree()
    {
        SkipUnless(Options.RecursiveCte, "递归 CTE");
        var db = await SeededAsync();
        await db.Insert(
            new Dept { Id = 1, ParentId = null, Name = "hq" },
            new Dept { Id = 2, ParentId = 1, Name = "rd" },
            new Dept { Id = 3, ParentId = 1, Name = "sales" },
            new Dept { Id = 4, ParentId = 2, Name = "web" },
            new Dept { Id = 5, ParentId = null, Name = "sub" }).ExecuteAsync(Ct);

        var tree = await db.Query<Dept>().Where(d => d.Id == 1).AsTreeCte().OrderBy(d => d.Id).ToTreeAsync(Ct);
        Assert.Equal([("hq", new[] { "rd", "sales" })], tree.Select(n => (n.Name, n.Children!.Select(c => c.Name).Order(StringComparer.Ordinal).ToArray())));

        // 往上走：起点自己也在结果里
        var ancestors = await db.Query<Dept>().Where(d => d.Id == 4).AsTreeCte(o => o.Up = true).OrderBy(d => d.Id).Select(d => d.Name).ToListAsync(Ct);
        Assert.Equal(["hq", "rd", "web"], ancestors);
    }
}
