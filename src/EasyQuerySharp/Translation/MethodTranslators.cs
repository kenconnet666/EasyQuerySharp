using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>把一次方法调用翻译成 SQL；参数已经是原始表达式，按需用 <see cref="ExpressionTranslator"/> 翻译。</summary>
internal delegate SqlExpr MethodTranslator(ExpressionTranslator t, MethodCallExpression call);

/// <summary>
/// BCL 方法到 SQL 函数的映射表：string、Math 等常用方法直接写在查询里即可（<c>u.Name.ToUpper()</c>、<c>Math.Abs(x)</c>），
/// 不需要专门的 SQL 函数入口。方法按 <see cref="MethodInfo"/> 精确匹配，泛型方法按泛型定义匹配。
/// 方言差异不在这里处理：这里只产出与方言无关的 <see cref="FunctionExpr"/>，写法由方言的函数表决定。
/// </summary>
internal static class MethodTranslators
{
    /// <summary>查表翻译；没有登记的方法返回 false。</summary>
    public static bool TryTranslate(ExpressionTranslator t, MethodCallExpression call, out SqlExpr result)
    {
        var key = call.Method.IsGenericMethod ? call.Method.GetGenericMethodDefinition() : call.Method;
        if (Table.TryGetValue(key, out var translator))
        {
            result = translator(t, call);
            return true;
        }
        result = null!;
        return false;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2065", Justification = AotJustification.TrimmedBcl)]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.TrimmedBcl)]
    private static Dictionary<MethodInfo, MethodTranslator> Build()
    {
        var table = new Dictionary<MethodInfo, MethodTranslator>();
        // 裁剪后（Native AOT）没人调用的 BCL 方法不在了，取到 null：查询里本来也写不出它，跳过即可
        void Add(MethodInfo? method, MethodTranslator translator)
        {
            if (method is not null) table[method] = translator;
        }

        var s = typeof(string);
        // ---------- string 实例方法 ----------
        Add(s.GetMethod(nameof(string.ToLower), Type.EmptyTypes), Unary(SqlFunctionNames.Lower));
        Add(s.GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes), Unary(SqlFunctionNames.Lower));
        Add(s.GetMethod(nameof(string.ToUpper), Type.EmptyTypes), Unary(SqlFunctionNames.Upper));
        Add(s.GetMethod(nameof(string.ToUpperInvariant), Type.EmptyTypes), Unary(SqlFunctionNames.Upper));
        Add(s.GetMethod(nameof(string.Trim), Type.EmptyTypes), Unary(SqlFunctionNames.Trim));
        Add(s.GetMethod(nameof(string.TrimStart), Type.EmptyTypes), Unary(SqlFunctionNames.TrimStart));
        Add(s.GetMethod(nameof(string.TrimEnd), Type.EmptyTypes), Unary(SqlFunctionNames.TrimEnd));
        Add(s.GetMethod(nameof(string.Replace), [s, s]), (t, c) => Fn(SqlFunctionNames.Replace, t.Translate(c.Object!), t.Translate(c.Arguments[0]), t.Translate(c.Arguments[1])));
        // C# 下标从 0 开始，SQL 从 1 开始：常量直接加 1 写成字面量（Java 版也写字面量）
        Add(s.GetMethod(nameof(string.Substring), [typeof(int)]), (t, c) => Fn(SqlFunctionNames.SubString, t.Translate(c.Object!), Literal(t, c.Arguments[0], 1)));
        Add(s.GetMethod(nameof(string.Substring), [typeof(int), typeof(int)]), (t, c) =>
            Fn(SqlFunctionNames.SubString, t.Translate(c.Object!), Literal(t, c.Arguments[0], 1), Literal(t, c.Arguments[1], 0)));
        Add(s.GetMethod(nameof(string.PadLeft), [typeof(int)]), (t, c) => Fn(SqlFunctionNames.PadLeft, t.Translate(c.Object!), Literal(t, c.Arguments[0], 0)));
        Add(s.GetMethod(nameof(string.PadLeft), [typeof(int), typeof(char)]), (t, c) =>
            Fn(SqlFunctionNames.PadLeft, t.Translate(c.Object!), Literal(t, c.Arguments[0], 0), Text(t, c.Arguments[1])));
        Add(s.GetMethod(nameof(string.PadRight), [typeof(int)]), (t, c) => Fn(SqlFunctionNames.PadRight, t.Translate(c.Object!), Literal(t, c.Arguments[0], 0)));
        Add(s.GetMethod(nameof(string.PadRight), [typeof(int), typeof(char)]), (t, c) =>
            Fn(SqlFunctionNames.PadRight, t.Translate(c.Object!), Literal(t, c.Arguments[0], 0), Text(t, c.Arguments[1])));
        Add(s.GetMethod(nameof(string.CompareTo), [s]), (t, c) => Fn(SqlFunctionNames.CompareTo, t.Translate(c.Object!), t.Translate(c.Arguments[0])));
        Add(s.GetMethod(nameof(string.IndexOf), [s]), (t, c) =>
            new BinaryExpr(Fn(SqlFunctionNames.IndexOf, t.Translate(c.Object!), t.Translate(c.Arguments[0])), SqlBinaryOperator.Subtract, LiteralExpr.One));

        // ---------- string 静态方法 ----------
        Add(s.GetMethod(nameof(string.IsNullOrEmpty), [s]), (t, c) => new BlankCheckExpr(t.Translate(c.Arguments[0]), whitespace: false, negated: false));
        Add(s.GetMethod(nameof(string.IsNullOrWhiteSpace), [s]), (t, c) => new BlankCheckExpr(t.Translate(c.Arguments[0]), whitespace: true, negated: false));
        Add(s.GetMethod(nameof(string.Compare), [s, s]), (t, c) => Fn(SqlFunctionNames.CompareTo, t.Translate(c.Arguments[0]), t.Translate(c.Arguments[1])));
        foreach (var concat in s.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == nameof(string.Concat) && m.GetParameters().All(p => p.ParameterType == s || p.ParameterType == typeof(string[]) || p.ParameterType == typeof(object))))
            Add(concat, (t, c) => Fn(SqlFunctionNames.Concat, Arguments(t, c, t.ConcatPart)));
        foreach (var format in s.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == nameof(string.Format) && m.GetParameters()[0].ParameterType == s))
            Add(format, TranslateFormat);

        // ---------- 类型转换：Parse / Convert.ToXxx → CAST（对应 Java 的 toNumber / asStr / cast）----------
        foreach (var target in new[]
                 {
                     typeof(byte), typeof(short), typeof(int), typeof(long), typeof(decimal), typeof(double), typeof(float),
                     typeof(bool), typeof(DateTime), typeof(DateOnly), typeof(TimeOnly), typeof(Guid),
                 })
            Add(target.GetMethod("Parse", [s]), Cast(target));
        // 只有一个参数的 Convert.ToXxx 都是类型转换：object、string 与各标量类型的重载（Convert.ToString(int)、ToInt32(long) …）
        foreach (var convert in typeof(Convert).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name.StartsWith("To", StringComparison.Ordinal) && IsCastable(m.ReturnType)
                                 && m.GetParameters() is [{ ParameterType: var p }] && (p == typeof(object) || IsCastable(p))))
            Add(convert, Cast(convert.ReturnType));

        // ---------- 日期 ----------
        // 格式串在 SQL 里是字面量（Java 版的写法），由方言转换格式符
        foreach (var dateType in new[] { typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly) })
            Add(dateType.GetMethod(nameof(DateTime.ToString), [s]), (t, c) =>
                Fn(SqlFunctionNames.DateFormat, t.Translate(c.Object!), new LiteralExpr((string)ClientEvaluator.Evaluate(c.Arguments[0])!)));
        // 加减：AddDays(n) 等，n 是值时作为参数（整数值按 long 传，与 Java 一致）
        foreach (var dateType in new[] { typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly) })
        {
            foreach (var (name, unit) in DateAdds)
            {
                if ((dateType.GetMethod(name, [typeof(double)]) ?? dateType.GetMethod(name, [typeof(int)])) is not { } add) continue;
                Add(add, (t, c) => Fn(SqlFunctionNames.DateAdd(unit), t.Translate(c.Object!), Amount(t, c.Arguments[0])));
            }
        }

        // DateOnly.FromDateTime(x)：日期部分（读回时转成 DateOnly）
        Add(typeof(DateOnly).GetMethod(nameof(DateOnly.FromDateTime), [typeof(DateTime)]), (t, c) => Fn(SqlFunctionNames.ToDateOnly, t.Translate(c.Arguments[0])));
        // Math.Max(a, b) / Math.Min(a, b) / Math.Clamp(x, lo, hi)：同一行里几个值取最大 / 最小（与 Sql.MaxColumns / MinColumns 相同）
        foreach (var m in typeof(Math).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.GetParameters().All(p => IsNumeric(p.ParameterType))))
        {
            if (m.Name == nameof(Math.Max)) Add(m, (t, c) => Fn(SqlFunctionNames.MaxColumns, Arguments(t, c)));
            else if (m.Name == nameof(Math.Min)) Add(m, (t, c) => Fn(SqlFunctionNames.MinColumns, Arguments(t, c)));
            else if (m.Name == nameof(Math.Clamp))
                Add(m, (t, c) => Fn(SqlFunctionNames.MaxColumns, t.Translate(c.Arguments[1]), Fn(SqlFunctionNames.MinColumns, t.Translate(c.Arguments[0]), t.Translate(c.Arguments[2]))));
        }

        // ---------- Math ----------
        foreach (var name in new[]
                 {
                     nameof(Math.Abs), nameof(Math.Floor), nameof(Math.Ceiling), nameof(Math.Sign), nameof(Math.Sqrt), nameof(Math.Exp), nameof(Math.Log10), nameof(Math.Round),
                     nameof(Math.Truncate), nameof(Math.Pow), nameof(Math.Log), nameof(Math.Sin), nameof(Math.Cos), nameof(Math.Tan), nameof(Math.Asin), nameof(Math.Acos), nameof(Math.Atan),
                     nameof(Math.Atan2),
                 })
        {
            foreach (var m in typeof(Math).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == name))
            {
                var parameters = m.GetParameters();
                // 只映射数值参数的重载（MidpointRounding 等枚举参数的重载没有 SQL 对应）
                if (parameters.Any(p => !IsNumeric(p.ParameterType))) continue;
                var function = name switch
                {
                    nameof(Math.Ceiling) => SqlFunctionNames.Ceiling,
                    nameof(Math.Pow) => SqlFunctionNames.Power,
                    nameof(Math.Log) when parameters.Length == 1 => SqlFunctionNames.Ln,
                    nameof(Math.Log) => SqlFunctionNames.LogBase,
                    nameof(Math.Truncate) => SqlFunctionNames.Truncate,
                    nameof(Math.Atan2) => SqlFunctionNames.Atan2,
                    _ => name.ToUpperInvariant(),
                };
                Add(m, (t, c) => Fn(function, Arguments(t, c)));
            }
        }
        return table;
    }

    private static readonly (string Method, DateUnit Unit)[] DateAdds =
    [
        (nameof(DateTime.AddYears), DateUnit.Year), (nameof(DateTime.AddMonths), DateUnit.Month), (nameof(DateTime.AddDays), DateUnit.Day),
        (nameof(DateTime.AddHours), DateUnit.Hour), (nameof(DateTime.AddMinutes), DateUnit.Minute), (nameof(DateTime.AddSeconds), DateUnit.Second),
        (nameof(DateTime.AddMilliseconds), DateUnit.Millisecond),
    ];

    private static readonly Dictionary<string, DatePart> DateParts = Enum.GetValues<DatePart>().ToDictionary(p => p.ToString(), StringComparer.Ordinal);

    private static readonly Dictionary<string, DateUnit> TimeSpanTotals = new(StringComparer.Ordinal)
    {
        [nameof(TimeSpan.Days)] = DateUnit.Day,
        [nameof(TimeSpan.TotalDays)] = DateUnit.Day,
        [nameof(TimeSpan.TotalHours)] = DateUnit.Hour,
        [nameof(TimeSpan.TotalMinutes)] = DateUnit.Minute,
        [nameof(TimeSpan.TotalSeconds)] = DateUnit.Second,
        [nameof(TimeSpan.TotalMilliseconds)] = DateUnit.Millisecond,
    };

    // 放在上面这些表之后：静态字段按声明顺序初始化
    private static readonly Dictionary<MethodInfo, MethodTranslator> Table = Build();

    /// <summary>
    /// 成员访问：<c>s.Length</c>、日期的 <c>x.Year</c> / <c>x.DayOfWeek</c> 等，以及两个日期相减后的
    /// <c>(end - start).TotalDays</c> / <c>.Days</c> / <c>.TotalHours</c> 等（整数相差，截断小数，对应 Java 的 duration）。
    /// </summary>
    public static bool TryTranslateMember(ExpressionTranslator t, MemberExpression m, out SqlExpr result)
    {
        result = null!;
        if (m.Expression is not { } owner) return false;
        var ownerType = Nullable.GetUnderlyingType(owner.Type) ?? owner.Type;
        if (ownerType == typeof(string) && m.Member.Name == nameof(string.Length))
            result = Fn(SqlFunctionNames.Length, t.Translate(owner));
        else if (IsDate(ownerType) && DateParts.TryGetValue(m.Member.Name, out var part))
            result = Fn(SqlFunctionNames.DatePartOf(part), t.Translate(owner));
        // x.Date：截断到天（与 DateTrunc(Day) 相同）
        else if ((ownerType == typeof(DateTime) || ownerType == typeof(DateTimeOffset)) && m.Member.Name == nameof(DateTime.Date))
            result = Fn(SqlFunctionNames.DateTrunc(DateTruncUnit.Day), t.Translate(owner));
        else if (ownerType == typeof(TimeSpan) && TimeSpanTotals.TryGetValue(m.Member.Name, out var unit)
                 && UnwrapValue(owner) is BinaryExpression { NodeType: ExpressionType.Subtract } diff && IsDate(Nullable.GetUnderlyingType(diff.Left.Type) ?? diff.Left.Type))
            result = Fn(SqlFunctionNames.DateDiff(unit), t.Translate(diff.Right), t.Translate(diff.Left));
        return result is not null;
    }

    // (a - b).Value：可空 TimeSpan 取 Value 时看里面的减法
    private static Expression UnwrapValue(Expression e)
    {
        e = ExpressionTranslator.StripConvert(e);
        return e is MemberExpression { Member.Name: "Value", Expression: { } inner } && Nullable.GetUnderlyingType(inner.Type) is not null
            ? ExpressionTranslator.StripConvert(inner) : e;
    }

    private static bool IsDate(Type type) => type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly);

    // 加减的数量：值作为参数，整数值统一按 long（AddDays 的参数是 double，常量 2 也写成 2(Long)，与 Java 一致）
    private static SqlExpr Amount(ExpressionTranslator t, Expression e)
    {
        var stripped = ExpressionTranslator.StripConvert(e);
        if (ClientEvaluator.MustTranslate(stripped)) return t.Translate(stripped);
        var value = ClientEvaluator.Evaluate(e);
        return value switch
        {
            double d when d == Math.Truncate(d) => new ParameterExpr((long)d, typeof(long)),
            int i => new ParameterExpr((long)i, typeof(long)),
            _ => new ParameterExpr(value, value?.GetType() ?? typeof(long)),
        };
    }

    private static MethodTranslator Unary(string function) => (t, c) => Fn(function, t.Translate(c.Object!));

    // 能 CAST 的类型：标量，字节数组除外（Convert.ToBase64String / ToHexString 是编码，不是类型转换）
    private static bool IsCastable(Type type) => Metadata.ScalarTypes.IsScalar(type) && type != typeof(byte[]);

    // 唯一参数转换成目标类型；Convert.ToString 等同 CAST 为字符串
    private static MethodTranslator Cast(Type target) => (t, c) => new CastExpr(t.Translate(c.Arguments[0]), target);

    private static FunctionExpr Fn(string name, params SqlExpr[] args) => new(name, args);

    private static FunctionExpr Fn(string name, IReadOnlyList<SqlExpr> args) => new(name, args);

    // string.Concat(params string[]) 与 Concat(a, b, …) 都展开成平铺的参数
    private static List<SqlExpr> Arguments(ExpressionTranslator t, MethodCallExpression c, Func<Expression, SqlExpr>? translate = null)
    {
        translate ??= t.Translate;
        var result = new List<SqlExpr>();
        foreach (var a in c.Arguments)
        {
            if (a is NewArrayExpression array) result.AddRange(array.Expressions.Select(translate));
            else result.Add(translate(a));
        }
        return result;
    }

    /// <summary>
    /// string.Format（也是插值字符串在表达式树里的形式）→ CONCAT：模板的文字段作为参数，<c>{n}</c> 处放对应表达式
    /// （对应 Java 的 stringFormat）。
    /// </summary>
    private static SqlExpr TranslateFormat(ExpressionTranslator t, MethodCallExpression c)
    {
        var format = (string)ClientEvaluator.Evaluate(c.Arguments[0])!;
        var args = c.Arguments.Skip(1).SelectMany(a => a is NewArrayExpression array ? array.Expressions : [a]).ToList();
        var parts = new List<SqlExpr>();
        var start = 0;
        for (var i = 0; i < format.Length; i++)
        {
            if (format[i] != '{') continue;
            var close = format.IndexOf('}', i);
            var spec = format[(i + 1)..close];
            var comma = spec.IndexOfAny([',', ':']);
            var index = int.Parse(comma < 0 ? spec : spec[..comma], CultureInfo.InvariantCulture);
            if (i > start) parts.Add(new ParameterExpr(format[start..i], typeof(string)));
            parts.Add(t.ConcatPart(args[index]));
            start = close + 1;
            i = close;
        }
        if (start < format.Length) parts.Add(new ParameterExpr(format[start..], typeof(string)));
        return parts.Count == 1 ? parts[0] : Fn(SqlFunctionNames.Concat, parts);
    }

    /// <summary>
    /// 整数参数：常量时写成字面量（加上 <paramref name="add"/>，用于 0 基转 1 基），否则翻译成表达式（必要时 + add）。
    /// </summary>
    private static SqlExpr Literal(ExpressionTranslator t, Expression e, int add)
    {
        if (!ClientEvaluator.MustTranslate(e))
            return new LiteralExpr((Convert.ToInt64(ClientEvaluator.Evaluate(e), CultureInfo.InvariantCulture) + add).ToString(CultureInfo.InvariantCulture));
        var sql = t.Translate(e);
        // 与 Java 一致：表达式加偏移写成 x+1（不加括号和空格）
        return add == 0 ? sql : new FragmentExpr("{0}+" + add.ToString(CultureInfo.InvariantCulture), [sql]);
    }

    // char 参数按字符串参数传（SQL 没有 char 类型）
    private static SqlExpr Text(ExpressionTranslator t, Expression e) =>
        ClientEvaluator.MustTranslate(e) ? t.Translate(e) : new ParameterExpr(ClientEvaluator.Evaluate(e)?.ToString(), typeof(string));

    // 枚举的类型码是底层整数，要排除（Math.Round 的 MidpointRounding 参数没有 SQL 对应）
    private static bool IsNumeric(Type type) => !type.IsEnum && Type.GetTypeCode(type) is >= TypeCode.SByte and <= TypeCode.Decimal;
}
