using System.Linq.Expressions;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Filtering;

/// <summary>
/// 条件值过滤（对应 Java 的 filterConfigure(ValueFilter)）：开启后，之后追加的 Where 里值不满足要求的比较条件被整个跳过，
/// 适合"参数为空就不加这个条件"的查询页面：<c>.UseValueFilter(ValueFilter.NotNullOrBlank).Where(u => u.Name == name)</c>。
/// </summary>
public enum ValueFilter
{
    /// <summary>不过滤（默认，对应 Java 的 AnyValueFilter）。</summary>
    All,

    /// <summary>值为 null 的条件跳过。</summary>
    NotNull,

    /// <summary>值为 null 或空白字符串的条件跳过（对应 Java 的 NotNullOrEmptyValueFilter）。</summary>
    NotNullOrBlank,
}

/// <summary>
/// 按 <see cref="ValueFilter"/> 改写 Where 的 lambda：比较、字符串匹配里客户端一侧的值被拒绝时，这个条件去掉；
/// AND / OR 里去掉一侧就只剩另一侧，整个条件都去掉时返回 null（不加 WHERE）。
/// </summary>
internal static class ValueFilterRewriter
{
    public static LambdaExpression? Apply(LambdaExpression predicate, ValueFilter filter) =>
        filter == ValueFilter.All ? predicate
        : Rewrite(predicate.Body, filter) is { } body ? Expression.Lambda(predicate.Type, body, predicate.Parameters) : null;

    private static Expression? Rewrite(Expression e, ValueFilter filter)
    {
        switch (e)
        {
            case BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.OrElse } b:
            {
                var left = Rewrite(b.Left, filter);
                var right = Rewrite(b.Right, filter);
                if (left is null) return right;
                if (right is null) return left;
                return b.NodeType == ExpressionType.AndAlso ? Expression.AndAlso(left, right) : Expression.OrElse(left, right);
            }
            case UnaryExpression { NodeType: ExpressionType.Not } n:
                return Rewrite(n.Operand, filter) is { } operand ? Expression.Not(operand) : null;
            case BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual } c:
                return Rejected(c.Left, filter) || Rejected(c.Right, filter) ? null : e;
            case MethodCallExpression { Method.Name: nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith), Object: not null } m
                when m.Method.DeclaringType == typeof(string):
                return m.Arguments.Any(a => Rejected(a, filter)) ? null : e;
            default:
                return e;
        }
    }

    // 只看客户端一侧（不需要翻译成 SQL 的那一侧）的值
    private static bool Rejected(Expression side, ValueFilter filter)
    {
        if (ClientEvaluator.MustTranslate(side)) return false;
        return ClientEvaluator.Evaluate(side) switch
        {
            null => true,
            string s => filter == ValueFilter.NotNullOrBlank && string.IsNullOrWhiteSpace(s),
            _ => false,
        };
    }
}
