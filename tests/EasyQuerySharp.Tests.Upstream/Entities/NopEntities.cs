using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：nop/MyObject（表名就是类名；对一父对象，父对象下有子集合，子下还有子集合）。库里没有这些表，只比对 SQL。</summary>
[Table("MyObject")]
public class MyObject
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? F1 { get; set; }

    public string? F2 { get; set; }

    public string? ParentId { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(ParentId)])]
    public MyObjectParent? Parent { get; set; }
}

/// <summary>对应 Java：nop/MyObjectParent。</summary>
[Table("MyObjectParent")]
public class MyObjectParent
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(MyObjectParentChildren.ParentId)])]
    public List<MyObjectParentChildren>? Children { get; set; }
}

/// <summary>对应 Java：nop/MyObjectParentChildren。</summary>
[Table("MyObjectParentChildren")]
public class MyObjectParentChildren
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(MyChild.ChildrenParentId)])]
    public List<MyChild>? MyChildren { get; set; }
}

/// <summary>对应 Java：nop/MyChild。</summary>
[Table("MyChild")]
public class MyChild
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ChildrenParentId { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：nop/OtherTable。</summary>
[Table("OtherTable")]
public class OtherTable
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Type { get; set; }
}
