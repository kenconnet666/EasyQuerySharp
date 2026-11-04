using EasyQuerySharp.Encryption;

namespace EasyQuerySharp.Tests.Upstream.Encryption;

/// <summary>对应 Java：encryption/DefaultAesEasyEncryptionStrategy（测试用的固定密钥）。</summary>
public sealed class DefaultAesEasyEncryptionStrategy : AesBase64EncryptionStrategy
{
    protected override string Iv => "A-16-Byte-String";

    protected override string Key => "abcdef1234567890";
}
