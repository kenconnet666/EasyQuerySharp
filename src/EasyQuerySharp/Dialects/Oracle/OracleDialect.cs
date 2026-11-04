using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects.Oracle;

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

    /// <inheritdoc />
    public override string ParameterPlaceholder(int index) => ":p" + index;

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
    }
}
