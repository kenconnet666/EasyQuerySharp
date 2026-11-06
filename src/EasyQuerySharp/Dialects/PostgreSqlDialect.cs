using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// PostgreSQL 方言（17 实测）。驱动用 Npgsql：<c>o.UsePostgreSql(NpgsqlDataSource.Create(cs))</c>。
/// PG 内核的 Kingbase、GaussDB / openGauss 各有子类（<see cref="KingbaseDialect"/>、<see cref="GaussDbDialect"/>）。
/// </summary>
public class PostgreSqlDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static PostgreSqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "pgsql";

    /// <inheritdoc />
    public override bool SupportsWriteReturning => true;

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
        WriteCommaJoinedTables(renderer, update.Joins, update.Where);
    }

    /// <summary><c>DELETE FROM "a" t USING "b" t1 WHERE 各 ON 条件 AND 原条件</c>（与 Java 逐字一致）。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTable(statement.Table).Append(" USING ");
        WriteCommaJoinedTables(renderer, statement.Joins, statement.Where);
    }

    /// <summary>
    /// 模式在 SQL 里拼（<c>Sql.LikeContains</c> 等）时值先转成 TEXT：<c>LIKE CONCAT('%',(?)::TEXT,'%')</c>；
    /// 值里含 % 或 _ 时同样写成 CONCAT，参数里的 \ % _ 先转义并加 <c>ESCAPE '\'</c>，值里的通配符按字面匹配（与 Java 版一致）。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        var wildcard = predicate.HasWildcardValue;
        var concat = predicate is { Pattern: FunctionExpr { Name: SqlFunctionNames.Concat }, Value: not null };
        if (!wildcard && !concat)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        var value = wildcard && predicate.Value is ParameterExpr { Value: string text } ? new ParameterExpr(EscapeLike(text), typeof(string)) : predicate.Value!;
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE CONCAT(" : " LIKE CONCAT(");
        if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%',");
        renderer.Append("(").Write(value).Append(")::TEXT");
        if (predicate.Mode != LikeMode.EndsWith) renderer.Append(",'%'");
        renderer.Append(wildcard ? ") ESCAPE '\\'" : ")");
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

    /// <summary>json / jsonb 列的参数写成 <c>(?)::jsonb</c>：驱动把字符串当 text 发送，不转换时写不进 jsonb 列。</summary>
    public override (string Before, string After)? ParameterWrap(string? columnDbType) =>
        columnDbType?.Trim().ToLowerInvariant() is "json" or "jsonb" ? ("(", ")::" + columnDbType.Trim().ToLowerInvariant()) : null;

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG((").Write(a[0]).Append(")::TEXT, ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("STRING_AGG(DISTINCT (").Write(a[0]).Append(")::TEXT, ").Write(a[1]).Append(")");
        // 与 Java 的 PgSQLDateTimeFormatSQLFunction 一致：先转成 TIMESTAMP 再格式化
        f[SqlFunctionNames.NullSafeEquals] = Template("({0} IS NOT DISTINCT FROM {1})");
        f[SqlFunctionNames.MaxColumns] = Call("GREATEST");
        f[SqlFunctionNames.MinColumns] = Call("LEAST");
        f[SqlFunctionNames.Power] = Call("POW");
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        // JSON：列须是 json / jsonb（与 Java 的 PgSQLJSON*SQLFunction 一致）
        f[SqlFunctionNames.JsonGet] = Template("({0} -> '{1}')");
        f[SqlFunctionNames.JsonGetIndex] = Template("({0} -> {1})");
        f[SqlFunctionNames.JsonText] = Template("({0} ->> '{1}')");
        f[SqlFunctionNames.JsonTextIndex] = Template("({0} ->> {1})");
        f[SqlFunctionNames.JsonLength] = Call("JSONB_ARRAY_LENGTH");
        f[SqlFunctionNames.JsonContainsKey] = Template("JSONB_EXISTS({0}, '{1}')");
        foreach (var unit in Enum.GetValues<DateTruncUnit>())
            f[SqlFunctionNames.DateTrunc(unit)] = Template("DATE_TRUNC('" + unit.ToString().ToLowerInvariant() + "', {0})");
        f[SqlFunctionNames.DateFormat] = DateFormat("TO_CHAR(({0})::TIMESTAMP,{1})", ("yyyy", "YYYY"), ("MM", "MM"), ("dd", "DD"), ("HH", "HH24"), ("mm", "MI"), ("ss", "SS"));
    }

}
