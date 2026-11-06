using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// 华为 GaussDB / openGauss 方言（对应 Java 的 sql-gauss-db）：PG 内核，只覆盖与 Java 的 GaussDB 方言不同于 PostgreSQL 的部分——
/// 包含 / 开头 / 结尾写成 STRPOS、条件当值写成 CASE。驱动由调用方通过 <see cref="System.Data.Common.DbDataSource"/> 传入：<c>o.UseGaussDb(dataSource)</c>。
/// </summary>
public class GaussDbDialect : PostgreSqlDialect
{
    /// <summary>共享实例。</summary>
    public static new GaussDbDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "gauss";

    /// <summary>PG 写成 <c>LIKE CONCAT(…)</c> 的包含 / 开头 / 结尾改写成 STRPOS（与 Java 的 GaussDBLikeSQLFunction 一致），其余同 PG。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!WriteStrpos(renderer, predicate)) base.WriteLike(renderer, predicate);
    }

    /// <summary>
    /// 值是含 % 或 _ 的常量、或模式在 SQL 里拼（PG 方言在这两种情况下写 <c>LIKE CONCAT(…)</c>）时写成
    /// <c>STRPOS(x,?) &gt; 0</c> / <c>= 1</c> / <c>= (CHAR_LENGTH(x) - CHAR_LENGTH(?) + 1)</c>，值不转义；返回是否写了。Kingbase 共用。
    /// </summary>
    internal static bool WriteStrpos(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue && predicate is not { Pattern: FunctionExpr { Name: SqlFunctionNames.Concat }, Value: not null }) return false;
        renderer.Append("STRPOS(").Write(predicate.Operand).Append(",").Write(predicate.Value!).Append(")");
        OracleDialect.WriteInstrComparison(renderer, predicate, "CHAR_LENGTH");
        return true;
    }

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 条件当值：与 Java 的 GaussDBBooleanSQLFunction 一致
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
    }
}
