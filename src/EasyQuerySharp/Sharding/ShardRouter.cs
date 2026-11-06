using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Sharding;

/// <summary>
/// 从语句的 WHERE 裁剪出一张分片表要查的节点（对应 Java 的 RoutePredicateDiscover + routeFilter）：
/// 分片键列与参数的比较交给 <see cref="IShardRule.Prune"/>；<c>IN</c>（值全是参数）取各值的并集；AND 取交集；
/// OR 取并集，有一项不能裁剪时整体不约束；其余写法（<c>&lt;&gt;</c>、LIKE、函数、与列比较）不约束。
/// 结果总是全部节点的子集、保持规则里的顺序，只会少裁剪，不会裁错。
/// </summary>
internal static class ShardRouter
{
    /// <summary><paramref name="table"/> 在条件 <paramref name="where"/>（按 AND 连接）下要查的节点；条件互相矛盾时为空。</summary>
    public static IReadOnlyList<ShardNode> Prune(IShardRule rule, EntityTableSource table, IEnumerable<SqlExpr> where)
    {
        var nodes = rule.Nodes;
        HashSet<ShardNode>? result = null;
        foreach (var term in where)
        {
            if (Evaluate(rule, table, term) is not { } matched) continue;
            if (result is null) result = matched;
            else result.IntersectWith(matched);
        }
        return result is null ? nodes : [.. nodes.Where(result.Contains)];
    }

    // 一个条件可能命中的节点；不约束时为 null
    private static HashSet<ShardNode>? Evaluate(IShardRule rule, EntityTableSource table, SqlExpr term)
    {
        switch (term)
        {
            case LogicalExpr { IsAnd: true } and:
            {
                HashSet<ShardNode>? result = null;
                foreach (var operand in and.Operands)
                {
                    if (Evaluate(rule, table, operand) is not { } matched) continue;
                    if (result is null) result = matched;
                    else result.IntersectWith(matched);
                }
                return result;
            }
            case LogicalExpr or:
            {
                var result = new HashSet<ShardNode>();
                foreach (var operand in or.Operands)
                {
                    if (Evaluate(rule, table, operand) is not { } matched) return null;
                    result.UnionWith(matched);
                }
                return result;
            }
            case BinaryExpr { IsComparison: true } b when Operator(b.Operator) is { } op:
                if (KeyOf(rule, table, b.Left) is { } left && b.Right is ParameterExpr right) return [.. rule.Prune(left, op, right.Value)];
                if (KeyOf(rule, table, b.Right) is { } right2 && b.Left is ParameterExpr left2) return [.. rule.Prune(right2, Flip(op), left2.Value)];
                return null;
            case InExpr { Negated: false, Values: { } values } i when KeyOf(rule, table, i.Operand) is { } property && values.All(v => v is ParameterExpr):
            {
                var result = new HashSet<ShardNode>();
                foreach (ParameterExpr value in values) result.UnionWith(rule.Prune(property, ShardOperator.Equal, value.Value));
                return result;
            }
            default:
                return null;
        }
    }

    // 这张表上参与路由的列对应的属性名；不是时为 null
    private static string? KeyOf(IShardRule rule, EntityTableSource table, SqlExpr expr) =>
        expr is ColumnExpr { Column: { } column } c && ReferenceEquals(c.Table, table) && rule.KeyProperties.Contains(column.PropertyName) ? column.PropertyName : null;

    private static ShardOperator? Operator(SqlBinaryOperator op) => op switch
    {
        SqlBinaryOperator.Equal => ShardOperator.Equal,
        SqlBinaryOperator.GreaterThan => ShardOperator.GreaterThan,
        SqlBinaryOperator.GreaterThanOrEqual => ShardOperator.GreaterThanOrEqual,
        SqlBinaryOperator.LessThan => ShardOperator.LessThan,
        SqlBinaryOperator.LessThanOrEqual => ShardOperator.LessThanOrEqual,
        _ => null,
    };

    // 参数在左边时把比较反过来：? < 列 等于 列 > ?
    private static ShardOperator Flip(ShardOperator op) => op switch
    {
        ShardOperator.GreaterThan => ShardOperator.LessThan,
        ShardOperator.GreaterThanOrEqual => ShardOperator.LessThanOrEqual,
        ShardOperator.LessThan => ShardOperator.GreaterThan,
        ShardOperator.LessThanOrEqual => ShardOperator.GreaterThanOrEqual,
        _ => op,
    };
}
