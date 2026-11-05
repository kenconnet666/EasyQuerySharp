using EasyQuerySharp.Encryption;
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

    /// <summary>同 Users，但上面的聚合总是合并成分组 JOIN（Java 的 subQueryToGroupJoin = true）。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Title)], GroupJoin = true)]
    public List<SysUser>? Users2 { get; set; }
}

/// <summary>对应 Java：entity/SysUser（phone、idCard、address 是支持 LIKE 的加密列）。</summary>
[Table("t_sys_user", Schema = "easy-query-test", IgnoreProperties = ["updateTime", "createBy", "updateBy", "deleted"])]
public class SysUser : BaseEntity
{
    public string? Username { get; set; }

    [Encryption(typeof(DefaultAesEasyEncryptionStrategy), SupportQueryLike = true)]
    public string? Phone { get; set; }

    [Encryption(typeof(DefaultAesEasyEncryptionStrategy), SupportQueryLike = true)]
    public string? IdCard { get; set; }

    [Encryption(typeof(DefaultAesEasyEncryptionStrategy), SupportQueryLike = true)]
    public string? Address { get; set; }

    /// <summary>一对多：博客的 title 等于用户 id（Java 用例里的人为关系）。</summary>
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(BlogEntity.Title)])]
    public List<BlogEntity>? Blogs { get; set; }

    /// <summary>一对一：同 id 的博客。</summary>
    [Navigate(RelationType.OneToOne)]
    public BlogEntity? MyBlog { get; set; }
}

/// <summary>对应 Java：entity/SysUserTrack（追踪用例的用户表，手机号、身份证、地址是加密列）。</summary>
[Table("t_sys_user_track")]
public class SysUserTrack
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Username { get; set; }

    [Encryption(typeof(DefaultAesEasyEncryptionStrategy), SupportQueryLike = true)]
    public string? Phone { get; set; }

    [Encryption(typeof(DefaultAesEasyEncryptionStrategy), SupportQueryLike = true)]
    public string? IdCard { get; set; }

    [Encryption(typeof(DefaultAesEasyEncryptionStrategy), SupportQueryLike = true)]
    public string? Address { get; set; }

    public DateTime? CreateTime { get; set; }
}
