using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 一处集合导航的分组合并设置（对应 Java 的 subQueryToGroupJoin / subQueryConfigure）：
/// <paramref name="Enabled"/> 为 false 时显式关闭（导航上声明了 GroupJoin 也按子查询翻译）；
/// <paramref name="Configure"/> 给分组子查询再加条件。
/// </summary>
internal sealed record GroupJoinSpec(Type OwnerType, string Member, bool Enabled, Func<Querying.QueryState, Querying.QueryState>? Configure);

/// <summary>
/// 分组合并前先"取一段行"（Java 的 <c>orderBy(…).elements(i, j)</c>，C# 的 <c>OrderBy(…).Skip(i).Take(n)</c>）：
/// <paramref name="Filters"/> 是取行之前的条件，<paramref name="Orders"/> 是每组内的排序；<paramref name="Key"/> 区分不同的取法，
/// 同样取法的聚合合并到同一张派生表。
/// </summary>
internal sealed record GroupJoinWindow(
    IReadOnlyList<System.Linq.Expressions.LambdaExpression> Filters,
    IReadOnlyList<(System.Linq.Expressions.LambdaExpression Key, bool Descending)> Orders,
    long Skip,
    long? Take,
    string Key);

/// <summary>分组聚合的一个条件（按 AND 拆开的合取项）；<paramref name="Key"/> 是结构键，用来判断几个聚合是否共有这个条件。</summary>
internal sealed record GroupFilter(SqlExpr Sql, string Key);

/// <summary>分组合并里的一种聚合。</summary>
internal enum GroupAggregate
{
    Count,
    Sum,
    Max,
    Min,
    Avg,
    Any,
    None,
    Joining,
}

/// <summary>
/// 一处集合导航合并成的 <c>LEFT JOIN (SELECT 关联键 AS __group_key1__, 聚合… FROM 目标 GROUP BY 关联键) tN ON …</c>（与 Java 一致）：
/// 导航上的每个不同聚合（按种类、条件、值去重）是派生表的一列，依次编号 <c>__sum2__</c>、<c>__count3__</c>…；
/// 各聚合的 Where 条件写成 <c>CASE WHEN 条件 THEN 值 ELSE … END</c>；全部聚合共有的条件提到派生表的 WHERE。
/// 外层引用：计数、求和写成 <c>IFNULL(tN.__sum2__,0)</c>，Any / None 写成 <c>IFNULL(tN.__any2__,false / true)</c>。
/// 聚合在整条查询翻译完之后才知道全部，派生表的列由 <see cref="Complete"/> 生成。
/// </summary>
internal sealed class GroupJoinPlan
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<string, Entry> _byKey = new(StringComparer.Ordinal);

    /// <param name="inner">分组子查询（FROM 目标表，取一段行时 FROM 编了行号的派生表）。</param>
    /// <param name="element">子查询里集合元素的绑定：目标表，或取一段行后的派生表。</param>
    /// <param name="keys">子查询里的关联键列：一对多是目标表的外键，多对多是中间表里对应所属表的列。</param>
    public GroupJoinPlan(SelectQuery inner, SourceBinding element, IReadOnlyList<ColumnExpr> keys)
    {
        Inner = inner;
        Element = element;
        Table = new DerivedTableSource(inner);
        for (var i = 0; i < keys.Count; i++)
            _keyColumns.Add((keys[i], "__group_key" + (i + 1) + "__"));
        Inner.GroupBy.AddRange(_keyColumns.Select(k => (SqlExpr)k.Column));
    }

    private readonly List<(ColumnExpr Column, string Alias)> _keyColumns = [];
    private readonly List<SqlExpr> _baseWhere = [];

    /// <summary>分组子查询。</summary>
    public SelectQuery Inner { get; }

    /// <summary>子查询里集合元素的绑定。</summary>
    public SourceBinding Element { get; }

    /// <summary>各聚合共有的条件是否提到 WHERE（默认是；Java 的 GROUP_JOIN_NOT_ALLOW_AUTO_MERGE 对应 false）。</summary>
    public bool MergeFilters { get; set; } = true;

    /// <summary>必有的导航（INNER JOIN）：每行都连得上，外层引用聚合时不包 IFNULL。</summary>
    public bool Required { get; set; }

    /// <summary>GroupJoin(…, q =&gt; q.UseValueFilter(…)) 配的值过滤：只作用于这个导航聚合里的条件（主查询的值过滤管不到导航里）。</summary>
    public Filtering.ValueFilter ValueFilter { get; set; }

    /// <summary>子查询的翻译上下文（条件里的隐式 JOIN 加到子查询上，同一导航只连一次）。</summary>
    public TranslationContext InnerContext { get; set; } = null!;

    /// <summary>外层 JOIN 的派生表。</summary>
    public DerivedTableSource Table { get; }

    /// <summary>外层 ON 用的分组键列（派生表上的 __group_keyN__）。</summary>
    public IEnumerable<ColumnExpr> KeyColumns => _keyColumns.Select(k => new ColumnExpr(Table, k.Alias));

    /// <summary>子查询固定的条件（目标表自带的过滤、Configure 加的条件），写在 WHERE 里。</summary>
    public void AddBaseWhere(SqlExpr predicate) => _baseWhere.Add(predicate);

    /// <summary>
    /// 登记一个聚合，返回外层引用它的表达式。<paramref name="filters"/> 是聚合的条件（按 AND 拆开的合取项及其结构键），
    /// <paramref name="valueKey"/> 是值的结构键：种类、条件、值、是否去重都相同时复用同一列。
    /// </summary>
    public SqlExpr Reference(GroupAggregate kind, SqlExpr? value, IReadOnlyList<GroupFilter> filters, bool distinct, string valueKey, SqlExpr? separator = null)
    {
        var filterKey = string.Join(" && ", filters.Select(f => f.Key));
        var key = string.Join('\u0001', kind, filterKey, valueKey, distinct, separator is ParameterExpr p ? p.Value : null);
        if (!_byKey.TryGetValue(key, out var entry))
        {
            // 编号接在分组键之后（单列键时从 2 开始）
            var alias = "__" + kind.ToString().ToLowerInvariant() + (_entries.Count + _keyColumns.Count + 1) + "__";
            entry = new Entry(kind, value, filters, distinct, alias, separator);
            _entries.Add(entry);
            _byKey[key] = entry;
        }
        var column = new ColumnExpr(Table, entry.Alias);
        if (Required) return column;
        return kind switch
        {
            GroupAggregate.Count or GroupAggregate.Sum or GroupAggregate.Avg => new FunctionExpr(SqlFunctionNames.NullOrDefault, [column, new LiteralExpr("0")]),
            GroupAggregate.Any => new FunctionExpr(SqlFunctionNames.NullOrDefault, [column, new ParameterExpr(false, typeof(bool))]),
            GroupAggregate.None => new FunctionExpr(SqlFunctionNames.NullOrDefault, [column, new ParameterExpr(true, typeof(bool))]),
            _ => column,
        };
    }

    /// <summary>
    /// 按已登记的聚合生成子查询的 SELECT 与 WHERE；可重复调用（以最后一次为准）。
    /// 全部聚合都有条件时，它们共有的合取项（按第一个聚合里的顺序）提到 WHERE，各聚合的 CASE 里只留其余部分（与 Java 一致）；
    /// <see cref="MergeFilters"/> 为 false 时不提。
    /// </summary>
    public void Complete()
    {
        Inner.Columns.Clear();
        Inner.Where.Clear();
        Inner.Where.AddRange(_baseWhere);
        foreach (var (column, alias) in _keyColumns) Inner.Columns.Add(new SelectColumn(column, alias));

        var shared = MergeFilters && _entries.Count > 0 && _entries.All(e => e.Filters.Count > 0)
            ? _entries[0].Filters.Select(f => f.Key).Where(k => _entries.All(e => e.Filters.Any(f => f.Key == k))).ToHashSet(StringComparer.Ordinal)
            : [];
        Inner.Where.AddRange(_entries.Count == 0 ? [] : _entries[0].Filters.Where(f => shared.Contains(f.Key)).Select(f => f.Sql));
        foreach (var e in _entries) Inner.Columns.Add(new SelectColumn(Aggregate(e, e.Filters.Where(f => !shared.Contains(f.Key)).Select(f => f.Sql).ToList()), e.Alias));
    }

    // remaining：提到 WHERE 之后还要写进 CASE 的条件
    private static SqlExpr Aggregate(Entry e, List<SqlExpr> remaining)
    {
        // 没有值的计数：无条件是 COUNT(*)，有条件时计 1（与 Java 一致）
        SqlExpr? value = e.Value;
        if (value is null && e.Filters.Count > 0) value = new ParameterExpr(1, typeof(int));
        if (remaining.Count > 0)
        {
            SqlExpr otherwise = e.Kind == GroupAggregate.Sum ? new ParameterExpr(0, typeof(int)) : LiteralExpr.Null;
            value = new CaseExpr([(LogicalExpr.And(remaining), value!)], otherwise);
        }
        var argument = value ?? LiteralExpr.Star;
        return e.Kind switch
        {
            GroupAggregate.Count => new FunctionExpr(e.Distinct ? SqlFunctionNames.CountDistinct : SqlFunctionNames.Count, [argument]),
            GroupAggregate.Sum => new FunctionExpr(e.Distinct ? SqlFunctionNames.SumDistinct : SqlFunctionNames.Sum, [argument]),
            GroupAggregate.Max => new FunctionExpr(SqlFunctionNames.Max, [argument]),
            GroupAggregate.Min => new FunctionExpr(SqlFunctionNames.Min, [argument]),
            GroupAggregate.Avg => new FunctionExpr(e.Distinct ? SqlFunctionNames.AvgDistinct : SqlFunctionNames.Avg, [argument]),
            GroupAggregate.Any => new FragmentExpr("(COUNT({0}) > 0)", [argument]),
            GroupAggregate.None => new FragmentExpr("(COUNT({0}) <= 0)", [argument]),
            _ => new FunctionExpr(e.Distinct ? SqlFunctionNames.StringJoinDistinct : SqlFunctionNames.StringJoin, [argument, e.Separator!]),
        };
    }

    private sealed record Entry(GroupAggregate Kind, SqlExpr? Value, IReadOnlyList<GroupFilter> Filters, bool Distinct, string Alias, SqlExpr? Separator);
}
