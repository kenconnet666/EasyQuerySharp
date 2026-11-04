using EasyQuerySharp.Metadata;
using EasyQuerySharp.Tests.Upstream.Mysql8;
using EasyQuerySharp.Tests.Upstream.Pgsql;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：entity/ForkPost（只用到这几列）。</summary>
[Table("t_fork_post")]
public class ForkPost
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? Title { get; set; }

    public decimal? Amount { get; set; }
}

/// <summary>
/// 对应 Java fork：live/LockWaitLiveTest.java。一个事务锁住 p1 时，另一个事务 NOWAIT 立即报锁冲突、SKIP LOCKED 只拿到其余的行。
/// Java 跑 MySQL 8.0.18、PG 17、HANA；这里跑本地必测的 MySQL 8.4 与 PG 17。
/// </summary>
public sealed class LockWaitLiveTest(Mysql8Fixture mysql, PgFixture pg)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory, Upstream("fork/live/LockWaitLiveTest.java#noWaitFailsAndSkipLockedSkips")]
    [InlineData("mysql")]
    [InlineData("pg")]
    public async Task NoWaitFailsAndSkipLockedSkips(string database)
    {
        var db = database == "mysql" ? mysql.Db : pg.Db;
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(ForkPost)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(ForkPost)], Ct);
        await db.Insert(new[] { Post("p1"), Post("p2"), Post("p3") }).ExecuteAsync(Ct);

        var timeout = TimeSpan.FromSeconds(30);
        var locked = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var holder = Task.Run(async () =>
        {
            await using var tx = db.BeginTransaction();
            await db.Query<ForkPost>().Where(p => p.Id == "p1").ForUpdate().ToListAsync(Ct);
            locked.SetResult();
            await release.Task.WaitAsync(timeout, Ct);
        }, Ct);
        try
        {
            await locked.Task.WaitAsync(timeout, Ct);

            await using (var tx = db.BeginTransaction())
            {
                var ex = await Assert.ThrowsAsync<EasyQueryException>(() => db.Query<ForkPost>().Where(p => p.Id == "p1").ForUpdateNoWait().ToListAsync(Ct));
                // 各库的错误不同（MySQL 3572、PG 55P03），确认是锁冲突而不是语法错误
                var message = ex.InnerException!.Message.ToLowerInvariant();
                Assert.True(message.Contains("lock") || message.Contains("nowait"), message);
            }

            await using (var tx = db.BeginTransaction())
            {
                var free = await db.Query<ForkPost>().OrderBy(p => p.Id).ForUpdateSkipLocked().Select(p => p.Id).ToListAsync(Ct);
                Assert.Equal(["p2", "p3"], free);
            }
        }
        finally
        {
            release.TrySetResult();
            await holder.WaitAsync(timeout, Ct);
        }
    }

    private static ForkPost Post(string id) => new() { Id = id, UserId = "u1", Title = id, Amount = 1m };
}
