using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 把对象属性按 JSON 文本存成一列（对应 Java 的 JsonConverter）。属性名用 camelCase，与 Java 的 Jackson / fastjson 默认一致。
/// 用法：<c>[Column(Converter = typeof(JsonValueConverter&lt;Address&gt;))] public Address? Address { get; set; }</c>。
/// </summary>
/// <typeparam name="T">属性的类型。</typeparam>
[RequiresUnreferencedCode("JSON 序列化依赖反射；裁剪 / AOT 场景请自定义基于源生成的转换器")]
[RequiresDynamicCode("JSON 序列化依赖反射；裁剪 / AOT 场景请自定义基于源生成的转换器")]
public class JsonValueConverter<T> : ValueConverter<T, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    protected override string ToProvider(T value) => JsonSerializer.Serialize(value, Options);

    /// <inheritdoc />
    protected override T FromProvider(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}
