using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Schema;

/// <summary>
/// code-first 的语句规划：建表要执行哪些语句、实体与库里的表比较后同步要执行哪些语句。
/// 同步只做"安全"的事——建缺的表、按 <see cref="RenamedFromAttribute"/> 改表名列名、加缺的列、建缺的索引与外键；
/// 库里多出来的列、可空性不一致等只写进差异，永不删表删列、不改类型（与 Java 的 syncTableCommand 一致）。
/// </summary>
internal static class SchemaPlanner
{
    /// <summary>建表：每张表依次是建表语句、注释、索引；外键排在全部建表之后（被引用的表要先建好），写在建表语句里的方言不另加。</summary>
    public static List<string> CreateStatements(SqlDialect dialect, IReadOnlyList<EntityMetadata> entities)
    {
        var statements = new List<string>();
        foreach (var entity in entities)
        {
            statements.Add(dialect.CreateTableSql(entity));
            AddIfNotNull(statements, dialect.TableCommentSql(entity));
            foreach (var column in SqlDialect.TableColumns(entity)) AddIfNotNull(statements, dialect.ColumnCommentSql(entity, column));
            statements.AddRange(entity.Indexes.Select(index => dialect.CreateIndexSql(entity, index)));
        }
        if (!dialect.InlineForeignKeys)
            statements.AddRange(entities.SelectMany(e => e.ForeignKeys).Select(dialect.AddForeignKeySql));
        return statements;
    }

    /// <summary>比较实体与库里的表：库里没有的表整张建（排在最前），已有的表改名、加列、补索引与外键。</summary>
    public static (List<string> Statements, List<string> Differences) PlanSync(SqlDialect dialect, IReadOnlyList<EntityMetadata> entities, IReadOnlyList<DbTableInfo> tables)
    {
        var created = new List<EntityMetadata>();
        var statements = new List<string>();
        var differences = new List<string>();
        foreach (var entity in entities)
        {
            var table = Find(tables, entity.TableName);
            if (table is null && entity.RenamedFrom is { } oldName && Find(tables, oldName) is { } old)
            {
                statements.Add(dialect.RenameTableSql(entity, old.Name));
                table = old;
            }
            if (table is null) created.Add(entity);
            else Diff(dialect, entity, table, statements, differences);
        }
        return ([.. CreateStatements(dialect, created), .. statements], differences);
    }

    private static void Diff(SqlDialect dialect, EntityMetadata entity, DbTableInfo table, List<string> statements, List<string> differences)
    {
        var where = entity.TableName;
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in SqlDialect.TableColumns(entity))
        {
            declared.Add(column.Name);
            var existing = table.FindColumn(column.Name);
            if (existing is null && column.RenamedFrom is { } oldName && table.FindColumn(oldName) is { } old)
            {
                statements.Add(dialect.RenameColumnSql(entity, old.Name, column));
                declared.Add(old.Name);
                existing = old;
            }
            if (existing is null)
            {
                statements.Add(dialect.AddColumnSql(entity, column));
                AddIfNotNull(statements, dialect.ColumnCommentSql(entity, column));
                continue;
            }
            var nullable = SqlDialect.ColumnNullable(column);
            if (!column.IsPrimaryKey && existing.Nullable != nullable)
                differences.Add($"{where}.{column.Name}：库里是 {(existing.Nullable ? "NULL" : "NOT NULL")}，实体声明 {(nullable ? "NULL" : "NOT NULL")}");
        }
        differences.AddRange(table.Columns.Where(c => !declared.Contains(c.Name)).Select(c => $"{where}.{c.Name}：库里有这一列，实体上没有"));

        foreach (var index in entity.Indexes)
            if (!table.Indexes.Any(i => string.Equals(i.Name, index.Name, StringComparison.OrdinalIgnoreCase)))
                statements.Add(dialect.CreateIndexSql(entity, index));

        foreach (var foreignKey in entity.ForeignKeys)
        {
            var columns = foreignKey.Columns.Select(c => c.Name).ToList();
            if (table.ForeignKeys.Any(f => string.Equals(f.Name, foreignKey.Name, StringComparison.OrdinalIgnoreCase)
                                           || f.Columns.SequenceEqual(columns, StringComparer.OrdinalIgnoreCase))) continue;
            if (dialect.InlineForeignKeys) differences.Add($"{where}：缺外键 {foreignKey.Name}，{dialect.Name} 不能给已有的表加外键");
            else statements.Add(dialect.AddForeignKeySql(foreignKey));
        }
    }

    /// <summary>库里叫这个名字的表：先按原样比较，再忽略大小写（MySQL 在 Windows 上把表名转成小写，Oracle 转成大写）。</summary>
    private static DbTableInfo? Find(IReadOnlyList<DbTableInfo> tables, string name) =>
        tables.FirstOrDefault(t => t.Name == name) ?? tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void AddIfNotNull(List<string> statements, string? sql)
    {
        if (sql is not null) statements.Add(sql);
    }
}
