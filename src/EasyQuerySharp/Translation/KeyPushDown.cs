using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 把所属查询 WHERE 里只涉及关联列的等值 / IN 条件（或全由它们组成的 OR）复制一份到关联的派生表（分组合并、取第几行的分区表）的 WHERE 末尾，
/// 列换成派生表里对应的关联键，先过滤再分组 / 编号（与 Java 的 DefaultSubQueryExtraPredicateProvider 一致）。
/// </summary>
/// <param name="ownerColumns">所属行上的关联列（外层 ON 右侧的列）。</param>
/// <param name="innerColumns">派生表内层里对应的关联键，与 <paramref name="ownerColumns"/> 一一对应。</param>
internal sealed class KeyPushDown(IReadOnlyList<ColumnExpr> ownerColumns, IReadOnlyList<ColumnExpr> innerColumns)
{
    /// <summary>所属查询 WHERE 里可以下推的条件（已换成内层的列）。</summary>
    public IEnumerable<SqlExpr> From(IEnumerable<SqlExpr> ownerWhere) =>
        ownerWhere.SelectMany(Conjuncts).Select(Retarget).OfType<SqlExpr>();

    // 一个 Where 里的 a && b 是一个 AND 节点，按合取项逐个看
    private static IEnumerable<SqlExpr> Conjuncts(SqlExpr e) =>
        e is LogicalExpr { IsAnd: true, IsGrouped: false } and ? and.Operands.SelectMany(Conjuncts) : [e];

    private SqlExpr? Retarget(SqlExpr predicate)
    {
        switch (predicate)
        {
            case BinaryExpr { Operator: SqlBinaryOperator.Equal } b when KeyFor(b.Left) is { } key && IsValue(b.Right):
                return new BinaryExpr(key, b.Operator, b.Right);
            case InExpr { Values: { } values, Negated: false } i when KeyFor(i.Operand) is { } key && values.All(IsValue):
                return new InExpr(key, values, negated: false);
            case LogicalExpr { IsAnd: false } or:
                var parts = or.Operands.Select(Retarget).ToList();
                return parts.All(p => p is not null) ? new LogicalExpr(false, parts!) : null;
            default:
                return null;
        }

        static bool IsValue(SqlExpr e) => e is ParameterExpr or LiteralExpr;
    }

    // 所属行上的某个关联列 → 内层里对应的关联键
    private ColumnExpr? KeyFor(SqlExpr e)
    {
        if (e is not ColumnExpr column) return null;
        for (var i = 0; i < ownerColumns.Count; i++)
            if (ownerColumns[i].Table == column.Table && ownerColumns[i].Name == column.Name) return innerColumns[i];
        return null;
    }
}
