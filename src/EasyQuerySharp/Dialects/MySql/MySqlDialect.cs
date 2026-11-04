using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects.MySql;

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

    /// <summary>忽略冲突用 <c>INSERT IGNORE</c>。</summary>
    public override string InsertKeyword(InsertStatement insert) => insert.Conflict is { Ignore: true } ? "INSERT IGNORE INTO " : "INSERT INTO ";

    /// <summary><c>ON DUPLICATE KEY UPDATE `c` = VALUES(`c`), …</c>（与 Java 的 MySQL 方言逐字一致）；没有要更新的列时不写。</summary>
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

    /// <inheritdoc />
    public override void WriteInsertConflict(Rendering.SqlRenderer renderer, InsertConflict conflict)
    {
        if (conflict.Ignore || conflict.UpdateColumns!.Count == 0) return;
        renderer.Append(" ON DUPLICATE KEY UPDATE ").Append(string.Join(", ", conflict.UpdateColumns.Select(c =>
        {
            var name = QuoteIdentifier(c.Name);
            return $"{name} = VALUES({name})";
        })));
    }

    /// <summary>与 Java 版逐字一致的建表格式：每列 <c>`列` 类型 NULL [ COMMENT '…'],</c> 一行，主键单独一行，<c>Engine=InnoDB</c>。</summary>
    public override string CreateTableSql(Metadata.EntityMetadata entity)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS ").Append(QualifiedTable(entity)).Append(" ( \n");
        for (var i = 0; i < entity.Columns.Count; i++)
        {
            var c = entity.Columns[i];
            sb.Append(QuoteIdentifier(c.Name)).Append(' ').Append(ColumnType(c)).Append(IsNullable(c) ? " NULL " : " NOT NULL ");
            if (c.IsGenerated) sb.Append("AUTO_INCREMENT ");
            if (c.Comment is not null) sb.Append(" COMMENT '").Append(c.Comment.Replace("'", "''")).Append('\'');
            sb.Append(i == entity.Columns.Count - 1 ? ", \n" : ",\n");
        }
        if (entity.KeyColumns.Count > 0)
            sb.Append(" PRIMARY KEY (").Append(string.Join(",", entity.KeyColumns.Select(k => QuoteIdentifier(k.Name)))).Append(")\n");
        sb.Append(") Engine=InnoDB");
        if (entity.Comment is not null) sb.Append(" COMMENT='").Append(entity.Comment.Replace("'", "''")).Append('\'');
        return sb.Append(';').ToString();
    }

    /// <inheritdoc />
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(int) || type.IsEnum => "INT(11)",
        _ when type == typeof(long) => "BIGINT(20)",
        _ when type == typeof(short) => "SMALLINT(6)",
        _ when type == typeof(byte) => "TINYINT(4)",
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
        f[SqlFunctionNames.NullOrDefault] = Call("IFNULL");
        f[SqlFunctionNames.CompareTo] = Call("STRCMP");
        f[SqlFunctionNames.IndexOf] = Reordered("LOCATE", 1, 0);
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        f[SqlFunctionNames.Truncate] = Call("TRUNCATE");
        // MySQL 的 LOG(x) 是自然对数（Java 版也这样写）
        f[SqlFunctionNames.Ln] = Call("LOG");
        f[SqlFunctionNames.DateFormat] = DateFormat("DATE_FORMAT({0},{1})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%i"), ("ss", "%s"));
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
