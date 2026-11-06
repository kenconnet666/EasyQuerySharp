using System.Linq.Expressions;
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

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.UserId)], Cascade = Cascade.Delete)]
    public List<Post>? Posts { get; set; }

    /// <summary>多对多：经 <see cref="UserRole"/> 中间表（对应 TS 的 roles）。</summary>
    [Navigate(RelationType.ManyToMany, Mapping = typeof(UserRole),
        SelfMappingProperties = [nameof(UserRole.UserId)], TargetMappingProperties = [nameof(UserRole.RoleId)], Cascade = Cascade.Delete)]
    public List<Role>? Roles { get; set; }

    /// <summary>带固定条件的一对多：本人的大额帖子（对应 TS 的 manyOn）。</summary>
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.UserId)], ExtraFilter = nameof(BigPostFilter))]
    public List<Post>? BigPosts { get; set; }

    /// <summary>每个用户最新的一篇帖子（对应 TS 的 oneOfMany）：一对一导航按 id 倒序取 1 条（取那一行按声明的 OrderBy，不用写 PartitionOrder）。</summary>
    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Post.UserId)], OrderBy = "Id desc", Limit = 1)]
    public Post? LatestPost { get; set; }

    private static Expression<Func<Post, bool>> BigPostFilter => p => p.Amount > 100;
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

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Comment.PostId)], Cascade = Cascade.Delete)]
    public List<Comment>? Comments { get; set; }

    /// <summary>直达导航：经作者到他的公司（对应 TS 的 pathOne）。</summary>
    [Navigate(RelationType.ManyToOne, DirectMapping = [nameof(User), nameof(User.Company)])]
    public Company? Company { get; set; }
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

    public string? Remark { get; set; }
}

/// <summary>带乐观锁版本的便签：验证批量更新后新版本写回实体。</summary>
[Table("t_live_note")]
public class Note
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Text { get; set; } = "";

    [Version]
    public long? Version { get; set; }
}

/// <summary>JSON 列：地址列表与扩展字段（对应 Java fork 的 ForkProfile）。</summary>
[Table("t_live_profile")]
public class Profile
{
    [Column(PrimaryKey = true)]
    public string Id { get; set; } = "";

    [Column(Converter = typeof(JsonValueConverter<List<ProfileAddress>>))]
    public List<ProfileAddress>? Addresses { get; set; }

    [Column(Converter = typeof(JsonValueConverter<Dictionary<string, object?>>))]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>地址。</summary>
public sealed class ProfileAddress
{
    public string? City { get; set; }

    public int? Zip { get; set; }
}

/// <summary>门店：地址与联系方式是值对象（与 TS 版 embed.test 的 Shop 相同）。</summary>
[Table("t_live_shop")]
public class Shop
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    [ValueObject]
    public ShopAddress Address { get; set; } = new();

    [ValueObject]
    public ShopContact? Contact { get; set; }
}

/// <summary>门店地址（值对象，里面再嵌一个坐标）。</summary>
public class ShopAddress
{
    public string Province { get; set; } = "";

    public string City { get; set; } = "";

    public string? Street { get; set; }

    [ValueObject(Prefix = "geo_")]
    public GeoPoint Geo { get; set; } = new();
}

/// <summary>坐标。</summary>
public class GeoPoint
{
    public double Lat { get; set; }

    public double Lng { get; set; }
}

/// <summary>联系方式（可空的值对象）。</summary>
public class ShopContact
{
    [Column("tel")]
    public string? Phone { get; set; }
}

/// <summary>角色；与用户多对多（对应 TS 的 Role）。</summary>
[Table("t_role")]
public class Role
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    [Navigate(RelationType.ManyToMany, Mapping = typeof(UserRole),
        SelfMappingProperties = [nameof(UserRole.RoleId)], TargetMappingProperties = [nameof(UserRole.UserId)])]
    public List<User>? Users { get; set; }
}

/// <summary>用户与角色的中间表（复合主键）。</summary>
[Table("t_user_role")]
public class UserRole
{
    [Column(PrimaryKey = true)]
    public long UserId { get; set; }

    [Column(PrimaryKey = true)]
    public long RoleId { get; set; }
}

/// <summary>成员：以 (TenantId, Id) 为主键，笔记按这一对列引用成员（对应 TS 的 Member）。</summary>
[Table("t_member")]
public class Member
{
    [Column(PrimaryKey = true)]
    public int TenantId { get; set; }

    [Column(PrimaryKey = true)]
    public int Id { get; set; }

    public string Name { get; set; } = "";

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(TenantId), nameof(Id)], TargetProperties = [nameof(MemberNote.TenantId), nameof(MemberNote.MemberId)], Cascade = Cascade.Delete)]
    public List<MemberNote>? Notes { get; set; }
}

/// <summary>成员的笔记（复合外键；对应 TS 的 Note）。</summary>
[Table("t_member_note")]
public class MemberNote
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public int TenantId { get; set; }

    public int MemberId { get; set; }

    public string Body { get; set; } = "";

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(TenantId), nameof(MemberId)], TargetProperties = [nameof(Member.TenantId), nameof(Member.Id)])]
    public Member? Member { get; set; }
}

/// <summary>帖子的归档表：列与 <see cref="Post"/> 相同，用例里单独建（INSERT … SELECT、换表名）。</summary>
[Table("t_post_archive")]
public class PostArchive
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Title { get; set; } = "";

    [Column(DbType = "DECIMAL(12,2)")]
    public decimal Amount { get; set; }
}

/// <summary>自关联的树：部门的上级是另一个部门（对应 TS 的 Dept）。</summary>
[Table("t_dept")]
public class Dept
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long? ParentId { get; set; }

    public string Name { get; set; } = "";

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)], Cascade = Cascade.Delete)]
    public List<Dept>? Children { get; set; }
}
