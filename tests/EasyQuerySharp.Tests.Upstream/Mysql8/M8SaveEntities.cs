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
