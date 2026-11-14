using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// MySQL 方言（8.0.18 起，兼容 PolarDB MySQL 版、MariaDB 的大部分写法）。标识符用反引号。
/// 驱动建议用 MySqlConnector：<c>new EasyDb(o => o.UseMySql(new MySqlDataSource(cs)))</c>。
/// </summary>
public class MySqlDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static MySqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "mysql";

    /// <inheritdoc />
    public override string QuoteIdentifier(string identifier) => $"`{identifier}`";

    /// <inheritdoc />
    public override string? LastInsertIdQuery => "SELECT LAST_INSERT_ID()";

    /// <summary>OFFSET 前必须有 LIMIT：只跳过行数时写 MySQL 文档给出的最大值 <c>LIMIT 18446744073709551615 OFFSET m</c>。</summary>
    public override string OffsetOnlyLimit => "18446744073709551615";

    /// <inheritdoc />
    public override void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join)
    {
        renderer.Append(join.Distinct ? "GROUP_CONCAT(DISTINCT " : "GROUP_CONCAT(").Write(join.Value).Append(" ");
        WriteOrderBy(renderer, join.Orders);
        renderer.Append(" SEPARATOR ").Write(join.Separator).Append(")");
    }

    /// <summary><c>UPDATE `a` t INNER JOIN `b` t1 ON … SET t.`c` = t1.`d` WHERE …</c>（与 Java 逐字一致）。</summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").WriteTable(update.Table);
        foreach (var join in update.Joins) renderer.WriteJoin(join);
        renderer.Append(" SET ").WriteSets(update.Sets).WriteWhere(update.Where);
    }

    /// <summary><c>DELETE t FROM `a` t INNER JOIN `b` t1 ON … WHERE …</c>（与 Java 逐字一致）。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE ").Append(statement.Table.Alias!).Append(" FROM ").WriteTable(statement.Table);
        foreach (var join in statement.Joins) renderer.WriteJoin(join);
        renderer.WriteWhere(statement.Where);
    }

    /// <summary>MySQL 只有 <c>GROUP BY a,b WITH ROLLUP</c> 一种写法，没有 CUBE（与 Java 一致）。</summary>
    public override void WriteGroupBy(SqlRenderer renderer, IReadOnlyList<SqlExpr> keys, GroupByMode mode)
    {
        if (mode == GroupByMode.Cube) throw new NotSupportedException("MySQL 不支持 GROUP BY CUBE");
        renderer.WriteList(keys, ",");
        if (mode == GroupByMode.Rollup) renderer.Append(" WITH ROLLUP");
    }

    /// <summary>
    /// 忽略冲突不写 <c>INSERT IGNORE</c>，改写成 <c>ON DUPLICATE KEY UPDATE 主键 = VALUES(主键)</c>（对应 Java 的 mysqlInsertIgnoreToUpdateKey）：
    /// INSERT IGNORE 把类型不符、超长这类错误也降成警告，这样只忽略键冲突。实体要有非自增的主键。
    /// 用法：<c>o.Use(new MySqlDialect { IgnoreConflictAsKeyUpdate = true }, dataSource)</c>。
    /// </summary>
    public bool IgnoreConflictAsKeyUpdate { get; init; }

    /// <summary>忽略冲突用 <c>INSERT IGNORE</c>（<see cref="IgnoreConflictAsKeyUpdate"/> 时除外）。</summary>
    public override string InsertKeyword(InsertStatement insert) =>
        insert.Conflict is { Ignore: true } && !IgnoreConflictAsKeyUpdate ? "INSERT IGNORE INTO " : "INSERT INTO ";

    /// <summary>
    /// 值里含 % 或 _ 时转义后写成 <c>x LIKE CONCAT('%',?,'%') ESCAPE '\\'</c>（与 Java 的 MySQLLikeFunctionTemplateGenerator 一致），
    /// 否则是普通的 <c>x LIKE ?</c>。
    /// </summary>
    public override void WriteLike(Rendering.SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        var value = new ParameterExpr(EscapeLike((string)((ParameterExpr)predicate.Value).Value!), typeof(string));
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE CONCAT(" : " LIKE CONCAT(");
        if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%',");
        renderer.Write(value);
        if (predicate.Mode != LikeMode.EndsWith) renderer.Append(",'%'");
        renderer.Append(") ESCAPE '\\\\'");
    }

    /// <summary>ON DUPLICATE KEY 在任何唯一键上冲突都会更新，更新到的可能是主键以外的行。</summary>
    internal override bool UpsertMatchesOnlyConflictColumns => false;

    /// <summary><c>ON DUPLICATE KEY UPDATE `c` = VALUES(`c`), …</c>（与 Java 的 MySQL 方言逐字一致）；没有要更新的列时不写。</summary>
    public override void WriteInsertConflict(Rendering.SqlRenderer renderer, InsertStatement insert)
    {
        var conflict = insert.Conflict!;
        var columns = !conflict.Ignore ? conflict.UpdateColumns!.Select(c => c.Name).ToList()
            : IgnoreConflictAsKeyUpdate ? KeyUpdateColumns(insert) : [];
        if (columns.Count == 0) return;
        renderer.Append(" ON DUPLICATE KEY UPDATE ").Append(string.Join(", ", columns.Select(c =>
        {
            var name = QuoteIdentifier(c);
            return $"{name} = VALUES({name})";
        })));
    }

    private static List<string> KeyUpdateColumns(InsertStatement insert)
    {
        var keys = insert.Table.Entity.KeyColumns.Where(k => !k.IsGenerated).Select(k => k.Name).ToList();
        return keys.Count > 0 ? keys
            : throw new InvalidOperationException($"{insert.Table.Entity.Type.Name} 没有非自增的主键：MySQL 的忽略冲突改写不成 ON DUPLICATE KEY UPDATE 主键 = VALUES(主键)");
    }

    /// <summary>与 Java 版逐字一致的建表格式：每列 <c>`列` 类型 NULL [ AUTO_INCREMENT][ COMMENT '…'],</c> 一行，主键单独一行，<c>Engine=InnoDB</c>，注释写在语句里。</summary>
    public override string CreateTableSql(Metadata.EntityMetadata entity) =>
        MultiLineCreateTable(entity, c => (c.IsGenerated ? " AUTO_INCREMENT" : "") + InlineComment(c.Comment),
            ") Engine=InnoDB" + (entity.Comment is null ? "" : " COMMENT=" + SqlString(entity.Comment)) + ";");

    private string InlineComment(string? comment) => comment is null ? "" : " COMMENT " + SqlString(comment);

    // 下面的同步语句与 Java 的 DefaultDatabaseMigrationProvider 逐字一致：注释写在语句里，索引名、外键名不加引号，以分号结尾

    /// <inheritdoc />
    internal override string? TableCommentSql(Metadata.EntityMetadata entity) => null;

    /// <inheritdoc />
    internal override string? ColumnCommentSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) => null;

    /// <summary><c>CREATE [UNIQUE ]INDEX 名 ON `表` (`列` ASC,…)[ COMMENT '…'];</c></summary>
    internal override string CreateIndexSql(Metadata.EntityMetadata entity, Metadata.IndexMetadata index) =>
        $"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {index.Name} ON {QualifiedTable(entity)} ({IndexColumns(index, ",")}){InlineComment(index.Comment)};";

    /// <summary><c>ALTER TABLE `表` ADD CONSTRAINT 名 FOREIGN KEY (`列`) REFERENCES `目标` (`列`)[ 动作 ];</c></summary>
    internal override string AddForeignKeySql(Metadata.ForeignKeyMetadata foreignKey) =>
        $"ALTER TABLE {QualifiedTable(foreignKey.Entity)} ADD CONSTRAINT {foreignKey.Name} FOREIGN KEY ({QuotedNames(foreignKey.Columns)}) "
        + $"REFERENCES {QuoteIdentifier(foreignKey.Target.TableName)} ({QuotedNames(foreignKey.TargetColumns)}){(foreignKey.Action is null ? "" : " " + foreignKey.Action + " ")};";

    /// <summary><c>ALTER TABLE `旧表` RENAME TO `新表`;</c></summary>
    internal override string RenameTableSql(Metadata.EntityMetadata entity, string oldName) => base.RenameTableSql(entity, oldName) + ";";

    /// <summary>
    /// <c>ALTER TABLE `表` CHANGE `旧列` `新列` 类型 NULL[ AUTO_INCREMENT | DEFAULT …][ COMMENT '…'];</c>（CHANGE 连同类型一起写，5.7 也能用）。
    /// CHANGE 是整列重定义，不写自增与默认值就会丢掉（Java 没写）。
    /// </summary>
    internal override string RenameColumnSql(Metadata.EntityMetadata entity, string oldName, Metadata.ColumnMetadata column) =>
        $"ALTER TABLE {QualifiedTable(entity)} CHANGE {QuoteIdentifier(oldName)} {QuoteIdentifier(column.Name)} {ColumnType(column)}{(IsNullable(column) ? " NULL" : " NOT NULL")}"
        + $"{(column.IsGenerated ? " AUTO_INCREMENT" : DefaultClause(column))}{InlineComment(column.Comment)};";

    /// <summary><c>ALTER TABLE `表` ADD `列` 类型 NULL[ DEFAULT …][ COMMENT '…'];</c></summary>
    internal override string AddColumnSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        $"ALTER TABLE {QualifiedTable(entity)} ADD {QuoteIdentifier(column.Name)} {ColumnType(column)}{(IsNullable(column) ? " NULL" : " NOT NULL")}{DefaultClause(column)}{InlineComment(column.Comment)};";

    /// <summary>读 information_schema（当前库）。</summary>
    internal override CatalogQueries CatalogQueries { get; } = new(
        "SELECT TABLE_NAME AS table_name, TABLE_COMMENT AS comment FROM information_schema.TABLES "
        + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME",
        "SELECT TABLE_NAME AS table_name, COLUMN_NAME AS column_name, COLUMN_TYPE AS column_type, IS_NULLABLE AS is_nullable, "
        + "COLUMN_DEFAULT AS column_default, COLUMN_COMMENT AS comment, EXTRA LIKE '%auto_increment%' AS is_generated, COLUMN_KEY = 'PRI' AS primary_key "
        + "FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME, ORDINAL_POSITION",
        "SELECT TABLE_NAME AS table_name, INDEX_NAME AS index_name, NON_UNIQUE = 0 AS is_unique, COLUMN_NAME AS column_name "
        + "FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME <> 'PRIMARY' ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX",
        "SELECT TABLE_NAME AS table_name, CONSTRAINT_NAME AS constraint_name, COLUMN_NAME AS column_name, "
        + "REFERENCED_TABLE_NAME AS referenced_table, REFERENCED_COLUMN_NAME AS referenced_column "
        + "FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL "
        + "ORDER BY TABLE_NAME, CONSTRAINT_NAME, ORDINAL_POSITION");

    /// <inheritdoc />
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(int) || type.IsEnum => "INT(11)",
        _ when type == typeof(long) => "BIGINT(20)",
        _ when type == typeof(short) => "SMALLINT(6)",
        _ when type == typeof(byte) || type == typeof(sbyte) => "TINYINT(4)",
        _ when type == typeof(bool) => "TINYINT(1)",
        _ when type == typeof(double) => "DOUBLE",
        _ when type == typeof(float) => "FLOAT",
        _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => "DATETIME(3)",
        _ when type == typeof(byte[]) => "LONGBLOB",
        _ => base.ClrColumnType(type, column),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // DIV 是整数相除、向零截断（/ 得到小数）
        f[SqlFunctionNames.IntegerDivide] = (r, a) => r.Append("(").Write(a[0]).Append(" DIV ").Write(a[1]).Append(")");
        f[SqlFunctionNames.NullOrDefault] = Call("IFNULL");
        // MySQL 的 LPAD / RPAD 必须给填充字符（Java 不给时写两个参数，执行不了）
        f[SqlFunctionNames.PadLeft] = Pad("LPAD", "' '");
        f[SqlFunctionNames.PadRight] = Pad("RPAD", "' '");
        f[SqlFunctionNames.MaxColumns] = Call("GREATEST");
        f[SqlFunctionNames.MinColumns] = Call("LEAST");
        f[SqlFunctionNames.NullSafeEquals] = Template("({0} <=> {1})");
        f[SqlFunctionNames.InCsv] = Template("FIND_IN_SET({0},{1})");
        // JSON：键写进路径字面量（与 Java 的 JSONObjectFieldSQLFunction 等一致，数组下标的逗号后没有空格）
        f[SqlFunctionNames.JsonGet] = Template("JSON_EXTRACT({0}, '$.{1}')");
        f[SqlFunctionNames.JsonGetIndex] = Template("JSON_EXTRACT({0},'$[{1}]')");
        f[SqlFunctionNames.JsonText] = Template("JSON_UNQUOTE(JSON_EXTRACT({0}, '$.{1}'))");
        f[SqlFunctionNames.JsonTextIndex] = Template("JSON_UNQUOTE(JSON_EXTRACT({0},'$[{1}]'))");
        f[SqlFunctionNames.JsonLength] = Call("JSON_LENGTH");
        f[SqlFunctionNames.JsonContainsKey] = Template("JSON_CONTAINS_PATH({0}, 'one', '$.{1}')");
        // MySQL 没有 DATE_TRUNC：DATE_FORMAT 把更小的部分清零再转回 DATETIME；季度、周再减去月 / 天的偏移（WEEKDAY 周一为 0），与 Java 一致
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Year)] = Template("CAST(DATE_FORMAT({0},'%Y-01-01 00:00:00') AS DATETIME)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Quarter)] = Template("DATE_SUB(CAST(DATE_FORMAT({0},'%Y-%m-01 00:00:00') AS DATETIME), INTERVAL ((MONTH({0}) - 1) % 3) MONTH)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Month)] = Template("CAST(DATE_FORMAT({0},'%Y-%m-01 00:00:00') AS DATETIME)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Week)] = Template("DATE_SUB(CAST(DATE_FORMAT({0},'%Y-%m-%d 00:00:00') AS DATETIME), INTERVAL WEEKDAY({0}) DAY)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Day)] = Template("CAST(DATE_FORMAT({0},'%Y-%m-%d 00:00:00') AS DATETIME)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Hour)] = Template("CAST(DATE_FORMAT({0},'%Y-%m-%d %H:00:00') AS DATETIME)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Minute)] = Template("CAST(DATE_FORMAT({0},'%Y-%m-%d %H:%i:00') AS DATETIME)");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Second)] = Template("CAST(DATE_FORMAT({0},'%Y-%m-%d %H:%i:%s') AS DATETIME)");
        f[SqlFunctionNames.CompareTo] = Call("STRCMP");
        f[SqlFunctionNames.IndexOf] = Reordered("LOCATE", 1, 0);
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        f[SqlFunctionNames.Truncate] = WithDigits("TRUNCATE");
        f[SqlFunctionNames.Power] = Call("POW");
        // MySQL 的 LOG(x) 是自然对数（Java 版也这样写）
        f[SqlFunctionNames.Ln] = Call("LOG");
        f[SqlFunctionNames.DateFormat] = DateFormat("DATE_FORMAT({0},{1})", ("yyyy", "%Y"), ("yy", "%y"), ("MM", "%m"), ("M", "%c"), ("dd", "%d"), ("d", "%e"),
            ("HH", "%H"), ("H", "%k"), ("hh", "%h"), ("h", "%l"), ("mm", "%i"), ("ss", "%s"), ("ffffff", "%f"), ("tt", "%p"));
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("GROUP_CONCAT(").Write(a[0]).Append(" SEPARATOR ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("GROUP_CONCAT(DISTINCT ").Write(a[0]).Append(" SEPARATOR ").Write(a[1]).Append(")");
        f[SqlFunctionNames.Now] = Template("NOW()");
        f[SqlFunctionNames.Random] = Template("RAND()");
        f[SqlFunctionNames.UtcNow] = Template("UTC_TIMESTAMP()");
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "date_add({0}, interval ({1})*1000 microsecond)" : "date_add({0}, interval ({1}) " + Lower(unit) + ")",
            part => part switch
            {
                DatePart.DayOfYear => "DAYOFYEAR({0})",
                DatePart.DayOfWeek => "(DAYOFWEEK({0})-1)",
                DatePart.IsoDayOfWeek => "(WEEKDAY({0})+1)",
                DatePart.Day => "DAYOFMONTH({0})",
                _ => part.ToString().ToUpperInvariant() + "({0})",
            },
            unit => unit == DateUnit.Millisecond ? "(timestampdiff(MICROSECOND, {0}, {1}) DIV 1000)" : "timestampdiff(" + unit.ToString().ToUpperInvariant() + ", {0}, {1})");
    }
}
