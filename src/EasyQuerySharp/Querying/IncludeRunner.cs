using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 执行带出：对每个 <see cref="IncludePlan"/>，用主查询收集到的父键查一次子数据（<c>WHERE 子键 IN (…)</c>，
/// 先写子数据自己的条件），按子键分组挂回父对象；子数据里还有带出时递归执行。
/// 多对多先查中间表（<c>SELECT 自身列,目标列 FROM 中间表 WHERE 自身列 IN (…)</c>），再按目标键查目标表（与 Java 一致）。
/// </summary>
internal static partial class IncludeRunner
{
    /// <summary>
    /// 执行 <paramref name="plans"/>。子查询沿用主查询的拦截器选择（主查询 DisableInterceptors 时子数据同样不过滤，与 Java 一致）、
    /// 配置参数和分组大小：父键多于分组大小时分成几条查询。
    /// </summary>
    public static async Task RunAsync(EasyDb db, IReadOnlyList<IncludePlan> plans, IncludeContext context, CancellationToken ct)
    {
        foreach (var plan in plans)
        {
            if (plan.Pending.Count == 0) continue;
            var nav = plan.Navigation;

            var links = await ResolveLinksAsync(db, plan, ct).ConfigureAwait(false);
            if (links.Count == 0)
            {
                // 一个子对象也没有：追踪查询照样记下"加载过、为空"
                if (context.Track && db.Tracker.CanTrack(plan.ChildType) && plan.Via.Count == 0) RecordLoaded(db, plan, links, []);
                plan.Pending.Clear();
                plan.Owners.Clear();
                continue;
            }

            // 先按配置加条件、排序、条数，再投影（投影之后再加条件会包成派生表）
            var baseState = new QueryState(db, [new EntitySource(nav.Target.Type)])
            {
                Wheres = [.. plan.ChildFilters], Interceptors = context.Interceptors, ConfigureArgument = context.ConfigureArgument,
            };
            // 平铺带出（Java 的 @NavigateFlat）单表不写别名；其余带出的子查询在 Java 里总带别名 t
            var flat = plan.FlatPaths is not null;
            var configured = plan.Configure?.Invoke(baseState) ?? baseState;
            // LoadIncludeAsync 的单表子查询不写别名（Java 的 loadInclude）；按父对象取段时包成派生表，照常写
            var state = configured with { ForceAlias = !flat && !(plan.Unaliased && configured.Limit is null && configured.Offset is null) };
            // 带出的子数据没写排序、条数时按导航声明的（Java 的 orderByProps、limit）
            if (state.OrderBys.IsEmpty && nav.DefaultOrder.Count > 0)
                state = state with { OrderBys = [.. nav.DefaultOrder.Select(o => new OrderSpec(o.Key, o.Descending))] };
            if (state.Limit is null && nav.Limit is { } navLimit) state = state with { Limit = navLimit };
            if (plan.ChildSelector is not null) state = state with { Selector = plan.ChildSelector, ImplicitSelector = flat };

            var childrenByKey = new Dictionary<CompositeKey, List<object?>>();
            var nested = new List<IReadOnlyList<IncludePlan>>();
            foreach (var targets in links.Select(l => l.Target).Distinct().Chunk(context.GroupSize ?? db.Options.IncludeGroupSize))
            {
                var compiled = CompileChunk(db, nav, state, targets, context);
                var statement = SqlRenderer.Render(db.Options.Dialect, compiled.Sql);
                var rows = await db.Executor.QueryAsync(statement, r => (Child: compiled.Query.Shaper(r), Key: CompositeKey.Read(r, compiled.Query.RelationOrdinals)), ct)
                    .ConfigureAwait(false);
                foreach (var (child, key) in rows)
                {
                    if (key is null) continue;
                    if (!childrenByKey.TryGetValue(key.Value, out var list)) childrenByKey[key.Value] = list = [];
                    list.Add(child);
                }
                nested.Add(compiled.Query.Includes);
            }
            // 追踪查询：子实体一并追踪（同一主键已追踪时用已追踪的实例）
            var track = context.Track && db.Tracker.CanTrack(plan.ChildType);
            if (track)
                foreach (var children in childrenByKey.Values)
                    for (var i = 0; i < children.Count; i++)
                        if (children[i] is { } child) children[i] = db.Tracker.AttachQueried(child);
            foreach (var (parent, target) in links)
            {
                if (!plan.Pending.TryGetValue(parent, out var sinks) || !childrenByKey.TryGetValue(target, out var children)) continue;
                foreach (var child in children)
                foreach (var sink in sinks)
                    sink(child);
            }
            if (track && plan.Via.Count == 0) RecordLoaded(db, plan, links, childrenByKey);
            plan.Pending.Clear();
            plan.Owners.Clear();
            foreach (var includes in nested) await RunAsync(db, includes, context, ct).ConfigureAwait(false);
        }
    }

    // 每个父实体的这条导航加载了哪些子对象（没有子对象的父实体记空），聚合保存据此算孤儿（见 0004）
    private static void RecordLoaded(EasyDb db, IncludePlan plan, List<(CompositeKey Parent, CompositeKey Target)> links, Dictionary<CompositeKey, List<object?>> childrenByKey)
    {
        var targetsByParent = links.ToLookup(l => l.Parent, l => l.Target);
        foreach (var (parent, owners) in plan.Owners)
        {
            List<object> children = [.. targetsByParent[parent].SelectMany(t => childrenByKey.TryGetValue(t, out var c) ? c : []).OfType<object>().Distinct(ReferenceEqualityComparer.Instance)];
            foreach (var owner in owners) db.Tracker.RecordLoaded(owner, plan.Navigation.Property.Name, children);
        }
    }

    // 按一组目标键编译带出查询；子数据带了 Take / Skip 时每个父对象各取一段，不是整体取一段
    private static (CompiledQuery<object?> Query, SelectQuery Sql) CompileChunk(EasyDb db, NavigationMetadata nav, QueryState state, CompositeKey[] targets, IncludeContext context)
    {
        CompiledQuery<object?> CompileFor(QueryState s, IReadOnlyCollection<CompositeKey> keys) => QueryCompiler.Compile<object?>(
            s with { Wheres = s.Wheres.Add(WithExtraFilter(KeyFilter(nav, keys), nav.ExtraFilter)) }, relationKeys: nav.TargetColumns);

        if (state.Limit is null && state.Offset is null)
        {
            var compiled = CompileFor(state, targets);
            return (compiled, compiled.Sql);
        }
        // 取段要按父对象分区，逗号列表的键里一行子数据属于多个父对象
        if (nav.KeyMatch != KeyMatch.Equal) throw new NotSupportedException($"{nav} 的键是逗号列表，带出时不能按父对象取一段（Take / Skip / Limit）");
        if (db.Options.IncludeLimitMode == IncludeLimitMode.UnionAll)
        {
            var parts = targets.Select(t => CompileFor(state, [t])).ToList();
            return (parts[0], SelectByName(new UnionTableSource([.. parts.Select(p => p.Sql)], all: true) { IndependentAliases = true }, ColumnNames(parts[0].Sql)));
        }
        // 内层取整个实体按父对象编号、取段，子对象的投影写在最外层（与 Java 一致）
        var whole = CompileFor(state with { Limit = null, Offset = null, Selector = null }, targets);
        var ranged = Ranged(whole.Sql, whole.RelationOrdinals[0], state.Offset ?? 0, state.Limit);
        var target = nav.Target;
        var columns = target.Columns.Where(c => c.AutoSelect).ToDictionary(c => c.PropertyName, c => c.Name, StringComparer.Ordinal);
        // 外层从匿名的派生表取列：Java 都写 AS，平铺带出（单表不写别名的风格）不写
        var binding = new DerivedBinding(ranged, columns, target, state.ForceAlias ? columns.Keys.ToHashSet(StringComparer.Ordinal) : null);
        var outer = QueryCompiler.Compile<object?>(
            new QueryState(db, [new BoundSource(binding, target.Type)]) { Selector = state.Selector, ForceAlias = state.ForceAlias, ImplicitSelector = state.ImplicitSelector, Interceptors = context.Interceptors },
            relationKeys: nav.TargetColumns);
        return (outer, outer.Sql);
    }

    /// <summary>
    /// 按父对象分区取段（Java 的 PARTITION 模式，与 Java 逐字一致）：子查询的排序挪进窗口，返回取段后的派生表
    /// <c>(SELECT t1.列 AS 列,… FROM (SELECT …,(ROW_NUMBER() OVER (PARTITION BY 关联键 ORDER BY …)) AS __row__ FROM …) t1
    /// WHERE t1.__row__ &gt;= ? AND t1.__row__ &lt;= ?)</c>，子对象的投影再从它取列。
    /// </summary>
    private static DerivedTableSource Ranged(SelectQuery inner, int keyOrdinal, long skip, long? take)
    {
        var names = ColumnNames(inner);
        var orders = inner.OrderBy.ToList();
        inner.OrderBy.Clear();
        inner.Columns.Add(new SelectColumn(WindowExpr.RowNumber([inner.Columns[keyOrdinal].Expr], orders), "__row__"));
        var numbered = new DerivedTableSource(inner);
        var ranged = SelectByName(numbered, names);
        var row = new ColumnExpr(numbered, "__row__");
        ranged.Where.Add(new BinaryExpr(row, SqlBinaryOperator.GreaterThanOrEqual, new ParameterExpr(skip + 1, typeof(long))));
        if (take is { } t) ranged.Where.Add(new BinaryExpr(row, SqlBinaryOperator.LessThanOrEqual, new ParameterExpr(skip + t, typeof(long))));
        return new DerivedTableSource(ranged);
    }

    // 查询结果各列的名字（有别名用别名，否则是列名）
    private static List<string> ColumnNames(SelectQuery query) =>
        query.Columns.Select(c => c.Alias ?? (c.Expr as ColumnExpr)?.Name ?? throw new InvalidOperationException("带出的子查询有无名的列")).ToList();

    // SELECT 表.列 AS 列,…：按名字原样取出来源的列（列序不变，形状器按序号读）
    private static SelectQuery SelectByName(TableSource table, IEnumerable<string> names)
    {
        var query = new SelectQuery(table);
        foreach (var name in names) query.Columns.Add(new SelectColumn(new ColumnExpr(table, name), name));
        return query;
    }
}

/// <summary>带出查询从主查询沿用的设置：拦截器选择、分组大小、配置参数，以及子实体是否一并追踪。</summary>
internal sealed record IncludeContext(InterceptorSelection Interceptors, int? GroupSize, object? ConfigureArgument, bool Track = false)
{
    /// <summary>不带主查询时（LoadIncludeAsync）：默认拦截器、全局分组大小、没有参数。</summary>
    public static IncludeContext Default { get; } = new(InterceptorSelection.Default, null, null);

    /// <summary>取主查询上的设置。</summary>
    public static IncludeContext Of(QueryState state) =>
        new(state.Interceptors, state.IncludeGroupSize, state.ConfigureArgument, !state.NoTracking && state.Db.Tracker.Active);
}
