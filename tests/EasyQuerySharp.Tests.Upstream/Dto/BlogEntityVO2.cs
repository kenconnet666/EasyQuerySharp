namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：vo/BlogEntityVO2（id 取 Topic 的，其余是 Blog 的属性）。</summary>
public class BlogEntityVO2
{
    public string? Id { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? Url { get; set; }

    public int? Star { get; set; }

    public DateTime? PublishTime { get; set; }

    public decimal? Score { get; set; }

    public int? Status { get; set; }

    public decimal? Order { get; set; }

    public bool? IsTop { get; set; }

    public bool? Top { get; set; }
}
