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
                // 与字面量 null 的相等比较是判空（Java 的 isNull / isNotNull），不是值，不过滤；变量的值为 null 才过滤
                if (c.NodeType is ExpressionType.Equal or ExpressionType.NotEqual && (IsNullLiteral(c.Left) || IsNullLiteral(c.Right))) return e;
                return Rejected(c.Left, filter) || Rejected(c.Right, filter) ? null : e;
            case MethodCallExpression range when Sql.RangeKind(range.Method) is var (lowerOpen, upperOpen):
                return RewriteRange(range, lowerOpen, upperOpen, filter);
            case MethodCallExpression { Method.Name: nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith), Object: not null } m
                when m.Method.DeclaringType == typeof(string):
                return m.Arguments.Any(a => Rejected(a, filter)) ? null : e;
            case MethodCallExpression { Method.Name: nameof(Sql.LikeContains) or nameof(Sql.LikeStartsWith) or nameof(Sql.LikeEndsWith) } like
                when like.Method.DeclaringType == typeof(Sql):
                return Rejected(like.Arguments[1], filter) ? null : e;
            default:
                return e;
        }
    }

    /// <summary>过滤后去掉的部分里是否用到了导航（参数上实体类型的成员，如 <c>x.User.Name</c>、<c>x.Cards.Count()</c>）。</summary>
    public static bool DroppedNavigation(LambdaExpression original, LambdaExpression? filtered) =>
        NavigationUsage.Count(original.Body) > (filtered is null ? 0 : NavigationUsage.Count(filtered.Body));

    // 区间：两端都保留时不变；只剩一端时退化成单个比较（不加括号，与 Java 一致）；两端都被拒绝时去掉
    private static Expression? RewriteRange(MethodCallExpression range, bool lowerOpen, bool upperOpen, ValueFilter filter)
    {
        var (value, lower, upper) = (range.Arguments[0], range.Arguments[1], range.Arguments[2]);
        var keepLower = !Rejected(lower, filter);
        var keepUpper = !Rejected(upper, filter);
        if (keepLower && keepUpper) return range;
        if (keepLower) return Compare(value, lower, lowerOpen ? ExpressionType.GreaterThan : ExpressionType.GreaterThanOrEqual);
        if (keepUpper) return Compare(value, upper, upperOpen ? ExpressionType.LessThan : ExpressionType.LessThanOrEqual);
        return null;
    }

    // 字符串没有比较运算符，改写成 string.Compare(a, b) op 0（翻译器认得这个形状）
    private static Expression Compare(Expression left, Expression right, ExpressionType op)
    {
        if (left.Type != typeof(string)) return Expression.MakeBinary(op, left, right);
        var compare = Expression.Call(typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!, left, right);
        return Expression.MakeBinary(op, compare, Expression.Constant(0));
    }

    private static bool IsNullLiteral(Expression e) => ExpressionTranslator.StripConvert(e) is ConstantExpression { Value: null };

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
