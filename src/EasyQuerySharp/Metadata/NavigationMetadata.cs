using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>一个导航属性的映射信息。由 <see cref="MetadataRegistry"/> 在首次使用时解析（需要目标实体的元数据）。</summary>
public sealed class NavigationMetadata
{
    internal NavigationMetadata(PropertyInfo property, NavigateAttribute attribute, EntityMetadata self, EntityMetadata target,
        IReadOnlyList<ColumnMetadata> selfColumns, IReadOnlyList<ColumnMetadata> targetColumns,
        EntityMetadata? mapping, IReadOnlyList<ColumnMetadata> selfMappingColumns, IReadOnlyList<ColumnMetadata> targetMappingColumns,
        Func<LambdaExpression?>? extraFilter, Func<LambdaExpression?>? extraMappingFilter)
    {
        Property = property;
        Relation = attribute.Relation;
        Required = attribute.Required;
        Self = self;
        Target = target;
        SelfColumns = selfColumns;
        TargetColumns = targetColumns;
        Mapping = mapping;
        SelfMappingColumns = selfMappingColumns;
        TargetMappingColumns = targetMappingColumns;
        _extraFilter = extraFilter;
        _extraMappingFilter = extraMappingFilter;
        DirectPath = attribute.DirectMapping;
        GroupJoin = attribute.GroupJoin;
        PartitionOrder = attribute.PartitionOrder;
        Limit = attribute.Limit > 0 ? attribute.Limit : null;
        Offset = attribute.Offset > 0 ? attribute.Offset : null;
        Cascade = attribute.Cascade;
        KeyMatch = attribute.KeyMatch;
        DefaultOrder = ParseOrder(attribute.OrderBy, target.Type);
    }

    /// <summary>键怎样算匹配（<see cref="NavigateAttribute.KeyMatch"/>）。</summary>
    public KeyMatch KeyMatch { get; }

    /// <summary>聚合保存时的级联方式（<see cref="NavigateAttribute.Cascade"/>）。</summary>
    public Cascade Cascade { get; }

    /// <summary>带出时每个父对象最多取几条（<see cref="NavigateAttribute.Limit"/>）；不限时为 null。</summary>
    public int? Limit { get; }

    /// <summary>带出时每个父对象跳过几条（<see cref="NavigateAttribute.Offset"/>）；不跳时为 null。</summary>
    public int? Offset { get; }

    /// <summary>取元素、取一段行而链上没写排序时组内按什么排。</summary>
    public PartitionOrder PartitionOrder { get; }

    /// <summary>导航上声明的默认排序（<see cref="NavigateAttribute.OrderBy"/>）：参数为目标实体的键，NULL 的位置包成 Sql.NullsFirst / NullsLast。</summary>
    public IReadOnlyList<(LambdaExpression Key, bool Descending)> DefaultOrder { get; }

    // "OpenTime, Code desc nulls first" → [(x => x.OpenTime, false), (x => Sql.NullsFirst(x.Code), true)]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    internal static List<(LambdaExpression Key, bool Descending)> ParseOrder(string? orderBy, Type target)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return [];
        return orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item =>
        {
            var words = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parameter = Expression.Parameter(target, "x");
            Expression key = Expression.Property(parameter, target.GetProperty(words[0])
                ?? throw new InvalidOperationException($"导航排序 '{orderBy}' 里的 {words[0]} 不是 {target.Name} 的属性"));
            var rest = string.Join(' ', words.Skip(1)).ToLowerInvariant();
            var nulls = rest.EndsWith("nulls first", StringComparison.Ordinal) ? nameof(Sql.NullsFirst)
                : rest.EndsWith("nulls last", StringComparison.Ordinal) ? nameof(Sql.NullsLast) : null;
            if (nulls is not null) key = Expression.Call(typeof(Sql).GetMethod(nulls)!.MakeGenericMethod(key.Type), key);
            return ((LambdaExpression)Expression.Lambda(key, parameter), rest.StartsWith("desc", StringComparison.Ordinal));
        }).ToList();
    }

    /// <summary>集合导航上的聚合默认合并成分组 LEFT JOIN。</summary>
    public bool GroupJoin { get; }

    /// <summary>直达路径上依次经过的导航属性名；不是直达导航时为空。</summary>
    public IReadOnlyList<string> DirectPath { get; }

    /// <summary>只给满足条件的父对象带出（<see cref="IncludeOnAttribute"/>）：父实体上的列与要等于的值；没有时为 null。</summary>
    public (ColumnMetadata Column, object Value)? IncludeOn { get; internal init; }

    /// <summary>导航属性。</summary>
    public PropertyInfo Property { get; }

    /// <summary>关系类型。</summary>
    public RelationType Relation { get; }

    /// <summary>对一导航是否必然存在（隐式 JOIN 用 INNER）。</summary>
    public bool Required { get; }

    /// <summary>本实体。</summary>
    public EntityMetadata Self { get; }

    /// <summary>目标实体。</summary>
    public EntityMetadata Target { get; }

    /// <summary>本表参与关联的列。</summary>
    public IReadOnlyList<ColumnMetadata> SelfColumns { get; }

    /// <summary>目标表参与关联的列（多对多时是目标表与中间表关联的列）。</summary>
    public IReadOnlyList<ColumnMetadata> TargetColumns { get; }

    /// <summary>多对多的中间表；其他关系为 null。</summary>
    public EntityMetadata? Mapping { get; }

    /// <summary>中间表里对应本表的列。</summary>
    public IReadOnlyList<ColumnMetadata> SelfMappingColumns { get; }

    /// <summary>中间表里对应目标表的列。</summary>
    public IReadOnlyList<ColumnMetadata> TargetMappingColumns { get; }

    private readonly Func<LambdaExpression?>? _extraFilter;
    private readonly Func<LambdaExpression?>? _extraMappingFilter;

    /// <summary>导航上的固定条件（参数为目标实体），每次取值时重新计算；没有时为 null。</summary>
    public LambdaExpression? ExtraFilter => _extraFilter?.Invoke();

    /// <summary>多对多中间表上的固定条件（参数为中间表实体），每次取值时重新计算；没有时为 null。</summary>
    public LambdaExpression? ExtraMappingFilter => _extraMappingFilter?.Invoke();

    /// <summary>是否集合导航（一对多、多对多）。</summary>
    public bool IsCollection => Relation is RelationType.OneToMany or RelationType.ManyToMany;

    /// <inheritdoc />
    public override string ToString() => $"{Self.Type.Name}.{Property.Name} → {Target.Type.Name} ({Relation})";
}
