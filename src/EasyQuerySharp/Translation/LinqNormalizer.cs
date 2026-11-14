using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 把几种 LINQ 写法换成翻译器已经支持的等价形状，之后的翻译、规划只认这几种形状：
/// <list type="bullet">
/// <item><c>list.Exists(p)</c> → <c>list.Any(p)</c>；</item>
/// <item><c>xs.Select(sel).Contains(v)</c> → <c>xs.Any(x =&gt; sel(x) == v)</c>（集合要在 SQL 里求值，客户端集合照旧是 IN）；</item>
/// <item><c>xs.MaxBy(sel)</c> / <c>MinBy</c> → <c>xs.OrderByDescending(sel).FirstOrDefault()</c> / <c>OrderBy</c>；</item>
/// <item><c>xs.OrderBy(..).Last()</c> / <c>LastOrDefault()</c> → 排序全部反过来再取 <c>First()</c> / <c>FirstOrDefault()</c>（没有排序时不改写，照样报不支持）。</item>
/// </list>
/// </summary>
internal sealed class LinqNormalizer : ExpressionVisitor
{
    private static readonly LinqNormalizer Instance = new();

    /// <summary>改写 <paramref name="expression"/> 里所有能改写的地方。</summary>
    public static T Normalize<T>(T expression) where T : Expression => (T)Instance.Visit(expression);

    /// <summary>只看这一个节点（及成员访问的对象）：能改写时返回改写后的，否则原样返回。</summary>
    public static Expression Rewrite(Expression node) => node switch
    {
        MethodCallExpression call => RewriteCall(call) ?? node,
        MemberExpression { Expression: MethodCallExpression inner } member when RewriteCall(inner) is { } rewritten => member.Update(rewritten),
        _ => node,
    };

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var visited = (MethodCallExpression)base.VisitMethodCall(node);
        return RewriteCall(visited) is { } rewritten ? Visit(rewritten) : visited;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2060", Justification = AotJustification.Marker)]
    private static MethodCallExpression? RewriteCall(MethodCallExpression call)
    {
        var method = call.Method;
        // List<T>.Exists(p) / Array.Exists(arr, p)
        if (method.Name == nameof(List<object>.Exists) && (method.DeclaringType is { IsGenericType: true } owner && owner.GetGenericTypeDefinition() == typeof(List<>) || method.DeclaringType == typeof(Array)))
        {
            var (source, predicate) = call.Object is { } list ? (list, call.Arguments[0]) : (call.Arguments[0], call.Arguments[1]);
            var element = ElementType(source.Type);
            return Expression.Call(LinqMethod(nameof(System.Linq.Enumerable.Any), 2).MakeGenericMethod(element), source, Lambda(predicate, element));
        }
        if (method.DeclaringType != typeof(System.Linq.Enumerable)) return null;
        switch (method.Name)
        {
            // xs.Select(sel).Contains(v)：要在 SQL 里求值的集合（导航）才改写
            case nameof(System.Linq.Enumerable.Contains) when call.Arguments.Count == 2
                                                       && call.Arguments[0] is MethodCallExpression { Method.Name: nameof(System.Linq.Enumerable.Select), Arguments.Count: 2 } select
                                                       && select.Method.DeclaringType == typeof(System.Linq.Enumerable) && ClientEvaluator.MustTranslate(select.Arguments[0])
                                                       && ExpressionTranslator.StripConvert(select.Arguments[1]) is LambdaExpression { Parameters.Count: 1 } selector:
            {
                var source = select.Arguments[0];
                var element = selector.Parameters[0].Type;
                var body = Expression.Equal(selector.Body, Expression.Convert(call.Arguments[1], selector.Body.Type));
                return Expression.Call(LinqMethod(nameof(System.Linq.Enumerable.Any), 2).MakeGenericMethod(element), source, Expression.Lambda(body, selector.Parameters));
            }
            // MaxBy / MinBy → 排序后取第一个（没有元素时与 MaxBy 一样是 null）
            case nameof(System.Linq.Enumerable.MaxBy) or nameof(System.Linq.Enumerable.MinBy) when call.Arguments.Count == 2:
            {
                var element = method.GetGenericArguments()[0];
                var key = method.GetGenericArguments()[1];
                var order = method.Name == nameof(System.Linq.Enumerable.MaxBy) ? nameof(System.Linq.Enumerable.OrderByDescending) : nameof(System.Linq.Enumerable.OrderBy);
                var ordered = Expression.Call(LinqMethod(order, 2).MakeGenericMethod(element, key), call.Arguments[0], call.Arguments[1]);
                return Expression.Call(LinqMethod(nameof(System.Linq.Enumerable.FirstOrDefault), 1).MakeGenericMethod(element), ordered);
            }
            // OrderBy(..).Last() / LastOrDefault()：排序反过来取第一个
            case nameof(System.Linq.Enumerable.Last) or nameof(System.Linq.Enumerable.LastOrDefault) when call.Arguments.Count == 1 && Reversed(call.Arguments[0]) is { } reversed:
            {
                var element = method.GetGenericArguments()[0];
                var first = method.Name == nameof(System.Linq.Enumerable.Last) ? nameof(System.Linq.Enumerable.First) : nameof(System.Linq.Enumerable.FirstOrDefault);
                return Expression.Call(LinqMethod(first, 1).MakeGenericMethod(element), reversed);
            }
            default:
                return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2060", Justification = AotJustification.Marker)]
    // 链上的 OrderBy / ThenBy 全部反过来；链上没有排序时返回 null（Last 没有确定的顺序）
    private static Expression? Reversed(Expression source)
    {
        if (source is not MethodCallExpression call || call.Method.DeclaringType != typeof(System.Linq.Enumerable)) return null;
        var opposite = call.Method.Name switch
        {
            nameof(System.Linq.Enumerable.OrderBy) => nameof(System.Linq.Enumerable.OrderByDescending),
            nameof(System.Linq.Enumerable.OrderByDescending) => nameof(System.Linq.Enumerable.OrderBy),
            nameof(System.Linq.Enumerable.ThenBy) => nameof(System.Linq.Enumerable.ThenByDescending),
            nameof(System.Linq.Enumerable.ThenByDescending) => nameof(System.Linq.Enumerable.ThenBy),
            _ => null,
        };
        if (opposite is null || call.Arguments.Count != 2) return null;
        // ThenBy 的前面还有排序：一起反过来；OrderBy 是排序的起点，前面的不动
        var inner = call.Method.Name.StartsWith("Then", StringComparison.Ordinal) ? Reversed(call.Arguments[0]) : call.Arguments[0];
        if (inner is null) return null;
        return Expression.Call(LinqMethod(opposite, 2).MakeGenericMethod(call.Method.GetGenericArguments()), inner, call.Arguments[1]);
    }

    private static readonly MethodInfo[] EnumerableMethods = typeof(System.Linq.Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static);

    // Enumerable 上的泛型方法定义：按名字与参数个数（第二个参数是 Func<,> 的那个重载）
    private static MethodInfo LinqMethod(string name, int parameters) =>
        EnumerableMethods.First(m => m.Name == name && m.GetParameters().Length == parameters
                                     && (parameters == 1 || m.GetParameters()[1].ParameterType is { IsGenericType: true } p && p.GetGenericTypeDefinition() is var d
                                         && (d == typeof(Func<,>))));

    // Exists 的参数是 Predicate<T>：换成 Func<T, bool> 的 lambda
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression Lambda(Expression predicate, Type element)
    {
        var lambda = (LambdaExpression)ExpressionTranslator.StripConvert(predicate);
        return Expression.Lambda(typeof(Func<,>).MakeGenericType(element, typeof(bool)), lambda.Body, lambda.Parameters);
    }

    private static Type ElementType(Type collection) =>
        collection.IsArray ? collection.GetElementType()! : collection.GetGenericArguments()[0];
}
