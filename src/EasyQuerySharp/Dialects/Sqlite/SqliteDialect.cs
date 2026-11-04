namespace EasyQuerySharp.Dialects.Sqlite;

/// <summary>SQLite 方言。驱动用 Microsoft.Data.Sqlite：<c>o.UseSqlite(new SqliteDataSource(cs))</c>（见测试里的 SqliteDataSource 包装）。</summary>
public class SqliteDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static SqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "sqlite";

    // RETURNING 需要 3.35；用 last_insert_rowid() 兼容更旧的版本
    /// <inheritdoc />
    public override string? LastInsertIdQuery => "SELECT last_insert_rowid()";
}
