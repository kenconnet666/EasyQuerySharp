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
    /// <summary>执行 <paramref name="plans"/>；子查询沿用主查询的拦截器选择（主查询 DisableInterceptors 时子数据同样不过滤，与 Java 一致）。</summary>
    public static async Task RunAsync(EasyDb db, IReadOnlyList<IncludePlan> plans, InterceptorSelection interceptors, CancellationToken ct)
    {
        foreach (var plan in plans)
        {
            if (plan.Pending.Count == 0) continue;
            var nav = plan.Navigation;
            if (nav.TargetColumns.Count != 1) throw new NotSupportedException($"复合键导航 {nav} 的带出还不支持");

            // 多对多：父键 → 目标键（经中间表）；其余：父键就是目标键
            var links = nav.Mapping is { } mapping
                ? await LoadLinksAsync(db, nav, mapping, plan.Pending.Keys, ct).ConfigureAwait(false)
                : plan.Pending.Keys.Select(k => (Parent: k, Target: k)).ToList();
            if (links.Count == 0)
            {
                plan.Pending.Clear();
                continue;
            }

            // 先按配置加条件、排序、条数，再投影（投影之后再加条件会包成派生表）
            var baseState = new QueryState(db, [new EntitySource(nav.Target.Type)]) { Wheres = [.. plan.ChildFilters], Interceptors = interceptors };
            var state = (plan.Configure?.Invoke(baseState) ?? baseState) with { ForceAlias = true };
            if (plan.ChildSelector is not null) state = state with { Selector = plan.ChildSelector };
            var targets = links.Select(l => l.Target.First).Distinct().ToList();
            CompiledQuery<object?> CompileFor(QueryState s, IEnumerable<object> keys) => QueryCompiler.Compile<object?>(
                s with { Wheres = s.Wheres.Add(WithExtraFilter(KeyIn(nav.Target.Type, nav.TargetColumns[0], keys), nav.ExtraFilter)) }, relationKeys: nav.TargetColumns);

            CompiledQuery<object?> compiled;
            SelectQuery sql;
            // 子数据带了 Take / Skip：每个父对象各取一段，不是整体取一段
            if (state.Limit is null && state.Offset is null)
                sql = (compiled = CompileFor(state, targets)).Sql;
            else if (db.Options.IncludeLimitMode == IncludeLimitMode.UnionAll)
            {
                var parts = targets.Select(t => CompileFor(state, [t])).ToList();
                compiled = parts[0];
                sql = SelectByName(new UnionTableSource([.. parts.Select(p => p.Sql)], all: true) { IndependentAliases = true }, ColumnNames(compiled.Sql));
            }
            else
            {
                // 内层取整个实体按父对象编号、取段，子对象的投影写在最外层（与 Java 一致）
                var whole = CompileFor(state with { Limit = null, Offset = null, Selector = null }, targets);
                var ranged = Ranged(whole.Sql, whole.RelationOrdinals[0], state.Offset ?? 0, state.Limit);
                var target = nav.Target;
                var columns = target.Columns.Where(c => c.AutoSelect).ToDictionary(c => c.PropertyName, c => c.Name, StringComparer.Ordinal);
                // 外层从匿名的派生表取列，Java 都写 AS
                var binding = new DerivedBinding(ranged, columns, target, columns.Keys.ToHashSet(StringComparer.Ordinal));
                compiled = QueryCompiler.Compile<object?>(
                    new QueryState(db, [new BoundSource(binding, target.Type)]) { Selector = state.Selector, ForceAlias = true, Interceptors = interceptors },
                    relationKeys: nav.TargetColumns);
                sql = compiled.Sql;
            }
            var statement = SqlRenderer.Render(db.Options.Dialect, sql);
            var rows = await db.Executor.QueryAsync(statement, r => (Child: compiled.Shaper(r), Key: CompositeKey.Read(r, compiled.RelationOrdinals)), ct)
                .ConfigureAwait(false);

            var childrenByKey = new Dictionary<CompositeKey, List<object?>>();
            foreach (var (child, key) in rows)
            {
                if (key is null) continue;
                if (!childrenByKey.TryGetValue(key.Value, out var list)) childrenByKey[key.Value] = list = [];
                list.Add(child);
            }
            foreach (var (parent, target) in links)
            {
                if (!plan.Pending.TryGetValue(parent, out var sinks) || !childrenByKey.TryGetValue(target, out var children)) continue;
                foreach (var child in children)
                foreach (var sink in sinks)
                    sink(child);
            }
            plan.Pending.Clear();
            await RunAsync(db, compiled.Includes, interceptors, ct).ConfigureAwait(false);
        }
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

    // x => keys.Contains(x.Key)，键值先转成列的 CLR 类型
    private static LambdaExpression KeyIn(Type entityType, ColumnMetadata column, IEnumerable<object> keys)
    {
        var listType = typeof(List<>).MakeGenericType(column.ClrType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var key in keys.Distinct()) list.Add(ValueReader.ConvertTo(key, column.ClrType));
        var p = Expression.Parameter(entityType, "x");
        var body = Expression.Call(Expression.Constant(list), listType.GetMethod("Contains")!, Expression.Property(p, column.Property));
        return Expression.Lambda(body, p);
    }
}
