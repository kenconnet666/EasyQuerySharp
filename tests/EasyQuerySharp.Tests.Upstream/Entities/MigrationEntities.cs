using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/MyMigrationBlog（code-first 建表用，带列注释）。</summary>
[Table("my_migration_blog")]
public class MyMigrationBlog : BaseEntity
{
    [Column(Comment = "标题")]
    public string? Title { get; set; }

    [Column(Comment = "内容")]
    public string? Content { get; set; }

    [Column(Comment = "博客链接")]
    public string? Url { get; set; }

    [Column(Comment = "点赞数")]
    public int? Star { get; set; }

    [Column(Comment = "发布时间")]
    public DateTime? PublishTime { get; set; }

    [Column(Comment = "评分")]
    public decimal? Score { get; set; }

    [Column(Comment = "状态")]
    public int? Status { get; set; }

    [Column(Comment = "排序")]
    public decimal? Order { get; set; }

    [Column(Comment = "是否置顶")]
    public bool? IsTop { get; set; }

    [Column(Comment = "是否置顶")]
    public bool? Top { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Title)])]
    public List<SysUser>? Users { get; set; }
}

/// <summary>对应 Java：entity/MyMigrationBlog0（各方言的 MigrationXxxTest 建表、删表用）。</summary>
[Table("my_migration_blog0")]
public class MyMigrationBlog0 : BaseEntity
{
    [Column(Comment = "标题")]
    public string? Title { get; set; }

    [Column(Comment = "内容")]
    public string? Content { get; set; }

    [Column(Comment = "博客链接")]
    public string? Url { get; set; }

    [Column(Comment = "点赞数")]
    public int? Star { get; set; }

    [Column(Comment = "发布时间")]
    public DateTime? PublishTime { get; set; }

    [Column(Comment = "评分")]
    public decimal? Score { get; set; }

    [Column(Comment = "状态")]
    public int? Status { get; set; }

    [Column(Comment = "排序")]
    public decimal? Order { get; set; }

    [Column(Comment = "是否置顶")]
    public bool? IsTop { get; set; }

    [Column(Comment = "是否置顶")]
    public bool? Top { get; set; }
}

/// <summary>对应 Java：entity/NewTopic（t_topic 改名成 t_topic2，Java 写作 <c>@Table(value = "t_topic2", oldName = "t_topic")</c>）。</summary>
[Table("t_topic2"), RenamedFrom("t_topic")]
public class NewTopic
{
    public string? Abc { get; set; }
}

/// <summary>对应 Java：entity/NewTopic3（t_topic 的 title 列改名成 abc，Java 写作 <c>@Column(oldName = "title", dbType = "varchar(200)")</c>）。</summary>
[Table("t_topic")]
public class NewTopic3
{
    [Column(DbType = "varchar(200)"), RenamedFrom("title")]
    public string? Abc { get; set; }
}


/// <summary>对应 Java：mysql8/entity/M8TestIndex（两个索引；Java 另有 dbDefault，见 MySQL8Test2 的 code-first 用例）。</summary>
[Table("m8_test_index")]
[TableIndex(nameof(Column1), nameof(Column2))]
[TableIndex(nameof(Column2), Unique = true)]
public class M8TestIndex
{
    [Column(PrimaryKey = true)]
    public string? Column1 { get; set; }

    public string? Column2 { get; set; }

    public string? Column3 { get; set; }

    public string? Column4 { get; set; }

    public DateTime CreateTime { get; set; }
}
