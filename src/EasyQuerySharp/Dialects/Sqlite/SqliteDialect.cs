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

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.Length] = Call("LENGTH");
    }
}
