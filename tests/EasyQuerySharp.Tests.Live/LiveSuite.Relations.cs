namespace EasyQuerySharp.Tests.Live;

// 多对多与复合外键：带固定条件的导航、直达导航、经中间表过滤 / 计数 / 对象树、复合键按每一列连接
public abstract partial class LiveSuite
{
    // alice：admin + dev；bob：dev；carol 没有角色；返回库里的 admin / dev / ops 三个角色
    private async Task<EasyDb> SeededWithRolesAsync()
    {
        var db = await SeededAsync();
        Role[] roles = [new() { Name = "admin" }, new() { Name = "dev" }, new() { Name = "ops" }];
        await db.Insert(roles).FillGeneratedKeys().ExecuteAsync(Ct);
        await db.Insert(
            new UserRole { UserId = 1, RoleId = roles[0].Id },
            new UserRole { UserId = 1, RoleId = roles[1].Id },
            new UserRole { UserId = 2, RoleId = roles[1].Id }).ExecuteAsync(Ct);
        return db;
    }

    // 导航上的固定条件（TS 的 manyOn）：只算大额帖子，计数、存在、列表、分组合并都按这个条件
    [Fact]
    public async Task ReadsRelationsWithAFixedCondition()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new BigPosts(u.Name, u.BigPosts!.Count(), u.BigPosts!.Any(), u.BigPosts!.Select(p => p.Title).ToList()))
            .ToListAsync(Ct);
        Assert.Equal(
            [("alice", 1L, true, new[] { "sql 100%" }), ("bob", 0L, false, []), ("carol", 0L, false, [])],
            rows.Select(r => (r.Name, r.Count, r.Any, r.Titles.ToArray())));

        var grouped = await db.Query<User>().GroupJoin(u => u.BigPosts).OrderBy(u => u.Id)
            .Select(u => new Draft<string, long>(u.Name, u.BigPosts!.Count()))
            .ToListAsync(Ct);
        Assert.Equal([("alice", 1L), ("bob", 0L), ("carol", 0L)], grouped.Select(r => (r.Value1, r.Value2)));
    }

    private sealed record BigPosts(string Name, long Count, bool Any, List<string> Titles);

    // 直达导航（TS 的 pathOne）：帖子经作者到公司；沿两级集合导航展开再去重（TS 的 path 桥）
    [Fact]
    public async Task ReadsPathRelations()
    {
        var db = await SeededWithRolesAsync();
        var post = await db.Query<Post>().Where(p => p.Id == 1).Select(p => new Draft<string, string?>(p.Title, p.Company!.Name)).SingleAsync(Ct);
        Assert.Equal(("orm tips", "acme"), (post.Value1, post.Value2));

        // 和 alice 有共同角色的人（含自己）
        var peers = await db.Query<User>().Where(u => u.Name == "alice").SelectMany(u => u.Roles).SelectMany(r => r.Users)
            .Select(u => u.Name).Distinct().ToListAsync(Ct);
        Assert.Equal(["alice", "bob"], peers.Order(StringComparer.Ordinal));
    }

    // 直达的对多导航写在投影里：条件、排序、取段作用在路径最后一段的目标上（acme 金额大于 2 的帖子里最贵的两篇）
    [Fact]
    public async Task ProjectsFilteredDirectCollections()
    {
        var db = await SeededAsync();
        var rows = await db.Query<Company>().OrderBy(c => c.Id)
            .Select(c => new Draft<string, List<string>>(c.Name, c.Posts!.Where(p => p.Amount > 2m).OrderByDescending(p => p.Amount).Take(2).Select(p => p.Title).ToList()))
            .ToListAsync(Ct);
        Assert.Equal([("acme", "sql 100%,orm tips"), ("globex", "")], rows.Select(r => (r.Value1, string.Join(",", r.Value2 ?? []))));
    }

    [Fact]
    public async Task FiltersAndCountsThroughTheJunctionTable()
    {
        var db = await SeededWithRolesAsync();
        var users = db.Query<User>().OrderBy(u => u.Id);
        Assert.Equal(["alice", "bob"], await users.Where(u => u.Roles!.Any(r => r.Name == "dev")).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(["alice"], await users.Where(u => u.Roles!.Any(r => r.Name == "admin")).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(["bob", "carol"], await users.Where(u => !u.Roles!.Any(r => r.Name == "admin")).Select(u => u.Name).ToListAsync(Ct));
        var counts = await users.Select(u => new Draft<string, long>(u.Name, u.Roles!.Count())).ToListAsync(Ct);
        Assert.Equal([("alice", 2L), ("bob", 1L), ("carol", 0L)], counts.Select(c => (c.Value1, c.Value2)));
    }

    // 对象树经中间表带出，并取每个用户排序后的第一个角色
    [Fact]
    public async Task BuildsObjectTreesThroughTheJunctionTable()
    {
        var db = await SeededWithRolesAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new UserRoles(u.Name, u.Roles!.OrderBy(r => r.Name).Select(r => r.Name).ToList(), u.Roles!.OrderByDescending(r => r.Name).First().Name))
            .ToListAsync(Ct);
        Assert.Equal(
            [("alice", new[] { "admin", "dev" }, "dev"), ("bob", ["dev"], "dev"), ("carol", [], null)],
            rows.Select(r => (r.Name, r.Roles.ToArray(), r.Last)));
    }

    private sealed record UserRoles(string Name, List<string> Roles, string? Last);

    [Fact]
    public async Task WorksFromTheOtherSideToo()
    {
        var db = await SeededWithRolesAsync();
        var roles = await db.Query<Role>().OrderBy(r => r.Id).Include(r => r.Users, q => q.OrderBy(u => u.Id)).ToListAsync(Ct);
        Assert.Equal(
            [("admin", new[] { "alice" }), ("dev", ["alice", "bob"]), ("ops", [])],
            roles.Select(r => (r.Name, r.Users!.Select(u => u.Name).ToArray())));
    }

    // m1 与 m3 的 id 相同、租户不同：只比较 id 会把租户 2 的笔记算到 m1 头上
    [Fact]
    public async Task MatchesCompositeForeignKeysOnEveryColumn()
    {
        var db = await SeededAsync();
        await db.Insert(
            new Member { TenantId = 1, Id = 1, Name = "m1" },
            new Member { TenantId = 1, Id = 2, Name = "m2" },
            new Member { TenantId = 2, Id = 1, Name = "m3" }).ExecuteAsync(Ct);
        await db.Insert(
            new MemberNote { TenantId = 1, MemberId = 1, Body = "a" },
            new MemberNote { TenantId = 1, MemberId = 1, Body = "b" },
            new MemberNote { TenantId = 2, MemberId = 1, Body = "c" },
            new MemberNote { TenantId = 1, MemberId = 2, Body = "d" }).ExecuteAsync(Ct);

        var members = await db.Query<Member>().OrderBy(m => m.TenantId).ThenBy(m => m.Id)
            .Select(m => new MemberNotes(m.Name, m.Notes!.Count(), m.Notes!.OrderBy(n => n.Body).Select(n => n.Body).ToList()))
            .ToListAsync(Ct);
        Assert.Equal(
            [("m1", 2L, new[] { "a", "b" }), ("m2", 1L, ["d"]), ("m3", 1L, ["c"])],
            members.Select(m => (m.Name, m.Count, m.Notes.ToArray())));

        // 多对一也按整组列连接
        var owners = await db.Query<MemberNote>().OrderBy(n => n.Body).Select(n => new Draft<string, string?>(n.Body, n.Member!.Name)).ToListAsync(Ct);
        Assert.Equal([("a", "m1"), ("b", "m1"), ("c", "m3"), ("d", "m2")], owners.Select(o => (o.Value1, o.Value2)));
        Assert.Equal(1, await db.Query<Member>().Where(m => m.Notes!.Any(n => n.Body == "c")).CountAsync(Ct));
    }

    private sealed record MemberNotes(string Name, long Count, List<string> Notes);
}
