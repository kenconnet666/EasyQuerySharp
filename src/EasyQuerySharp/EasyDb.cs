using System.Data;
using System.Data.Common;
using EasyQuerySharp.Commands;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp;

/// <summary>
/// 入口：查询、写入、事务、原生 SQL。线程安全，通常整个应用一个实例（按数据库）。
/// </summary>
/// <example><code>
/// var db = new EasyDb(o => o.UseMySql(new MySqlDataSource(connectionString)));
/// var users = await db.Query&lt;User&gt;().Where(u => u.Name.Contains("ali")).ToListAsync();
/// </code></example>
public sealed class EasyDb
{
    /// <summary>用配置函数创建。</summary>
    public EasyDb(Action<EasyDbOptions> configure) : this(Configure(configure))
    {
    }

    /// <summary>用配置对象创建。</summary>
    public EasyDb(EasyDbOptions options)
    {
        if (options.Dialect is null) throw new ArgumentException("必须指定方言，如 options.UseMySql(...)", nameof(options));
        Options = options;
        Metadata = new MetadataRegistry(options.NameConversion) { LogicDeleteRules = options.LogicDeleteRules };
        Executor = new SqlExecutor(this);
        CodeFirst = new Schema.CodeFirst(this);
        Tracker = new Tracking.Tracker(Metadata);
    }

    internal Tracking.Tracker Tracker { get; }

    /// <summary>按实体建表、删表（code-first）。</summary>
    public Schema.CodeFirst CodeFirst { get; }

    /// <summary>配置（创建后不要再修改）。</summary>
    public EasyDbOptions Options { get; }

    /// <summary>实体元数据。</summary>
    public MetadataRegistry Metadata { get; }

    /// <summary>拦截器。</summary>
    public InterceptorRegistry Interceptors => Options.Interceptors;

    internal SqlExecutor Executor { get; }

    /// <summary>从实体 <typeparamref name="T"/> 开始一条查询。</summary>
    public Query<T> Query<T>() => new(new QueryState(this, [new EntitySource(typeof(T))]) { ValueFilter = Options.QueryValueFilter });

    /// <summary>
    /// 给已经查出来的实体再带出导航（对应 Java 的 loadInclude），与查询时 Include 相同：一条 <c>WHERE 键 IN (…)</c> 查询，挂到导航属性上。
    /// <c>await db.LoadIncludeAsync(users, u =&gt; u.BankCards)</c>
    /// </summary>
    public Task LoadIncludeAsync<T, TProperty>(IEnumerable<T> entities, System.Linq.Expressions.Expression<Func<T, TProperty>> navigation,
        CancellationToken cancellationToken = default) where T : class =>
        LoadInclude(entities, navigation, null, cancellationToken);

    /// <summary>给已经查出来的实体带出集合导航，并配置子查询（条件、排序、Take 等，与 <c>Include(nav, q =&gt; …)</c> 相同）。</summary>
    public Task LoadIncludeAsync<T, TChild>(IEnumerable<T> entities, System.Linq.Expressions.Expression<Func<T, IEnumerable<TChild>?>> navigation,
        Func<Query<TChild>, Query<TChild>> configure, CancellationToken cancellationToken = default) where T : class where TChild : class =>
        LoadInclude(entities, navigation, s => configure(new Query<TChild>(s)).State, cancellationToken);

    private Task LoadInclude<T>(IEnumerable<T> entities, System.Linq.Expressions.LambdaExpression navigation, Func<QueryState, QueryState>? configure,
        CancellationToken cancellationToken) where T : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (Translation.ExpressionTranslator.StripConvert(navigation.Body) is not System.Linq.Expressions.MemberExpression { Expression: System.Linq.Expressions.ParameterExpression, Member: System.Reflection.PropertyInfo property })
            throw new ArgumentException($"LoadIncludeAsync 需要直接的导航属性，如 u => u.Posts：{navigation}", nameof(navigation));
        var nav = Metadata.FindNavigation(Metadata.Get<T>(), property.Name) ?? throw new ArgumentException($"{typeof(T).Name}.{property.Name} 不是导航属性", nameof(navigation));
        var plan = IncludePlan.ForNavigation(Metadata, nav, configure, unaliased: true);
        foreach (var entity in entities)
            if (entity is not null) plan.RegisterLoaded(entity, property);
        return IncludeRunner.RunAsync(this, [plan], IncludeContext.Default with { Track = Tracker.Active }, cancellationToken);
    }

    /// <summary>
    /// 不经实体查询表 <paramref name="tableName"/>（对应 Java 的 queryable(Map.class).asTable(..) / mapQueryable()）：
    /// 列用 <see cref="DynamicRow.Get{T}"/> 按名字引用，不投影时 <c>SELECT *</c> 读成 <see cref="DynamicRow"/>。
    /// 联表写 <c>.LeftJoin&lt;DynamicRow&gt;((a, b) => …).AsTable("t2")</c>；Join 另一个 <c>db.Table(..)</c> 查询时它作为派生表。
    /// </summary>
    /// <example><code>await db.Table("t_topic").Where(t => t.Get&lt;string&gt;("id") == "1").ToListAsync();</code></example>
    public Query<DynamicRow> Table(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        return new(new QueryState(this, [new EntitySource(typeof(DynamicRow)) { TableName = tableName }]));
    }

    /// <summary>
    /// 以原生 SQL 为来源开始一条查询（对应 Java 的 queryable(sql, Class, params)）：它作为派生表 <c>(sql) t</c>，
    /// 列按 <typeparamref name="T"/> 的列名访问，之后可以照常 Where / Select / 分页；<c>?</c> 依次对应 <paramref name="parameters"/>。
    /// <typeparamref name="T"/> 也可以是标量（取第一列）或 <c>Dictionary&lt;string, object?&gt;</c>（整行）。
    /// </summary>
    public Query<T> QuerySql<T>(string sql, params IReadOnlyList<object?> parameters) =>
        new(new QueryState(this, [new RawSqlSource(sql, typeof(T), parameters)]));

    /// <summary>执行原生写入 SQL，<c>?</c> 依次对应参数（对应 Java 的 sqlExecute），返回受影响的行数。</summary>
    public Task<long> ExecuteSqlAsync(string sql, IReadOnlyList<object?>? parameters = null, CancellationToken cancellationToken = default) =>
        Executor.ExecuteAsync(RawSql.FromPositional(Options.Dialect, sql, parameters ?? []), cancellationToken);

    /// <summary>执行原生写入 SQL，插值处全部参数化：<c>$"update t set a = {a} where id = {id}"</c>。</summary>
    public Task<long> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default) =>
        Executor.ExecuteAsync(RawSql.FromInterpolated(Options.Dialect, sql), cancellationToken);

    /// <summary>
    /// 执行原生查询 SQL（对应 Java 的 sqlQuery / sqlQueryMap）：<typeparamref name="T"/> 是实体或 DTO 时按列名映射，
    /// 是标量时取第一列，是 <c>Dictionary&lt;string, object?&gt;</c> 时整行读成字典。
    /// </summary>
    public Task<List<T>> SqlQueryAsync<T>(string sql, IReadOnlyList<object?>? parameters = null, CancellationToken cancellationToken = default) =>
        Executor.QueryAsync(RawSql.FromPositional(Options.Dialect, sql, parameters ?? []), RowReader<T>(), cancellationToken);

    /// <summary>执行原生查询 SQL，插值处全部参数化。</summary>
    public Task<List<T>> SqlQueryAsync<T>(FormattableString sql, CancellationToken cancellationToken = default) =>
        Executor.QueryAsync(RawSql.FromInterpolated(Options.Dialect, sql), RowReader<T>(), cancellationToken);

    // 标量取第一列；字典整行；实体 / DTO 按列名映射
    private Func<DbDataReader, T> RowReader<T>()
    {
        if (ScalarTypes.IsScalar(typeof(T))) return r => Materialization.ValueReader.Read<T>(r, 0, null);
        if (typeof(T) == typeof(Dictionary<string, object?>) || typeof(T) == typeof(DynamicRow)) return r => (T)(object)Materialization.EntityMaterializer.ReadDictionary(r);
        var entity = Metadata.Get<T>();
        return r => Materialization.EntityMaterializer.ReadByName<T>(r, entity);
    }

    // 单个实体、数组、List 都走同一个 params 集合签名：避免 Insert<T>(T) 把数组 / 列表当成实体
    /// <summary>插入一个或多个实体（每个一条 INSERT，同一连接上依次执行）。</summary>
    public InsertCommand<T> Insert<T>(params IEnumerable<T> entities) where T : class => new(this, entities.ToList());

    /// <summary>按条件更新：<c>db.Update&lt;T&gt;().Set(…).Where(…)</c>。</summary>
    public UpdateCommand<T> Update<T>() where T : class => new(this, null);

    /// <summary>按主键整行更新一个或多个实体（每个一条 UPDATE）。</summary>
    public UpdateCommand<T> Update<T>(params IEnumerable<T> entities) where T : class => new(this, entities.ToList());

    /// <summary>按字典插入到 <paramref name="tableName"/>（键是列名，对应 Java 的 mapInsertable）。</summary>
    public MapInsertCommand InsertMap(string tableName, params IEnumerable<IReadOnlyDictionary<string, object?>> rows) => new(this, tableName, rows.ToList());

    /// <summary>按字典更新 <paramref name="tableName"/>（键是列名，对应 Java 的 mapUpdatable），用 WhereColumns 指定定位列。</summary>
    public MapUpdateCommand UpdateMap(string tableName, params IEnumerable<IReadOnlyDictionary<string, object?>> rows) => new(this, tableName, rows.ToList());

    /// <summary>按条件删除：<c>db.Delete&lt;T&gt;().Where(…)</c>。有逻辑删除列时改写成更新。</summary>
    public DeleteCommand<T> Delete<T>() where T : class => new(this, null);

    /// <summary>按主键删除一个或多个实体。</summary>
    public DeleteCommand<T> Delete<T>(params IEnumerable<T> entities) where T : class => new(this, entities.ToList());

    /// <summary>
    /// 聚合保存（对应 Java 的 savable(root)，见 0004）：按追踪快照的差异一次写出整个聚合；必须在追踪作用域与事务里执行。
    /// </summary>
    public Commands.SaveCommand<T> Save<T>(params IEnumerable<T> roots) where T : class => new(this, roots.ToList());

    /// <summary>
    /// 开启追踪作用域（见 <see cref="Tracking.TrackingScope"/>）：作用域内查出的实体被追踪，按实体更新时只写变化的列。
    /// </summary>
    /// <example><code>
    /// using var tracking = db.BeginTracking();
    /// var topic = await db.Query&lt;Topic&gt;().WhereById("7").FirstAsync();
    /// topic.Title = "new";
    /// await db.Update(topic).ExecuteAsync();   // UPDATE t_topic SET title = ? WHERE id = ?
    /// </code></example>
    public Tracking.TrackingScope BeginTracking() => new(this);

    /// <summary>当前异步流程是否在追踪作用域里（对应 Java 的 currentThreadTracking）。</summary>
    public bool IsTracking => Tracker.Active;

    /// <summary>把实体加入当前追踪作用域（以它此刻的值为原始值）；不在作用域里返回 false（对应 Java 的 addTracking）。</summary>
    public bool AddTracking(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Tracker.Add(entity);
    }

    /// <summary>
    /// 被追踪的实体自追踪（或上次写入成功）以来值变了的属性（主键除外），按列顺序返回
    /// （对应 Java 的 getTrackEntityStateNotNull(e).getEntityValueState(属性).isChanged()）；没有被追踪时抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public IReadOnlyList<string> GetTrackedChanges(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var entry = Tracker.Find(entity) ?? throw new InvalidOperationException($"{entity.GetType().Name} 没有被追踪，先 BeginTracking() 并 AddTracking(..)");
        return entry.ChangedColumns().Select(c => c.PropertyName).ToList();
    }

    /// <summary>不再追踪这个实体；不在作用域里或本就没追踪时返回 false。</summary>
    public bool RemoveTracking(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Tracker.Remove(entity);
    }

    /// <summary>开启环境事务（见 <see cref="EasyTransaction"/>）。</summary>
    public EasyTransaction BeginTransaction(IsolationLevel isolation = IsolationLevel.Unspecified) => new(this, isolation);

    /// <summary>在事务里执行 <paramref name="work"/>：正常结束提交，抛出异常回滚。</summary>
    public async Task TransactionAsync(Func<Task> work, IsolationLevel isolation = IsolationLevel.Unspecified, CancellationToken cancellationToken = default)
    {
        await using var tx = BeginTransaction(isolation);
        await work().ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>打开一个新连接（调用方负责释放）。</summary>
    internal async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var source = Options.DataSource
                     ?? throw new InvalidOperationException("没有配置数据源，只能生成 SQL（ToSql）。请在 EasyDbOptions 里传入 DbDataSource");
        return await source.OpenConnectionAsync(ct).ConfigureAwait(false);
    }

    private static EasyDbOptions Configure(Action<EasyDbOptions> configure)
    {
        var options = new EasyDbOptions();
        configure(options);
        return options;
    }
}
