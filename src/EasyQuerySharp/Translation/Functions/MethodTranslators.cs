using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation.Functions;

/// <summary>把一次方法调用翻译成 SQL；参数已经是原始表达式，按需用 <see cref="ExpressionTranslator"/> 翻译。</summary>
internal delegate SqlExpr MethodTranslator(ExpressionTranslator t, MethodCallExpression call);

/// <summary>
/// BCL 方法到 SQL 函数的映射表：string、Math 等常用方法直接写在查询里即可（<c>u.Name.ToUpper()</c>、<c>Math.Abs(x)</c>），
/// 不需要专门的 SQL 函数入口。方法按 <see cref="MethodInfo"/> 精确匹配，泛型方法按泛型定义匹配。
/// 方言差异不在这里处理：这里只产出与方言无关的 <see cref="FunctionExpr"/>，写法由方言的函数表决定。
/// </summary>
internal static class MethodTranslators
{
    private static readonly Dictionary<MethodInfo, MethodTranslator> Table = Build();

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

    private static Dictionary<MethodInfo, MethodTranslator> Build()
    {
        var table = new Dictionary<MethodInfo, MethodTranslator>();
        void Add(MethodInfo? method, MethodTranslator translator) =>
            table[method ?? throw new InvalidOperationException("映射表里的方法不存在")] = translator;

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
        Add(s.GetMethod(nameof(string.Compare), [s, s]), (t, c) => Fn(SqlFunctionNames.CompareTo, t.Translate(c.Arguments[0]), t.Translate(c.Arguments[1])));
        foreach (var concat in s.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == nameof(string.Concat) && m.GetParameters().All(p => p.ParameterType == s || p.ParameterType == typeof(string[]) || p.ParameterType == typeof(object))))
            Add(concat, (t, c) => Fn(SqlFunctionNames.Concat, Arguments(t, c)));
        foreach (var format in s.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == nameof(string.Format) && m.GetParameters()[0].ParameterType == s))
            Add(format, TranslateFormat);

        // ---------- 日期 ----------
        // 格式串在 SQL 里是字面量（Java 版的写法），由方言转换格式符
        foreach (var dateType in new[] { typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly) })
            Add(dateType.GetMethod(nameof(DateTime.ToString), [s]), (t, c) =>
                Fn(SqlFunctionNames.DateFormat, t.Translate(c.Object!), new LiteralExpr((string)ClientEvaluator.Evaluate(c.Arguments[0])!)));

        // ---------- Math ----------
        foreach (var name in new[] { nameof(Math.Abs), nameof(Math.Floor), nameof(Math.Ceiling), nameof(Math.Sign), nameof(Math.Sqrt), nameof(Math.Exp), nameof(Math.Log10), nameof(Math.Round), nameof(Math.Truncate), nameof(Math.Pow), nameof(Math.Log) })
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
                    nameof(Math.Log) => "LOG",
                    nameof(Math.Truncate) => SqlFunctionNames.Truncate,
                    _ => name.ToUpperInvariant(),
                };
                Add(m, (t, c) => Fn(function, Arguments(t, c)));
            }
        }
        return table;
    }

    private static MethodTranslator Unary(string function) => (t, c) => Fn(function, t.Translate(c.Object!));

    private static FunctionExpr Fn(string name, params SqlExpr[] args) => new(name, args);

    private static FunctionExpr Fn(string name, IReadOnlyList<SqlExpr> args) => new(name, args);

    // string.Concat(params string[]) 与 Concat(a, b, …) 都展开成平铺的参数
    private static List<SqlExpr> Arguments(ExpressionTranslator t, MethodCallExpression c)
    {
        var result = new List<SqlExpr>();
        foreach (var a in c.Arguments)
        {
            if (a is NewArrayExpression array) result.AddRange(array.Expressions.Select(t.Translate));
            else result.Add(t.Translate(a));
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
            parts.Add(t.Translate(args[index]));
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
        if (!ClientEvaluator.DependsOnParameters(e))
            return new LiteralExpr((Convert.ToInt64(ClientEvaluator.Evaluate(e), CultureInfo.InvariantCulture) + add).ToString(CultureInfo.InvariantCulture));
        var sql = t.Translate(e);
        return add == 0 ? sql : new BinaryExpr(sql, SqlBinaryOperator.Add, new LiteralExpr(add.ToString(CultureInfo.InvariantCulture)));
    }

    // char 参数按字符串参数传（SQL 没有 char 类型）
    private static SqlExpr Text(ExpressionTranslator t, Expression e) =>
        ClientEvaluator.DependsOnParameters(e) ? t.Translate(e) : new ParameterExpr(ClientEvaluator.Evaluate(e)?.ToString(), typeof(string));

    private static bool IsNumeric(Type type) => Type.GetTypeCode(type) is >= TypeCode.SByte and <= TypeCode.Decimal;
}
