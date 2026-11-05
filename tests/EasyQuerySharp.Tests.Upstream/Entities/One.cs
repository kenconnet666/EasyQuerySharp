using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

// 对应 Java：entity/one/One1、One2（关联键是空字符串时照常按值匹配，不当成 null）

[Table("one1")]
public class One1
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Oid { get; set; }

    public string? Oname { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Oid)], TargetProperties = [nameof(One2.Oid)])]
    public One2? One2 { get; set; }
}

[Table("one2")]
public class One2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Oid { get; set; }

    public string? Oname { get; set; }
}
