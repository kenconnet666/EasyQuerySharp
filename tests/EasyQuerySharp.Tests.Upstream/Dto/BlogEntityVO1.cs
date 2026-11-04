using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：vo/BlogEntityVO1（status 映射到列别名 status1）。</summary>
public class BlogEntityVO1
{
    public decimal? Score { get; set; }

    [Column("status1")]
    public int? Status { get; set; }

    public decimal? Order { get; set; }

    public bool? IsTop { get; set; }

    public bool? Top { get; set; }
}
