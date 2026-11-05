using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// 各方言共用的测试模型，与 TS 版 liveSuite 的 fixture 相同（公司、用户、帖子、评论），表由 code-first 建

/// <summary>公司。</summary>
[Table("t_company")]
public class Company
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(User.CompanyId)])]
    public List<User>? Users { get; set; }
}

/// <summary>用户；<see cref="Attrs"/> 是 JSON 列。</summary>
[Table("t_user")]
public class User
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public int? Age { get; set; }

    public long? CompanyId { get; set; }

    [Column(Converter = typeof(JsonValueConverter<UserAttrs>), DbType = "VARCHAR(1000)")]
    public UserAttrs? Attrs { get; set; }

    public DateTime CreatedAt { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.UserId)])]
    public List<Post>? Posts { get; set; }
}

/// <summary>用户的 JSON 属性。</summary>
public sealed record UserAttrs(List<string> Tags);

/// <summary>帖子。</summary>
[Table("t_post")]
public class Post
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Title { get; set; } = "";

    [Column(DbType = "DECIMAL(12,2)")]
    public decimal Amount { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(UserId)])]
    public User? User { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Comment.PostId)])]
    public List<Comment>? Comments { get; set; }
}

/// <summary>评论。</summary>
[Table("t_comment")]
public class Comment
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long PostId { get; set; }

    public string Body { get; set; } = "";

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(PostId)])]
    public Post? Post { get; set; }
}

/// <summary>主键由调用方给出的配置项（不是自增键）：验证按主键冲突的 upsert。</summary>
[Table("t_setting")]
public class Setting
{
    [Column(PrimaryKey = true)]
    public string Code { get; set; } = "";

    public string? Value { get; set; }
}
