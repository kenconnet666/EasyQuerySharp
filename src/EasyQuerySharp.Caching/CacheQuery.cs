using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp.Caching;

/// <summary>
/// <see cref="KvCache{T}"/> 与 <see cref="AllCache{T}"/> 的公共部分：条件、拦截器开关、内存里再筛，按主键取行。
/// 不可变，每次链式调用返回新对象，可以放心复用。
/// <para>
/// 条件与拦截器开关是缓存键的一部分（条件查询的 SQL 与参数）：同一个主键在不同条件下各缓存各的，不满足条件的行当作没有。
/// <see cref="Filter"/> 在内存里对取出的行再筛，不进缓存键，也不查库。
/// </para>
/// </summary>
public abstract class CacheQuery<TSelf, T>
    where TSelf : CacheQuery<TSelf, T>
    where T : class
{
    private protected CacheQuery(CacheClient client, Query<T> condition, Func<T, bool>? filter)
    {
        Client = client;
        Condition = condition;
        RowFilter = filter;
    }

    private protected CacheClient Client { get; }

    // 条件查询：db.Query<T>() 加上 Where 与拦截器开关，不带排序
    private protected Query<T> Condition { get; }

    private protected Func<T, bool>? RowFilter { get; }

    private protected abstract TSelf With(Query<T> condition, Func<T, bool>? filter);

    /// <summary>追加条件（多个之间是 AND，对应 Java 的 where）；不满足条件的行当作没有。</summary>
    public TSelf Where(Expression<Func<T, bool>> predicate) => With(Condition.Where(predicate), RowFilter);

    /// <summary><paramref name="condition"/> 为 true 时才追加条件。</summary>
    public TSelf WhereIf(bool condition, Expression<Func<T, bool>> predicate) => condition ? Where(predicate) : (TSelf)this;

    /// <summary>应用名为 <paramref name="name"/> 的拦截器，包括默认关闭的（对应 Java 的 useInterceptor(name)）。</summary>
    public TSelf EnableInterceptor(string name) => With(Condition.EnableInterceptor(name), RowFilter);

    /// <summary>恢复默认生效的全部拦截器（对应 Java 的 useInterceptor()）。</summary>
    public TSelf EnableInterceptors() => With(Condition.EnableInterceptors(), RowFilter);

    /// <summary>不应用名为 <paramref name="name"/> 的拦截器（对应 Java 的 noInterceptor(name)）。</summary>
    public TSelf DisableInterceptor(string name) => With(Condition.DisableInterceptor(name), RowFilter);

    /// <summary>不应用任何拦截器（对应 Java 的 noInterceptor()）。</summary>
    public TSelf DisableInterceptors() => With(Condition.DisableInterceptors(), RowFilter);

    /// <summary>在内存里对取出的行再筛（对应 Java 的 filter），多次调用之间是 AND；不进缓存键、不查库。</summary>
    public TSelf Filter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var previous = RowFilter;
        return With(Condition, previous is null ? predicate : row => previous(row) && predicate(row));
    }

    /// <summary>按主键取一行（对应 Java 的 singleOrNull(id)）；没有或不满足条件时为 null。主键先转成实体的主键类型。</summary>
    public async Task<T?> FindAsync(object id, CancellationToken cancellationToken = default)
    {
        var rows = await RowsAsync([id], cancellationToken).ConfigureAwait(false);
        return rows[0];
    }

    /// <summary>按主键取一行；没有时抛出 <see cref="NotFoundException"/>，消息默认取实体配置的 <c>NotFoundMessage</c>。</summary>
    public async Task<T> FindOrThrowAsync(object id, string? message = null, CancellationToken cancellationToken = default) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundException(message ?? Client.Db.Metadata.Get<T>().NotFoundMessage ?? $"没有查到 {typeof(T).Name}");

    /// <summary>按一组主键取行（对应 Java 的 toList(ids)）：按给出的顺序返回，没有的跳过；没命中缓存的合成一条 IN 查询。</summary>
    public async Task<List<T>> FindManyAsync(IEnumerable ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var rows = await RowsAsync([.. ids.Cast<object>()], cancellationToken).ConfigureAwait(false);
        return [.. rows.OfType<T>()];
    }

    /// <summary>这个主键的行是否存在且满足条件（对应 Java 的 any(id)）。</summary>
    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is not null;

    // 按主键取行并应用 Filter；结果与 ids 一一对应，没有的位置是 null
    private protected async Task<T?[]> RowsAsync(IReadOnlyList<object> ids, CancellationToken ct)
    {
        var key = Client.Entity<T>().SingleKey;
        var rows = await Client.RowsAsync(Condition, [.. ids.Select(id => EasyCache.ToKey(key, id))], ct).ConfigureAwait(false);
        if (RowFilter is { } filter)
            for (var i = 0; i < rows.Length; i++)
                if (rows[i] is { } row && !filter(row)) rows[i] = null;
        return rows;
    }
}

/// <summary>按主键缓存单行（对应 Java 的 kvStorage），由 <see cref="CacheClient.Kv{T}"/> 得到。</summary>
/// <example><code>
/// var blog = await cache.Of(db).Kv&lt;Blog&gt;().Where(b =&gt; b.Published).FindAsync("1");
/// var blogs = await cache.Of(db).Kv&lt;Blog&gt;().FindManyAsync(new[] { "1", "2", "3" });
/// </code></example>
public sealed class KvCache<T> : CacheQuery<KvCache<T>, T> where T : class
{
    internal KvCache(CacheClient client, Query<T> condition, Func<T, bool>? filter) : base(client, condition, filter)
    {
    }

    private protected override KvCache<T> With(Query<T> condition, Func<T, bool>? filter) => new(Client, condition, filter);
}
