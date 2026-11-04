using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Schema;

/// <summary>
/// code-first：按实体元数据建表、删表（对应 Java 的 DatabaseCodeFirst）。DDL 写法由方言决定（<see cref="Dialects.SqlDialect.CreateTableSql"/>）。
/// 列类型取 <see cref="ColumnAttribute.DbType"/>，没写时按 CLR 类型映射；值类型非可空为 NOT NULL，其余 NULL（与 Java 版一致）。
/// </summary>
/// <example><code>
/// await db.CodeFirst.DropTablesIfExistsAsync(typeof(Topic), typeof(Blog));
/// await db.CodeFirst.CreateTablesAsync(typeof(Topic), typeof(Blog));
/// </code></example>
public sealed class CodeFirst
{
    private readonly EasyDb _db;

    internal CodeFirst(EasyDb db) => _db = db;

    /// <summary>建表语句（不执行）。</summary>
    public string CreateTableSql(Type entityType) => _db.Options.Dialect.CreateTableSql(_db.Metadata.Get(entityType));

    /// <summary>依次建表（表已存在时跳过）。</summary>
    public async Task CreateTablesAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        foreach (var type in entityTypes)
        {
            var sql = CreateTableSql(type);
            await _db.Executor.ExecuteAsync(new SqlStatement(sql, sql, []), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>依次建表（表已存在时跳过）。</summary>
    public Task CreateTablesAsync(params Type[] entityTypes) => CreateTablesAsync(entityTypes, CancellationToken.None);

    /// <summary>依次删表（表不存在时跳过）。</summary>
    public async Task DropTablesIfExistsAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        foreach (var type in entityTypes)
        {
            var sql = _db.Options.Dialect.DropTableIfExistsSql(_db.Metadata.Get(type));
            await _db.Executor.ExecuteAsync(new SqlStatement(sql, sql, []), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>依次删表（表不存在时跳过）。</summary>
    public Task DropTablesIfExistsAsync(params Type[] entityTypes) => DropTablesIfExistsAsync(entityTypes, CancellationToken.None);
}
