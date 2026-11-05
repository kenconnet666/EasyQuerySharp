using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java fork：entity/ForkPost。</summary>
[Table("t_fork_post")]
public class ForkPost
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? Title { get; set; }

    public decimal? Amount { get; set; }
}

/// <summary>对应 Java fork：entity/ForkUser（公司导航随用到的用例再补）。</summary>
[Table("t_fork_user")]
public class ForkUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public string? CompanyId { get; set; }

    public string? Attrs { get; set; }

    public DateTime? CreatedAt { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ForkPost.UserId)])]
    public List<ForkPost>? Posts { get; set; }
}
