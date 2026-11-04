using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects.SqlServer;

/// <summary>
/// SQL Server 方言（2012 起的 OFFSET … FETCH 分页；2008 及更早用 <see cref="SqlServer2008Dialect"/>）。标识符用方括号。
/// 驱动用 Microsoft.Data.SqlClient：<c>o.Use(SqlServerDialect.Instance, SqlClientFactory.Instance.CreateDataSource(cs))</c>。
/// </summary>
public class SqlServerDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static SqlServerDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "mssql";

    /// <inheritdoc />
    public override string QuoteIdentifier(string identifier) => $"[{identifier}]";

    /// <inheritdoc />
    public override string? LastInsertIdQuery => "SELECT SCOPE_IDENTITY()";

    /// <summary>OFFSET … ROWS FETCH NEXT … ROWS ONLY；SQL Server 要求有 ORDER BY，没有时补 <c>ORDER BY (SELECT 1)</c>。</summary>
    public override void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.OrderBy.Count == 0) renderer.Append(" ORDER BY (SELECT 1)");
        renderer.Append(" OFFSET ").Append((query.Offset ?? 0).ToString(CultureInfo.InvariantCulture)).Append(" ROWS");
        if (query.Limit is { } limit) renderer.Append(" FETCH NEXT ").Append(limit.ToString(CultureInfo.InvariantCulture)).Append(" ROWS ONLY");
    }

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.NullOrDefault] = Call("ISNULL");
        f[SqlFunctionNames.Length] = Call("LEN");
        f[SqlFunctionNames.Trim] = (r, a) => r.Append("LTRIM(RTRIM(").Write(a[0]).Append("))");
        f[SqlFunctionNames.SubString] = Call("SUBSTRING");
        f[SqlFunctionNames.IndexOf] = Reordered("CHARINDEX", 1, 0);
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        f[SqlFunctionNames.Ln] = Call("LOG");
        // ROUND 的第三个参数非 0 表示截断
        f[SqlFunctionNames.Truncate] = (r, a) =>
        {
            r.Append("ROUND(").Write(a[0]).Append(",");
            if (a.Count > 1) r.Write(a[1]);
            else r.Append("0");
            r.Append(",1)");
        };
    }
}

/// <summary>SQL Server 2008 及更早：没有 OFFSET … FETCH，分页用 ROW_NUMBER() 包一层（Java 的 MsSQLRowNumber 方言）。</summary>
public class SqlServer2008Dialect : SqlServerDialect
{
    /// <summary>共享实例。</summary>
    public static new SqlServer2008Dialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "mssql2008";
}
