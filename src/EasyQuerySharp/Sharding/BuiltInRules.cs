using System.Globalization;

namespace EasyQuerySharp.Sharding;

/// <summary>额外分片键：它的等值换算成主分片键的值（换算不出时为 null，不裁剪）。</summary>
internal sealed record ExtraShardKey(string Property, Func<object, object?> ToShardKey);

/// <summary>
/// 内置规则的公共部分：一个主分片键，加上额外分片键与默认排序。额外键只有等值能裁剪：换算成主键的值后按主键的等值裁剪
/// （与 Java 的 getExtraRouteFilter 一致），与主键各自的裁剪由引擎取交集。
/// </summary>
internal abstract class KeyedShardRule(string entityName, string keyProperty, Func<object, object?> keyGetter) : IShardRule
{
    public string KeyProperty { get; } = keyProperty;

    public IReadOnlyList<ExtraShardKey> ExtraKeys { get; init; } = [];

    public ShardOrder? DefaultOrder { get; init; }

    public abstract IReadOnlyList<ShardNode> Nodes { get; }

    public IReadOnlyCollection<string> KeyProperties => [KeyProperty, .. ExtraKeys.Select(k => k.Property)];

    public ShardNode NodeOf(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var value = keyGetter(entity) ?? throw new InvalidOperationException($"{entityName} 的分片键 {KeyProperty} 为 null，算不出写到哪张表：{entity}");
        return NodeOfKey(value) ?? throw new InvalidOperationException($"{entityName} 的分片键 {KeyProperty} = {value} 没有对应的表（节点 {Nodes[0]} … {Nodes[^1]}）：{entity}");
    }

    public IReadOnlyList<ShardNode> Prune(string propertyName, ShardOperator op, object? value)
    {
        if (value is null) return Nodes;
        if (propertyName == KeyProperty) return PruneKey(op, value);
        if (op != ShardOperator.Equal || ExtraKeys.FirstOrDefault(k => k.Property == propertyName) is not { } extra) return Nodes;
        return extra.ToShardKey(value) is { } key ? PruneKey(ShardOperator.Equal, key) : Nodes;
    }

    /// <summary>主分片键的值落在哪个节点；不在任何节点里时为 null。</summary>
    protected abstract ShardNode? NodeOfKey(object value);

    /// <summary>主分片键上的条件可能命中的节点。</summary>
    protected abstract IReadOnlyList<ShardNode> PruneKey(ShardOperator op, object value);
}

/// <summary>
/// 取模分表（对应 Java 的 AbstractShardingTableModInitializer + AbstractModTableRoute）：节点 <c>表名_0 … 表名_{n-1}</c>（尾巴左补 0 到 tailLength 位），
/// 尾巴是键的字符串按 Java <c>String.hashCode</c> 取模的绝对值，与 Java 写入的数据互通。只有等值能裁剪。
/// </summary>
internal sealed class ModShardRule : KeyedShardRule
{
    private readonly ShardNode[] _nodes;

    public ModShardRule(string entityName, string table, string keyProperty, Func<object, object?> keyGetter, int count, int tailLength, string separator, string dataSource)
        : base(entityName, keyProperty, keyGetter)
    {
        _nodes = [.. Enumerable.Range(0, count).Select(i => new ShardNode(dataSource, table + separator + i.ToString(CultureInfo.InvariantCulture).PadLeft(tailLength, '0')))];
    }

    public override IReadOnlyList<ShardNode> Nodes => _nodes;

    protected override ShardNode NodeOfKey(object value) => _nodes[Tail(value)];

    protected override IReadOnlyList<ShardNode> PruneKey(ShardOperator op, object value) => op == ShardOperator.Equal ? [_nodes[Tail(value)]] : _nodes;

    // Math.abs(String.valueOf(value).hashCode() % mod)，键先转成不区分区域的字符串
    private int Tail(object value) => Math.Abs(JavaHashCode(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "") % _nodes.Length);

    /// <summary>Java 的 <c>String.hashCode()</c>：UTF-16 码元按 31 进制累加，int 溢出回绕。</summary>
    internal static int JavaHashCode(string text)
    {
        var hash = 0;
        foreach (var c in text) hash = unchecked((31 * hash) + c);
        return hash;
    }
}

/// <summary>按时间分片的粒度。</summary>
internal enum ShardPeriod
{
    Year,
    Month,
    Day,
}

/// <summary>
/// 按年 / 月 / 日分片（对应 Java 的 AbstractShardingYear/Month/DayInitializer + AbstractYear/Month/DayTableRoute）：
/// 从 from 所在的一期到 to（默认现在）所在的一期，每期一个节点，表名 <c>表名_yyyy</c> / <c>_yyyyMM</c> / <c>_yyyyMMdd</c>，
/// 数据源由 dataSource 按每期的起点算（不分库时是默认数据源）；splitTable 为 false 时只分库、表名不变。
/// <c>=</c>、<c>&gt; &gt;= &lt; &lt;=</c> 能裁剪；<c>&lt;</c> 恰好等于某期起点时不含那一期。
/// </summary>
internal sealed class TimeShardRule : KeyedShardRule
{
    private readonly ShardPeriod _period;
    private readonly DateTime _from;
    private readonly Func<DateTime> _to;
    private readonly Func<DateTime, ShardNode> _nodeOf;
    private volatile Snapshot? _snapshot;

    public TimeShardRule(string entityName, string table, string keyProperty, Func<object, object?> keyGetter, ShardPeriod period, DateTime from, Func<DateTime>? to,
        string separator, Func<DateTime, string>? dataSource, bool splitTable, string defaultDataSource)
        : base(entityName, keyProperty, keyGetter)
    {
        _period = period;
        _from = Truncate(from);
        _to = to ?? (() => DateTime.Now);
        var format = period switch { ShardPeriod.Year => "yyyy", ShardPeriod.Month => "yyyyMM", _ => "yyyyMMdd" };
        _nodeOf = start => new ShardNode(dataSource?.Invoke(start) ?? defaultDataSource,
            splitTable ? table + separator + start.ToString(format, CultureInfo.InvariantCulture) : table);
    }

    public override IReadOnlyList<ShardNode> Nodes => Current().Nodes;

    protected override ShardNode? NodeOfKey(object value) =>
        ToDateTime(value) is { } time ? Current().Periods.FirstOrDefault(p => p.Start == Truncate(time))?.Node : null;

    protected override IReadOnlyList<ShardNode> PruneKey(ShardOperator op, object value)
    {
        var snapshot = Current();
        if (ToDateTime(value) is not { } time) return snapshot.Nodes;
        var start = Truncate(time);
        Func<DateTime, bool> match = op switch
        {
            ShardOperator.Equal => s => s == start,
            ShardOperator.GreaterThan or ShardOperator.GreaterThanOrEqual => s => s >= start,
            // 小于某期的起点：那一期不用查
            ShardOperator.LessThan when time == start => s => s < start,
            _ => s => s <= start,
        };
        return [.. snapshot.Periods.Where(p => match(p.Start)).Select(p => p.Node).Distinct()];
    }

    // 节点按截止时间实时计算，截止时间落在同一期时沿用上次的结果
    private Snapshot Current()
    {
        var end = Truncate(_to());
        if (_snapshot is { } cached && cached.End == end) return cached;
        if (end < _from) throw new InvalidOperationException($"分片的起始时间 {_from:yyyy-MM-dd} 晚于截止时间 {end:yyyy-MM-dd}");
        var periods = new List<Period>();
        for (var start = _from; start <= end; start = Next(start)) periods.Add(new Period(start, _nodeOf(start)));
        return _snapshot = new Snapshot(end, periods, [.. periods.Select(p => p.Node).Distinct()]);
    }

    private DateTime Truncate(DateTime time) => _period switch
    {
        ShardPeriod.Year => new DateTime(time.Year, 1, 1, 0, 0, 0, time.Kind),
        ShardPeriod.Month => new DateTime(time.Year, time.Month, 1, 0, 0, 0, time.Kind),
        _ => time.Date,
    };

    private DateTime Next(DateTime start) => _period switch
    {
        ShardPeriod.Year => start.AddYears(1),
        ShardPeriod.Month => start.AddMonths(1),
        _ => start.AddDays(1),
    };

    private static DateTime? ToDateTime(object value) => value switch
    {
        DateTime d => d,
        DateTimeOffset o => o.DateTime,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        _ => null,
    };

    private sealed record Period(DateTime Start, ShardNode Node);

    private sealed record Snapshot(DateTime End, IReadOnlyList<Period> Periods, IReadOnlyList<ShardNode> Nodes);
}
