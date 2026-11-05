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

/// <summary>对应 Java：entity/SysUserSQLEncryption（手机号在库端加解密）。</summary>
[Table("t_sys_user_sql_encryption")]
public class SysUserSQLEncryption
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Username { get; set; }

    [Column(SqlConversion = typeof(MySqlAesEncryptConversion))]
    public string? Phone { get; set; }

    public string? IdCard { get; set; }

    public string? Address { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/SysUserEncrypt（两种库端加解密；Java 地址列的 Java 端加密策略这里用不到，省略）。</summary>
[Table("sys_user_encrypt")]
public class SysUserEncrypt
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column(SqlConversion = typeof(MySqlAesEncryptConversion))]
    public string? Phone { get; set; }

    [Column(SqlConversion = typeof(MySqlAesEncrypt2Conversion))]
    public string? Phone2 { get; set; }

    [Column(DbType = "varchar(4000)")]
    public string? Address { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/SysUserEncrypt2（库里没有这张表）。</summary>
[Table("sys_user2")]
public class SysUserEncrypt2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column(SqlConversion = typeof(MySqlAesEncrypt2Conversion))]
    public string? Phone { get; set; }
}
