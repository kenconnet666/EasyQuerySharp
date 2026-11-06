namespace EasyQuerySharp.Tests.Live;

// 写入：表达式更新、经集合导航的更新删除、整行更新、整表删除的保护、事务、行锁串行化、期望行数
public abstract partial class LiveSuite
{
    [Fact]
    public async Task UpdatesWithExpressionsAndConditions()
    {
        var db = await SeededAsync();
        Assert.Equal(1, await db.Update<User>().Set(u => new User { Age = u.Age + 1, Name = "alice2" }).Where(u => u.Name == "alice").ExecuteAsync(Ct));
        var alice = await db.Query<User>().Where(u => u.Id == 1).SingleAsync(Ct);
        Assert.Equal(31, alice.Age);
        Assert.Equal("alice2", alice.Name);
    }

    [Fact]
    public async Task UpdatesAndDeletesThroughManyNavigationSubqueries()
    {
        var db = await SeededAsync();
        Assert.Equal(1, await db.Update<User>().Set(u => u.Age, 99).Where(u => u.Posts!.Any(p => p.Amount > 1000)).ExecuteAsync(Ct));
        Assert.Equal(2, await db.Delete<Post>().Where(p => p.Title.StartsWith("orm")).ExecuteAsync(Ct));
        Assert.Equal(2, await db.Query<Post>().CountAsync(Ct));
    }

    [Fact]
    public async Task UpdatesAWholeRowByPrimaryKey()
    {
        var db = await SeededAsync();
        var alice = await db.Query<User>().Where(u => u.Name == "alice").SingleAsync(Ct);
        alice.Name = "alice2";
        alice.Attrs = new UserAttrs(["x"]);
        Assert.Equal(1, await db.Update(alice).ExecuteAsync(Ct));
        var after = await db.Query<User>().Where(u => u.Id == alice.Id).SingleAsync(Ct);
        Assert.Equal("alice2", after.Name);
        Assert.Equal(["x"], after.Attrs!.Tags);
        Assert.Equal(CreatedAt, after.CreatedAt);
    }

    // 按主键冲突：更新改写已有的行，忽略保留已有的行（SQL Server / Oracle 写成 MERGE）
    [Fact]
    public async Task UpsertsByPrimaryKey()
    {
        var db = await SeededAsync();
        await db.Insert(new Setting { Code = "theme", Value = "dark" }).OnConflictUpdate().ExecuteAsync(Ct);
        await db.Insert(new Setting { Code = "theme", Value = "light" }).OnConflictUpdate().ExecuteAsync(Ct);
        Assert.Equal("light", (await db.Query<Setting>().Where(s => s.Code == "theme").SingleAsync(Ct)).Value);
        await db.Insert(new Setting { Code = "theme", Value = "ignored" }).OnConflictIgnore().ExecuteAsync(Ct);
        await db.Insert(new Setting { Code = "lang", Value = "zh" }).OnConflictIgnore().ExecuteAsync(Ct);
        Assert.Equal(["light", "zh"], (await db.Query<Setting>().OrderByDescending(s => s.Code).Select(s => s.Value).ToListAsync(Ct)));
    }

    [Fact]
    public async Task DeletesEverythingOnlyWithAll()
    {
        var db = await SeededAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Delete<Comment>().ExecuteAsync(Ct));
        Assert.Equal(2, await db.Delete<Comment>().All().ExecuteAsync(Ct));
    }

    [Fact]
    public async Task CommitsAndRollsBackTransactions()
    {
        var db = await SeededAsync();
        await db.TransactionAsync(() => db.Update<User>().Set(u => u.Age, 1).Where(u => u.Id == 1).ExecuteAsync(Ct), cancellationToken: Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.TransactionAsync(async () =>
        {
            await db.Update<User>().Set(u => u.Age, 2).Where(u => u.Id == 1).ExecuteAsync(Ct);
            throw new InvalidOperationException("boom");
        }, cancellationToken: Ct));
        Assert.Equal(1, (await db.Query<User>().FindAsync(1L, Ct))!.Age);
    }

    // 同一个异步方法里先后开几个事务：提交、回滚后环境事务真正结束，之后的语句不再走旧事务，也能再开新事务
    [Fact]
    public async Task RunsTransactionsOneAfterAnotherInTheSameMethod()
    {
        var db = await SeededAsync();
        for (var age = 1; age <= 3; age++)
        {
            await using var tx = db.BeginTransaction();
            await db.Update<User>().Set(u => u.Age, age).Where(u => u.Id == 1).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        await using (var tx = db.BeginTransaction())
        {
            await db.Update<User>().Set(u => u.Age, 9).Where(u => u.Id == 1).ExecuteAsync(Ct);
            await tx.RollbackAsync(Ct);
        }
        await db.Update<User>().Set(u => u.Age, 4).Where(u => u.Id == 2).ExecuteAsync(Ct);
        Assert.Equal(3, (await db.Query<User>().FindAsync(1L, Ct))!.Age);
        Assert.Equal(4, (await db.Query<User>().FindAsync(2L, Ct))!.Age);
    }

    // 与 Java 一致：已经在事务里时不能再开事务
    [Fact]
    public async Task RejectsNestedTransactions()
    {
        var db = await SeededAsync();
        await using var tx = db.BeginTransaction();
        Assert.Throws<InvalidOperationException>(() => db.BeginTransaction());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.TransactionAsync(() => Task.CompletedTask, cancellationToken: Ct));
    }

    // 追踪作用域里并行查询：各条查询的结果都进同一个作用域
    [Fact]
    public async Task TracksEntitiesQueriedInParallel()
    {
        var db = await SeededAsync();
        using var tracking = db.BeginTracking();
        var users = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => db.Query<User>().FindAsync(i % 3 + 1L, Ct)));
        foreach (var user in users) Assert.Empty(db.GetTrackedChanges(user!));
        Assert.Equal(3, users.Distinct().Count());
    }

    // 三个事务同时读改写同一行：FOR UPDATE 让它们排队，结果不丢更新
    [Fact]
    public async Task SerializesConcurrentTransactionsThatLockTheSameRow()
    {
        SkipUnless(Options.RowLocks, "行锁");
        var db = await SeededAsync();
        Task Bump() => Task.Run(() => db.TransactionAsync(async () =>
        {
            var user = await db.Query<User>().Where(u => u.Id == 1).ForUpdate().SingleAsync(Ct);
            await Task.Delay(5, Ct);
            await db.Update<User>().Set(u => u.Age, (user.Age ?? 0) + 1).Where(u => u.Id == 1).ExecuteAsync(Ct);
        }, cancellationToken: Ct), Ct);
        await Task.WhenAll(Bump(), Bump(), Bump());
        Assert.Equal(33, (await db.Query<User>().FindAsync(1L, Ct))!.Age);
    }

    // 期望行数不符时抛出 ConcurrencyException，事务里之前的更新一起回滚
    [Fact]
    public async Task ExpectedRowsThrowAndRollBack()
    {
        var db = await SeededAsync();
        await db.TransactionAsync(() => db.Update<User>().Set(u => u.Age, 7).Where(u => u.Id == 1).ExecuteAsync(1, cancellationToken: Ct), cancellationToken: Ct);
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() => db.TransactionAsync(async () =>
        {
            await db.Update<User>().Set(u => u.Age, 8).Where(u => u.Id == 1).ExecuteAsync(Ct);
            await db.Update<User>().Set(u => u.Age, 9).Where(u => u.Id == -1).ExecuteAsync(1, "已被处理", Ct);
        }, cancellationToken: Ct));
        Assert.Equal(0, ex.ActualRows);
        Assert.Equal(7, (await db.Query<User>().FindAsync(1L, Ct))!.Age);
    }
}
