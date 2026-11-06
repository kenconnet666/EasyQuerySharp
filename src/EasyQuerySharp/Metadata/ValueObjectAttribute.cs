namespace EasyQuerySharp.Metadata;

/// <summary>
/// 值对象（对应 TS 版的 <c>embed()</c>、Java 已弃用的 @ValueObject）：属性的类型是一个普通的类，它的每个可映射属性在本表上占一列，
/// 列名默认是"属性的列名_子属性的列名"（如 <c>Address.City</c> → <c>address_city</c>），子属性上的 <c>[Column("…")]</c> 给出完整列名。
/// 值对象可以嵌套；查询里 <c>s.Address.City</c> 就是一列，<c>s.Address == null</c> 是它的列全为 NULL；读出时列全为 NULL 的值对象是 null。
/// 值对象的类型要有无参构造函数，里面不能有主键。
/// </summary>
/// <example><code>
/// public class Shop
/// {
///     [Column(PrimaryKey = true)] public long Id { get; set; }
///     [ValueObject] public Address? Address { get; set; }                  // address_province、address_city …
///     [ValueObject(Prefix = "geo_")] public GeoPoint Location { get; set; } = new();   // geo_lat、geo_lng
/// }
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValueObjectAttribute : Attribute
{
    /// <summary>列名前缀；为 null 时是"属性的列名_"。</summary>
    public string? Prefix { get; init; }
}
