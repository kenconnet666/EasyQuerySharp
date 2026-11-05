using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：navigateflat/MyUserHome（表 my_user，没有主键）。</summary>
[Table("my_user")]
public class MyUserHome
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }
}

/// <summary>对应 Java：navigateflat/MyUserHome2（多级 schema <c>a.dbo</c>，各级分别加引号）。库里没有这个 schema，只比对 SQL。</summary>
[Table("my_user", Schema = "a.dbo")]
public class MyUserHome2
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }
}

/// <summary>对应 Java：entity/MyCategory（自关联的一对多）。</summary>
[Table("category")]
public class MyCategory
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ParentId)])]
    public List<MyCategory>? Children { get; set; }
}

/// <summary>对应 Java：entity/MyCategoryVO。</summary>
public class MyCategoryVO
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public long? Count { get; set; }
}

/// <summary>对应 Java：vo/MyUnion（原生 SQL 查询的结果，一列 task_status）。</summary>
public class MyUnion
{
    public string? TaskStatus { get; set; }
}
