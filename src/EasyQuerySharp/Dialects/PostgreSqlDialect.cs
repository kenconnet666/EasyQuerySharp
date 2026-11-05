using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// PostgreSQL 方言（17 实测）。openGauss、人大金仓 PG 模式共用。驱动用 Npgsql：<c>o.UsePostgreSql(NpgsqlDataSource.Create(cs))</c>。
/// </summary>
public class PostgreSqlDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static PostgreSqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "pgsql";

    /// <summary>PostgreSQL 的 SET 列不能带表别名（<c>SET t."a"</c> 会被当成复合列）。</summary>
    public override bool QualifySetColumns => false;

    /// <summary>二进制列用 BYTEA，其余同标准类型。</summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) =>
        type == typeof(byte[]) ? "BYTEA" : base.ClrColumnType(type, column);

    /// <summary>
    /// <c>UPDATE "a" t SET "c" = t1."d" FROM "b" t1 , "e" t2 WHERE 各 ON 条件 AND 原条件</c>（与 Java 逐字一致）：
    /// 连接的表写在 FROM 里，ON 条件按顺序并入 WHERE。
    /// </summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").WriteTable(update.Table).Append(" SET ").WriteSets(update.Sets).Append(" FROM ");
        WriteJoinedTables(renderer, update.Joins, update.Where);
    }

    /// <summary><c>DELETE FROM "a" t USING "b" t1 WHERE 各 ON 条件 AND 原条件</c>（与 Java 逐字一致）。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTable(statement.Table).Append(" USING ");
        WriteJoinedTables(renderer, statement.Joins, statement.Where);
    }

    // 逗号分隔的表，ON 条件按连接顺序排在原条件前面
    private static void WriteJoinedTables(SqlRenderer renderer, List<JoinClause> joins, List<SqlExpr> where)
    {
        for (var i = 0; i < joins.Count; i++)
        {
            if (i > 0) renderer.Append(" , ");
            renderer.WriteTable(joins[i].Table);
        }
        renderer.WriteWhere([.. joins.Where(j => j.On is not null).Select(j => j.On!), .. where]);
    }

    /// <summary>
    /// 值里含 % 或 _ 时，包含 / 开头 / 结尾写成 <c>LIKE CONCAT('%',(?)::TEXT,'%') ESCAPE '\'</c>，参数里的 \ % _ 先转义，
    /// 值里的通配符按字面匹配（与 Java 版一致）。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        var value = predicate.Value is ParameterExpr { Value: string text } ? new ParameterExpr(EscapeLike(text), typeof(string)) : predicate.Value;
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE CONCAT(" : " LIKE CONCAT(");
        if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%',");
        renderer.Append("(").Write(value).Append(")::TEXT");
        if (predicate.Mode != LikeMode.EndsWith) renderer.Append(",'%'");
        renderer.Append(") ESCAPE '\\'");
    }

    /// <summary>PostgreSQL 的 <c>(x)::类型</c> 转换（对应 Java 的 PgSQLCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "(({0})::varchar not in ('0','false','f','no'))",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) || targetType == typeof(short) => "({0})::int2",
        _ when targetType == typeof(char) => "substr(({0})::char, 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "({0})::time",
        _ when targetType == typeof(DateOnly) => "({0})::date",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "({0})::timestamp",
        _ when targetType == typeof(decimal) => "({0})::numeric",
        _ when targetType == typeof(double) || targetType == typeof(float) => "({0})::float8",
        _ when targetType == typeof(int) || targetType == typeof(ushort) => "({0})::int4",
        _ when targetType == typeof(long) || targetType == typeof(uint) || targetType == typeof(ulong) => "({0})::int8",
        _ when targetType == typeof(Guid) => "({0})::uuid",
        _ when targetType == typeof(string) => "({0})::text",
        _ => base.CastTemplate(targetType),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG((").Write(a[0]).Append(")::TEXT, ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("STRING_AGG(DISTINCT (").Write(a[0]).Append(")::TEXT, ").Write(a[1]).Append(")");
    }

}
