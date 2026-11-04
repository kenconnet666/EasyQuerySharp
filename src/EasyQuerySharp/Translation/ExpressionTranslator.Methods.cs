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
        if (method.DeclaringType == typeof(Sql.Over)) return TranslateWindow(call);
        if (method.DeclaringType is { } declaring && IsGroupType(declaring)) return TranslateGroupAggregate(call);
        if (TryTranslateCollection(call, out var collection)) return collection;
        if (TryTranslateCollectionStringJoin(call, out var joined)) return joined;
        if (method.DeclaringType == typeof(string) && method.Name is nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith) && call.Object is not null)
            return TranslateString(call);
        if (Functions.MethodTranslators.TryTranslate(this, call, out var function)) return function;
        if (TryExpandClientCollection(call, out var expanded)) return expanded;
        if (method.Name == "Contains" && TryTranslateCollectionContains(call, out var inExpr)) return inExpr;
        if (method.Name == nameof(Equals) && call.Arguments.Count == 1 && call.Object is not null)
        {
            var (l, r) = TranslatePair(call.Object, call.Arguments[0]);
            return new BinaryExpr(l, SqlBinaryOperator.Equal, r);
        }
        // 无参 ToString()：数字、日期等转字符串（任意类型的 ToString 都是同一个形状，不进函数表）
        if (method.Name == nameof(ToString) && call.Arguments.Count == 0 && call.Object is not null)
            return new CastExpr(Translate(call.Object), typeof(string));

        throw Unsupported(call);
    }

    private SqlExpr TranslateSqlMarker(MethodCallExpression call)
    {
        if (Sql.RangeKind(call.Method) is var (lowerOpen, upperOpen)) return TranslateRange(call, lowerOpen, upperOpen);
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
                    ? array.Expressions.Select(RawArgument).ToList()
                    : ((object?[])ClientEvaluator.Evaluate(call.Arguments[1])!).Select(v => ValueArgument(v)).ToList();
                return new FragmentExpr(template, args, isPredicate: call.Type == typeof(bool));
            }
            case nameof(Sql.Count) when call.Arguments.Count == 0:
                return new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]);
            case nameof(Sql.Count):
                return Aggregate(call, SqlFunctionNames.Count, LiteralExpr.Null);
            case nameof(Sql.CountDistinct):
                return Aggregate(call, SqlFunctionNames.CountDistinct, LiteralExpr.Null);
            // SUM 的过滤用 ELSE 0（没有满足条件的行时和为 0），其余聚合用 ELSE NULL（与 Java 版一致）
            case nameof(Sql.StringJoin):
                return new FunctionExpr(SqlFunctionNames.StringJoin, [Translate(call.Arguments[0]), Translate(call.Arguments[1])]);
            case nameof(Sql.Sum):
                return Aggregate(call, SqlFunctionNames.Sum, new ParameterExpr(0L, typeof(long)));
            case nameof(Sql.SumDistinct):
                return Aggregate(call, SqlFunctionNames.SumDistinct, new ParameterExpr(0L, typeof(long)));
            case nameof(Sql.Max):
                return Aggregate(call, SqlFunctionNames.Max, LiteralExpr.Null);
            case nameof(Sql.Min):
                return Aggregate(call, SqlFunctionNames.Min, LiteralExpr.Null);
            case nameof(Sql.Avg):
                return Aggregate(call, SqlFunctionNames.Avg, LiteralExpr.Null);
            case nameof(Sql.AvgDistinct):
                return Aggregate(call, SqlFunctionNames.AvgDistinct, LiteralExpr.Null);
            case nameof(Sql.EqualsWith):
            {
                var (a, b) = TranslatePair(call.Arguments[0], call.Arguments[1]);
                return new FunctionExpr(SqlFunctionNames.EqualsWith, [a, b]);
            }
            case nameof(Sql.Exists):
            case nameof(Sql.NotExists):
                return TranslateExists(call.Arguments[0], call.Method.Name == nameof(Sql.NotExists));
            case nameof(Sql.Group):
            {
                var inner = TranslatePredicate(call.Arguments[0]);
                return inner is LogicalExpr { IsAnd: true } and ? LogicalExpr.Grouped(and.Operands) : inner;
            }
            case nameof(Sql.Now):
                return new FunctionExpr(SqlFunctionNames.Now, []);
            case nameof(Sql.UtcNow):
                return new FunctionExpr(SqlFunctionNames.UtcNow, []);
            case nameof(Sql.IsoDayOfWeek):
                return new FunctionExpr(SqlFunctionNames.DatePartOf(DatePart.IsoDayOfWeek), [Translate(call.Arguments[0])]);
            case nameof(Sql.Cast):
                return new CastExpr(Translate(StripConvert(call.Arguments[0])), call.Method.GetGenericArguments()[0]);
            case nameof(Sql.SubQuery):
                return new SubQueryExpr(TranslateSubQuery(call.Arguments[0]).Query);
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

    // 区间：两端都是值、左侧是函数且为闭区间时用 BETWEEN（Java 对函数走原生片段），否则是带括号的两个比较
    private SqlExpr TranslateRange(MethodCallExpression call, bool lowerOpen, bool upperOpen)
    {
        var value = Translate(call.Arguments[0]);
        var (lowerArg, upperArg) = (StripConvert(call.Arguments[1]), StripConvert(call.Arguments[2]));
        var lower = Translate(lowerArg);
        var upper = Translate(upperArg);
        var boundsAreValues = !ClientEvaluator.MustTranslate(lowerArg) && !ClientEvaluator.MustTranslate(upperArg);
        if (value is not ColumnExpr && boundsAreValues && !lowerOpen && !upperOpen)
            return new FragmentExpr("{0} BETWEEN {1} AND {2}", [value, lower, upper], isPredicate: true);
        return LogicalExpr.Grouped([
            new BinaryExpr(value, lowerOpen ? SqlBinaryOperator.GreaterThan : SqlBinaryOperator.GreaterThanOrEqual, lower),
            new BinaryExpr(value, upperOpen ? SqlBinaryOperator.LessThan : SqlBinaryOperator.LessThanOrEqual, upper),
        ]);
    }

    // 原生片段的参数：列 / 表达式照常翻译；客户端集合展开成 ?,?,?（对应 Java 的 collection(…)）
    private SqlExpr RawArgument(Expression argument) =>
        ClientEvaluator.MustTranslate(StripConvert(argument)) ? Translate(StripConvert(argument)) : ValueArgument(ClientEvaluator.Evaluate(argument));

    private static SqlExpr ValueArgument(object? value)
    {
        if (value is IEnumerable items and not string and not byte[])
        {
            var parameters = items.Cast<object?>().Select(v => (SqlExpr)Parameter(v, v?.GetType() ?? typeof(object))).ToList();
            return new FragmentExpr(string.Join(",", parameters.Select((_, i) => "{" + i + "}")), parameters);
        }
        return Parameter(value, value?.GetType() ?? typeof(object));
    }

    // 聚合：第二个参数是过滤条件时，值包进 CASE WHEN filter THEN x ELSE … END
    private FunctionExpr Aggregate(MethodCallExpression call, string function, SqlExpr otherwise)
    {
        var value = Translate(call.Arguments[0]);
        if (call.Arguments.Count == 2) value = new CaseExpr([(TranslatePredicate(call.Arguments[1]), value)], otherwise);
        return new FunctionExpr(function, [value]);
    }

    // string 的实例方法：Contains / StartsWith / EndsWith 翻译成 LIKE，值在客户端拼好 %（与 Java 版的 like 一致）
    private SqlExpr TranslateString(MethodCallExpression call)
    {
        switch (call.Method.Name)
        {
            case nameof(string.Contains) when call.Object is not null && call.Arguments.Count >= 1:
                return LikeOf(call.Object, call.Arguments[0], LikeMode.Contains);
            case nameof(string.StartsWith) when call.Object is not null && call.Arguments.Count >= 1:
                return LikeOf(call.Object, call.Arguments[0], LikeMode.StartsWith);
            case nameof(string.EndsWith) when call.Object is not null && call.Arguments.Count >= 1:
                return LikeOf(call.Object, call.Arguments[0], LikeMode.EndsWith);
            default:
                throw Unsupported(call);
        }
    }

    // Pattern 是拼好 % 的通用写法；同时带上原值与匹配方式，供方言改写（PG 的 ESCAPE、Oracle / SQLite 的 INSTR）
    private LikeExpr LikeOf(Expression target, Expression valueExpr, LikeMode mode)
    {
        var column = Translate(target);
        var (prefix, suffix) = mode switch
        {
            LikeMode.Contains => ("%", "%"),
            LikeMode.StartsWith => ("", "%"),
            _ => ("%", ""),
        };
        if (ClientEvaluator.MustTranslate(valueExpr))
        {
            // 模式本身是列：CONCAT('%',col,'%')
            var value = Translate(valueExpr);
            var parts = new List<SqlExpr>();
            if (prefix.Length > 0) parts.Add(new LiteralExpr("'%'"));
            parts.Add(value);
            if (suffix.Length > 0) parts.Add(new LiteralExpr("'%'"));
            return new LikeExpr(column, new FunctionExpr(SqlFunctionNames.Concat, parts), false, mode, value);
        }
        var text = ClientEvaluator.Evaluate(valueExpr)?.ToString();
        // 支持 LIKE 的加密列：值先按同样的分段规则加密，再加 %（与 Java 的 LikeFunctionTemplateGenerator 一致）
        if (column is ColumnExpr { Column.Converter: Encryption.EncryptionConverter { SupportQueryLike: true } encryption } && text is not null)
            text = (string?)encryption.ToProvider(text);
        return new LikeExpr(column, new ParameterExpr(prefix + text + suffix, typeof(string)), false, mode, new ParameterExpr(text, typeof(string)));
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

        if (ClientEvaluator.MustTranslate(source)) return false;
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
