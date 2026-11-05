using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Materialization;
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
internal static class IncludeRunner
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
                plan.Pending.Clear();
                continue;
            }

            // 先按配置加条件、排序、条数，再投影（投影之后再加条件会包成派生表）
            var baseState = new QueryState(db, [new EntitySource(nav.Target.Type)])
            {
                Wheres = [.. plan.ChildFilters], Interceptors = context.Interceptors, ConfigureArgument = context.ConfigureArgument,
            };
            // 平铺带出（Java 的 @NavigateFlat）单表不写别名；其余带出的子查询在 Java 里总带别名 t
            var flat = plan.FlatPaths is not null;
            var state = (plan.Configure?.Invoke(baseState) ?? baseState) with { ForceAlias = !flat };
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
            foreach (var (parent, target) in links)
            {
                if (!plan.Pending.TryGetValue(parent, out var sinks) || !childrenByKey.TryGetValue(target, out var children)) continue;
                foreach (var child in children)
                foreach (var sink in sinks)
                    sink(child);
            }
            plan.Pending.Clear();
            foreach (var includes in nested) await RunAsync(db, includes, context, ct).ConfigureAwait(false);
        }
    }

    // 按一组目标键编译带出查询；子数据带了 Take / Skip 时每个父对象各取一段，不是整体取一段
    private static (CompiledQuery<object?> Query, SelectQuery Sql) CompileChunk(EasyDb db, NavigationMetadata nav, QueryState state, CompositeKey[] targets, IncludeContext context)
    {
        CompiledQuery<object?> CompileFor(QueryState s, IReadOnlyCollection<CompositeKey> keys) => QueryCompiler.Compile<object?>(
            s with { Wheres = s.Wheres.Add(WithExtraFilter(KeyIn(nav.Target.Type, nav.TargetColumns, keys), nav.ExtraFilter)) }, relationKeys: nav.TargetColumns);

        if (state.Limit is null && state.Offset is null)
        {
            var compiled = CompileFor(state, targets);
            return (compiled, compiled.Sql);
        }
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
        // 外层从匿名的派生表取列，Java 都写 AS
        var binding = new DerivedBinding(ranged, columns, target, columns.Keys.ToHashSet(StringComparer.Ordinal));
        var outer = QueryCompiler.Compile<object?>(
            new QueryState(db, [new BoundSource(binding, target.Type)]) { Selector = state.Selector, ForceAlias = true, Interceptors = context.Interceptors },
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


    /// <summary>
    /// 父键 → 子数据的目标键：经过中间导航（<see cref="IncludePlan.Via"/>）时逐段查中间表换成下一段的键；
    /// 多对多再经中间表换成目标键；其余情况父键就是目标键。
    /// </summary>
    private static async Task<List<(CompositeKey Parent, CompositeKey Target)>> ResolveLinksAsync(EasyDb db, IncludePlan plan, CancellationToken ct)
    {
        var links = plan.Pending.Keys.Select(k => (Parent: k, Target: k)).ToList();
        for (var i = 0; i < plan.Via.Count && links.Count > 0; i++)
        {
            var next = i + 1 < plan.Via.Count ? plan.Via[i + 1] : plan.Navigation;
            links = Compose(links, await LoadViaAsync(db, plan.Via[i], next, links.Select(l => l.Target), ct).ConfigureAwait(false));
        }
        if (plan.Navigation.Mapping is { } mapping && links.Count > 0)
            links = Compose(links, await LoadLinksAsync(db, plan.Navigation, mapping, links.Select(l => l.Target), ct).ConfigureAwait(false));
        return links;

        // (父, 中间键) 与 (中间键, 下一段键) 连成 (父, 下一段键)；同一对只留一个
        static List<(CompositeKey Parent, CompositeKey Target)> Compose(List<(CompositeKey Parent, CompositeKey Target)> links, List<(CompositeKey From, CompositeKey To)> step)
        {
            var byFrom = step.ToLookup(s => s.From, s => s.To);
            return links.SelectMany(l => byFrom[l.Target].Select(to => (l.Parent, Target: to))).Distinct().ToList();
        }
    }

    // 经过的一段导航：SELECT 下一段的自身列 AS __relation__…, 本段的目标列 AS __relation__… FROM 本段目标表 WHERE 本段目标列 IN (…)（与 Java 一致，不写别名）
    private static async Task<List<(CompositeKey From, CompositeKey To)>> LoadViaAsync(
        EasyDb db, NavigationMetadata step, NavigationMetadata next, IEnumerable<CompositeKey> keys, CancellationToken ct)
    {
        if (step.Mapping is not null || step.TargetColumns.Count != 1)
            throw new NotSupportedException($"平铺路径经过的 {step} 只支持单列键的一对多 / 对一导航");
        var table = new EntityTableSource(step.Target);
        var from = step.TargetColumns[0];
        var query = new SelectQuery(table);
        foreach (var to in next.SelfColumns) query.Columns.Add(new SelectColumn(table.Column(to), ProjectionCompiler.RelationAlias(to)));
        query.Columns.Add(new SelectColumn(table.Column(from), ProjectionCompiler.RelationAlias(from)));
        var values = keys.Select(k => k.First).Distinct()
            .Select(v => (SqlExpr)ExpressionTranslator.Parameter(ValueReader.ConvertTo(v, from.ClrType), from.ClrType)).ToList();
        query.Where.Add(new InExpr(table.Column(from), values, negated: false));
        var statement = SqlRenderer.Render(db.Options.Dialect, query);
        var toOrdinals = Enumerable.Range(0, next.SelfColumns.Count).ToArray();
        var rows = await db.Executor.QueryAsync(statement, r => (From: CompositeKey.Read(r, [toOrdinals.Length]), To: CompositeKey.Read(r, toOrdinals)), ct)
            .ConfigureAwait(false);
        return rows.Where(r => r.From is not null && r.To is not null).Select(r => (r.From!.Value, r.To!.Value)).ToList();
    }

    // 多对多的中间表：按父键取出 (父键, 目标键) 对
    private static async Task<List<(CompositeKey Parent, CompositeKey Target)>> LoadLinksAsync(
        EasyDb db, NavigationMetadata nav, EntityMetadata mapping, IEnumerable<CompositeKey> parentKeys, CancellationToken ct)
    {
        if (nav.SelfMappingColumns.Count != 1 || nav.TargetMappingColumns.Count != 1) throw new NotSupportedException($"复合键的多对多导航 {nav} 的带出还不支持");
        var table = new EntityTableSource(mapping);
        var self = table.Column(nav.SelfMappingColumns[0]);
        var target = table.Column(nav.TargetMappingColumns[0]);
        var query = new SelectQuery(table);
        query.Columns.Add(new SelectColumn(self, null));
        query.Columns.Add(new SelectColumn(target, null));
        var values = parentKeys.Select(k => k.First).Distinct()
            .Select(v => (SqlExpr)ExpressionTranslator.Parameter(ValueReader.ConvertTo(v, nav.SelfMappingColumns[0].ClrType), nav.SelfMappingColumns[0].ClrType)).ToList();
        query.Where.Add(new InExpr(self, values, negated: false));
        if (nav.ExtraMappingFilter is { } mappingFilter)
        {
            var translator = new ExpressionTranslator([]);
            translator.Bind(mappingFilter, [new EntityBinding(table)]);
            query.Where.Add(translator.TranslatePredicate(mappingFilter.Body));
        }
        var statement = SqlRenderer.Render(db.Options.Dialect, query);
        var rows = await db.Executor.QueryAsync(statement, r => (Parent: CompositeKey.Read(r, [0]), Target: CompositeKey.Read(r, [1])), ct).ConfigureAwait(false);
        return rows.Where(r => r.Parent is not null && r.Target is not null).Select(r => (r.Parent!.Value, r.Target!.Value)).ToList();
    }

    // 导航的固定条件与键条件合成一组：x => Sql.Group(keys.Contains(x.Key) && extra(x))，写成 (… IN (…) AND …)（与 Java 一致）
    private static LambdaExpression WithExtraFilter(LambdaExpression keyIn, LambdaExpression? extra)
    {
        if (extra is null) return keyIn;
        var p = keyIn.Parameters[0];
        var extraBody = new ExpressionTranslator.ParameterReplacer(extra.Parameters[0], p).Visit(extra.Body);
        return Expression.Lambda(Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Group))!, Expression.AndAlso(keyIn.Body, extraBody)), p);
    }

    // 单列键：x => keys.Contains(x.Key)；复合键：((x.A =? AND x.B =?) OR (…))（与 Java 一致，= 后面不留空格）。键值先转成列的 CLR 类型
    private static LambdaExpression KeyIn(Type entityType, IReadOnlyList<ColumnMetadata> columns, IReadOnlyCollection<CompositeKey> keys)
    {
        var p = Expression.Parameter(entityType, "x");
        if (columns.Count == 1)
        {
            var column = columns[0];
            var listType = typeof(List<>).MakeGenericType(column.ClrType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var key in keys.Select(k => k.First).Distinct()) list.Add(ValueReader.ConvertTo(key, column.ClrType));
            return Expression.Lambda(Expression.Call(Expression.Constant(list), listType.GetMethod("Contains")!, Expression.Property(p, column.Property)), p);
        }
        var arguments = columns.Select(c => (Expression)Expression.Convert(Expression.Property(p, c.Property), typeof(object))).ToList();
        var groups = new List<string>();
        foreach (var key in keys)
        {
            var values = key.Values(columns.Count).ToList();
            groups.Add("(" + string.Join(" AND ", columns.Select((c, i) => $"{{{i}}} ={{{arguments.Count + i}}}")) + ")");
            arguments.AddRange(columns.Select((c, i) => (Expression)Expression.Constant(ValueReader.ConvertTo(values[i], c.ClrType), typeof(object))));
        }
        var raw = Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Raw))!.MakeGenericMethod(typeof(bool)),
            Expression.Constant("(" + string.Join(" OR ", groups) + ")"), Expression.NewArrayInit(typeof(object), arguments));
        return Expression.Lambda(raw, p);
    }
}

/// <summary>带出查询从主查询沿用的设置：拦截器选择、分组大小、配置参数。</summary>
internal sealed record IncludeContext(InterceptorSelection Interceptors, int? GroupSize, object? ConfigureArgument)
{
    /// <summary>不带主查询时（LoadIncludeAsync）：默认拦截器、全局分组大小、没有参数。</summary>
    public static IncludeContext Default { get; } = new(InterceptorSelection.Default, null, null);

    /// <summary>取主查询上的设置。</summary>
    public static IncludeContext Of(QueryState state) => new(state.Interceptors, state.IncludeGroupSize, state.ConfigureArgument);
}
