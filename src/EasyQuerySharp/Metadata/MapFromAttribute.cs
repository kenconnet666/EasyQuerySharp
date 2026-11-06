namespace EasyQuerySharp.Metadata;

/// <summary>
/// 声明 DTO 映射自实体 <typeparamref name="TEntity"/>（见 0002 的"按结构自动映射"）：分析器在编译期检查成员能否对上（EQ0003）、
/// 对一导航的可空性（EQ0005）。<c>Select&lt;TDto&gt;()</c> 的映射与没有标注的 DTO 相同，只是要求查询的实体就是 <typeparamref name="TEntity"/>。
/// </summary>
/// <example><code>
/// [MapFrom&lt;User&gt;]
/// public sealed class UserVo
/// {
///     public long Id { get; set; }
///     public string Name { get; set; } = "";
///     [MapPath("Company.Name")] public string? CompanyName { get; set; }
///     public List&lt;PostVo&gt;? Posts { get; set; }
/// }
/// </code></example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MapFromAttribute<TEntity> : Attribute;
