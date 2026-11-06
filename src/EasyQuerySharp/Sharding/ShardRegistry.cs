using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Sharding;

/// <summary>一个 <see cref="EasyDb"/> 的分片配置：各实体的规则（创建 EasyDb 时建好并校验数据源名）。</summary>
internal sealed class ShardRegistry
{
    private readonly Dictionary<Type, IShardRule> _rules;

    public ShardRegistry(EasyDbOptions options, MetadataRegistry metadata)
    {
        DefaultDataSource = options.DefaultDataSourceName;
        _rules = options.ShardRules.ToDictionary(r => r.Key, r => r.Value(metadata.Get(r.Key), DefaultDataSource));
        foreach (var (type, rule) in _rules)
        foreach (var node in rule.Nodes)
            if (node.DataSource != DefaultDataSource && !options.DataSources.ContainsKey(node.DataSource))
                throw new InvalidOperationException($"{type.Name} 的分片节点 {node} 用到的数据源 {node.DataSource} 没有登记：用 options.AddDataSource(\"{node.DataSource}\", …) 登记");
    }

    /// <summary>默认数据源的名字。</summary>
    public string DefaultDataSource { get; }

    /// <summary>有没有分片的实体；没有时执行不做路由。</summary>
    public bool Enabled => _rules.Count > 0;

    /// <summary>实体的分片规则；不分片时为 null。</summary>
    public IShardRule? RuleOf(Type entityType) => _rules.GetValueOrDefault(entityType);

    /// <summary>这张表要不要分片：实体有规则、且没有显式换表（AsTable / MapTable / AsTableSegment 的来源不走分片）。</summary>
    public IShardRule? RuleOf(EntityTableSource table) => table is { TableNameOverride: null, Segment: null } ? RuleOf(table.Entity.Type) : null;
}
