namespace EasyQuerySharp.Tests.Live;

// 查询：读值与解码、LIKE 转义、导航与隐式子查询、嵌套投影与对象树、include、分组、分页、复用、函数
public abstract partial class LiveSuite
{
    [Fact]
    public async Task ReadsRowsWithDecodedValues()
    {
        var db = await SeededAsync();
        var alice = await db.Query<User>().Where(u => u.Name == "alice").SingleAsync(Ct);
        Assert.Equal(1, alice.Id);
        Assert.Equal(["vip"], alice.Attrs!.Tags);
        Assert.Equal(CreatedAt, alice.CreatedAt);
        Assert.Null(await db.Query<User>().Where(u => u.Name == "nobody").FirstOrDefaultAsync(Ct));
    }

    // 日期截断到各粒度的起点；周从周一起（2026-01-02 是周五）
    [Fact]
    public async Task TruncatesDatesToEachUnit()
    {
        var db = await SeededAsync();
        var alice = db.Query<User>().Where(u => u.Name == "alice");
        async Task<DateTime> Trunc(DateTruncUnit unit) => await alice.Select(u => u.CreatedAt.DateTrunc(unit)).SingleAsync(Ct);
        Assert.Equal(new DateTime(2026, 1, 1), await Trunc(DateTruncUnit.Year));
        Assert.Equal(new DateTime(2026, 1, 1), await Trunc(DateTruncUnit.Quarter));
        Assert.Equal(new DateTime(2026, 1, 1), await Trunc(DateTruncUnit.Month));
        Assert.Equal(new DateTime(2025, 12, 29), await Trunc(DateTruncUnit.Week));
        Assert.Equal(new DateTime(2026, 1, 2), await Trunc(DateTruncUnit.Day));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 0, 0), await Trunc(DateTruncUnit.Hour));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 0), await Trunc(DateTruncUnit.Minute));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), await Trunc(DateTruncUnit.Second));
    }

    [Fact]
    public async Task EscapesLikeWildcardsInTheSearchedText()
    {
        var db = await SeededAsync();
        Assert.Equal(["orm_v2"], await db.Query<Post>().Where(p => p.Title.Contains("_")).Select(p => p.Title).ToListAsync(Ct));
        Assert.Equal(["sql 100%"], await db.Query<Post>().Where(p => p.Title.EndsWith("%")).Select(p => p.Title).ToListAsync(Ct));
    }

    // 忽略大小写的 LIKE：C# 没有 ignoreCase 参数，两边转成同一种大小写再比（正则匹配没有对应的 API）
    [Fact]
    public async Task LikesIgnoringCase()
    {
        var db = await SeededAsync();
        var users = db.Query<User>().OrderBy(u => u.Id);
#pragma warning disable CA1862 // 要翻译成 SQL：StringComparison 重载没有对应的 SQL，只能先转大写
        Assert.Equal(["alice"], await users.Where(u => u.Name.ToUpper().StartsWith("AL")).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(["bob", "carol"], await users.Where(u => u.Name.ToUpper().Contains("O")).Select(u => u.Name).ToListAsync(Ct));
#pragma warning restore CA1862
    }

    [Fact]
    public async Task FiltersThroughNavigationsAndImplicitSubqueries()
    {
        var db = await SeededAsync();
        var names = await db.Query<User>()
            .Where(u => u.Company!.Name == "acme" && u.Posts!.Any(p => p.Title.StartsWith("orm")))
            .OrderBy(u => u.Id)
            .Select(u => u.Name)
            .ToListAsync(Ct);
        Assert.Equal(["alice", "bob"], names);
        Assert.Equal(1, await db.Query<User>().Where(u => !u.Posts!.Any()).CountAsync(Ct));
    }

    [Fact]
    public async Task SelectsNullableNavigationsAndAggregates()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>()
            .OrderBy(u => u.Id)
            .Select(u => new UserStats(u.Name, u.Company!.Name, u.Posts!.Count(), u.Posts!.Count(p => p.Amount > 100), u.Posts!.Max(p => p.Title)))
            .ToListAsync(Ct);
        Assert.Equal(
        [
            new UserStats("alice", "acme", 2, 1, "sql 100%"),
            new UserStats("bob", "acme", 2, 0, "orm_v2"),
            new UserStats("carol", null, 0, 0, null),
        ], rows);
    }

    private sealed record UserStats(string Name, string? Company, long Posts, long Big, string? MaxTitle);

    // 对象树两层：每层一次查询，共三条语句
    [Fact]
    public async Task AssemblesObjectTreesNestedTwoLevels()
    {
        var db = await SeededAsync();
        using var listen = SqlCapture.Listen();
        var rows = await db.Query<User>()
            .Where(u => u.Company!.Name == "acme")
            .OrderBy(u => u.Id)
            .Select(u => new UserPosts(u.Name, u.Posts!
                .Where(p => p.Amount > 2)
                .OrderByDescending(p => p.Id)
                .Select(p => new PostTree(p.Title, p.User!.Name, p.Comments!.OrderBy(c => c.Id).Select(c => c.Body).ToList()))
                .ToList()))
            .ToListAsync(Ct);
        Assert.Equal(2, rows.Count);
        Assert.Equal("alice", rows[0].Name);
        Assert.Equal(["sql 100%", "orm tips"], rows[0].Posts.Select(p => p.Title));
        Assert.All(rows[0].Posts, p => Assert.Equal("alice", p.Author));
        Assert.Empty(rows[0].Posts[0].Comments);
        Assert.Equal(["nice", "thanks"], rows[0].Posts[1].Comments);
        Assert.Equal(["hello"], rows[1].Posts.Select(p => p.Title));
        Assert.Equal(3, listen.Events.Count);
    }

    private sealed record UserPosts(string Name, List<PostTree> Posts);

    private sealed record PostTree(string Title, string? Author, List<string> Comments);

    [Fact]
    public async Task IncludesNavigationsOnWholeRows()
    {
        var db = await SeededAsync();
        var users = await db.Query<User>()
            .Where(u => u.Name == "bob" || u.Name == "carol")
            .OrderBy(u => u.Id)
            .Include(u => u.Company)
            .Include(u => u.Posts, q => q.OrderBy(p => p.Id))
            .ToListAsync(Ct);
        Assert.Equal("acme", users[0].Company?.Name);
        Assert.Equal(["orm_v2", "hello"], users[0].Posts!.Select(p => p.Title));
        Assert.Null(users[1].Company);
        Assert.Empty(users[1].Posts!);
    }

    [Fact]
    public async Task GroupsWithConditionalAggregatesAndHaving()
    {
        var db = await SeededAsync();
        var rows = await db.Query<Post>()
            .GroupBy(p => p.UserId)
            .Having(g => g.Count() >= 2)
            .OrderBy(g => g.Key)
            .Select(g => new PostGroup(g.Key, g.Count(), g.Count(p => p.Title.StartsWith("orm")), g.Sum(p => p.Amount)))
            .ToListAsync(Ct);
        Assert.Equal([(1L, 2L, 1L), (2L, 2L, 1L)], rows.Select(r => (r.UserId, r.N, r.Orm)));
        Assert.Equal(2010.50m, rows[0].Total);
        Assert.Equal(2, await db.Query<Post>().GroupBy(p => p.UserId).Keys().CountAsync(Ct));
    }

    private sealed record PostGroup(long UserId, long N, long Orm, decimal Total);

    [Fact]
    public async Task PagesWithATotalCount()
    {
        var db = await SeededAsync();
        var page = await db.Query<Post>().OrderBy(p => p.Id).Select(p => p.Id).ToPageAsync(2, 3, Ct);
        Assert.Equal([4L], page.Rows);
        Assert.Equal(4, page.Total);
    }

    // 只跳过、不限条数：MySQL、SQLite、HANA 的 OFFSET 前必须有 LIMIT
    [Fact]
    public async Task SkipsRowsWithoutTakingACount()
    {
        var db = await SeededAsync();
        Assert.Equal([3L, 4L], await db.Query<Post>().OrderBy(p => p.Id).Skip(2).Select(p => p.Id).ToListAsync(Ct));
    }

    // 查询对象不可变：在同一个查询上再加条件不影响原查询
    [Fact]
    public async Task ReusesQueriesWithoutLeakingState()
    {
        var db = await SeededAsync();
        var orm = db.Query<Post>().Where(p => p.Title.StartsWith("orm"));
        Assert.Equal(2, await orm.CountAsync(Ct));
        Assert.Single(await orm.Where(p => p.User!.Name == "bob").ToListAsync(Ct));
        Assert.Equal(2, (await orm.ToListAsync(Ct)).Count);
        Assert.True(await orm.AnyAsync(Ct));
    }

    [Fact]
    public async Task CombinesAndOrNot()
    {
        var db = await SeededAsync();
        Assert.Equal(2, await db.Query<User>().Where(u => (u.Age >= 18 && u.Name == "alice") || u.Company == null).CountAsync(Ct));
        // NOT 走 SQL 的三值逻辑：年龄为 NULL 的 bob 既不满足条件也不满足它的否定
        Assert.Equal(1, await db.Query<User>().Where(u => !(u.Age >= 18)).CountAsync(Ct));
    }

    [Fact]
    public async Task UsesDialectFunctions()
    {
        var db = await SeededAsync();
        var r = await db.Query<User>()
            .Where(u => u.Id == 1)
            .Select(u => new Functions(u.Name.ToUpper(), u.Name.Length, u.CreatedAt.Year, u.Name + "@" + (u.Company!.Name ?? "-")))
            .SingleAsync(Ct);
        Assert.Equal(new Functions("ALICE", 5, 2026, "alice@acme"), r);
    }

    private sealed record Functions(string Upper, int Length, int Year, string Label);

    [Fact]
    public async Task RunsSubqueriesThroughDeepNavigation()
    {
        var db = await SeededAsync();
        Assert.Equal(["acme"], await db.Query<Company>().Where(c => c.Users!.Any(u => u.Posts!.Any(p => p.Comments!.Any()))).Select(c => c.Name).ToListAsync(Ct));
        Assert.Equal(2, await db.Query<Comment>().Where(c => c.Post!.User!.Company!.Name == "acme").CountAsync(Ct));
    }

    // UNION 的各分支在 SQLite 上不能加括号：方言决定写法，各库都能执行
    // 沿两级集合导航展开：公司 → 用户 → 帖子，条件、排序、投影都写在最后取出的帖子表上
    [Fact]
    public async Task FlattensCollectionNavigationsWithSelectMany()
    {
        var db = await SeededAsync();
        var titles = await db.Query<Company>().Where(c => c.Name == "acme").SelectMany(c => c.Users).SelectMany(u => u.Posts)
            .Where(p => p.Amount < 100).OrderBy(p => p.Title).Select(p => p.Title).ToListAsync(Ct);
        Assert.Equal(["hello", "orm tips", "orm_v2"], titles);
        var owners = await (from u in db.Query<User>() from p in u.Posts! select new { u.Name, p.Title }).CountAsync(Ct);
        Assert.Equal(4, owners);
    }

    [Fact]
    public async Task UnionsQueriesOnEveryDatabase()
    {
        var db = await SeededAsync();
        var names = await db.Query<User>().Where(u => u.Name == "alice").Select(u => new User { Id = u.Id, Name = u.Name })
            .Union(db.Query<User>().Where(u => u.Name == "bob").Select(u => new User { Id = u.Id, Name = u.Name }))
            .OrderBy(u => u.Name).Select(u => u.Name).ToListAsync(Ct);
        Assert.Equal(["alice", "bob"], names);
    }

    // 递归 CTE（树查询）的写法在各库上都能执行：起点两条评论；评论的 id 与 post_id 可能成环，用子节点条件让递归部分不走下去
    [Fact]
    public async Task RunsRecursiveTreeCte()
    {
        var db = await SeededAsync();
        var bodies = await db.Query<Comment>().AsTreeCte(c => c.Id, c => c.PostId, childFilter: c => c.Body == "none").Select(c => c.Body).ToListAsync(Ct);
        Assert.Equal(["nice", "thanks"], bodies.Order(StringComparer.Ordinal));
    }
}
