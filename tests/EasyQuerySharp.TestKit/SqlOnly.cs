using EasyQuerySharp.Dialects;

namespace EasyQuerySharp.TestKit;

/// <summary>
/// 只生成 SQL、不连库的 EasyDb（对应 Java 测试里的 EmptyDataSource）：按方言短名缓存，
/// 用于 Java 版那些"同一个查询在各方言下的 SQL"的用例。
/// </summary>
public static class SqlOnly
{
    private static readonly Dictionary<string, SqlDialect> Dialects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mysql"] = MySqlDialect.Instance,
        ["pgsql"] = PostgreSqlDialect.Instance,
        ["mssql"] = SqlServerDialect.Instance,
        ["oracle"] = OracleDialect.Instance,
        ["sqlite"] = SqliteDialect.Instance,
        ["duckdb"] = DuckDbDialect.Instance,
        ["dameng"] = DamengDialect.Instance,
        ["hana"] = HanaDialect.Instance,
        ["kingbase"] = KingbaseDialect.Instance,
        ["gauss"] = GaussDbDialect.Instance,
        ["clickhouse"] = ClickHouseDialect.Instance,
        ["db2"] = Db2Dialect.Instance,
        ["tdengine"] = TDengineDialect.Instance,
    };

    private static readonly Dictionary<string, EasyDb> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>按方言短名（mysql / pgsql / mssql / oracle / sqlite / duckdb / dameng / hana / kingbase / gauss / clickhouse / db2 / tdengine）取 EasyDb。</summary>
    public static EasyDb Db(string dialect)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue(dialect, out var db))
                Cache[dialect] = db = new EasyDb(o => o.WithJavaNulls().Use(Dialects[dialect]).AddListener(SqlCapture.Instance));
            return db;
        }
    }

    /// <summary>断言语句的 SQL（<c>?</c> 占位）与 Java 风格的参数文本。</summary>
    public static void AssertStatement(SqlStatement statement, string expectedSql, string? expectedParams = null)
    {
        Assert.Equal(expectedSql, statement.Text);
        if (expectedParams is not null) Assert.Equal(expectedParams, JavaParams.Format(statement));
    }
}
