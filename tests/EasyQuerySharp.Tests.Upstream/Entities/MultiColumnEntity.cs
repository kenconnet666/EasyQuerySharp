using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/MultiColumnEntity（十个字符串列，用于动态表名）。库里没有这张表，只比对 SQL。</summary>
[Table("multi")]
public class MultiColumnEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Col1 { get; set; }

    public string? Col2 { get; set; }

    public string? Col3 { get; set; }

    public string? Col4 { get; set; }

    public string? Col5 { get; set; }

    public string? Col6 { get; set; }

    public string? Col7 { get; set; }

    public string? Col8 { get; set; }

    public string? Col9 { get; set; }

    public string? Col10 { get; set; }
}
