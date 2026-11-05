using System.Collections;
using System.Linq.Expressions;
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
        // 用户声明的模板函数：[SqlFunction("…{0}…")]
        if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SqlFunctionAttribute>(method) is { } userFunction)
            return new FragmentExpr(userFunction.Template, call.Arguments.Select(RawArgument).ToList(), isPredicate: call.Type == typeof(bool));
        // Map 投影之后再过滤：d["aa"] 是派生表里别名为 aa 的列（Java 的 o.get("aa")）
        if (method.Name == "get_Item" && call is { Object: { } map, Arguments: [var keyArg] } && BindingOf(map) is DerivedBinding derived
            && ClientEvaluator.Evaluate(keyArg) is string key)
            return derived.MemberColumns.TryGetValue(key, out var mapColumn)
                ? new ColumnExpr(derived.Table, mapColumn)
                : throw Unsupported(call, $"Map 投影里没有键 {key}");
        if (TryTranslateCollection(call, out var collection)) return collection;
        if (TryTranslateCollectionStringJoin(call, out var joined)) return joined;
        if (method.DeclaringType == typeof(string) && method.Name is nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith) && call.Object is not null)
            return TranslateString(call);
        if (MethodTranslators.TryTranslate(this, call, out var function)) return function;
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
            case nameof(Sql.Random):
                return new FunctionExpr(SqlFunctionNames.Random, []);
            case nameof(Sql.IsoDayOfWeek):
                return new FunctionExpr(SqlFunctionNames.DatePartOf(DatePart.IsoDayOfWeek), [Translate(call.Arguments[0])]);
            case nameof(Sql.Cast):
                return new CastExpr(Translate(StripConvert(call.Arguments[0])), call.Method.GetGenericArguments()[0]);
            case nameof(Sql.Column):
                return Translate(ColumnPath(StripConvert(call.Arguments[0]), ClientEvaluator.Evaluate(call.Arguments[1]) as string
                    ?? throw Unsupported(call, "Sql.Column 的属性路径必须是字符串")));
            case nameof(Sql.SubQuery):
            {
                var state = ReplayQuery(call.Arguments[0]);
                return new SubQueryExpr(Querying.QueryCompiler.CompileSelect(state, _scope, Context).Query) { ReserveExtraAlias = !SelectsBareCount(state) };
            }
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
                return InList(operand, values, call.Method.Name == nameof(Sql.NotIn));
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

    // Sql.Column(entity, "a.b")：沿属性路径（忽略大小写）构造 entity.A.B，之后与手写的成员访问走同一条翻译
    private static Expression ColumnPath(Expression entity, string path)
    {
        var current = entity;
        foreach (var segment in path.Split('.'))
        {
            var property = current.Type.GetProperty(segment, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
                           ?? throw new NotSupportedException($"{current.Type.Name} 上没有属性 {segment}（路径 {path}）");
            current = Expression.Property(current, property);
        }
        return current;
    }

    private static bool IsQueryType(Type type) => typeof(Querying.IQueryStateAccessor).IsAssignableFrom(type);

    // Java 的显式子查询：selectCount() 直接计数；selectColumn(…) 会多建一张匿名表，之后的表别名编号跳过一个
    private static bool SelectsBareCount(Querying.QueryState state) =>
        state.Selector?.Body is { } body
        && StripConvert(body) is MethodCallExpression { Method.Name: nameof(Sql.Count), Arguments.Count: 0 } count
        && count.Method.DeclaringType == typeof(Sql);
}
