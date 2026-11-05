namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：dto/TopicType1VO（四个字符串列，用于投影后再按投影列过滤）。</summary>
public class TopicType1VO
{
    public string? Column1 { get; set; }

    public string? Column2 { get; set; }

    public string? Column3 { get; set; }

    public string? Column4 { get; set; }
}
