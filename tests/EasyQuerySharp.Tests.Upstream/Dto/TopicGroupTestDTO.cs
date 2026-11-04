namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：dto/TopicGroupTestDTO。</summary>
public class TopicGroupTestDTO
{
    public string? Id { get; set; }

    public int? IdCount { get; set; }
}
/// <summary>对应 Java：dto/BlogEntityGroup（分组统计的结果）。</summary>
public class BlogEntityGroup
{
    public string? Id { get; set; }

    public int? IdCount { get; set; }

    public string? Title { get; set; }

    public int? TitleLength { get; set; }

    public int? StarSum { get; set; }

    public DateTime? PublishTimeMax { get; set; }

    public decimal? ScoreSum { get; set; }

    public int? StatusAvg { get; set; }

    public decimal? OrderMin { get; set; }
}
