using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Rendering;

/// <summary>
/// 给一条语句里的全部表来源分配别名，规则与 Java 版一致：
/// <list type="bullet">
/// <item>整条语句（含子查询）只有一个表来源、且没有指定别名时，不加别名；</item>
/// <item>否则按遍历顺序编号为 <c>t</c>、<c>t1</c>、<c>t2</c>…，用户指定的别名优先但仍占用编号；</item>
/// <item>遍历顺序：FROM、各 JOIN（派生表先编号内部的表，再编号自己），然后 WHERE、GROUP BY、HAVING、ORDER BY、SELECT 里的子查询（Java 按调用顺序建表，投影总在最后）。</item>
/// </list>
/// </summary>
internal static class AliasAllocator
{
    /// <summary>
    /// 分配别名，返回语句里引用到的 CTE（按定义应出现的顺序：被依赖的在前，同名只留第一个）。
    /// 分片表路由成几张分表的 UNION ALL 派生表时（<paramref name="routes"/>），单表也要有别名。
    /// </summary>
    public static IReadOnlyList<CteTableSource> Allocate(SelectQuery query, Sharding.TableRoutes? routes = null)
    {
        var tables = new List<TableSource>();
        Collect(query, tables);
        Assign(tables, query.ExplicitProjection || (tables.Count == 1 && routes?.IsUnion(tables[0]) == true));
        // 递归 CTE 的引用先于它的定义编号，定义里又可能引用别的 CTE（如视图实体）：按定义完成的先后写
        return tables.OfType<CteTableSource>().DistinctBy(c => c.Name, StringComparer.Ordinal).OrderBy(c => c.DefinedAt).ToList();
    }

    /// <summary>UPDATE / DELETE：目标表、连接的表（含 ON 里的子查询），再是 SET / WHERE 里的子查询。</summary>
    public static void Allocate(TableSource single, IReadOnlyList<JoinClause> joins, IEnumerable<SqlExpr> expressions)
    {
        var tables = new List<TableSource> { single };
        foreach (var join in joins) CollectJoin(join, tables);
        foreach (var e in expressions) CollectExpr(e, tables);
        Assign(tables, forceAlias: false);
    }

    // 单表：有显式投影、或是派生表 / 原生 SQL 来源时用 t，否则不加别名
    private static void Assign(List<TableSource> tables, bool forceAlias)
    {
        if (tables.Count == 1)
        {
            var table = tables[0];
            table.Alias = table.ExplicitAlias ?? (forceAlias || table is not EntityTableSource ? "t" : null);
            return;
        }
        for (var i = 0; i < tables.Count; i++)
            tables[i].Alias = tables[i].ExplicitAlias ?? (i == 0 ? "t" : "t" + i);
    }

    private static void Collect(SelectQuery query, List<TableSource> tables, DerivedTableSource? owner = null)
    {
        CollectTable(query.From, tables);
        // 声明的 JOIN 先编号（Java 在声明时就建了表），翻译中自动加上的 JOIN（隐式 JOIN、分组合并）排在后面
        var declared = query.Joins.Where(j => !j.Implicit).ToList();
        // 条件翻译完之后（排序、投影里）才加的隐式 JOIN 排在条件里的子查询之后（Java 按创建顺序编号）
        var automatic = query.Joins.Where(j => j is { Implicit: true, AfterWhere: false }).ToList();
        var afterWhere = query.Joins.Where(j => j is { Implicit: true, AfterWhere: true }).ToList();
        foreach (var join in declared) CollectJoin(join, tables);
        for (var i = 0; i < automatic.Count; i++)
        {
            // 按创建顺序编号的派生表：在它创建时已有的 JOIN 之后占位
            if (owner?.AliasAfterJoins == i) AddOwner(owner, tables);
            CollectJoin(automatic[i], tables);
        }
        if (owner is not null && !tables.Contains(owner)) AddOwner(owner, tables);
        foreach (var e in query.Where) CollectExpr(e, tables);
        foreach (var join in afterWhere) CollectJoin(join, tables);
        foreach (var e in query.GroupBy) CollectExpr(e, tables);
        foreach (var e in query.Having) CollectExpr(e, tables);
        foreach (var o in query.OrderBy) CollectExpr(o.Expr, tables);
        foreach (var c in query.Columns) CollectExpr(c.Expr, tables);
    }

    private static void CollectJoin(JoinClause join, List<TableSource> tables)
    {
        CollectTable(join.Table, tables);
        if (join.On is not null) CollectExpr(join.On, tables);
    }

    private static void CollectTable(TableSource table, List<TableSource> tables)
    {
        if (table is DerivedTableSource derived)
        {
            Collect(derived.Query, tables, derived.AliasAfterJoins is null ? null : derived);
            if (derived.AliasAfterJoins is null) Reserve(derived.ReservedAliases, tables);
        }
        // 递归 CTE（树查询）：先编外层引用自己，再编基础部分、递归部分（递归部分里引用自己时名字已登记，只编号）（与 Java 一致）
        if (table is CteTableSource { RecursivePart: { } recursive } tree && !tables.OfType<CteTableSource>().Any(c => c.Name == tree.Name))
        {
            tables.Add(tree);
            Collect(tree.Query, tables);
            Collect(recursive, tables);
            tree.DefinedAt = tables.Count;
            // Java 拼树时内部还建了 UNION 与 CTE 两层匿名表：之后的表从 t6 起编号
            Reserve(2, tables);
            return;
        }
        // CTE：定义在第一次引用时编号（Java 按创建顺序），按定义来自哪个查询对象分三种情况（与 Java 一致）
        if (table is CteTableSource cte)
        {
            var earlier = tables.OfType<CteTableSource>().ToList();
            bool SameDefinition(CteTableSource c) => c.DefinitionKey is not null && ReferenceEquals(c.DefinitionKey, cte.DefinitionKey);
            // 同一个查询对象换了名字再作 CTE：Java 里那个对象的表已经编过号，从它第一次出现的编号起单独编号，不占外层编号
            if (earlier.FirstOrDefault(c => c.Name != cte.Name && SameDefinition(c)) is { } same && tables.IndexOf(same.Query.From) is var start and >= 0)
            {
                var own = Enumerable.Repeat<TableSource>(Placeholder.Instance, start).ToList();
                Collect(cte.Query, own);
                Assign(own, forceAlias: true);
            }
            // 第一次出现的名字；或同名但定义是另建的查询（如视图实体每次引用都重新构造）：编号照常占用，语句里只写第一份定义
            else if (!earlier.Any(c => c.Name == cte.Name) || earlier.Any(c => c.Name == cte.Name && c.DefinitionKey is not null && cte.DefinitionKey is not null && !SameDefinition(c)))
            {
                Collect(cte.Query, tables);
                if (cte.ReserveExtraAlias) tables.Add(Placeholder.Instance);
            }
            cte.DefinedAt = tables.Count;
        }
        if (table is UnionTableSource { IndependentAliases: true } independent)
        {
            // 各分支单独编号；外层占一个编号之后（分支里的 t 已经用掉 t）
            foreach (var part in independent.Parts)
            {
                var own = new List<TableSource>();
                Collect(part, own);
                Assign(own, forceAlias: true);
            }
            tables.Add(Placeholder.Instance);
        }
        else if (table is UnionTableSource union)
            foreach (var part in union.Parts) Collect(part, tables);
        if (!tables.Contains(table)) tables.Add(table);
    }

    // 按创建顺序编号的派生表：需要跳号时先占一个空位
    private static void AddOwner(DerivedTableSource owner, List<TableSource> tables)
    {
        Reserve(owner.ReservedAliases, tables);
        tables.Add(owner);
    }

    private static void Reserve(int count, List<TableSource> tables)
    {
        for (var i = 0; i < count; i++) tables.Add(Placeholder.Instance);
    }

    // 只占编号、不渲染的表来源
    private sealed class Placeholder : TableSource
    {
        public static readonly Placeholder Instance = new();
    }

    // 只关心表达式里出现的子查询；普通列引用的表已经在 FROM / JOIN 里登记
    private static void CollectExpr(SqlExpr expr, List<TableSource> tables)
    {
        switch (expr)
        {
            case ExistsExpr e: Collect(e.Query, tables); break;
            case SubQueryExpr s:
                Collect(s.Query, tables);
                if (s.ReserveExtraAlias) tables.Add(Placeholder.Instance);
                break;
            case InExpr { SubQuery: { } sub } i:
                CollectExpr(i.Operand, tables);
                Collect(sub, tables);
                break;
            case InExpr i:
                CollectExpr(i.Operand, tables);
                foreach (var v in i.Values!) CollectExpr(v, tables);
                break;
            case BinaryExpr b: CollectExpr(b.Left, tables); CollectExpr(b.Right, tables); break;
            case LogicalExpr l: foreach (var o in l.Operands) CollectExpr(o, tables); break;
            case NotExpr n: CollectExpr(n.Operand, tables); break;
            case IsNullExpr n: CollectExpr(n.Operand, tables); break;
            case LikeExpr l: CollectExpr(l.Operand, tables); CollectExpr(l.Pattern, tables); break;
            case FunctionExpr f: foreach (var a in f.Arguments) CollectExpr(a, tables); break;
            case FragmentExpr f: foreach (var a in f.Arguments) CollectExpr(a, tables); break;
            case OrderedStringJoinExpr j:
                CollectExpr(j.Value, tables);
                foreach (var o in j.Orders) CollectExpr(o.Expr, tables);
                break;
            case CastExpr c: CollectExpr(c.Operand, tables); break;
            case ComputedExpr c: CollectExpr(c.Inner, tables); break;
            case BlankCheckExpr b: CollectExpr(b.Operand, tables); break;
            case CaseExpr c:
                foreach (var (when, then) in c.Branches) { CollectExpr(when, tables); CollectExpr(then, tables); }
                if (c.Else is not null) CollectExpr(c.Else, tables);
                break;
        }
    }
}
