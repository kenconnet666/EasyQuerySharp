using System.Diagnostics.CodeAnalysis;

namespace EasyQuerySharp.Encryption;

/// <summary>
/// 这一列加密存储（对应 Java 的 @Encryption）：<c>[Encryption(typeof(MyAes), SupportQueryLike = true)]</c>。
/// 等值条件的值整体加密后比较；<see cref="SupportQueryLike"/> 为 true 时 LIKE 的值也加密（需要策略按片段加密，
/// 如 <see cref="AesBase64EncryptionStrategy"/>），否则 LIKE 用明文、匹配不到密文。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptionAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type strategy) : Attribute
{
    /// <summary>实现 <see cref="IEncryptionStrategy"/> 且有无参构造函数的类型；同一类型全局共享一个实例。</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type Strategy { get; } = strategy;

    /// <summary>LIKE 条件的值是否也加密。</summary>
    public bool SupportQueryLike { get; init; }
}
