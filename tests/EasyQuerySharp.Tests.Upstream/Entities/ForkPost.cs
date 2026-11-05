using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java fork：entity/ForkPost（只用到这几列）。</summary>
[Table("t_fork_post")]
public class ForkPost
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? Title { get; set; }

    public decimal? Amount { get; set; }
}
