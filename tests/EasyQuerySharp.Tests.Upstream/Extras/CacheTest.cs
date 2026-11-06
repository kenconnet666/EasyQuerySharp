using System.Linq.Expressions;
using System.Text;
using EasyQuerySharp.Caching;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 二级缓存（EasyQuerySharp.Caching，0007）的 C# 独有用例：kv 与 all 的读路径、各种写入的失效、事务、同表两个实体、
/// 两个实例经共享的 L2、查无此行的缓存、整表纪元。每个用例一个 SQLite 文件库（WAL：事务写入时别的连接仍能读）；
/// 每个 <see cref="App"/> 是一个应用实例（自己的 HybridCache 本机层），传入同一个 <see cref="SharedDistributedCache"/> 时共享 L2。
/// </summary>
public sealed class CacheTest : IAsyncLifetime
{
    private const string TopicSql = "SELECT \"id\",\"stars\",\"title\" FROM \"ct_topic\" WHERE \"id\" IN ";

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"eqs-cache-{Guid.NewGuid():N}.db");
    private readonly string _cs;

    public CacheTest() => _cs = $"Data Source={_file};Pooling=False";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var db = new EasyDb(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)));
        await db.ExecuteSqlAsync("PRAGMA journal_mode=WAL", cancellationToken: Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(CtTopic), typeof(CtPost), typeof(CtCode)], Ct);
        await db.Insert(Enumerable.Range(1, 5).Select(i => new CtTopic { Id = i.ToString(), Stars = i, Title = "t" + i })).ExecuteAsync(Ct);
        await db.Insert(new CtPost { Id = "p1", TopicId = "1", Body = "a", Deleted = false }, new CtPost { Id = "p2", TopicId = "1", Body = "b", Deleted = false },
            new CtPost { Id = "p3", TopicId = "2", Body = "c", Deleted = true }).ExecuteAsync(Ct);
        await db.Insert(new CtCode { Id = new Sku("a"), Name = "A" }, new CtCode { Id = new Sku("b"), Name = "B" }).ExecuteAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var file in new[] { _file, _file + "-wal", _file + "-shm" })
            if (File.Exists(file)) File.Delete(file);
        return ValueTask.CompletedTask;
    }

    /// <summary>一个应用实例：自己的 HybridCache（本机层）、EasyCache 与接上它的 EasyDb。</summary>
    private sealed record App(EasyDb Db, EasyCache Cache)
    {
        public CacheClient Client => Cache.Of(Db);
    }

    private App NewApp(SharedDistributedCache? l2 = null, Action<EasyCacheOptions>? configure = null)
    {
        var cache = new EasyCache(CacheSupport.NewHybridCache(l2), o =>
        {
            o.Cache<CtTopic>().Cache<CtTopicLong>().Cache<CtPost>().Cache<CtCode>();
            configure?.Invoke(o);
        });
        return new App(NewDb(cache), cache);
    }

    private EasyDb NewDb(EasyCache cache) =>
        new(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)).AddListener(SqlCapture.Instance).AddInterceptor(new CtStarsFilter()).UseCache(cache));

    // 分片实体不能进二级缓存：分片写入的事件里是各分片的实际表名（ct_topic_0…），按逻辑表登记的失效对不上
    [Fact]
    public void RefusesShardedEntities()
    {
        var cache = new EasyCache(CacheSupport.NewHybridCache(null), o => o.Cache<CtTopic>());
        var db = new EasyDb(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)).Shard<CtTopic>(r => r.Mod(t => t.Id, 2)).UseCache(cache));
        Assert.Contains("分片实体", Assert.Throws<NotSupportedException>(() => cache.Of(db)).Message, StringComparison.Ordinal);
    }
    private static async Task<T> NoSqlAsync<T>(Func<Task<T>> read)
    {
        using var listen = SqlCapture.Listen();
        var result = await read();
        Assert.Empty(listen.Events);
        return result;
    }

    private static async Task<T> OneSqlAsync<T>(Func<Task<T>> read, string? sql = null, string? parameters = null)
    {
        using var listen = SqlCapture.Listen();
        var result = await read();
        Assert.Single(listen.Events);
        if (sql is not null) listen.AssertSql(sql);
        if (parameters is not null) listen.AssertParams(parameters);
        return result;
    }

    // ---------- kv 的读路径 ----------

    [Fact]
    public async Task KvCachesTheRowAndReturnsCopies()
    {
        var app = NewApp();
        var first = await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct), TopicSql + "(?)", "1(String)");
        var second = await NoSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct));
        Assert.Equal(("1", 1, "t1"), (second!.Id, second.Stars, second.Title));
        Assert.NotSame(first, second);
        Assert.NotSame(second, await app.Client.Kv<CtTopic>().FindAsync("1", Ct));
    }

    // 查无此行也缓存（防穿透），NullExpiration 之后再查库
    [Fact]
    public async Task MissingRowsAreCachedForTheNullExpiration()
    {
        var app = NewApp(configure: o => o.NullExpiration = TimeSpan.FromMilliseconds(300));
        Assert.Null(await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("404", Ct)));
        Assert.Null(await NoSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("404", Ct)));
        await Task.Delay(400, Ct);
        Assert.Null(await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("404", Ct)));
    }

    // 条件与拦截器开关是缓存键的一部分：同一主键在不同条件下各缓存各的；Filter 在内存里筛，不查库
    [Fact]
    public async Task ConditionsAreSeparateVariantsAndFilterRunsInMemory()
    {
        var app = NewApp();
        Assert.Null(await OneSqlAsync(() => app.Client.Kv<CtTopic>().Where(t => t.Stars > 3).FindAsync("1", Ct),
            "SELECT \"id\",\"stars\",\"title\" FROM \"ct_topic\" WHERE \"stars\" > ? AND \"id\" IN (?)", "3(Integer),1(String)"));
        Assert.Null(await OneSqlAsync(() => app.Client.Kv<CtTopic>().EnableInterceptor(nameof(CtStarsFilter)).FindAsync("1", Ct),
            "SELECT \"id\",\"stars\",\"title\" FROM \"ct_topic\" WHERE \"stars\" >= ? AND \"id\" IN (?)", "3(Integer),1(String)"));
        Assert.NotNull(await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct)));

        Assert.Null(await NoSqlAsync(() => app.Client.Kv<CtTopic>().Where(t => t.Stars > 3).FindAsync("1", Ct)));
        Assert.Null(await NoSqlAsync(() => app.Client.Kv<CtTopic>().EnableInterceptor(nameof(CtStarsFilter)).FindAsync("1", Ct)));
        Assert.NotNull(await NoSqlAsync(() => app.Client.Kv<CtTopic>().WhereIf(false, t => t.Stars > 3).FindAsync("1", Ct)));
        Assert.Null(await NoSqlAsync(() => app.Client.Kv<CtTopic>().Filter(t => t.Stars > 3).FindAsync("1", Ct)));
        Assert.False(await NoSqlAsync(() => app.Client.Kv<CtTopic>().Filter(t => t.Stars > 0).Filter(t => t.Title != "t1").ExistsAsync("1", Ct)));
    }

    // 逻辑删除照常生效：删除了的行当作没有
    [Fact]
    public async Task LogicDeletedRowsAreMissing()
    {
        var app = NewApp();
        Assert.Null(await OneSqlAsync(() => app.Client.Kv<CtPost>().FindAsync("p3", Ct),
            "SELECT \"id\",\"topic_id\",\"body\",\"deleted\" FROM \"ct_post\" WHERE \"deleted\" = ? AND \"id\" IN (?)", "false(Boolean),p3(String)"));
        Assert.NotNull(await app.Client.Kv<CtPost>().FindAsync("p1", Ct));
    }

    // 一组主键：没命中的合成一条 IN 查询（主键先转成实体的主键类型），按给出的顺序返回，没有的跳过
    [Fact]
    public async Task FindManyQueriesOnlyTheMisses()
    {
        var app = NewApp();
        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        var rows = await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindManyAsync(new object[] { 3, "1", "2", "404", "2" }, Ct),
            TopicSql + "(?,?,?)", "3(String),2(String),404(String)");
        Assert.Equal(["3", "1", "2", "2"], rows.Select(r => r.Id));
        Assert.Equal(3, (await NoSqlAsync(() => app.Client.Kv<CtTopic>().FindManyAsync(new[] { "1", "2", "3", "404" }, Ct))).Count);
    }

    [Fact]
    public async Task FindOrThrowAndExists()
    {
        var app = NewApp();
        Assert.Equal("t2", (await app.Client.Kv<CtTopic>().FindOrThrowAsync("2", cancellationToken: Ct)).Title);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => app.Client.Kv<CtTopic>().FindOrThrowAsync("404", cancellationToken: Ct));
        Assert.Equal("没有查到 CtTopic", ex.Message);
        await Assert.ThrowsAsync<NotFoundException>(() => app.Client.All<CtTopic>().FindOrThrowAsync("404", "no topic", Ct));
        Assert.True(await app.Client.Kv<CtTopic>().ExistsAsync("2", Ct));
        Assert.False(await app.Client.Kv<CtTopic>().ExistsAsync("404", Ct));
    }

    // 读出的实体不进追踪作用域
    [Fact]
    public async Task CachedReadsAreNotTracked()
    {
        var app = NewApp();
        using var tracking = app.Db.BeginTracking();
        var loaded = await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        var cached = await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        Assert.False(app.Db.RemoveTracking(loaded!));
        Assert.False(app.Db.RemoveTracking(cached!));
    }

    // ---------- 失效 ----------

    [Fact]
    public async Task UpdateByEntityEvictsThatRow()
    {
        var app = NewApp();
        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        await app.Client.Kv<CtTopic>().FindAsync("2", Ct);
        await app.Db.Update(new CtTopic { Id = "1", Stars = 1, Title = "changed" }).ExecuteAsync(Ct);
        Assert.Equal("changed", (await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
        Assert.Equal("t2", (await NoSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("2", Ct)))!.Title);
    }

    // 条件里没有主键：整表换纪元，所有行重新查
    [Fact]
    public async Task UpdateWithUnknownKeysEvictsTheWholeTable()
    {
        var app = NewApp();
        await app.Client.Kv<CtTopic>().FindManyAsync(new[] { "1", "2" }, Ct);
        await app.Db.Update<CtTopic>().Set(t => t.Stars, 0).Where(t => t.Title!.StartsWith("t")).ExecuteAsync(Ct);
        var rows = await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindManyAsync(new[] { "1", "2" }, Ct));
        Assert.All(rows, r => Assert.Equal(0, r.Stars));
    }

    [Fact]
    public async Task DeleteEvictsTheRowAndTheIndex()
    {
        var app = NewApp();
        Assert.Equal(2, (await app.Client.All<CtPost>().ToListAsync(Ct)).Count);
        await app.Db.Delete<CtPost>().WhereById("p1").ExecuteAsync(Ct);
        Assert.Null(await OneSqlAsync(() => app.Client.Kv<CtPost>().FindAsync("p1", Ct)));
        Assert.Equal(["p2"], (await app.Client.All<CtPost>().ToListAsync(Ct)).Select(p => p.Id));
    }

    // 插入：主键已知时删行桶（之前缓存的"查无此行"）与索引；主键不明（按字典插入）时只删索引，已缓存的行还在
    [Fact]
    public async Task InsertsEvictTheIndex()
    {
        var app = NewApp();
        Assert.Null(await app.Client.Kv<CtTopic>().FindAsync("6", Ct));
        Assert.Equal(5, await app.Client.All<CtTopic>().CountAsync(Ct));
        await app.Db.Insert(new CtTopic { Id = "6", Stars = 6, Title = "t6" }).ExecuteAsync(Ct);
        Assert.NotNull(await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("6", Ct)));
        Assert.Equal(6, await app.Client.All<CtTopic>().CountAsync(Ct));

        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        await app.Db.InsertMap("ct_topic", new Dictionary<string, object?> { ["id"] = "7", ["stars"] = 7, ["title"] = "t7" }).ExecuteAsync(Ct);
        Assert.NotNull(await NoSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct)));
        Assert.Equal(7, await OneSqlAsync(() => app.Client.All<CtTopic>().CountAsync(Ct)));
    }

    // 同表的两个实体共用行桶：经一个实体的写入让另一个也失效（主键格式一致："1" 与 1L）
    [Fact]
    public async Task EntitiesOfTheSameTableShareInvalidation()
    {
        var app = NewApp();
        Assert.Equal("t1", (await app.Client.Kv<CtTopicLong>().FindAsync("1", Ct))!.Title);
        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        await app.Db.Update<CtTopic>().Set(t => t.Title, "x").WhereById("1").ExecuteAsync(Ct);
        Assert.Equal("x", (await OneSqlAsync(() => app.Client.Kv<CtTopicLong>().FindAsync(1L, Ct)))!.Title);
        Assert.Equal(5, (await app.Client.All<CtTopicLong>().ToIdsAsync(Ct)).Count);
        Assert.All(await app.Client.All<CtTopicLong>().ToIdsAsync(Ct), id => Assert.IsType<long>(id));
    }

    // 主键带转换器：缓存键用数据库值，索引还原成属性类型
    [Fact]
    public async Task ConvertedKeys()
    {
        var app = NewApp();
        Assert.Equal("A", (await app.Client.Kv<CtCode>().FindAsync(new Sku("a"), Ct))!.Name);
        Assert.Equal<object>([new Sku("a"), new Sku("b")], await app.Client.All<CtCode>().OrderBy(c => c.Name).ToIdsAsync(Ct));
        await app.Db.Update<CtCode>().Set(c => c.Name, "A2").WhereById(new Sku("a")).ExecuteAsync(Ct);
        Assert.Equal("A2", (await OneSqlAsync(() => app.Client.Kv<CtCode>().FindAsync(new Sku("a"), Ct)))!.Name);
    }

    // 手动失效：绕过本库的写入（原生 SQL）不通知，用 InvalidateAsync 补上
    [Fact]
    public async Task ManualInvalidation()
    {
        var app = NewApp();
        await app.Client.Kv<CtTopic>().FindManyAsync(new[] { "1", "2" }, Ct);
        await app.Db.ExecuteSqlAsync("UPDATE ct_topic SET title = 'raw'", cancellationToken: Ct);
        Assert.Equal("t1", (await app.Client.Kv<CtTopic>().FindAsync("1", Ct))!.Title);
        await app.Client.InvalidateAsync<CtTopic>("1", Ct);
        Assert.Equal("raw", (await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
        Assert.Equal("t2", (await app.Client.Kv<CtTopic>().FindAsync("2", Ct))!.Title);
        await app.Client.InvalidateAsync<CtTopic>(cancellationToken: Ct);
        Assert.Equal("raw", (await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("2", Ct)))!.Title);
    }

    // 延迟双删：隔一会儿再失效一次，期间绕过本库的改动在那之后可见
    [Fact]
    public async Task LateEvictionRunsAgain()
    {
        var app = NewApp(configure: o => o.LateEvictDelay = TimeSpan.FromMilliseconds(200));
        await app.Db.Update<CtTopic>().Set(t => t.Title, "new").WhereById("1").ExecuteAsync(Ct);
        Assert.Equal("new", (await app.Client.Kv<CtTopic>().FindAsync("1", Ct))!.Title);
        await app.Db.ExecuteSqlAsync("UPDATE ct_topic SET title = 'late' WHERE id = '1'", cancellationToken: Ct);
        Assert.Equal("new", (await app.Client.Kv<CtTopic>().FindAsync("1", Ct))!.Title);
        await Task.Delay(500, Ct);
        Assert.Equal("late", (await app.Client.Kv<CtTopic>().FindAsync("1", Ct))!.Title);
    }

    // ---------- 事务 ----------

    // 事务里的读绕过缓存；事务里的写入在提交后才失效（之前别的流程读到的仍是缓存里已提交的值），回滚不失效
    [Fact]
    public async Task TransactionsBypassReadsAndEvictAfterCommit()
    {
        var app = NewApp();
        var other = NewDb(app.Cache);
        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);

        await using (var tx = app.Db.BeginTransaction())
        {
            Assert.NotNull(await OneSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct)));
            Assert.Equal(5, await OneSqlAsync(() => app.Client.All<CtTopic>().CountAsync(Ct)));
            await app.Db.Update<CtTopic>().Set(t => t.Title, "tx").WhereById("1").ExecuteAsync(Ct);
            Assert.Equal("tx", (await app.Client.Kv<CtTopic>().FindAsync("1", Ct))!.Title);
            Assert.Equal("t1", (await NoSqlAsync(() => app.Cache.Of(other).Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal("tx", (await OneSqlAsync(() => app.Cache.Of(other).Kv<CtTopic>().FindAsync("1", Ct)))!.Title);

        await using (app.Db.BeginTransaction())
            await app.Db.Update<CtTopic>().Set(t => t.Title, "rolled back").WhereById("1").ExecuteAsync(Ct);
        Assert.Equal("tx", (await NoSqlAsync(() => app.Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
    }

    // ---------- 两个实例 ----------

    // 共享 L2：一个实例写回的行另一个实例直接读到；写入删掉自己的本机层与 L2，别的实例的本机层最多再旧 LocalCacheExpiration
    [Fact]
    public async Task TwoInstancesShareTheDistributedLayer()
    {
        var l2 = new SharedDistributedCache();
        void Short(EasyCacheOptions o) => o.LocalCacheExpiration = TimeSpan.FromSeconds(1);
        var a = NewApp(l2, Short);
        var b = NewApp(l2, Short);
        await OneSqlAsync(() => a.Client.Kv<CtTopic>().FindAsync("1", Ct));
        Assert.Equal("t1", (await NoSqlAsync(() => b.Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);

        await a.Db.Update<CtTopic>().Set(t => t.Title, "a").WhereById("1").ExecuteAsync(Ct);
        Assert.Equal("a", (await OneSqlAsync(() => a.Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
        Assert.Equal("a", (await NoSqlAsync(() => NewApp(l2).Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
        Assert.Equal("t1", (await NoSqlAsync(() => b.Client.Kv<CtTopic>().FindAsync("1", Ct)))!.Title);
        await Task.Delay(1200, Ct);
        Assert.Equal("a", (await b.Client.Kv<CtTopic>().FindAsync("1", Ct))!.Title);
    }

    // 整表失效换的纪元经 L2 传到别的实例（本机层过期之后）
    [Fact]
    public async Task EpochChangesReachOtherInstances()
    {
        var l2 = new SharedDistributedCache();
        void Short(EasyCacheOptions o) => o.LocalCacheExpiration = TimeSpan.FromSeconds(1);
        var a = NewApp(l2, Short);
        var b = NewApp(l2, Short);
        await b.Client.Kv<CtTopic>().FindAsync("2", Ct);
        await a.Db.Update<CtTopic>().Set(t => t.Stars, 0).Where(t => t.Title == "t2").ExecuteAsync(Ct);
        await Task.Delay(1200, Ct);
        Assert.Equal(0, (await OneSqlAsync(() => b.Client.Kv<CtTopic>().FindAsync("2", Ct)))!.Stars);
    }

    // 只缓存实体自己的列：导航属性、不映射的属性不进缓存
    [Fact]
    public async Task OnlyColumnsAreSerialized()
    {
        var l2 = new SharedDistributedCache();
        var app = NewApp(l2);
        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        var payloads = l2.Keys.Select(k => Encoding.UTF8.GetString(l2.Get(k) ?? [])).Where(p => p.Contains("t1", StringComparison.Ordinal)).ToList();
        Assert.Single(payloads);
        Assert.Contains("Title", payloads[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Posts", payloads[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Alias", payloads[0], StringComparison.Ordinal);
    }

    // ---------- all ----------

    // 索引查询只取主键列，行再按主键取（与 kv 在同一条件下共用行）；第二次不查库
    [Fact]
    public async Task AllLoadsTheIndexThenTheRows()
    {
        var app = NewApp();
        await app.Client.Kv<CtTopic>().FindAsync("1", Ct);
        using (var listen = SqlCapture.Listen())
        {
            var list = await app.Client.All<CtTopic>().ToListAsync(Ct);
            Assert.Equal(5, list.Count);
            Assert.Equal(2, listen.Events.Count);
            listen.AssertSql(0, "SELECT t.\"id\" FROM \"ct_topic\" t");
            listen.AssertSql(1, TopicSql + "(?,?,?,?)");
        }
        Assert.Equal(5, (await NoSqlAsync(() => app.Client.All<CtTopic>().ToListAsync(Ct))).Count);
        Assert.Equal(["2", "4"], (await NoSqlAsync(() => app.Client.All<CtTopic>().Filter(t => t.Stars % 2 == 0).ToListAsync(Ct))).Select(t => t.Id));
    }

    [Fact]
    public async Task AllOrdersAndConditionsAreSeparateIndexes()
    {
        var app = NewApp();
        Assert.Equal(["5", "4", "3"], (await app.Client.All<CtTopic>().Where(t => t.Stars >= 3).OrderByDescending(t => t.Stars).ToListAsync(Ct)).Select(t => t.Id));
        Assert.Equal<object>(["3", "4", "5"], await app.Client.All<CtTopic>().Where(t => t.Stars >= 3).OrderBy(t => t.Title).ThenByDescending(t => t.Id).ToIdsAsync(Ct));
        Assert.True(await app.Client.All<CtTopic>().AnyAsync(Ct));
        Assert.False(await app.Client.All<CtTopic>().Where(t => t.Stars > 100).AnyAsync(Ct));
        Assert.Equal(2, await app.Client.All<CtTopic>().Filter(t => t.Stars <= 2).CountAsync(Ct));
    }

    // 没有 Filter 时先对索引分页、只取这一页的行；有 Filter 时筛过全部行再分页
    [Fact]
    public async Task AllPagesOnTheIndex()
    {
        var app = NewApp();
        await app.Client.All<CtTopic>().OrderBy(t => t.Stars).ToIdsAsync(Ct);
        var page = await OneSqlAsync(() => app.Client.All<CtTopic>().OrderBy(t => t.Stars).ToPageAsync(2, 2, Ct), TopicSql + "(?,?)", "3(String),4(String)");
        Assert.Equal((5L, 3L), (page.Total, page.PageCount));
        Assert.Equal(["3", "4"], page.Rows.Select(r => r.Id));
        var filtered = await app.Client.All<CtTopic>().OrderBy(t => t.Stars).Filter(t => t.Stars != 1).ToPageAsync(2, 3, Ct);
        Assert.Equal(4L, filtered.Total);
        Assert.Equal(["5"], filtered.Rows.Select(r => r.Id));
        Assert.Empty((await app.Client.All<CtTopic>().ToPageAsync(9, 2, Ct)).Rows);
    }

    // ---------- 配置 ----------

    [Fact]
    public void MisconfigurationsAreReported()
    {
        var cache = new EasyCache(CacheSupport.NewHybridCache(), o => o.Cache<CtTopic>());
        var plain = new EasyDb(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)));
        Assert.Contains("UseCache", Assert.Throws<InvalidOperationException>(() => cache.Of(plain)).Message);
        var client = cache.Of(new EasyDb(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)).UseCache(cache)));
        Assert.Contains("没有登记", Assert.Throws<InvalidOperationException>(() => client.Kv<CtPost>()).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EasyCache(CacheSupport.NewHybridCache(), o => o.Expiration = TimeSpan.Zero));
    }
}

[Table("ct_topic")]
public class CtTopic
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    [ColumnIgnore]
    public string? Alias { get; set; } = "not cached";

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(CtPost.TopicId)])]
    public List<CtPost>? Posts { get; set; }
}

/// <summary>同表 ct_topic 的另一个实体，主键是 long。</summary>
[Table("ct_topic")]
public class CtTopicLong
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }
}

[Table("ct_post")]
public class CtPost
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? TopicId { get; set; }

    public string? Body { get; set; }

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    public bool? Deleted { get; set; }
}

[Table("ct_code")]
public class CtCode
{
    [Column(PrimaryKey = true, Converter = typeof(SkuConverter))]
    public Sku? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>默认关闭的条件拦截器：只看星数不少于 3 的主题。</summary>
public sealed class CtStarsFilter : QueryFilter<CtTopic>
{
    public override bool EnabledByDefault => false;

    public override Expression<Func<CtTopic, bool>>? Filter() => t => t.Stars >= 3;
}
