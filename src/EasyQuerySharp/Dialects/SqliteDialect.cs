using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

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

    /// <summary>UPDATE / DELETE … RETURNING 需要 SQLite 3.35 起（Microsoft.Data.Sqlite 自带的版本满足）。</summary>
    public override bool SupportsWriteReturning => true;

    /// <summary>SQLite 的 SET 列不能带表别名。</summary>
    public override bool QualifySetColumns => false;

    /// <summary>OFFSET 前必须有 LIMIT：只跳过行数时写 <c>LIMIT -1 OFFSET m</c>（负数表示不限）。</summary>
    public override string OffsetOnlyLimit => "-1";

    /// <summary>
    /// SQLite 的 UNION 分支不能加括号，也不能自带 ORDER BY / LIMIT（只能写在整个 UNION 后面）：带了的分支包一层 <c>SELECT * FROM (q)</c>，
    /// 其余直接写 <c>q</c>。
    /// </summary>
    public override void WriteCompoundPart(SqlRenderer renderer, SelectQuery part)
    {
        if (IsOrderedOrPaged(part)) renderer.Append("SELECT * FROM (").WriteSelect(part).Append(")");
        else renderer.WriteSelect(part);
    }

    /// <summary><c>UPDATE "a" AS t SET "c" = t1."d" FROM "b" t1 , "c" t2 WHERE 各 ON 条件 AND 原条件</c>（SQLite 3.33 起；目标表的别名要写 AS）。</summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").WriteTableName(update.Table).Append(" AS " + update.Table.Alias).Append(" SET ").WriteSets(update.Sets).Append(" FROM ");
        WriteCommaJoinedTables(renderer, update.Joins, update.Where);
    }

    /// <summary><c>DELETE FROM "a" AS t WHERE EXISTS(SELECT 1 FROM "b" t1 WHERE 各 ON 条件 AND 原条件)</c>：SQLite 的 DELETE 没有 USING / JOIN。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTableName(statement.Table).Append(" AS " + statement.Table.Alias).Append(" WHERE EXISTS(SELECT 1 FROM ");
        WriteCommaJoinedTables(renderer, statement.Joins, statement.Where);
        renderer.Append(")");
    }

    /// <summary>SQLite 没有 ROLLUP / CUBE。</summary>
    public override void WriteGroupBy(SqlRenderer renderer, IReadOnlyList<SqlExpr> keys, GroupByMode mode)
    {
        if (mode != GroupByMode.Plain) throw new NotSupportedException($"SQLite 不支持 GROUP BY {mode.ToString().ToUpperInvariant()}");
        base.WriteGroupBy(renderer, keys, mode);
    }

    /// <summary>UPDATE / DELETE 的目标表别名必须写 AS（<c>UPDATE "t" AS t SET …</c>）。</summary>
    public override string WriteTargetAliasSeparator => " AS ";

    /// <summary>自增主键只能写成列定义里的 <c>INTEGER PRIMARY KEY AUTOINCREMENT</c>（整型必须是 INTEGER 才是 rowid 别名）。</summary>
    protected override string ColumnDefinition(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        InlinePrimaryKey(column) ? $"{QuoteIdentifier(column.Name)} INTEGER PRIMARY KEY AUTOINCREMENT" : base.ColumnDefinition(entity, column);

    /// <inheritdoc />
    protected override bool InlinePrimaryKey(Metadata.ColumnMetadata key) => key is { IsGenerated: true, IsPrimaryKey: true };

    /// <summary>类型名与 Java 的 SQLiteMigrationEntityParser 一致：integer、boolean、double、decimal(16,2)，日期时间都是 TEXT。</summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(int) || type == typeof(long) || type.IsEnum => "integer",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(byte) || type == typeof(sbyte) => "int2",
        _ when type == typeof(bool) => "boolean",
        _ when type == typeof(float) => "float",
        _ when type == typeof(double) => "double",
        _ when type == typeof(decimal) => "decimal(16,2)",
        _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) => "TEXT",
        _ when type == typeof(byte[]) => "blob",
        _ => base.ClrColumnType(type, column),
    };

    // 下面的建表与同步语句与 Java 的 SQLiteDatabaseMigrationProvider 一致（Java 改列名时多写了类型、自增列的写法是错的，这里按 SQLite 的语法写）

    /// <summary>多行建表（见 <see cref="SqlDialect.MultiLineCreateTable"/>），外键写在语句里，以 <c>);</c> 结尾；SQLite 没有注释。</summary>
    public override string CreateTableSql(Metadata.EntityMetadata entity) => MultiLineCreateTable(entity, _ => "", ");");

    /// <inheritdoc />
    internal override string? TableCommentSql(Metadata.EntityMetadata entity) => null;

    /// <inheritdoc />
    internal override string? ColumnCommentSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) => null;

    /// <summary>SQLite 不能给已有的表加外键，只能写在建表语句里。</summary>
    internal override bool InlineForeignKeys => true;

    /// <summary><c>CREATE [UNIQUE ]INDEX 名 ON "表" ("列" ASC,…);</c></summary>
    internal override string CreateIndexSql(Metadata.EntityMetadata entity, Metadata.IndexMetadata index) =>
        $"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {index.Name} ON {QualifiedTable(entity)} ({IndexColumns(index, ",")});";

    /// <summary><c>ALTER TABLE "旧表" RENAME TO "新表";</c>（新表名不能带 schema）。</summary>
    internal override string RenameTableSql(Metadata.EntityMetadata entity, string oldName) =>
        $"ALTER TABLE {QualifiedName(entity.Schema, oldName)} RENAME TO {QuoteIdentifier(entity.TableName)};";

    /// <summary><c>ALTER TABLE "表" RENAME COLUMN "旧列" TO "新列";</c>（3.25 起）。</summary>
    internal override string RenameColumnSql(Metadata.EntityMetadata entity, string oldName, Metadata.ColumnMetadata column) =>
        base.RenameColumnSql(entity, oldName, column) + ";";

    /// <summary><c>ALTER TABLE "表" ADD COLUMN "列" 类型 NULL;</c></summary>
    internal override string AddColumnSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        $"ALTER TABLE {QualifiedTable(entity)} ADD COLUMN {QuoteIdentifier(column.Name)} {ColumnType(column)}{(IsNullable(column) ? " NULL" : " NOT NULL")};";

    /// <summary>
    /// 读 sqlite_master 与 pragma_table_xinfo / index_list / index_info / foreign_key_list（表值函数，3.16 起）；SQLite 没有注释，
    /// 外键没有名字，按 <c>fk_表_序号</c> 命名（同步时按列比较）。
    /// </summary>
    internal override CatalogQueries CatalogQueries { get; } = new(
        "SELECT name AS table_name, NULL AS comment FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
        // 主键列可以不写 NOT NULL，按声明它不会是 NULL；建表语句带 AUTOINCREMENT 时整数主键是自增列
        "SELECT m.name AS table_name, p.name AS column_name, p.type AS column_type, p.\"notnull\" = 0 AND p.pk = 0 AS is_nullable, p.dflt_value AS column_default, "
        + "NULL AS comment, p.pk = 1 AND m.sql LIKE '%AUTOINCREMENT%' AS is_generated, p.pk > 0 AS primary_key "
        + "FROM sqlite_master m JOIN pragma_table_xinfo(m.name) p WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' AND p.hidden <> 1 ORDER BY m.name, p.cid",
        "SELECT m.name AS table_name, l.name AS index_name, l.\"unique\" AS is_unique, i.name AS column_name "
        + "FROM sqlite_master m JOIN pragma_index_list(m.name) l JOIN pragma_index_info(l.name) i "
        + "WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' AND l.origin <> 'pk' ORDER BY m.name, l.name, i.seqno",
        "SELECT m.name AS table_name, 'fk_' || m.name || '_' || f.id AS constraint_name, f.\"from\" AS column_name, f.\"table\" AS referenced_table, f.\"to\" AS referenced_column "
        + "FROM sqlite_master m JOIN pragma_foreign_key_list(m.name) f WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' ORDER BY m.name, f.id, f.seq");

    /// <summary>
    /// SQLite 没有行锁语法（写事务锁整个库），FOR UPDATE 会是语法错误，所以直接拒绝。
    /// Java 版照样拼出 FOR UPDATE，执行时才失败，这里提前报错。
    /// </summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock) =>
        throw new NotSupportedException("SQLite 不支持 FOR UPDATE 行锁：写事务会锁住整个库，需要先占写锁时用 BEGIN IMMEDIATE");

    /// <summary>
    /// 值里含 % 或 _ 时，包含 / 开头 / 结尾改写成 INSTR，避免被当成通配符；模式在 SQL 里拼（列、<c>Sql.LikeContains</c> 等）时
    /// 写 <c>LIKE ('%'||v||'%')</c>（都与 Java 的 SQLiteLikeSQLFunction 一致；CONCAT 要 SQLite 3.44 起才有）。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue && predicate is { Pattern: FunctionExpr { Name: SqlFunctionNames.Concat }, Value: not null })
        {
            renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE (" : " LIKE (");
            if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%'||");
            renderer.Write(predicate.Value);
            if (predicate.Mode != LikeMode.EndsWith) renderer.Append("||'%'");
            renderer.Append(")");
            return;
        }
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        renderer.Append("INSTR(").Write(predicate.Operand).Append(",").Write(predicate.Value).Append(")");
        OracleDialect.WriteInstrComparison(renderer, predicate);
    }

    /// <summary>SQLite 的类型转换（对应 Java 的 SQLiteCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "({0} NOT IN ('0','false'))",
        _ when targetType == typeof(char) => "substr(cast({0} as character), 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "time({0})",
        _ when targetType == typeof(DateOnly) => "date({0})",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "datetime({0})",
        _ when targetType == typeof(decimal) => "cast({0} as decimal(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "cast({0} as double)",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) => "cast({0} as int2)",
        _ when targetType == typeof(short) || targetType == typeof(ushort) => "CAST({0} AS SMALLINT)",
        _ when IsInteger(targetType) => "CAST({0} AS SIGNED)",
        _ when targetType == typeof(Guid) => "substr(cast({0} as character), 1, 36)",
        _ when targetType == typeof(string) => "cast({0} as character)",
        _ => base.CastTemplate(targetType),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.Length] = Call("LENGTH");
        // 多参数的 MAX / MIN 是标量函数（SQLite 没有 GREATEST / LEAST，Java 版在这里写 GREATEST，在 SQLite 上执行不了）
        f[SqlFunctionNames.MaxColumns] = Call("MAX");
        f[SqlFunctionNames.MinColumns] = Call("MIN");
        f[SqlFunctionNames.NullSafeEquals] = Template("({0} IS {1})");
        // instr 精确匹配、区分大小写；LIKE 不区分 ASCII 大小写，键里的 % _ 还会被当通配符
        // 日期存成文本：按格式截断后仍是 'yyyy-MM-dd HH:mm:ss'；周从周一起（与 MySQL 的 WEEKDAY 一致）
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Year)] = Template("strftime('%Y-01-01 00:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Quarter)] = Template("datetime(strftime('%Y-%m-01', {0}), '-' || ((CAST(strftime('%m', {0}) AS INTEGER) - 1) % 3) || ' months')");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Month)] = Template("strftime('%Y-%m-01 00:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Week)] = Template("datetime(date({0}, '-' || ((CAST(strftime('%w', {0}) AS INTEGER) + 6) % 7) || ' days'))");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Day)] = Template("strftime('%Y-%m-%d 00:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Hour)] = Template("strftime('%Y-%m-%d %H:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Minute)] = Template("strftime('%Y-%m-%d %H:%M:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Second)] = Template("strftime('%Y-%m-%d %H:%M:%S', {0})");
        f[SqlFunctionNames.InCsv] = Template("(instr(',' || {1} || ',', ',' || {0} || ',') > 0)");
        // JSON：json_extract 对标量返回 SQL 值、对对象 / 数组返回 JSON 文本，取值与取文本同一个写法
        f[SqlFunctionNames.JsonGet] = Template("json_extract({0},'$.{1}')");
        f[SqlFunctionNames.JsonGetIndex] = Template("json_extract({0},'$[{1}]')");
        f[SqlFunctionNames.JsonText] = Template("json_extract({0},'$.{1}')");
        f[SqlFunctionNames.JsonTextIndex] = Template("json_extract({0},'$[{1}]')");
        f[SqlFunctionNames.JsonLength] = Call("json_array_length");
        f[SqlFunctionNames.JsonContainsKey] = Template("(json_type({0},'$.{1}') IS NOT NULL)");
        f[SqlFunctionNames.DateFormat] = DateFormat("strftime({1}, {0})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%M"), ("ss", "%S"));
        f[SqlFunctionNames.StringJoin] = Call("GROUP_CONCAT");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("GROUP_CONCAT(DISTINCT ").Write(a[0]).Append(",").Write(a[1]).Append(")");
        f[SqlFunctionNames.Now] = Template("datetime(current_timestamp,'localtime')");
        f[SqlFunctionNames.UtcNow] = Template("CURRENT_TIMESTAMP");
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "datetime({0},(({1})/1000)||' seconds')" : "datetime({0},({1})||' " + Lower(unit) + "s')",
            part => part == DatePart.IsoDayOfWeek
                ? "(CASE WHEN CAST(strftime('%w',{0}) AS INTEGER) = 0 THEN 7 ELSE CAST(strftime('%w',{0}) AS INTEGER) END)"
                : "CAST(strftime('" + part switch
            {
                DatePart.Year => "%Y", DatePart.Month => "%m", DatePart.Day => "%d", DatePart.Hour => "%H", DatePart.Minute => "%M",
                DatePart.Second => "%S", DatePart.DayOfYear => "%j", _ => "%w",
            } + "',{0}) AS INTEGER)",
            unit => "(strftime('%s',{1})-strftime('%s',{0}))" + unit switch
            {
                DateUnit.Day => "/86400", DateUnit.Hour => "/3600", DateUnit.Minute => "/60", DateUnit.Second => "", _ => "*1000",
            });
    }
}
