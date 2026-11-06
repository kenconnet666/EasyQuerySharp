using System.Data.Common;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp;

/// <summary>
/// <see cref="EasyDb"/> 的配置。方言必填；数据源为 null 时只能生成 SQL（<c>ToSql()</c>），不能执行。
/// 交给 <see cref="EasyDb"/> 之后就冻结了，再修改会抛出 <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class EasyDbOptions
{
    /// <summary>一条按类型配置的逻辑删除。</summary>
    internal sealed record LogicDeleteRule(Func<Type, bool> AppliesTo, string Property, Metadata.LogicDeleteAttribute LogicDelete);

    private readonly List<IExecutionListener> _listeners = [];
    private readonly List<IWriteListener> _writeListeners = [];
    private readonly Dictionary<string, ILogicDeleteStrategy> _logicDeleteStrategies = new(StringComparer.Ordinal);
    private readonly List<LogicDeleteRule> _logicDeleteRules = [];
    private readonly Dictionary<string, DbDataSource> _dataSources = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, Func<EntityMetadata, string, Sharding.IShardRule>> _shardRules = [];
    private bool _frozen;

    /// <summary>方言。</summary>
    public SqlDialect Dialect
    {
        get;
        set => field = Mutable(value);
    } = null!;

    /// <summary>ADO.NET 数据源（MySqlDataSource、NpgsqlDataSource 等）。</summary>
    public DbDataSource? DataSource
    {
        get;
        set => field = Mutable(value);
    }

    /// <summary>由类名、属性名推出表名、列名的约定，默认驼峰转下划线。</summary>
    public NameConversion NameConversion
    {
        get;
        set => field = Mutable(value);
    } = NameConversion.Underlined;

    /// <summary>
    /// 是否允许生成物理 DELETE（对应 Java 的 deleteThrowError 取反）。默认 true；设为 false 后，
    /// 没有逻辑删除的实体只有在命令上调用 <c>AllowDeleteStatement(true)</c> 才能删除，防止误删。
    /// </summary>
    public bool AllowDeleteStatement
    {
        get;
        set => field = Mutable(value);
    } = true;

    /// <summary>
    /// 深分页反向排序的偏移阈值（对应 Java 的 reverseOffsetThreshold）：分页的偏移量不小于它、且超过总数一半时，
    /// 把 ORDER BY 整体反过来取 <c>总数 - 偏移 - 本页条数</c> 处的一页，读出后在内存里倒序，减少数据库跳过的行数。
    /// 0（默认）表示不启用；排序里有原生片段或 NULL 位置时不反转。单条查询可用 <c>DisableReversePaging()</c> 关闭。
    /// </summary>
    public long ReverseOffsetThreshold
    {
        get;
        set => field = Mutable(value);
    }

    /// <summary>
    /// 按实体更新时默认写哪些列（对应 Java 的 updateStrategy）：默认全部列；设成 <see cref="Commands.ColumnStrategy.OnlyNotNull"/> 时只写非 null 的列。
    /// 命令上的 <c>Columns(..)</c> 与追踪的差异更新优先于它。
    /// </summary>
    public Commands.ColumnStrategy UpdateStrategy
    {
        get;
        set => field = Mutable(value);
    } = Commands.ColumnStrategy.All;

    /// <summary>
    /// 查询默认的条件值过滤（对应 Java 的 ValueFilterFactory.getQueryValueFilter）：如设成 <see cref="Filtering.ValueFilter.NotNullOrBlank"/>，
    /// 所有查询的 Where 里值为 null / 空白的比较条件都跳过；更新、删除不受影响。单条查询可用 <c>UseValueFilter(..)</c> 改。
    /// </summary>
    public Filtering.ValueFilter QueryValueFilter
    {
        get;
        set => field = Mutable(value);
    } = Filtering.ValueFilter.All;

    /// <summary>
    /// 按字典写入（<c>InsertMap</c> / <c>UpdateMap</c>）时键到列名的换算（对应 Java 的 MapKeyNameConversion）：默认键就是列名，
    /// 如 Oracle 上要全大写可设成 <c>key =&gt; key.ToUpperInvariant()</c>。
    /// </summary>
    public Func<string, string> MapKeyConversion
    {
        get;
        set => field = Mutable(value);
    } = key => key;

    /// <summary>
    /// IN 值列表的上限（对应 Java 的 maxInClauseSize）：超过时按上限拆成多个 IN 用 OR 连接；0（默认）表示不拆。
    /// Oracle 单个 IN 最多 1000 个值，需要时设成 1000。
    /// </summary>
    public int MaxInClauseSize
    {
        get;
        set => field = Mutable(value);
    }

    /// <summary>Include 的子数据带了 Take / Skip 时怎样按父对象各取一段（对应 Java 的 includeLimitMode），默认按父对象分区编行号。</summary>
    public IncludeLimitMode IncludeLimitMode
    {
        get;
        set => field = Mutable(value);
    } = IncludeLimitMode.Partition;

    /// <summary>
    /// 带出查询每次最多按多少个父键查（<c>IN (…)</c> 的项数，超出时分成几条查询；对应 Java 的 relationGroupSize），默认 512。
    /// 多对多的中间表和带排序的多对多只查一条，IN 按这个大小拆成 OR。
    /// </summary>
    public int IncludeGroupSize
    {
        get;
        set => field = Mutable(value);
    } = 512;

    /// <summary>
    /// <see cref="DataSource"/> 的名字（对应 Java 的 defaultDataSourceName），默认 <c>ds0</c>。分库时节点按名字引用数据源，
    /// 不分片的表都在这个数据源里。
    /// </summary>
    public string DefaultDataSourceName
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            field = Mutable(value);
        }
    } = "ds0";

    /// <summary>默认数据源之外、按名字登记的数据源（用 <see cref="AddDataSource"/> 添加），分库时节点按名字落在其中。</summary>
    public IReadOnlyDictionary<string, DbDataSource> DataSources => _dataSources;

    /// <summary>
    /// 同一数据源里一条语句最多把几张分表写成 <c>(SELECT * FROM a UNION ALL SELECT * FROM b) t</c>（对应 Java 的 SHARDING_UNION_ALL），默认 64：
    /// 排序、分页、分组、聚合都交给数据库；超出的再分成几条语句，在内存里归并。设成 1 时每张表一条语句（Java 的默认做法）。
    /// </summary>
    public int MaxUnionTables
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = Mutable(value);
        }
    } = 64;

    /// <summary>
    /// 一条语句裁剪之后最多命中多少个分片节点（对应 Java 的 maxShardingRouteCount），默认 128；超出时报错，防止条件漏写时扫遍全部分表。
    /// </summary>
    public int MaxShardNodes
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = Mutable(value);
        }
    } = 128;

    /// <summary>按实体登记的分片规则（创建 EasyDb 时按实体元数据建出）。</summary>
    internal IReadOnlyDictionary<Type, Func<EntityMetadata, string, Sharding.IShardRule>> ShardRules => _shardRules;

    /// <summary>执行监听器（用 <see cref="AddListener"/> 添加）。</summary>
    public IReadOnlyList<IExecutionListener> Listeners => _listeners;

    /// <summary>写入监听器（用 <see cref="AddWriteListener"/> 添加）。</summary>
    public IReadOnlyList<IWriteListener> WriteListeners => _writeListeners;

    /// <summary>拦截器（用 <see cref="AddInterceptor"/> 添加）。</summary>
    public InterceptorRegistry Interceptors { get; } = new();

    /// <summary>按名字注册的自定义逻辑删除策略，供 <c>[LogicDelete(LogicDeleteStrategy.Custom, StrategyName = "名字")]</c> 使用。</summary>
    public IReadOnlyDictionary<string, ILogicDeleteStrategy> LogicDeleteStrategies => _logicDeleteStrategies;

    /// <summary>按类型配置的逻辑删除（见 <see cref="ApplyLogicDelete"/>）。</summary>
    internal IReadOnlyList<LogicDeleteRule> LogicDeleteRules => _logicDeleteRules;

    /// <summary>使用 MySQL 方言（8.0.18 起；PolarDB、MariaDB 兼容）。</summary>
    public EasyDbOptions UseMySql(DbDataSource? dataSource = null) => Use(MySqlDialect.Instance, dataSource);

    /// <summary>使用 PostgreSQL 方言。</summary>
    public EasyDbOptions UsePostgreSql(DbDataSource? dataSource = null) => Use(PostgreSqlDialect.Instance, dataSource);

    /// <summary>使用 SQLite 方言。</summary>
    public EasyDbOptions UseSqlite(DbDataSource? dataSource = null) => Use(SqliteDialect.Instance, dataSource);

    /// <summary>使用 SQL Server 方言（驱动 Microsoft.Data.SqlClient：<c>SqlClientFactory.Instance.CreateDataSource(cs)</c>）。</summary>
    public EasyDbOptions UseSqlServer(DbDataSource? dataSource = null) => Use(SqlServerDialect.Instance, dataSource);

    /// <summary>使用 Oracle 方言（驱动 Oracle.ManagedDataAccess.Core）。</summary>
    public EasyDbOptions UseOracle(DbDataSource? dataSource = null) => Use(OracleDialect.Instance, dataSource);

    /// <summary>使用 DuckDB 方言（驱动 DuckDB.NET：<c>DuckDBClientFactory.Instance.CreateDataSource(cs)</c>）。</summary>
    public EasyDbOptions UseDuckDb(DbDataSource? dataSource = null) => Use(DuckDbDialect.Instance, dataSource);

    /// <summary>使用达梦 DM8 方言（驱动 DM.DmProvider）。</summary>
    public EasyDbOptions UseDameng(DbDataSource? dataSource = null) => Use(DamengDialect.Instance, dataSource);

    /// <summary>使用 SAP HANA 方言（驱动是 HANA 客户端自带的 Sap.Data.Hana，不在 nuget.org 上）。</summary>
    public EasyDbOptions UseHana(DbDataSource? dataSource = null) => Use(HanaDialect.Instance, dataSource);

    /// <summary>使用人大金仓 KingbaseES 方言。</summary>
    public EasyDbOptions UseKingbase(DbDataSource? dataSource = null) => Use(KingbaseDialect.Instance, dataSource);

    /// <summary>使用华为 GaussDB / openGauss 方言。</summary>
    public EasyDbOptions UseGaussDb(DbDataSource? dataSource = null) => Use(GaussDbDialect.Instance, dataSource);

    /// <summary>使用 ClickHouse 方言（更新、删除写成 ALTER TABLE 的 mutation；没有自增列、upsert、行锁、事务）。连接串要带的会话设置见 <see cref="ClickHouseDialect"/>。</summary>
    public EasyDbOptions UseClickHouse(DbDataSource? dataSource = null) => Use(ClickHouseDialect.Instance, dataSource);

    /// <summary>使用 IBM Db2 LUW 方言（驱动如 Net.IBM.Data.Db2）。</summary>
    public EasyDbOptions UseDb2(DbDataSource? dataSource = null) => Use(Db2Dialect.Instance, dataSource);

    /// <summary>使用 TDengine 方言（时序库：时间戳主键，没有 UPDATE、upsert、行锁、事务；驱动如 TDengine.Connector 的 WebSocket 连接）。</summary>
    public EasyDbOptions UseTDengine(DbDataSource? dataSource = null) => Use(TDengineDialect.Instance, dataSource);

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
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(Mutable(listener));
        return this;
    }

    /// <summary>
    /// 添加写入监听器（见 <see cref="IWriteListener"/>）：经本库成功写入后按表通知，事务里推迟到提交之后。
    /// 同一个实例只加一次，重复添加时抛出异常。
    /// </summary>
    public EasyDbOptions AddWriteListener(IWriteListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (_writeListeners.Contains(listener)) throw new InvalidOperationException($"写入监听器 {listener.GetType().Name} 重复添加");
        _writeListeners.Add(Mutable(listener));
        return this;
    }

    /// <summary>添加拦截器；同一类型重复注册时抛出异常（与 Java 的 applyInterceptor 一致）。</summary>
    public EasyDbOptions AddInterceptor(IInterceptor interceptor)
    {
        Interceptors.Add(Mutable(interceptor));
        return this;
    }

    /// <summary>注册自定义逻辑删除策略（对应 Java 的 applyLogicDeleteStrategy）；名字重复时抛出异常。</summary>
    public EasyDbOptions AddLogicDeleteStrategy(string name, ILogicDeleteStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(strategy);
        if (!_logicDeleteStrategies.TryAdd(name, Mutable(strategy))) throw new InvalidOperationException($"逻辑删除策略 {name} 重复注册");
        return this;
    }

    /// <summary>
    /// 不改实体、用代码给命中的实体配置逻辑删除（对应 Java 的 AbstractConfigurationLogicDeleteStrategy）：
    /// <paramref name="appliesTo"/> 命中、且实体自己没有标注 <see cref="Metadata.LogicDeleteAttribute"/> 时，把 <paramref name="property"/> 当作逻辑删除列。
    /// </summary>
    /// <example><code>options.ApplyLogicDelete(t =&gt; t == typeof(Config), nameof(Config.Deleted), new LogicDeleteAttribute());</code></example>
    public EasyDbOptions ApplyLogicDelete(Func<Type, bool> appliesTo, string property, Metadata.LogicDeleteAttribute logicDelete)
    {
        ArgumentNullException.ThrowIfNull(appliesTo);
        ArgumentException.ThrowIfNullOrEmpty(property);
        ArgumentNullException.ThrowIfNull(logicDelete);
        _logicDeleteRules.Add(Mutable(new LogicDeleteRule(appliesTo, property, logicDelete)));
        return this;
    }

    /// <summary>给默认数据源起名字（对应 Java 的 setDefaultDataSourceName），分库规则按名字引用它，见 <see cref="DefaultDataSourceName"/>。</summary>
    public EasyDbOptions WithDefaultDataSourceName(string name)
    {
        DefaultDataSourceName = name;
        return this;
    }

    /// <summary>
    /// 按名字登记一个数据源（对应 Java 的 ShardingDataSource），分库规则的节点按名字落在其中；方言与默认数据源相同。
    /// 名字不能与 <see cref="DefaultDataSourceName"/> 或已登记的重复。
    /// </summary>
    /// <example><code>options.UseMySql(ds2020).WithDefaultDataSourceName("ds2020").AddDataSource("ds2021", ds2021)</code></example>
    public EasyDbOptions AddDataSource(string name, DbDataSource dataSource)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(dataSource);
        Mutable(name);
        if (name == DefaultDataSourceName || !_dataSources.TryAdd(name, dataSource)) throw new InvalidOperationException($"数据源 {name} 重复登记");
        return this;
    }

    /// <summary>
    /// 实体 <typeparamref name="T"/> 分库分表（见 0006）：用内置规则 <see cref="Sharding.ShardRuleBuilder{T}.Mod"/>、
    /// <see cref="Sharding.ShardRuleBuilder{T}.ByMonth"/> 等登记。查询按条件裁剪出要查的表，写入按分片键落到各自的表。
    /// </summary>
    /// <example><code>
    /// options.Shard&lt;Order&gt;(r =&gt; r.Mod(o =&gt; o.Id, 3))
    ///        .Shard&lt;Log&gt;(r =&gt; r.ByMonth(l =&gt; l.CreateTime, from: new(2024, 1, 1)).DefaultOrder(l =&gt; l.CreateTime, descending: true));
    /// </code></example>
    public EasyDbOptions Shard<T>(Action<Sharding.ShardRuleBuilder<T>> configure) where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new Sharding.ShardRuleBuilder<T>();
        configure(builder);
        return AddShardRule(typeof(T), builder.Build);
    }

    /// <summary>实体 <typeparamref name="T"/> 按自定义规则分库分表（照 Java 的路由类实现 <see cref="Sharding.IShardRule"/>）。</summary>
    public EasyDbOptions Shard<T>(Sharding.IShardRule rule) where T : class
    {
        ArgumentNullException.ThrowIfNull(rule);
        return AddShardRule(typeof(T), (_, _) => rule);
    }

    private EasyDbOptions AddShardRule(Type type, Func<EntityMetadata, string, Sharding.IShardRule> rule)
    {
        if (!_shardRules.TryAdd(type, Mutable(rule))) throw new InvalidOperationException($"{type.Name} 的分片规则重复登记");
        return this;
    }

    /// <summary>交给 <see cref="EasyDb"/> 时调用：之后不能再改。</summary>
    internal void Freeze() => _frozen = true;

    private T Mutable<T>(T value) =>
        _frozen ? throw new InvalidOperationException("EasyDbOptions 已经交给 EasyDb，不能再修改：在创建 EasyDb 之前完成配置") : value;
}

/// <summary>Include 的子数据带了 Take / Skip 时按父对象取段的方式（对应 Java 的 IncludeLimitModeEnum）。</summary>
public enum IncludeLimitMode
{
    /// <summary>一条查询，按关联键分区编行号再按行号取段（ROW_NUMBER，需要数据库支持窗口函数）。</summary>
    Partition,

    /// <summary>每个父对象一段带 LIMIT 的查询，UNION ALL 合并。</summary>
    UnionAll,
}
