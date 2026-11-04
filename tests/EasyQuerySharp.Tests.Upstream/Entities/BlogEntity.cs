using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/BlogEntity。</summary>
[Table("t_blog")]
public class BlogEntity : BaseEntity
{
    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? Url { get; set; }

    public int? Star { get; set; }

    public DateTime? PublishTime { get; set; }

    public decimal? Score { get; set; }

    public int? Status { get; set; }

    public decimal? Order { get; set; }

    public bool? IsTop { get; set; }

    public bool? Top { get; set; }

    /// <summary>一对多：用户 id 等于博客的 title（Java 用例里的人为关系）。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Title)])]
    public List<SysUser>? Users { get; set; }
}

/// <summary>对应 Java：entity/SysUser（phone、idCard、address 在 Java 里是加密列，加密见 M5）。</summary>
[Table("t_sys_user", Schema = "easy-query-test", IgnoreProperties = ["updateTime", "createBy", "updateBy", "deleted"])]
public class SysUser : BaseEntity
{
    public string? Username { get; set; }

    public string? Phone { get; set; }

    public string? IdCard { get; set; }

    public string? Address { get; set; }

    /// <summary>一对多：博客的 title 等于用户 id（Java 用例里的人为关系）。</summary>
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(BlogEntity.Title)])]
    public List<BlogEntity>? Blogs { get; set; }

    /// <summary>一对一：同 id 的博客。</summary>
    [Navigate(RelationType.OneToOne)]
    public BlogEntity? MyBlog { get; set; }
}
