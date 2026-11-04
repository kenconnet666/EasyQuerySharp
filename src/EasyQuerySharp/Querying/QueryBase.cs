using System.Linq.Expressions;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 各查询类型的公共部分：与表个数无关的修饰（分页、去重、关闭逻辑删除…）和全部终结方法。
/// <typeparamref name="TSelf"/> 是具体的查询类型（链式调用返回它），<typeparamref name="TElement"/> 是结果元素类型。
/// 查询对象不可变，每次调用返回新对象，可以放心复用（对应 Java 的 cloneQueryable）。
/// </summary>
public abstract class QueryBase<TSelf, TElement> : IQueryStateAccessor
    where TSelf : QueryBase<TSelf, TElement>
{
    private protected QueryBase(QueryState state) => State = state;

    internal QueryState State { get; }

    QueryState IQueryStateAccessor.State => State;

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db => State.Db;

    private protected abstract TSelf With(QueryState state);

    // ---------- 修饰 ----------

    /// <summary>跳过 <paramref name="count"/> 行（OFFSET）。</summary>
    public TSelf Skip(long count) => With(State with { Offset = count });

    /// <summary>最多取 <paramref name="count"/> 行（LIMIT）。</summary>
    public TSelf Take(long count) => With(State with { Limit = count });

    /// <summary>跳过 <paramref name="offset"/> 行后取 <paramref name="rows"/> 行（对应 Java 的 limit(offset, rows)）。</summary>
    public TSelf Limit(long offset, long rows) => With(State with { Offset = offset, Limit = rows });

    /// <summary>DISTINCT。</summary>
    public TSelf Distinct() => With(State with { Distinct = true });

    /// <summary>这条查询不加逻辑删除条件。</summary>
    public TSelf DisableLogicDelete() => With(State with { DisableLogicDelete = true });

    /// <summary>
    /// 按条件对象追加条件（对应 Java 的 whereObject）：<paramref name="request"/> 上标了 <see cref="Filtering.FilterAttribute"/> 的属性
    /// 有值时变成条件；为 null 时不变。投影之后调用时作用于投影结果（先包成派生表）。
    /// </summary>
    /// <example><code>db.Query&lt;Blog&gt;().FilterBy(new BlogQuery { Title = "abc", Statuses = [1, 2] })</code></example>
    public TSelf FilterBy(object? request)
    {
        if (request is null) return (TSelf)this;
        var state = Opened();
        return Filtering.ObjectFilter.Where(request, SourceTypes(state)) is { } predicate
            ? With(state with { Wheres = state.Wheres.Add(predicate) })
            : (TSelf)this;
    }

    /// <summary>
    /// 之后追加的 Where 按 <paramref name="filter"/> 跳过值为 null / 空白的比较条件（对应 Java 的 filterConfigure），
    /// 再次调用可以切换，<see cref="Filtering.ValueFilter.All"/> 恢复不过滤。
    /// </summary>
    public TSelf UseValueFilter(Filtering.ValueFilter filter) => With(State with { ValueFilter = filter });

    /// <summary><paramref name="condition"/> 为 true 时才 <see cref="FilterBy"/>（对应 Java 的 whereObject(bool, obj)）。</summary>
    public TSelf FilterByIf(bool condition, object? request) => condition ? FilterBy(request) : (TSelf)this;

    /// <summary>按排序请求追加排序（对应 Java 的 orderByObject）；为 null 时不变。</summary>
    public TSelf SortBy(Filtering.ISortRequest? request)
    {
        if (request is null) return (TSelf)this;
        var state = Opened();
        var orders = Filtering.ObjectFilter.Sort(request, SourceTypes(state)).Select(o => new OrderSpec(o.Key, o.Descending));
        return With(state with { OrderBys = state.OrderBys.AddRange(orders) });
    }

    /// <summary><paramref name="condition"/> 为 true 时才 <see cref="SortBy"/>。</summary>
    public TSelf SortByIf(bool condition, Filtering.ISortRequest? request) => condition ? SortBy(request) : (TSelf)this;

    /// <summary>
    /// 追加一个原生排序片段，原样写进 ORDER BY（自带方向，对应 Java 的 orderBy 里的 sql(…)）：<c>.OrderByRaw("user_name ASC")</c>。
    /// 片段由调用方拼写，不要把用户输入直接拼进去。
    /// </summary>
    public TSelf OrderByRaw(string sql)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        var state = Opened();
        var body = Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Raw))!.MakeGenericMethod(typeof(object)),
            Expression.Constant(sql), Expression.NewArrayInit(typeof(object)));
        var parameters = SourceTypes(state).Select((t, i) => Expression.Parameter(t, "t" + i));
        return With(state with { OrderBys = state.OrderBys.Add(new OrderSpec(Expression.Lambda(body, parameters), false, Raw: true)) });
    }

    // 投影之后再加条件 / 排序要先包一层
    private QueryState Opened() => State.HasSelector ? State.Wrap() : State;

    private static Type[] SourceTypes(QueryState state) => state.Sources.Select(QueryState.TypeOf).ToArray();

    /// <summary>追踪作用域里追踪结果实体（作用域里默认就追踪，对应 Java 的 asTracking）。</summary>
    public TSelf AsTracking() => With(State with { NoTracking = false });

    /// <summary>追踪作用域里也不追踪这条查询的结果（对应 Java 的 asNoTracking）。</summary>
    public TSelf AsNoTracking() => With(State with { NoTracking = true });

    /// <summary>这条查询不应用名为 <paramref name="name"/> 的拦截器（对应 Java 的 noInterceptor(name)）。</summary>
    public TSelf DisableInterceptor(string name) => With(State with { Interceptors = State.Interceptors.Disable(name) });

    /// <summary>这条查询不应用任何拦截器（对应 Java 的 noInterceptor()）；之后再 EnableInterceptor 的除外。</summary>
    public TSelf DisableInterceptors() => With(State with { Interceptors = State.Interceptors.DisableAll() });

    /// <summary>这条查询应用名为 <paramref name="name"/> 的拦截器，包括默认关闭的（对应 Java 的 useInterceptor(name)）。</summary>
    public TSelf EnableInterceptor(string name) => With(State with { Interceptors = State.Interceptors.Enable(name) });

    /// <summary>恢复默认生效的全部拦截器（对应 Java 的 useInterceptor()）。</summary>
    public TSelf EnableInterceptors() => With(State with { Interceptors = State.Interceptors.EnableAll() });

    // ---------- SQL ----------

    /// <summary>渲染成 SQL（参数用 <c>?</c> 占位），不执行。</summary>
    public string ToSql() => ToStatement().Text;

    /// <summary>渲染成带参数的语句，不执行。</summary>
    public SqlStatement ToStatement() => SqlRenderer.Render(Db.Options.Dialect, QueryCompiler.Compile<TElement>(State).Sql);

    // ---------- 终结方法 ----------

    /// <summary>执行并返回全部结果。</summary>
    public async Task<List<TElement>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var compiled = QueryCompiler.Compile<TElement>(State);
        var statement = SqlRenderer.Render(Db.Options.Dialect, compiled.Sql);
        var rows = await Db.Executor.QueryAsync(statement, compiled.Shaper, cancellationToken).ConfigureAwait(false);
        // 投影里的导航带出：每个导航一次 IN 查询，挂回刚读出的行
        await Includes.IncludeRunner.RunAsync(Db, compiled.Includes, cancellationToken).ConfigureAwait(false);
        Track(rows);
        return rows;
    }

    // 追踪作用域里，结果是有主键的实体时逐行追踪；同一主键已追踪的，以已追踪的实例替换（与 Java 一致）
    private void Track(List<TElement> rows)
    {
        if (State.NoTracking || !Db.Tracker.Active || !Db.Tracker.CanTrack(typeof(TElement))) return;
        for (var i = 0; i < rows.Count; i++)
            if (rows[i] is { } row) rows[i] = (TElement)Db.Tracker.AttachQueried(row);
    }

    /// <summary>
    /// 流式读取（对应 Java 的 toStreamResult）：边读边返回，适合大结果集，连接在枚举结束时释放。
    /// 投影里的导航带出（include）不适用于流式读取；追踪作用域里照常追踪。
    /// </summary>
    /// <example><code>await foreach (var blog in db.Query&lt;Blog&gt;().OrderBy(b => b.Id).ToAsyncEnumerable(ct)) { … }</code></example>
    public async IAsyncEnumerable<TElement> ToAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var compiled = QueryCompiler.Compile<TElement>(State);
        if (compiled.Includes.Count > 0) throw new NotSupportedException("投影里的导航带出不能流式读取，请用 ToListAsync");
        var statement = SqlRenderer.Render(Db.Options.Dialect, compiled.Sql);
        var track = !State.NoTracking && Db.Tracker.Active && Db.Tracker.CanTrack(typeof(TElement));
        await foreach (var row in Db.Executor.StreamAsync(statement, compiled.Shaper, cancellationToken).ConfigureAwait(false))
            yield return track && row is { } r ? (TElement)Db.Tracker.AttachQueried(r) : row;
    }

    /// <summary>执行并把每行读成 列名 → 值 的字典（对应 Java 的 toMaps）；列名忽略大小写，重复的列名抛出异常。</summary>
    public Task<List<Dictionary<string, object?>>> ToDictionariesAsync(CancellationToken cancellationToken = default) =>
        Db.Executor.QueryAsync(ToStatement(), Materialization.EntityMaterializer.ReadDictionary, cancellationToken);

    /// <summary>第一行读成字典（追加 LIMIT 1）；没有时返回 null（对应 Java 的 toMap）。</summary>
    public async Task<Dictionary<string, object?>?> FirstDictionaryOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await With(State with { Limit = 1 }).ToDictionariesAsync(cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? null : list[0];
    }

    /// <summary>第一行（追加 LIMIT 1）；没有时返回 null（对应 Java 的 firstOrNull）。</summary>
    public async Task<TElement?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await With(State with { Limit = 1 }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? default : list[0];
    }

    /// <summary>第一行；没有时抛出 <see cref="InvalidOperationException"/>（对应 Java 的 firstNotNull）。</summary>
    public async Task<TElement> FirstAsync(CancellationToken cancellationToken = default) =>
        await FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"没有查到 {typeof(TElement).Name}");

    /// <summary>唯一一行（不加 LIMIT）；没有时返回 null，多于一行时抛出异常（对应 Java 的 singleOrNull）。</summary>
    public async Task<TElement?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.Count switch
        {
            0 => default,
            1 => list[0],
            _ => throw new InvalidOperationException($"期望最多一行 {typeof(TElement).Name}，实际 {list.Count} 行"),
        };
    }

    /// <summary>唯一一行；没有或多于一行时抛出异常（对应 Java 的 singleNotNull）。</summary>
    public async Task<TElement> SingleAsync(CancellationToken cancellationToken = default) =>
        await SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"没有查到 {typeof(TElement).Name}");

    /// <summary>是否有任何一行：<c>SELECT  1  … LIMIT 1</c>（与 Java 的 any 逐字一致，1 两侧各有一个空格）。</summary>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var frame = QueryCompiler.Build(ForAggregate() with { Limit = 1 });
        frame.Query.Columns.Add(new SelectColumn(new LiteralExpr(" 1 "), null));
        var statement = SqlRenderer.Render(Db.Options.Dialect, frame.Query);
        var rows = await Db.Executor.QueryAsync(statement, _ => true, cancellationToken).ConfigureAwait(false);
        return rows.Count > 0;
    }

    /// <summary>行数：<c>SELECT COUNT(*)</c>；有 DISTINCT 或分页时先包成派生表再计数。</summary>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default) =>
        await ScalarAsync<long>(CountStatement(), cancellationToken).ConfigureAwait(false);

    /// <summary>计数语句（分页时与数据查询共用条件）。</summary>
    internal SelectQuery CountQuery()
    {
        var state = State.Distinct || State.Offset is not null || State.Limit is not null ? State.Wrap() : ForAggregate();
        var frame = QueryCompiler.Build(state with { OrderBys = [] });
        frame.Query.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]), null));
        return frame.Query;
    }

    private SqlStatement CountStatement() => SqlRenderer.Render(Db.Options.Dialect, CountQuery());

    /// <summary>分页：先计数，有数据时再取第 <paramref name="page"/> 页（从 1 开始）。</summary>
    public async Task<Page<TElement>> ToPageAsync(long page, long size, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var total = await CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = total == 0 || (page - 1) * size >= total
            ? []
            : await Limit((page - 1) * size, size).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new Page<TElement>(rows, total, page, size);
    }

    // 聚合时不需要用户的投影；去重、分页、分组后的投影先包一层再聚合
    private protected QueryState ForAggregate() => State.HasSelector && (State.Distinct || State.Offset is not null || State.Limit is not null || State.GroupKey is not null)
        ? State.Wrap()
        : State with { Selector = null };

    /// <summary>执行一个只取一个值的聚合：<c>SELECT FUNC(expr) …</c>。</summary>
    private protected async Task<TResult> AggregateAsync<TResult>(string function, LambdaExpression selector, CancellationToken ct)
    {
        var frame = QueryCompiler.Build(ForAggregate() with { OrderBys = [] });
        frame.Translator.Bind(selector, frame.Bindings);
        var value = frame.Translator.Translate(selector.Body);
        frame.Query.Columns.Add(new SelectColumn(new FunctionExpr(function, [value]), null));
        frame.Query.ExplicitProjection = true;
        var statement = SqlRenderer.Render(Db.Options.Dialect, frame.Query);
        return await ScalarAsync<TResult>(statement, ct).ConfigureAwait(false);
    }

    private async Task<TResult> ScalarAsync<TResult>(SqlStatement statement, CancellationToken ct)
    {
        var raw = await Db.Executor.ScalarAsync(statement, ct).ConfigureAwait(false);
        // 聚合没有行时数据库返回 NULL：结果类型可空就是 null，否则是 default
        return raw is null ? default! : (TResult)Materialization.ValueReader.ConvertTo(raw, typeof(TResult))!;
    }
}

/// <summary>分页结果。</summary>
/// <param name="Rows">当前页的行。</param>
/// <param name="Total">总行数。</param>
/// <param name="PageIndex">页码，从 1 开始。</param>
/// <param name="PageSize">每页行数。</param>
public sealed record Page<T>(IReadOnlyList<T> Rows, long Total, long PageIndex, long PageSize)
{
    /// <summary>总页数。</summary>
    public long PageCount => Total == 0 ? 0 : (Total + PageSize - 1) / PageSize;
}

/// <summary>不论具体是哪种查询类型，都能取到它的状态（子查询翻译时用）。</summary>
internal interface IQueryStateAccessor
{
    QueryState State { get; }
}
