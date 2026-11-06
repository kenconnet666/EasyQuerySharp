namespace EasyQuerySharp.Metadata;

/// <summary>
/// 这个枚举在所有实体里都按名字存成字符串（对应 Java 的 @Enumerated），不必在每个属性上写 <see cref="EnumNameConverter{TEnum}"/>；
/// 属性上显式声明了转换器时以属性上的为准。没有标注的枚举按整数存。
/// </summary>
/// <example><code>
/// [StoreAsName] public enum NamedEnum { User, Teacher, Book }
/// public NamedEnum? Type { get; set; }   // 库里存 "Book"
/// </code></example>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class StoreAsNameAttribute : Attribute;
