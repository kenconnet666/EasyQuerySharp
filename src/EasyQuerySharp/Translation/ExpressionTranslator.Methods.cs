using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    private SqlExpr TranslateCall(MethodCallExpression call)
    {
        var method = call.Method;

        if (method.DeclaringType == typeof(Sql)) return TranslateSqlMarker(call);
        if (method.DeclaringType == typeof(string)) return TranslateString(call);
        if (TryExpandClientCollection(call, out var expanded)) return expanded;
        if (method.Name == "Contains" && TryTranslateCollectionContains(call, out var inExpr)) return inExpr;
        if (method.Name == nameof(Equals) && call.Arguments.Count == 1 && call.Object is not null)
        {
            var (l, r) = TranslatePair(call.Object, call.Arguments[0]);
            return new BinaryExpr(l, SqlBinaryOperator.Equal, r);
        }

        throw Unsupported(call);
    }

    private SqlExpr TranslateSqlMarker(MethodCallExpression call)
    {
        switch (call.Method.Name)
        {
            case nameof(Sql.Like):
            case nameof(Sql.NotLike):
            {
                var (value, pattern) = TranslatePair(call.Arguments[0], call.Arguments[1]);
                return new LikeExpr(value, pattern, call.Method.Name == nameof(Sql.NotLike));
            }
            case nameof(Sql.Raw):
            {
                var template = (string)ClientEvaluator.Evaluate(call.Arguments[0])!;
                var args = call.Arguments[1] is NewArrayExpression array
                    ? array.Expressions.Select(Translate).ToList()
                    : ((object?[])ClientEvaluator.Evaluate(call.Arguments[1])!).Select(v => (SqlExpr)Parameter(v, v?.GetType() ?? typeof(object))).ToList();
                return new FragmentExpr(template, args);
            }
            case nameof(Sql.Exists):
            case nameof(Sql.NotExists):
                return TranslateExists(call.Arguments[0], call.Method.Name == nameof(Sql.NotExists));
            case nameof(Sql.In) or nameof(Sql.NotIn) when IsQueryType(call.Arguments[1].Type):
            {
                var operand = Translate(call.Arguments[0]);
                var (sub, _) = TranslateSubQuery(call.Arguments[1]);
                return new InExpr(operand, sub, call.Method.Name == nameof(Sql.NotIn));
            }
            case nameof(Sql.In):
            case nameof(Sql.NotIn):
            {
                var operand = Translate(call.Arguments[0]);
                var values = ValuesOf(call.Arguments[1], operand);
                return new InExpr(operand, values, call.Method.Name == nameof(Sql.NotIn));
            }
            default:
                throw Unsupported(call);
        }
    }

    // string 的实例方法：Contains / StartsWith / EndsWith 翻译成 LIKE，值在客户端拼好 %（与 Java 版的 like 一致）
    private SqlExpr TranslateString(MethodCallExpression call)
    {
        switch (call.Method.Name)
        {
            case nameof(string.Contains) when call.Object is not null && call.Arguments.Count >= 1:
                return LikeOf(call.Object, call.Arguments[0], "%", "%");
            case nameof(string.StartsWith) when call.Object is not null && call.Arguments.Count >= 1:
                return LikeOf(call.Object, call.Arguments[0], "", "%");
            case nameof(string.EndsWith) when call.Object is not null && call.Arguments.Count >= 1:
                return LikeOf(call.Object, call.Arguments[0], "%", "");
            default:
                throw Unsupported(call);
        }
    }

    private LikeExpr LikeOf(Expression target, Expression valueExpr, string prefix, string suffix)
    {
        var column = Translate(target);
        if (ClientEvaluator.DependsOnParameters(valueExpr))
        {
            // 模式本身是列：CONCAT('%',col,'%')
            var parts = new List<SqlExpr>();
            if (prefix.Length > 0) parts.Add(new LiteralExpr("'%'"));
            parts.Add(Translate(valueExpr));
            if (suffix.Length > 0) parts.Add(new LiteralExpr("'%'"));
            return new LikeExpr(column, new FunctionExpr(SqlFunctionNames.Concat, parts), false);
        }
        var value = ClientEvaluator.Evaluate(valueExpr)?.ToString();
        return new LikeExpr(column, new ParameterExpr(prefix + value + suffix, typeof(string)), false);
    }

    /// <summary>
    /// 集合的 Contains 翻译成 IN。三种形状：<c>Enumerable.Contains(src, x)</c>、实例方法 <c>list.Contains(x)</c>，
    /// 以及 C# 14 first-class span 下数组绑定到的 <c>MemoryExtensions.Contains(ReadOnlySpan&lt;T&gt;, T)</c>（参数外包一层 op_Implicit）。
    /// </summary>
    private bool TryTranslateCollectionContains(MethodCallExpression call, out SqlExpr result)
    {
        result = null!;
        Expression source, item;
        if (call.Object is null && call.Arguments.Count == 2)
        {
            source = UnwrapSpan(call.Arguments[0]);
            item = call.Arguments[1];
        }
        else if (call.Object is not null && call.Arguments.Count == 1 && call.Object.Type != typeof(string))
        {
            source = call.Object;
            item = call.Arguments[0];
        }
        else return false;

        if (ClientEvaluator.DependsOnParameters(source)) return false;
        var operand = Translate(item);
        result = new InExpr(operand, ValuesOf(source, operand), negated: false);
        return true;
    }

    /// <summary>
    /// 客户端集合上的 Any / All 展开成 OR / AND：<c>titles.Any(t => a.Title.Contains(t))</c> →
    /// <c>(a.title LIKE ? OR a.title LIKE ? …)</c>（对应 Java 在 or(() -> { for … }) 里循环追加条件）。
    /// 集合为空时 Any 为恒假、All 为恒真。
    /// </summary>
    private bool TryExpandClientCollection(MethodCallExpression call, out SqlExpr result)
    {
        result = null!;
        if (call.Method.DeclaringType != typeof(Enumerable) || call.Method.Name is not ("Any" or "All") || call.Arguments.Count != 2) return false;
        if (ClientEvaluator.DependsOnParameters(call.Arguments[0])) return false;

        var lambda = (LambdaExpression)StripConvert(call.Arguments[1]);
        var isAny = call.Method.Name == "Any";
        var items = new List<SqlExpr>();
        foreach (var element in (IEnumerable)ClientEvaluator.Evaluate(call.Arguments[0])!)
        {
            var body = new ParameterReplacer(lambda.Parameters[0], Expression.Constant(element, lambda.Parameters[0].Type)).Visit(lambda.Body);
            items.Add(TranslatePredicate(body));
        }
        result = items.Count == 0
            ? new LiteralExpr(isAny ? "1 = 2" : "1 = 1")
            : isAny ? LogicalExpr.Or(items) : LogicalExpr.And(items);
        return true;
    }

    /// <summary>把表达式里的某个参数替换成另一个表达式。</summary>
    internal sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }

    private static Expression UnwrapSpan(Expression e) =>
        e is MethodCallExpression { Method.Name: "op_Implicit", Arguments.Count: 1 } conv ? conv.Arguments[0] : e;

    /// <summary>把客户端集合求值成 IN 的参数列表；元素经过列的值转换器。</summary>
    private static List<SqlExpr> ValuesOf(Expression source, SqlExpr operand)
    {
        var values = ClientEvaluator.Evaluate(source) as IEnumerable
                     ?? throw new NotSupportedException($"IN 的值必须是集合：{source}");
        var elementType = ElementType(source.Type);
        var result = new List<SqlExpr>();
        foreach (var v in values) result.Add(ApplyConverter(Parameter(v, v?.GetType() ?? elementType), operand));
        return result;
    }

    private static Type ElementType(Type collection)
    {
        if (collection.IsArray) return collection.GetElementType()!;
        var enumerable = collection.GetInterfaces().Append(collection)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0] ?? typeof(object);
    }

    private static bool IsQueryType(Type type) => typeof(Querying.IQueryStateAccessor).IsAssignableFrom(type);
}
