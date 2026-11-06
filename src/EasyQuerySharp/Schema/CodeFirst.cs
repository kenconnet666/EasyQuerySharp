using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Schema;

/// <summary>
/// code-first（对应 Java 的 DatabaseCodeFirst）：按实体元数据建表、删表，读库结构，与库里的表比较后同步。DDL 写法由方言决定。
/// 列类型取 <see cref="ColumnAttribute.DbType"/>，没写时按 CLR 类型映射；值类型非可空为 NOT NULL，其余 NULL（与 Java 版一致）。
/// 注释取 <see cref="TableAttribute.Comment"/> / <see cref="ColumnAttribute.Comment"/>，索引取 <see cref="TableIndexAttribute"/>，
/// 外键取对一导航上的 <see cref="ForeignKeyAttribute"/>，改名取 <see cref="RenamedFromAttribute"/>。
/// 分库分表的实体（<see cref="EasyDbOptions.Shard{T}(Action{Sharding.ShardRuleBuilder{T}})"/>）按规则的每个节点在节点所在的数据源上建表、删表、同步；
/// 按时间分表时新一期的表不会自动建，定期调用 <see cref="SyncTablesAsync(IEnumerable{Type}, CancellationToken)"/> 补上。
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

    /// <summary>一个实体的建表语句（不执行）：建表、注释、索引、外键，一条一行；分片的实体是每个节点的建表语句。</summary>
    public string CreateTableSql(Type entityType) => string.Join("\n", SchemaPlanner.CreateStatements(_db.Options.Dialect, [.. Tables([entityType]).Select(t => t.Table)]));

    /// <summary>
    /// 建表（连同注释、索引、外键，外键排在全部建表之后）。表已存在时建表语句跳过，但索引、外键会重复建而报错：已有的表用 <see cref="SyncTablesAsync(IEnumerable{Type}, CancellationToken)"/>。
    /// </summary>
    public Task CreateTablesAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default) =>
        Command([.. Tables(entityTypes).GroupBy(t => t.DataSource)
            .SelectMany(g => SchemaPlanner.CreateStatements(_db.Options.Dialect, [.. g.Select(t => t.Table)]).Select(sql => (g.Key, sql)))], []).ExecuteAsync(cancellationToken);

    /// <inheritdoc cref="CreateTablesAsync(IEnumerable{Type}, CancellationToken)"/>
    public Task CreateTablesAsync(params Type[] entityTypes) => CreateTablesAsync(entityTypes, CancellationToken.None);

    /// <summary>依次删表（表不存在时跳过）。</summary>
    public async Task DropTablesIfExistsAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        foreach (var (dataSource, table) in Tables(entityTypes))
        {
            var sql = _db.Options.Dialect.DropTableIfExistsSql(table);
            await _db.Executor.ExecuteAsync(new SqlStatement(sql, sql, []) { DataSource = dataSource }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>依次删表（表不存在时跳过）。</summary>
    public Task DropTablesIfExistsAsync(params Type[] entityTypes) => DropTablesIfExistsAsync(entityTypes, CancellationToken.None);

    /// <summary>
    /// 读库里当前 schema（MySQL 是当前库）的表结构：列、主键、索引、外键（对应 Java 的 getTableInfos，读得更全）。读的是默认数据源。
    /// 不支持读结构的方言（TDengine）抛 <see cref="NotSupportedException"/>。
    /// </summary>
    public Task<IReadOnlyList<DbTableInfo>> ReadTablesAsync(CancellationToken cancellationToken = default) => SchemaReader.ReadAsync(_db, null, cancellationToken);

    /// <summary>实体的表在当前 schema 里是否存在（表名大小写不同也算）；分片的实体要每个节点的表都在。</summary>
    public async Task<bool> TableExistsAsync(Type entityType, CancellationToken cancellationToken = default)
    {
        foreach (var group in Tables([entityType]).GroupBy(t => t.DataSource))
        {
            var tables = await SchemaReader.ReadAsync(_db, group.Key, cancellationToken).ConfigureAwait(false);
            if (!group.All(t => tables.Any(existing => string.Equals(existing.Name, t.Table.TableName, StringComparison.OrdinalIgnoreCase)))) return false;
        }
        return true;
    }

    /// <summary>
    /// 比较实体与库里的表，得出同步要执行的语句，不执行（对应 Java 的 syncTableCommand）：只增不删——库里没有的表整张建（含注释、索引、外键），
    /// 已有的表按 <see cref="RenamedFromAttribute"/> 改表名、列名，加缺的列，补缺的索引与外键；
    /// 库里多出来的列、可空性不一致等不自动处理，写进 <see cref="CodeFirstCommand.Differences"/>。只比较当前 schema 里的表；
    /// 分库的实体按节点所在的数据源分别读结构、比较。
    /// </summary>
    public async Task<CodeFirstCommand> PlanSyncAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        var statements = new List<(string?, string)>();
        var differences = new List<string>();
        foreach (var group in Tables(entityTypes).GroupBy(t => t.DataSource))
        {
            var tables = await SchemaReader.ReadAsync(_db, group.Key, cancellationToken).ConfigureAwait(false);
            var (sqls, diffs) = SchemaPlanner.PlanSync(_db.Options.Dialect, [.. group.Select(t => t.Table)], tables);
            statements.AddRange(sqls.Select(sql => (group.Key, sql)));
            differences.AddRange(diffs.Select(d => group.Key is null ? d : $"{group.Key}：{d}"));
        }
        return Command(statements, differences);
    }

    /// <inheritdoc cref="PlanSyncAsync(IEnumerable{Type}, CancellationToken)"/>
    public Task<CodeFirstCommand> PlanSyncAsync(params Type[] entityTypes) => PlanSyncAsync(entityTypes, CancellationToken.None);

    /// <summary>
    /// 同 <see cref="PlanSyncAsync(IEnumerable{Type}, CancellationToken)"/>，但与给出的库结构比较、不连库：
    /// 例如先用 <see cref="ReadTablesAsync"/> 读出生产库的结构保存下来，之后离线生成迁移语句。给出的结构视为默认数据源的，节点落在别的数据源的分库实体不能离线比较。
    /// </summary>
    public CodeFirstCommand PlanSync(IEnumerable<Type> entityTypes, IReadOnlyList<DbTableInfo> tables)
    {
        var expanded = Tables(entityTypes);
        if (expanded.FirstOrDefault(t => t.DataSource is not null) is { DataSource: { } other })
            throw new NotSupportedException($"离线比较只能比较默认数据源里的表，有分片节点落在数据源 {other}：请用 PlanSyncAsync 按各数据源读结构比较");
        var (statements, differences) = SchemaPlanner.PlanSync(_db.Options.Dialect, [.. expanded.Select(t => t.Table)], tables);
        return Command([.. statements.Select(sql => ((string?)null, sql))], differences);
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

    // 实体展开成要建的表：分片的实体每个节点一张，落在节点的数据源；其余一张，在默认数据源（数据源记为 null）
    private List<(string? DataSource, EntityMetadata Table)> Tables(IEnumerable<Type> entityTypes) =>
    [
        .. entityTypes.SelectMany(type =>
        {
            var entity = _db.Metadata.Get(type);
            return _db.Shards.RuleOf(type) is { } rule
                ? rule.Nodes.Select(n => (n.DataSource == _db.Options.DefaultDataSourceName ? null : n.DataSource, entity.ForTable(n.Table)))
                : [((string?)null, entity)];
        }),
    ];

    private CodeFirstCommand Command(IReadOnlyList<(string?, string)> statements, IReadOnlyList<string> differences) => new(_db, statements, differences);
}
