using System.Linq.Expressions;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Querying;

// 终结方法：执行查询并返回结果
public abstract partial class QueryBase<TSelf, TElement>
{
    /// <summary>执行并返回全部结果。</summary>
    public async Task<List<TElement>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var compiled = QueryCompiler.Compile<TElement>(State);
        var statement = SqlRenderer.Render(Db.Options.Dialect, compiled.Sql);
        var shaper = compiled.Shaper;
        if (State.ResultSizeLimit is { } limit)
        {
            var count = 0;
            var inner = shaper;
            shaper = reader => ++count > limit ? throw new ResultSizeLimitException(limit) : inner(reader);
        }
        var rows = await Db.Executor.QueryAsync(statement, shaper, cancellationToken).ConfigureAwait(false);
        // 每行的回调在带出之前执行（与 Java 的 forEach 一致：回调抛异常时带出查询不再执行）
        foreach (var row in rows)
            foreach (var callback in State.RowCallbacks) callback(row);
        // 投影里的导航带出：每个导航一次 IN 查询，挂回刚读出的行
        await IncludeRunner.RunAsync(Db, compiled.Includes, IncludeContext.Of(State), cancellationToken).ConfigureAwait(false);
        foreach (var fill in State.Fills) await fill(rows, cancellationToken).ConfigureAwait(false);
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
    /// <example><code>await foreach (var blog in db.Query&lt;Blog&gt;().OrderBy(b => b.Id).AsAsyncEnumerable(ct)) { … }</code></example>
    public async IAsyncEnumerable<TElement> AsAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var compiled = QueryCompiler.Compile<TElement>(State);
        if (compiled.Includes.Count > 0) throw new NotSupportedException("投影里的导航带出不能流式读取，请用 ToListAsync");
        var statement = SqlRenderer.Render(Db.Options.Dialect, compiled.Sql);
        var track = !State.NoTracking && Db.Tracker.Active && Db.Tracker.CanTrack(typeof(TElement));
        await foreach (var row in Db.Executor.StreamAsync(statement, compiled.Shaper, cancellationToken).ConfigureAwait(false))
            yield return track && row is { } r ? (TElement)Db.Tracker.AttachQueried(r) : row;
    }

    /// <summary>
    /// 分块读取（对应 Java 的 toChunk / toChunkIf）：每次按 LIMIT / OFFSET 取 <paramref name="size"/> 行，取到不足一块为止。
    /// 需要稳定的排序（否则块之间可能重复或遗漏）；想提前结束直接跳出 <c>await foreach</c>。
    /// </summary>
    /// <example><code>await foreach (var blogs in db.Query&lt;Blog&gt;().OrderBy(b => b.Id).ToChunksAsync(100)) { … }</code></example>
    public async IAsyncEnumerable<List<TElement>> ToChunksAsync(int size, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        for (long offset = 0; ; offset += size)
        {
            var chunk = await Skip(offset).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (chunk.Count > 0) yield return chunk;
            if (chunk.Count < size) yield break;
        }
    }

    /// <summary>
    /// 结果超过 <paramref name="rows"/> 行时抛出 <see cref="ResultSizeLimitException"/>（对应 Java 的 setResultSizeLimit），
    /// 用来防止误查出整张大表；读到第 rows + 1 行时立即停止。
    /// </summary>
    public TSelf MaxResultSize(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 0);
        return With(State with { ResultSizeLimit = rows });
    }

    /// <summary>
    /// 执行查询，自己读结果集（对应 Java 的 toResultSet）：<paramref name="read"/> 拿到的是这条查询的 <see cref="System.Data.Common.DbDataReader"/>，
    /// 列按投影的顺序；读完即关闭。导航带出、追踪都不适用。
    /// </summary>
    /// <example><code>var ids = await db.Query&lt;User&gt;().ReadAsync(async (r, ct) =&gt; { var list = new List&lt;string&gt;(); while (await r.ReadAsync(ct)) list.Add(r.GetString(0)); return list; });</code></example>
    public Task<TResult> ReadAsync<TResult>(Func<System.Data.Common.DbDataReader, CancellationToken, Task<TResult>> read, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        var statement = ToStatement();
        return Db.Executor.SessionAsync(s => s.ReadAsync(statement, read, cancellationToken), cancellationToken);
    }

    /// <summary>执行并把每行读成 列名 → 值 的 <see cref="DynamicRow"/>（对应 Java 的 toMaps）；列名忽略大小写，重复的列名抛出异常。</summary>
    public Task<List<DynamicRow>> ToRowsAsync(CancellationToken cancellationToken = default) =>
        Db.Executor.QueryAsync(ToStatement(), Materialization.EntityMaterializer.ReadDictionary, cancellationToken);

    /// <summary>第一行读成 <see cref="DynamicRow"/>（追加 LIMIT 1）；没有时返回 null（对应 Java 的 toMap）。</summary>
    public async Task<DynamicRow?> FirstRowOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await With(State with { Limit = 1 }).ToRowsAsync(cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? null : list[0];
    }

    /// <summary>第一行（追加 LIMIT 1）；没有时返回 null（对应 Java 的 firstOrNull）。</summary>
    public async Task<TElement?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await With(State with { Limit = 1 }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? default : list[0];
    }

    /// <summary>第一行；没有时抛出 <see cref="NotFoundException"/>，消息取根实体配置的默认消息（对应 Java 的 firstNotNull）。</summary>
    public Task<TElement> FirstAsync(CancellationToken cancellationToken = default) => FirstAsync(NotFound, cancellationToken);

    /// <summary>第一行；没有时抛出带 <paramref name="message"/> 的 <see cref="NotFoundException"/>。</summary>
    public Task<TElement> FirstAsync(string message, CancellationToken cancellationToken = default) =>
        FirstAsync(() => new NotFoundException(message), cancellationToken);

    /// <summary>第一行；没有时抛出 <paramref name="error"/> 创建的异常。</summary>
    public async Task<TElement> FirstAsync(Func<Exception> error, CancellationToken cancellationToken = default)
    {
        var list = await With(State with { Limit = 1 }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.Count > 0 ? list[0] : throw error();
    }

    /// <summary>唯一一行（不加 LIMIT）；没有时返回 null，多于一行时抛出 <see cref="InvalidOperationException"/>（对应 Java 的 singleOrNull）。</summary>
    public async Task<TElement?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var list = await ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? default : Single(list);
    }

    /// <summary>唯一一行；没有时抛出 <see cref="NotFoundException"/>，多于一行时抛出 <see cref="InvalidOperationException"/>（对应 Java 的 singleNotNull）。</summary>
    public Task<TElement> SingleAsync(CancellationToken cancellationToken = default) => SingleAsync(NotFound, cancellationToken);

    /// <summary>唯一一行；没有时抛出带 <paramref name="message"/> 的 <see cref="NotFoundException"/>。</summary>
    public Task<TElement> SingleAsync(string message, CancellationToken cancellationToken = default) =>
        SingleAsync(() => new NotFoundException(message), cancellationToken);

    /// <summary>唯一一行；没有时抛出 <paramref name="error"/> 创建的异常，多于一行时抛出 <see cref="InvalidOperationException"/>。</summary>
    public async Task<TElement> SingleAsync(Func<Exception> error, CancellationToken cancellationToken = default)
    {
        var list = await ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? throw error() : Single(list);
    }

    private static TElement Single(List<TElement> list) => list.Count == 1
        ? list[0]
        : throw new InvalidOperationException($"期望最多一行 {typeof(TElement).Name}，实际 {list.Count} 行");

    /// <summary>
    /// 要求至少有一行（对应 Java 的 required）：按 <see cref="AnyAsync"/> 查询，没有时抛出 <see cref="NotFoundException"/>，
    /// 消息为 <paramref name="message"/>，不给时取根实体配置的默认消息。
    /// </summary>
    public Task EnsureAnyAsync(string? message = null, CancellationToken cancellationToken = default) =>
        EnsureAnyAsync(message is null ? NotFound : () => new NotFoundException(message), cancellationToken);

    /// <summary>要求至少有一行；没有时抛出 <paramref name="error"/> 创建的异常。</summary>
    public async Task EnsureAnyAsync(Func<Exception> error, CancellationToken cancellationToken = default)
    {
        if (!await AnyAsync(cancellationToken).ConfigureAwait(false)) throw error();
    }

    /// <summary>查不到行时的默认异常：消息取 FROM 实体上 <see cref="Metadata.TableAttribute.NotFoundMessage"/> 的配置。</summary>
    private protected NotFoundException NotFound() =>
        new((State.Sources[0] is EntitySource source ? Db.Metadata.Get(source.EntityType).NotFoundMessage : null)
            ?? $"没有查到 {typeof(TElement).Name}");

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
        await ScalarAsync<long>(ToCountStatement(), cancellationToken).ConfigureAwait(false);

    /// <summary>计数语句（分页时与数据查询共用条件）。</summary>
    internal SelectQuery CountQuery()
    {
        // 去重且投影只有一列：COUNT(DISTINCT 列)，不必包子查询（与 Java 一致）
        if (State is { Distinct: true, HasSelector: true, Offset: null, Limit: null, GroupKey: null })
        {
            var (select, _) = QueryCompiler.CompileSelect(State with { OrderBys = [], Distinct = false });
            if (select.Columns.Count == 1)
            {
                var column = select.Columns[0].Expr;
                select.Columns.Clear();
                select.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.CountDistinct, [column]), null));
                return select;
            }
        }
        var state = Aggregated();
        var frame = QueryCompiler.Build(state with { OrderBys = [] });
        // 排序、投影里的导航在 Java 里已经建了表（隐式 JOIN、分组合并）：计数去掉排序与投影后主表仍带别名 t。
        // 是否建表要看完整查询翻译出的 JOIN，只在确实去掉了东西时多翻译一次
        var dropped = !State.OrderBys.IsEmpty || (state.Selector is null && State.HasSelector);
        if (dropped && !frame.Query.ExplicitProjection)
            frame.Query.ExplicitProjection = QueryCompiler.CompileSelect(State).Query.Joins.Count > frame.Query.Joins.Count;
        frame.Query.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]), null));
        return frame.Query;
    }

    /// <summary>计数时执行的语句（<c>SELECT COUNT(*) …</c>），不执行；用于调试或自己拼分页。</summary>
    public SqlStatement ToCountStatement() => SqlRenderer.Render(Db.Options.Dialect, CountQuery());

    /// <summary>
    /// 分页：先计数，有数据时再取第 <paramref name="page"/> 页（从 1 开始）。最后一页只取剩下的条数；
    /// 偏移量过半时按 <see cref="EasyDbOptions.ReverseOffsetThreshold"/> 反向排序取数（与 Java 一致）。
    /// </summary>
    public async Task<Page<TElement>> ToPageAsync(long page, long size, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        return await PageOfAsync(page, size, await CountAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按列表请求分页（对应 TS 版的 table(request)，见 <see cref="Filtering.TableRequest"/>）：先加上请求里的表头筛选（<c>filter.列</c> IN、<c>search.列</c> 包含）
    /// 与排序（排在查询自己的排序之前），再取第 <see cref="Filtering.TableRequest.Page"/> 页；请求带了总数时不再计数。
    /// 键是结果里的成员名（忽略大小写），请求碰不到结果以外的列；查询栏的条件在 <see cref="Filtering.TableRequest.Params"/> 里，由调用方自己写成 Where。
    /// </summary>
    /// <example><code>
    /// var request = TableRequest.Parse(httpContext.Request.QueryString.Value);
    /// var page = await db.Query&lt;Order&gt;().Select(o =&gt; new OrderRow { Id = o.Id, Status = o.Status, Supplier = o.Supplier!.Name }).ToPageAsync(request);
    /// </code></example>
    public async Task<Page<TElement>> ToPageAsync(Filtering.TableRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        var query = With(Filtering.TableFilter.Apply(State, request));
        var total = request.Total ?? await query.CountAsync(cancellationToken).ConfigureAwait(false);
        return await query.PageOfAsync(request.Page, request.PageSize, total, cancellationToken).ConfigureAwait(false);
    }

    // 已知总数时取第 page 页
    private async Task<Page<TElement>> PageOfAsync(long page, long size, long total, CancellationToken cancellationToken)
    {
        if (PageWindow(page, size, total) is not var (state, reverse)) return new Page<TElement>([], total, page, size);
        var rows = await With(state).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (reverse) rows.Reverse();
        return new Page<TElement>(rows, total, page, size);
    }

    /// <summary>
    /// 按偏移分批读取（对应 Java 的 offsetChunk）：每批最多 <paramref name="size"/> 行交给 <paramref name="handle"/>，
    /// 它返回下一批的偏移要前进多少（处理完就删掉的场景返回 0）；某批不满 <paramref name="size"/> 行、或累计读到
    /// <paramref name="maxFetchSize"/> 行时结束（防止返回 0 时死循环）。查询需要有确定的排序。
    /// </summary>
    public async Task OffsetChunkAsync(int size, Func<IReadOnlyList<TElement>, long> handle, long maxFetchSize = 100_000, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentNullException.ThrowIfNull(handle);
        long offset = 0, fetched = 0;
        while (fetched < maxFetchSize)
        {
            var rows = await Skip(offset).Take(Math.Min(size, maxFetchSize - fetched)).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (rows.Count == 0) break;
            fetched += rows.Count;
            offset += handle(rows);
            if (rows.Count < size) break;
        }
    }

    /// <summary>
    /// 取第 <paramref name="page"/> 页要执行的查询：超出总数时为 null；剩余不足一页时只取剩下的；
    /// 偏移量不小于阈值且超过总数一半、排序可以整体反转时，反转排序从另一头取（结果要在内存里倒序）。
    /// </summary>
    private protected (QueryState State, bool Reverse)? PageWindow(long page, long size, long total)
    {
        var offset = (page - 1) * size;
        if (total <= offset) return null;
        var take = Math.Min(total - offset, size);
        var threshold = Db.Options.ReverseOffsetThreshold;
        var reverse = threshold > 0 && offset >= threshold && offset > total / 2 && !State.NoReversePaging
                      && !State.OrderBys.IsEmpty && State.OrderBys.All(Reversible)
                      && !Translation.ExpressionTranslator.InheritsQueryOrder(State.Selector?.Body);
        return reverse
            ? (State with { OrderBys = [.. State.OrderBys.Select(o => o with { Descending = !o.Descending })], Offset = total - offset - take, Limit = take }, true)
            : (State with { Offset = offset, Limit = take }, false);

        // 原生片段与指定了 NULL 位置的排序不能简单反转
        static bool Reversible(OrderSpec order) => !order.Raw
            && Translation.ExpressionTranslator.StripConvert(order.Key.Body) is not MethodCallExpression { Method.Name: nameof(Sql.NullsFirst) or nameof(Sql.NullsLast) };
    }

    // 聚合时不需要用户的投影；去重、分页、分组后的投影先包一层再聚合
    private protected QueryState ForAggregate() => State.HasSelector && (State.Distinct || State.Offset is not null || State.Limit is not null || State.GroupKey is not null)
        ? State.Wrap()
        : State with { Selector = null };

    // 计数与聚合：去重、取段之后的行先包成派生表再算（SELECT SUM(..) FROM (… LIMIT n) t1），否则 LIMIT 作用在聚合结果上
    private QueryState Aggregated() => State.Distinct || State.Offset is not null || State.Limit is not null ? State.Wrap() : ForAggregate();

    /// <summary>执行一个只取一个值的聚合：<c>SELECT FUNC(expr) …</c>。</summary>
    private protected async Task<TResult> AggregateAsync<TResult>(string function, LambdaExpression selector, CancellationToken ct)
    {
        var frame = QueryCompiler.Build(Aggregated() with { OrderBys = [] });
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
