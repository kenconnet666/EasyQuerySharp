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
}

/// <summary>子查询作为派生表。</summary>
internal sealed record SubQuerySource(QueryState Inner) : QuerySource;

/// <summary>多条查询做 UNION / UNION ALL 后作为派生表，列按第一条查询的投影访问。</summary>
internal sealed record UnionSource(ImmutableArray<QueryState> Parts, bool All) : QuerySource;

/// <summary>原生 SQL 作为派生表，列按实体的列名访问（对应 Java 的 queryable(sql, Class)）。</summary>
internal sealed record RawSqlSource(string Sql, Type EntityType) : QuerySource;

/// <summary>按什么排序。</summary>
internal sealed record OrderSpec(LambdaExpression Key, bool Descending);

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

    /// <summary>关闭逻辑删除过滤。</summary>
    public bool DisableLogicDelete { get; init; }

    /// <summary>追踪作用域里也不追踪这条查询的结果（AsNoTracking）。</summary>
    public bool NoTracking { get; init; }

    /// <summary>这条查询上生效的拦截器。</summary>
    public InterceptorSelection Interceptors { get; init; } = InterceptorSelection.Default;

    /// <summary>投影之后再加条件、排序、连接时，把当前查询包成派生表，在外层继续（与 Java 版的 select 之后再 where 一致）。</summary>
    public QueryState Wrap() => new(Db, [new SubQuerySource(this)]);

    /// <summary>是否已经有投影（之后的条件要包一层）。</summary>
    public bool HasSelector => Selector is not null;

    /// <summary>查询结果的元素类型：投影的返回类型，没有投影时是第一个来源的类型。</summary>
    public Type ElementType => Selector?.ReturnType ?? Sources[0] switch
    {
        EntitySource e => e.EntityType,
        SubQuerySource s => s.Inner.ElementType,
        UnionSource u => u.Parts[0].ElementType,
        RawSqlSource r => r.EntityType,
        _ => throw new NotSupportedException(Sources[0].GetType().Name),
    };
}
