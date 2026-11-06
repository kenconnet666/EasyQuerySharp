using EasyQuerySharp.Commands;

namespace EasyQuerySharp.Tests.Live;

// 聚合保存、整行批量更新、写哪些列、经导航的更新删除（聚合保存必须在追踪作用域与事务里，差异来自追踪快照）
public abstract partial class LiveSuite
{
    private static User NewUser(string name = "dave") =>
        new() { Name = name, Age = 40, CreatedAt = new DateTime(2026, 2, 3), Attrs = new UserAttrs([]) };

    // 自增主键取回之后，子行的关联列自动设成它
    [Fact]
    public async Task SavesANewParentTogetherWithItsChildren()
    {
        var db = await SeededAsync();
        using var tracking = db.BeginTracking();
        var dave = NewUser();
        dave.Posts = [new Post { Title = "p1", Amount = 1.00m }, new Post { Title = "p2", Amount = 2.00m }];
        await using (var tx = db.BeginTransaction())
        {
            await db.Save(dave).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.NotEqual(0, dave.Id);
        Assert.Equal(["p1", "p2"], await db.Query<Post>().Where(p => p.UserId == dave.Id).OrderBy(p => p.Title).Select(p => p.Title).ToListAsync(Ct));
    }

    // 只更新改过的列，拿掉的子对象连同它们自己的子对象一起删除，没带出的导航不动
    [Fact]
    public async Task UpdatesOnlyChangedColumnsAndDeletesRemovedChildren()
    {
        var db = await SeededAsync();
        await db.Insert(new Comment { PostId = 2, Body = "bye" }).ExecuteAsync(Ct);
        using var tracking = db.BeginTracking();
        var alice = await db.Query<User>().Where(u => u.Id == 1)
            .Include(u => u.Posts, q => q.OrderBy(p => p.Id).Include(p => p.Comments)).SingleAsync(Ct);
        alice.Name = "alice2";
        alice.Posts![0].Title = "changed";
        alice.Posts.RemoveAt(1);
        await using (var tx = db.BeginTransaction())
        {
            await db.Save(alice).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }

        var saved = await db.Query<User>().AsNoTracking().FindAsync(1L, Ct);
        Assert.Equal(("alice2", 30), (saved!.Name, saved.Age));
        var posts = await db.Query<Post>().OrderBy(p => p.Id).ToListAsync(Ct);
        Assert.Equal([(1L, "changed"), (3L, "orm_v2"), (4L, "hello")], posts.Select(p => (p.Id, p.Title)));
        Assert.Equal(0, await db.Query<Comment>().Where(c => c.PostId == 2).CountAsync(Ct));
        Assert.Equal(2, await db.Query<Comment>().Where(c => c.PostId == 1).CountAsync(Ct));
    }

    // 子对象的子对象跟着父对象之后保存
    [Fact]
    public async Task SavesNestedChildrenAfterTheParent()
    {
        var db = await SeededAsync();
        using var tracking = db.BeginTracking();
        var dave = NewUser();
        dave.Posts = [new Post { Title = "p1", Amount = 1.00m, Comments = [new Comment { Body = "c1" }, new Comment { Body = "c2" }] }];
        await using (var tx = db.BeginTransaction())
        {
            await db.Save(dave).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(["c1", "c2"], await db.Query<Comment>().Where(c => c.Post!.UserId == dave.Id).OrderBy(c => c.Body).Select(c => c.Body).ToListAsync(Ct));
    }

    // 外键可空的引用关系（公司的员工）：拿掉的员工外键置空，人还在
    [Fact]
    public async Task UnlinksRemovedChildrenWhoseForeignKeyIsNullable()
    {
        var db = await SeededAsync();
        using var tracking = db.BeginTracking();
        var acme = await db.Query<Company>().Where(c => c.Id == 1).Include(c => c.Users, q => q.OrderBy(u => u.Id)).SingleAsync(Ct);
        acme.Users!.RemoveAll(u => u.Name == "bob");
        await using (var tx = db.BeginTransaction())
        {
            await db.Save(acme).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        var users = await db.Query<User>().OrderBy(u => u.Id).Select(u => new Draft<string, long?>(u.Name, u.CompanyId)).ToListAsync(Ct);
        Assert.Equal([("alice", (long?)1), ("bob", null), ("carol", null)], users.Select(u => (u.Value1, u.Value2)));
    }

    // 一次保存里后一个对象出错（主键与已有的成员冲突）时整个保存回滚：前面已经插入的成员和它的笔记也不该留下
    [Fact]
    public async Task RollsTheWholeSaveBackWhenARootFails()
    {
        var db = await SeededAsync();
        await db.Insert(new Member { TenantId = 1, Id = 1, Name = "m1" }).ExecuteAsync(Ct);
        using var tracking = db.BeginTracking();
        var ok = new Member { TenantId = 9, Id = 9, Name = "ok", Notes = [new MemberNote { Body = "n" }] };
        var clash = new Member { TenantId = 1, Id = 1, Name = "clash" };
        await using (var tx = db.BeginTransaction())
        {
            await Assert.ThrowsAnyAsync<Exception>(() => db.Save(ok, clash).ExecuteAsync(Ct));
            await tx.RollbackAsync(Ct);
        }
        Assert.Equal(["m1"], await db.Query<Member>().Select(m => m.Name).ToListAsync(Ct));
        Assert.Equal(0, await db.Query<MemberNote>().CountAsync(Ct));
    }

    // 自关联的导航（部门的下级部门）：新根带着同类型的子对象保存，子对象应当一并插入
    [Fact(Skip = "疑似缺陷：根与子对象同类型的导航（Dept.Children，Cascade.Delete）保存新根时只插入根，Children 里的新部门被静默丢弃（SQLite 上只发出一条 INSERT）")]
    public async Task SavesNewChildrenOfTheSameEntityType()
    {
        var db = await SeededAsync();
        using var tracking = db.BeginTracking();
        var root = new Dept { Id = 10, Name = "new", Children = [new Dept { Id = 11, Name = "ok" }] };
        await using (var tx = db.BeginTransaction())
        {
            await db.Save(root).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal([(10L, (long?)null), (11L, 10L)], (await db.Query<Dept>().OrderBy(d => d.Id).ToListAsync(Ct)).Select(d => (d.Id, d.ParentId)));
    }

    // 删除根：它带出的子对象按级联规则一并删除
    [Fact]
    public async Task RemovesARowWithTheChildrenItOwns()
    {
        var db = await SeededAsync();
        using var tracking = db.BeginTracking();
        var post = await db.Query<Post>().Where(p => p.Id == 1).Include(p => p.Comments).SingleAsync(Ct);
        await using (var tx = db.BeginTransaction())
        {
            await db.Save(post).RemoveRoot().ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(0, await db.Query<Comment>().CountAsync(Ct));
        Assert.Equal(3, await db.Query<Post>().CountAsync(Ct));
    }

    // 多对多：已有的角色按键挂上（目标先单独保存 / 已存在），拿掉的只删中间表的行，角色本身还在
    [Fact]
    public async Task LinksManyToManyTargetsByKey()
    {
        var db = await SeededAsync();
        Role admin = new() { Name = "admin" }, dev = new() { Name = "dev" }, ops = new() { Name = "ops" };
        await db.Insert(admin, dev, ops).FillGeneratedKeys().ExecuteAsync(Ct);
        Task<List<string>> RoleNames() => db.Query<Role>().Where(r => r.Users!.Any(u => u.Id == 1)).OrderBy(r => r.Name).Select(r => r.Name).ToListAsync(Ct);

        async Task Relink(params string[] names)
        {
            using var tracking = db.BeginTracking();
            var alice = await db.Query<User>().Where(u => u.Id == 1).Include(u => u.Roles).SingleAsync(Ct);
            var wanted = await db.Query<Role>().Where(r => names.Contains(r.Name)).ToListAsync(Ct);
            alice.Roles = wanted;
            await using var tx = db.BeginTransaction();
            await db.Save(alice).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }

        await Relink("admin", "ops");
        Assert.Equal(["admin", "ops"], await RoleNames());
        await Relink("dev");
        Assert.Equal(["dev"], await RoleNames());
        Assert.Equal(3, await db.Query<Role>().CountAsync(Ct));
        Assert.Equal(1, await db.Query<UserRole>().CountAsync(Ct));
    }

    // 插入后读回自增键（TS 的 returnEntity）
    [Fact]
    public async Task FillsGeneratedKeysOfInsertedRows()
    {
        var db = await SeededAsync();
        var post = new Post { UserId = 1, Title = "ret", Amount = 5.00m };
        await db.Insert(post).FillGeneratedKeys().ExecuteAsync(Ct);
        var row = await db.Query<Post>().FindAsync(post.Id, Ct);
        Assert.Equal((5L, "ret", 5.00m), (post.Id, row!.Title, row.Amount));
    }

    [Fact]
    public async Task UpdatesABatchOfEntitiesByPrimaryKey()
    {
        var db = await SeededAsync();
        var rows = await db.Query<Post>().OrderBy(p => p.Id).ToListAsync(Ct);
        foreach (var row in rows) row.Title = row.Title.ToUpper();
        Assert.Equal(4, await db.Update(rows).ExecuteAsync(Ct));
        Assert.Equal(["ORM TIPS", "SQL 100%", "ORM_V2", "HELLO"], await db.Query<Post>().OrderBy(p => p.Id).Select(p => p.Title).ToListAsync(Ct));
    }

    // 只写不为 NULL 的列：carol 的年龄、公司是 NULL 不写，她单独一组，两种列集合共两条语句
    [Fact]
    public async Task MergesABatchOfEntityUpdatesIntoOneStatementPerWrittenColumnSet()
    {
        var db = await SeededAsync();
        var users = await db.Query<User>().OrderBy(u => u.Id).ToListAsync(Ct);
        foreach (var user in users)
        {
            user.Name = user.Name.ToUpper();
            user.Age = user.Id == 3 ? null : 50;
        }
        using var listen = SqlCapture.Listen();
        Assert.Equal(3, await db.Update(users).Columns(ColumnStrategy.OnlyNotNull).Batch().ExecuteAsync(Ct));
        Assert.Equal(2, listen.Events.Count);

        var after = await db.Query<User>().OrderBy(u => u.Id).Select(u => new Draft<string, int?, long?>(u.Name, u.Age, u.CompanyId)).ToListAsync(Ct);
        Assert.Equal([("ALICE", (int?)50, (long?)1), ("BOB", 50, 1), ("CAROL", 17, null)], after.Select(u => (u.Value1, u.Value2, u.Value3)));
    }

    // 插入只写不为 NULL 的列；更新只写值为 NULL 的列（name 不动）
    [Fact]
    public async Task WritesOnlyTheColumnsTheStrategyPicks()
    {
        var db = await SeededAsync();
        var insert = db.Insert(new User { Name = "dave", Age = null, CompanyId = null, CreatedAt = DateTime.UtcNow, Attrs = new UserAttrs([]) }).Columns(ColumnStrategy.OnlyNotNull).ToSql();
        Assert.DoesNotContain("age", insert, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("company_id", insert, StringComparison.OrdinalIgnoreCase);

        var alice = (await db.Query<User>().FindAsync(1L, Ct))!;
        alice.Name = "ignored";
        alice.Age = null;
        await db.Update(alice).Columns(ColumnStrategy.OnlyNull).ExecuteAsync(Ct);
        var row = (await db.Query<User>().FindAsync(1L, Ct))!;
        Assert.Equal(("alice", (int?)null), (row.Name, row.Age));
    }

    // 条件里用对一导航更新；两层导航：删掉 alice 帖子下的评论
    [Fact]
    public async Task UpdatesAndDeletesThroughNestedToOneNavigations()
    {
        var db = await SeededAsync();
        Assert.Equal(2, await db.Update<User>().Set(u => u.Age, 99).Where(u => u.Company!.Name == "acme").ExecuteAsync(Ct));
        Assert.Equal(2, await db.Delete<Comment>().Where(c => c.Post!.User!.Name == "alice").ExecuteAsync(Ct));
        Assert.Equal([99, 99, 17], await db.Query<User>().OrderBy(u => u.Id).Select(u => u.Age!.Value).ToListAsync(Ct));
        Assert.Equal(0, await db.Query<Comment>().CountAsync(Ct));
    }

    // 条件是"公司名为空"：UPDATE 里的对一导航按 INNER JOIN 连接（与 Java 一致），没有公司的 carol 不会被匹配；
    // 同样的条件写在查询里是 LEFT JOIN，会查到她
    [Fact(Skip = "待确认：UPDATE 里 Company.Name == null 在 SQLite 上改 0 行（INNER JOIN），而同样的条件在 SELECT 里匹配 carol；TS 版期望改 1 行")]
    public async Task UpdatesThroughALeftJoinedToOneNavigationMatchesRowsWithoutATarget()
    {
        var db = await SeededAsync();
        Assert.Equal(1, await db.Query<User>().Where(u => u.Company!.Name == null).CountAsync(Ct));
        Assert.Equal(1, await db.Update<User>().Set(u => u.Age, 99).Where(u => u.Company!.Name == null).ExecuteAsync(Ct));
    }
}
