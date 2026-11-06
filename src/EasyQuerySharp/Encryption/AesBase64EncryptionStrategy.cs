using System.Security.Cryptography;
using System.Text;

namespace EasyQuerySharp.Encryption;

/// <summary>
/// AES/CBC/PKCS7 + Base64 的分段加密，与 Java 的 AbstractUnSupportEmojiAesBase64EncryptionStrategy（
/// <see cref="SegmentedEncryptionStrategy.SupportEmoji"/> 为 true 时对应 AbstractSupportEmojiAesBase64EncryptionStrategy）密文逐字节一致。
/// 每段加密成 24 个 Base64 字符；分段规则见 <see cref="SegmentedEncryptionStrategy"/>。
/// </summary>
public abstract class AesBase64EncryptionStrategy : SegmentedEncryptionStrategy
{
    /// <summary>16 / 24 / 32 字节的密钥（UTF-8）。</summary>
    protected abstract string Key { get; }

    /// <summary>16 字节的初始向量（UTF-8）。</summary>
    protected abstract string Iv { get; }

    /// <inheritdoc />
    protected override int SegmentCipherLength => 24;

    /// <inheritdoc />
    protected override string EncryptSegment(string segment)
    {
        using var aes = CreateAes();
        return Convert.ToBase64String(aes.EncryptCbc(Encoding.UTF8.GetBytes(segment), Encoding.UTF8.GetBytes(Iv), PaddingMode.PKCS7));
    }

    /// <inheritdoc />
    protected override string DecryptSegment(string segment)
    {
        using var aes = CreateAes();
        return Encoding.UTF8.GetString(aes.DecryptCbc(Convert.FromBase64String(segment), Encoding.UTF8.GetBytes(Iv), PaddingMode.PKCS7));
    }

    private Aes CreateAes()
    {
        var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(Key);
        return aes;
    }
}
