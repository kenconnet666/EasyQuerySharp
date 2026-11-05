using EasyQuerySharp.Encryption;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/SysUserEncryption。</summary>
[Table("t_sys_user_encryption")]
public class SysUserEncryption
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Encryption(typeof(Base64EncryptionStrategy))]
    public string? PhoneNotSupportLike { get; set; }

    [Encryption(typeof(Base64EncryptionStrategy))]
    public string? AddressNotSupportLike { get; set; }

    [Encryption(typeof(MyEncryptionStrategy), SupportQueryLike = true)]
    public string? PhoneSupportLike { get; set; }

    [Encryption(typeof(MyEncryptionStrategy), SupportQueryLike = true)]
    public string? AddressSupportLike { get; set; }
}
