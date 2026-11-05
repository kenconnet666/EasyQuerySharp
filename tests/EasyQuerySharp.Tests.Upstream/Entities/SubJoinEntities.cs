using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/SubJoinUser（用户有多本书）。表由 <see cref="Support.MySqlFixture"/> 按代码优先创建。</summary>
[Table("sub_join_user")]
public class SubJoinUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Age { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SubJoinBook.Uid)])]
    public List<SubJoinBook>? Books { get; set; }
}

/// <summary>对应 Java：entity/SubJoinBook（书属于一位作者）。</summary>
[Table("sub_join_book")]
public class SubJoinBook
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public decimal? Price { get; set; }

    public string? Uid { get; set; }

    public string? AuthorId { get; set; }

    public DateTime? CreateTime { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)])]
    public SubJoinAuthor? Author { get; set; }
}

/// <summary>对应 Java：entity/SubJoinAuthor。</summary>
[Table("sub_join_author")]
public class SubJoinAuthor
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Age { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }
}
