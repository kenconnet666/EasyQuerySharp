using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Sharding;

/// <summary>语句里的一张分片表：所在查询的条件（按它裁剪），以及是否在最外层的 FROM / JOIN。</summary>
internal sealed record ShardedTable(EntityTableSource Table, IShardRule Rule, IReadOnlyList<SqlExpr> Where, bool Outer);

/// <summary>一个执行单元：一条语句，在一个数据源上执行；<see cref="Nodes"/> 是最外层分片表在这个单元里的节点。</summary>
internal sealed record ShardUnit(string DataSource, TableRoutes Routes, IReadOnlyList<ShardNode> Nodes);

/// <summary>
/// 一条语句的路由结果。<see cref="Units"/> 为空表示条件互相矛盾（空路由）：不访问数据库，列表为空、计数为 0、写入 0 行。
/// </summary>
internal sealed record ShardPlan(IReadOnlyList<ShardUnit> Units, ShardedTable? Outer)
{
    /// <summary>最外层分片表命中的节点数（多于一个时补默认排序）。</summary>
    public int OuterNodeCount => Units.Sum(u => u.Nodes.Count);
}

/// <summary>
/// 分库分表的路由（见 0006 §3、§4）：在翻译好的语句与执行器之间工作，不碰翻译层。
/// <list type="number">
/// <item>收集语句里的分片表，区分最外层 FROM / JOIN 与嵌套处（子查询、派生表、UNION 分支、CTE）；</item>
/// <item>每处按它所在查询的 WHERE 裁剪（<see cref="ShardRouter"/>）；</item>
/// <item>最外层分片表的节点按数据源分组，每组按 <see cref="EasyDbOptions.MaxUnionTables"/> 切块，一块一个单元；</item>
/// <item>嵌套处的分片表必须落在一个数据源里，在各单元里内联成 UNION ALL；不分片的表只在默认数据源。</item>
/// </list>
/// </summary>
internal static class ShardPlanner
{
    /// <summary>
    /// 查询的路由；语句里没有分片表时为 null（照常在默认数据源执行）。<paramref name="outerNodes"/> 不为 null 时最外层分片表只查这些节点。
    /// </summary>
    public static ShardPlan? Plan(EasyDb db, SelectQuery query, IReadOnlyList<ShardNode>? outerNodes = null)
    {
        var scan = new StatementScan(db.Shards);
        scan.Query(query, outer: true);
        return scan.Sharded.Count == 0 ? null : Build(db, scan, db.Options.MaxUnionTables, outerNodes);
    }

    /// <summary>
    /// 更新 / 删除的路由：目标表（与连接的表）在最外层，按 <paramref name="where"/> 裁剪，每个节点一条语句；
    /// <paramref name="targetNode"/> 不为 null 时（按实体写入）只写这个节点。语句里没有分片表时为 null。
    /// </summary>
    public static ShardPlan? PlanWrite(EasyDb db, EntityTableSource target, IReadOnlyList<SqlExpr> where, IEnumerable<JoinClause> joins, IEnumerable<SqlExpr> expressions,
        ShardNode? targetNode)
    {
        var scan = new StatementScan(db.Shards);
        scan.Table(target, where, outer: true);
        foreach (var join in joins)
        {
            scan.Table(join.Table, where, outer: true);
            if (join.On is not null) scan.Expression(join.On);
        }
        foreach (var expression in expressions) scan.Expression(expression);
        return scan.Sharded.Count == 0 ? null : Build(db, scan, unitSize: 1, targetNode is null ? null : [targetNode]);
    }

    /// <summary>语句里有没有分片表（不支持分片的写法据此报错）。</summary>
    public static bool HasSharded(EasyDb db, SelectQuery query)
    {
        if (!db.Shards.Enabled) return false;
        var scan = new StatementScan(db.Shards);
        scan.Query(query, outer: true);
        return scan.Sharded.Count > 0;
    }

    private static ShardPlan Build(EasyDb db, StatementScan scan, int unitSize, IReadOnlyList<ShardNode>? outerNodes)
    {
        var defaultSource = db.Options.DefaultDataSourceName;
        var outerTables = scan.Sharded.Where(s => s.Outer).ToList();
        if (outerTables.Count > 1)
            throw new NotSupportedException($"同一条语句的最外层有两张分片表（{outerTables[0].Table.Entity.Type.Name}、{outerTables[1].Table.Entity.Type.Name}）：分片实体之间不能联查，请分成两次查询再在内存里关联");
        var outer = outerTables.Count == 1 ? outerTables[0] : null;

        // 嵌套处各自按所在查询的条件裁剪；必须在同一个数据源里
        var nested = scan.Sharded.Where(s => !s.Outer).Select(s => (Table: s, Nodes: ShardRouter.Prune(s.Rule, s.Table, s.Where))).ToList();
        var nestedSources = nested.SelectMany(n => n.Nodes).Select(n => n.DataSource).Distinct().ToList();
        if (nestedSources.Count > 1)
            throw new NotSupportedException($"子查询、派生表或 UNION 分支里的分片表跨了数据源（{string.Join("、", nestedSources)}）：嵌套处的分片表只能落在一个数据源里，请加条件缩小范围");

        var routedNodes = outerNodes ?? (outer is null ? [] : ShardRouter.Prune(outer.Rule, outer.Table, outer.Where));
        var total = routedNodes.Count + nested.Sum(n => n.Nodes.Count);
        if (total > db.Options.MaxShardNodes)
            throw new InvalidOperationException($"这条语句命中 {total} 个分片节点，超过上限 MaxShardNodes = {db.Options.MaxShardNodes}：请加分片键上的条件缩小范围，或调大 EasyDbOptions.MaxShardNodes");
        if (outer is not null && routedNodes.Count == 0) return new ShardPlan([], outer);

        var units = new List<ShardUnit>();
        if (outer is null) units.Add(Unit(nestedSources.Count == 1 ? nestedSources[0] : defaultSource, null, []));
        else
            foreach (var group in routedNodes.GroupBy(n => n.DataSource, StringComparer.Ordinal))
            foreach (var chunk in group.Chunk(unitSize))
                units.Add(Unit(group.Key, outer.Table, chunk));
        return new ShardPlan(units, outer);

        ShardUnit Unit(string dataSource, EntityTableSource? outerTable, IReadOnlyList<ShardNode> nodes)
        {
            if (scan.HasUnsharded && dataSource != defaultSource)
                throw new NotSupportedException($"这条语句要在数据源 {dataSource} 上执行，但联了不分片的表（它们只在默认数据源 {defaultSource} 里）：请分成两次查询");
            var routes = new TableRoutes();
            if (outerTable is not null) routes[outerTable] = [.. nodes.Select(n => n.Table)];
            foreach (var (table, tableNodes) in nested)
            {
                if (tableNodes.Count > 0 && tableNodes[0].DataSource != dataSource)
                    throw new NotSupportedException($"子查询里的分片表 {table.Table.Entity.Type.Name} 在数据源 {tableNodes[0].DataSource}，外层语句在数据源 {dataSource}：嵌套处的分片表要与外层在同一个数据源");
                // 嵌套处条件互相矛盾时任取这个数据源里的一张分表：它的条件本来就查不到行
                routes[table.Table] = tableNodes.Count > 0 ? [.. tableNodes.Select(n => n.Table)] : [EmptyRouteTable(table, dataSource)];
            }
            return new ShardUnit(dataSource, routes, nodes);
        }
    }

    private static string EmptyRouteTable(ShardedTable table, string dataSource) =>
        (table.Rule.Nodes.FirstOrDefault(n => n.DataSource == dataSource)
         ?? throw new NotSupportedException($"子查询里的分片表 {table.Table.Entity.Type.Name} 在数据源 {dataSource} 里没有分表")).Table;

    /// <summary>收集一条语句里的表来源：分片表连同它所在查询的条件，以及有没有不分片的表。</summary>
    private sealed class StatementScan(ShardRegistry shards)
    {
        private readonly HashSet<SelectQuery> _visited = new(ReferenceEqualityComparer.Instance);

        public List<ShardedTable> Sharded { get; } = [];

        public bool HasUnsharded { get; private set; }

        public void Query(SelectQuery query, bool outer)
        {
            // 递归 CTE 的递归部分按名字引用自己，同一个定义只看一次
            if (!_visited.Add(query)) return;
            Table(query.From, query.Where, outer);
            foreach (var join in query.Joins)
            {
                Table(join.Table, query.Where, outer);
                if (join.On is not null) Expression(join.On);
            }
            foreach (var e in query.Where) Expression(e);
            foreach (var e in query.GroupBy) Expression(e);
            foreach (var e in query.Having) Expression(e);
            foreach (var o in query.OrderBy) Expression(o.Expr);
            foreach (var c in query.Columns) Expression(c.Expr);
        }

        public void Table(TableSource table, IReadOnlyList<SqlExpr> where, bool outer)
        {
            switch (table)
            {
                case EntityTableSource e when shards.RuleOf(e) is { } rule:
                    if (!Sharded.Exists(s => ReferenceEquals(s.Table, e))) Sharded.Add(new ShardedTable(e, rule, where, outer));
                    break;
                case EntityTableSource or RawTableSource:
                    HasUnsharded = true;
                    break;
                case DerivedTableSource d:
                    Query(d.Query, outer: false);
                    break;
                case UnionTableSource u:
                    foreach (var part in u.Parts) Query(part, outer: false);
                    break;
                case CteTableSource c:
                    Query(c.Query, outer: false);
                    if (c.RecursivePart is { } recursive) Query(recursive, outer: false);
                    break;
            }
        }

        public void Expression(SqlExpr expr)
        {
            switch (expr)
            {
                case ExistsExpr e: Query(e.Query, outer: false); break;
                case SubQueryExpr s: Query(s.Query, outer: false); break;
                case InExpr i:
                    Expression(i.Operand);
                    if (i.SubQuery is not null) Query(i.SubQuery, outer: false);
                    else foreach (var v in i.Values!) Expression(v);
                    break;
                case BinaryExpr b: Expression(b.Left); Expression(b.Right); break;
                case LogicalExpr l: foreach (var o in l.Operands) Expression(o); break;
                case NotExpr n: Expression(n.Operand); break;
                case IsNullExpr n: Expression(n.Operand); break;
                case LikeExpr l: Expression(l.Operand); Expression(l.Pattern); break;
                case FunctionExpr f: foreach (var a in f.Arguments) Expression(a); break;
                case FragmentExpr f: foreach (var a in f.Arguments) Expression(a); break;
                case OrderedStringJoinExpr j:
                    Expression(j.Value);
                    foreach (var o in j.Orders) Expression(o.Expr);
                    break;
                case WindowExpr w:
                    Expression(w.Function);
                    foreach (var p in w.Partitions) Expression(p);
                    foreach (var o in w.Orders) Expression(o.Expr);
                    break;
                case CastExpr c: Expression(c.Operand); break;
                case ComputedExpr c: Expression(c.Inner); break;
                case BlankCheckExpr b: Expression(b.Operand); break;
                case CaseExpr c:
                    foreach (var (when, then) in c.Branches)
                    {
                        Expression(when);
                        Expression(then);
                    }
                    if (c.Else is not null) Expression(c.Else);
                    break;
            }
        }
    }
}
