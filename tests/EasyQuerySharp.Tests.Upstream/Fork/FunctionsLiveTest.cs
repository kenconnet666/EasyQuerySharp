using EasyQuerySharp.Tests.Upstream.Mysql8;
using EasyQuerySharp.Tests.Upstream.Pgsql;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：live 下的 DateTruncLiveTest、GroupingLiveTest、NullSafeLiveTest、WindowLiveTest。
/// Java 跑 MySQL 8.0.18、PG 17、HANA；这里跑本地必测的 MySQL 8.4 与 PG 17（HANA 随 HANA 方言补上）。
/// </summary>
public sealed class FunctionsLiveTest(Mysql8Fixture mysql, PgFixture pg)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<EasyDb> Fresh<T>(string database)
    {
        var db = database == "mysql" ? mysql.Db : pg.Db;
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(T)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(T)], Ct);
        return db;
    }

    private static ForkPost Post(string id, string userId, decimal amount) => new() { Id = id, UserId = userId, Title = id, Amount = amount };

    [Theory, Upstream("fork/live/DateTruncLiveTest.java#truncatesToEachUnit")]
    [InlineData("mysql")]
    [InlineData("pg")]
    public async Task TruncatesToEachUnit(string database)
    {
        var db = await Fresh<ForkUser>(database);
        await db.Insert(new ForkUser { Id = "u1", CreatedAt = new DateTime(2026, 8, 27, 13, 45, 56) }).ExecuteAsync(Ct);

        var r = await db.Query<ForkUser>().Select(u => new Draft<DateTime?, DateTime?, DateTime?, DateTime?, DateTime?, DateTime?, DateTime?, DateTime?>(
            u.CreatedAt.DateTrunc(DateTruncUnit.Year), u.CreatedAt.DateTrunc(DateTruncUnit.Quarter), u.CreatedAt.DateTrunc(DateTruncUnit.Month),
            u.CreatedAt.DateTrunc(DateTruncUnit.Week), u.CreatedAt.DateTrunc(DateTruncUnit.Day), u.CreatedAt.DateTrunc(DateTruncUnit.Hour),
            u.CreatedAt.DateTrunc(DateTruncUnit.Minute), u.CreatedAt.DateTrunc(DateTruncUnit.Second))).SingleAsync(Ct);
        Assert.Equal(new DateTime(2026, 1, 1), r.Value1);
        Assert.Equal(new DateTime(2026, 7, 1), r.Value2);
        Assert.Equal(new DateTime(2026, 8, 1), r.Value3);
        Assert.Equal(new DateTime(2026, 8, 24), r.Value4);
        Assert.Equal(new DateTime(2026, 8, 27), r.Value5);
        Assert.Equal(new DateTime(2026, 8, 27, 13, 0, 0), r.Value6);
        Assert.Equal(new DateTime(2026, 8, 27, 13, 45, 0), r.Value7);
        Assert.Equal(new DateTime(2026, 8, 27, 13, 45, 56), r.Value8);

        Assert.Equal(1, await db.Query<ForkUser>().Where(u => u.CreatedAt.DateTrunc(DateTruncUnit.Month) == new DateTime(2026, 8, 1)).CountAsync(Ct));
    }

    [Theory, Upstream("fork/live/GroupingLiveTest.java#rollupAddsGrandTotal")]
    [InlineData("mysql")]
    [InlineData("pg")]
    public async Task RollupAddsGrandTotal(string database)
    {
        var db = await Fresh<ForkPost>(database);
        await db.Insert(Post("p1", "u1", 10), Post("p2", "u1", 20), Post("p3", "u2", 5)).ExecuteAsync(Ct);

        var rows = await db.Query<ForkPost>().GroupBy(p => p.UserId).Rollup()
            .Select(g => new Draft<string?, decimal?, int>(g.Key, g.Sum(p => p.Amount), g.Grouping(g.Key))).ToListAsync(Ct);
        var lines = rows.Select(r => $"{r.Value1 ?? "null"}={r.Value2!.Value:0.##}/{r.Value3}").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["null=35/1", "u1=30/0", "u2=5/0"], lines);
    }

    [Theory, Upstream("fork/live/NullSafeLiveTest.java#nullMatchesNullAndEmptyBecomesNull")]
    [InlineData("mysql")]
    [InlineData("pg")]
    public async Task NullMatchesNullAndEmptyBecomesNull(string database)
    {
        var db = await Fresh<ForkUser>(database);
        await db.Insert(new ForkUser { Id = "u1", Name = "a" }, new ForkUser { Id = "u2" }, new ForkUser { Id = "u3", Name = "" }).ExecuteAsync(Ct);

        async Task<List<string?>> Ids(System.Linq.Expressions.Expression<Func<ForkUser, bool>> predicate) =>
            (await db.Query<ForkUser>().Where(predicate).Select(u => u.Id).ToListAsync(Ct)).Order(StringComparer.Ordinal).ToList();
        string? none = null;
        Assert.Equal(["u2"], await Ids(u => u.Name.EqualsNullSafe(none)));
        Assert.Equal(["u1"], await Ids(u => u.Name.EqualsNullSafe("a")));
        Assert.Equal(["u2", "u3"], await Ids(u => u.Name.NullIf("") == null));
    }

    [Theory, Upstream("fork/live/WindowLiveTest.java#runningTotalRowNumberAndNtile")]
    [InlineData("mysql")]
    [InlineData("pg")]
    public async Task RunningTotalRowNumberAndNtile(string database)
    {
        var db = await Fresh<ForkPost>(database);
        await db.Insert(Post("p1", "u1", 10), Post("p2", "u1", 20), Post("p3", "u1", 30), Post("p4", "u2", 5)).ExecuteAsync(Ct);

        var rows = await db.Query<ForkPost>().OrderBy(p => p.Id).Select(p => new Draft<string?, decimal?, long, long>(
            p.Id,
            Sql.Over.Sum(p.Amount, w => w.PartitionBy(p.UserId).OrderBy(p.Id).Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow)),
            Sql.Over.RowNumber(w => w.OrderByDescending(p.Amount)),
            Sql.Over.Ntile(2, w => w.OrderBy(p.Amount)))).ToListAsync(Ct);

        Assert.Equal(["p1", "p2", "p3", "p4"], rows.Select(r => r.Value1));
        Assert.Equal([10m, 30m, 60m, 5m], rows.Select(r => r.Value2!.Value));
        Assert.Equal([3L, 2L, 1L, 4L], rows.Select(r => r.Value3));
        Assert.Equal([1L, 2L, 2L, 1L], rows.Select(r => r.Value4));
    }
}
