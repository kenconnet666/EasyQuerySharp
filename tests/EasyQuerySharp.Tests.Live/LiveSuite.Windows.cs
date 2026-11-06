namespace EasyQuerySharp.Tests.Live;

// 窗口函数与按父对象限条数：行号 / 累计 / LAG / 全局计数，每个父对象取最新一条与前 N 条
public abstract partial class LiveSuite
{
    // 行号（倒序）、按用户累计的金额、上一行标题、全表计数同时出现在一条语句里
    [Fact]
    public async Task ComputesRowNumbersRunningTotalsLagAndGlobalCounts()
    {
        var db = await SeededAsync();
        var rows = await db.Query<Post>()
            .OrderBy(p => p.Id)
            .Select(p => new Draft<long, long, decimal, string?, long>(
                p.Id,
                Sql.Over.RowNumber(w => w.PartitionBy(p.UserId).OrderByDescending(p.Id)),
                Sql.Over.Sum(p.Amount, w => w.PartitionBy(p.UserId).OrderBy(p.Id).Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow)),
                Sql.Over.Lag(p.Title, 1, w => w.OrderBy(p.Id)),
                Sql.Over.Count(p.Id, w => w)))
            .ToListAsync(Ct);
        // MariaDB 的 bug：这三种窗口函数同时出现时 LAG 算错，不比较 prev
        string? Prev(string? value) => Options.MixedWindowLag ? value : null;
        Assert.Equal(
            [(1L, 2L, 10.5m, Prev(null), 4L), (2L, 1L, 2010.5m, Prev("orm tips"), 4L), (3L, 2L, 1.25m, Prev("sql 100%"), 4L), (4L, 1L, 4.25m, Prev("orm_v2"), 4L)],
            rows.Select(r => (r.Value1, r.Value2, r.Value3, Prev(r.Value4), r.Value5)));
    }

    // 不带任何列引用的窗口函数（COUNT(*) OVER ()）写在投影里：翻译器按"没有查询参数"在客户端求值，抛出"只能写在查询表达式里"
    [Fact]
    public async Task CountsAllRowsWithAWindowThatReferencesNoColumn()
    {
        var db = await SeededAsync();
        var totals = await db.Query<Post>().Select(p => new Draft<long, long>(p.Id, Sql.Over.Count(w => w))).ToListAsync(Ct);
        Assert.All(totals, t => Assert.Equal(4L, t.Value2));
    }

    // 每个用户最新的一篇（取第一个元素）与金额最高的一篇（Include 里按父对象限 1 条）
    [Fact]
    public async Task TakesTheFirstRowAndTopNPerParent()
    {
        var db = await SeededAsync();
        var latest = await db.Query<User>()
            .OrderBy(u => u.Id)
            .Select(u => new Draft<string, string?>(u.Name, u.Posts!.OrderByDescending(p => p.Id).First().Title))
            .ToListAsync(Ct);
        Assert.Equal([("alice", "sql 100%"), ("bob", "hello"), ("carol", null)], latest.Select(r => (r.Value1, r.Value2)));

        var users = await db.Query<User>()
            .OrderBy(u => u.Id)
            .Include(u => u.Posts, q => q.OrderByDescending(p => p.Amount).Take(1))
            .ToListAsync(Ct);
        Assert.Equal([["sql 100%"], ["hello"], []], users.Select(u => u.Posts!.Select(p => p.Title).ToArray()));
    }
}
