using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Docs;

/// <summary>"分库分表"页的卡片：取模分表与按月分表的写入路由、按条件裁剪、同一数据源的分表在库内 UNION ALL。见 docs/guide/sharding.md。</summary>
public static class ShardingExamples
{
    /// <summary>订单流水：按 id 取模或按创建时间按月分表（卡片里各自登记规则）。</summary>
    [Table("t_order_log")]
    public class OrderLog
    {
        [Column(PrimaryKey = true)]
        public string Id { get; set; } = "";

        public long UserId { get; set; }

        public int Amount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    [Example("sharding", "按 id 取模分三张表：写入落到各自的表，按 id 查只查一张，IN 查命中的几张，全表在库内 UNION ALL；按条件更新逐张表执行")]
    public static async Task<object> ModTables(EasyDb db)
    {
        var sharded = Sharded(db, o => o.Shard<OrderLog>(r => r.Mod(l => l.Id, 3)));
        await sharded.CodeFirst.CreateTablesAsync(typeof(OrderLog));
        try
        {
            await sharded.Insert(
                new OrderLog { Id = "a1", UserId = 1, Amount = 10, CreatedAt = new DateTime(2025, 1, 5) },
                new OrderLog { Id = "a2", UserId = 1, Amount = 20, CreatedAt = new DateTime(2025, 2, 5) },
                new OrderLog { Id = "a3", UserId = 2, Amount = 30, CreatedAt = new DateTime(2025, 3, 5) },
                new OrderLog { Id = "a4", UserId = 2, Amount = 40, CreatedAt = new DateTime(2025, 3, 6) }).ExecuteAsync();
            var a1 = await sharded.Query<OrderLog>().Where(l => l.Id == "a1").SingleAsync();
            string[] ids = ["a2", "a3"];
            var picked = await sharded.Query<OrderLog>().Where(l => ids.Contains(l.Id)).OrderBy(l => l.Id).Select(l => l.Amount).ToListAsync();
            var top = await sharded.Query<OrderLog>().OrderByDescending(l => l.Amount).Take(2).Select(l => l.Id).ToListAsync();
            var updated = await sharded.Update<OrderLog>().Set(l => l.Amount, l => l.Amount + 1).Where(l => l.UserId == 1).ExecuteAsync();
            return new { a1.Amount, Picked = picked, Total = await sharded.Query<OrderLog>().CountAsync(), Top = top, Updated = updated };
        }
        finally
        {
            await sharded.CodeFirst.DropTablesIfExistsAsync(typeof(OrderLog));
        }
    }

    [Example("sharding", "按月分表：时间条件只查命中的月份；没写排序、取前几行时按规则的默认排序（时间倒序）")]
    public static async Task<object> MonthTables(EasyDb db)
    {
        var sharded = Sharded(db, o => o.Shard<OrderLog>(r => r
            .ByMonth(l => l.CreatedAt, from: new DateTime(2025, 1, 1), to: () => new DateTime(2025, 3, 31))
            .DefaultOrder(l => l.CreatedAt, descending: true)));
        await sharded.CodeFirst.CreateTablesAsync(typeof(OrderLog));
        try
        {
            await sharded.Insert(
                new OrderLog { Id = "j1", UserId = 1, Amount = 10, CreatedAt = new DateTime(2025, 1, 5) },
                new OrderLog { Id = "f1", UserId = 1, Amount = 20, CreatedAt = new DateTime(2025, 2, 5) },
                new OrderLog { Id = "m1", UserId = 2, Amount = 30, CreatedAt = new DateTime(2025, 3, 5) }).ExecuteAsync();
            var sinceFebruary = await sharded.Query<OrderLog>().Where(l => l.CreatedAt >= new DateTime(2025, 2, 1)).SumAsync(l => l.Amount);
            var latest = await sharded.Query<OrderLog>().Take(2).Select(l => l.Id).ToListAsync();
            return new { SinceFebruary = sinceFebruary, Latest = latest };
        }
        finally
        {
            await sharded.CodeFirst.DropTablesIfExistsAsync(typeof(OrderLog));
        }
    }

    // 同一个数据源上另建一个登记了分片规则的 EasyDb（卡片的监听器照样挂上）
    private static EasyDb Sharded(EasyDb db, Action<EasyDbOptions> shard) => new(o =>
    {
        o.Use(db.Options.Dialect, db.Options.DataSource);
        foreach (var listener in db.Options.Listeners) o.AddListener(listener);
        shard(o);
    });
}
