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
        var value = predicate.Value is ParameterExpr { Value: string text } ? new ParameterExpr(Escape(text), typeof(string)) : predicate.Value;
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE CONCAT(" : " LIKE CONCAT(");
        if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%',");
        renderer.Append("(").Write(value).Append(")::TEXT");
        if (predicate.Mode != LikeMode.EndsWith) renderer.Append(",'%'");
        renderer.Append(") ESCAPE '\\'");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
