namespace EasyQuerySharp.Encryption;

/// <summary>
/// 列加密策略（对应 Java 的 EncryptionStrategy）：写入和作为条件值时加密，读出时解密。
/// 通过 <see cref="EncryptionAttribute"/> 标在属性上；与 Java 版密文兼容的实现见 <see cref="AesBase64EncryptionStrategy"/>。
/// </summary>
public interface IEncryptionStrategy
{
    /// <summary>明文 → 密文；null 原样返回。</summary>
    object? Encrypt(Type entityType, string propertyName, object? plaintext);

    /// <summary>密文 → 明文；null 原样返回。</summary>
    object? Decrypt(Type entityType, string propertyName, object? ciphertext);
}
