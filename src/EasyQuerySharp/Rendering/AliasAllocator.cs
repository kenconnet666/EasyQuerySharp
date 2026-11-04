using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Rendering;

/// <summary>
/// 给一条语句里的全部表来源分配别名，规则与 Java 版一致：
/// <list type="bullet">
/// <item>整条语句（含子查询）只有一个表来源、且没有指定别名时，不加别名；</item>
/// <item>否则按遍历顺序编号为 <c>t</c>、<c>t1</c>、<c>t2</c>…，用户指定的别名优先但仍占用编号；</item>
/// <item>遍历顺序：FROM、各 JOIN（派生表先编号内部的表，再编号自己），然后 WHERE、SELECT、GROUP BY、HAVING、ORDER BY 里的子查询。</item>
/// </list>
/// </summary>
internal static class AliasAllocator
{
    public static void Allocate(SelectQuery query)
    {
        var tables = new List<TableSource>();
        Collect(query, tables);
        Assign(tables, query.ExplicitProjection);
    }

    public static void Allocate(TableSource single, IEnumerable<SqlExpr> expressions)
    {
        var tables = new List<TableSource> { single };
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

    private static void Collect(SelectQuery query, List<TableSource> tables)
    {
        CollectTable(query.From, tables);
        foreach (var join in query.Joins)
        {
            CollectTable(join.Table, tables);
            if (join.On is not null) CollectExpr(join.On, tables);
        }
        foreach (var e in query.Where) CollectExpr(e, tables);
        foreach (var c in query.Columns) CollectExpr(c.Expr, tables);
        foreach (var e in query.GroupBy) CollectExpr(e, tables);
        foreach (var e in query.Having) CollectExpr(e, tables);
        foreach (var o in query.OrderBy) CollectExpr(o.Expr, tables);
    }

    private static void CollectTable(TableSource table, List<TableSource> tables)
    {
        if (table is DerivedTableSource derived)
        {
            Collect(derived.Query, tables);
            if (derived.ReserveExtraAlias) tables.Add(Placeholder.Instance);
        }
        if (!tables.Contains(table)) tables.Add(table);
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
            case SubQueryExpr s: Collect(s.Query, tables); break;
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
            case CaseExpr c:
                foreach (var (when, then) in c.Branches) { CollectExpr(when, tables); CollectExpr(then, tables); }
                if (c.Else is not null) CollectExpr(c.Else, tables);
                break;
        }
    }
}
