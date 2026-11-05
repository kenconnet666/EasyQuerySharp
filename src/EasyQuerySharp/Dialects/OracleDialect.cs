using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// Oracle 方言。标识符用双引号，占位符 <c>:p0</c>。分页与 Java 版一致用 ROWNUM 包两层（各版本通用）。
/// 驱动用 Oracle.ManagedDataAccess.Core。达梦方言继承它。
/// </summary>
public class OracleDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static OracleDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "oracle";

    /// <summary>Oracle 的类型名：VARCHAR2、NUMBER(p)、BINARY_DOUBLE（23ai 起有 BOOLEAN 与 IF NOT EXISTS）。</summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) => "VARCHAR2(255)",
        _ when type == typeof(int) || type.IsEnum => "NUMBER(10)",
        _ when type == typeof(long) => "NUMBER(19)",
        _ when type == typeof(short) || type == typeof(byte) => "NUMBER(5)",
        _ when type == typeof(double) => "BINARY_DOUBLE",
        _ when type == typeof(float) => "BINARY_FLOAT",
        _ when type == typeof(TimeOnly) || type == typeof(TimeSpan) => "INTERVAL DAY TO SECOND",
        _ => base.ClrColumnType(type, column),
    };

    /// <inheritdoc />
    public override string ParameterPlaceholder(int index) => ":p" + index;

    /// <inheritdoc />
    public override string RecursiveWith => "WITH ";

    /// <summary>
    /// <c>SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (…) rt WHERE ROWNUM &lt; 末行+1) rt1 WHERE rt1."__rownum__" &gt; 跳过行数</c>；
    /// 只有 LIMIT 时省掉外层。
    /// </summary>
    public override void WritePagedSelect(SqlRenderer renderer, SelectQuery query, Action writeUnpaged)
    {
        var offset = query.Offset ?? 0;
        var rownum = QuoteIdentifier("__rownum__");
        if (offset > 0) renderer.Append("SELECT rt1.* FROM (");
        renderer.Append(offset > 0 ? $"SELECT rt.*, ROWNUM AS {rownum} FROM (" : "SELECT rt.* FROM (");
        writeUnpaged();
        renderer.Append(") rt");
        if (query.Limit is { } limit) renderer.Append(" WHERE ROWNUM < ").Append((offset + limit + 1).ToString(CultureInfo.InvariantCulture));
        if (offset > 0) renderer.Append($") rt1 WHERE rt1.{rownum} > ").Append(offset.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>值里含 % 或 _ 时，包含 / 开头 / 结尾改写成 INSTR，避免被当成通配符（与 Java 版一致）。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        renderer.Append("INSTR(").Write(predicate.Operand).Append(",").Write(predicate.Value).Append(",1,1)");
        WriteInstrComparison(renderer, predicate);
    }

    /// <summary>INSTR 结果的比较：包含 &gt; 0、开头 = 1、结尾 = 长度差 + 1；取反时用 = 0 / &lt;&gt;。</summary>
    internal static void WriteInstrComparison(SqlRenderer renderer, LikeExpr predicate)
    {
        switch (predicate.Mode)
        {
            case LikeMode.Contains:
                renderer.Append(predicate.Negated ? " = 0" : " > 0");
                break;
            case LikeMode.StartsWith:
                renderer.Append(predicate.Negated ? " <> 1" : " = 1");
                break;
            default:
                renderer.Append(predicate.Negated ? " <> (LENGTH(" : " = (LENGTH(").Write(predicate.Operand).Append(") - LENGTH(").Write(predicate.Value!).Append(") + 1)");
                break;
        }
    }

    /// <summary>Oracle 原生支持 NULLS FIRST / LAST（Java 的 OracleOrderByNullsModeSQLFunction）。</summary>
    public override void WriteOrderByItem(Rendering.SqlRenderer renderer, OrderByItem item)
    {
        renderer.Write(item.Expr);
        if (item.Raw) return;
        renderer.Append(item.Descending ? " DESC" : " ASC");
        if (item.Nulls is { } nulls) renderer.Append(nulls == NullsOrder.Last ? " NULLS LAST" : " NULLS FIRST");
    }

    /// <summary>冲突处理要用 MERGE 改写整条语句，计划在 M6 方言铺开时实现；在此之前明确报错，避免生成错误的 SQL。</summary>
    public override void WriteInsertConflict(Rendering.SqlRenderer renderer, InsertConflict conflict) =>
        throw new NotSupportedException($"{Name} 的插入冲突处理（MERGE）尚未实现");

    /// <summary>Oracle 的类型转换（对应 Java 的 OracleCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "CASE WHEN {0} IN ('1','true','TRUE') THEN 1 ELSE 0 END",
        _ when targetType == typeof(char) => "SUBSTR(TO_CHAR({0}), 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "TO_TIMESTAMP({0},'HH24:MI:SS.FF6')",
        _ when targetType == typeof(DateOnly) => "TO_TIMESTAMP({0},'YYYY-MM-DD')",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "TO_TIMESTAMP({0},'YYYY-MM-DD HH24:MI:SS.FF6')",
        _ when targetType == typeof(decimal) => "CAST({0} AS DECIMAL(18,4))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "CAST({0} AS FLOAT)",
        _ when targetType == typeof(int) => "CAST({0} AS INT)",
        _ when targetType == typeof(long) || targetType == typeof(uint) || targetType == typeof(ulong) => "CAST({0} AS BIGINT)",
        _ when IsInteger(targetType) => "CAST({0} AS NUMBER)",
        _ when targetType == typeof(Guid) || targetType == typeof(string) => "TO_CHAR({0})",
        _ => base.CastTemplate(targetType),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.NullOrDefault] = Call("NVL");
        f[SqlFunctionNames.Concat] = Infix(" || ");
        f[SqlFunctionNames.Length] = Call("LENGTH");
        f[SqlFunctionNames.PadLeft] = Pad("LPAD", "' '");
        f[SqlFunctionNames.PadRight] = Pad("RPAD", "' '");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("LISTAGG(TO_CHAR(").Write(a[0]).Append("), ").Write(a[1]).Append(") WITHIN GROUP(ORDER BY 1)");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("LISTAGG(DISTINCT TO_CHAR(").Write(a[0]).Append("), ").Write(a[1]).Append(") WITHIN GROUP(ORDER BY 1)");
        f[SqlFunctionNames.DateFormat] = (r, a) => r.Append("TO_CHAR(").Write(a[0]).Append(",'").Append(OracleDateFormat(((LiteralExpr)a[1]).Sql)).Append("')");
        // 相差：两个日期相减得到天数，再拆成 DAY / HOUR / MINUTE / SECOND 累加（与 Java 一致）
        const string interval = "NUMTODSINTERVAL(({1}+0)-({0}+0),'DAY')";
        f[SqlFunctionNames.Now] = Template("SYSTIMESTAMP");
        f[SqlFunctionNames.Random] = Template("DBMS_RANDOM.VALUE()");
        f[SqlFunctionNames.UtcNow] = Template("SYS_EXTRACT_UTC(SYSTIMESTAMP)");
        DateFunctions(f,
            unit => unit switch
            {
                DateUnit.Year => "add_months({0},({1})*12)",
                DateUnit.Month => "add_months({0},{1})",
                DateUnit.Day => "({0}+{1})",
                DateUnit.Hour => "({0}+({1})/24.0)",
                DateUnit.Minute => "({0}+({1})/1440.0)",
                DateUnit.Second => "({0}+({1})/86400.0)",
                _ => "({0}+({1})/86400000.0)",
            },
            part => part switch
            {
                DatePart.DayOfYear => "cast(to_char({0},'DDD') as number)",
                DatePart.DayOfWeek => "(case when to_char({0},'D')='7' then 0 else cast(to_char({0},'D') as number) end)",
                DatePart.IsoDayOfWeek => "cast(to_char({0},'D') as number)",
                _ => "cast(to_char({0},'" + part switch
                {
                    DatePart.Year => "YYYY", DatePart.Month => "MM", DatePart.Day => "DD", DatePart.Hour => "HH24", DatePart.Minute => "MI", _ => "SS",
                } + "') as number)",
            },
            unit => unit switch
            {
                DateUnit.Day => "EXTRACT(DAY FROM " + interval + ")",
                DateUnit.Hour => "(EXTRACT(DAY FROM " + interval + ")*24+EXTRACT(HOUR FROM " + interval + "))",
                DateUnit.Minute => "(EXTRACT(DAY FROM " + interval + ")*1440+EXTRACT(HOUR FROM " + interval + ")*60+EXTRACT(MINUTE FROM " + interval + "))",
                _ => "(EXTRACT(DAY FROM " + interval + ")*86400+EXTRACT(HOUR FROM " + interval + ")*3600+EXTRACT(MINUTE FROM " + interval + ")*60+EXTRACT(SECOND FROM " + interval + "))"
                     + (unit == DateUnit.Millisecond ? "*1000" : ""),
            });
    }

    // 与 Java 的 OracleDateTimeFormatSQLFunction.formatDefault 一致：格式记号与 :/-_ 空格原样转换，其余文字用双引号括起来
    private static readonly System.Text.RegularExpressions.Regex DateFormatToken = new("yyyy|yy|MM|dd|HH|hh|mm|ss|[MdHhmsa:/_ -]");

    /// <summary>C# 的日期格式（yyyy-MM-dd HH:mm:ss 一类）转成 Oracle TO_CHAR 的格式串（不含两侧引号，单引号已转义）。</summary>
    private static string OracleDateFormat(string format)
    {
        var sb = new System.Text.StringBuilder();
        var cursor = 0;
        foreach (System.Text.RegularExpressions.Match m in DateFormatToken.Matches(format))
        {
            if (m.Index > cursor) sb.Append('"').Append(format, cursor, m.Index - cursor).Append('"');
            sb.Append(m.Value switch
            {
                "M" => "FMMM", "d" => "FMDD", "HH" => "HH24", "H" => "FMHH24", "h" => "FMHH",
                "mm" => "MI", "m" => "FMMI", "s" => "FMSS", "a" => "AM",
                _ => m.Value.ToUpperInvariant(),
            });
            cursor = m.Index + m.Length;
        }
        if (cursor < format.Length) sb.Append('"').Append(format, cursor, format.Length - cursor).Append('"');
        return sb.ToString().Replace("'", "''", StringComparison.Ordinal);
    }
}
