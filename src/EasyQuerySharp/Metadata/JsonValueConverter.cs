using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 把对象属性按 JSON 文本存成一列（对应 Java 的 JsonConverter）。属性名用 camelCase，与 Java 的 Jackson / fastjson 默认一致。
/// 读出时文本与 UTF-8 字节（二进制 / BLOB 列）都能解码，空白文本读成 null；按属性声明的类型（含泛型集合）解码。
/// 用法：<c>[Column(Converter = typeof(JsonValueConverter&lt;Address&gt;))] public Address? Address { get; set; }</c>。
/// 裁剪 / Native AOT 下用源生成的 JSON 元数据：写一个子类，把 <see cref="JsonTypeInfo{T}"/> 传给受保护的构造函数，
/// 如 <c>sealed class AddressJson() : JsonValueConverter&lt;Address&gt;(AppJson.Default.Address);</c>
/// （<c>AppJson</c> 是标了 <c>[JsonSerializable(typeof(Address))]</c>、<c>[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]</c> 的 <c>JsonSerializerContext</c>）。
/// </summary>
/// <typeparam name="T">属性的类型。</typeparam>
public class JsonValueConverter<T> : ValueConverter<T, string>, IValueConverter
{
    private const string ReflectionJson = "JSON 序列化依赖反射；裁剪 / Native AOT 下继承本类，把源生成的 JsonTypeInfo<T> 传给受保护的构造函数";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly JsonTypeInfo<T>? _typeInfo;

    /// <summary>按反射序列化（属性名 camelCase）。</summary>
    [RequiresUnreferencedCode(ReflectionJson)]
    [RequiresDynamicCode(ReflectionJson)]
    public JsonValueConverter()
    {
    }

    /// <summary>按源生成的 JSON 元数据序列化，裁剪 / Native AOT 下可用；命名规则由生成它的 <c>JsonSerializerContext</c> 决定。</summary>
    protected JsonValueConverter(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        _typeInfo = typeInfo;
    }

    /// <inheritdoc />
    protected override string ToProvider(T value) => _typeInfo is null ? SerializeByReflection(value) : JsonSerializer.Serialize(value, _typeInfo);

    /// <inheritdoc />
    protected override T FromProvider(string value) => (_typeInfo is null ? DeserializeByReflection(value) : JsonSerializer.Deserialize(value, _typeInfo))!;

    object? IValueConverter.FromProvider(object? value) => value switch
    {
        null => null,
        byte[] bytes => Decode(Encoding.UTF8.GetString(bytes)),
        _ => Decode(value as string ?? value.ToString()!),
    };

    private object? Decode(string text) => string.IsNullOrWhiteSpace(text) ? null : FromProvider(text);

    // 只有标了 RequiresUnreferencedCode / RequiresDynamicCode 的无参构造函数会留下 _typeInfo 为 null，调用方已经收到过警告
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "只经由标注过的无参构造函数到达")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "只经由标注过的无参构造函数到达")]
    private static string SerializeByReflection(T value) => JsonSerializer.Serialize(value, Options);

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "只经由标注过的无参构造函数到达")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "只经由标注过的无参构造函数到达")]
    private static T? DeserializeByReflection(string value) => JsonSerializer.Deserialize<T>(value, Options);
}
