using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 单表查询（也是投影之后的查询）。条件、排序、投影都写成普通 C# lambda，由表达式树翻译成 SQL：
/// <code>
/// var list = await db.Query&lt;Topic&gt;()
///     .Where(t => t.Title.Contains("easy") &amp;&amp; t.Stars > 10)
///     .OrderByDescending(t => t.CreateTime)
///     .Select(t => new { t.Id, t.Title })
///     .ToListAsync();
/// </code>
/// <c>OrderBy</c> 是追加排序（与 Java 版一致），<c>ThenBy</c> 是它的别名；LINQ 查询语法（from / where / orderby / select）同样可用。
/// </summary>
public sealed class Query<T> : QueryBase<Query<T>, T>
{
    internal Query(QueryState state) : base(state)
    {
    }

    private protected override Query<T> With(QueryState state) => new(state);

    // 投影之后再加条件或排序：先把当前查询包成派生表
    private QueryState Open => State.HasSelector ? State.Wrap() : State;

    /// <summary>追加条件（多个 Where 之间是 AND）。</summary>
    public Query<T> Where(Expression<Func<T, bool>> predicate) => new(Open with { Wheres = Open.Wheres.Add(predicate) });

    /// <summary><paramref name="condition"/> 为 true 时才追加条件。</summary>
    public Query<T> WhereIf(bool condition, Expression<Func<T, bool>> predicate) => condition ? Where(predicate) : this;

    /// <summary>按主键等值（对应 Java 的 whereById）。</summary>
    public Query<T> WhereById(object id) => Where(Commands.KeyPredicates.ById<T>(Db.Metadata.Get<T>(), id));

    /// <summary>按主键 IN（对应 Java 的 whereByIds）。</summary>
    public Query<T> WhereByIds(IEnumerable ids) => Where(Commands.KeyPredicates.ByIds<T>(Db.Metadata.Get<T>(), ids));

    /// <summary>追加升序排序。</summary>
    public Query<T> OrderBy<TKey>(Expression<Func<T, TKey>> key) => Order(key, false);

    /// <summary>追加降序排序。</summary>
    public Query<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key) => Order(key, true);

    /// <summary><paramref name="condition"/> 为 true 时才追加升序排序。</summary>
    public Query<T> OrderByIf<TKey>(bool condition, Expression<Func<T, TKey>> key) => condition ? OrderBy(key) : this;

    /// <summary><paramref name="condition"/> 为 true 时才追加降序排序。</summary>
    public Query<T> OrderByDescendingIf<TKey>(bool condition, Expression<Func<T, TKey>> key) => condition ? OrderByDescending(key) : this;

    /// <summary>追加升序排序（同 <see cref="OrderBy{TKey}"/>）。</summary>
    public Query<T> ThenBy<TKey>(Expression<Func<T, TKey>> key) => Order(key, false);

    /// <summary>追加降序排序（同 <see cref="OrderByDescending{TKey}"/>）。</summary>
    public Query<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key) => Order(key, true);

    /// <summary>按键分组（对应 Java 的 groupBy(GroupKeys.of(…))）。多列键写成匿名类型：<c>GroupBy(b => new { b.Status, b.Top })</c>。</summary>
    public GroupQuery<TKey, T> GroupBy<TKey>(Expression<Func<T, TKey>> key) => new(Open with { GroupKey = key });

    /// <summary>投影到 DTO、匿名类型或标量。投影之后再 Where / OrderBy 会把它包成派生表。</summary>
    public Query<TResult> Select<TResult>(Expression<Func<T, TResult>> selector) => new(Open with { Selector = selector });

    /// <summary>
    /// 按属性名自动投影到 DTO（对应 Java 的 select(Dto.class)）：DTO 的每个可写属性取实体上的同名列，
    /// DTO 属性上的 <c>[Column("…")]</c> 决定别名。
    /// </summary>
    public Query<TResult> Select<TResult>() where TResult : new() =>
        new(Open with { Selector = AutoProjection.Build(Db.Metadata.Get<T>(), typeof(TResult)) });

    /// <summary>
    /// 按属性名自动投影到 <typeparamref name="TResult"/>，但排除 <paramref name="excluded"/> 列出的属性（对应 Java 的 FETCHER.allFieldsExclude）。
    /// </summary>
    /// <example><code>.SelectExcept&lt;Blog&gt;(b => new { b.Id, b.Content })</code></example>
    public Query<TResult> SelectExcept<TResult>(Expression<Func<T, object?>> excluded) where TResult : new() =>
        new(Open with { Selector = AutoProjection.Build(Db.Metadata.Get<T>(), typeof(TResult), AutoProjection.MemberNames(excluded)) });

    /// <summary>
    /// 用原生 SQL 作为 SELECT 列表（对应 Java 的 select(String)），结果按列名映射回 <typeparamref name="T"/>。
    /// </summary>
    /// <example><code>db.Query&lt;Blog&gt;().WhereById("1").SelectRaw("1 as id")</code></example>
    public Query<T> SelectRaw(string selectSql) => new(Open with { RawSelect = selectSql });

    /// <summary>给最近一个表来源指定别名（对应 Java 的 asAlias）。</summary>
    public Query<T> AsAlias(string alias) => new(QueryStates.WithLast(State, s => s with { Alias = alias }));

    /// <summary>这条查询里把最近一个实体表换成 <paramref name="tableName"/>（对应 Java 的 asTable）。</summary>
    public Query<T> AsTable(string tableName) => new(QueryStates.WithLast(State, s => s is EntitySource e ? e with { TableName = tableName } : s));

    /// <summary>LEFT JOIN 实体 <typeparamref name="T2"/>。</summary>
    public Query<T, T2> LeftJoin<T2>(Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Left, new EntitySource(typeof(T2)), on);

    /// <summary>INNER JOIN 实体 <typeparamref name="T2"/>。</summary>
    public Query<T, T2> InnerJoin<T2>(Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Inner, new EntitySource(typeof(T2)), on);

    /// <summary>RIGHT JOIN 实体 <typeparamref name="T2"/>。</summary>
    public Query<T, T2> RightJoin<T2>(Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Right, new EntitySource(typeof(T2)), on);

    /// <summary>LEFT JOIN 一个子查询（作为派生表）。</summary>
    public Query<T, T2> LeftJoin<T2>(Query<T2> subQuery, Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Left, new SubQuerySource(subQuery.State), on);

    /// <summary>INNER JOIN 一个子查询（作为派生表）。</summary>
    public Query<T, T2> InnerJoin<T2>(Query<T2> subQuery, Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Inner, new SubQuerySource(subQuery.State), on);

    /// <summary>按主键查一行；没有时返回 null（对应 Java 的 findOrNull）。</summary>
    public Task<T?> FindAsync(object id, CancellationToken cancellationToken = default) => WhereById(id).FirstOrDefaultAsync(cancellationToken);

    /// <summary>SUM。没有行时返回 <c>default</c>（结果类型写成可空即得到 null）。</summary>
    public Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
        AggregateAsync<TResult>(SqlFunctionNames.Sum, selector, cancellationToken);

    /// <summary>MAX。</summary>
    public Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
        AggregateAsync<TResult>(SqlFunctionNames.Max, selector, cancellationToken);

    /// <summary>MIN。</summary>
    public Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) =>
        AggregateAsync<TResult>(SqlFunctionNames.Min, selector, cancellationToken);

    /// <summary>AVG，结果为 double；没有行时为 null。</summary>
    public Task<double?> AverageAsync<TValue>(Expression<Func<T, TValue>> selector, CancellationToken cancellationToken = default) =>
        AggregateAsync<double?>(SqlFunctionNames.Avg, selector, cancellationToken);

    /// <summary>AVG，结果为 decimal；没有行时为 null。</summary>
    public Task<decimal?> AverageDecimalAsync<TValue>(Expression<Func<T, TValue>> selector, CancellationToken cancellationToken = default) =>
        AggregateAsync<decimal?>(SqlFunctionNames.Avg, selector, cancellationToken);

    private Query<T> Order(LambdaExpression key, bool descending) => new(Open with { OrderBys = Open.OrderBys.Add(new OrderSpec(key, descending)) });

    private Query<T, T2> Join<T2>(JoinKind kind, QuerySource source, LambdaExpression on) =>
        new(QueryStates.AddJoin(Open, source with { Join = kind, On = on }));
}

/// <summary><see cref="QueryState"/> 上与表个数无关的变换，供各查询类型共用。</summary>
internal static class QueryStates
{
    /// <summary>修改最近一个表来源。</summary>
    public static QueryState WithLast(QueryState state, Func<QuerySource, QuerySource> change) =>
        state with { Sources = state.Sources.SetItem(state.Sources.Length - 1, change(state.Sources[^1])) };

    /// <summary>追加一个连接的表来源。</summary>
    public static QueryState AddJoin(QueryState state, QuerySource source) => state with { Sources = state.Sources.Add(source) };
}