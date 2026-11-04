using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>一个导航属性的映射信息。由 <see cref="MetadataRegistry"/> 在首次使用时解析（需要目标实体的元数据）。</summary>
public sealed class NavigationMetadata
{
    internal NavigationMetadata(PropertyInfo property, NavigateAttribute attribute, EntityMetadata self, EntityMetadata target,
        IReadOnlyList<ColumnMetadata> selfColumns, IReadOnlyList<ColumnMetadata> targetColumns,
        EntityMetadata? mapping, IReadOnlyList<ColumnMetadata> selfMappingColumns, IReadOnlyList<ColumnMetadata> targetMappingColumns,
        LambdaExpression? extraFilter)
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
        ExtraFilter = extraFilter;
    }

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

    /// <summary>导航上的固定条件（参数为目标实体）；没有时为 null。</summary>
    public LambdaExpression? ExtraFilter { get; }

    /// <summary>是否集合导航（一对多、多对多）。</summary>
    public bool IsCollection => Relation is RelationType.OneToMany or RelationType.ManyToMany;

    /// <inheritdoc />
    public override string ToString() => $"{Self.Type.Name}.{Property.Name} → {Target.Type.Name} ({Relation})";
}
