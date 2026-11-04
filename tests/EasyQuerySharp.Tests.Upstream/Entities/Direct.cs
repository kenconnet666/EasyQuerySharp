using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

// 对应 Java：entity/direct/Direct1 … Direct5：一条五张表的关联链，Direct1 上用直达导航跳到 Direct4 / Direct5。

[Table("direct1")]
public class Direct1
{
    [Column(PrimaryKey = true)]
    public string? C1 { get; set; }

    public string? C2 { get; set; }

    public string? C3 { get; set; }

    public string? C4 { get; set; }

    public string? C5 { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(C1)], TargetProperties = [nameof(Direct2.C7)])]
    public Direct2? Direct2 { get; set; }

    [Navigate(RelationType.ManyToOne, DirectMapping = [nameof(Direct2), nameof(Entities.Direct2.Direct3), nameof(Direct3.Direct4)])]
    public Direct4? Direct4 { get; set; }

    [Navigate(RelationType.ManyToOne, DirectMapping = [nameof(Direct2), nameof(Entities.Direct2.Direct3), nameof(Direct3.Direct4), nameof(Entities.Direct4.Direct5)])]
    public Direct5? Direct5 { get; set; }
}

[Table("direct2")]
public class Direct2
{
    [Column(PrimaryKey = true)]
    public string? C6 { get; set; }

    public string? C7 { get; set; }

    public string? C8 { get; set; }

    public string? C9 { get; set; }

    public string? C10 { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(C8), nameof(C9)], TargetProperties = [nameof(Direct3.C13), nameof(Direct3.C14)])]
    public Direct3? Direct3 { get; set; }
}

[Table("direct3")]
public class Direct3
{
    [Column(PrimaryKey = true)]
    public string? C11 { get; set; }

    public string? C12 { get; set; }

    public string? C13 { get; set; }

    public string? C14 { get; set; }

    public string? C15 { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(C15)], TargetProperties = [nameof(Entities.Direct4.C20)])]
    public Direct4? Direct4 { get; set; }
}

[Table("direct4")]
public class Direct4
{
    [Column(PrimaryKey = true)]
    public string? C16 { get; set; }

    public string? C17 { get; set; }

    public string? C18 { get; set; }

    public string? C19 { get; set; }

    public string? C20 { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(C16), nameof(C18)], TargetProperties = [nameof(Entities.Direct5.C22), nameof(Entities.Direct5.C21)])]
    public Direct5? Direct5 { get; set; }
}

[Table("direct5")]
public class Direct5
{
    [Column(PrimaryKey = true)]
    public string? C21 { get; set; }

    public string? C22 { get; set; }

    public string? C23 { get; set; }

    public string? C24 { get; set; }

    public string? C25 { get; set; }
}
