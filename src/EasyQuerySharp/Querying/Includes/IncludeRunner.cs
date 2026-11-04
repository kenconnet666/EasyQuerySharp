using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Rendering;

namespace EasyQuerySharp.Querying.Includes;

/// <summary>
/// 执行带出：对每个 <see cref="IncludePlan"/>，用主查询收集到的父键查一次子数据（<c>WHERE 子键 IN (…)</c>，
/// 先写子数据自己的条件），按子键分组挂回父对象；子数据里还有带出时递归执行。
/// </summary>
internal static class IncludeRunner
{
    public static async Task RunAsync(EasyDb db, IReadOnlyList<IncludePlan> plans, CancellationToken ct)
    {
        foreach (var plan in plans)
        {
            if (plan.Pending.Count == 0) continue;
            var nav = plan.Navigation;
            if (nav.Mapping is not null) throw new NotSupportedException($"多对多导航 {nav} 的带出还不支持");
            if (nav.TargetColumns.Count != 1) throw new NotSupportedException($"复合键导航 {nav} 的带出还不支持");

            var state = new QueryState(db, [new EntitySource(nav.Target.Type)])
            {
                Wheres = [.. plan.ChildFilters, KeyIn(nav.Target.Type, nav.TargetColumns[0], plan.Pending.Keys.Select(k => k.First))],
                Selector = plan.ChildSelector,
                ForceAlias = true,
            };
            var compiled = QueryCompiler.Compile<object?>(state, relationKeys: nav.TargetColumns);
            var statement = SqlRenderer.Render(db.Options.Dialect, compiled.Sql);
            var rows = await db.Executor.QueryAsync(statement, r => (Child: compiled.Shaper(r), Key: CompositeKey.Read(r, compiled.RelationOrdinals)), ct)
                .ConfigureAwait(false);

            foreach (var (child, key) in rows)
            {
                if (key is null || !plan.Pending.TryGetValue(key.Value, out var sinks)) continue;
                foreach (var sink in sinks) sink(child);
            }
            plan.Pending.Clear();
            await RunAsync(db, compiled.Includes, ct).ConfigureAwait(false);
        }
    }

    // x => keys.Contains(x.Key)，键值先转成列的 CLR 类型
    private static LambdaExpression KeyIn(Type entityType, Metadata.ColumnMetadata column, IEnumerable<object> keys)
    {
        var listType = typeof(List<>).MakeGenericType(column.ClrType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var key in keys.Distinct()) list.Add(ValueReader.ConvertTo(key, column.ClrType));
        var p = Expression.Parameter(entityType, "x");
        var body = Expression.Call(Expression.Constant(list), listType.GetMethod("Contains")!, Expression.Property(p, column.Property));
        return Expression.Lambda(body, p);
    }
}
