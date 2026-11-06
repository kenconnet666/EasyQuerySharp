using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

// 对应 Java：entity/tolistflat（A、B、C 两两多对多，库里没有这几张表，用例只验证执行到 SQL 为止）

[Table("test_a")]
public class TestA
{
    [Column(PrimaryKey = true)]
    public string? Aid { get; set; }

    public string? Aname { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Aid)], Mapping = typeof(TestAB), SelfMappingProperties = [nameof(TestAB.Aid)],
        TargetProperties = [nameof(TestB.Bid)], TargetMappingProperties = [nameof(TestAB.Bid)], GroupJoin = true)]
    public List<TestB>? BList { get; set; }
}

[Table("test_ab")]
public class TestAB
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Aid { get; set; }

    public string? Bid { get; set; }
}

[Table("test_b")]
public class TestB
{
    [Column(PrimaryKey = true)]
    public string? Bid { get; set; }

    public string? Bname { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Bid)], Mapping = typeof(TestBC), SelfMappingProperties = [nameof(TestBC.Bid)],
        TargetProperties = [nameof(Testc.Cid)], TargetMappingProperties = [nameof(TestBC.Cid)], GroupJoin = true)]
    public List<Testc>? CList { get; set; }
}

[Table("test_bc")]
public class TestBC
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Bid { get; set; }

    public string? Cid { get; set; }
}

[Table("test_c")]
public class Testc
{
    [Column(PrimaryKey = true)]
    public string? Cid { get; set; }

    public string? Cname { get; set; }
}
