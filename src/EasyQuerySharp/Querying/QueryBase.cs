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

    /// <summary>追踪作用域里追踪结果实体（作用域里默认就追踪，对应 Java 的 asTracking）。</summary>
    public TSelf AsTracking() => With(State with { NoTracking = false });

    /// <summary>追踪作用域里也不追踪这条查询的结果（对应 Java 的 asNoTracking）。</summary>
    public TSelf AsNoTracking() => With(State with { NoTracking = true });

    /// <summary>这条查询不应用名为 <paramref name="name"/> 的拦截器。</summary>
    public TSelf DisableInterceptor(string name) => With(State with { DisabledInterceptors = State.DisabledInterceptors.Add(name) });

    /// <summary>这条查询不应用任何拦截器。</summary>
    public TSelf DisableInterceptors() => DisableInterceptor("*");

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

    /// <summary>是否有任何一行：<c>SELECT 1 … LIMIT 1</c>。</summary>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var frame = QueryCompiler.Build(ForAggregate() with { Limit = 1 });
        frame.Query.Columns.Add(new SelectColumn(LiteralExpr.One, null));
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
