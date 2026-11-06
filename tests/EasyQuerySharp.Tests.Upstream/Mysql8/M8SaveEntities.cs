using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// 对应 Java：mysql8/entity/save 下的聚合保存模型。根 → 一对一、一对多（一对多的子对象再一对一）、经中间表的多对多；全部级联 Delete。

/// <summary>主键由 <see cref="SaveInterceptor"/> 在插入时补上的实体（对应 Java SaveInterceptor.apply 列出的类型）。</summary>
public interface IM8SaveEntity
{
    string? Id { get; set; }
}

/// <summary>对应 Java：mysql8/entity/save/SaveInterceptor（插入时主键为空就填 UUID）。</summary>
public sealed class SaveInterceptor : EntityInterceptor<IM8SaveEntity>
{
    public override void BeforeInsert(IM8SaveEntity entity) => entity.Id ??= Guid.NewGuid().ToString();
}

[Table("m8_save_root")]
public class M8SaveRoot : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Code { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8SaveRootOne.RootId)], Cascade = Cascade.Delete)]
    public M8SaveRootOne? M8SaveRootOne { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8SaveRootOne2.RootId)], Cascade = Cascade.Delete)]
    public M8SaveRootOne2? M8SaveRootOne2 { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8SaveRootMany.RootId)], Cascade = Cascade.Delete)]
    public List<M8SaveRootMany>? M8SaveRootManyList { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8SaveRootMiddleMany.RootId)], Mapping = typeof(M8SaveRootMiddleMany),
        TargetProperties = [nameof(M8SaveRoot2Many.Id)], TargetMappingProperties = [nameof(M8SaveRootMiddleMany.ManyId)], Cascade = Cascade.Delete)]
    public List<M8SaveRoot2Many>? M8SaveRoot2ManyList { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8SaveRootMiddleMany.RootId)], Cascade = Cascade.Delete)]
    public List<M8SaveRootMiddleMany>? M8SaveRootMiddleManyList { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8SaveRootMiddleMany2.RootId)], Cascade = Cascade.Delete)]
    public List<M8SaveRootMiddleMany2>? M8SaveRootMiddleManyList2 { get; set; }
}

[Table("m8_save_root_one")]
public class M8SaveRootOne : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RootId { get; set; }

    public string? Name { get; set; }
}

[Table("m8_save_root_one2")]
public class M8SaveRootOne2 : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RootId { get; set; }

    public string? Name { get; set; }
}

[Table("m8_save_root_many")]
public class M8SaveRootMany : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RootId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Mysql8.M8SaveRootManyOne.RootManyId)], Cascade = Cascade.Delete)]
    public M8SaveRootManyOne? M8SaveRootManyOne { get; set; }
}

[Table("m8_save_root_many_one")]
public class M8SaveRootManyOne : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RootManyId { get; set; }

    public string? Name { get; set; }
}

[Table("m8_save_root_2many")]
public class M8SaveRoot2Many : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }
}

[Table("m8_save_root_middle_many")]
public class M8SaveRootMiddleMany : IM8SaveEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RootId { get; set; }

    public string? ManyId { get; set; }
}

/// <summary>同一张中间表，按 (root_id, many_id) 业务键匹配。</summary>
[Table("m8_save_root_middle_many")]
public class M8SaveRootMiddleMany2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    [SaveKey]
    public string? RootId { get; set; }

    [SaveKey]
    public string? ManyId { get; set; }
}

[Table("m8_save_b")]
public class M8SaveB
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Mysql8.M8SaveC.Pid)], Cascade = Cascade.Delete)]
    public M8SaveC? M8SaveC { get; set; }
}

[Table("m8_save_c")]
public class M8SaveC
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Mysql8.M8SaveD.Pid)], Cascade = Cascade.Delete)]
    public M8SaveD? M8SaveD { get; set; }
}

[Table("m8_save_d")]
public class M8SaveD
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8User3（多对多角色，级联默认：不维护中间表）。</summary>
[Table("m8_user3")]
public class M8User3
{
    [Column(PrimaryKey = true)]
    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public int? UserAge { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(UserId)], SelfMappingProperties = [nameof(M8UserRole3.UserId)], Mapping = typeof(M8UserRole3),
        TargetProperties = [nameof(M8Role3.RoleId)], TargetMappingProperties = [nameof(M8UserRole3.RoleId)])]
    public List<M8Role3>? Roles { get; set; }
}

[Table("m8_role3")]
public class M8Role3
{
    [Column(PrimaryKey = true)]
    public string? RoleId { get; set; }

    public string? RoleName { get; set; }
}

[Table("m8_user_role3")]
public class M8UserRole3
{
    [Column(PrimaryKey = true)]
    public string? UserRoleId { get; set; }

    public string? UserId { get; set; }

    public string? RoleId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/save/M8ToMany1（一对多再一对多，全部级联 Delete）。</summary>
[Table("m8_many1")]
public class M8ToMany1
{
    [Column(PrimaryKey = true)]
    public string? A { get; set; }

    public string? B { get; set; }

    public string? C { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(A)], TargetProperties = [nameof(M8ToMany2.A)], Cascade = Cascade.Delete)]
    public List<M8ToMany2>? M8ToMany2List { get; set; }
}

[Table("m8_many2")]
public class M8ToMany2
{
    [Column(PrimaryKey = true)]
    public string? D { get; set; }

    public string? A { get; set; }

    public string? E { get; set; }

    public string? F { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(D)], TargetProperties = [nameof(M8ToMany3.D)], Cascade = Cascade.Delete)]
    public List<M8ToMany3>? M8ToMany3List { get; set; }
}

[Table("m8_many3")]
public class M8ToMany3
{
    [Column(PrimaryKey = true)]
    public string? G { get; set; }

    public string? D { get; set; }

    public string? H { get; set; }

    public string? I { get; set; }
}
