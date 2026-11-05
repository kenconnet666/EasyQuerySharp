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

/// <summary>对应 Java：mysql8/entity/many/M8Province（省 → 市 → 区的三层一对多）。</summary>
[Table("m8_province")]
public class M8Province
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8City.Pid)])]
    public List<M8City>? Cities { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8City。</summary>
[Table("m8_city")]
public class M8City
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8Area.Cid)])]
    public List<M8Area>? Areas { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8Area.Cid)])]
    public List<M8Area>? Areas2 { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8Area。</summary>
[Table("m8_area")]
public class M8Area
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Cid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8AreaBuild.Aid)])]
    public List<M8AreaBuild>? Builds { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8AreaBuild。</summary>
[Table("m8_build")]
public class M8AreaBuild
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Aid { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Parent（一对多与经中间表的多对多子项，order 是关键字列）。</summary>
[Table("m8_parent")]
public class M8Parent
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Order { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8Child.ParentId)], OrderBy = nameof(M8Child.Order))]
    public List<M8Child>? Children { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8ParentChild.ParentId)], Mapping = typeof(M8ParentChild),
        TargetProperties = [nameof(M8Child.Id)], TargetMappingProperties = [nameof(M8ParentChild.ChildId)], OrderBy = nameof(M8Child.Order))]
    public List<M8Child>? Children2 { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Child。</summary>
[Table("m8_child")]
public class M8Child
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public int? Order { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8ParentChild。</summary>
[Table("m8_parent_child")]
public class M8ParentChild
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? ChildId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/BatchInsert（自增主键）。</summary>
[Table("m8_batch")]
public class BatchInsert
{
    [Column(PrimaryKey = true, Generated = true)]
    public long? Id { get; set; }

    public string? Name { get; set; }
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
