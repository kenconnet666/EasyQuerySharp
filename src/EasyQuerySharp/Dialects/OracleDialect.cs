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

    /// <summary>自增键用 <c>RETURNING "id" INTO :p</c> 写进输出参数（Oracle 的 RETURNING 必须带 INTO）。</summary>
    public override void WriteInsertReturning(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning)
    {
        renderer.Append(" RETURNING ").WriteList(returning, ",").Append(" INTO ");
        for (var i = 0; i < returning.Count; i++)
        {
            if (i > 0) renderer.Append(",");
            renderer.WriteOutputParameter(((ColumnExpr)returning[i]).Column?.ClrType ?? typeof(long));
        }
    }

    /// <summary>
    /// Oracle 没有 UPDATE … FROM：写成按 ROWID 对上的 MERGE，连接与条件都在 USING 的查询里：
    /// <c>MERGE INTO "a" eq_w USING (SELECT t.ROWID AS "__rid__",值 AS "__v0__",… FROM "a" t INNER JOIN "b" t1 ON … WHERE …) eq_src
    /// ON (eq_w.ROWID = eq_src."__rid__") WHEN MATCHED THEN UPDATE SET eq_w."c" = eq_src."__v0__",…</c>。
    /// </summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        var rowId = QuoteIdentifier("__rid__");
        renderer.Append("MERGE INTO ").WriteTableName(update.Table).Append(" eq_w USING (SELECT " + update.Table.Alias + ".ROWID AS " + rowId);
        for (var i = 0; i < update.Sets.Count; i++) renderer.Append(",").Write(update.Sets[i].Value).Append(" AS " + QuoteIdentifier($"__v{i}__"));
        renderer.Append(" FROM ").WriteTable(update.Table);
        foreach (var join in update.Joins) renderer.WriteJoin(join);
        renderer.WriteWhere(update.Where).Append(") eq_src ON (eq_w.ROWID = eq_src." + rowId + ") WHEN MATCHED THEN UPDATE SET ");
        for (var i = 0; i < update.Sets.Count; i++)
            renderer.Append((i > 0 ? "," : "") + "eq_w." + QuoteIdentifier(update.Sets[i].Column.Name) + " = eq_src." + QuoteIdentifier($"__v{i}__"));
    }

    /// <summary><c>DELETE FROM "a" WHERE ROWID IN (SELECT t.ROWID FROM "a" t INNER JOIN "b" t1 ON … WHERE …)</c>。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTableName(statement.Table).Append(" WHERE ROWID IN (SELECT " + statement.Table.Alias + ".ROWID FROM ").WriteTable(statement.Table);
        foreach (var join in statement.Joins) renderer.WriteJoin(join);
        renderer.WriteWhere(statement.Where).Append(")");
    }

    /// <inheritdoc />
    public override string RecursiveWith => "WITH ";

    /// <summary>递归 CTE 必须写列名清单（否则 ORA-32039）。</summary>
    public override bool RecursiveCteColumnList => true;

    /// <summary>
    /// <c>SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (…) rt WHERE ROWNUM &lt; 末行+1) rt1 WHERE rt1."__rownum__" &gt; 跳过行数</c>；
    /// 只有 LIMIT 时省掉外层。
    /// </summary>
    public override void WritePagedSelect(SqlRenderer renderer, SelectQuery query, Action writeUnpaged)
    {
        var offset = query.Offset ?? 0;
        var rownum = QuoteIdentifier("__rownum__");
        // 没有排序、没有分组时 ROWNUM 直接写进这一层（与 Java 一致）：<c>SELECT rt.* FROM(SELECT …, ROWNUM AS "__rownum__" … WHERE … AND ROWNUM &lt; 末行+1) rt WHERE rt."__rownum__" &gt; 跳过行数</c>
        if (query.OrderBy.Count == 0 && query.GroupBy.Count == 0)
        {
            WriteInlineRowNum(renderer, query, writeUnpaged, offset, rownum);
            return;
        }
        if (offset > 0) renderer.Append("SELECT rt1.* FROM (");
        renderer.Append(offset > 0 ? $"SELECT rt.*, ROWNUM AS {rownum} FROM (" : "SELECT rt.* FROM (");
        writeUnpaged();
        renderer.Append(") rt");
        if (query.Limit is { } limit) renderer.Append(" WHERE ROWNUM < ").Append((offset + limit + 1).ToString(CultureInfo.InvariantCulture));
        if (offset > 0) renderer.Append($") rt1 WHERE rt1.{rownum} > ").Append(offset.ToString(CultureInfo.InvariantCulture));
    }

    // 临时给这一层加上 ROWNUM 列与 ROWNUM 条件，写完再拿掉（查询对象在别处还会复用）
    private static void WriteInlineRowNum(SqlRenderer renderer, SelectQuery query, Action writeUnpaged, long offset, string rownum)
    {
        // Java 在列表后写 ", ROWNUM AS …"（逗号后有空格）
        var column = offset > 0 ? new SelectColumn(new LiteralExpr(" ROWNUM"), "__rownum__") : null;
        var limit = query.Limit is { } rows ? new LiteralExpr("ROWNUM < " + (offset + rows + 1).ToString(CultureInfo.InvariantCulture)) : null;
        if (column is not null) query.Columns.Add(column);
        if (limit is not null) query.Where.Add(limit);
        try
        {
            if (offset > 0) renderer.Append("SELECT rt.* FROM(");
            writeUnpaged();
            if (offset > 0) renderer.Append($") rt WHERE rt.{rownum} > ").Append(offset.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            if (column is not null) query.Columns.Remove(column);
            if (limit is not null) query.Where.Remove(limit);
        }
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

    /// <inheritdoc />
    public override bool InsertConflictUsesMerge => true;

    /// <summary>Oracle 的 MERGE 来源查询从 DUAL 取值。</summary>
    protected override string MergeSourceSuffix => " FROM DUAL ";

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
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
        foreach (var (unit, format) in new[] { (DateTruncUnit.Year, "YYYY"), (DateTruncUnit.Quarter, "Q"), (DateTruncUnit.Month, "MM"), (DateTruncUnit.Week, "IW"), (DateTruncUnit.Day, "DD"), (DateTruncUnit.Hour, "HH24"), (DateTruncUnit.Minute, "MI") })
            f[SqlFunctionNames.DateTrunc(unit)] = Template("TRUNC({0}, '" + format + "')");
        // TRUNC 没有到秒的格式：转成 DATE 就去掉了秒以下的部分
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Second)] = Template("CAST({0} AS DATE)");
        f[SqlFunctionNames.InCsv] = Template("(INSTR(',' || {1} || ',', ',' || {0} || ',') > 0)");
        f[SqlFunctionNames.NullOrDefault] = Call("NVL");
        f[SqlFunctionNames.Concat] = Infix(" || ");
        // 与 Java 的 OracleMaxMinColumnsSQLFunction 一致（达梦沿用）：每个值 SELECT … FROM DUAL
        f[SqlFunctionNames.MaxColumns] = UnionAllExtreme("MAX", " FROM DUAL");
        f[SqlFunctionNames.MinColumns] = UnionAllExtreme("MIN", " FROM DUAL");
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
