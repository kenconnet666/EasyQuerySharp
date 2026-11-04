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
        f[SqlFunctionNames.DateFormat] = DateFormat("DATE_FORMAT({0},{1})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%i"), ("ss", "%s"));
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("GROUP_CONCAT(").Write(a[0]).Append(" SEPARATOR ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("GROUP_CONCAT(DISTINCT ").Write(a[0]).Append(" SEPARATOR ").Write(a[1]).Append(")");
    }
}
