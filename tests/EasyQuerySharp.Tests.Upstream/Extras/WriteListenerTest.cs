using System.Collections.Concurrent;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Metadata;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 写入监听（<see cref="IWriteListener"/>，0007 §2）：每种写入路径都按表合并成一个事件、主键从语句里取出，
/// 事务里推迟到提交之后、回滚不通知，监听器的异常不吞。C# 独有，在进程内 SQLite 上跑。
/// </summary>
public sealed class WriteListenerTest : IAsyncLifetime
{
    private readonly string _cs = $"Data Source=eqs-write-listener-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;
    private readonly RecordingListener _listener = new();
    private readonly EasyDb _db;

    public WriteListenerTest()
    {
        _keepAlive = new SqliteConnection(_cs);
        _db = new EasyDb(o => o.WithJavaNulls().UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)).AddListener(SqlCapture.Instance).AddWriteListener(_listener));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync(Ct);
        await _db.CodeFirst.CreateTablesAsync([typeof(WlItem), typeof(WlLine), typeof(WlSoft), typeof(WlCode)], Ct);
        await _db.ExecuteSqlRawAsync("CREATE TABLE wl_item_2 AS SELECT * FROM wl_item WHERE 0", cancellationToken: Ct);
        await _db.ExecuteSqlRawAsync("CREATE UNIQUE INDEX wl_item_name ON wl_item (name)", cancellationToken: Ct);
        await _db.ExecuteSqlRawAsync("INSERT INTO wl_item (id, name, qty) VALUES ('a', 'A', 1), ('b', 'B', 2), ('c', 'C', 3)", cancellationToken: Ct);
        await _db.ExecuteSqlRawAsync("INSERT INTO wl_soft (id, name, deleted) VALUES (1, 'one', 0), (2, 'two', 0)", cancellationToken: Ct);
        _listener.Events.Clear();
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    private WriteEvent Single() => Assert.Single(_listener.Events);

    private static void AssertEvent(WriteEvent e, string table, WriteKind kind, Type? entityType, params object[]? keys)
    {
        Assert.Equal((table, kind, entityType), (e.Table, e.Kind, e.EntityType));
        if (keys is null) Assert.Null(e.Keys);
        else Assert.Equal(keys, e.Keys);
    }

    [Fact]
    public async Task InsertsOfOneCallAreMergedIntoOneEvent()
    {
        await _db.Insert(new WlItem { Id = "d" }, new WlItem { Id = "e" }).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Insert, typeof(WlItem), "d", "e");
    }

    [Fact]
    public async Task BatchedInsertsAreOneEvent()
    {
        await _db.Insert(new WlItem { Id = "d" }, new WlItem { Id = "e" }, new WlItem { Id = "f" }).Batch().ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Insert, typeof(WlItem), "d", "e", "f");
    }

    // 自增主键不在语句里：主键不明，回填了也一样（监听器只看语句）
    [Fact]
    public async Task GeneratedKeysAreUnknown()
    {
        await _db.Insert(new WlLine { ItemId = "a", Sku = "s" }).FillGeneratedKeys().ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_line", WriteKind.Insert, typeof(WlLine), null);
    }

    [Fact]
    public async Task UpdateByEntityCarriesItsKey()
    {
        await _db.Update(new WlItem { Id = "a", Name = "A2", Qty = 1 }).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Update, typeof(WlItem), "a");
    }

    public static TheoryData<string, string[]?> ConditionShapes => new()
    {
        { "eq", ["a"] },
        { "in", ["a", "b"] },
        { "or-of-keys", ["a", "b"] },
        { "key-and-other", ["a"] },
        { "other", null },
        { "or-with-other", null },
        { "not-in", null },
    };

    // 顶层 AND 里第一个主键的 = / IN（或全由它们组成的 OR）给出主键；别的形状主键不明
    [Theory, MemberData(nameof(ConditionShapes))]
    public async Task UpdateByConditionTakesKeysFromTheWhere(string shape, string[]? keys)
    {
        var ids = new List<string> { "a", "b" };
        var update = _db.Update<WlItem>().Set(i => i.Qty, 9);
        update = shape switch
        {
            "eq" => update.Where(i => i.Id == "a"),
            "in" => update.Where(i => ids.Contains(i.Id!)),
            "or-of-keys" => update.Where(i => i.Id == "a" || i.Id == "b"),
            "key-and-other" => update.Where(i => i.Name == "A").Where(i => i.Id == "a"),
            "other" => update.Where(i => i.Name == "A"),
            "or-with-other" => update.Where(i => i.Id == "a" || i.Name == "B"),
            _ => update.Where(i => !ids.Contains(i.Id!)),
        };
        await update.ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Update, typeof(WlItem), keys);
    }

    [Fact]
    public async Task ChangingTheKeyMakesKeysUnknown()
    {
        await _db.Update<WlItem>().Set(i => i.Id, "z").Where(i => i.Id == "c").ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Update, typeof(WlItem), null);
    }

    [Fact]
    public async Task DeletesCarryKeys()
    {
        await _db.Delete(new WlItem { Id = "a" }).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Delete, typeof(WlItem), "a");
        _listener.Events.Clear();
        await _db.Delete<WlItem>().WhereByIds(new[] { "b", "c" }).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Delete, typeof(WlItem), "b", "c");
    }

    // 逻辑删除是 UPDATE；Hard() 才是 DELETE
    [Fact]
    public async Task LogicalDeleteIsAnUpdate()
    {
        await _db.Delete<WlSoft>().WhereById(1).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_soft", WriteKind.Update, typeof(WlSoft), 1);
        _listener.Events.Clear();
        await _db.Delete<WlSoft>().WhereById(2).Hard().ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_soft", WriteKind.Delete, typeof(WlSoft), 2);
    }

    // SQLite 的 ON CONFLICT (id) 只在主键上处理冲突：更新到的就是语句里的主键
    [Fact]
    public async Task UpsertIsInsertAndUpdate()
    {
        await _db.Insert(new WlItem { Id = "a", Name = "A3" }).OnConflictUpdate().ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Insert | WriteKind.Update, typeof(WlItem), "a");
        _listener.Events.Clear();
        await _db.Insert(new WlItem { Id = "a", Name = "A4" }).OnConflictIgnore().ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Insert, typeof(WlItem), "a");
        _listener.Events.Clear();
        // 约束列不是主键：冲突时更新的行不一定是语句里的主键
        await _db.Insert(new WlItem { Id = "x", Name = "A3", Qty = 5 }).OnConflictUpdate(i => i.Qty, i => i.Name).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Insert | WriteKind.Update, typeof(WlItem), null);
    }

    [Fact]
    public async Task ReturningWritesAreNotified()
    {
        var updated = await _db.Update<WlItem>().Set(i => i.Qty, 5).Where(i => i.Id == "a").Returning().ExecuteAsync(Ct);
        Assert.Equal(5, Assert.Single(updated).Qty);
        AssertEvent(Single(), "wl_item", WriteKind.Update, typeof(WlItem), "a");
        _listener.Events.Clear();
        await _db.Delete<WlItem>().Where(i => i.Id == "b").Returning(i => i.Id).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Delete, typeof(WlItem), "b");
    }

    // 按字典写入没有实体类型，主键也不知道
    [Fact]
    public async Task MapWritesHaveNoEntityType()
    {
        await _db.InsertMap("wl_item", new Dictionary<string, object?> { ["id"] = "m", ["name"] = "M" }).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Insert, null, null);
        _listener.Events.Clear();
        await _db.UpdateMap("wl_item", new Dictionary<string, object?> { ["id"] = "m", ["name"] = "M2" }).WhereColumns("id").ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item", WriteKind.Update, null, null);
    }

    // AsTable 之后的实际表名；INSERT … SELECT 的主键不明
    [Fact]
    public async Task EventsUseTheActualTable()
    {
        await _db.Insert(new WlItem { Id = "t" }).AsTable("wl_item_2").ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item_2", WriteKind.Insert, typeof(WlItem), "t");
        _listener.Events.Clear();
        await _db.Query<WlItem>().Where(i => i.Id != "t").InsertInto("wl_item_2").ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_item_2", WriteKind.Insert, typeof(WlItem), null);
    }

    [Fact]
    public async Task ConvertedKeysAreReportedAsPropertyValues()
    {
        await _db.Insert(new WlCode { Id = new Sku("k1"), Name = "n" }).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_code", WriteKind.Insert, typeof(WlCode), new Sku("k1"));
        _listener.Events.Clear();
        await _db.Update<WlCode>().Set(c => c.Name, "n2").WhereById(new Sku("k1")).ExecuteAsync(Ct);
        AssertEvent(Single(), "wl_code", WriteKind.Update, typeof(WlCode), new Sku("k1"));
    }

    // 原生 SQL 不经过渲染器，不通知
    [Fact]
    public async Task RawSqlIsNotNotified()
    {
        await _db.ExecuteSqlRawAsync("UPDATE wl_item SET qty = 0", cancellationToken: Ct);
        Assert.Empty(_listener.Events);
    }

    // 事务里推迟到提交之后，按表合并；提交时已经不在事务里
    [Fact]
    public async Task TransactionNotifiesAfterCommit()
    {
        await using (var tx = _db.BeginTransaction())
        {
            Assert.True(_db.InTransaction);
            await _db.Update<WlItem>().Set(i => i.Qty, 7).Where(i => i.Id == "a").ExecuteAsync(Ct);
            await _db.Delete<WlItem>().WhereById("b").ExecuteAsync(Ct);
            await _db.Insert(new WlLine { ItemId = "a", Sku = "s" }).ExecuteAsync(Ct);
            Assert.Empty(_listener.Events);
            await tx.CommitAsync(Ct);
        }
        Assert.False(_db.InTransaction);
        Assert.Equal(2, _listener.Events.Count);
        AssertEvent(_listener.Events[0], "wl_item", WriteKind.Update | WriteKind.Delete, typeof(WlItem), "a", "b");
        AssertEvent(_listener.Events[1], "wl_line", WriteKind.Insert, typeof(WlLine), null);
        Assert.All(_listener.Events, e => Assert.False(_listener.InTransactionWhenNotified[e]));
    }

    [Fact]
    public async Task RollbackIsNotNotified()
    {
        await using (_db.BeginTransaction())
            await _db.Update<WlItem>().Set(i => i.Qty, 7).Where(i => i.Id == "a").ExecuteAsync(Ct);
        Assert.Empty(_listener.Events);
        Assert.Equal(1, (await _db.Query<WlItem>().FindAsync("a", Ct))!.Qty);
    }

    // 聚合保存：父表与子表各一个事件，提交后通知
    [Fact]
    public async Task AggregateSaveNotifiesEachTable()
    {
        using var tracking = _db.BeginTracking();
        var item = new WlItem { Id = "n", Name = "N", Lines = [new WlLine { Sku = "s1" }, new WlLine { Sku = "s2" }] };
        await using (var tx = _db.BeginTransaction())
        {
            await _db.Save(item).ExecuteAsync(Ct);
            Assert.Empty(_listener.Events);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(2, _listener.Events.Count);
        AssertEvent(_listener.Events[0], "wl_item", WriteKind.Insert, typeof(WlItem), "n");
        AssertEvent(_listener.Events[1], "wl_line", WriteKind.Insert, typeof(WlLine), null);
    }

    // 监听器失败：异常包成 EasyQueryException 抛给调用方，数据库已经写入
    [Fact]
    public async Task ListenerFailureIsRaisedAfterTheWrite()
    {
        _listener.Fail = true;
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => _db.Update<WlItem>().Set(i => i.Qty, 8).Where(i => i.Id == "a").ExecuteAsync(Ct));
        Assert.Contains("数据库已写入", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        _listener.Fail = false;
        Assert.Equal(8, (await _db.Query<WlItem>().FindAsync("a", Ct))!.Qty);
    }

    // 不在事务里逐条执行时，失败之前自动提交的写入照样通知
    [Fact]
    public async Task WritesBeforeAFailureAreNotified()
    {
        await Assert.ThrowsAsync<EasyQueryException>(() => _db.Insert(new WlItem { Id = "d" }, new WlItem { Id = "a" }).ExecuteAsync(Ct));
        AssertEvent(Single(), "wl_item", WriteKind.Insert, typeof(WlItem), "d");
    }

    // 批量在自己的本地事务里，失败整批回滚，不通知
    [Fact]
    public async Task FailedBatchIsNotNotified()
    {
        await Assert.ThrowsAsync<EasyQueryException>(() => _db.Insert(new WlItem { Id = "d" }, new WlItem { Id = "a" }).Batch().ExecuteAsync(Ct));
        Assert.Empty(_listener.Events);
        Assert.Null(await _db.Query<WlItem>().FindAsync("d", Ct));
    }

    [Fact]
    public void SameListenerCannotBeAddedTwice() =>
        Assert.Throws<InvalidOperationException>(() => new EasyDbOptions().AddWriteListener(_listener).AddWriteListener(_listener));

    private sealed class RecordingListener : IWriteListener
    {
        public List<WriteEvent> Events { get; } = [];

        public ConcurrentDictionary<WriteEvent, bool> InTransactionWhenNotified { get; } = new();

        public bool Fail { get; set; }

        public ValueTask OnWrittenAsync(WriteEvent e, CancellationToken cancellationToken)
        {
            lock (Events) Events.Add(e);
            InTransactionWhenNotified[e] = e.Db.InTransaction;
            return Fail ? throw new InvalidOperationException("listener failed") : ValueTask.CompletedTask;
        }
    }
}

[Table("wl_item")]
public class WlItem
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Qty { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(WlLine.ItemId)], Cascade = Cascade.Delete)]
    public List<WlLine>? Lines { get; set; }
}

[Table("wl_line")]
public class WlLine
{
    [Column(PrimaryKey = true, Generated = true)]
    public long? Id { get; set; }

    public string? ItemId { get; set; }

    public string? Sku { get; set; }
}

[Table("wl_soft")]
public class WlSoft
{
    [Column(PrimaryKey = true)]
    public int Id { get; set; }

    public string? Name { get; set; }

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    public bool? Deleted { get; set; }
}

[Table("wl_code")]
public class WlCode
{
    [Column(PrimaryKey = true, Converter = typeof(SkuConverter))]
    public Sku? Id { get; set; }

    public string? Name { get; set; }
}

public sealed record Sku(string Value);

public sealed class SkuConverter : ValueConverter<Sku, string>
{
    protected override string ToProvider(Sku value) => value.Value;

    protected override Sku FromProvider(string value) => new(value);
}
