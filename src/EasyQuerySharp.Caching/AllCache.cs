using System.Linq.Expressions;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp.Caching;

/// <summary>
/// 按条件缓存主键索引、再按主键取行（对应 Java 的 allStorage），由 <see cref="CacheClient.All{T}"/> 得到，适合小表（配置、字典、省市）。
/// 索引是条件（含排序）下的主键列表；行与 <see cref="KvCache{T}"/> 在同一条件下共用。没有排序时按数据库返回的顺序。
/// </summary>
/// <example><code>
/// var page = await cache.Of(db).All&lt;Province&gt;().Where(p =&gt; p.Name.StartsWith("浙")).OrderBy(p =&gt; p.Code).ToPageAsync(1, 20);
/// </code></example>
public sealed class AllCache<T> : CacheQuery<AllCache<T>, T> where T : class
{
    private readonly Func<Query<T>, Query<T>>? _order;

    internal AllCache(CacheClient client, Query<T> condition, Func<T, bool>? filter, Func<Query<T>, Query<T>>? order) : base(client, condition, filter) =>
        _order = order;

    private protected override AllCache<T> With(Query<T> condition, Func<T, bool>? filter) => new(Client, condition, filter, _order);

    /// <summary>按 <paramref name="key"/> 升序排（排序进索引的缓存键；与 <see cref="ThenBy{TKey}"/> 相同，依次追加）。</summary>
    public AllCache<T> OrderBy<TKey>(Expression<Func<T, TKey>> key) => Order(q => q.OrderBy(key));

    /// <summary>按 <paramref name="key"/> 降序排。</summary>
    public AllCache<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key) => Order(q => q.OrderByDescending(key));

    /// <summary>再按 <paramref name="key"/> 升序排。</summary>
    public AllCache<T> ThenBy<TKey>(Expression<Func<T, TKey>> key) => Order(q => q.ThenBy(key));

    /// <summary>再按 <paramref name="key"/> 降序排。</summary>
    public AllCache<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key) => Order(q => q.ThenByDescending(key));

    /// <summary>条件下的全部行，按索引的顺序（对应 Java 的 toList()）。</summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await RowsAsync(await ToIdsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return [.. rows.OfType<T>()];
    }

    /// <summary>条件下的主键（实体的主键类型），按索引的顺序；不应用 <see cref="CacheQuery{TSelf,T}.Filter"/>。</summary>
    public Task<List<object>> ToIdsAsync(CancellationToken cancellationToken = default) =>
        Client.KeysAsync(_order is null ? Condition : _order(Condition), cancellationToken);

    /// <summary>条件下的行数；有 Filter 时取出全部行再数。</summary>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default) =>
        RowFilter is null
            ? (await ToIdsAsync(cancellationToken).ConfigureAwait(false)).Count
            : (await ToListAsync(cancellationToken).ConfigureAwait(false)).Count;

    /// <summary>条件下是否有行。</summary>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) => await CountAsync(cancellationToken).ConfigureAwait(false) > 0;

    /// <summary>
    /// 在缓存的索引上分页（对应 Java 的 toPageResult，<paramref name="page"/> 从 1 开始）。没有 Filter 时先对索引分页、只取这一页的行
    /// （Java 先取全部行）；有 Filter 时取出全部行筛过再分页。
    /// </summary>
    public async Task<Page<T>> ToPageAsync(long page, long size, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var skip = (page - 1) * size;
        if (RowFilter is not null)
        {
            var all = await ToListAsync(cancellationToken).ConfigureAwait(false);
            return new Page<T>([.. all.Skip((int)Math.Min(skip, all.Count)).Take((int)Math.Min(size, int.MaxValue))], all.Count, page, size);
        }
        var ids = await ToIdsAsync(cancellationToken).ConfigureAwait(false);
        var pageIds = ids.Skip((int)Math.Min(skip, ids.Count)).Take((int)Math.Min(size, int.MaxValue)).ToList();
        var rows = pageIds.Count == 0 ? [] : await RowsAsync(pageIds, cancellationToken).ConfigureAwait(false);
        return new Page<T>([.. rows.OfType<T>()], ids.Count, page, size);
    }

    private AllCache<T> Order(Func<Query<T>, Query<T>> order)
    {
        var previous = _order;
        return new AllCache<T>(Client, Condition, RowFilter, previous is null ? order : q => order(previous(q)));
    }
}
