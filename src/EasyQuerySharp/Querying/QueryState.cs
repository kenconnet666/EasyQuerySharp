using System.Collections.Immutable;
using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Querying;

/// <summary>查询里的一个表来源：实体表，或另一条查询作为派生表。</summary>
internal abstract record QuerySource
{
    /// <summary>用户指定的别名（AsAlias）。</summary>
    public string? Alias { get; init; }

    /// <summary>这个表来源不加逻辑删除条件（DisableTableLogicDelete）。</summary>
    public bool NoLogicDelete { get; init; }

    /// <summary>与前面表的连接方式；FROM 的第一个来源为 null。</summary>
    public JoinKind? Join { get; init; }

    /// <summary>ON 条件 lambda，参数依次对应到这一个为止的全部来源。</summary>
    public LambdaExpression? On { get; init; }
}

/// <summary>实体表来源。</summary>
internal sealed record EntitySource(Type EntityType) : QuerySource
{
    /// <summary>覆盖表名（AsTable）。</summary>
    public string? TableName { get; init; }

    /// <summary>整个表引用的写法（AsTableSegment）：(带引号的表名, 别名) → SQL 片段。</summary>
    public Func<string, string?, string>? TableSegment { get; init; }
}

/// <summary>子查询作为派生表。</summary>
internal sealed record SubQuerySource(QueryState Inner) : QuerySource;

/// <summary>已经建好绑定的表来源（编译器内部用：如带出按父对象分区取段后的派生表），按 <paramref name="EntityType"/> 的成员访问列。</summary>
internal sealed record BoundSource(Translation.SourceBinding Binding, Type EntityType) : QuerySource;

/// <summary>多条查询做 UNION / UNION ALL 后作为派生表，列按第一条查询的投影访问。</summary>
internal sealed record UnionSource(ImmutableArray<QueryState> Parts, bool All) : QuerySource
{
    /// <summary>各分支各自编号别名（每支都从 t 开始，外层是 t1）：填充按键拆出的分支是各自独立的查询（与 Java 一致）。</summary>
    public bool IndependentAliases { get; init; }
}

/// <summary>原生 SQL 作为派生表，列按实体的列名访问（对应 Java 的 queryable(sql, Class)）。</summary>
internal sealed record RawSqlSource(string Sql, Type EntityType, IReadOnlyList<object?> Parameters) : QuerySource;

/// <summary>公共表表达式（AsCte）：定义是 <paramref name="Inner"/>，按名字引用。</summary>
internal sealed record CteSource(string Name, QueryState Inner) : QuerySource
{
    /// <summary>由查询定义的实体（ICteView）的类型；普通 AsCte 时为 null。</summary>
    public Type? ViewType { get; init; }
}

/// <summary>
/// 树查询（AsTreeCte，对应 Java 的 asTreeCTE）：递归 CTE 的基础部分是 <paramref name="Base"/>，递归部分按
/// <paramref name="SelfColumns"/>（节点键）与 <paramref name="ParentColumns"/>（指向上级的键）连接；外层按 CTE 的名字引用。
/// </summary>
internal sealed record TreeCteSource(QueryState Base, IReadOnlyList<Metadata.ColumnMetadata> SelfColumns, IReadOnlyList<Metadata.ColumnMetadata> ParentColumns,
    TreeCteOptions Options, LambdaExpression? ChildFilter) : QuerySource;

/// <summary>按什么排序。</summary>
internal sealed record OrderSpec(LambdaExpression Key, bool Descending, bool Raw = false);

/// <summary>
/// 查询的不可变描述：链式调用每次返回新的 <see cref="QueryState"/>，只记录 lambda，执行时才由 <see cref="QueryCompiler"/> 翻译。
/// 各 lambda 的参数依次对应 <see cref="Sources"/>。
/// </summary>
internal sealed record QueryState(EasyDb Db, ImmutableArray<QuerySource> Sources)
{
    /// <summary>WHERE 条件，按 AND 连接。</summary>
    public ImmutableArray<LambdaExpression> Wheres { get; init; } = [];

    /// <summary>ORDER BY。</summary>
    public ImmutableArray<OrderSpec> OrderBys { get; init; } = [];

    /// <summary>投影；为 null 时投影到第一个来源的实体。</summary>
    public LambdaExpression? Selector { get; init; }

    /// <summary>原生 SQL 的 SELECT 列表（对应 Java 的 select(String)）；结果按列名映射到第一个来源的实体。</summary>
    public string? RawSelect { get; init; }

    /// <summary>分组键 lambda（参数依次对应各来源）；为 null 表示不分组。</summary>
    public LambdaExpression? GroupKey { get; init; }

    /// <summary>分组的汇总方式（<c>Rollup()</c> / <c>Cube()</c>）。</summary>
    public SqlAst.GroupByMode GroupByMode { get; init; }

    /// <summary>HAVING 条件，lambda 的参数是 <see cref="IGroup{TKey,TElement}"/>。</summary>
    public ImmutableArray<LambdaExpression> Havings { get; init; } = [];

    /// <summary>DISTINCT。</summary>
    public bool Distinct { get; init; }

    /// <summary>跳过的行数。</summary>
    public long? Offset { get; init; }

    /// <summary>最多返回的行数。</summary>
    public long? Limit { get; init; }

    /// <summary>单表时也写别名 t（带出的子查询在 Java 版里总带别名）。</summary>
    public bool ForceAlias { get; init; }

    /// <summary>投影之后加的条件尽量推进派生表内层（见 <see cref="SmartPredicates"/>，对应 Java 的 SMART_PREDICATE）。</summary>
    public bool SmartPredicate { get; init; }

    /// <summary>树查询（AsTreeCte）有自定义投影时也在最后带上层级列（ToTreeAsync 按层级配对父子）。</summary>
    public bool AppendTreeDeep { get; init; }

    /// <summary>结果读出后依次执行的填充（FillMany / FillOne），参数是本次的结果行。</summary>
    public ImmutableArray<Func<System.Collections.IList, CancellationToken, Task>> Fills { get; init; } = [];

    /// <summary>第一个表来源上要合并成分组 LEFT JOIN 的集合导航（GroupJoin / GroupJoinIf）。</summary>
    public ImmutableArray<Translation.GroupJoinSpec> GroupJoins { get; init; } = [];

    /// <summary>导航级的固定条件（FilterNavigation，对应 Java 的 nav().filter(..)）：对一导航写进 JOIN 的 ON，集合导航写进它的子查询 / 分组合并。</summary>
    public ImmutableArray<Translation.NavigationFilterSpec> NavigationFilters { get; init; } = [];

    /// <summary>全部集合导航都合并成分组 JOIN（GroupJoinAll）。</summary>
    public bool GroupJoinAll { get; init; }

    /// <summary>分组合并时不提取共有条件（GroupJoinNoMerge）。</summary>
    public bool GroupJoinNoMerge { get; init; }

    /// <summary>实体查询上的导航带出（Include）；有投影时不生效。</summary>
    public ImmutableArray<IncludeSpec> Includes { get; init; } = [];

    /// <summary>投影是框架补上的（如分组后只取键），不算显式投影：单表时不写别名（与 Java 的 groupBy(…).toList() 一致）。</summary>
    public bool ImplicitSelector { get; init; }

    /// <summary>关闭逻辑删除过滤。</summary>
    public bool DisableLogicDelete { get; init; }

    /// <summary>结果最多允许的行数（MaxResultSize），超过时报错；为 null 时不限制。</summary>
    public int? ResultSizeLimit { get; init; }

    /// <summary>追踪作用域里也不追踪这条查询的结果（AsNoTracking）。</summary>
    public bool NoTracking { get; init; }

    /// <summary>分页时不做深分页反向排序（DisableReversePaging）。</summary>
    public bool NoReversePaging { get; init; }

    /// <summary>行锁（ForUpdate / ForUpdateNoWait / ForUpdateSkipLocked）。</summary>
    public RowLock Lock { get; init; }

    /// <summary>这条查询上生效的拦截器。</summary>
    public InterceptorSelection Interceptors { get; init; } = InterceptorSelection.Default;

    /// <summary>之后追加的 Where 适用的条件值过滤（UseValueFilter）。</summary>
    public Filtering.ValueFilter ValueFilter { get; init; }

    /// <summary>传播到导航条件与子查询里的值过滤（UseValueFilter(…, includeNavigations: true)，对应 Java 的 DEFAULT_PROPAGATION_SUPPORTS）。</summary>
    public Filtering.ValueFilter NavigationValueFilter { get; init; }

    /// <summary>追加一个 Where 条件：先按 <see cref="ValueFilter"/> 去掉值被拒绝的比较，全部去掉时不追加。</summary>
    public QueryState AddWhere(LambdaExpression predicate)
    {
        var filtered = Filtering.ValueFilterRewriter.Apply(predicate, ValueFilter);
        // 被过滤掉的条件里用过会建表的导航时，Java 已经按多表写法给主表起了别名 t，去掉条件后仍保留（逐字对齐）
        var state = Filtering.ValueFilterRewriter.DroppedNavigation(predicate, filtered, IsGroupJoined) ? this with { ForceAlias = true } : this;
        if (filtered is null) return state;
        if (!SmartPredicate) return state with { Wheres = state.Wheres.Add(filtered) };
        var (pushed, remaining) = SmartPredicates.PushDown(state, filtered);
        return remaining is null ? pushed : pushed with { Wheres = pushed.Wheres.Add(remaining) };
    }

    // 这个集合导航是否合并成分组 JOIN：GroupJoinAll、GroupJoin 声明或导航上标了 GroupJoin
    private bool IsGroupJoined(System.Reflection.MemberInfo member) =>
        GroupJoins.LastOrDefault(s => s.Member == member.Name) is { } spec
            ? spec.Enabled
            : GroupJoinAll || member.GetCustomAttributes(typeof(Metadata.NavigateAttribute), true) is [Metadata.NavigateAttribute { GroupJoin: true }];

    /// <summary>投影之后再加条件、排序、连接时，把当前查询包成派生表，在外层继续（与 Java 版的 select 之后再 where 一致）。行锁跟到外层：锁只写在最外层查询上。</summary>
    public QueryState Wrap() => new(Db, [new SubQuerySource(this with { Fills = [], Lock = RowLock.None })])
    {
        ValueFilter = ValueFilter, NavigationValueFilter = NavigationValueFilter, Fills = Fills, Lock = Lock,
        // 分组合并的设置跟到外层：投影包一层后，外层投影里的导航仍按同样的方式合并
        GroupJoins = GroupJoins, GroupJoinAll = GroupJoinAll, GroupJoinNoMerge = GroupJoinNoMerge, NavigationFilters = NavigationFilters,
        SmartPredicate = SmartPredicate,
    };

    /// <summary>是否已经有投影（之后的条件要包一层）。</summary>
    public bool HasSelector => Selector is not null;

    /// <summary>
    /// 只有一个表来源、没有加任何条件、投影、排序等的查询（<c>db.Query&lt;T&gt;()</c>、AsCte 之后直接引用）：
    /// 作为 JOIN 来源时直接用这个表来源，不再包一层派生表（与 Java 一致）。
    /// 无实体的表（db.Table）除外：Java 的 Map 查询作为 JOIN 来源总是 <c>(SELECT * FROM 表 t) t1</c>。
    /// </summary>
    private QuerySource? BareSource =>
        Sources is [EntitySource or CteSource] && !IsDynamicTable && Wheres.IsEmpty && OrderBys.IsEmpty && Selector is null && RawSelect is null && GroupKey is null
        && !Distinct && Offset is null && Limit is null && Includes.IsEmpty && Fills.IsEmpty && !DisableLogicDelete && Lock == RowLock.None
        && Interceptors == InterceptorSelection.Default
            ? Sources[0]
            : null;

    /// <summary>第一个来源是无实体的表（<see cref="EasyDb.Table"/>）。</summary>
    public bool IsDynamicTable => Sources[0] is EntitySource { EntityType: var type } && type == typeof(DynamicRow);

    /// <summary>作为 JOIN 来源：裸的实体表 / CTE 引用直接用，其余包成派生表。</summary>
    public QuerySource AsJoinSource() => BareSource is { } source ? source with { } : new SubQuerySource(this);

    /// <summary>查询结果的元素类型：投影的返回类型，没有投影时是第一个来源的类型。</summary>
    public Type ElementType => Selector?.ReturnType ?? TypeOf(Sources[0]);

    /// <summary>一个表来源的元素类型（lambda 里对应参数的类型）。</summary>
    public static Type TypeOf(QuerySource source) => source switch
    {
        EntitySource e => e.EntityType,
        SubQuerySource s => s.Inner.ElementType,
        UnionSource u => u.Parts[0].ElementType,
        RawSqlSource r => r.EntityType,
        CteSource c => c.Inner.ElementType,
        TreeCteSource t => t.Base.ElementType,
        BoundSource b => b.EntityType,
        _ => throw new NotSupportedException(source.GetType().Name),
    };
}
