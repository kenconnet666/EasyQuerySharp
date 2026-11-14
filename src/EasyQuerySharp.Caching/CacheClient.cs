using System.Linq.Expressions;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp.Caching;

/// <summary>
/// 绑定了一个库的缓存入口（<see cref="EasyCache.Of"/>）：<see cref="Kv{T}"/> 按主键取，<see cref="All{T}"/> 按条件取主键索引再按主键取。
/// 缓存没命中时用 <c>db.Query&lt;T&gt;()</c> 查库，拦截器、逻辑删除、租户过滤照常生效（它们加的条件是缓存键的一部分）。
/// 事务里的读绕过缓存（可能读到自己未提交的写入）。读出的实体不被追踪。
/// </summary>
public sealed class CacheClient
{
    // 一条 IN 查询最多带的主键数（与批量写入的分组一致）
    private const int MaxIdsPerQuery = 1000;

    private readonly EasyCache _cache;

    internal CacheClient(EasyCache cache, EasyDb db)
    {
        _cache = cache;
        Db = db;
    }

    /// <summary>绑定的库。</summary>
    public EasyDb Db { get; }

    /// <summary>按主键缓存单行（对应 Java 的 kvStorage）。<typeparamref name="T"/> 要登记过、有单列主键。</summary>
    public KvCache<T> Kv<T>() where T : class => new(this, Condition<T>(), null);

    /// <summary>按条件缓存主键索引，再按主键取行（对应 Java 的 allStorage），适合小表（配置、字典、省市）。</summary>
    public AllCache<T> All<T>() where T : class => new(this, Condition<T>(), null, null);

    /// <summary>
    /// 手动失效（绕过本库的写入、别的服务改了库）：给了 <paramref name="id"/> 时删这一行的缓存与索引，否则整表失效。
    /// </summary>
    public Task InvalidateAsync<T>(object? id = null, CancellationToken cancellationToken = default) where T : class
    {
        var entity = Entity<T>();
        var table = EasyCache.TableId(entity);
        return id is null
            ? _cache.Store.InvalidateAsync(table, WriteKind.Update, null, cancellationToken)
            : _cache.Store.InvalidateAsync(table, WriteKind.Update, [EasyCache.KeyText(entity.SingleKey, EasyCache.ToKey(entity.SingleKey, id))], cancellationToken);
    }

    internal EntityMetadata Entity<T>() where T : class
    {
        if (!_cache.IsCached(typeof(T)))
            throw new InvalidOperationException($"{typeof(T).Name} 没有登记缓存：new EasyCache(store, o => o.Cache<{typeof(T).Name}>())");
        var entity = Db.Metadata.Get<T>();
        if (entity.KeyColumns.Count != 1)
            throw new NotSupportedException($"{typeof(T).Name} 不是单列主键：二级缓存只支持单列主键");
        return entity;
    }

    private Query<T> Condition<T>() where T : class
    {
        Entity<T>();
        return Db.Query<T>().AsNoTracking();
    }

    /// <summary>
    /// 按主键取行（0007 §6 的行路径），结果与 <paramref name="ids"/> 一一对应，查无此行的位置是 null。
    /// 逐个探测行桶，没命中的合成 <c>WHERE 条件 AND 主键 IN (…)</c> 查库，查到的与查无此行的分别写回。
    /// </summary>
    internal async Task<T?[]> RowsAsync<T>(Query<T> condition, IReadOnlyList<object> ids, CancellationToken ct) where T : class
    {
        var entity = Entity<T>();
        var key = entity.SingleKey;
        var texts = ids.Select(id => EasyCache.KeyText(key, id)).ToList();
        var result = new T?[ids.Count];
        if (Db.InTransaction)
        {
            var rows = await LoadAsync(condition, ids, key, ct).ConfigureAwait(false);
            for (var i = 0; i < ids.Count; i++) result[i] = rows.GetValueOrDefault(texts[i]);
            return result;
        }

        var state = _cache.State(Db);
        var table = EasyCache.TableId(entity);
        var epoch = await _cache.Store.EpochAsync(table, ct).ConfigureAwait(false);
        var variant = EasyCache.Variant(typeof(T), condition.ToStatement());
        var missing = new List<int>();
        for (var i = 0; i < ids.Count; i++)
        {
            var item = await _cache.Store.GetRowAsync(table, epoch, texts[i], variant, ct).ConfigureAwait(false);
            if (item is null) missing.Add(i);
            else if (item.Json is { } json) result[i] = state.Deserialize<T>(json);
        }
        if (missing.Count == 0) return result;

        var unique = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var i in missing) unique.TryAdd(texts[i], ids[i]);
        var loaded = await LoadAsync(condition, [.. unique.Values], key, ct).ConfigureAwait(false);
        var now = CacheStore.Now();
        foreach (var text in unique.Keys)
        {
            var row = loaded.GetValueOrDefault(text);
            var item = row is null
                ? new CachedItem(null, now + (long)_cache.Store.NullExpiration.TotalMilliseconds)
                : new CachedItem(state.Serialize(row), now + (long)_cache.Store.Expiration.TotalMilliseconds);
            await _cache.Store.SetRowAsync(table, epoch, text, variant, item, ct).ConfigureAwait(false);
        }
        foreach (var i in missing) result[i] = loaded.GetValueOrDefault(texts[i]);
        return result;
    }

    /// <summary>条件（含排序）下的主键，按顺序（0007 §6 的索引路径）：索引桶没有时只查主键列。</summary>
    internal async Task<List<object>> KeysAsync<T>(Query<T> ordered, CancellationToken ct) where T : class
    {
        var entity = Entity<T>();
        var key = entity.SingleKey;
        if (Db.InTransaction) return await SelectKeysAsync(ordered, key, ct).ConfigureAwait(false);

        var table = EasyCache.TableId(entity);
        var epoch = await _cache.Store.EpochAsync(table, ct).ConfigureAwait(false);
        var variant = EasyCache.Variant(typeof(T), ordered.ToStatement());
        if (await _cache.Store.GetIndexAsync(table, epoch, variant, ct).ConfigureAwait(false) is { } index)
            return [.. index.Keys.Select(t => EasyCache.KeyOf(key, t))];

        var keys = await SelectKeysAsync(ordered, key, ct).ConfigureAwait(false);
        var expiresAt = CacheStore.Now() + (long)_cache.Store.Expiration.TotalMilliseconds;
        await _cache.Store.SetIndexAsync(table, epoch, variant, new CachedIndex([.. keys.Select(k => EasyCache.KeyText(key, k))], expiresAt), ct).ConfigureAwait(false);
        return keys;
    }

    // 查库：WHERE 条件 AND 主键 IN (…)，每批最多 1000 个；按主键文本索引
    private static async Task<Dictionary<string, T>> LoadAsync<T>(Query<T> condition, IReadOnlyList<object> ids, ColumnMetadata key, CancellationToken ct) where T : class
    {
        var rows = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var chunk in ids.Chunk(MaxIdsPerQuery))
            foreach (var row in await condition.WhereByIds(chunk).ToListAsync(ct).ConfigureAwait(false))
                rows[EasyCache.KeyText(key, key.Getter(row)!)] = row;
        return rows;
    }

    // 只取主键列：SELECT t.`id` FROM …（与 Java 的索引查询一致）
    private static async Task<List<object>> SelectKeysAsync<T>(Query<T> ordered, ColumnMetadata key, CancellationToken ct) where T : class
    {
        var row = Expression.Parameter(typeof(T), "t");
        var selector = Expression.Lambda<Func<T, object?>>(Expression.Convert(Expression.Property(row, key.Property), typeof(object)), row);
        var keys = await ordered.Select(selector).ToListAsync(ct).ConfigureAwait(false);
        return [.. keys.Select(k => EasyCache.ToKey(key, k))];
    }
}
