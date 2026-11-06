namespace EasyQuerySharp.Sharding;

/// <summary>
/// 一张物理表：所在的数据源与表名（对应 Java 的数据源 + ActualTable）。不分库的实体，数据源就是默认数据源。
/// </summary>
/// <param name="DataSource">数据源名：<see cref="EasyDbOptions.DefaultDataSourceName"/> 或 <see cref="EasyDbOptions.AddDataSource"/> 登记的名字。</param>
/// <param name="Table">物理表名（不加引号）。</param>
public sealed record ShardNode(string DataSource, string Table)
{
    /// <inheritdoc />
    public override string ToString() => $"{DataSource}.{Table}";
}

/// <summary>分片键上的比较（对应 Java 的 ShardingOperatorEnum）。IN 由引擎拆成各值的等值再取并集。</summary>
public enum ShardOperator
{
    /// <summary><c>=</c></summary>
    Equal,

    /// <summary><c>&gt;</c></summary>
    GreaterThan,

    /// <summary><c>&gt;=</c></summary>
    GreaterThanOrEqual,

    /// <summary><c>&lt;</c></summary>
    LessThan,

    /// <summary><c>&lt;=</c></summary>
    LessThanOrEqual,
}

/// <summary>
/// 查询没写排序、带了 Take / Skip、命中多个节点时补上的排序（对应 Java 的 addPropertyDefaultUseDesc）：
/// 跨表取前几行时结果才是确定的。
/// </summary>
/// <param name="Property">实体属性名。</param>
/// <param name="Descending">是否降序。</param>
public sealed record ShardOrder(string Property, bool Descending);

/// <summary>
/// 一个实体的分片规则（对应 Java 的初始化器 ShardingInitializer 加路由 TableRoute / DataSourceRoute）：
/// 有哪些节点、一行写到哪个节点、一个原子条件可能命中哪些节点。内置规则由 <c>options.Shard&lt;T&gt;(r =&gt; r.Mod(..))</c> 等得到，
/// 自定义规则实现这个接口后用 <see cref="EasyDbOptions.Shard{T}(IShardRule)"/> 登记。
/// </summary>
/// <remarks>
/// <see cref="Prune"/> 只处理单个原子条件，AND（交集）/ OR（并集）/ IN（各值并集）由引擎组合；返回值必须是 <see cref="Nodes"/> 的子集，
/// 宁可少裁剪也不能裁错。实现要线程安全。
/// </remarks>
public interface IShardRule
{
    /// <summary>全部节点。按时间分片的规则按截止时间实时计算。</summary>
    IReadOnlyList<ShardNode> Nodes { get; }

    /// <summary>参与路由的属性：分片键与额外分片键（对应 Java 的 ShardingTableKey、ShardingExtraTableKey、ShardingDataSourceKey）。</summary>
    IReadOnlyCollection<string> KeyProperties { get; }

    /// <summary>实体写到哪个节点；分片键为 null 或落在节点范围之外时抛出带实体信息的异常。</summary>
    ShardNode NodeOf(object entity);

    /// <summary>条件 <c>属性 op 值</c> 可能命中的节点；帮不上裁剪时返回 <see cref="Nodes"/>。</summary>
    IReadOnlyList<ShardNode> Prune(string propertyName, ShardOperator op, object? value);

    /// <summary>无排序、带 Take / Skip、命中多个节点时补的排序；不需要时为 null。</summary>
    ShardOrder? DefaultOrder => null;
}
