using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 插入、更新、删除共有的部分：目标表（AsTable / AsSchema / WithTableLink）与拦截器的开关。
/// 写入命令是一次性的可变构建器（与不可变的查询不同），链式调用返回自身。
/// </summary>
public abstract class EntityCommandBase<TSelf, T>
    where TSelf : EntityCommandBase<TSelf, T>
    where T : class
{
    private string? _tableName;
    private string? _schema;
    private string? _alias;
    private Func<string, string>? _link;

    private protected EntityCommandBase(EasyDb db)
    {
        Db = db;
        Entity = db.Metadata.Get<T>();
    }

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db { get; }

    private protected EntityMetadata Entity { get; }

    private protected InterceptorSelection Interceptors { get; private set; } = InterceptorSelection.Default;

    private protected TSelf Self => (TSelf)this;

    private protected bool UseBatch { get; private set; }

    /// <summary>
    /// 批量执行（对应 Java 的 batch()）：多个实体的语句按 SQL 分组，每组（最多 1000 条）一次发出，监听器每组收到一次执行。
    /// 驱动支持 <see cref="System.Data.Common.DbBatch"/> 时一组是一个往返，否则在同一连接上逐条执行。
    /// 组按首次出现的顺序执行，组内保持原顺序（与 Java 一致）。不在事务里时每组在自己的事务里执行，整组成功或整组不生效。
    /// 驱动不逐条报告行数时（MySqlConnector），带版本的更新只有整组都写到才把新版本写回实体。
    /// </summary>
    public TSelf Batch(bool use = true)
    {
        UseBatch = use;
        return Self;
    }

    /// <summary>写入到另一张同结构的表（对应 Java 的 asTable）。</summary>
    public TSelf AsTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        _tableName = tableName;
        return Self;
    }

    /// <summary>按当前表名算出新表名（对应 Java 的 asTable(Function)），如 <c>.AsTable(name => name + "_2024")</c>。</summary>
    public TSelf AsTable(Func<string, string> rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        return AsTable(rename(_tableName ?? Entity.TableName));
    }

    /// <summary>写入到另一个 schema（对应 Java 的 asSchema）。</summary>
    public TSelf AsSchema(string schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        _schema = schema;
        return Self;
    }

    /// <summary>按当前 schema 算出新 schema（对应 Java 的 asSchema(Function)）；实体没有 schema 时传入空串。</summary>
    public TSelf AsSchema(Func<string, string> rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        return AsSchema(rename(_schema ?? Entity.Schema ?? ""));
    }

    /// <summary>
    /// 目标表名前面加一段写法（对应 Java 的 asTableLink）：参数是原来的连接符，写入的目标表是空串，返回值直接写在表名前面，
    /// 如 <c>.WithTableLink(link =&gt; link + "1")</c> 写成 <c>INSERT INTO 1`t_topic`</c>。
    /// </summary>
    public TSelf WithTableLink(Func<string, string> link)
    {
        ArgumentNullException.ThrowIfNull(link);
        _link = link;
        return Self;
    }

    /// <summary>目标表名前面加上 <paramref name="link"/>（对应 Java 的 asTableLink(String)）。</summary>
    public TSelf WithTableLink(string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        return WithTableLink(_ => link);
    }

    /// <summary>给目标表起别名（对应 Java 的 asAlias），列引用随之带上别名：<c>UPDATE `t_blog` xx SET xx.`deleted` = ?</c>。</summary>
    public TSelf AsAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrEmpty(alias);
        _alias = alias;
        return Self;
    }

    /// <summary>不应用名为 <paramref name="name"/> 的拦截器（对应 Java 的 noInterceptor(name)）。</summary>
    public TSelf DisableInterceptor(string name)
    {
        Interceptors = Interceptors.Disable(name);
        return Self;
    }

    /// <summary>不应用任何拦截器（对应 Java 的 noInterceptor()）；之后再 EnableInterceptor 的除外。</summary>
    public TSelf DisableInterceptors()
    {
        Interceptors = Interceptors.DisableAll();
        return Self;
    }

    /// <summary>应用名为 <paramref name="name"/> 的拦截器，包括默认关闭的（对应 Java 的 useInterceptor(name)）。</summary>
    public TSelf EnableInterceptor(string name)
    {
        Interceptors = Interceptors.Enable(name);
        return Self;
    }

    /// <summary>恢复默认生效的全部拦截器（对应 Java 的 useInterceptor()）。</summary>
    public TSelf EnableInterceptors()
    {
        Interceptors = Interceptors.EnableAll();
        return Self;
    }

    private protected EntityTableSource NewTable() => new(Entity) { TableNameOverride = _tableName, SchemaOverride = _schema, ExplicitAlias = _alias, Link = _link };

    /// <summary>目标表的分片规则；实体不分片或显式 AsTable 时为 null（显式换表的写入不走分片）。</summary>
    private protected Sharding.IShardRule? ShardRule => _tableName is null ? Db.Shards.RuleOf(typeof(T)) : null;

    /// <summary>
    /// 写入语句按落点渲染（见 0006 §6）：分片的目标表按实体的节点（<paramref name="entity"/> 不为 null 时）或按条件裁剪出的节点，每个节点一条、带上数据源；
    /// 条件、SET 里的子查询用到分片表时内联成 UNION ALL。语句里没有分片表时原样渲染一条。
    /// </summary>
    private protected List<SqlStatement> Routed(EntityTableSource table, IReadOnlyList<SqlExpr> where, IEnumerable<JoinClause> joins, IEnumerable<SqlExpr> expressions, T? entity,
        Func<Sharding.TableRoutes?, SqlStatement> render)
    {
        var node = entity is not null && ShardRule is { } rule ? rule.NodeOf(entity) : null;
        var plan = Db.Shards.Enabled ? Sharding.ShardPlanner.PlanWrite(Db, table, where, joins, expressions, node) : null;
        return plan is null ? [render(null)] : [.. plan.Units.Select(u => render(u.Routes) with { DataSource = u.DataSource })];
    }

    /// <summary>按语句的数据源分组执行：每个数据源一个会话（事务里是该数据源的事务连接），组内保持原顺序，分片的写入逐个节点串行。返回各组行数之和。</summary>
    private protected async Task<long> ExecuteRoutedAsync<TItem>(IReadOnlyList<TItem> items, Func<TItem, SqlStatement> statement,
        Func<Execution.SqlSession, IReadOnlyList<TItem>, Task<long>> run, CancellationToken ct)
    {
        long rows = 0;
        foreach (var group in items.GroupBy(i => statement(i).DataSource))
            rows += await Db.Executor.SessionAsync(group.Key, s => run(s, [.. group]), ct).ConfigureAwait(false);
        return rows;
    }
}
