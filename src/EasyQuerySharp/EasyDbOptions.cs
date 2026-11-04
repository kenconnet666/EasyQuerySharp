using System.Data.Common;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Dialects.MySql;
using EasyQuerySharp.Dialects.PostgreSql;
using EasyQuerySharp.Dialects.Sqlite;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp;

/// <summary><see cref="EasyDb"/> 的配置。方言必填；数据源为 null 时只能生成 SQL（<c>ToSql()</c>），不能执行。</summary>
public sealed class EasyDbOptions
{
    /// <summary>方言。</summary>
    public SqlDialect Dialect { get; set; } = null!;

    /// <summary>ADO.NET 数据源（MySqlDataSource、NpgsqlDataSource 等）。</summary>
    public DbDataSource? DataSource { get; set; }

    /// <summary>由类名、属性名推出表名、列名的约定，默认驼峰转下划线。</summary>
    public NameConversion NameConversion { get; set; } = NameConversion.Underlined;

    /// <summary>执行监听器。</summary>
    public List<IExecutionListener> Listeners { get; } = [];

    /// <summary>拦截器。</summary>
    public InterceptorRegistry Interceptors { get; } = new();

    /// <summary>使用 MySQL 方言（8.0.18 起；PolarDB、MariaDB 兼容）。</summary>
    public EasyDbOptions UseMySql(DbDataSource? dataSource = null) => Use(MySqlDialect.Instance, dataSource);

    /// <summary>使用 PostgreSQL 方言。</summary>
    public EasyDbOptions UsePostgreSql(DbDataSource? dataSource = null) => Use(PostgreSqlDialect.Instance, dataSource);

    /// <summary>使用 SQLite 方言。</summary>
    public EasyDbOptions UseSqlite(DbDataSource? dataSource = null) => Use(SqliteDialect.Instance, dataSource);

    /// <summary>使用任意方言。</summary>
    public EasyDbOptions Use(SqlDialect dialect, DbDataSource? dataSource = null)
    {
        Dialect = dialect;
        DataSource = dataSource;
        return this;
    }

    /// <summary>添加执行监听器。</summary>
    public EasyDbOptions AddListener(IExecutionListener listener)
    {
        Listeners.Add(listener);
        return this;
    }

    /// <summary>添加拦截器。</summary>
    public EasyDbOptions AddInterceptor(IInterceptor interceptor)
    {
        Interceptors.Add(interceptor);
        return this;
    }
}
