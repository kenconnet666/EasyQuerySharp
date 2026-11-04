using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Encryption;

/// <summary>把加密策略接到值转换的管线上：写入、条件值经 <see cref="ToProvider"/> 加密，读出经 <see cref="FromProvider"/> 解密。</summary>
internal sealed class EncryptionConverter(IEncryptionStrategy strategy, Type entityType, string propertyName, bool supportQueryLike) : IValueConverter
{
    /// <summary>LIKE 的值是否加密。</summary>
    public bool SupportQueryLike => supportQueryLike;

    /// <inheritdoc />
    public Type ModelType => typeof(string);

    /// <inheritdoc />
    public Type ProviderType => typeof(string);

    /// <inheritdoc />
    public object? ToProvider(object? value) => strategy.Encrypt(entityType, propertyName, value);

    /// <inheritdoc />
    public object? FromProvider(object? value) => strategy.Decrypt(entityType, propertyName, value);
}
