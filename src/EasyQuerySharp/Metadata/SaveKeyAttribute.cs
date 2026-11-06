namespace EasyQuerySharp.Metadata;

/// <summary>
/// 聚合保存时匹配子对象用的业务键（对应 Java 的 @SaveKey）：子对象主键为空时，按标了它的属性与原集合里的对象配对，
/// 配上的按差异更新，而不是删掉旧的再插入新的。
/// </summary>
/// <example><code>[SaveKey] public string? Sku { get; set; }</code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SaveKeyAttribute : Attribute;
