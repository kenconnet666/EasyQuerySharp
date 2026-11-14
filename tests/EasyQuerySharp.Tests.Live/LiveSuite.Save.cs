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

    // 已有的父对象上新增子对象：非空外键还是默认值 0（没设）时当作新挂上的，不当作属于别的父对象（Java 里没设的外键是 null）
    [Fact]
    public async Task AddsChildrenToALoadedParentWithoutSettingTheirForeignKeys()
    {
        var db = await SeededAsync();
        Task<User> LoadAliceAsync() => db.Query<User>().Where(u => u.Id == 1)
            .Include(u => u.Posts, q => q.OrderBy(p => p.Id).Include(p => p.Comments)).SingleAsync(Ct);
        using (db.BeginTracking())
        {
            var alice = await LoadAliceAsync();
            alice.Posts!.Add(new Post { Title = "added", Amount = 3.00m, Comments = [new Comment { Body = "first" }] });
            alice.Posts[0].Comments!.Add(new Comment { Body = "more" });
            await using var tx = db.BeginTransaction();
            await db.Save(alice).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        var added = await db.Query<Post>().Where(p => p.Title == "added").SingleAsync(Ct);
        Assert.Equal(1L, added.UserId);
        Assert.Equal(["first"], await db.Query<Comment>().Where(c => c.PostId == added.Id).Select(c => c.Body).ToListAsync(Ct));
        Assert.Equal(["more", "nice", "thanks"], await db.Query<Comment>().Where(c => c.PostId == 1).OrderBy(c => c.Body).Select(c => c.Body).ToListAsync(Ct));

        // 外键设成了别的父对象时照样报错
        using (db.BeginTracking())
        {
            var alice = await LoadAliceAsync();
            alice.Posts!.Add(new Post { UserId = 2, Title = "stolen", Amount = 1.00m });
            await using var tx = db.BeginTransaction();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Save(alice).ExecuteAsync(Ct));
            Assert.Contains(nameof(SaveBehavior.AllowOwnershipChange), ex.Message);
        }

        // 新对象带着别人的子对象的主键（表单提交的 DTO，外键没设）：报错，不改别人的数据，也不悄悄忽略主键另建一行
        var bobsPost = await db.Query<Post>().Where(p => p.UserId == 2).OrderBy(p => p.Id).FirstAsync(Ct);
        foreach (var loadBob in new[] { false, true })
            using (db.BeginTracking())
            {
                if (loadBob) await db.Query<User>().Where(u => u.Id == 2).Include(u => u.Posts).SingleAsync(Ct);
                var alice = await LoadAliceAsync();
                alice.Posts!.Add(new Post { Id = bobsPost.Id, Title = "hijacked", Amount = 1.00m });
                await using var tx = db.BeginTransaction();
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Save(alice).ExecuteAsync(Ct));
                Assert.Contains(loadBob ? nameof(SaveBehavior.AllowOwnershipChange) : "自增主键", ex.Message);
            }
        var after = await db.Query<Post>().Where(p => p.Id == bobsPost.Id).SingleAsync(Ct);
        Assert.Equal((2L, bobsPost.Title), (after.UserId, after.Title));
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

    // 自关联的导航（部门的下级部门）：新根带着同类型的子对象保存，子对象一并插入（再往下一层不级联，与 Java 一致）
    [Fact]
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

    // 条件是"公司名为空"：UPDATE 里的对一导航按 INNER JOIN 连接（与 Java 的 UpdateDeleteJoinTest 一致，各方言的
    // UPDATE … JOIN / FROM / MERGE 都是内连接），没有公司的 carol 不在连接结果里；同样的条件写在查询里是 LEFT JOIN，会查到她。
    // TS 版按 LEFT JOIN 改到 carol；C# 跟 Java。要连没有公司的行一起改，条件写在外键上
    [Fact]
    public async Task UpdatesThroughAToOneNavigationOnlyRowsThatHaveATarget()
    {
        var db = await SeededAsync();
        Assert.Equal(1, await db.Query<User>().Where(u => u.Company!.Name == null).CountAsync(Ct));
        Assert.Equal(0, await db.Update<User>().Set(u => u.Age, 99).Where(u => u.Company!.Name == null).ExecuteAsync(Ct));
        Assert.Equal(1, await db.Update<User>().Set(u => u.Age, 99).Where(u => u.CompanyId == null).ExecuteAsync(Ct));
        Assert.Equal(99, (await db.Query<User>().FindAsync(3L, Ct))!.Age);
    }

    // 多对一导航的目标是别的聚合根：保存时不级联（TS 会先存目标），只把目标的主键回填到外键；改成 null 时外键置空，目标不动
    [Fact]
    public async Task BackfillsTheKeyOfAManyToOneTargetWithoutSavingIt()
    {
        var db = await SeededAsync();
        var initech = new Company { Name = "initech" };
        await db.Insert(initech).FillGeneratedKeys().ExecuteAsync(Ct);
        using (db.BeginTracking())
        {
            var dave = NewUser();
            dave.Company = initech;
            initech.Name = "renamed";
            await using var tx = db.BeginTransaction();
            await db.Save(dave).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
            Assert.Equal(initech.Id, dave.CompanyId);
        }
        Assert.Equal("initech", (await db.Query<Company>().FindAsync(initech.Id, Ct))!.Name);

        using (db.BeginTracking())
        {
            var dave = await db.Query<User>().Where(u => u.Name == "dave").Include(u => u.Company).SingleAsync(Ct);
            dave.Company = null;
            await using var tx = db.BeginTransaction();
            await db.Save(dave).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.Null((await db.Query<User>().AsNoTracking().Where(u => u.Name == "dave").SingleAsync(Ct)).CompanyId);
        Assert.Equal(1, await db.Query<Company>().Where(c => c.Id == initech.Id).CountAsync(Ct));
    }

    // 写入监听（TS 的 afterWrite）：每条写入语句按表通知一次，主键从语句里取；事务里推迟到提交之后，回滚的丢弃
    // （TS 的 afterWrite 在事务里同步调用、抛错让写入回滚；C# 的监听在提交后调用，用于缓存失效这类事后通知）
    [Fact]
    public async Task NotifiesWriteListenersAfterEveryCommittedWrite()
    {
        var log = new WriteLog();
        var db = await SeededAsync(o => o.AddWriteListener(log));
        log.Events.Clear();
        await db.Insert(new Company { Name = "initech" }).ExecuteAsync(Ct);
        await db.Update<Post>().Set(p => p.Title, "x").Where(p => p.UserId == 1).ExecuteAsync(Ct);
        await db.Delete<Comment>().Where(c => c.Id == 1).ExecuteAsync(Ct);
        Assert.Equal(["Insert:t_company", "Update:t_post", "Delete:t_comment:1"], log.Events);

        log.Events.Clear();
        await using (var tx = db.BeginTransaction())
        {
            await db.Insert(new Company { Name = "umbrella" }).ExecuteAsync(Ct);
            Assert.Empty(log.Events);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(["Insert:t_company"], log.Events);

        log.Events.Clear();
        await using (db.BeginTransaction())
            await db.Insert(new Company { Name = "rolled back" }).ExecuteAsync(Ct);
        Assert.Empty(log.Events);
        Assert.Equal(0, await db.Query<Company>().Where(c => c.Name == "rolled back").CountAsync(Ct));
    }

    private sealed class WriteLog : Execution.IWriteListener
    {
        public List<string> Events { get; } = [];

        public ValueTask OnWrittenAsync(Execution.WriteEvent e, CancellationToken cancellationToken)
        {
            lock (Events) Events.Add(string.Join(":", [e.Kind.ToString(), e.Table, .. e.Keys?.Select(k => k.ToString()!) ?? []]));
            return ValueTask.CompletedTask;
        }
    }
}