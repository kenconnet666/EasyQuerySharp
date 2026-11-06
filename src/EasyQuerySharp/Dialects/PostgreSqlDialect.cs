using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// PostgreSQL 方言（17 实测）。驱动用 Npgsql：<c>o.UsePostgreSql(NpgsqlDataSource.Create(cs))</c>。
/// PG 内核的 Kingbase、GaussDB / openGauss 各有子类（<see cref="KingbaseDialect"/>、<see cref="GaussDbDialect"/>）。
/// </summary>
public class PostgreSqlDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static PostgreSqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override NullsOrder DefaultNullsOrder(bool descending) => descending ? NullsOrder.First : NullsOrder.Last;

    /// <inheritdoc />
    public override string Name => "pgsql";

    /// <inheritdoc />
    public override bool SupportsWriteReturning => true;

    /// <summary>PostgreSQL 的 SET 列不能带表别名（<c>SET t."a"</c> 会被当成复合列）。</summary>
    public override bool QualifySetColumns => false;

    /// <summary>类型名与 Java 的 PgSQLMigrationEntityParser 一致：INT4 / INT8 / INT2、BOOL、FLOAT4 / FLOAT8、numeric(16,2)；二进制列用 BYTEA。</summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(int) || type.IsEnum => "INT4",
        _ when type == typeof(long) => "INT8",
        _ when type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) => "INT2",
        _ when type == typeof(bool) => "BOOL",
        _ when type == typeof(float) => "FLOAT4",
        _ when type == typeof(double) => "FLOAT8",
        _ when type == typeof(decimal) => "numeric(16,2)",
        _ when type == typeof(byte[]) => "BYTEA",
        _ => base.ClrColumnType(type, column),
    };

    // 下面的建表与同步语句与 Java 的 PgSQLDatabaseMigrationProvider 逐字一致：多行建表，注释接在语句后面，索引名、外键名不加引号，以分号结尾

    /// <summary>
    /// 多行建表（见 <see cref="SqlDialect.MultiLineCreateTable"/>），以 <c>);</c> 结尾；有注释时空一行，接着表注释、各列注释的 <c>COMMENT ON …;</c>。
    /// </summary>
    public override string CreateTableSql(Metadata.EntityMetadata entity)
    {
        var table = QualifiedTable(entity);
        var comments = string.Concat(TableColumns(entity).Where(c => c.Comment is not null)
            .Select(c => $"\nCOMMENT ON COLUMN {table}.{QuoteIdentifier(c.Name)} IS {SqlString(c.Comment!)};")
            .Prepend(entity.Comment is null ? "" : $"\nCOMMENT ON TABLE {table} IS {SqlString(entity.Comment)};"));
        var sql = MultiLineCreateTable(entity, c => c.IsGenerated ? GeneratedColumnTail(entity, c) : "", ");");
        return comments.Length > 0 ? sql + "\n" + comments : sql;
    }

    /// <summary>多行建表里自增列跟在 NULL / NOT NULL 后面的写法；DuckDB 改成取序列的默认值。</summary>
    private protected virtual string GeneratedColumnTail(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) => " " + IdentityClause;

    /// <inheritdoc />
    internal override string? TableCommentSql(Metadata.EntityMetadata entity) => null;

    /// <inheritdoc />
    internal override string? ColumnCommentSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) => null;

    // 加列、改列名之后的列注释：另起一行的 COMMENT ON COLUMN
    private string ColumnCommentTail(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) => column.Comment is null ? ""
        : $"\n COMMENT ON COLUMN {QualifiedTable(entity)}.{QuoteIdentifier(column.Name)} IS {SqlString(column.Comment)};";

    /// <summary><c>CREATE [UNIQUE ]INDEX 名 ON "表" ("列" ASC,…);</c>，有注释时另起一行 <c>COMMENT ON INDEX "名" IS '…';</c></summary>
    internal override string CreateIndexSql(Metadata.EntityMetadata entity, Metadata.IndexMetadata index) =>
        $"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {index.Name} ON {QualifiedTable(entity)} ({IndexColumns(index, ",")});"
        + (index.Comment is null ? "" : $"\nCOMMENT ON INDEX {QuoteIdentifier(index.Name)} IS {SqlString(index.Comment)};");

    /// <summary><c>ALTER TABLE "表" ADD CONSTRAINT 名 FOREIGN KEY ("列") REFERENCES "目标" ("列")[ 动作 ];</c></summary>
    internal override string AddForeignKeySql(Metadata.ForeignKeyMetadata foreignKey) =>
        $"ALTER TABLE {QualifiedTable(foreignKey.Entity)} ADD CONSTRAINT {foreignKey.Name} FOREIGN KEY ({QuotedNames(foreignKey.Columns)}) "
        + $"REFERENCES {QuoteIdentifier(foreignKey.Target.TableName)} ({QuotedNames(foreignKey.TargetColumns)}){(foreignKey.Action is null ? "" : " " + foreignKey.Action + " ")};";

    /// <summary><c>ALTER TABLE "旧表" RENAME TO "新表";</c>（新表名不能带 schema）。</summary>
    internal override string RenameTableSql(Metadata.EntityMetadata entity, string oldName) =>
        $"ALTER TABLE {QualifiedName(entity.Schema, oldName)} RENAME TO {QuoteIdentifier(entity.TableName)};";

    /// <summary><c>ALTER TABLE "表" RENAME COLUMN "旧列" TO "新列";</c>，有注释时接着写列注释。</summary>
    internal override string RenameColumnSql(Metadata.EntityMetadata entity, string oldName, Metadata.ColumnMetadata column) =>
        base.RenameColumnSql(entity, oldName, column) + ";" + ColumnCommentTail(entity, column);

    /// <summary><c>ALTER TABLE "表" ADD "列" 类型 NULL;</c>，有注释时接着写列注释。</summary>
    internal override string AddColumnSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        $"ALTER TABLE {QualifiedTable(entity)} ADD {QuoteIdentifier(column.Name)} {ColumnType(column)}{(IsNullable(column) ? " NULL" : " NOT NULL")}{DefaultClause(column)};"
        + ColumnCommentTail(entity, column);

    /// <summary>读库结构时判断自增列（pg_attribute 别名 a、pg_attrdef 别名 d）：IDENTITY 列或默认值取序列。</summary>
    private protected virtual string IntrospectedIdentity => "a.attidentity <> '' OR coalesce(pg_get_expr(d.adbin, d.adrelid), '') LIKE 'nextval(%'";

    /// <summary>读系统目录（<c>current_schema()</c> 里的普通表与分区表）。</summary>
    internal override CatalogQueries? CatalogQueries => PgCatalogQueries(IntrospectedIdentity);

    private protected static CatalogQueries PgCatalogQueries(string identity)
    {
        const string tables = "SELECT c.oid, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = current_schema() AND c.relkind IN ('r', 'p')";
        return new CatalogQueries(
            $"SELECT t.relname AS table_name, obj_description(t.oid, 'pg_class') AS comment FROM ({tables}) t ORDER BY t.relname",
            "SELECT t.relname AS table_name, a.attname AS column_name, format_type(a.atttypid, a.atttypmod) AS column_type, NOT a.attnotnull AS is_nullable, "
            + $"pg_get_expr(d.adbin, d.adrelid) AS column_default, col_description(t.oid, a.attnum) AS comment, ({identity}) AS is_generated, "
            + "EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = t.oid AND i.indisprimary AND a.attnum = ANY (i.indkey)) AS primary_key "
            + $"FROM ({tables}) t JOIN pg_attribute a ON a.attrelid = t.oid LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum "
            + "WHERE a.attnum > 0 AND NOT a.attisdropped ORDER BY t.relname, a.attnum",
            "SELECT t.relname AS table_name, ic.relname AS index_name, i.indisunique AS is_unique, "
            + "coalesce(a.attname, pg_get_indexdef(i.indexrelid, k.ord::int, true)) AS column_name "
            + $"FROM ({tables}) t JOIN pg_index i ON i.indrelid = t.oid AND NOT i.indisprimary JOIN pg_class ic ON ic.oid = i.indexrelid "
            + "CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY k(attnum, ord) LEFT JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum "
            + "ORDER BY t.relname, ic.relname, k.ord",
            "SELECT t.relname AS table_name, c.conname AS constraint_name, a.attname AS column_name, r.relname AS referenced_table, ra.attname AS referenced_column "
            + $"FROM ({tables}) t JOIN pg_constraint c ON c.conrelid = t.oid AND c.contype = 'f' JOIN pg_class r ON r.oid = c.confrelid "
            + "CROSS JOIN generate_series(1, array_length(c.conkey, 1)) k(ord) JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = c.conkey[k.ord] "
            + "JOIN pg_attribute ra ON ra.attrelid = r.oid AND ra.attnum = c.confkey[k.ord] ORDER BY t.relname, c.conname, k.ord");
    }

    /// <summary>
    /// <c>UPDATE "a" t SET "c" = t1."d" FROM "b" t1 , "e" t2 WHERE 各 ON 条件 AND 原条件</c>（与 Java 逐字一致）：
    /// 连接的表写在 FROM 里，ON 条件按顺序并入 WHERE。
    /// </summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").WriteTable(update.Table).Append(" SET ").WriteSets(update.Sets).Append(" FROM ");
        WriteCommaJoinedTables(renderer, update.Joins, update.Where);
    }

    /// <summary><c>DELETE FROM "a" t USING "b" t1 WHERE 各 ON 条件 AND 原条件</c>（与 Java 逐字一致）。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTable(statement.Table).Append(" USING ");
        WriteCommaJoinedTables(renderer, statement.Joins, statement.Where);
    }

    /// <summary>
    /// 模式在 SQL 里拼（<c>Sql.LikeContains</c> 等）时值先转成 TEXT：<c>LIKE CONCAT('%',(?)::TEXT,'%')</c>；
    /// 值里含 % 或 _ 时同样写成 CONCAT，参数里的 \ % _ 先转义并加 <c>ESCAPE '\'</c>，值里的通配符按字面匹配（与 Java 版一致）。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        var wildcard = predicate.HasWildcardValue;
        var concat = predicate is { Pattern: FunctionExpr { Name: SqlFunctionNames.Concat }, Value: not null };
        if (!wildcard && !concat)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        var value = wildcard && predicate.Value is ParameterExpr { Value: string text } ? new ParameterExpr(EscapeLike(text), typeof(string)) : predicate.Value!;
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE CONCAT(" : " LIKE CONCAT(");
        if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%',");
        renderer.Append("(").Write(value).Append(")::TEXT");
        if (predicate.Mode != LikeMode.EndsWith) renderer.Append(",'%'");
        renderer.Append(wildcard ? ") ESCAPE '\\'" : ")");
    }

    /// <summary>PostgreSQL 的 <c>(x)::类型</c> 转换（对应 Java 的 PgSQLCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "(({0})::varchar not in ('0','false','f','no'))",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) || targetType == typeof(short) => "({0})::int2",
        _ when targetType == typeof(char) => "substr(({0})::char, 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "({0})::time",
        _ when targetType == typeof(DateOnly) => "({0})::date",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "({0})::timestamp",
        _ when targetType == typeof(decimal) => "({0})::numeric",
        _ when targetType == typeof(double) || targetType == typeof(float) => "({0})::float8",
        _ when targetType == typeof(int) || targetType == typeof(ushort) => "({0})::int4",
        _ when targetType == typeof(long) || targetType == typeof(uint) || targetType == typeof(ulong) => "({0})::int8",
        _ when targetType == typeof(Guid) => "({0})::uuid",
        _ when targetType == typeof(string) => "({0})::text",
        _ => base.CastTemplate(targetType),
    };

    /// <summary>json / jsonb 列的参数写成 <c>(?)::jsonb</c>：驱动把字符串当 text 发送，不转换时写不进 jsonb 列。</summary>
    public override (string Before, string After)? ParameterWrap(string? columnDbType) =>
        columnDbType?.Trim().ToLowerInvariant() is "json" or "jsonb" ? ("(", ")::" + columnDbType.Trim().ToLowerInvariant()) : null;

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG((").Write(a[0]).Append(")::TEXT, ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("STRING_AGG(DISTINCT (").Write(a[0]).Append(")::TEXT, ").Write(a[1]).Append(")");
        // 与 Java 的 PgSQLDateTimeFormatSQLFunction 一致：先转成 TIMESTAMP 再格式化
        f[SqlFunctionNames.NullSafeEquals] = Template("({0} IS NOT DISTINCT FROM {1})");
        f[SqlFunctionNames.MaxColumns] = Call("GREATEST");
        f[SqlFunctionNames.MinColumns] = Call("LEAST");
        f[SqlFunctionNames.Power] = Call("POW");
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        // PostgreSQL 没有 INSTR；C# 的 IndexOf 是 0 起点，翻译时再减 1（与 Java 的 PgSQLIndexOfSQLFunction 一致）
        f[SqlFunctionNames.IndexOf] = Call("STRPOS");
        // PG 只有 log(numeric, numeric)，没有双精度的两参数版本
        f[SqlFunctionNames.LogBase] = Template("LOG(({1})::numeric,({0})::numeric)");
        // JSON：列须是 json / jsonb（与 Java 的 PgSQLJSON*SQLFunction 一致）
        f[SqlFunctionNames.JsonGet] = Template("({0} -> '{1}')");
        f[SqlFunctionNames.JsonGetIndex] = Template("({0} -> {1})");
        f[SqlFunctionNames.JsonText] = Template("({0} ->> '{1}')");
        f[SqlFunctionNames.JsonTextIndex] = Template("({0} ->> {1})");
        f[SqlFunctionNames.JsonLength] = Call("JSONB_ARRAY_LENGTH");
        f[SqlFunctionNames.JsonContainsKey] = Template("JSONB_EXISTS({0}, '{1}')");
        foreach (var unit in Enum.GetValues<DateTruncUnit>())
            f[SqlFunctionNames.DateTrunc(unit)] = Template("DATE_TRUNC('" + unit.ToString().ToLowerInvariant() + "', {0})");
        f[SqlFunctionNames.DateFormat] = DateFormat("TO_CHAR(({0})::TIMESTAMP,{1})", ("yyyy", "YYYY"), ("MM", "MM"), ("dd", "DD"), ("HH", "HH24"), ("mm", "MI"), ("ss", "SS"));
    }

}
