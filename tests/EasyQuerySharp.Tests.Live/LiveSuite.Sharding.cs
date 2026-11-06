using System.Globalization;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// 分库分表（0006）：取模分表在一个库里（库内 UNION ALL）、按月分表跨两个库（各库内 UNION ALL、跨库在内存里归并），
// 路由、裁剪、计数与分页的合并、逐节点的更新删除、跨库事务
public abstract partial class LiveSuite
{
    /// <summary>按 id 取模分成 t_shard_log_0 … _2 的日志（都在默认数据源）。</summary>
    [Table("t_shard_log")]
    public sealed class ShardLog
    {
        [Column(PrimaryKey = true)]
        public string Id { get; set; } = "";

        public string Kind { get; set; } = "";

        public int Amount { get; set; }
    }

    /// <summary>按月分成 t_shard_event_202401 … _202406 的事件：1 – 3 月在默认数据源 ds0，4 – 6 月在第二个库 ds1。</summary>
    [Table("t_shard_event")]
    public sealed class ShardEvent
    {
        [Column(PrimaryKey = true)]
        public string Id { get; set; } = "";

        public int Amount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    // 建一个分片的库：取模分表的 3 张表；twoDataSources 时再按月分表到两个库的 6 张表。表每次重建，各插入 12 行
    private async Task<EasyDb> ShardedAsync(bool twoDataSources)
    {
        var dataSource = DataSources.GetOrAdd(await ConnectionStringAsync(), cs => Database.Factory.CreateDataSource(cs));
        var second = twoDataSources ? DataSources.GetOrAdd(await ShardConnectionStringAsync(), cs => Database.Factory.CreateDataSource(cs)) : null;
        var db = new EasyDb(o =>
        {
            o.Use(Dialect, dataSource).AddListener(SqlCapture.Instance).Shard<ShardLog>(r => r.Mod(l => l.Id, 3));
            if (second is null) return;
            o.AddDataSource("ds1", second).Shard<ShardEvent>(r => r.ByMonth(e => e.CreatedAt, from: new DateTime(2024, 1, 1), to: () => new DateTime(2024, 6, 30),
                dataSource: d => d.Month <= 3 ? "ds0" : "ds1").DefaultOrder(e => e.CreatedAt, descending: true));
        });
        Type[] tables = twoDataSources ? [typeof(ShardLog), typeof(ShardEvent)] : [typeof(ShardLog)];
        await db.CodeFirst.DropTablesIfExistsAsync(tables, Ct);
        await db.CodeFirst.CreateTablesAsync(tables, Ct);
        await db.Insert(Enumerable.Range(1, 12).Select(i => new ShardLog { Id = Text(i), Kind = i % 2 == 0 ? "even" : "odd", Amount = i })).ExecuteAsync(Ct);
        if (twoDataSources)
            await db.Insert(Enumerable.Range(0, 12).Select(i => new ShardEvent { Id = "e" + Text(i), Amount = i, CreatedAt = new DateTime(2024, i / 2 + 1, 10 + i % 2) }))
                .ExecuteAsync(Ct);
        return db;
    }

    private static string Text(int i) => i.ToString(CultureInfo.InvariantCulture);

    // 写入按 id 落到各自的表；按 id 查只查那一张表；条件互相矛盾时不访问数据库
    [Fact]
    public async Task ShardingRoutesWritesAndPrunesReads()
    {
        var db = await ShardedAsync(twoDataSources: false);
        var rule = db.GetShardRule<ShardLog>()!;
        foreach (var node in rule.Nodes)
        {
            var ids = await db.Query<ShardLog>().AsTable(node.Table).Select(l => l.Id).ToListAsync(Ct);
            Assert.All(ids, id => Assert.Equal(node, rule.NodeOf(new ShardLog { Id = id })));
        }

        using var listen = SqlCapture.Listen();
        Assert.Equal(7, (await db.Query<ShardLog>().Where(l => l.Id == "7").SingleAsync(Ct)).Amount);
        var byId = listen.Last.Statement.Text;
        Assert.Contains(rule.NodeOf(new ShardLog { Id = "7" }).Table, byId);
        Assert.DoesNotContain("UNION ALL", byId);

        string[] ids2 = ["1", "2"];
        Assert.Equal([1, 2], (await db.Query<ShardLog>().Where(l => ids2.Contains(l.Id)).OrderBy(l => l.Amount).ToListAsync(Ct)).Select(l => l.Amount));

        // "1" 与 "2" 落在不同的表：同时等于两者的条件是空路由
        var before = listen.Events.Count;
        Assert.Empty(await db.Query<ShardLog>().Where(l => l.Id == "1" && l.Id == "2").ToListAsync(Ct));
        Assert.Equal(0, await db.Query<ShardLog>().Where(l => l.Id == "1" && l.Id == "2").CountAsync(Ct));
        Assert.Equal(before, listen.Events.Count);
    }

    // 同一库的三张表合成一条 UNION ALL：排序、分页、分组、聚合都由数据库做
    [Fact]
    public async Task ShardingUnionsTablesOfOneDataSource()
    {
        var db = await ShardedAsync(twoDataSources: false);
        using var listen = SqlCapture.Listen();
        Assert.Equal(12, await db.Query<ShardLog>().CountAsync(Ct));
        Assert.Contains("UNION ALL", listen.Last.Statement.Text);

        var page = await db.Query<ShardLog>().OrderBy(l => l.Amount).ToPageAsync(2, 5, Ct);
        Assert.Equal(12, page.Total);
        Assert.Equal([6, 7, 8, 9, 10], page.Rows.Select(l => l.Amount));

        var kinds = await db.Query<ShardLog>().GroupBy(l => l.Kind).OrderBy(g => g.Key).Select(g => new { g.Key, Count = g.Count(), Sum = g.Sum(l => l.Amount) }).ToListAsync(Ct);
        Assert.Equal([("even", 6L, 42), ("odd", 6L, 36)], kinds.Select(k => (k.Key, k.Count, k.Sum)));
        Assert.Equal(78, await db.Query<ShardLog>().SumAsync(l => l.Amount, Ct));
    }

    // 按条件更新 / 删除逐个节点执行、行数相加；按实体只写它所在的表；改分片键报错
    [Fact]
    public async Task ShardingWritesPerNode()
    {
        var db = await ShardedAsync(twoDataSources: false);
        Assert.Equal(6, await db.Update<ShardLog>().Set(l => l.Amount, l => l.Amount + 100).Where(l => l.Kind == "even").ExecuteAsync(Ct));
        Assert.Equal(42 + 600, await db.Query<ShardLog>().Where(l => l.Kind == "even").SumAsync(l => l.Amount, Ct));

        var seven = await db.Query<ShardLog>().Where(l => l.Id == "7").SingleAsync(Ct);
        seven.Kind = "lucky";
        using (var listen = SqlCapture.Listen())
        {
            Assert.Equal(1, await db.Update(seven).ExecuteAsync(Ct));
            Assert.Contains(db.GetShardRule<ShardLog>()!.NodeOf(seven).Table, listen.Last.Statement.Text);
        }
        Assert.Throws<InvalidOperationException>(() => db.Update<ShardLog>().Set(l => l.Id, "x").Where(l => l.Kind == "odd").ToSql());

        Assert.Equal(1, await db.Delete(seven).ExecuteAsync(Ct));
        Assert.Equal(5, await db.Delete<ShardLog>().Where(l => l.Kind == "odd").ExecuteAsync(Ct));
        Assert.Equal(6, await db.Query<ShardLog>().CountAsync(Ct));
    }

    // 两个库：按时间裁剪到一个库；跨库时并行查询再归并排序、分页，计数与聚合相加；跨库的分组不支持
    [Fact]
    public async Task ShardingMergesAcrossDataSources()
    {
        SkipUnless(Options.ShardDataSources, "第二个库");
        var db = await ShardedAsync(twoDataSources: true);
        using var listen = SqlCapture.Listen();
        Assert.Equal(6, await db.Query<ShardEvent>().Where(e => e.CreatedAt < new DateTime(2024, 4, 1)).CountAsync(Ct));
        Assert.Equal("ds0", Assert.Single(listen.Events).Statement.DataSource);

        var all = await db.Query<ShardEvent>().OrderByDescending(e => e.CreatedAt).ToListAsync(Ct);
        Assert.Equal(Enumerable.Range(0, 12).Reverse(), all.Select(e => e.Amount));
        var page = await db.Query<ShardEvent>().OrderBy(e => e.Amount).ToPageAsync(2, 5, Ct);
        Assert.Equal(12, page.Total);
        Assert.Equal([5, 6, 7, 8, 9], page.Rows.Select(e => e.Amount));

        // 没写排序、带了 Take：补上默认排序（时间降序）
        Assert.Equal([11, 10, 9], (await db.Query<ShardEvent>().Take(3).ToListAsync(Ct)).Select(e => e.Amount));
        Assert.Equal(66, await db.Query<ShardEvent>().SumAsync(e => e.Amount, Ct));
        Assert.Equal((0, 11), (await db.Query<ShardEvent>().MinAsync(e => e.Amount, Ct), await db.Query<ShardEvent>().MaxAsync(e => e.Amount, Ct)));
        Assert.True(await db.Query<ShardEvent>().Where(e => e.Amount == 11).AnyAsync(Ct));

        var sharded = await db.Query<ShardEvent>().OrderBy(e => e.CreatedAt).ToShardingPageAsync(1, 4, null, Ct);
        Assert.Equal([2L, 2, 2, 2, 2, 2], sharded.TotalLines);
        Assert.Equal([0, 1, 2, 3], sharded.Rows.Select(e => e.Amount));

        await Assert.ThrowsAsync<NotSupportedException>(() => db.Query<ShardEvent>().GroupBy(e => e.Amount % 2).Select(g => g.Count()).ToListAsync(Ct));
    }

    // 跨库的写入：按条件更新落到两个库；事务在两个库上各开一个，回滚两边都撤销，提交两边都生效
    [Fact]
    public async Task ShardingWritesAndTransactionsAcrossDataSources()
    {
        SkipUnless(Options.ShardDataSources, "第二个库");
        var db = await ShardedAsync(twoDataSources: true);
        Assert.Equal(12, await db.Update<ShardEvent>().Set(e => e.Amount, e => e.Amount + 100).Where(e => e.Amount < 100).ExecuteAsync(Ct));
        Assert.Equal(66 + 1200, await db.Query<ShardEvent>().SumAsync(e => e.Amount, Ct));
        Assert.Equal(6, await db.Delete<ShardEvent>().Where(e => e.CreatedAt >= new DateTime(2024, 4, 1)).ExecuteAsync(Ct));

        ShardEvent[] added = [new() { Id = "jan", Amount = 1, CreatedAt = new DateTime(2024, 1, 20) }, new() { Id = "may", Amount = 1, CreatedAt = new DateTime(2024, 5, 20) }];
        await using (db.BeginTransaction())
        {
            Assert.Equal(2, await db.Insert(added).ExecuteAsync(Ct));
            Assert.Equal(8, await db.Query<ShardEvent>().CountAsync(Ct));
        }
        Assert.Equal(6, await db.Query<ShardEvent>().CountAsync(Ct));

        await using (var tx = db.BeginTransaction())
        {
            await db.Insert(added).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(["jan", "may"], (await db.Query<ShardEvent>().Where(e => e.Amount == 1).OrderBy(e => e.CreatedAt).ToListAsync(Ct)).Select(e => e.Id));
    }
}
