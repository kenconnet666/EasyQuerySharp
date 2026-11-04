using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying.Includes;

/// <summary>
/// 执行带出：对每个 <see cref="IncludePlan"/>，用主查询收集到的父键查一次子数据（<c>WHERE 子键 IN (…)</c>，
/// 先写子数据自己的条件），按子键分组挂回父对象；子数据里还有带出时递归执行。
/// 多对多先查中间表（<c>SELECT 自身列,目标列 FROM 中间表 WHERE 自身列 IN (…)</c>），再按目标键查目标表（与 Java 一致）。
/// </summary>
internal static class IncludeRunner
{
    public static async Task RunAsync(EasyDb db, IReadOnlyList<IncludePlan> plans, CancellationToken ct)
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

            var baseState = new QueryState(db, [new EntitySource(nav.Target.Type)]) { Wheres = [.. plan.ChildFilters], Selector = plan.ChildSelector };
            var state = plan.Configure?.Invoke(baseState) ?? baseState;
            state = state with
            {
                Wheres = state.Wheres.Add(WithExtraFilter(KeyIn(nav.Target.Type, nav.TargetColumns[0], links.Select(l => l.Target.First)), nav.ExtraFilter)),
                ForceAlias = true,
            };
            var compiled = QueryCompiler.Compile<object?>(state, relationKeys: nav.TargetColumns);
            var statement = SqlRenderer.Render(db.Options.Dialect, compiled.Sql);
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
            await RunAsync(db, compiled.Includes, ct).ConfigureAwait(false);
        }
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
