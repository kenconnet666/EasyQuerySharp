using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 在客户端求值不依赖查询参数的子表达式（闭包变量、常量、属性链），结果作为 SQL 参数。
/// 常见形状（常量、字段 / 属性链、转换、数组下标、new[]{…}）直接反射取值，不走 <c>Compile()</c>（见 0001 的性能对策）；
/// 其它形状（方法调用等，少见）退回编译执行。
/// </summary>
internal static class ClientEvaluator
{
    /// <summary>
    /// 表达式是否必须翻译成 SQL：引用了查询参数（<see cref="ParameterExpression"/>），或调用了 <see cref="Sql"/> 的标记方法
    /// （如不带参数的 <c>Sql.Raw("RAND()")</c>、<c>Sql.SubQuery(…)</c>，它们只能在 SQL 里有意义，不能在客户端求值）。
    /// </summary>
    public static bool MustTranslate(Expression expression) => ParameterFinder.Contains(expression);

    /// <summary>求值；调用方保证 <see cref="MustTranslate"/> 为 false。</summary>
    public static object? Evaluate(Expression expression)
    {
        switch (expression)
        {
            case ConstantExpression c:
                return c.Value;
            case MemberExpression m:
                var target = m.Expression is null ? null : Evaluate(m.Expression);
                return m.Member switch
                {
                    FieldInfo f => f.GetValue(target),
                    PropertyInfo p => p.GetValue(target),
                    _ => Compiled(expression),
                };
            case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u
                when u.Method is null:
                return ConvertValue(Evaluate(u.Operand), u.Type);
            case UnaryExpression { NodeType: ExpressionType.Quote } q:
                return q.Operand;
            case BinaryExpression { NodeType: ExpressionType.ArrayIndex } b:
                return ((Array)Evaluate(b.Left)!).GetValue(Convert.ToInt64(Evaluate(b.Right), System.Globalization.CultureInfo.InvariantCulture));
            case NewArrayExpression { NodeType: ExpressionType.NewArrayInit } a:
                var array = Array.CreateInstance(a.Type.GetElementType()!, a.Expressions.Count);
                for (var i = 0; i < a.Expressions.Count; i++) array.SetValue(Evaluate(a.Expressions[i]), i);
                return array;
            default:
                return Compiled(expression);
        }
    }

    // 只做装箱值的类型调整：可空 / 枚举 / 数值拓宽；引用类型原样返回
    private static object? ConvertValue(object? value, Type type)
    {
        if (value is null) return null;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum) return Enum.ToObject(target, value);
        if (value is Enum && IsNumeric(target)) return Convert.ChangeType(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture), target, System.Globalization.CultureInfo.InvariantCulture);
        if (value is IConvertible && IsNumeric(target)) return Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    private static bool IsNumeric(Type t) => Type.GetTypeCode(t) is >= TypeCode.SByte and <= TypeCode.Decimal;

    private static object? Compiled(Expression expression)
    {
        var fn = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile();
        return fn();
    }

    /// <summary>查找表达式里是否有参数引用（lambda 自己声明的参数除外）或 <see cref="Sql"/> 标记方法调用。</summary>
    private sealed class ParameterFinder : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _declared = [];
        private bool _found;

        public static bool Contains(Expression e)
        {
            var finder = new ParameterFinder();
            finder.Visit(e);
            return finder._found;
        }

        public override Expression? Visit(Expression? node) => _found ? node : base.Visit(node);

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            foreach (var p in node.Parameters) _declared.Add(p);
            return base.VisitLambda(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_declared.Contains(node)) _found = true;
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Sql)) _found = true;
            return base.VisitMethodCall(node);
        }
    }
}
