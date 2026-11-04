using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects.PostgreSql;

/// <summary>
/// PostgreSQL 方言（17 实测）。openGauss、人大金仓 PG 模式共用。驱动用 Npgsql：<c>o.UsePostgreSql(NpgsqlDataSource.Create(cs))</c>。
/// </summary>
public class PostgreSqlDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static PostgreSqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "pgsql";

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
