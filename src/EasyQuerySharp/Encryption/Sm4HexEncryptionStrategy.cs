using System.Text;

namespace EasyQuerySharp.Encryption;

/// <summary>
/// 国密 SM4（ECB + PKCS7）+ 十六进制的分段加密，与 Java 的 AbstractSupportEmojiSm4HexEncryptionStrategy 密文一致：
/// 每段加密成 32 个十六进制字符，按完整字符分段（emoji 不会被截开）；分段规则见 <see cref="SegmentedEncryptionStrategy"/>。
/// </summary>
/// <example><code>
/// public sealed class MySm4 : Sm4HexEncryptionStrategy { protected override string Key =&gt; "sm4key1234567890"; }
/// [Encryption(typeof(MySm4), SupportQueryLike = true)] public string? Phone { get; set; }
/// </code></example>
public abstract class Sm4HexEncryptionStrategy : SegmentedEncryptionStrategy
{
    /// <summary>16 字节的密钥（UTF-8）。</summary>
    protected abstract string Key { get; }

    /// <inheritdoc />
    protected override int SegmentCipherLength => 32;

    /// <inheritdoc />
    protected override bool SupportEmoji => true;

    /// <inheritdoc />
    protected override string EncryptSegment(string segment) => Sm4.EncryptToHex(segment, Encoding.UTF8.GetBytes(Key));

    /// <inheritdoc />
    protected override string DecryptSegment(string segment) => Sm4.DecryptFromHex(segment, Encoding.UTF8.GetBytes(Key));
}
