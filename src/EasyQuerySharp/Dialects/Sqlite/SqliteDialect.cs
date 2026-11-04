using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects.Sqlite;

/// <summary>SQLite 方言。驱动用 Microsoft.Data.Sqlite：<c>o.UseSqlite(SqliteFactory.Instance.CreateDataSource(cs))</c>。</summary>
public class SqliteDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static SqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "sqlite";

    // RETURNING 需要 3.35；用 last_insert_rowid() 兼容更旧的版本
    /// <inheritdoc />
    public override string? LastInsertIdQuery => "SELECT last_insert_rowid()";

    /// <summary>值里含 % 或 _ 时，包含 / 开头 / 结尾改写成 INSTR，避免被当成通配符（与 Java 版一致）。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        renderer.Append("INSTR(").Write(predicate.Operand).Append(",").Write(predicate.Value).Append(")");
        Oracle.OracleDialect.WriteInstrComparison(renderer, predicate);
    }

    /// <summary>SQLite 的类型转换（对应 Java 的 SQLiteCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "({0} NOT IN ('0','false'))",
        _ when targetType == typeof(char) => "substr(cast({0} as character), 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "time({0})",
        _ when targetType == typeof(DateOnly) => "date({0})",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "datetime({0})",
        _ when targetType == typeof(decimal) => "cast({0} as decimal(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "cast({0} as double)",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) => "cast({0} as int2)",
        _ when targetType == typeof(short) || targetType == typeof(ushort) => "CAST({0} AS SMALLINT)",
        _ when IsInteger(targetType) => "CAST({0} AS SIGNED)",
        _ when targetType == typeof(Guid) => "substr(cast({0} as character), 1, 36)",
        _ when targetType == typeof(string) => "cast({0} as character)",
        _ => base.CastTemplate(targetType),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.Length] = Call("LENGTH");
        f[SqlFunctionNames.DateFormat] = DateFormat("strftime({1},{0})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%M"), ("ss", "%S"));
        f[SqlFunctionNames.StringJoin] = Call("GROUP_CONCAT");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("GROUP_CONCAT(DISTINCT ").Write(a[0]).Append(",").Write(a[1]).Append(")");
    }
}
