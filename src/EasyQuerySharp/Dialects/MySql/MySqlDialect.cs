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

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.NullOrDefault] = Call("IFNULL");
        f[SqlFunctionNames.CompareTo] = Call("STRCMP");
        f[SqlFunctionNames.IndexOf] = Reordered("LOCATE", 1, 0);
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        f[SqlFunctionNames.Truncate] = Call("TRUNCATE");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("GROUP_CONCAT(").Write(a[0]).Append(" SEPARATOR ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("GROUP_CONCAT(DISTINCT ").Write(a[0]).Append(" SEPARATOR ").Write(a[1]).Append(")");
    }
}
