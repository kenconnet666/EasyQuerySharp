using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Schema;

/// <summary>
/// code-first（对应 Java 的 DatabaseCodeFirst）：按实体元数据建表、删表，读库结构，与库里的表比较后同步。DDL 写法由方言决定。
/// 列类型取 <see cref="ColumnAttribute.DbType"/>，没写时按 CLR 类型映射；值类型非可空为 NOT NULL，其余 NULL（与 Java 版一致）。
/// 注释取 <see cref="TableAttribute.Comment"/> / <see cref="ColumnAttribute.Comment"/>，索引取 <see cref="TableIndexAttribute"/>，
/// 外键取对一导航上的 <see cref="ForeignKeyAttribute"/>，改名取 <see cref="RenamedFromAttribute"/>。
/// </summary>
/// <example><code>
/// await db.CodeFirst.DropTablesIfExistsAsync(typeof(Topic), typeof(Blog));
/// await db.CodeFirst.CreateTablesAsync(typeof(Topic), typeof(Blog));
/// var sync = await db.CodeFirst.SyncTablesAsync(typeof(Topic), typeof(Blog)); // 只增不删：建缺的表、加缺的列、改名、补索引与外键
/// </code></example>
public sealed class CodeFirst
{
    private readonly EasyDb _db;

    internal CodeFirst(EasyDb db) => _db = db;

    /// <summary>一个实体的建表语句（不执行）：建表、注释、索引、外键，一条一行。</summary>
    public string CreateTableSql(Type entityType) => string.Join("\n", SchemaPlanner.CreateStatements(_db.Options.Dialect, [_db.Metadata.Get(entityType)]));

    /// <summary>
    /// 建表（连同注释、索引、外键，外键排在全部建表之后）。表已存在时建表语句跳过，但索引、外键会重复建而报错：已有的表用 <see cref="SyncTablesAsync(IEnumerable{Type}, CancellationToken)"/>。
    /// </summary>
    public Task CreateTablesAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default) =>
        Command(SchemaPlanner.CreateStatements(_db.Options.Dialect, Entities(entityTypes)), []).ExecuteAsync(cancellationToken);

    /// <inheritdoc cref="CreateTablesAsync(IEnumerable{Type}, CancellationToken)"/>
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

    /// <summary>
    /// 读库里当前 schema（MySQL 是当前库）的表结构：列、主键、索引、外键（对应 Java 的 getTableInfos，读得更全）。
    /// 不支持读结构的方言（TDengine）抛 <see cref="NotSupportedException"/>。
    /// </summary>
    public Task<IReadOnlyList<DbTableInfo>> ReadTablesAsync(CancellationToken cancellationToken = default) => SchemaReader.ReadAsync(_db, cancellationToken);

    /// <summary>实体的表在当前 schema 里是否存在（表名大小写不同也算）。</summary>
    public async Task<bool> TableExistsAsync(Type entityType, CancellationToken cancellationToken = default)
    {
        var name = _db.Metadata.Get(entityType).TableName;
        var tables = await ReadTablesAsync(cancellationToken).ConfigureAwait(false);
        return tables.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 比较实体与库里的表，得出同步要执行的语句，不执行（对应 Java 的 syncTableCommand）：只增不删——库里没有的表整张建（含注释、索引、外键），
    /// 已有的表按 <see cref="RenamedFromAttribute"/> 改表名、列名，加缺的列，补缺的索引与外键；
    /// 库里多出来的列、可空性不一致等不自动处理，写进 <see cref="CodeFirstCommand.Differences"/>。只比较当前 schema 里的表。
    /// </summary>
    public async Task<CodeFirstCommand> PlanSyncAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default) =>
        PlanSync(entityTypes, await ReadTablesAsync(cancellationToken).ConfigureAwait(false));

    /// <inheritdoc cref="PlanSyncAsync(IEnumerable{Type}, CancellationToken)"/>
    public Task<CodeFirstCommand> PlanSyncAsync(params Type[] entityTypes) => PlanSyncAsync(entityTypes, CancellationToken.None);

    /// <summary>
    /// 同 <see cref="PlanSyncAsync(IEnumerable{Type}, CancellationToken)"/>，但与给出的库结构比较、不连库：
    /// 例如先用 <see cref="ReadTablesAsync"/> 读出生产库的结构保存下来，之后离线生成迁移语句。
    /// </summary>
    public CodeFirstCommand PlanSync(IEnumerable<Type> entityTypes, IReadOnlyList<DbTableInfo> tables)
    {
        var (statements, differences) = SchemaPlanner.PlanSync(_db.Options.Dialect, Entities(entityTypes), tables);
        return Command(statements, differences);
    }

    /// <summary>同 <see cref="PlanSyncAsync(IEnumerable{Type}, CancellationToken)"/>，并且执行得出的语句；返回执行的语句与没有处理的差异。</summary>
    public async Task<CodeFirstCommand> SyncTablesAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        var command = await PlanSyncAsync(entityTypes, cancellationToken).ConfigureAwait(false);
        await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return command;
    }

    /// <inheritdoc cref="SyncTablesAsync(IEnumerable{Type}, CancellationToken)"/>
    public Task<CodeFirstCommand> SyncTablesAsync(params Type[] entityTypes) => SyncTablesAsync(entityTypes, CancellationToken.None);

    private List<EntityMetadata> Entities(IEnumerable<Type> entityTypes) => entityTypes.Select(_db.Metadata.Get).ToList();

    private CodeFirstCommand Command(IReadOnlyList<string> statements, IReadOnlyList<string> differences) => new(_db, statements, differences);
}
