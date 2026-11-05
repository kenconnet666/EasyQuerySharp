using EasyQuerySharp.Metadata;

// 与 Entities 里的 SysUser、UserBook 同名（Java 不同包），单独一个命名空间区分；文件仍放在 Entities 下，不再多建一层目录
// ReSharper disable once CheckNamespace
namespace EasyQuerySharp.Tests.Upstream.Entities.BlogTest;

// 对应 Java：entity/blogtest 包（用户、角色、菜单、公司、地址、书）。Java 的测试库里没有这些表（t_user 只有 id、name 两列），
// 用到它们的 Java 用例执行失败后只比对 SQL，C# 这边也只比对 SQL。

/// <summary>对应 Java：entity/blogtest/SysUser。</summary>
[Table("t_user")]
public class SysUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? CompanyId { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(SysUserAddress.UserId)])]
    public SysUserAddress? Address { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(UserRole), SelfMappingProperties = [nameof(UserRole.UserId)],
        TargetMappingProperties = [nameof(UserRole.RoleId)])]
    public List<SysRole>? Roles { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(UserBook.UserId)])]
    public List<UserBook>? Books { get; set; }
}

/// <summary>对应 Java：entity/blogtest/SysRole。</summary>
[Table("t_role")]
public class SysRole
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(UserRole), SelfMappingProperties = [nameof(UserRole.RoleId)],
        TargetMappingProperties = [nameof(UserRole.UserId)])]
    public List<SysUser>? Users { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(RoleMenu), SelfMappingProperties = [nameof(RoleMenu.RoleId)],
        TargetMappingProperties = [nameof(RoleMenu.MenuId)])]
    public List<SysMenu>? Menus { get; set; }
}

/// <summary>对应 Java：entity/blogtest/UserRole（用户与角色的中间表）。</summary>
[Table("t_user_role")]
public class UserRole
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? RoleId { get; set; }
}

/// <summary>对应 Java：entity/blogtest/SysMenu。</summary>
[Table("t_menu")]
public class SysMenu
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Route { get; set; }

    public string? Icon { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(RoleMenu), SelfMappingProperties = [nameof(RoleMenu.MenuId)],
        TargetMappingProperties = [nameof(RoleMenu.RoleId)])]
    public List<SysRole>? Roles { get; set; }
}

/// <summary>对应 Java：entity/blogtest/RoleMenu（角色与菜单的中间表）。</summary>
[Table("t_role_menu")]
public class RoleMenu
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RoleId { get; set; }

    public string? MenuId { get; set; }
}

/// <summary>对应 Java：entity/blogtest/Company。</summary>
[Table("t_company")]
public class Company
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SysUser.CompanyId)])]
    public List<SysUser>? Users { get; set; }
}

/// <summary>对应 Java：entity/blogtest/SysUserAddress。</summary>
[Table("t_user_address")]
public class SysUserAddress
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? Province { get; set; }

    public string? City { get; set; }

    public string? Area { get; set; }

    public string? Addr { get; set; }
}

/// <summary>对应 Java：entity/blogtest/UserBook。</summary>
[Table("t_user_book")]
public class UserBook
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    public string? Author { get; set; }
}
