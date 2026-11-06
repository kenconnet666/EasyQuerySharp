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

/// <summary>对应 Java fork：entity/ForkProfile（地址列表与扩展字段存成 JSON 列）。</summary>
[Table("t_fork_profile")]
public class ForkProfile
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    [Column(Converter = typeof(JsonValueConverter<List<Address>>))]
    public List<Address>? Addresses { get; set; }

    [Column(Converter = typeof(JsonValueConverter<Dictionary<string, object?>>))]
    public Dictionary<string, object?>? Extra { get; set; }

    /// <summary>地址。</summary>
    public sealed class Address
    {
        public string? City { get; set; }

        public int? Zip { get; set; }
    }
}

/// <summary>对应 Java fork：entity/ForkEvent（Instant 对应 DateTime，OffsetDateTime / ZonedDateTime 对应 DateTimeOffset）。</summary>
[Table("t_fork_event")]
public class ForkEvent
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Kind { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? SeenAt { get; set; }
}
