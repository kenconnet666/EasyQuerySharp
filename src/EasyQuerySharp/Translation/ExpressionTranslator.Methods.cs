using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    private SqlExpr TranslateCall(MethodCallExpression call)
    {
        // 条件、排序里的可复用投影（投影里的已在编译前内联）
        if (Querying.ProjectionInliner.TryExpand(call) is { } inlined) return Translate(inlined);
        var method = call.Method;

        if (method.DeclaringType == typeof(JsonNode) || (method.DeclaringType == typeof(Sql) && method.Name == nameof(Sql.Json))) return TranslateJson(call);
        if (method.DeclaringType == typeof(Sql)) return TranslateSqlMarker(call);
        if (method.DeclaringType == typeof(Sql.Over)) return TranslateWindow(call);
        if (method.DeclaringType is { } entry && IsDialectEntry(entry)) return TranslateDialectEntry(call);
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
        // 无实体的表上按名字引用列：row.Get<T>("列名")（Java 的 m.get("id") / eq("id", ..)）
        if (method.DeclaringType == typeof(DynamicRow) && method.Name == nameof(DynamicRow.Get) && call.Object is { } row)
            return BindingOf(row) is { } rowBinding && ClientEvaluator.Evaluate(call.Arguments[0]) is string column
                ? new ColumnExpr(rowBinding.Table, column)
                : throw Unsupported(call, "Get 的列名必须是能在客户端求值的字符串，且只能用在 db.Table(..) 的行上");
        if (TryTranslateCollection(call, out var collection)) return collection;
        if (TryTranslateCollectionStringJoin(call, out var joined)) return joined;
        if (method.DeclaringType == typeof(string) && method.Name is nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith) && call.Object is not null)
            return TranslateString(call);
        // string.Equals(a, b, StringComparison) / a.Equals(b, StringComparison)
        if (method.DeclaringType == typeof(string) && method.Name == nameof(string.Equals) && ComparisonOf(call) is { } comparison)
        {
            var (left, right) = call.Object is null ? (call.Arguments[0], call.Arguments[1]) : (call.Object, call.Arguments[0]);
            var (l, r) = IgnoresCase(comparison) ? TranslatePair(Lower(left), Lower(right)) : TranslatePair(left, right);
            return new BinaryExpr(l, SqlBinaryOperator.Equal, r);
        }
        if (MethodTranslators.TryTranslate(this, call, out var function)) return function;
        if (TryExpandClientCollection(call, out var expanded)) return expanded;
        if (method.Name == "Contains" && TryTranslateCollectionContains(call, out var inExpr)) return inExpr;
        if (method.Name == nameof(Equals) && call.Arguments.Count == 1 && call.Object is not null)
        {
            var (l, r) = TranslatePair(call.Object, call.Arguments[0]);
            return new BinaryExpr(l, SqlBinaryOperator.Equal, r);
        }
        // x.GetValueOrDefault() / GetValueOrDefault(v)：COALESCE(x, 类型的默认值 / v)
        if (method.Name == nameof(Nullable<int>.GetValueOrDefault) && call.Object is { } nullable && Nullable.GetUnderlyingType(nullable.Type) is { } valueType)
        {
            var fallback = call.Arguments.Count == 1 ? Translate(call.Arguments[0]) : new ParameterExpr(RuntimeHelpers.GetUninitializedObject(valueType), valueType);
            return new FunctionExpr(SqlFunctionNames.NullOrDefault, [Translate(nullable), fallback]);
        }
        // object.Equals(a, b)：两个值相等（两边按各自的类型翻译）
        if (method.DeclaringType == typeof(object) && method.Name == nameof(Equals) && method.IsStatic && call.Arguments.Count == 2)
        {
            var (l, r) = TranslatePair(StripConvert(call.Arguments[0]), StripConvert(call.Arguments[1]));
            return new BinaryExpr(l, SqlBinaryOperator.Equal, r);
        }
        // 枚举的 ToString() 是成员名：按名字存的列就是它本身，按数值存的列写成 CASE（与 C# 一致，不是数字）
        if (method.Name == nameof(ToString) && call.Arguments.Count == 0 && call.Object is not null
            && (Nullable.GetUnderlyingType(StripConvert(call.Object).Type) ?? StripConvert(call.Object).Type) is { IsEnum: true } enumType)
            return EnumName(Translate(StripConvert(call.Object)), enumType, Nullable.GetUnderlyingType(StripConvert(call.Object).Type) is not null);
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
            case nameof(Sql.LikeContains):
                return LikeOf(call.Arguments[0], call.Arguments[1], LikeMode.Contains, concat: true);
            case nameof(Sql.LikeStartsWith):
                return LikeOf(call.Arguments[0], call.Arguments[1], LikeMode.StartsWith, concat: true);
            case nameof(Sql.LikeEndsWith):
                return LikeOf(call.Arguments[0], call.Arguments[1], LikeMode.EndsWith, concat: true);
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
            case nameof(Sql.MaxColumns):
            case nameof(Sql.MinColumns):
                return new FunctionExpr(call.Method.Name == nameof(Sql.MaxColumns) ? SqlFunctionNames.MaxColumns : SqlFunctionNames.MinColumns,
                    call.Arguments.SelectMany(a => a is NewArrayExpression array ? array.Expressions : [a]).Select(Translate).ToList());
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
            case nameof(Sql.DateTrunc):
                return new FunctionExpr(SqlFunctionNames.DateTrunc((DateTruncUnit)ClientEvaluator.Evaluate(call.Arguments[1])!), [Translate(call.Arguments[0])]);
            case nameof(Sql.NullIf):
            {
                var (value, other) = TranslatePair(call.Arguments[0], call.Arguments[1]);
                return new FunctionExpr(SqlFunctionNames.NullIf, [value, other]);
            }
            case nameof(Sql.EqualsNullSafe):
            {
                var (left, right) = TranslatePair(call.Arguments[0], call.Arguments[1]);
                return new FunctionExpr(SqlFunctionNames.NullSafeEquals, [left, right]) { IsPredicate = true };
            }
            // 列表是普通字符串，不随值的列做值转换，两边分别翻译
            case nameof(Sql.InCsv):
                return new FunctionExpr(SqlFunctionNames.InCsv, [Translate(StripConvert(call.Arguments[0])), Translate(call.Arguments[1])]) { IsPredicate = true };
            case nameof(Sql.IsoDayOfWeek):
                return new FunctionExpr(SqlFunctionNames.DatePartOf(DatePart.IsoDayOfWeek), [Translate(call.Arguments[0])]);
            case nameof(Sql.Constant):
                return Parameter(ClientEvaluator.Evaluate(call.Arguments[0]), call.Method.GetGenericArguments()[0]);
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

    // string 的实例方法：Contains / StartsWith / EndsWith 翻译成 LIKE，值在客户端拼好 %（与 Java 版的 like 一致）。
    // 带 StringComparison 的重载：忽略大小写时两边都转小写再比较；其余比较方式与不带参数时相同（区分大小写与否取决于库的排序规则）
    private SqlExpr TranslateString(MethodCallExpression call)
    {
        if (ComparisonOf(call) is { } comparison && IgnoresCase(comparison))
            call = Expression.Call(Lower(call.Object!), typeof(string).GetMethod(call.Method.Name, [typeof(string)])!, Lower(call.Arguments[0]));
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

    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    // 转小写：客户端能求值的直接在客户端转（参数里就是小写），否则在 SQL 里 LOWER
    private static Expression Lower(Expression value)
    {
        if (value.Type == typeof(char)) value = Expression.Call(value, typeof(char).GetMethod(nameof(ToString), Type.EmptyTypes)!);
        return ClientEvaluator.MustTranslate(value)
            ? Expression.Call(value, ToLowerMethod)
            : Expression.Constant((ClientEvaluator.Evaluate(value) as string)?.ToLowerInvariant(), typeof(string));
    }

    // 方法的最后一个参数是 StringComparison 时取它的值（在客户端求值）
    private static System.StringComparison? ComparisonOf(MethodCallExpression call) =>
        call.Arguments.Count > 0 && call.Arguments[^1].Type == typeof(System.StringComparison) ? (System.StringComparison)ClientEvaluator.Evaluate(call.Arguments[^1])! : null;

    private static bool IgnoresCase(System.StringComparison comparison) =>
        comparison is System.StringComparison.OrdinalIgnoreCase or System.StringComparison.CurrentCultureIgnoreCase or System.StringComparison.InvariantCultureIgnoreCase;

    // 枚举列 → CASE WHEN 列 = 存的值 THEN '成员名' … END：按数值存的比底层整数，有转换器的（按名字存、名字可能不同于成员名）比转换器给出的值；
    // 可空枚举没有值时是空串（Nullable<T>.ToString() 的行为）
    private static SqlExpr EnumName(SqlExpr column, Type enumType, bool nullable)
    {
        var converter = column is ColumnExpr { Column.Converter: { } c } ? c : null;
        var underlying = Enum.GetUnderlyingType(enumType);
        var branches = Enum.GetValuesAsUnderlyingType(enumType).Cast<object>().Distinct()
            .Select(v =>
            {
                SqlExpr stored = converter is null
                    ? new ParameterExpr(v, underlying)
                    : new ParameterExpr(converter.ToProvider(Enum.ToObject(enumType, v)), converter.ProviderType);
                return ((SqlExpr)new BinaryExpr(column, SqlBinaryOperator.Equal, stored), (SqlExpr)new ParameterExpr(Enum.GetName(enumType, v), typeof(string)));
            })
            .ToList();
        if (nullable) branches.Insert(0, (new IsNullExpr(column, negated: false), new ParameterExpr("", typeof(string))));
        return new CaseExpr(branches, null);
    }

    // Pattern 是拼好 % 的通用写法；同时带上原值与匹配方式，供方言改写（PG 的 ESCAPE、Oracle / SQLite 的 INSTR）。
    // concat：值也在 SQL 里拼 %（Sql.LikeContains 等，对应 Java 的 contains）；否则值是参数时 % 拼进参数
    private LikeExpr LikeOf(Expression target, Expression valueExpr, LikeMode mode, bool concat = false)
    {
        var column = Translate(target);
        var (prefix, suffix) = mode switch
        {
            LikeMode.Contains => ("%", "%"),
            LikeMode.StartsWith => ("", "%"),
            _ => ("%", ""),
        };
        // 支持 LIKE 的加密列：值先按同样的分段规则加密（与 Java 的 LikeFunctionTemplateGenerator 一致）
        var encryption = column is ColumnExpr { Column.Converter: Encryption.EncryptionConverter { SupportQueryLike: true } converter } ? converter : null;
        if (concat || ClientEvaluator.MustTranslate(valueExpr))
        {
            // 模式本身是列（或要求在 SQL 里拼）：CONCAT('%',col,'%')；常量值照样先加密
            var value = encryption is not null && !ClientEvaluator.MustTranslate(valueExpr) && ClientEvaluator.Evaluate(valueExpr)?.ToString() is { } plain
                ? new ParameterExpr(encryption.ToProvider(plain), typeof(string))
                : Translate(valueExpr);
            var parts = new List<SqlExpr>();
            if (prefix.Length > 0) parts.Add(new LiteralExpr("'%'"));
            parts.Add(value);
            if (suffix.Length > 0) parts.Add(new LiteralExpr("'%'"));
            return new LikeExpr(column, new FunctionExpr(SqlFunctionNames.Concat, parts), false, mode, value);
        }
        var text = ClientEvaluator.Evaluate(valueExpr)?.ToString();
        if (encryption is not null && text is not null) text = (string?)encryption.ToProvider(text);
        return new LikeExpr(column, new ParameterExpr(prefix + text + suffix, typeof(string)), false, mode, new ParameterExpr(text, typeof(string)));
    }

    // Sql.Column(entity, "a.b")：沿属性路径（忽略大小写）构造 entity.A.B，之后与手写的成员访问走同一条翻译
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
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
