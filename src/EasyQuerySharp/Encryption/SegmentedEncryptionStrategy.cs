using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasyQuerySharp.Encryption;

/// <summary>
/// 分段加密的公共部分（Java 的 AbstractSupportEmoji… / AbstractUnSupportEmoji… 加密策略）：明文按滑动窗口切成若干段
/// （每段占位长度到 <see cref="EncryptWordMinLength"/> 为止，中文按 2 计），每段单独加密成定长密文再拼接，
/// 于是明文的任意连续片段加密后仍是密文的子串，LIKE 可以直接作用在密文上。解密时按定长切开，除最后一段外只取首字符。
/// 子类给出单段的加解密与密文长度（<see cref="AesBase64EncryptionStrategy"/>、<see cref="Sm4HexEncryptionStrategy"/>）。
/// </summary>
public abstract class SegmentedEncryptionStrategy : IEncryptionStrategy
{
    /// <summary>一段明文加密后的固定长度。</summary>
    protected abstract int SegmentCipherLength { get; }

    /// <summary>加密一段明文。</summary>
    protected abstract string EncryptSegment(string segment);

    /// <summary>解密一段密文。</summary>
    protected abstract string DecryptSegment(string segment);

    /// <summary>每段的占位长度。</summary>
    protected virtual int EncryptWordMinLength => 4;

    /// <summary>中文字符的占位长度。</summary>
    protected virtual int ChineseCharOccupancyLength => 2;

    /// <summary>其它字符的占位长度。</summary>
    protected virtual int OtherCharOccupancyLength => 1;

    /// <summary>解密失败时是否抛异常；false 时原样返回密文。</summary>
    protected virtual bool ThrowIfDecryptFail => true;

    /// <summary>按完整字符（不切开代理对）分段，emoji 不会被截成两半；对应 Java 的 SupportEmoji 版本。</summary>
    protected virtual bool SupportEmoji => false;

    /// <inheritdoc />
    public object? Encrypt(Type entityType, string propertyName, object? plaintext)
    {
        if (plaintext is null) return null;
        var text = Convert.ToString(plaintext, CultureInfo.InvariantCulture)!;
        var segments = Segments(text);
        if (segments.Count == 0) segments.Add(text);
        var sb = new StringBuilder();
        foreach (var segment in segments) sb.Append(EncryptSegment(segment));
        return sb.ToString();
    }

    /// <inheritdoc />
    public object? Decrypt(Type entityType, string propertyName, object? ciphertext)
    {
        if (ciphertext is null) return null;
        var text = Convert.ToString(ciphertext, CultureInfo.InvariantCulture)!;
        try
        {
            if (text.Length % SegmentCipherLength != 0)
                throw new FormatException($"{entityType.Name}.{propertyName} 的密文长度不是 {SegmentCipherLength} 的倍数：{text}");
            var sb = new StringBuilder();
            var count = text.Length / SegmentCipherLength;
            for (var i = 0; i < count; i++)
            {
                var plain = DecryptSegment(text.Substring(i * SegmentCipherLength, SegmentCipherLength));
                // 相邻段只差一个字符：除最后一段外只取首字符
                if (i == count - 1) sb.Append(plain);
                else sb.Append(SupportEmoji && char.IsHighSurrogate(plain[0]) && plain.Length > 1 ? plain[..2] : plain[..Math.Min(1, plain.Length)]);
            }
            return sb.ToString();
        }
        catch (Exception ex) when (!ThrowIfDecryptFail && ex is FormatException or CryptographicException)
        {
            return ciphertext;
        }
    }

    // 滑动窗口分段（Java 的 getStringCharSegments / getStringSafeCharSegments）
    private List<string> Segments(string text)
    {
        var chars = SupportEmoji ? TextElements(text) : text.Select(c => c.ToString()).ToList();
        var segments = new List<string>(chars.Count);
        for (var i = 0; i < chars.Count; i++)
        {
            var length = 0;
            var sb = new StringBuilder();
            var j = i;
            for (; j < chars.Count && length < EncryptWordMinLength; j++)
            {
                var c = chars[j];
                length += c.Length > 1 || IsChinese(c[0]) ? ChineseCharOccupancyLength : OtherCharOccupancyLength;
                sb.Append(c);
            }
            segments.Add(sb.ToString());
            // 已经到末尾且没有超长：后面的窗口都是这一段的后缀，不必再切
            if (j == chars.Count && length <= EncryptWordMinLength) break;
        }
        return segments;
    }

    // 按码点切分（代理对作为一个字符）
    private static List<string> TextElements(string text)
    {
        var result = new List<string>(text.Length);
        foreach (var rune in text.EnumerateRunes()) result.Add(rune.ToString());
        return result;
    }

    // 与 Java 的 isChinese 相同的 Unicode 区块：CJK 统一汉字（含扩展 A）、兼容汉字、CJK 符号、半角全角、通用标点
    private static bool IsChinese(char c) =>
        c is >= '一' and <= '鿿' or >= '㐀' and <= '䶿' or >= '豈' and <= '﫿'
            or >= '　' and <= '〿' or >= '＀' and <= '￯' or >= ' ' and <= '⁯';
}
