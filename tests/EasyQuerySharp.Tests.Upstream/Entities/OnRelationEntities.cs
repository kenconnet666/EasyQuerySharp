using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/onrelation/OnRelationA（一对一 → B → C 的链，用于 JOIN 的 ON 里引用导航）。表由固件按代码优先创建。</summary>
[Table("on_relation_a")]
public class OnRelationA
{
    [Column(PrimaryKey = true)]
    public string? Aid { get; set; }

    public string? Aname { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Aid)], TargetProperties = [nameof(OnRelationB.Aid)])]
    public OnRelationB? OnRelationB { get; set; }
}

/// <summary>对应 Java：entity/onrelation/OnRelationB。</summary>
[Table("on_relation_b")]
public class OnRelationB
{
    [Column(PrimaryKey = true)]
    public string? Bid { get; set; }

    public string? Aid { get; set; }

    public string? Bname { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Bid)], TargetProperties = [nameof(OnRelationC.Bid)])]
    public OnRelationC? OnRelationC { get; set; }
}

/// <summary>对应 Java：entity/onrelation/OnRelationC。</summary>
[Table("on_relation_c")]
public class OnRelationC
{
    [Column(PrimaryKey = true)]
    public string? Cid { get; set; }

    public string? Bid { get; set; }

    public string? Cname { get; set; }
}

/// <summary>对应 Java：entity/onrelation/OnRelationD。</summary>
[Table("on_relation_d")]
public class OnRelationD
{
    [Column(PrimaryKey = true)]
    public string? Did { get; set; }

    public string? Dname { get; set; }
}
