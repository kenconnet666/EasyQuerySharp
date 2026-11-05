using System.Data.Common;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp;

/// <summary><see cref="EasyDb"/> 的配置。方言必填；数据源为 null 时只能生成 SQL（<c>ToSql()</c>），不能执行。</summary>
public sealed class EasyDbOptions
{
    /// <summary>一条按类型配置的逻辑删除。</summary>
    internal sealed record LogicDeleteRule(Func<Type, bool> AppliesTo, string Property, Metadata.LogicDeleteAttribute LogicDelete);

    /// <summary>方言。</summary>
    public SqlDialect Dialect { get; set; } = null!;

    /// <summary>ADO.NET 数据源（MySqlDataSource、NpgsqlDataSource 等）。</summary>
    public DbDataSource? DataSource { get; set; }

    /// <summary>由类名、属性名推出表名、列名的约定，默认驼峰转下划线。</summary>
    public NameConversion NameConversion { get; set; } = NameConversion.Underlined;

    /// <summary>
    /// 是否允许生成物理 DELETE（对应 Java 的 deleteThrowError 取反）。默认 true；设为 false 后，
    /// 没有逻辑删除的实体只有在命令上调用 <c>AllowDeleteStatement(true)</c> 才能删除，防止误删。
    /// </summary>
    public bool AllowDeleteStatement { get; set; } = true;

    /// <summary>
    /// 深分页反向排序的偏移阈值（对应 Java 的 reverseOffsetThreshold）：分页的偏移量不小于它、且超过总数一半时，
    /// 把 ORDER BY 整体反过来取 <c>总数 - 偏移 - 本页条数</c> 处的一页，读出后在内存里倒序，减少数据库跳过的行数。
    /// 0（默认）表示不启用；排序里有原生片段或 NULL 位置时不反转。单条查询可用 <c>DisableReversePaging()</c> 关闭。
    /// </summary>
    public long ReverseOffsetThreshold { get; set; }

    /// <summary>
    /// IN 值列表的上限（对应 Java 的 maxInClauseSize）：超过时按上限拆成多个 IN 用 OR 连接；0（默认）表示不拆。
    /// Oracle 单个 IN 最多 1000 个值，需要时设成 1000。
    /// </summary>
    public int MaxInClauseSize { get; set; }

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

    /// <summary>Include 的子数据带了 Take / Skip 时怎样按父对象各取一段（对应 Java 的 includeLimitMode），默认按父对象分区编行号。</summary>
    public IncludeLimitMode IncludeLimitMode { get; set; } = IncludeLimitMode.Partition;

    /// <summary>添加拦截器。</summary>
    public EasyDbOptions AddInterceptor(IInterceptor interceptor)
    {
        Interceptors.Add(interceptor);
        return this;
    }

    /// <summary>按名字注册的自定义逻辑删除策略，供 <c>[LogicDelete(LogicDeleteStrategy.Custom, StrategyName = "名字")]</c> 使用。</summary>
    public Dictionary<string, Metadata.ILogicDeleteStrategy> LogicDeleteStrategies { get; } = new(StringComparer.Ordinal);

    /// <summary>注册自定义逻辑删除策略（对应 Java 的 applyLogicDeleteStrategy）。</summary>
    public EasyDbOptions AddLogicDeleteStrategy(string name, Metadata.ILogicDeleteStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        LogicDeleteStrategies[name] = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    /// <summary>按类型配置的逻辑删除（见 <see cref="ApplyLogicDelete"/>）。</summary>
    internal List<LogicDeleteRule> LogicDeleteRules { get; } = [];

    /// <summary>
    /// 不改实体、用代码给命中的实体配置逻辑删除（对应 Java 的 AbstractConfigurationLogicDeleteStrategy）：
    /// <paramref name="appliesTo"/> 命中、且实体自己没有标注 <see cref="Metadata.LogicDeleteAttribute"/> 时，把 <paramref name="property"/> 当作逻辑删除列。
    /// </summary>
    /// <example><code>options.ApplyLogicDelete(t =&gt; t == typeof(Config), nameof(Config.Deleted), new LogicDeleteAttribute());</code></example>
    public EasyDbOptions ApplyLogicDelete(Func<Type, bool> appliesTo, string property, Metadata.LogicDeleteAttribute logicDelete)
    {
        ArgumentNullException.ThrowIfNull(appliesTo);
        ArgumentException.ThrowIfNullOrEmpty(property);
        LogicDeleteRules.Add(new LogicDeleteRule(appliesTo, property, logicDelete ?? throw new ArgumentNullException(nameof(logicDelete))));
        return this;
    }
}

/// <summary>Include 的子数据带了 Take / Skip 时按父对象取段的方式（对应 Java 的 IncludeLimitModeEnum）。</summary>
public enum IncludeLimitMode
{
    /// <summary>一条查询，按关联键分区编行号再按行号取段（ROW_NUMBER，需要数据库支持窗口函数）。</summary>
    Partition,

    /// <summary>每个父对象一段带 LIMIT 的查询，UNION ALL 合并。</summary>
    UnionAll,
}
