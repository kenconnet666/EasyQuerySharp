using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/m2m/Station（站点经 t_station_tenant 多对多到运营商，集合聚合总是分组合并）。库里没有这些表，只比对 SQL。</summary>
[Table("t_station")]
public class Station
{
    public string? StationId { get; set; }

    public string? StationName { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(StationId)], Mapping = typeof(StationTenant),
        SelfMappingProperties = [nameof(StationTenant.StationId)], TargetProperties = [nameof(TenantOperator.TenantOperatorId)],
        TargetMappingProperties = [nameof(StationTenant.TenantId)], GroupJoin = true)]
    public List<TenantOperator>? Operators { get; set; }
}

/// <summary>对应 Java：entity/m2m/StationTenant（中间表）。</summary>
[Table("t_station_tenant")]
public class StationTenant
{
    public string? StationId { get; set; }

    public string? TenantId { get; set; }
}

/// <summary>对应 Java：entity/m2m/TenantOperator。</summary>
[Table("t_tenant_operator")]
public class TenantOperator
{
    public string? TenantOperatorId { get; set; }

    public string? TenantOperatorName { get; set; }
}
