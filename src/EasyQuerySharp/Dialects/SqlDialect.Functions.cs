using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

// 函数表与类型转换的写法
public abstract partial class SqlDialect
{
    /// <summary>
    /// 类型转换的模板，<c>{0}</c> 是被转换的表达式（逐字对应 Java 各方言的 CastSQLFunction）。
    /// 默认是 MySQL 的写法；不支持的目标类型抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public virtual string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => " CAST({0} AS SIGNED)",
        _ when targetType == typeof(char) => "SUBSTR(CAST({0} AS CHAR), 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "CAST({0} AS TIME)",
        _ when targetType == typeof(DateOnly) => "CAST({0} AS DATE)",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "CAST({0} AS DATETIME)",
        _ when targetType == typeof(decimal) => "CAST({0} AS DECIMAL(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "CAST({0} AS DECIMAL(32,16))",
        _ when IsInteger(targetType) => "CAST({0} AS SIGNED)",
        _ when targetType == typeof(Guid) => "SUBSTR(CAST({0} AS CHAR), 1, 36)",
        _ when targetType == typeof(string) => "CAST({0} AS CHAR)",
        _ => throw new NotSupportedException($"{Name} 不支持转换到 {targetType.Name}"),
    };

    /// <summary>是否整数类型（byte / short / int / long 及无符号版本）。</summary>
    protected static bool IsInteger(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);

    /// <summary>写一个函数调用：查函数表，没有登记的按 <c>NAME(a,b)</c> 输出。</summary>
    public void WriteFunction(SqlRenderer renderer, FunctionExpr functionCall)
    {
        _functions ??= BuildFunctions();
        if (_functions.TryGetValue(functionCall.Name, out var writer)) writer(renderer, functionCall.Arguments);
        else Call(functionCall.Name)(renderer, functionCall.Arguments);
    }

    /// <summary>
    /// 登记函数写法。子类先调用 <c>base.RegisterFunctions(f)</c>，再覆盖与标准写法不同的部分。
    /// 名字见 <see cref="SqlFunctionNames"/>。
    /// </summary>
    protected virtual void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        f[SqlFunctionNames.NullOrDefault] = Call("COALESCE");
        f[SqlFunctionNames.EqualsWith] = (r, a) => r.Append("(CASE ").Write(a[0]).Append(" WHEN ").Write(a[1]).Append(" THEN 1 ELSE 0 END)");
        f[SqlFunctionNames.NullIf] = Template("NULLIF({0}, {1})");
        f[SqlFunctionNames.NullSafeEquals] = Template("(({0} = {1}) OR ({0} IS NULL AND {1} IS NULL))");
        f[SqlFunctionNames.BooleanValue] = (r, a) => r.Write(a[0]);
        f[SqlFunctionNames.InCsv] = Template("(({0})::text = ANY(string_to_array({1},',')))");
        foreach (var unit in Enum.GetValues<DateTruncUnit>())
            f[SqlFunctionNames.DateTrunc(unit)] = (_, _) => throw new NotSupportedException($"{Name} 方言不支持 DateTrunc({unit})");
        foreach (var json in new[] { SqlFunctionNames.JsonGet, SqlFunctionNames.JsonGetIndex, SqlFunctionNames.JsonText, SqlFunctionNames.JsonTextIndex, SqlFunctionNames.JsonLength, SqlFunctionNames.JsonContainsKey })
            f[json] = (_, _) => throw new NotSupportedException($"{Name} 方言还不支持 Sql.Json 的 {json}");
        f[SqlFunctionNames.MaxColumns] = UnionAllExtreme("MAX", "");
        f[SqlFunctionNames.MinColumns] = UnionAllExtreme("MIN", "");
        f[SqlFunctionNames.Concat] = Call("CONCAT");
        f[SqlFunctionNames.Length] = Call("CHAR_LENGTH");
        f[SqlFunctionNames.Trim] = Call("TRIM");
        f[SqlFunctionNames.SubString] = Call("SUBSTR");
        f[SqlFunctionNames.PadLeft] = Pad("LPAD", null);
        f[SqlFunctionNames.PadRight] = Pad("RPAD", null);
        f[SqlFunctionNames.CompareTo] = (r, a) => r.Append("(CASE WHEN ").Write(a[0]).Append(" = ").Write(a[1])
            .Append(" THEN 0 WHEN ").Write(a[0]).Append(" > ").Write(a[1]).Append(" THEN 1 ELSE -1 END)");
        f[SqlFunctionNames.IndexOf] = Call("INSTR");
        f[SqlFunctionNames.Ceiling] = Call("CEIL");
        f[SqlFunctionNames.Ln] = Call("LN");
        f[SqlFunctionNames.LogBase] = Reordered("LOG", 1, 0);
        f[SqlFunctionNames.Truncate] = WithDigits("TRUNC");
        f[SqlFunctionNames.CountDistinct] = Distinct("COUNT");
        f[SqlFunctionNames.SumDistinct] = Distinct("SUM");
        f[SqlFunctionNames.AvgDistinct] = Distinct("AVG");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG(").Write(a[0]).Append(", ").Write(a[1]).Append(")");
        f[SqlFunctionNames.DateFormat] = DateFormat("TO_CHAR({0},{1})", ("yyyy", "YYYY"), ("yy", "YY"), ("MM", "MM"), ("M", "FMMM"), ("dd", "DD"), ("d", "FMDD"), ("HH", "HH24"), ("H", "FMHH24"), ("hh", "HH12"), ("h", "FMHH12"), ("mm", "MI"), ("m", "FMMI"), ("ss", "SS"), ("s", "FMSS"), ("fff", "MS"), ("ffffff", "US"), ("tt", "AM"));
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("STRING_AGG(DISTINCT ").Write(a[0]).Append(", ").Write(a[1]).Append(")");
        f[SqlFunctionNames.Now] = Template("CURRENT_TIMESTAMP");
        f[SqlFunctionNames.Random] = Template("RANDOM()");
        f[SqlFunctionNames.UtcNow] = Template("(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");
        // 日期：标准写法取 PostgreSQL（与 Java 的 PgSQL 函数一致）
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "(({0})::timestamp+(({1})||' milliseconds')::interval)" : "(({0})::timestamp+(({1})||' " + Lower(unit) + "')::interval)",
            part => part switch
            {
                DatePart.DayOfYear => "extract(doy from ({0})::timestamp)",
                DatePart.DayOfWeek => "extract(dow from ({0})::timestamp)",
                DatePart.IsoDayOfWeek => "(CASE WHEN extract(dow from ({0})::timestamp) = 0 THEN 7 ELSE extract(dow from ({0})::timestamp) END)",
                _ => "extract(" + Lower(part) + " from ({0})::timestamp)",
            },
            // Java 写 (…)::int，::int 是四舍五入；这里先 trunc，与其余方言一样截断（5.5 小时算 5）
            unit => "trunc(extract(epoch from ({1})::timestamp-({0})::timestamp)" + unit switch
            {
                DateUnit.Day => "/86400", DateUnit.Hour => "/3600", DateUnit.Minute => "/60", DateUnit.Second => "", _ => "*1000",
            } + ")::int");
    }

    /// <summary>
    /// 日期格式化：<paramref name="pattern"/> 写出带格式串的调用，C# 的格式符按 <paramref name="tokens"/>（C# 格式符 → 方言格式符）转换，
    /// 映射里没有的格式符抛 <see cref="NotSupportedException"/>，不当文字输出。格式串里的文字按方言转义：方言格式符以 <c>%</c> 开头时
    /// （strftime、DATE_FORMAT 一类）文字里的 <c>%</c> 写成 <c>%%</c>；否则（TO_CHAR 一类）文字里的英文字母用双引号括起来，免得被当成格式符。
    /// </summary>
    protected static FunctionWriter DateFormat(string pattern, params (string CSharp, string Sql)[] tokens) => (r, a) =>
    {
        var percentStyle = tokens.Any(t => t.Sql.StartsWith('%'));
        var sb = new System.Text.StringBuilder();
        foreach (var (isToken, text) in DateFormatParts(r, ((LiteralExpr)a[1]).Sql, tokens))
        {
            if (isToken) sb.Append(text);
            else if (percentStyle) sb.Append(text.Replace("%", "%%", StringComparison.Ordinal));
            else sb.Append(AsciiLetters.Replace(text, "\"$0\""));
        }
        var parts = pattern.Split("{0}");
        var literal = "'" + sb.ToString().Replace("'", "''", StringComparison.Ordinal) + "'";
        r.Append(parts[0].Replace("{1}", literal));
        r.Write(a[0]);
        r.Append(parts[1].Replace("{1}", literal));
    };

    private static readonly System.Text.RegularExpressions.Regex AsciiLetters = new("[A-Za-z]+");

    // 夹在两个格式符之间时留在格式串里的分隔符
    private const string DateFormatSeparators = "-/:., ";

    /// <summary>
    /// 格式串里只能有格式符与分隔符的日期格式化（HANA 的 TO_NVARCHAR、Db2 的 VARCHAR_FORMAT）：格式符和夹在格式符之间的单个分隔符写进 <c>函数(x,'…')</c>，
    /// 其余文字单独作为字面量拼接，如 <c>yyyy年MM月dd日 HH:mm</c> 写成
    /// <c>(F(x,'YYYY') || '年' || F(x,'MM') || '月' || F(x,'DD') || '日 ' || F(x,'HH24:MI'))</c>。映射里没有的格式符抛 <see cref="NotSupportedException"/>。
    /// </summary>
    protected static FunctionWriter SegmentedDateFormat(string function, params (string CSharp, string Sql)[] tokens) => (r, a) =>
    {
        var source = DateFormatParts(r, ((LiteralExpr)a[1]).Sql, tokens);
        var parts = new List<(bool IsFormat, System.Text.StringBuilder Text)>();
        for (var i = 0; i < source.Count; i++)
        {
            var (isToken, text) = source[i];
            var inFormat = parts.Count > 0 && parts[^1].IsFormat;
            var joinsFormat = isToken || (inFormat && text.Length == 1 && DateFormatSeparators.Contains(text[0]) && i + 1 < source.Count && source[i + 1].IsToken);
            if (parts.Count == 0 || parts[^1].IsFormat != joinsFormat) parts.Add((joinsFormat, new()));
            parts[^1].Text.Append(text);
        }
        if (parts.Count > 1) r.Append("(");
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0) r.Append(" || ");
            var text = parts[i].Text.ToString();
            if (parts[i].IsFormat) r.Append(function + "(").Write(a[0]).Append(",'" + text + "')");
            else r.Append("'" + text.Replace("'", "''", StringComparison.Ordinal) + "'");
        }
        if (parts.Count > 1) r.Append(")");
    };

    /// <summary>
    /// 自己拼格式化 SQL 的方言（与 Java 逐字对齐的 Oracle、SQL Server）用它切分格式串：格式符原样返回（只能是 <paramref name="supported"/> 里的），
    /// 其余是去掉了 .NET 引号与转义的文字。
    /// </summary>
    private protected static List<(bool IsToken, string Text)> DateFormatTokens(SqlRenderer r, string format, params string[] supported) =>
        DateFormatParts(r, format, [.. supported.Select(s => (s, s))]);

    // .NET 自定义日期格式里有含义的字母：同一字母的连续重复是一个格式符（yyyy、MM、d、tt、fff）
    private const string DateFormatLetters = "dfFgHhKMmstyz";

    /// <summary>
    /// 把 .NET 的自定义日期格式切成格式符（已换成方言的写法）与文字：引号里的内容、反斜杠后的字符、不是格式字母的字符都是文字。
    /// 映射里没有的格式符、单个字母的标准格式（<c>"d"</c>、<c>"T"</c>）抛 <see cref="NotSupportedException"/>。
    /// </summary>
    private static List<(bool IsToken, string Text)> DateFormatParts(SqlRenderer r, string format, (string CSharp, string Sql)[] tokens)
    {
        if (format.Length == 1)
            throw new NotSupportedException($"日期的标准格式串 \"{format}\" 依赖区域设置，不能翻译成 SQL：改用自定义格式，如 ToString(\"yyyy-MM-dd\")");
        var parts = new List<(bool IsToken, string Text)>();
        var literal = new System.Text.StringBuilder();
        for (var i = 0; i < format.Length;)
        {
            var c = format[i];
            // 成对的引号括住文字；落单的引号本身是文字（o'clock，与 Java fork 的用例一致）
            if (c is '\'' or '"' && format.IndexOf(c, i + 1) is var end and >= 0)
            {
                literal.Append(format, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }
            if (c == '\\' && i + 1 < format.Length)
            {
                literal.Append(format[i + 1]);
                i += 2;
                continue;
            }
            // %M 是只取一个字母的格式符：yyyy%MM 是 yyyy、M、M
            var single = c == '%' && i + 1 < format.Length && DateFormatLetters.Contains(format[i + 1]);
            if (single) c = format[++i];
            if (!DateFormatLetters.Contains(c))
            {
                literal.Append(c);
                i++;
                continue;
            }
            var length = 1;
            while (!single && i + length < format.Length && format[i + length] == c) length++;
            var token = format.Substring(i, length);
            var sql = tokens.FirstOrDefault(t => t.CSharp == token).Sql
                ?? throw new NotSupportedException(
                    $"{r.Dialect.Name} 的日期格式不支持 \"{token}\"（格式串 \"{format}\"），支持的格式符：{string.Join(" ", tokens.Select(t => t.CSharp))}");
            if (literal.Length > 0) parts.Add((false, literal.ToString()));
            literal.Clear();
            parts.Add((true, sql));
            i += length;
        }
        if (literal.Length > 0) parts.Add((false, literal.ToString()));
        return parts;
    }

    /// <summary>
    /// 登记日期加减、取部分、相差三组函数：<paramref name="add"/> 的模板里 {0} 是日期、{1} 是数量；
    /// <paramref name="diff"/> 的模板里 {0} 是起点、{1} 是终点。
    /// </summary>
    protected static void DateFunctions(IDictionary<string, FunctionWriter> f, Func<DateUnit, string> add, Func<DatePart, string> part, Func<DateUnit, string> diff)
    {
        foreach (var unit in Enum.GetValues<DateUnit>())
        {
            f[SqlFunctionNames.DateAdd(unit)] = Template(add(unit));
            f[SqlFunctionNames.DateDiff(unit)] = Template(diff(unit));
        }
        foreach (var p in Enum.GetValues<DatePart>()) f[SqlFunctionNames.DatePartOf(p)] = Template(part(p));
    }

    /// <summary>小写的单位 / 部分名，用于拼模板（<c>day</c>、<c>hour</c>）。</summary>
    protected static string Lower<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    /// <summary>按小数位数处理（截断、四舍五入）：<c>NAME(x,n)</c>，没给位数时写 <c>NAME(x,0)</c>（与 Java 一致）。</summary>
    protected static FunctionWriter WithDigits(string name) => (r, a) =>
    {
        r.Append(name + "(").Write(a[0]).Append(",");
        if (a.Count > 1) r.Write(a[1]);
        else r.Append("0");
        r.Append(")");
    };

    /// <summary>按模板输出：<c>{n}</c> 处写第 n 个参数，其余原样。</summary>
    protected static FunctionWriter Template(string template) => (r, a) =>
    {
        var start = 0;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{' || i + 2 >= template.Length || !char.IsAsciiDigit(template[i + 1]) || template[i + 2] != '}') continue;
            r.Append(template[start..i]).Write(a[template[i + 1] - '0']);
            start = i + 3;
            i += 2;
        }
        r.Append(template[start..]);
    };

    /// <summary>没有布尔类型的库（SQL Server、Oracle）把条件当值时写成 <c>(CASE WHEN 条件 THEN ? ELSE ? END)</c>（与 Java 的 MsSQLBooleanSQLFunction 一致）。</summary>
    protected static FunctionWriter CaseBoolean { get; } = (r, a) => r.Append("(CASE WHEN ").Write(a[0])
        .Append(" THEN ").Write(new ParameterExpr(true, typeof(bool))).Append(" ELSE ").Write(new ParameterExpr(false, typeof(bool))).Append(" END)");

    /// <summary><c>NAME(a,b)</c>。</summary>
    protected static FunctionWriter Call(string name) => (r, a) => r.Append(name).Append("(").WriteList(a, ",").Append(")");

    /// <summary>
    /// 几个值取最大 / 最小的通用写法（与 Java 的 MaxMinColumnsSQLFunction 一致）：每个值一个 <c>SELECT v AS __col__</c>，UNION ALL 后再聚合；
    /// <paramref name="selectSuffix"/> 接在每个 SELECT 后（Oracle 的 <c> FROM DUAL</c>）；<paramref name="table"/>、<paramref name="column"/> 是派生表与列的名字。
    /// </summary>
    protected static FunctionWriter UnionAllExtreme(string aggregate, string selectSuffix, string table = "__mmc", string column = "__col__") => (r, a) =>
    {
        r.Append("(SELECT " + aggregate + "(" + table + "." + column + ") FROM (");
        for (var i = 0; i < a.Count; i++)
        {
            if (i > 0) r.Append(" UNION ALL ");
            r.Append("SELECT ").Write(a[i]).Append(" AS " + column + selectSuffix);
        }
        r.Append(") " + table + ")");
    };

    /// <summary><c>NAME(DISTINCT x)</c>。</summary>
    protected static FunctionWriter Distinct(string name) => (r, a) => r.Append(name).Append("(DISTINCT ").Write(a[0]).Append(")");

    /// <summary>参数顺序与 C# 不同的调用：<paramref name="order"/> 给出输出时依次取第几个参数。</summary>
    protected static FunctionWriter Reordered(string name, params int[] order) =>
        (r, a) => r.Append(name).Append("(").WriteList(order.Where(i => i < a.Count).Select(i => a[i]).ToList(), ",").Append(")");

    /// <summary><c>LPAD(s, n)</c> / <c>LPAD(s, n, pad)</c>：Java 版在长度前留一个空格；<paramref name="defaultPad"/> 不为 null 时没给填充字符也写出它。</summary>
    protected static FunctionWriter Pad(string name, string? defaultPad) => (r, a) =>
    {
        r.Append(name).Append("(").Write(a[0]).Append(", ").Write(a[1]);
        if (a.Count > 2) r.Append(", ").Write(a[2]);
        else if (defaultPad is not null) r.Append(", ").Append(defaultPad);
        r.Append(")");
    };

    /// <summary>用分隔符连接参数（不加括号），如 Oracle 的 <c>a || b</c>。</summary>
    protected static FunctionWriter Infix(string separator) => (r, a) => r.WriteList(a, separator);

    private Dictionary<string, FunctionWriter> BuildFunctions()
    {
        var functions = new Dictionary<string, FunctionWriter>(StringComparer.Ordinal);
        RegisterFunctions(functions);
        return functions;
    }
}
