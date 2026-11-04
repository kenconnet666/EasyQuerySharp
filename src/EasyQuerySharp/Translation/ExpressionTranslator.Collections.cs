using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>客户端集合参与的翻译：Contains 转 IN（超长时分组）、Any / All 展开成 OR / AND。</summary>
internal sealed partial class ExpressionTranslator
{
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

        if (ClientEvaluator.MustTranslate(source)) return false;
        var operand = Translate(item);
        result = InList(operand, ValuesOf(source, operand), negated: false);
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
        if (ClientEvaluator.MustTranslate(call.Arguments[0])) return false;

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

    /// <summary>
    /// 值列表的 IN：超过 <see cref="EasyDbOptions.MaxInClauseSize"/> 时按上限分组，写成 <c>(x IN (…) OR x IN (…))</c>，
    /// NOT IN 写成 <c>(x NOT IN (…) AND x NOT IN (…))</c>（对应 Java 的 maxInClauseSize，Oracle 等有 IN 列表上限的库要用）。
    /// </summary>
    private SqlExpr InList(SqlExpr operand, IReadOnlyList<SqlExpr> values, bool negated)
    {
        var max = Context?.Db?.Options.MaxInClauseSize ?? 0;
        if (max <= 0 || values.Count <= max) return new InExpr(operand, values, negated);
        var parts = values.Chunk(max).Select(chunk => (SqlExpr)new InExpr(operand, chunk, negated)).ToList();
        return negated ? LogicalExpr.Grouped(parts) : LogicalExpr.GroupedOr(parts);
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
}
