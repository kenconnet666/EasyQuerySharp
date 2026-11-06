using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Sharding;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 分库分表（0006）里 Java 用例没有覆盖的部分：内置规则的取模与时间区间的裁剪、额外分片键、嵌套处与多个单元的限制、写入的报错。
/// 两个 SQLite 共享内存库当作 ds0、ds1。
/// </summary>
public sealed class ShardingRuleTest : IAsyncLifetime
{
    private const string First = "Data Source=eqs-extras-shard-0;Mode=Memory;Cache=Shared";
    private const string Second = "Data Source=eqs-extras-shard-1;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepFirst = new(First);
    private readonly SqliteConnection _keepSecond = new(Second);

    /// <summary>按 id 取模 3 张表（ds0）。</summary>
    [Table("t_rule_mod")]
    public sealed class ModRow
    {
        [Column(PrimaryKey = true)]
        public string Id { get; set; } = "";

        public int Amount { get; set; }
    }

    /// <summary>按月 2024-01 … 2024-06：1 – 3 月在 ds0，4 – 6 月在 ds1；id 末 6 位是月份。</summary>
    [Table("t_rule_month")]
    public sealed class MonthRow
    {
        [Column(PrimaryKey = true)]
        public string Id { get; set; } = "";

        public int Amount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>不分片的表（只在默认数据源）。</summary>
    [Table("t_rule_plain")]
    public sealed class PlainRow
    {
        [Column(PrimaryKey = true)]
        public string Id { get; set; } = "";
    }

    private EasyDb Db { get; } = new(o => o
        .Use(SqliteDialect.Instance, SqliteFactory.Instance.CreateDataSource(First)).AddListener(SqlCapture.Instance)
        .AddDataSource("ds1", SqliteFactory.Instance.CreateDataSource(Second))
        .Shard<ModRow>(r => r.Mod(t => t.Id, 3))
        .Shard<MonthRow>(r => r.ByMonth(t => t.CreatedAt, from: new DateTime(2024, 1, 1), to: () => new DateTime(2024, 6, 30), dataSource: d => d.Month <= 3 ? "ds0" : "ds1")
            .ExtraKey(t => t.Id, id => id.Length > 6 && DateTime.TryParseExact(id[^6..], "yyyyMM", null, System.Globalization.DateTimeStyles.None, out var month) ? month : null)
            .DefaultOrder(t => t.CreatedAt, descending: true)));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await _keepFirst.OpenAsync();
        await _keepSecond.OpenAsync();
        await Db.CodeFirst.CreateTablesAsync(typeof(ModRow), typeof(MonthRow), typeof(PlainRow));
        await Db.Insert(Enumerable.Range(0, 12).Select(i => new MonthRow { Id = $"m{i}{2024:D4}{i / 2 + 1:D2}", Amount = i, CreatedAt = new DateTime(2024, i / 2 + 1, 10 + i % 2) })).ExecuteAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _keepFirst.DisposeAsync();
        await _keepSecond.DisposeAsync();
    }

    // Java 的 Math.abs(String.hashCode() % n)："polygenelubricants" 的哈希正好是 int.MinValue
    [Fact]
    public void ModUsesJavaStringHashCode()
    {
        var rule = Db.GetShardRule<ModRow>()!;
        Assert.Equal(["t_rule_mod_0", "t_rule_mod_1", "t_rule_mod_2"], rule.Nodes.Select(n => n.Table));
        Assert.Equal("t_rule_mod_2", rule.NodeOf(new ModRow { Id = "20000" }).Table);
        Assert.Equal("t_rule_mod_0", rule.NodeOf(new ModRow { Id = "20001" }).Table);
        Assert.Equal("t_rule_mod_2", rule.NodeOf(new ModRow { Id = "polygenelubricants" }).Table);
        Assert.Equal(rule.Nodes, rule.Prune(nameof(ModRow.Id), ShardOperator.GreaterThan, "1"));
    }

    // 区间的端点：< 恰好等于某月的起点时不含那个月；额外键只有等值能裁剪
    [Fact]
    public void MonthRulePrunesRanges()
    {
        var rule = Db.GetShardRule<MonthRow>()!;
        Assert.Equal(["ds0", "ds0", "ds0", "ds1", "ds1", "ds1"], rule.Nodes.Select(n => n.DataSource));
        Assert.Equal(["CreatedAt", "Id"], rule.KeyProperties);
        string[] Months(ShardOperator op, object value) => [.. rule.Prune(nameof(MonthRow.CreatedAt), op, value).Select(n => n.Table[^2..])];
        Assert.Equal(["03"], Months(ShardOperator.Equal, new DateTime(2024, 3, 15)));
        Assert.Equal(["03", "04", "05", "06"], Months(ShardOperator.GreaterThan, new DateTime(2024, 3, 15)));
        Assert.Equal(["01", "02"], Months(ShardOperator.LessThan, new DateTime(2024, 3, 1)));
        Assert.Equal(["01", "02", "03"], Months(ShardOperator.LessThan, new DateTime(2024, 3, 1, 0, 0, 1)));
        Assert.Equal(["01", "02", "03"], Months(ShardOperator.LessThanOrEqual, new DateTime(2024, 3, 1)));
        Assert.Equal(["05"], rule.Prune(nameof(MonthRow.Id), ShardOperator.Equal, "x202405").Select(n => n.Table[^2..]));
        Assert.Equal(rule.Nodes, rule.Prune(nameof(MonthRow.Id), ShardOperator.Equal, "short"));
        Assert.Equal(rule.Nodes, rule.Prune(nameof(MonthRow.Id), ShardOperator.GreaterThan, "x202405"));
        Assert.Throws<InvalidOperationException>(() => rule.NodeOf(new MonthRow { Id = "late", CreatedAt = new DateTime(2024, 7, 1) }));
    }

    // 额外键与主键各自裁剪后取交集；参数在左边时比较反过来；OR 有一边不能裁剪时整体不约束
    [Fact]
    public async Task PrunesWithExtraKeyAndCombinations()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal(4, (await Db.Query<MonthRow>().Where(t => t.Id == "m4202403").ToListAsync(Ct)).Single().Amount);
        Assert.Equal("ds0", listen.Last.Statement.DataSource);
        Assert.Contains("t_rule_month_202403", listen.Last.Statement.Text);

        var before = listen.Events.Count;
        Assert.Empty(await Db.Query<MonthRow>().Where(t => t.Id == "m4202403" && t.CreatedAt >= new DateTime(2024, 4, 1)).ToListAsync(Ct));
        Assert.Equal(before, listen.Events.Count);

        var since = new DateTime(2024, 5, 1);
        Assert.Equal(4, await Db.Query<MonthRow>().Where(t => since <= t.CreatedAt).CountAsync(Ct));
        Assert.Equal("ds1", listen.Last.Statement.DataSource);

        before = listen.Events.Count;
        Assert.Equal(8, await Db.Query<MonthRow>().Where(t => t.CreatedAt < new DateTime(2024, 2, 1) || t.Amount > 5).CountAsync(Ct));
        Assert.Equal(["ds0", "ds1"], listen.Events.Skip(before).Select(e => e.Statement.DataSource).Order());
    }

    // 分到两个数据源：并行执行后归并；不能合并的形状报 NotSupportedException
    [Fact]
    public async Task RejectsShapesThatCannotMergeAcrossUnits()
    {
        Assert.Equal([11, 10, 9], (await Db.Query<MonthRow>().Take(3).ToListAsync(Ct)).Select(t => t.Amount));
        await Assert.ThrowsAsync<NotSupportedException>(() => Db.Query<MonthRow>().Select(t => t.Amount).Distinct().ToListAsync(Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() => Db.Query<MonthRow>().AverageAsync(t => t.Amount, Ct));
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in Db.Query<MonthRow>().AsAsyncEnumerable(Ct)) { }
        });
        // 落在一个单元上时可以流式读取
        var streamed = new List<int>();
        await foreach (var row in Db.Query<MonthRow>().Where(t => t.CreatedAt < new DateTime(2024, 2, 1)).OrderBy(t => t.Amount).AsAsyncEnumerable(Ct)) streamed.Add(row.Amount);
        Assert.Equal([0, 1], streamed);
    }

    // 最外层两张分片表、联不分片的表却落在别的数据源、子查询里的分片表与外层不在一个数据源：都不支持
    [Fact]
    public async Task RejectsCrossDataSourceShapes()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => Db.Query<ModRow>().InnerJoin<ModRow>((a, b) => a.Id == b.Id).ToListAsync(Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            Db.Query<MonthRow>().LeftJoin<PlainRow>((m, p) => m.Id == p.Id).Where((m, _) => m.CreatedAt >= new DateTime(2024, 5, 1)).ToListAsync(Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            Db.Query<PlainRow>().Where(p => Sql.Exists(Db.Query<MonthRow>().Where(m => m.Id == p.Id && m.CreatedAt >= new DateTime(2024, 5, 1)))).ToListAsync(Ct));
        // 同一个数据源里的子查询内联成 UNION ALL；ToSql 仍是逻辑表名
        var inline = Db.Query<PlainRow>().Where(p => Sql.Exists(Db.Query<MonthRow>().Where(m => m.Id == p.Id && m.CreatedAt < new DateTime(2024, 3, 1))));
        Assert.Contains("FROM \"t_rule_month\" ", inline.ToSql());
        using var listen = SqlCapture.Listen();
        Assert.Empty(await inline.ToListAsync(Ct));
        Assert.Contains("(SELECT * FROM \"t_rule_month_202401\" UNION ALL SELECT * FROM \"t_rule_month_202402\")", listen.Last.Statement.Text);
    }

    // 节点数上限、分片键为 null、Returning / INSERT … SELECT、未登记的数据源
    [Fact]
    public async Task GuardsWritesAndLimits()
    {
        var narrow = new EasyDb(o =>
        {
            o.MaxShardNodes = 2;
            o.Use(SqliteDialect.Instance, SqliteFactory.Instance.CreateDataSource(First)).Shard<ModRow>(r => r.Mod(t => t.Id, 3));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => narrow.Query<ModRow>().ToListAsync(Ct));
        Assert.Empty(await narrow.Query<ModRow>().Where(t => t.Id == "1").ToListAsync(Ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Insert(new MonthRow { Id = "x", CreatedAt = new DateTime(2023, 1, 1) }).ExecuteAsync(Ct));
        Assert.Throws<NotSupportedException>(() => Db.Delete<ModRow>().Where(t => t.Amount > 0).Returning());
        await Assert.ThrowsAsync<NotSupportedException>(() => Db.Query<ModRow>().InsertInto("t_rule_plain").ExecuteAsync(Ct));
        Assert.Throws<InvalidOperationException>(() => new EasyDb(o => o.UseSqlite().Shard<ModRow>(r => r.Mod(t => t.Id, 3, dataSource: "nowhere"))));
    }
}
