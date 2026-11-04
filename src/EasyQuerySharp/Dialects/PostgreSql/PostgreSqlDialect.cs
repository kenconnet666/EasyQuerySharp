namespace EasyQuerySharp.Dialects.PostgreSql;

/// <summary>
/// PostgreSQL 方言（17 实测）。openGauss、人大金仓 PG 模式共用。驱动用 Npgsql：<c>o.UsePostgreSql(NpgsqlDataSource.Create(cs))</c>。
/// </summary>
public class PostgreSqlDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static PostgreSqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "pgsql";
}
