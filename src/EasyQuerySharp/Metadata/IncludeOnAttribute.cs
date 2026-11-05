namespace EasyQuerySharp.Metadata;

/// <summary>
/// 导航只给满足条件的父对象带出（对应 Java 的 @IncludeOnProperty）：父对象的 <see cref="Property"/> 等于 <see cref="Value"/> 时才查询并挂上子数据，
/// 其余父对象的这个导航保持 null。适合按类型 / 版本字段决定挂哪张扩展表的模型。
/// </summary>
/// <example><code>
/// [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Task1Ext.TaskId)])]
/// [IncludeOn(nameof(Version), 1L)]
/// public Task1Ext? Ext1 { get; set; }
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IncludeOnAttribute(string property, object value) : Attribute
{
    /// <summary>父实体上的属性名。</summary>
    public string Property { get; } = property;

    /// <summary>满足条件的值（按属性类型比较）。</summary>
    public object Value { get; } = value;
}
