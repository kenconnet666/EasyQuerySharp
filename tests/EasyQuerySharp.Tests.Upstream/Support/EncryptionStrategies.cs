using System.Globalization;
using System.Text;
using EasyQuerySharp.Encryption;

namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>对应 Java：encryption/Base64EncryptionStrategy（整体 Base64，不支持 LIKE）。</summary>
public sealed class Base64EncryptionStrategy : IEncryptionStrategy
{
    public object? Encrypt(Type entityType, string propertyName, object? plaintext) =>
        plaintext is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(Convert.ToString(plaintext, CultureInfo.InvariantCulture)!));

    public object? Decrypt(Type entityType, string propertyName, object? ciphertext) =>
        ciphertext is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(Convert.ToString(ciphertext, CultureInfo.InvariantCulture)!));
}

/// <summary>对应 Java：encryption/MyEncryptionStrategy。</summary>
public sealed class MyEncryptionStrategy : AesBase64EncryptionStrategy
{
    protected override string Iv => "A-16-Byte-String";

    protected override string Key => "1234567890abcdef";
}

/// <summary>对应 Java：encryption/DefaultAesEasyEncryptionStrategy（测试用的固定密钥）。</summary>
public sealed class DefaultAesEasyEncryptionStrategy : AesBase64EncryptionStrategy
{
    protected override string Iv => "A-16-Byte-String";

    protected override string Key => "abcdef1234567890";
}

/// <summary>对应 Java：encryption/DefaultSafeAesEasyEncryptionStrategy（按完整字符分段，emoji 不被切开）。</summary>
public sealed class DefaultSafeAesEasyEncryptionStrategy : AesBase64EncryptionStrategy
{
    protected override string Iv => "A-16-Byte-String";

    protected override string Key => "abcdef1234567890";

    protected override bool SupportEmoji => true;
}

/// <summary>对应 Java：conversion/MySQLAesEncryptColumnValueSQLConverter（库端 AES 加解密，密钥作为参数）。</summary>
public sealed class MySqlAesEncryptConversion : Metadata.ColumnSqlConversion
{
    public const string Secret = "1234567890123456";

    public override string Read => "AES_DECRYPT(from_base64({0}),{1})";

    public override string Write => "to_base64(AES_ENCRYPT({0},{1}))";

    public override IReadOnlyList<object?> Arguments => [Secret];
}
