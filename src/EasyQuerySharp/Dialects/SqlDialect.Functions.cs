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
        f[SqlFunctionNames.InCsv] = Template("(({0})::text = ANY(string_to_array({1},',')))");
        foreach (var unit in Enum.GetValues<DateTruncUnit>())
            f[SqlFunctionNames.DateTrunc(unit)] = (_, _) => throw new NotSupportedException($"{Name} 方言不支持 DateTrunc({unit})");
        foreach (var json in new[] { SqlFunctionNames.JsonGet, SqlFunctionNames.JsonGetIndex, SqlFunctionNames.JsonText, SqlFunctionNames.JsonTextIndex, SqlFunctionNames.JsonLength, SqlFunctionNames.JsonContainsKey })
            f[json] = (_, _) => throw new NotSupportedException($"{Name} 方言还不支持 Sql.Json 的 {json}");
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
        f[SqlFunctionNames.Truncate] = TruncateTo("TRUNC");
        f[SqlFunctionNames.CountDistinct] = Distinct("COUNT");
        f[SqlFunctionNames.SumDistinct] = Distinct("SUM");
        f[SqlFunctionNames.AvgDistinct] = Distinct("AVG");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG(").Write(a[0]).Append(", ").Write(a[1]).Append(")");
        f[SqlFunctionNames.DateFormat] = DateFormat("TO_CHAR({0},{1})", ("yyyy", "YYYY"), ("MM", "MM"), ("dd", "DD"), ("HH", "HH24"), ("mm", "MI"), ("ss", "SS"));
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
            unit => "(extract(epoch from ({1})::timestamp-({0})::timestamp)" + unit switch
            {
                DateUnit.Day => "/86400", DateUnit.Hour => "/3600", DateUnit.Minute => "/60", DateUnit.Second => "", _ => "*1000",
            } + ")::int");
    }

    /// <summary>
    /// 日期格式化：<paramref name="pattern"/> 写出带格式串的调用，格式串按 <paramref name="tokens"/>（C# 格式符 → 方言格式符，长的在前）转换后加引号。
    /// </summary>
    protected static FunctionWriter DateFormat(string pattern, params (string CSharp, string Sql)[] tokens) => (r, a) =>
    {
        var format = ((LiteralExpr)a[1]).Sql;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < format.Length;)
        {
            var (cs, sql) = tokens.FirstOrDefault(t => string.CompareOrdinal(format, i, t.CSharp, 0, t.CSharp.Length) == 0);
            if (cs is not null)
            {
                sb.Append(sql);
                i += cs.Length;
            }
            else sb.Append(format[i++]);
        }
        var parts = pattern.Split("{0}");
        var literal = "'" + sb.ToString().Replace("'", "''") + "'";
        r.Append(parts[0].Replace("{1}", literal));
        r.Write(a[0]);
        r.Append(parts[1].Replace("{1}", literal));
    };

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

    /// <summary>截断小数：<c>NAME(x,n)</c>，没给位数时截到整数 <c>NAME(x,0)</c>（与 Java 一致）。</summary>
    protected static FunctionWriter TruncateTo(string name) => (r, a) =>
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

    /// <summary><c>NAME(a,b)</c>。</summary>
    protected static FunctionWriter Call(string name) => (r, a) => r.Append(name).Append("(").WriteList(a, ",").Append(")");

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
