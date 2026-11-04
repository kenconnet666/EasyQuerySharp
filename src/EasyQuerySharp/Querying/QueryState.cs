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

/// <summary>多条查询做 UNION / UNION ALL 后作为派生表，列按第一条查询的投影访问。</summary>
internal sealed record UnionSource(ImmutableArray<QueryState> Parts, bool All) : QuerySource
{
    /// <summary>各分支各自编号别名（每支都从 t 开始，外层是 t1）：填充按键拆出的分支是各自独立的查询（与 Java 一致）。</summary>
    public bool IndependentAliases { get; init; }
}

/// <summary>原生 SQL 作为派生表，列按实体的列名访问（对应 Java 的 queryable(sql, Class)）。</summary>
internal sealed record RawSqlSource(string Sql, Type EntityType, IReadOnlyList<object?> Parameters) : QuerySource;

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

    /// <summary>结果读出后依次执行的填充（FillMany / FillOne），参数是本次的结果行。</summary>
    public ImmutableArray<Func<System.Collections.IList, CancellationToken, Task>> Fills { get; init; } = [];

    /// <summary>实体查询上的导航带出（Include）；有投影时不生效。</summary>
    public ImmutableArray<Includes.IncludeSpec> Includes { get; init; } = [];

    /// <summary>关闭逻辑删除过滤。</summary>
    public bool DisableLogicDelete { get; init; }

    /// <summary>结果最多允许的行数（MaxResultSize），超过时报错；为 null 时不限制。</summary>
    public int? ResultSizeLimit { get; init; }

    /// <summary>追踪作用域里也不追踪这条查询的结果（AsNoTracking）。</summary>
    public bool NoTracking { get; init; }

    /// <summary>这条查询上生效的拦截器。</summary>
    public InterceptorSelection Interceptors { get; init; } = InterceptorSelection.Default;

    /// <summary>之后追加的 Where 适用的条件值过滤（UseValueFilter）。</summary>
    public Filtering.ValueFilter ValueFilter { get; init; }

    /// <summary>追加一个 Where 条件：先按 <see cref="ValueFilter"/> 去掉值被拒绝的比较，全部去掉时不追加。</summary>
    public QueryState AddWhere(LambdaExpression predicate) =>
        Filtering.ValueFilterRewriter.Apply(predicate, ValueFilter) is { } filtered ? this with { Wheres = Wheres.Add(filtered) } : this;

    /// <summary>投影之后再加条件、排序、连接时，把当前查询包成派生表，在外层继续（与 Java 版的 select 之后再 where 一致）。</summary>
    public QueryState Wrap() => new(Db, [new SubQuerySource(this with { Fills = [] })]) { ValueFilter = ValueFilter, Fills = Fills };

    /// <summary>是否已经有投影（之后的条件要包一层）。</summary>
    public bool HasSelector => Selector is not null;

    /// <summary>查询结果的元素类型：投影的返回类型，没有投影时是第一个来源的类型。</summary>
    public Type ElementType => Selector?.ReturnType ?? TypeOf(Sources[0]);

    /// <summary>一个表来源的元素类型（lambda 里对应参数的类型）。</summary>
    public static Type TypeOf(QuerySource source) => source switch
    {
        EntitySource e => e.EntityType,
        SubQuerySource s => s.Inner.ElementType,
        UnionSource u => u.Parts[0].ElementType,
        RawSqlSource r => r.EntityType,
        _ => throw new NotSupportedException(source.GetType().Name),
    };
}
