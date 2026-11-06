using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Sharding;

/// <summary>
/// 内置分片规则的写法（<see cref="EasyDbOptions.Shard{T}(Action{ShardRuleBuilder{T}})"/> 的参数）：<see cref="Mod"/>、<see cref="ByYear"/> /
/// <see cref="ByMonth"/> / <see cref="ByDay"/> 选一种，再按需加 <see cref="ExtraKey{TKey}"/> 与 <see cref="DefaultOrder{TKey}"/>。
/// 表名的前缀是实体的表名，节点的数据源不写时是默认数据源。
/// </summary>
/// <example><code>
/// options.Shard&lt;Order&gt;(r =&gt; r.Mod(o =&gt; o.UserId, 4));                                   // t_order_0 … t_order_3
/// options.Shard&lt;Log&gt;(r =&gt; r.ByMonth(l =&gt; l.CreateTime, from: new(2024, 1, 1))           // t_log_202401 … 本月
///     .DefaultOrder(l =&gt; l.CreateTime, descending: true));
/// options.Shard&lt;Bill&gt;(r =&gt; r.ByYear(b =&gt; b.Time, from: new(2022, 1, 1), dataSource: d =&gt; $"ds{d.Year}", splitTable: false)); // 按年分库
/// </code></example>
public sealed class ShardRuleBuilder<T> where T : class
{
    private Func<EntityMetadata, string, KeyedShardRule>? _primary;
    private readonly List<(string Property, Func<object, object?> ToShardKey)> _extraKeys = [];
    private ShardOrder? _defaultOrder;

    internal ShardRuleBuilder()
    {
    }

    /// <summary>
    /// 取模分表：节点是 <c>表名{separator}0 … 表名{separator}{count-1}</c>，尾巴左补 0 到 <paramref name="tailLength"/> 位；
    /// 一行落在键的字符串按 Java <c>String.hashCode</c> 取模的那张表（与 Java 的 AbstractModTableRoute 一致）。只有 <c>=</c>、<c>IN</c> 能裁剪。
    /// </summary>
    /// <param name="key">分片键属性，如 <c>t =&gt; t.Id</c>。</param>
    /// <param name="count">表的个数。</param>
    /// <param name="tailLength">尾巴的位数。</param>
    /// <param name="separator">表名与尾巴之间的连接符。</param>
    /// <param name="dataSource">全部节点所在的数据源；为 null 时是默认数据源。</param>
    public ShardRuleBuilder<T> Mod(Expression<Func<T, object?>> key, int count, int tailLength = 1, string separator = "_", string? dataSource = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(tailLength, 1);
        ArgumentNullException.ThrowIfNull(separator);
        var property = ExpressionTranslator.MemberPath(key);
        return Primary((entity, defaultDataSource) =>
            new ModShardRule(typeof(T).Name, entity.TableName, property, Getter(entity, property), count, tailLength, separator, dataSource ?? defaultDataSource)
            {
                ExtraKeys = ExtraKeys(), DefaultOrder = _defaultOrder,
            });
    }

    /// <summary>按年分片：节点 <c>表名_yyyy</c>，从 <paramref name="from"/> 所在的年到 <paramref name="to"/>（默认现在）所在的年。</summary>
    /// <param name="key">分片键属性（时间）。</param>
    /// <param name="from">第一期所在的时间。</param>
    /// <param name="to">最后一期所在的时间，每次路由时求值；为 null 时是 <see cref="DateTime.Now"/>。</param>
    /// <param name="separator">表名与尾巴之间的连接符。</param>
    /// <param name="dataSource">每期的数据源（参数是这一期的起点）；为 null 时是默认数据源。</param>
    /// <param name="splitTable">是否分表；为 false 时只按 <paramref name="dataSource"/> 分库，各库里的表名就是实体的表名。</param>
    public ShardRuleBuilder<T> ByYear(Expression<Func<T, DateTime?>> key, DateTime from, Func<DateTime>? to = null, string separator = "_",
        Func<DateTime, string>? dataSource = null, bool splitTable = true) =>
        ByTime(ShardPeriod.Year, key, from, to, separator, dataSource, splitTable);

    /// <summary>按月分片：节点 <c>表名_yyyyMM</c>；参数同 <see cref="ByYear"/>。</summary>
    /// <param name="key">分片键属性（时间）。</param>
    /// <param name="from">第一期所在的时间。</param>
    /// <param name="to">最后一期所在的时间，每次路由时求值；为 null 时是 <see cref="DateTime.Now"/>。</param>
    /// <param name="separator">表名与尾巴之间的连接符。</param>
    /// <param name="dataSource">每期的数据源（参数是这一期的起点）；为 null 时是默认数据源。</param>
    /// <param name="splitTable">是否分表；为 false 时只按 <paramref name="dataSource"/> 分库。</param>
    public ShardRuleBuilder<T> ByMonth(Expression<Func<T, DateTime?>> key, DateTime from, Func<DateTime>? to = null, string separator = "_",
        Func<DateTime, string>? dataSource = null, bool splitTable = true) =>
        ByTime(ShardPeriod.Month, key, from, to, separator, dataSource, splitTable);

    /// <summary>按日分片：节点 <c>表名_yyyyMMdd</c>；参数同 <see cref="ByYear"/>。</summary>
    /// <param name="key">分片键属性（时间）。</param>
    /// <param name="from">第一期所在的时间。</param>
    /// <param name="to">最后一期所在的时间，每次路由时求值；为 null 时是 <see cref="DateTime.Now"/>。</param>
    /// <param name="separator">表名与尾巴之间的连接符。</param>
    /// <param name="dataSource">每期的数据源（参数是这一期的起点）；为 null 时是默认数据源。</param>
    /// <param name="splitTable">是否分表；为 false 时只按 <paramref name="dataSource"/> 分库。</param>
    public ShardRuleBuilder<T> ByDay(Expression<Func<T, DateTime?>> key, DateTime from, Func<DateTime>? to = null, string separator = "_",
        Func<DateTime, string>? dataSource = null, bool splitTable = true) =>
        ByTime(ShardPeriod.Day, key, from, to, separator, dataSource, splitTable);

    /// <summary>
    /// 额外分片键（对应 Java 的 ShardingExtraTableKey）：条件里它的 <c>=</c> / <c>IN</c> 值经 <paramref name="toShardKey"/> 换算成主分片键的值再裁剪，
    /// 如 id 末 6 位是月份时 <c>.ExtraKey(t =&gt; t.Id, id =&gt; new DateTime(int.Parse(id[^6..^2]), int.Parse(id[^2..]), 1))</c>；换算不出时返回 null，不裁剪。
    /// </summary>
    public ShardRuleBuilder<T> ExtraKey<TKey>(Expression<Func<T, TKey>> key, Func<TKey, object?> toShardKey)
    {
        ArgumentNullException.ThrowIfNull(toShardKey);
        _extraKeys.Add((ExpressionTranslator.MemberPath(key), value => value is TKey typed ? toShardKey(typed) : null));
        return this;
    }

    /// <summary>
    /// 查询没写排序、带了 Take / Skip、命中多个节点时补的排序（对应 Java 的 addPropertyDefaultUseDesc），如按时间分表时取最新的几行。
    /// </summary>
    public ShardRuleBuilder<T> DefaultOrder<TKey>(Expression<Func<T, TKey>> property, bool descending = false)
    {
        _defaultOrder = new ShardOrder(ExpressionTranslator.MemberPath(property), descending);
        return this;
    }

    /// <summary>按登记的写法建出规则：校验属性都是实体的列。</summary>
    internal IShardRule Build(EntityMetadata entity, string defaultDataSource)
    {
        if (_primary is null) throw new InvalidOperationException($"Shard<{typeof(T).Name}> 要选一种分片方式：Mod、ByYear、ByMonth 或 ByDay");
        foreach (var (property, _) in _extraKeys) entity.GetColumn(property);
        if (_defaultOrder is { } order) entity.GetColumn(order.Property);
        return _primary(entity, defaultDataSource);
    }

    private ExtraShardKey[] ExtraKeys() => [.. _extraKeys.Select(k => new ExtraShardKey(k.Property, k.ToShardKey))];

    private ShardRuleBuilder<T> ByTime(ShardPeriod period, Expression<Func<T, DateTime?>> key, DateTime from, Func<DateTime>? to, string separator,
        Func<DateTime, string>? dataSource, bool splitTable)
    {
        ArgumentNullException.ThrowIfNull(separator);
        var property = ExpressionTranslator.MemberPath(key);
        return Primary((entity, defaultDataSource) => new TimeShardRule(typeof(T).Name, entity.TableName, property, Getter(entity, property), period, from, to,
            separator, dataSource, splitTable, defaultDataSource)
        {
            ExtraKeys = ExtraKeys(), DefaultOrder = _defaultOrder,
        });
    }

    private ShardRuleBuilder<T> Primary(Func<EntityMetadata, string, KeyedShardRule> primary)
    {
        if (_primary is not null) throw new InvalidOperationException($"Shard<{typeof(T).Name}> 只能选一种分片方式（Mod、ByYear、ByMonth、ByDay 之一）");
        _primary = primary;
        return this;
    }

    private static Func<object, object?> Getter(EntityMetadata entity, string property) => entity.GetColumn(property).Getter;
}
