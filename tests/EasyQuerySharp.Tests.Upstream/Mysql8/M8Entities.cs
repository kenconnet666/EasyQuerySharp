using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/entity/M8User（经 m8_user_role 多对多角色）。</summary>
[Table("m8_user")]
public class M8User
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8UserRole.UserId)], Mapping = typeof(M8UserRole),
        TargetProperties = [nameof(M8Role.Id)], TargetMappingProperties = [nameof(M8UserRole.RoleId)])]
    public List<M8Role>? Roles { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8User2（主键不叫 id 的多对多与一对多）。</summary>
[Table("m8_user2")]
public class M8User2
{
    [Column(PrimaryKey = true)]
    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public int? UserAge { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(UserId)], SelfMappingProperties = [nameof(M8UserRole2.UserId)], Mapping = typeof(M8UserRole2),
        TargetProperties = [nameof(M8Role2.RoleId)], TargetMappingProperties = [nameof(M8UserRole2.RoleId)])]
    public List<M8Role2>? Roles { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(UserId)], TargetProperties = [nameof(M8UserBook.UserId)])]
    public List<M8UserBook>? Books { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Role（经 m8_role_menu 多对多菜单）。</summary>
[Table("m8_role")]
public class M8Role
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8RoleMenu.RoleId)], Mapping = typeof(M8RoleMenu),
        TargetProperties = [nameof(M8Menu.Id)], TargetMappingProperties = [nameof(M8RoleMenu.MenuId)])]
    public List<M8Menu>? Menus { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Role2。</summary>
[Table("m8_role2")]
public class M8Role2
{
    [Column(PrimaryKey = true)]
    public string? RoleId { get; set; }

    public string? RoleName { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Menu。</summary>
[Table("m8_menu")]
public class M8Menu
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Path { get; set; }

    public string? OwnerId { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(OwnerId)], TargetProperties = [nameof(M8MenuOwner.Id)])]
    public M8MenuOwner? MenuOwner { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8MenuOwner（与 M8Menu 同表，只映射 id、name）。</summary>
[Table("m8_menu")]
public class M8MenuOwner
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8RoleMenu。</summary>
[Table("m8_role_menu")]
public class M8RoleMenu
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RoleId { get; set; }

    public string? MenuId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserBook。</summary>
[Table("m8_user_book")]
public class M8UserBook
{
    [Column(PrimaryKey = true)]
    public string? BookId { get; set; }

    public string? UserId { get; set; }

    public string? BookName { get; set; }

    public decimal? BookPrice { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserRole。</summary>
[Table("m8_user_role")]
public class M8UserRole
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? RoleId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserRole2。</summary>
[Table("m8_user_role2")]
public class M8UserRole2
{
    [Column(PrimaryKey = true)]
    public string? UserRoleId { get; set; }

    public string? UserId { get; set; }

    public string? RoleId { get; set; }
}
