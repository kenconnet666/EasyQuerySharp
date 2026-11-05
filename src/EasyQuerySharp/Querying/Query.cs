using System.Collections;
using System.Collections.Immutable;
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
public sealed partial class Query<T> : QueryBase<Query<T>, T>
{
    internal Query(QueryState state) : base(state)
    {
    }

    private protected override Query<T> With(QueryState state) => new(state);

    // 投影之后再加条件或排序：先把当前查询包成派生表
    private QueryState Open => State.HasSelector ? State.Wrap() : State;

    /// <summary>追加条件（多个 Where 之间是 AND）。</summary>
    public Query<T> Where(Expression<Func<T, bool>> predicate) => new(Open.AddWhere(predicate));

    /// <summary><paramref name="condition"/> 为 true 时才追加条件。</summary>
    public Query<T> WhereIf(bool condition, Expression<Func<T, bool>> predicate) => condition ? Where(predicate) : this;

    /// <summary>按主键等值（对应 Java 的 whereById）。</summary>
    public Query<T> WhereById(object id) => Where(Translation.KeyPredicates.ById<T>(Db.Metadata.Get<T>(), id));

    /// <summary>按主键 IN（对应 Java 的 whereByIds）。</summary>
    public Query<T> WhereByIds(IEnumerable ids) => Where(Translation.KeyPredicates.ByIds<T>(Db.Metadata.Get<T>(), ids));

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
    /// 带出导航（对应 Java 的 include）：主查询之后每个导航一条 <c>WHERE 键 IN (…)</c> 查询，结果挂到实体的导航属性上，没有 N+1。
    /// 多对多经中间表。<c>db.Query&lt;SysUser&gt;().Include(s =&gt; s.MyBlog).Include(s =&gt; s.Blogs)</c>
    /// </summary>
    public Query<T> Include<TProperty>(Expression<Func<T, TProperty>> navigation) => AddInclude(navigation, null);

    /// <summary>
    /// 带出集合导航，并配置子查询（条件、排序、嵌套 Include）：
    /// <c>.Include(c =&gt; c.SchoolStudents, q =&gt; q.Where(s =&gt; s.Name != "x").Include(s =&gt; s.SchoolStudentAddress))</c>
    /// </summary>
    public Query<T> Include<TChild>(Expression<Func<T, IEnumerable<TChild>?>> navigation, Func<Query<TChild>, Query<TChild>> configure) where TChild : class =>
        AddInclude(navigation, s => configure(new Query<TChild>(s)).State);

    /// <summary>带出对一导航，并配置子查询（如嵌套 Include）。</summary>
    public Query<T> Include<TChild>(Expression<Func<T, TChild?>> navigation, Func<Query<TChild>, Query<TChild>> configure) where TChild : class =>
        AddInclude(navigation, s => configure(new Query<TChild>(s)).State);

    private Query<T> AddInclude(LambdaExpression navigation, Func<QueryState, QueryState>? configure)
    {
        if (Translation.ExpressionTranslator.StripConvert(navigation.Body) is not MemberExpression { Expression: ParameterExpression } member)
            throw new ArgumentException($"Include 需要直接的导航属性，如 u => u.Posts：{navigation}", nameof(navigation));
        return new(State with { Includes = State.Includes.Add(new IncludeSpec(member.Member.Name, configure)) });
    }

    /// <summary>
    /// 先分页、再投影（对应 Java 的 limitSelect）：分页后的查询作为派生表，投影里的子查询（如导航计数）只对这一页的行执行。
    /// <c>q.LimitSelect(20, 10, u =&gt; new { u.Id, Blogs = u.Blogs!.Count() })</c>。
    /// </summary>
    public Query<TResult> LimitSelect<TResult>(long offset, long rows, Expression<Func<T, TResult>> selector)
    {
        var paged = Open with { Offset = offset, Limit = rows };
        return new(paged.Wrap() with { Selector = selector });
    }

    /// <summary>
    /// 分页后再投影（对应 Java 的 toPageSelectResult）：总数按投影前的查询计，当前页用 <see cref="LimitSelect{TResult}"/>，
    /// 投影里的子查询只对这一页执行。
    /// </summary>
    public async Task<Page<TResult>> ToPageAsync<TResult>(long page, long size, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var total = await CountAsync(cancellationToken).ConfigureAwait(false);
        if (PageWindow(page, size, total) is not var (paged, reverse)) return new Page<TResult>([], total, page, size);
        var rows = await new Query<TResult>(paged.Wrap() with { Selector = selector }).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (reverse) rows.Reverse();
        return new Page<TResult>(rows, total, page, size);
    }

    /// <summary>
    /// 与其它查询做 UNION（去重），结果作为派生表继续 Where / Select / 分页：
    /// <c>q1.Union(q2, q3).Where(t => t.Id == "1")</c>。各查询的投影列须一一对应。
    /// </summary>
    public Query<T> Union(params IEnumerable<Query<T>> others) => SetOperation(others, all: false);

    /// <summary>与其它查询做 UNION ALL（保留重复行），其余同 <see cref="Union"/>。</summary>
    public Query<T> UnionAll(params IEnumerable<Query<T>> others) => SetOperation(others, all: true);

    private Query<T> SetOperation(IEnumerable<Query<T>> others, bool all)
    {
        var parts = others.Select(o => o.State).Prepend(State).ToImmutableArray();
        if (parts.Length < 2) throw new ArgumentException("至少需要另一条查询参与集合运算", nameof(others));
        return new(new QueryState(Db, [new UnionSource(parts, all)]));
    }

    /// <summary>
    /// 按属性名自动投影到 DTO（对应 Java 的 select(Dto.class)）：DTO 的每个可写属性取实体上的同名列，
    /// DTO 属性上的 <c>[Column("…")]</c> 决定别名。
    /// </summary>
    public Query<TResult> Select<TResult>() where TResult : new() =>
        new(Open with { Selector = AutoProjection.Build(Db.Metadata.Get<T>(), typeof(TResult)) });

    /// <summary>
    /// 按 DTO 的结构自动投影并带出导航（对应 Java 的 selectAutoInclude）：同名属性取列；与实体导航同名的属性按导航映射——
    /// 集合是嵌套 DTO（或实体）的列表、对一是嵌套 DTO（或实体），都用带出查询按父键批量取（没有 N+1）；<c>[MapPath("A.B")]</c> 取导航路径上的值。
    /// 嵌套 DTO 实现 <see cref="IAutoIncludeConfigure{TEntity}"/> 时给它的带出查询加条件、排序、条数（根 DTO 实现时配置这条查询本身）；
    /// <c>[MapExpression]</c> 的属性取表达式的值。
    /// </summary>
    /// <example><code>db.Query&lt;SysUser&gt;().SelectAutoInclude&lt;UserDto&gt;()   // UserDto { Id, Name, List&lt;CardDto&gt; BankCards }</code></example>
    public Query<TResult> SelectAutoInclude<TResult>() where TResult : new()
    {
        // 根 DTO 也实现了 IAutoIncludeConfigure<T> 时，先按它配置这条查询（如合并分组、加条件）
        var configured = AutoProjection.ConfigureOf(typeof(TResult), typeof(T)) is { } configure ? configure(Open) : Open;
        return new(configured with { Selector = AutoProjection.BuildWithIncludes(Db.Metadata, Db.Metadata.Get<T>(), typeof(TResult)) });
    }

    /// <summary>
    /// 按属性名自动投影到 <typeparamref name="TResult"/>，但排除 <paramref name="excluded"/> 列出的属性（对应 Java 的 FETCHER.allFieldsExclude）。
    /// </summary>
    /// <example><code>.SelectExcept&lt;Blog&gt;(b => new { b.Id, b.Content })</code></example>
    public Query<TResult> SelectExcept<TResult>(Expression<Func<T, object?>> excluded) where TResult : new() =>
        new(Open with { Selector = AutoProjection.Build(Db.Metadata.Get<T>(), typeof(TResult), Translation.ExpressionTranslator.MemberNames(excluded).ToHashSet()) });

    /// <summary>
    /// 用原生 SQL 作为 SELECT 列表（对应 Java 的 select(String)），结果按列名映射回 <typeparamref name="T"/>。
    /// </summary>
    /// <example><code>db.Query&lt;Blog&gt;().WhereById("1").SelectRaw("1 as id")</code></example>
    public Query<T> SelectRaw(string selectSql) => new(Open with { RawSelect = selectSql });

    /// <summary>给最近一个表来源指定别名（对应 Java 的 asAlias）。</summary>
    public Query<T> AsAlias(string alias) => new(QueryStates.WithLast(State, s => s with { Alias = alias }));

    /// <summary>最近一个表来源不加逻辑删除条件（对应 Java 的 tableLogicDelete(() -> false)），其它表照常。</summary>
    public Query<T> DisableTableLogicDelete() => new(QueryStates.WithLast(State, s => s with { NoLogicDelete = true }));

    /// <summary>这条查询里把最近一个实体表换成 <paramref name="tableName"/>（对应 Java 的 asTable）。</summary>
    public Query<T> AsTable(string tableName) => new(QueryStates.WithLast(State, s => s is EntitySource e ? e with { TableName = tableName } : s));

    /// <summary>按当前表名算出新表名（对应 Java 的 asTable(Function)），如分表：<c>.AsTable(name => name + "_202401")</c>。</summary>
    public Query<T> AsTable(Func<string, string> rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        return new(QueryStates.WithLast(State, s => s is EntitySource e
            ? e with { TableName = rename(e.TableName ?? Db.Metadata.Get(e.EntityType).TableName) }
            : s));
    }

    /// <summary>
    /// 自定义最近一个实体表的整个引用写法（对应 Java 的 asTableSegment）：参数是带引号的表名和别名，
    /// 如 ClickHouse 的 <c>.AsTableSegment((table, alias) =&gt; $"{table} {alias} FINAL")</c>。
    /// </summary>
    public Query<T> AsTableSegment(Func<string, string?, string> segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return new(QueryStates.WithLast(State, s => s is EntitySource e ? e with { TableSegment = segment } : s));
    }

    /// <summary>
    /// 把这条查询作为公共表表达式（对应 Java 的 toCteAs）：之后的查询从 <c>`名字`</c> 读，定义写在整条语句前面的
    /// <c>WITH `名字` AS (…)</c> 里。可以继续 Where / Select，也可以作为别的查询的 JOIN 来源；名字默认是 <c>with_类型名</c>（泛型去掉反引号，如 <c>with_Draft2</c>，与 Java 一致）。
    /// </summary>
    public Query<T> AsCte(string? name = null) =>
        new(new QueryState(Db, [new CteSource(name ?? "with_" + typeof(T).Name.Replace("`", "", StringComparison.Ordinal), State)])
        {
            // 外层沿用这条查询的值过滤与拦截器选择（Java 的外层从它的上下文扩展而来）
            ValueFilter = State.ValueFilter, Interceptors = State.Interceptors,
        });

    /// <summary>LEFT JOIN 实体 <typeparamref name="T2"/>。</summary>
    public Query<T, T2> LeftJoin<T2>(Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Left, new EntitySource(typeof(T2)), on);

    /// <summary>INNER JOIN 实体 <typeparamref name="T2"/>。</summary>
    public Query<T, T2> InnerJoin<T2>(Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Inner, new EntitySource(typeof(T2)), on);

    /// <summary>RIGHT JOIN 实体 <typeparamref name="T2"/>。</summary>
    public Query<T, T2> RightJoin<T2>(Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Right, new EntitySource(typeof(T2)), on);

    /// <summary>LEFT JOIN 一个子查询（作为派生表）。</summary>
    public Query<T, T2> LeftJoin<T2>(Query<T2> subQuery, Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Left, subQuery.State.AsJoinSource(), on);

    /// <summary>INNER JOIN 一个子查询（作为派生表）。</summary>
    public Query<T, T2> InnerJoin<T2>(Query<T2> subQuery, Expression<Func<T, T2, bool>> on) => Join<T2>(JoinKind.Inner, subQuery.State.AsJoinSource(), on);

    /// <summary>按主键查一行（主键至多一行，不加 LIMIT）；没有时返回 null（对应 Java 的 findOrNull）。</summary>
    public Task<T?> FindAsync(object id, CancellationToken cancellationToken = default) => WhereById(id).SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 按主键查一行；没有时抛出 <see cref="NotFoundException"/>（对应 Java 的 findNotNull），
    /// 消息为 <paramref name="message"/>，不给时取实体配置的默认消息。
    /// </summary>
    public Task<T> FindOrThrowAsync(object id, string? message = null, CancellationToken cancellationToken = default) =>
        message is null ? WhereById(id).SingleAsync(cancellationToken) : WhereById(id).SingleAsync(message, cancellationToken);

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

    /// <summary>
    /// 追加一个自带方向的原生排序项，片段里可以引用列（对应 Java 的 orderBy(x -&gt; x.expression().sql("{0} ASC", …))）：
    /// <c>.OrderByRaw(t =&gt; Sql.Raw&lt;object&gt;("{0} DESC", t.CreateTime))</c>。
    /// </summary>
    public Query<T> OrderByRaw(Expression<Func<T, object?>> raw) => new(Open with { OrderBys = Open.OrderBys.Add(new OrderSpec(raw, false, Raw: true)) });

    /// <summary>再加一个表来源，逗号连接 <c>FROM a t , b t1</c>（对应 Java 的 from(T.class)），关联条件写在 Where 里。</summary>
    public Query<T, T2> From<T2>() => new(QueryStates.AddJoin(Open, new EntitySource(typeof(T2)) { Join = JoinKind.Comma }));

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
