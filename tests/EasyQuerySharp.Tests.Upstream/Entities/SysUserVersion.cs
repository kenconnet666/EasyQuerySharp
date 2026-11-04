using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/SysUserVersionLong（long 版本列，VersionLongStrategy）。</summary>
[Table("t_sys_user_version")]
public class SysUserVersionLong
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Username { get; set; }

    public string? Phone { get; set; }

    public string? IdCard { get; set; }

    public string? Address { get; set; }

    public DateTime? CreateTime { get; set; }

    [Version]
    public long? Version { get; set; }
}

/// <summary>对应 Java：entity/SysUserVersionLongLogicDel（版本列 + 布尔逻辑删除）。</summary>
[Table("t_sys_user_version_del")]
public class SysUserVersionLongLogicDel
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Username { get; set; }

    public string? Phone { get; set; }

    public string? IdCard { get; set; }

    public string? Address { get; set; }

    public DateTime? CreateTime { get; set; }

    [Version]
    public long? Version { get; set; }

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    public bool? Deleted { get; set; }
}
