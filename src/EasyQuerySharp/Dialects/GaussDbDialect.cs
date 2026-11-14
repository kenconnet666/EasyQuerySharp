using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// 华为 GaussDB / openGauss 方言（对应 Java 的 sql-gauss-db）：PG 内核，只覆盖与 Java 的 GaussDB 方言不同于 PostgreSQL 的部分——
/// 包含 / 开头 / 结尾写成 STRPOS、条件当值写成 CASE。
/// Java 照 PG 写、在 openGauss 7 上实测不能用而改掉的（与 TS 版一致）：自增列写 SERIAL / BIGSERIAL，upsert 写 <c>ON DUPLICATE KEY UPDATE</c>，以 10 为底的对数写单参数 LOG。
/// 驱动由调用方通过 <see cref="System.Data.Common.DbDataSource"/> 传入：<c>o.UseGaussDb(dataSource)</c>；
/// 用 Npgsql 时连接串要加 <c>No Reset On Close=true</c>（openGauss 没有连接回池时 Npgsql 发的 <c>DISCARD ALL</c>），且只能用 md5 认证（Npgsql 不认 openGauss 的 sha256 认证）。
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

    /// <summary>
    /// 自增列写成 SERIAL / BIGSERIAL：openGauss 不支持 <c>GENERATED … AS IDENTITY</c>（Java 照 PG 写，实库报语法错误；TS 版同样改成 SERIAL）。
    /// </summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => column.IsGenerated && (type == typeof(int) || type == typeof(long))
        ? type == typeof(int) ? "SERIAL" : "BIGSERIAL"
        : base.ClrColumnType(type, column);

    /// <inheritdoc />
    private protected override string GeneratedColumnTail(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) => "";

    /// <summary>ON DUPLICATE KEY 在任何唯一键上冲突都会更新，更新到的可能是主键以外的行。</summary>
    internal override bool UpsertMatchesOnlyConflictColumns => false;

    /// <summary>
    /// openGauss 没有 <c>ON CONFLICT</c>：忽略冲突写 <c>ON DUPLICATE KEY UPDATE NOTHING</c>，更新写 <c>ON DUPLICATE KEY UPDATE "c" = EXCLUDED."c", …</c>；
    /// 冲突由表上的主键 / 唯一约束决定，指定的 conflictColumns 不写进 SQL。没有要更新的列时不写。
    /// </summary>
    public override void WriteInsertConflict(SqlRenderer renderer, InsertStatement insert)
    {
        var conflict = insert.Conflict!;
        if (conflict.Ignore)
        {
            renderer.Append(" ON DUPLICATE KEY UPDATE NOTHING");
            return;
        }
        if (conflict.UpdateColumns!.Count == 0) return;
        renderer.Append(" ON DUPLICATE KEY UPDATE ").Append(string.Join(", ", conflict.UpdateColumns.Select(c =>
        {
            var name = QuoteIdentifier(c.Name);
            return $"{name} = EXCLUDED.{name}";
        })));
    }

    /// <summary>openGauss 的 pg_attribute 没有 attidentity：默认值取序列的就是自增列（同 TS 版）。</summary>
    private protected override string IntrospectedIdentity => "coalesce(pg_get_expr(d.adbin, d.adrelid), '') LIKE 'nextval(%'";

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 条件当值：与 Java 的 GaussDBBooleanSQLFunction 一致
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
        // 整数 / 整数得到小数（Oracle 兼容）
        f[SqlFunctionNames.IntegerDivide] = IntegerDivideByTrunc;
        // 没有 LOG10（PG 12 才有）；单参数的 LOG 就是以 10 为底
        f[SqlFunctionNames.Log10] = Call("LOG");
    }
}
