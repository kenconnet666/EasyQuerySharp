namespace EasyQuerySharp.Tests.Live;

// 流式读取、取消、手动事务
public abstract partial class LiveSuite
{
    // 逐行流式读取、按块读取；读到第一行就放弃后连接要还回去，之后的查询照常
    [Fact]
    public async Task StreamsRowsWithoutLoadingEverythingFirst()
    {
        var db = await SeededAsync();
        await db.Insert(Enumerable.Range(0, 1200).Select(i => new Post { UserId = 1, Title = $"bulk {i}", Amount = 1.00m })).Batch().ExecuteAsync(Ct);

        var count = 0;
        await foreach (var _ in db.Query<Post>().OrderBy(p => p.Id).AsAsyncEnumerable(Ct)) count++;
        Assert.Equal(1204, count);

        var sizes = new List<int>();
        await foreach (var chunk in db.Query<Post>().OrderBy(p => p.Id).ToChunksAsync(500, Ct)) sizes.Add(chunk.Count);
        Assert.Equal([500, 500, 204], sizes);

        await foreach (var row in db.Query<Post>().OrderBy(p => p.Id).AsAsyncEnumerable(Ct))
        {
            Assert.Equal("orm tips", row.Title);
            break;
        }
        Assert.Equal(1204, await db.Query<Post>().CountAsync(Ct));
    }

    // 事务里的流式读取，读完后监听器收到的是读到的总行数
    [Fact]
    public async Task StreamsInsideATransactionAndReportsTheStreamedRows()
    {
        var db = await SeededAsync();
        using var listen = SqlCapture.Listen();
        await using (var tx = db.BeginTransaction())
        {
            await db.Insert(new Comment { PostId = 1, Body = "inside" }).ExecuteAsync(Ct);
            var bodies = new List<string>();
            await foreach (var c in db.Query<Comment>().OrderBy(c => c.Id).AsAsyncEnumerable(Ct)) bodies.Add(c.Body);
            Assert.Equal(["nice", "thanks", "inside"], bodies);
            await tx.CommitAsync(Ct);
        }
        Assert.Contains(listen.Events, e => e.Rows == 3 && e.Exception is null && e.Statement.Text.Contains("SELECT", StringComparison.Ordinal));
    }

    // 手动事务：提交才生效，没提交就离开作用域自动回滚，已结束再回滚什么也不做、再提交报错
    [Fact]
    public async Task BeginsATransactionManuallyAndRollsBackUnlessCommitted()
    {
        var db = await SeededAsync();
        Task<long> Count() => db.Query<Post>().Where(p => p.Title.StartsWith("manual-")).CountAsync(Ct);
        Task Add(string title) => db.Insert(new Post { UserId = 1, Title = title, Amount = 1.00m }).ExecuteAsync(Ct);

        await using (var tx = db.BeginTransaction())
        {
            await Add("manual-1");
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(1, await Count());

        await using (var tx = db.BeginTransaction()) await Add("manual-2");
        Assert.Equal(1, await Count());

        var third = db.BeginTransaction();
        await Add("manual-3");
        await third.RollbackAsync(Ct);
        await third.RollbackAsync(Ct);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => third.CommitAsync(Ct));
        Assert.Equal(1, await Count());
    }

    // 已取消的令牌：查询、流式读取都立即取消；没取消的令牌不影响结果
    [Fact]
    public async Task StopsWaitingWhenTheTokenIsCancelled()
    {
        var db = await SeededAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.Query<User>().ToListAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in db.Query<User>().AsAsyncEnumerable(cancelled.Token)) { }
        });

        using var live = new CancellationTokenSource();
        Assert.Equal(3, (await db.Query<User>().ToListAsync(live.Token)).Count);
    }
}
