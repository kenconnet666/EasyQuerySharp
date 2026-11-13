using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;
// 带出的键：父键经中间表换成目标键（多对多、经过的导航），以及按目标键过滤子数据的条件（单列 IN、复合键 OR）
internal static partial class IncludeRunner
{
    /// <summary>
    /// 父键 → 子数据的目标键：经过中间导航（<see cref="IncludePlan.Via"/>）时逐段查中间表换成下一段的键；
    /// 多对多再经中间表换成目标键；其余情况父键就是目标键。中间表每段一条查询，键多于 <paramref name="groupSize"/> 时 IN 拆成 OR。
    /// </summary>
    private static async Task<List<(CompositeKey Parent, CompositeKey Target)>> ResolveLinksAsync(EasyDb db, IncludePlan plan, int groupSize, TableMapping tables, CancellationToken ct)
    {
        if (plan.Via.FirstOrDefault(v => v.KeyMatch != KeyMatch.Equal) is { } csvStep)
            throw new NotSupportedException($"带出路径经过的 {csvStep} 的键是逗号列表，不能作为中间一段");
        var links = plan.Pending.Keys.Select(k => (Parent: k, Target: k)).ToList();
        for (var i = 0; i < plan.Via.Count && links.Count > 0; i++)
        {
            var step = plan.Via[i];
            var next = i + 1 < plan.Via.Count ? plan.Via[i + 1] : plan.Navigation;
            // 经过的是多对多：先经它的中间表换成目标键
            if (step.Mapping is { } stepMapping)
                links = Compose(links, await LoadLinksAsync(db, step, tables.Table(stepMapping), links.Select(l => l.Target), groupSize, ct).ConfigureAwait(false));
            links = Compose(links, await LoadViaAsync(db, step, next, tables.Table(step.Target), links.Select(l => l.Target), plan.DirectVia, groupSize, ct).ConfigureAwait(false));
        }
        if (plan.Navigation.Mapping is { } mapping && links.Count > 0)
            links = Compose(links, await LoadLinksAsync(db, plan.Navigation, tables.Table(mapping), links.Select(l => l.Target), groupSize, ct).ConfigureAwait(false));
        return plan.Navigation.KeyMatch == KeyMatch.SelfCsv ? SplitCsv(links, plan.Navigation.TargetColumns[0].ClrType) : links;

        // (父, 中间键) 与 (中间键, 下一段键) 连成 (父, 下一段键)；同一对只留一个
        static List<(CompositeKey Parent, CompositeKey Target)> Compose(List<(CompositeKey Parent, CompositeKey Target)> links, List<(CompositeKey From, CompositeKey To)> step)
        {
            var byFrom = step.ToLookup(s => s.From, s => s.To);
            return links.SelectMany(l => byFrom[l.Target].Select(to => (l.Parent, Target: to))).Distinct().ToList();
        }
    }

    // 父键是逗号列表：拆成 (父, 目标键) 对，与 FIND_IN_SET 一样不去空格；空项、转不成目标键类型的项不匹配任何目标
    private static List<(CompositeKey Parent, CompositeKey Target)> SplitCsv(List<(CompositeKey Parent, CompositeKey Target)> links, Type keyType) =>
        links.SelectMany(l => ((string)l.Target.First).Split(',')
                .Select(item => TryConvert(item, keyType) is { } key ? CompositeKey.Of([key]) : null)
                .OfType<CompositeKey>().Select(target => (l.Parent, Target: target)))
            .Distinct().ToList();

    private static object? TryConvert(string item, Type type)
    {
        if (item.Length == 0) return null;
        try
        {
            return ValueReader.ConvertTo(item, type);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // 按目标键取子数据的条件：键是逗号列表时写成 Sql.InCsv(目标键, "k1,k2,…")（与 Java 的 FIND_IN_SET 一致），否则 IN / 复合键 OR
    // single：只有一个父对象的键（UNION ALL 的一段），单列写成 = 而不是 IN (?)（与 Java 一致）
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    private static LambdaExpression KeyFilter(NavigationMetadata nav, IReadOnlyCollection<CompositeKey> keys, int groupSize, bool single = false)
    {
        if (nav.KeyMatch == KeyMatch.Equal)
            return single && nav.TargetColumns is [var keyColumn] ? KeyEquals(nav.Target.Type, keyColumn, keys.Single()) : KeyIn(nav.Target.Type, nav.TargetColumns, keys, groupSize);
        var p = Expression.Parameter(nav.Target.Type, "x");
        var column = nav.TargetColumns[0];
        var list = string.Join(",", keys.Select(k => Convert.ToString(k.First, System.Globalization.CultureInfo.InvariantCulture)).Distinct());
        return Expression.Lambda(Expression.Call(typeof(Sql).GetMethod(nameof(Sql.InCsv))!.MakeGenericMethod(column.Property.PropertyType),
            Expression.Property(p, column.Property), Expression.Constant(list)), p);
    }

    /// <summary>
    /// 经过的一段导航：按本段的目标列查本段目标表，取出下一段的自身列（与 Java 一致，单表不写别名）。
    /// 平铺路径（@NavigateFlat）写成 <c>SELECT 下一段自身列 AS __relation__…, 本段目标列 AS __relation__… FROM 表 WHERE 本段目标列 IN (…)</c>；
    /// 直达导航（DirectMapping）写成 <c>SELECT 本段目标列, 下一段自身列 FROM 表 WHERE …</c>，复合键写成 <c>((a =? AND b =?) OR …)</c>。
    /// </summary>
    private static async Task<List<(CompositeKey From, CompositeKey To)>> LoadViaAsync(
        EasyDb db, NavigationMetadata step, NavigationMetadata next, EntityTableSource table, IEnumerable<CompositeKey> keys, bool direct, int groupSize, CancellationToken ct)
    {
        var query = new SelectQuery(table);
        int Select(ColumnMetadata column, bool alias)
        {
            var existing = query.Columns.FindIndex(c => c.Expr is ColumnExpr ce && ce.Name == column.Name);
            if (existing >= 0) return existing;
            query.Columns.Add(new SelectColumn(table.Column(column), alias ? ProjectionCompiler.RelationAlias(column) : null));
            return query.Columns.Count - 1;
        }
        int[] fromOrdinals, toOrdinals;
        if (direct)
        {
            fromOrdinals = step.TargetColumns.Select(c => Select(c, alias: false)).ToArray();
            toOrdinals = next.SelfColumns.Select(c => Select(c, alias: false)).ToArray();
        }
        else
        {
            toOrdinals = next.SelfColumns.Select(c => Select(c, alias: true)).ToArray();
            fromOrdinals = step.TargetColumns.Select(c => Select(c, alias: true)).ToArray();
        }
        query.Where.Add(KeysIn(table, step.TargetColumns, keys.Distinct().ToList(), groupSize));
        var rows = await db.Executor.QueryAsync(query, r => (From: CompositeKey.Read(r, fromOrdinals, step.TargetColumns), To: CompositeKey.Read(r, toOrdinals, next.SelfColumns)), ct)
            .ConfigureAwait(false);
        return rows.Where(r => r.From is not null && r.To is not null).Select(r => (r.From!.Value, r.To!.Value)).ToList();
    }


    // 复合键的模板：先是各列 {0}…{n-1}，再是每组键的值，写成 (({0} ={n} AND {1} ={n+1}) OR (…))（与 Java 一致，= 后面不留空格）
    private static string TupleInTemplate(int columns, int keys) =>
        "(" + string.Join(" OR ", Enumerable.Range(0, keys).Select(k =>
            "(" + string.Join(" AND ", Enumerable.Range(0, columns).Select(i => $"{{{i}}} ={{{columns + k * columns + i}}}")) + ")")) + ")";
    // 单列键：列 IN (…)，多于 groupSize 个时拆成 (列 IN (…) OR 列 IN (…))；复合键：((a =? AND b =?) OR (…))（与 Java 一致，= 后面不留空格）。
    // 键值先转成列的 CLR 类型
    private static SqlExpr KeysIn(EntityTableSource table, IReadOnlyList<ColumnMetadata> columns, IReadOnlyList<CompositeKey> keys, int groupSize)
    {
        SqlExpr Value(object value, ColumnMetadata column) => ExpressionTranslator.Parameter(ValueReader.ConvertTo(value, column.ClrType), column.ClrType);
        if (columns.Count == 1)
        {
            var column = table.Column(columns[0]);
            var values = keys.Select(k => k.First).Distinct().Select(v => Value(CompositeKey.Unwrap(v), columns[0])).ToList();
            return values.Count <= groupSize
                ? new InExpr(column, values, negated: false)
                : LogicalExpr.GroupedOr([.. values.Chunk(groupSize).Select(chunk => new InExpr(column, chunk, negated: false))]);
        }
        var arguments = columns.Select(c => (SqlExpr)table.Column(c)).ToList();
        foreach (var key in keys) arguments.AddRange(columns.Zip(key.Values(columns.Count), (c, v) => Value(v, c)));
        return new FragmentExpr(TupleInTemplate(columns.Count, keys.Count), arguments, isPredicate: true);
    }

    // 多对多的中间表：按父键取出 (父键, 目标键) 对
    private static async Task<List<(CompositeKey Parent, CompositeKey Target)>> LoadLinksAsync(
        EasyDb db, NavigationMetadata nav, EntityTableSource table, IEnumerable<CompositeKey> parentKeys, int groupSize, CancellationToken ct)
    {
        if (nav.SelfMappingColumns.Count != 1 || nav.TargetMappingColumns.Count != 1) throw new NotSupportedException($"复合键的多对多导航 {nav} 的带出还不支持");
        var query = new SelectQuery(table);
        query.Columns.Add(new SelectColumn(table.Column(nav.SelfMappingColumns[0]), null));
        query.Columns.Add(new SelectColumn(table.Column(nav.TargetMappingColumns[0]), null));
        query.Where.Add(KeysIn(table, nav.SelfMappingColumns, parentKeys.ToList(), groupSize));
        if (nav.ExtraMappingFilter is { } mappingFilter)
        {
            var translator = new ExpressionTranslator([]);
            translator.Bind(mappingFilter, [new EntityBinding(table)]);
            query.Where.Add(translator.TranslatePredicate(mappingFilter.Body));
        }
        var rows = await db.Executor.QueryAsync(query, r => (Parent: CompositeKey.Read(r, [0], nav.SelfMappingColumns), Target: CompositeKey.Read(r, [1], nav.TargetMappingColumns)), ct).ConfigureAwait(false);
        return rows.Where(r => r.Parent is not null && r.Target is not null).Select(r => (r.Parent!.Value, r.Target!.Value)).ToList();
    }

    // 导航的固定条件与键条件合成一组：x => Sql.Group(keys.Contains(x.Key) && extra(x))，写成 (… IN (…) AND …)（与 Java 一致）
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression WithExtraFilter(LambdaExpression keyIn, LambdaExpression? extra)
    {
        if (extra is null) return keyIn;
        var p = keyIn.Parameters[0];
        var extraBody = new ExpressionTranslator.ParameterReplacer(extra.Parameters[0], p).Visit(extra.Body);
        return Expression.Lambda(Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Group))!, Expression.AndAlso(keyIn.Body, extraBody)), p);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression KeyEquals(Type entityType, ColumnMetadata column, CompositeKey key)
    {
        var p = Expression.Parameter(entityType, "x");
        var value = Expression.Constant(ValueReader.ConvertTo(CompositeKey.Unwrap(key.First), column.ClrType), column.Property.PropertyType);
        return Expression.Lambda(Expression.Equal(Expression.Property(p, column.Property), value), p);
    }

    // 单列键：x => keys.Contains(x.Key)，多于 groupSize 个时拆成 x => k1.Contains(x.Key) || k2.Contains(x.Key) …，写成 (… IN (…) OR … IN (…))；
    // 复合键：((x.A =? AND x.B =?) OR (…))（与 Java 一致，= 后面不留空格）。键值先转成列的 CLR 类型
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression KeyIn(Type entityType, IReadOnlyList<ColumnMetadata> columns, IReadOnlyCollection<CompositeKey> keys, int groupSize)
    {
        var p = Expression.Parameter(entityType, "x");
        if (columns.Count == 1)
        {
            var column = columns[0];
            Expression Contains(object[] values) => ClientValues.Contains(values.Select(CompositeKey.Unwrap), Expression.Property(p, column.Property));
            return Expression.Lambda(keys.Select(k => k.First).Distinct().Chunk(groupSize).Select(Contains).Aggregate(Expression.OrElse), p);
        }
        var arguments = columns.Select(c => (Expression)Expression.Convert(Expression.Property(p, c.Property), typeof(object))).ToList();
        foreach (var key in keys)
            arguments.AddRange(columns.Zip(key.Values(columns.Count), (c, v) => (Expression)Expression.Constant(ValueReader.ConvertTo(v, c.ClrType), typeof(object))));
        var raw = Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Raw))!.MakeGenericMethod(typeof(bool)),
            Expression.Constant(TupleInTemplate(columns.Count, keys.Count)), Expression.NewArrayInit(typeof(object), arguments));
        return Expression.Lambda(raw, p);
    }
}
