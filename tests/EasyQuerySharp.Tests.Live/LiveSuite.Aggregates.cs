namespace EasyQuerySharp.Tests.Live;

// 整个查询的聚合（不分组）、decimal 运算、大批量插入分块
public abstract partial class LiveSuite
{
    // 种子里四篇帖子的金额：10.50 + 2000.00 + 1.25 + 3.00；排序对聚合没有意义，不能让 PG 报"ORDER BY 不在聚合里"
    [Fact]
    public async Task AggregatesAWholeQueryWithoutGroupBy()
    {
        var db = await SeededAsync();
        Assert.Equal(2014.75m, await db.Query<Post>().SumAsync(p => p.Amount, Ct));
        Assert.Equal(2000m, await db.Query<Post>().MaxAsync(p => p.Amount, Ct));
        Assert.Equal(1.25m, await db.Query<Post>().MinAsync(p => p.Amount, Ct));
        Assert.Equal(2014.75m / 4, await db.Query<Post>().AverageDecimalAsync(p => p.Amount, Ct));
        Assert.Equal(2L, await db.Query<Post>().Select(p => Sql.CountDistinct(p.UserId)).SingleAsync(Ct));
        Assert.Equal(4.25m, await db.Query<Post>().Where(p => p.UserId == 2).OrderBy(p => p.Id).SumAsync(p => p.Amount, Ct));
        Assert.Equal(47, await db.Query<User>().SumAsync(u => u.Age, Ct));
    }

    [Fact]
    public async Task ReturnsNullForAggregatesOverNoRows()
    {
        var db = await SeededAsync();
        var none = db.Query<Post>().Where(p => p.UserId == 999);
        Assert.Null(await none.SumAsync(p => (decimal?)p.Amount, Ct));
        Assert.Null(await none.MaxAsync(p => (decimal?)p.Amount, Ct));
        Assert.Equal(0L, await none.Select(p => Sql.CountDistinct(p.UserId)).SingleAsync(Ct));
    }

    // 限条数的查询做聚合：聚合先于 LIMIT 算完，结果是整表的和而不是前两行的和，TS 版直接拒绝这种写法
    [Fact(Skip = "疑似缺陷：Query.Take(2).SumAsync(..) 渲染成 SELECT SUM(amount) FROM t_post LIMIT 2，静默返回整表的和（2014.75）；既不拒绝，也不先限条数再聚合（2010.50）")]
    public async Task RefusesAggregatesCombinedWithTake()
    {
        var db = await SeededAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Query<Post>().OrderBy(p => p.Id).Take(2).SumAsync(p => p.Amount, Ct));
    }

    // decimal 列与数字、小数字面量、别的列做运算；也能用在更新的 SET 里
    [Fact]
    public async Task DoesArithmeticOnDecimalColumns()
    {
        var db = await SeededAsync();
        var rows = await db.Query<Post>().OrderBy(p => p.Id)
            .Select(p => new Draft<decimal, decimal, decimal>(p.Amount * 2m, p.Amount + 0.50m, p.Amount - p.Amount))
            .ToListAsync(Ct);
        Assert.Equal([(21m, 11m, 0m), (4000m, 2000.5m, 0m), (2.5m, 1.75m, 0m), (6m, 3.5m, 0m)], rows.Select(r => (r.Value1, r.Value2, r.Value3)));

        await db.Update<Post>().Set(p => new Post { Amount = p.Amount + 1m }).Where(p => p.Id == 3).ExecuteAsync(Ct);
        Assert.Equal(2.25m, (await db.Query<Post>().FindAsync(3L, Ct))!.Amount);
    }

    // 12000 行：.Batch() 每组最多 1000 条、整组在一个事务里（逐条插入每行一个事务，库不同要慢几个数量级），分组后结果与一次插入一致
    [Fact]
    public async Task InsertsManyRowsInChunks()
    {
        var db = await SeededAsync();
        var rows = Enumerable.Range(0, 12000).Select(i => new Post { UserId = 1, Title = $"bulk {i}", Amount = 1.00m }).ToList();
        using var listen = SqlCapture.Listen();
        Assert.Equal(12000, await db.Insert(rows).Batch().ExecuteAsync(Ct));
        Assert.Equal([1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000], listen.Events.Select(e => e.Statements.Count));
        Assert.Equal(12000, await db.Query<Post>().Where(p => p.Title.StartsWith("bulk ")).CountAsync(Ct));
    }
}
