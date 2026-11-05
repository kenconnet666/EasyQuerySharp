using System.Reflection;
using System.Runtime.Serialization;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 枚举按名字存成字符串（不写转换器时按整数存）。成员上有 <see cref="EnumMemberAttribute"/> 时用它的 Value，
/// 这样库里的值可以和成员名不同（如成员 <c>Senior</c> 存成 <c>senior</c>）。
/// </summary>
/// <example><code>
/// public enum Level { [EnumMember(Value = "junior")] Junior, [EnumMember(Value = "senior")] Senior }
/// [Column(Converter = typeof(EnumNameConverter&lt;Level&gt;))] public Level Level { get; set; }
/// </code></example>
public sealed class EnumNameConverter<TEnum> : IValueConverter where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Names = Enum.GetValues<TEnum>().Distinct().ToDictionary(v => v, NameOf);
    private static readonly Dictionary<string, TEnum> Values = Names.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    /// <inheritdoc />
    public Type ModelType => typeof(TEnum);

    /// <inheritdoc />
    public Type ProviderType => typeof(string);

    // 查询里的常量可能已经被换成底层整数，Enum.ToObject 两种都接受
    /// <inheritdoc />
    public object? ToProvider(object? value) => value is null ? null : Names[(TEnum)Enum.ToObject(typeof(TEnum), value)];

    /// <inheritdoc />
    public object? FromProvider(object? value) =>
        value is null ? null
        : Values.TryGetValue((string)value, out var parsed) ? parsed
        : throw new InvalidOperationException($"库里的值 '{value}' 不是 {typeof(TEnum).Name} 的成员");

    private static string NameOf(TEnum value)
    {
        var name = value.ToString();
        return typeof(TEnum).GetField(name)?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? name;
    }
}
