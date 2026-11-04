using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：MyVO（分组结果）。</summary>
public sealed class MyVO
{
    public string? Title { get; set; }

    public string? Content { get; set; }

    public long? C { get; set; }
}

/// <summary>对应 Java：selectato/TopicAutoFalse（Alias 不自动查询）。</summary>
public sealed class TopicAutoFalse
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }

    [Column(AutoSelect = false)]
    public string? Alias { get; set; }
}
