namespace EasyQuerySharp.Tests.Live;

// 方言差异：null 安全的相等、按月截断分组、NOWAIT / SKIP LOCKED、rollup / cube、INSERT … SELECT 与换表名
public abstract partial class LiveSuite
{
    // 和自己比较：null 安全的相等连 NULL 也相等；普通的 == 碰到 NULL 得不到 bob
    [Fact]
    public async Task ComparesNullSafely()
    {
        var db = await SeededAsync();
        async Task<List<string>> Names(System.Linq.Expressions.Expression<Func<User, bool>> predicate) =>
            await db.Query<User>().Where(predicate).OrderBy(u => u.Id).Select(u => u.Name).ToListAsync(Ct);
        int? none = null;
        Assert.Equal(["bob"], await Names(u => u.Age.EqualsNullSafe(none)));
        Assert.Equal(["alice"], await Names(u => u.Age.EqualsNullSafe(30)));
        Assert.Equal(["alice", "bob", "carol"], await Names(u => u.Age.EqualsNullSafe(u.Age)));
        Assert.Equal(["alice", "carol"], await Names(u => u.Age == u.Age));
    }

    // 各粒度的截断已在 TruncatesDatesToEachUnit 里验证；这里按月分组统计
    [Fact]
    public async Task GroupsByTruncatedMonth()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().GroupBy(u => u.CreatedAt.DateTrunc(DateTruncUnit.Month))
            .Select(g => new Draft<DateTime, long>(g.Key, g.Count())).ToListAsync(Ct);
        Assert.Equal([(new DateTime(2026, 1, 1), 3L)], rows.Select(r => (r.Value1, r.Value2)));
    }

    // 一个事务锁住 alice 时，另一个事务 SKIP LOCKED 只拿到其余的行，NOWAIT 立即报锁冲突、没被锁的行照常拿到
    [Fact]
    public async Task LocksRowsWithNoWaitAndSkipLocked()
    {
        SkipUnless(Options.RowLocks, "行锁");
        var db = await SeededAsync();
        var timeout = TimeSpan.FromSeconds(30);
        var locked = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var holder = Task.Run(async () =>
        {
            await using var tx = db.BeginTransaction();
            // 按主键加锁只锁这一行（按没有索引的列查，InnoDB 会锁住扫描过的所有行）
            await db.Query<User>().Where(u => u.Id == 1).ForUpdate().ToListAsync(Ct);
            locked.SetResult();
            await release.Task.WaitAsync(timeout, Ct);
        }, Ct);
        try
        {
            await locked.Task.WaitAsync(timeout, Ct);

            await using (var tx = db.BeginTransaction())
            {
                var skipped = await db.Query<User>().OrderBy(u => u.Id).ForUpdateSkipLocked().Select(u => u.Name).ToListAsync(Ct);
                Assert.Equal(["bob", "carol"], skipped);
            }
            await using (var tx = db.BeginTransaction())
            {
                await Assert.ThrowsAsync<EasyQueryException>(() => db.Query<User>().Where(u => u.Id == 1).ForUpdateNoWait().ToListAsync(Ct));
            }
            await using (var tx = db.BeginTransaction())
            {
                Assert.Single(await db.Query<User>().Where(u => u.Id == 2).ForUpdateNoWait().ToListAsync(Ct));
            }
        }
        finally
        {
            release.TrySetResult();
            await holder.WaitAsync(timeout, Ct);
        }
    }

    // 按用户小计加总计；有 GROUPING() 的库再核对"这一行是不是汇总行"
    [Fact]
    public async Task RollsUpSubtotalsAndAGrandTotal()
    {
        SkipUnless(Options.Rollup, "ROLLUP");
        var db = await SeededAsync();
        var rows = await db.Query<Post>().GroupBy(p => p.UserId).Rollup()
            .Select(g => new Draft<long?, decimal, int>(g.Key, g.Sum(p => p.Amount), Options.GroupingFunction ? g.Grouping(g.Key) : 0))
            .ToListAsync(Ct);
        var sorted = rows.OrderBy(r => r.Value1 ?? long.MaxValue).Select(r => (r.Value1, r.Value2, r.Value3)).ToList();
        var total = Options.GroupingFunction ? 1 : 0;
        Assert.Equal([((long?)1, 2010.5m, 0), (2, 4.25m, 0), (null, 2014.75m, total)], sorted);
    }

    // 4 个明细 + 2 个用户小计 + 4 个标题小计 + 1 个总计
    [Fact]
    public async Task CubesEveryCombinationOfTheKeys()
    {
        SkipUnless(Options.Cube, "CUBE");
        var db = await SeededAsync();
        var rows = await db.Query<Post>().GroupBy(p => new { p.UserId, p.Title }).Cube()
            .Select(g => new Draft<long?, string?, long>(g.Key.UserId, g.Key.Title, g.Count()))
            .ToListAsync(Ct);
        Assert.Equal(11, rows.Count);
        Assert.Equal(4, rows.Single(r => r.Value1 is null && r.Value2 is null).Value3);
    }

    // 数据不出库的 INSERT … SELECT：金额大于 5 的两篇（10.50、2000.00）搬到归档表，标题顺手转大写
    [Fact]
    public async Task InsertsFromAQueryWithoutTheDataLeavingTheDatabase()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(PostArchive)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(PostArchive)], Ct);

        var affected = await db.Query<Post>().Where(p => p.Amount > 5m)
            .Select(p => new Post { Id = p.Id, UserId = p.UserId, Title = p.Title.ToUpper(), Amount = p.Amount })
            .InsertInto("t_post_archive").ExecuteAsync(Ct);
        Assert.Equal(2, affected);
        var archived = await db.Query<PostArchive>().OrderBy(p => p.Id).ToListAsync(Ct);
        Assert.Equal([(1L, "ORM TIPS"), (2L, "SQL 100%")], archived.Select(p => (p.Id, p.Title)));
        Assert.Equal(4, await db.Query<Post>().CountAsync(Ct));
    }

    // 同一个实体换一张表读：根表换成归档表（导航跟着换属于计划中的查询级表名映射，这里只验证根表）
    [Fact]
    public async Task ReadsAnotherTableForTheSameEntity()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(PostArchive)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(PostArchive)], Ct);
        await db.Query<Post>().Where(p => p.Id <= 2).InsertInto("t_post_archive").ExecuteAsync(Ct);

        Assert.Equal(2, await db.Query<Post>().AsTable("t_post_archive").CountAsync(Ct));
        Assert.Equal(4, await db.Query<Post>().CountAsync(Ct));
        Assert.Equal(["orm tips", "sql 100%"], await db.Query<Post>().AsTable("t_post_archive").OrderBy(p => p.Id).Select(p => p.Title).ToListAsync(Ct));
    }
}
