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
