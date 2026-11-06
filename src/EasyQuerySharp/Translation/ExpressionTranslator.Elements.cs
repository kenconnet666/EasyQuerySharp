using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

// 集合导航里的某一行（First / ElementAt）：按所属行分区编行号的派生表
internal sealed partial class ExpressionTranslator
{
    /// <summary>
    /// 集合导航里的某一行：<c>u.Cards.Where(…).OrderBy(…).First()</c> / <c>ElementAt(n)</c> → 派生表
    /// <c>(SELECT 目标列…, (ROW_NUMBER() OVER (PARTITION BY 外键 ORDER BY …)) AS __row__ FROM 目标 WHERE …) tN</c>，
    /// <c>ON (tN.外键 = 所属.键 AND tN.__row__ = n+1)</c>（对应 Java 的 first() / element(n)）。之后可以继续取列、走对一导航。
    /// 没有写排序时按导航的 PartitionOrder（见 <see cref="DefaultPartitionOrder"/>）；多对多经中间表编号。
    /// </summary>
    private DerivedBinding? ElementBinding(MethodCallExpression call)
    {
        if (call.Method.DeclaringType != typeof(Enumerable) || Context is null
            || call.Method.Name is not (nameof(Enumerable.First) or nameof(Enumerable.FirstOrDefault) or nameof(Enumerable.ElementAt) or nameof(Enumerable.ElementAtOrDefault)))
            return null;
        var chain = new List<MethodCallExpression>();
        var current = call.Arguments[0];
        while (current is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Arguments.Count > 0)
        {
            chain.Add(mc);
            current = mc.Arguments[0];
        }
        if (CollectionNavigation(current) is not var (owner, nav)) return null;
        if (nav.KeyMatch != KeyMatch.Equal) throw Unsupported(call, $"{nav} 的键是逗号列表，不能按父行分区取第几个元素");
        chain.Reverse();
        var elementAt = call.Method.Name.StartsWith(nameof(Enumerable.ElementAt), System.StringComparison.Ordinal);
        var index = elementAt ? Convert.ToInt32(ClientEvaluator.Evaluate(call.Arguments[1]), System.Globalization.CultureInfo.InvariantCulture) : 0;
        var predicate = !elementAt && call.Arguments.Count > 1 ? Lambda(call.Arguments[1]) : null;
        var key = string.Join("|", chain.Select(op => op.Method.Name + ":" + (op.Arguments.Count > 1 ? GroupJoinKey(Lambda(op.Arguments[1])) : "")))
                  + "|" + (predicate is null ? "" : GroupJoinKey(predicate)) + "|" + nav.Property.Name + "#" + index;
        return PartitionedRow(owner, nav, chain, predicate, index, key);
    }

    /// <summary>
    /// 声明了条数的对一导航（<c>[Navigate(OneToOne, OrderBy = …, Limit = 1)]</c>，Java 的 OneToOne + orderByProps + limit，如“最新的一条”）
    /// 在查询里：与 <c>First()</c> 一样按所属行分区取第 Offset + 1 行，每个所属行最多连上一行（与 Java 一致）。
    /// </summary>
    private DerivedBinding LimitedToOne(SourceBinding owner, NavigationMetadata nav) =>
        PartitionedRow(owner, nav, [], null, nav.Offset ?? 0, nav.Property.Name + "#navigate");

    // 按所属行分区编行号、取第 index + 1 行的派生表；chain 是取行之前的 Where / OrderBy，key 区分同一所属行上的不同取法
    private DerivedBinding PartitionedRow(SourceBinding owner, NavigationMetadata nav, IReadOnlyList<MethodCallExpression> chain, LambdaExpression? predicate, int index, string key)
    {
        return Context!.ElementJoinFor(owner, key, nav.Required, context =>
        {
            var element = new EntityTableSource(nav.Target);
            var inner = new SelectQuery(element);
            // 多对多：经中间表连目标表，按中间表里对应所属表的列分区，这些列以 __列名__ 选出供外层 ON 用（与 Java 一致）
            var link = nav.Mapping is { } mapping ? context.JoinMapping(inner, element, nav, mapping) : null;
            var partition = link is null
                ? nav.TargetColumns.Select(c => ((SqlExpr)element.Column(c), c.Name)).ToList()
                : nav.SelfMappingColumns.Select(c => ((SqlExpr)link.Column(c), "__" + c.Name + "__")).ToList();
            var t = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = context.ForSubQuery(inner) };
            var binding = new EntityBinding(element);
            inner.Where.AddRange(context.TableFilters(element));
            var orders = new List<OrderByItem>();
            foreach (var op in chain)
            {
                switch (op.Method.Name)
                {
                    case nameof(Enumerable.Where):
                        if (t.NavigationPredicate(Lambda(op.Arguments[1])) is { } where) inner.Where.Add(t.ElementPredicate(where, binding));
                        break;
                    case nameof(Enumerable.OrderBy) or nameof(Enumerable.ThenBy) or nameof(Enumerable.OrderByDescending) or nameof(Enumerable.ThenByDescending):
                        var keyLambda = Lambda(op.Arguments[1]);
                        t.Bind(keyLambda, [binding]);
                        orders.Add(t.OrderItem(keyLambda.Body, op.Method.Name.EndsWith("Descending", System.StringComparison.Ordinal)));
                        break;
                    default:
                        throw Unsupported(op, "取第几行之前只支持 Where / OrderBy / ThenBy");
                }
            }
            if (predicate is not null && t.NavigationPredicate(predicate) is { } kept) inner.Where.Add(t.ElementPredicate(kept, binding));
            if (orders.Count == 0) orders.AddRange(t.DefaultPartitionOrder(nav, element));

            if (link is not null) inner.Columns.AddRange(partition.Select(p => new SelectColumn(p.Item1, p.Item2)));
            var columns = nav.Target.Columns.Where(c => c.AutoSelect).ToList();
            foreach (var c in columns) inner.Columns.Add(new SelectColumn(element.Column(c), null));
            var row = WindowExpr.RowNumber(partition.Select(p => p.Item1).ToList(), orders);
            inner.Columns.Add(new SelectColumn(row, "__row__"));
            // Java 在这里多一层匿名表（多对多时还有中间表的副本），派生表的别名跳号
            var table = new DerivedTableSource(inner) { ReservedAliases = link is null ? 1 : 2 };
            var ownerColumns = nav.SelfColumns.Select(owner.OwnerColumn).ToList();
            var on = partition
                .Select((p, i) => (SqlExpr)new BinaryExpr(new ColumnExpr(table, p.Item2), SqlBinaryOperator.Equal, ownerColumns[i]))
                .Append(new BinaryExpr(new ColumnExpr(table, "__row__"), SqlBinaryOperator.Equal, new ParameterExpr(index + 1, typeof(int))))
                .ToList();
            // 所属查询里对关联列的条件（如带出查询的 id IN (…)）先在分区表内层过滤
            var pushDown = ownerColumns.All(c => c is ColumnExpr) && partition.All(p => p.Item1 is ColumnExpr)
                ? new KeyPushDown(ownerColumns.Cast<ColumnExpr>().ToList(), partition.Select(p => (ColumnExpr)p.Item1).ToList())
                : null;
            return (table, LogicalExpr.Grouped(on), new DerivedBinding(table, columns.ToDictionary(c => c.PropertyName, c => c.Name), nav.Target), pushDown);
        });
    }

    /// <summary>
    /// 取元素、取一段行而链上没写排序时组内的排序，按导航的 <see cref="NavigationMetadata.PartitionOrder"/>：
    /// 主键升序（默认）、不排序、或导航上声明的 <see cref="NavigateAttribute.OrderBy"/>。
    /// </summary>
    internal IEnumerable<OrderByItem> DefaultPartitionOrder(NavigationMetadata nav, EntityTableSource element) => nav.PartitionOrder switch
    {
        PartitionOrder.None => [],
        PartitionOrder.Navigation => nav.DefaultOrder.Select(o =>
        {
            Bind(o.Key, [new EntityBinding(element)]);
            return OrderItem(o.Key.Body, o.Descending);
        }).ToList(),
        _ => KeyOrder(nav, element),
    };

    // 按目标表主键排（Key 升序、KeyDescending 降序）；没有主键时无从排序，明确报错（与 Java 一致，不悄悄不排）
    private static IEnumerable<OrderByItem> KeyOrder(NavigationMetadata nav, EntityTableSource element) =>
        nav.Target.KeyColumns.Count == 0
            ? throw new NotSupportedException($"[{nav}] 取第几个元素要按主键排序，但 {nav.Target.Type.Name} 没有主键：请在导航上声明 OrderBy 或在链上写 OrderBy")
            : nav.Target.KeyColumns.Select(k => new OrderByItem(element.Column(k), nav.PartitionOrder == PartitionOrder.KeyDescending)).ToList();
}
