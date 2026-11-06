using System.Linq.Expressions;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// code-first（TS 的 code-first DDL、generated columns and index kinds、code-first sync 三组）：按实体建表后各类型往返、库端默认值、
// 唯一索引；计算列与降序 / 组合唯一索引；建表带注释、索引、外键后读结构核对、再比较没有要做的事；改名、加列、补索引与只列出不处理的差异
public abstract partial class LiveSuite
{
    /// <summary>各种列与唯一索引（对应 TS 的 Gadget）。</summary>
    [Table("t_live_gadget")]
    [TableIndex(nameof(Name), Unique = true)]
    public sealed class Gadget
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(DbType = "VARCHAR(50)")]
        public string Name { get; set; } = "";

        [Column(DbType = "DECIMAL(10,2)")]
        public decimal Price { get; set; }

        [Column(DbDefault = "0")]
        public int? Qty { get; set; }

        public bool Active { get; set; }

        public DateTime SeenAt { get; set; }

        public DateOnly? Born { get; set; }

        public byte[]? Photo { get; set; }

        [Column(Converter = typeof(JsonValueConverter<List<string>>), DbType = "VARCHAR(200)")]
        public List<string>? Tags { get; set; }
    }

    [Fact]
    public async Task CreatesATableFromTheEntityAndRoundTripsEveryColumnType()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(Gadget)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(Gadget)], Ct);
        var seenAt = new DateTime(2026, 3, 4, 5, 6, 7, 8);
        var gadget = new Gadget { Name = "widget", Price = 12.50m, Active = true, SeenAt = seenAt, Tags = ["a", "b"] };
        await db.Insert(gadget).FillGeneratedKeys().ExecuteAsync(Ct);
        Assert.True(gadget.Id > 0);

        // Qty 没给：库里的 DEFAULT 0 生效；可空列没给就是 null
        var row = await db.Query<Gadget>().SingleAsync(Ct);
        Assert.Equal(("widget", 12.50m, (int?)0, true, (DateOnly?)null, (byte[]?)null), (row.Name, row.Price, row.Qty, row.Active, row.Born, row.Photo));
        Assert.Equal(seenAt, row.SeenAt);
        Assert.Equal(["a", "b"], row.Tags);

        // 唯一索引
        await Assert.ThrowsAsync<EasyQueryException>(() => db.Insert(new Gadget { Name = "widget", Price = 1m, SeenAt = seenAt, Tags = [] }).ExecuteAsync(Ct));
    }

    /// <summary>计算列与几种索引（对应 TS 的 t_gline）：total 不在表里，由 qty * price 算出；(sku, qty desc) 普通索引，(sku, price) 唯一索引。</summary>
    [Table("t_live_gline")]
    [TableIndex(nameof(Sku), nameof(Qty), Descending = [nameof(Qty)])]
    [TableIndex(nameof(Sku), nameof(Price), Unique = true, Name = "t_live_gline_sku_price_uk")]
    public sealed class GLine
    {
        [Column(PrimaryKey = true)]
        public int Id { get; set; }

        [Column(DbType = "VARCHAR(40)")]
        public string Sku { get; set; } = "";

        public int Qty { get; set; }

        public int Price { get; set; }

        [Computed(nameof(TotalSql))]
        public int Total { get; set; }

        private static Expression<Func<GLine, int>> TotalSql => l => l.Qty * l.Price;
    }

    // C# 没有库端的生成列（GENERATED ALWAYS AS）、表达式索引与部分索引：计算列在查询里展开（Computed），写入时不写；索引建好后读得回来
    [Fact]
    public async Task ComputesColumnsInQueriesNeverWritesThemAndBuildsIndexKinds()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(GLine)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(GLine)], Ct);
        Assert.DoesNotContain("total", db.Insert(new GLine { Id = 2, Sku = "B", Qty = 1, Price = 1, Total = 9 }).ToSql(), StringComparison.OrdinalIgnoreCase);

        await db.Insert(new GLine { Id = 1, Sku = "A1", Qty = 3, Price = 5, Total = 999 }).ExecuteAsync(Ct);
        Assert.Equal(15, (await db.Query<GLine>().FindAsync(1, Ct))!.Total);
        await db.Update<GLine>().Set(l => l.Qty, 4).Where(l => l.Id == 1).ExecuteAsync(Ct);
        Assert.Equal(20, (await db.Query<GLine>().FindAsync(1, Ct))!.Total);
        Assert.Equal(1, await db.Query<GLine>().Where(l => l.Total >= 20).CountAsync(Ct));

        var table = (await db.CodeFirst.ReadTablesAsync(Ct)).Single(t => Is(t.Name, "t_live_gline"));
        Assert.DoesNotContain(table.Columns, c => Is(c.Name, "total"));
        var indexes = table.Indexes.ToDictionary(i => i.Name.ToLowerInvariant(), i => (i.Unique, Columns: string.Join(",", i.Columns).ToLowerInvariant()));
        Assert.Equal((false, "sku,qty"), indexes["t_live_gline_sku_qty_idx"]);
        Assert.Equal((true, "sku,price"), indexes["t_live_gline_sku_price_uk"]);
        Assert.Empty((await db.CodeFirst.PlanSyncAsync([typeof(GLine)], Ct)).Statements);
        await Assert.ThrowsAsync<EasyQueryException>(() => db.Insert(new GLine { Id = 3, Sku = "A1", Qty = 1, Price = 5 }).ExecuteAsync(Ct));
    }

    /// <summary>被引用的表：带表注释与列注释（对应 TS 的 SyncA）。</summary>
    [Table("t_live_sync_a", Comment = "同步测试")]
    public sealed class SyncA
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(DbType = "VARCHAR(50)", Comment = "名称")]
        public string Name { get; set; } = "";
    }

    /// <summary>经 AId 引用 <see cref="SyncA"/>（外键），title 上有索引（对应 TS 的 SyncB）。</summary>
    [Table("t_live_sync_b")]
    [TableIndex(nameof(Title))]
    public class SyncB
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public long? AId { get; set; }

        [Column(DbType = "VARCHAR(100)")]
        public string Title { get; set; } = "";

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AId)]), ForeignKey]
        public SyncA? A { get; set; }
    }

    /// <summary>同一张表，外键带级联删除（<see cref="LiveSuiteOptions.ForeignKeyActions"/>）。</summary>
    [Table("t_live_sync_b")]
    [TableIndex(nameof(Title))]
    public sealed class SyncBCascade
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public long? AId { get; set; }

        [Column(DbType = "VARCHAR(100)")]
        public string Title { get; set; } = "";

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AId)]), ForeignKey(Action = "ON DELETE CASCADE")]
        public SyncA? A { get; set; }
    }

    /// <summary><see cref="SyncA"/> 的下一版：name 改名成 title，加可空的 note 列和它的索引。</summary>
    [Table("t_live_sync_a", Comment = "同步测试")]
    [TableIndex(nameof(Note))]
    public sealed class SyncA2
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(DbType = "VARCHAR(50)", Comment = "名称"), RenamedFrom("name")]
        public string Title { get; set; } = "";

        [Column(DbType = "VARCHAR(20)")]
        public string? Note { get; set; }
    }

    /// <summary>再下一版：title 改成不可空的整数，note 删掉了——同步不改类型、不删列，只列出差异。</summary>
    [Table("t_live_sync_a")]
    public sealed class SyncA3
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public int Title { get; set; }
    }

    private static bool Is(string name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task CreatesTablesWithCommentsIndexesAndForeignKeysThenFindsNothingLeftToDo()
    {
        var db = await SeededAsync();
        Type[] types = [typeof(SyncA), Options.ForeignKeyActions ? typeof(SyncBCascade) : typeof(SyncB)];
        await db.CodeFirst.DropTablesIfExistsAsync(types.Reverse(), Ct);
        try
        {
            var plan = await db.CodeFirst.SyncTablesAsync(types, Ct);
            Assert.True(plan.Statements.Count >= 2, plan.ToSql());
            var tables = await db.CodeFirst.ReadTablesAsync(Ct);
            var a = tables.Single(t => Is(t.Name, "t_live_sync_a"));
            var b = tables.Single(t => Is(t.Name, "t_live_sync_b"));
            Assert.Equal(["id", "name"], a.Columns.Select(c => c.Name.ToLowerInvariant()));
            Assert.Equal(["id"], a.PrimaryKey.Select(c => c.ToLowerInvariant()));
            Assert.Contains(b.Indexes, i => Is(i.Name, "t_live_sync_b_title_idx") && !i.Unique);
            var foreignKey = Assert.Single(b.ForeignKeys);
            Assert.Equal(("a_id", "t_live_sync_a"), (string.Join(",", foreignKey.Columns).ToLowerInvariant(), foreignKey.ReferencedTable.ToLowerInvariant()));
            // SQLite 没有注释
            if (Dialect is not SqliteDialect)
            {
                Assert.Equal("同步测试", a.Comment);
                Assert.Equal("名称", a.FindColumn("name")!.Comment);
            }

            var again = await db.CodeFirst.PlanSyncAsync(types, Ct);
            Assert.Empty(again.Statements);
            Assert.Empty(again.Differences);
            Assert.True(await db.CodeFirst.TableExistsAsync(typeof(SyncA), Ct));

            // 外键生效：引用不存在的行报错；有级联删除时删掉被引用的行，引用它的行跟着删
            await Assert.ThrowsAsync<EasyQueryException>(() => db.ExecuteSqlAsync(
                $"INSERT INTO {Sql.Identifier("t_live_sync_b")} ({Sql.Identifier("a_id")}, {Sql.Identifier("title")}) VALUES ({42}, {"orphan"})", Ct));
            if (Options.ForeignKeyActions)
            {
                var parent = new SyncA { Name = "parent" };
                await db.Insert(parent).FillGeneratedKeys().ExecuteAsync(Ct);
                await db.Insert(new SyncBCascade { AId = parent.Id, Title = "child" }).ExecuteAsync(Ct);
                await db.Delete<SyncA>().Where(x => x.Id == parent.Id).ExecuteAsync(Ct);
                Assert.Equal(0, await db.Query<SyncBCascade>().CountAsync(Ct));
            }
        }
        finally
        {
            await db.CodeFirst.DropTablesIfExistsAsync(types.Reverse(), Ct);
        }
        Assert.False(await db.CodeFirst.TableExistsAsync(typeof(SyncA), Ct));
    }

    [Fact]
    public async Task RenamesAddsAndIndexesColumnsOfAnExistingTableAndListsWhatItWillNotChange()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(SyncB), typeof(SyncA)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(SyncA)], Ct);
        try
        {
            await db.Insert(new SyncA { Name = "kept" }).ExecuteAsync(Ct);
            var plan = await db.CodeFirst.SyncTablesAsync([typeof(SyncA2)], Ct);
            Assert.True(plan.Statements.Count >= 3, plan.ToSql());
            Assert.Empty(plan.Differences);
            Assert.Equal("kept", (await db.Query<SyncA2>().SingleAsync(Ct)).Title);
            var again = await db.CodeFirst.PlanSyncAsync([typeof(SyncA2)], Ct);
            Assert.Empty(again.Statements);
            Assert.Empty(again.Differences);
            Assert.Contains((await db.CodeFirst.ReadTablesAsync(Ct)).Single(t => Is(t.Name, "t_live_sync_a")).Indexes, i => Is(i.Name, "t_live_sync_a_note_idx"));

            // 类型变了、列删了：不自动改（不比较类型），可空性不一致与多出来的列写进差异
            var diff = await db.CodeFirst.PlanSyncAsync([typeof(SyncA3)], Ct);
            Assert.Empty(diff.Statements);
            Assert.Contains(diff.Differences, d => d.Contains("t_live_sync_a.title", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(diff.Differences, d => d.Contains("t_live_sync_a.note", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await db.CodeFirst.DropTablesIfExistsAsync([typeof(SyncA)], Ct);
        }
    }
}
