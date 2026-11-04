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
    /// <summary>分配别名，返回语句里引用到的 CTE（按定义应出现的顺序：被依赖的在前，同名只留第一个）。</summary>
    public static IReadOnlyList<CteTableSource> Allocate(SelectQuery query)
    {
        var tables = new List<TableSource>();
        Collect(query, tables);
        Assign(tables, query.ExplicitProjection);
        return tables.OfType<CteTableSource>().DistinctBy(c => c.Name, StringComparer.Ordinal).ToList();
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

    private static void Collect(SelectQuery query, List<TableSource> tables, DerivedTableSource? owner = null)
    {
        CollectTable(query.From, tables);
        // 声明的 JOIN 先编号（Java 在声明时就建了表），翻译中自动加上的 JOIN（隐式 JOIN、分组合并）排在后面
        var declared = query.Joins.Where(j => !j.Implicit).ToList();
        var automatic = query.Joins.Where(j => j.Implicit).ToList();
        foreach (var join in declared)
        {
            CollectTable(join.Table, tables);
            if (join.On is not null) CollectExpr(join.On, tables);
        }
        for (var i = 0; i < automatic.Count; i++)
        {
            // 按创建顺序编号的派生表：在它创建时已有的 JOIN 之后占位
            if (owner?.AliasAfterJoins == i) AddOwner(owner, tables);
            CollectTable(automatic[i].Table, tables);
            if (automatic[i].On is not null) CollectExpr(automatic[i].On!, tables);
        }
        if (owner is not null && !tables.Contains(owner)) AddOwner(owner, tables);
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
            Collect(derived.Query, tables, derived.AliasAfterJoins is null ? null : derived);
            if (derived.ReserveExtraAlias && derived.AliasAfterJoins is null) tables.Add(Placeholder.Instance);
        }
        // CTE：定义只编号一次（第一次引用时），之后的引用只编自己
        if (table is CteTableSource cte && !tables.OfType<CteTableSource>().Any(c => c.Name == cte.Name))
            Collect(cte.Query, tables);
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
        if (owner.ReserveExtraAlias) tables.Add(Placeholder.Instance);
        tables.Add(owner);
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
            case CastExpr c: CollectExpr(c.Operand, tables); break;
            case BlankCheckExpr b: CollectExpr(b.Operand, tables); break;
            case CaseExpr c:
                foreach (var (when, then) in c.Branches) { CollectExpr(when, tables); CollectExpr(then, tables); }
                if (c.Else is not null) CollectExpr(c.Else, tables);
                break;
        }
    }
}
