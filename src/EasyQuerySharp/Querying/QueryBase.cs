using System.Linq.Expressions;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 各查询类型的公共部分：与表个数无关的修饰（分页、去重、关闭逻辑删除…）和全部终结方法。
/// <typeparamref name="TSelf"/> 是具体的查询类型（链式调用返回它），<typeparamref name="TElement"/> 是结果元素类型。
/// 查询对象不可变，每次调用返回新对象，可以放心复用（对应 Java 的 cloneQueryable）。
/// </summary>
public abstract partial class QueryBase<TSelf, TElement> : IQueryStateAccessor
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
    /// 默认只作用于这条查询自己的条件；<paramref name="includeNavigations"/> 为 true 时也作用于导航里的条件（<c>u.Cards.Any(c =&gt; …)</c>）
    /// 和条件里的子查询（对应 Java 的 NotNullOrEmptyValueFilter.DEFAULT_PROPAGATION_SUPPORTS）。
    /// </summary>
    public TSelf UseValueFilter(Filtering.ValueFilter filter, bool includeNavigations = false) =>
        With(State with { ValueFilter = filter, NavigationValueFilter = includeNavigations ? filter : Filtering.ValueFilter.All });

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

    /// <summary>
    /// 作为带出查询时（Include 的配置、<see cref="IAutoIncludeConfigure{TEntity}.Configure"/>）不用导航上声明的排序与条数
    /// （实体或 DTO 上 <c>[Navigate]</c> 的 OrderBy / Limit / Offset，对应 Java 的 ignoreNavigateConfigure）；自己写了排序、条数时本来就以自己的为准。
    /// </summary>
    public TSelf IgnoreNavigationDefaults() => With(State with { IgnoreNavigationDefaults = true });

    /// <summary>分页时不做深分页反向排序（对应 Java 的 configure(op -> op.setReverseOrder(false))），见 <see cref="EasyDbOptions.ReverseOffsetThreshold"/>。</summary>
    public TSelf DisableReversePaging() => With(State with { NoReversePaging = true });

    /// <summary>
    /// 这条查询（含其中的子查询）里所有集合导航上的聚合都合并成分组 LEFT JOIN，不用逐个 GroupJoin（对应 Java 的 ALL_SUB_QUERY_GROUP_JOIN）。
    /// 单个导航仍可用 GroupJoinIf(false, …) 关闭。
    /// </summary>
    public TSelf GroupJoinAll() => With(State with { GroupJoinAll = true });

    /// <summary>
    /// 投影之后再加的条件尽量推进派生表内层（对应 Java 的 SMART_PREDICATE）：只用到某张表直接投影出的列的合取项，
    /// 第一张表的写进内层 WHERE，连接表的写进它的 ON；其余留在外层。
    /// <code>db.Query&lt;Blog&gt;().SmartPredicate().Select(b =&gt; new BlogVo { Status = b.Status }).Where(v =&gt; v.Status == 0)
    /// // SELECT … FROM (SELECT t.status AS status FROM t_blog t WHERE t.status = ?) t1</code>
    /// </summary>
    public TSelf SmartPredicate() => With(State with { SmartPredicate = true });

    /// <summary>
    /// 分组合并时不把各聚合共有的条件提到派生表的 WHERE，每个聚合都写成 CASE WHEN（对应 Java 的 GROUP_JOIN_NOT_ALLOW_AUTO_MERGE）。
    /// </summary>
    public TSelf GroupJoinNoMerge() => With(State with { GroupJoinNoMerge = true });

    /// <summary>
    /// 行锁 <c>FOR UPDATE</c>（对应 Java 的 forUpdate）：锁住查到的行直到事务结束。只能在事务里、对单表查询调用一次；
    /// 锁只写在最外层，计数、Any、分页的语句同样带上。SQL Server、SQLite 不支持，渲染时抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    /// <example><code>
    /// await using var tx = db.BeginTransaction();
    /// var stock = await db.Query&lt;Stock&gt;().Where(s => s.Id == id).ForUpdate().FirstAsync();
    /// </code></example>
    public TSelf ForUpdate() => WithLock(RowLock.ForUpdate);

    /// <summary>与 <see cref="ForUpdate"/> 相同，但行已被别的事务锁住时立即报错而不等待（<c>FOR UPDATE NOWAIT</c>）。</summary>
    public TSelf ForUpdateNoWait() => WithLock(RowLock.ForUpdateNoWait);

    /// <summary>与 <see cref="ForUpdate"/> 相同，但跳过已被别的事务锁住的行（<c>FOR UPDATE SKIP LOCKED</c>），常用于多个消费者抢任务。</summary>
    public TSelf ForUpdateSkipLocked() => WithLock(RowLock.ForUpdateSkipLocked);

    // 三种行锁共用的检查（与 Java 一致）：在事务里、单表、只设置一次；隐式导航补出的 JOIN 在渲染时检查
    private TSelf WithLock(RowLock rowLock)
    {
        var name = typeof(TElement).Name;
        if (Execution.EasyTransaction.Current(Db) is null)
            throw new InvalidOperationException($"查询 {name} 调用 ForUpdate 前需要先开启事务：db.BeginTransaction()");
        if (State.Sources.Length > 1 || !State.GroupJoins.IsEmpty)
            throw new InvalidOperationException($"ForUpdate 只支持单表查询（single-table），查询 {name} 带有连接");
        if (State.Lock != RowLock.None)
            throw new InvalidOperationException($"查询 {name} 已经设置过行锁，不支持重复调用（repeated）");
        return With(State with { Lock = rowLock });
    }

    /// <summary>这条查询不应用名为 <paramref name="name"/> 的拦截器（对应 Java 的 noInterceptor(name)）。</summary>
    public TSelf DisableInterceptor(string name) => With(State with { Interceptors = State.Interceptors.Disable(name) });

    /// <summary>这条查询不应用任何拦截器（对应 Java 的 noInterceptor()）；之后再 EnableInterceptor 的除外。</summary>
    public TSelf DisableInterceptors() => With(State with { Interceptors = State.Interceptors.DisableAll() });

    /// <summary>这条查询应用名为 <paramref name="name"/> 的拦截器，包括默认关闭的（对应 Java 的 useInterceptor(name)）。</summary>
    public TSelf EnableInterceptor(string name) => With(State with { Interceptors = State.Interceptors.Enable(name) });

    /// <summary>恢复默认生效的全部拦截器（对应 Java 的 useInterceptor()）。</summary>
    public TSelf EnableInterceptors() => With(State with { Interceptors = State.Interceptors.EnableAll() });

    // ---------- 带出 ----------

    /// <summary>这条查询的带出每次最多按 <paramref name="size"/> 个父键查，超出时分成几条查询（对应 Java 的 configure(o -&gt; o.setGroupSize(n))）。</summary>
    public TSelf IncludeGroupSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        return With(State with { IncludeGroupSize = size });
    }

    /// <summary>
    /// 带一个参数给这条查询及其带出查询的配置读取（<see cref="ConfigureArgument"/>，对应 Java 的 configure(o -&gt; o.setConfigureArgument(arg))），
    /// 如嵌套 DTO 的 <see cref="IAutoIncludeConfigure{TEntity}.Configure"/> 按它加条件。
    /// </summary>
    public TSelf WithConfigureArgument(object? argument) => With(State with { ConfigureArgument = argument });

    /// <summary>通过 <see cref="WithConfigureArgument"/> 带进来的参数；没有时为 null。</summary>
    public object? ConfigureArgument => State.ConfigureArgument;

    // ---------- SQL ----------

    /// <summary>渲染成 SQL（参数用 <c>?</c> 占位），不执行。</summary>
    public string ToSql() => ToStatement().Text;

    /// <summary>渲染成带参数的语句，不执行。</summary>
    public SqlStatement ToStatement() => SqlRenderer.Render(Db.Options.Dialect, QueryCompiler.CompileSql(State));
}

/// <summary>不论具体是哪种查询类型，都能取到它的状态（子查询翻译时用）。</summary>
internal interface IQueryStateAccessor
{
    QueryState State { get; }
}
