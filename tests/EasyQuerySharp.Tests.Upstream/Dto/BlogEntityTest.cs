using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：dto/BlogEntityTest。</summary>
public class BlogEntityTest
{
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

/// <summary>对应 Java：dto/BlogEntityTest2（url 映射到列别名 my_url）。</summary>
public class BlogEntityTest2
{
    public string? Title { get; set; }

    public string? Content { get; set; }

    [Column("my_url")]
    public string? Url { get; set; }

    public int? Star { get; set; }

    public DateTime? PublishTime { get; set; }

    public decimal? Score { get; set; }

    public int? Status { get; set; }

    public decimal? Order { get; set; }

    public bool? IsTop { get; set; }

    public bool? Top { get; set; }
}
