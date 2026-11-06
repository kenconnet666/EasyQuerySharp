using System.Linq.Expressions;
using System.Runtime.Serialization;
using EasyQuerySharp.Encryption;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Samples;

// 全部文档卡片共用的业务模型：一个技术社区。公司、部门、员工，用户发帖、评论、互相关注，社区商店卖课程和会员。
// 与 TS 版 apps/docs/src/site/model.ts 一致；支付（多态）等 C# 版有了对应能力再加。

/// <summary>公司：南风智能没有员工。</summary>
[Table("t_company")]
public class Company
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    [Column(DbType = "VARCHAR(20)")]
    public string City { get; set; } = "";

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(User.CompanyId)])]
    public List<User>? Users { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Department.CompanyId)])]
    public List<Department>? Departments { get; set; }
}

/// <summary>部门：一棵树，<see cref="ParentId"/> 指向上级部门。</summary>
[Table("t_department")]
public class Department
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long CompanyId { get; set; }

    public long? ParentId { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ParentId)])]
    public Department? Parent { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ParentId)])]
    public List<Department>? Children { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(User.DepartmentId)])]
    public List<User>? Users { get; set; }
}

/// <summary>职级，库里存小写名字。</summary>
public enum UserLevel
{
    [EnumMember(Value = "junior")] Junior,
    [EnumMember(Value = "senior")] Senior,
    [EnumMember(Value = "lead")] Lead,
}

/// <summary>用户：方圆的年龄为空，顾言未成年且没有公司；<see cref="ManagerId"/> 指向上级。</summary>
[Table("t_user")]
public class User
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    public int? Age { get; set; }

    public long? CompanyId { get; set; }

    public long? DepartmentId { get; set; }

    public long? ManagerId { get; set; }

    [Column(Converter = typeof(EnumNameConverter<UserLevel>), DbType = "VARCHAR(10)")]
    public UserLevel Level { get; set; }

    public DateOnly JoinedAt { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(DepartmentId)])]
    public Department? Department { get; set; }

    /// <summary>一对一：资料表的主键就是用户 id。</summary>
    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Samples.Profile.UserId)])]
    public Profile? Profile { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ManagerId)])]
    public User? Manager { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ManagerId)])]
    public List<User>? Reports { get; set; }

    /// <summary>关注我的人：经关注表的多对多。</summary>
    [Navigate(RelationType.ManyToMany, Mapping = typeof(Follow), SelfMappingProperties = [nameof(Follow.FolloweeId)], TargetMappingProperties = [nameof(Follow.FollowerId)])]
    public List<User>? Followers { get; set; }

    /// <summary>我关注的人。</summary>
    [Navigate(RelationType.ManyToMany, Mapping = typeof(Follow), SelfMappingProperties = [nameof(Follow.FollowerId)], TargetMappingProperties = [nameof(Follow.FolloweeId)])]
    public List<User>? Following { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.AuthorId)])]
    public List<Post>? Posts { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Comment.AuthorId)])]
    public List<Comment>? Comments { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Order.UserId)])]
    public List<Order>? Orders { get; set; }

    /// <summary>不是主键的关联：文档记的是创建人的名字。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Name)], TargetProperties = [nameof(Doc.CreatedBy)])]
    public List<Doc>? Docs { get; set; }

    /// <summary>固定条件的一对多：只算阅读量过千的帖子。</summary>
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.AuthorId)], ExtraFilter = nameof(HotPostFilter))]
    public List<Post>? HotPosts { get; set; }

    private static Expression<Func<Post, bool>> HotPostFilter => p => p.Views >= 1000;
}

/// <summary>用户资料：与用户一对一，主键就是用户 id；不是每个用户都填了。</summary>
[Table("t_profile")]
public class Profile
{
    [Column(PrimaryKey = true)]
    public long UserId { get; set; }

    [Column(DbType = "VARCHAR(200)")]
    public string Bio { get; set; } = "";

    [Column(Converter = typeof(JsonValueConverter<List<string>>), DbType = "VARCHAR(500)")]
    public List<string> Skills { get; set; } = [];

    [Column(DbType = "VARCHAR(100)")]
    public string? Website { get; set; }

    /// <summary>资料一定属于某个用户：Required 时隐式 JOIN 用 INNER JOIN。</summary>
    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(UserId)], Required = true)]
    public User? User { get; set; }
}

/// <summary>帖子分类：一棵树。</summary>
[Table("t_category")]
public class Category
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long? ParentId { get; set; }

    [Column(DbType = "VARCHAR(30)")]
    public string Name { get; set; } = "";

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ParentId)])]
    public Category? Parent { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ParentId)])]
    public List<Category>? Children { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.CategoryId)])]
    public List<Post>? Posts { get; set; }
}

/// <summary>帖子状态。</summary>
public enum PostStatus
{
    [EnumMember(Value = "draft")] Draft,
    [EnumMember(Value = "published")] Published,
}

/// <summary>帖子：两篇草稿，阅读量 0 到 3320。</summary>
[Table("t_post")]
public class Post
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long AuthorId { get; set; }

    public long CategoryId { get; set; }

    [Column(DbType = "VARCHAR(100)")]
    public string Title { get; set; } = "";

    [Column(Converter = typeof(EnumNameConverter<PostStatus>), DbType = "VARCHAR(10)")]
    public PostStatus Status { get; set; }

    public int Views { get; set; }

    public DateOnly? PublishedAt { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)])]
    public User? Author { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CategoryId)])]
    public Category? Category { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Comment.PostId)])]
    public List<Comment>? Comments { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(PostTag), SelfMappingProperties = [nameof(PostTag.PostId)], TargetMappingProperties = [nameof(PostTag.TagId)])]
    public List<Tag>? Tags { get; set; }
}

/// <summary>评论：<see cref="ParentId"/> 指向被回复的评论。</summary>
[Table("t_comment")]
public class Comment
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long PostId { get; set; }

    public long AuthorId { get; set; }

    public long? ParentId { get; set; }

    [Column(DbType = "VARCHAR(200)")]
    public string Body { get; set; } = "";

    public int Likes { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(PostId)])]
    public Post? Post { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)])]
    public User? Author { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ParentId)])]
    public Comment? Parent { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ParentId)])]
    public List<Comment>? Replies { get; set; }
}

/// <summary>标签。</summary>
[Table("t_tag")]
public class Tag
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(30)")]
    public string Name { get; set; } = "";

    [Navigate(RelationType.ManyToMany, Mapping = typeof(PostTag), SelfMappingProperties = [nameof(PostTag.TagId)], TargetMappingProperties = [nameof(PostTag.PostId)])]
    public List<Post>? Posts { get; set; }
}

/// <summary>帖子和标签的多对多。</summary>
[Table("t_post_tag")]
public class PostTag
{
    [Column(PrimaryKey = true)]
    public long PostId { get; set; }

    [Column(PrimaryKey = true)]
    public long TagId { get; set; }
}

/// <summary>关注：用户和用户的多对多。</summary>
[Table("t_follow")]
public class Follow
{
    [Column(PrimaryKey = true)]
    public long FollowerId { get; set; }

    [Column(PrimaryKey = true)]
    public long FolloweeId { get; set; }
}

/// <summary>社区商店的商品。</summary>
[Table("t_product")]
public class Product
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    [Column(DbType = "DECIMAL(10,2)")]
    public decimal Price { get; set; }

    public int Stock { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(OrderItem.ProductId)])]
    public List<OrderItem>? Items { get; set; }
}

/// <summary>订单状态。</summary>
public enum OrderStatus
{
    [EnumMember(Value = "pending")] Pending,
    [EnumMember(Value = "paid")] Paid,
    [EnumMember(Value = "shipped")] Shipped,
    [EnumMember(Value = "cancelled")] Cancelled,
}

/// <summary>订单。</summary>
[Table("t_order")]
public class Order
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long UserId { get; set; }

    [Column(Converter = typeof(EnumNameConverter<OrderStatus>), DbType = "VARCHAR(10)")]
    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(UserId)])]
    public User? User { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(OrderItem.OrderId)])]
    public List<OrderItem>? Items { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(OrderItem), SelfMappingProperties = [nameof(OrderItem.OrderId)], TargetMappingProperties = [nameof(OrderItem.ProductId)])]
    public List<Product>? Products { get; set; }
}

/// <summary>订单明细：主键是订单加商品，<see cref="Price"/> 是下单时的价格。</summary>
[Table("t_order_item")]
public class OrderItem
{
    [Column(PrimaryKey = true)]
    public long OrderId { get; set; }

    [Column(PrimaryKey = true)]
    public long ProductId { get; set; }

    public int Quantity { get; set; }

    [Column(DbType = "DECIMAL(10,2)")]
    public decimal Price { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(OrderId)])]
    public Order? Order { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ProductId)])]
    public Product? Product { get; set; }
}

/// <summary>用到列规则的文档：逻辑删除、乐观锁、只在插入时写的创建人、更新时由拦截器填的时间。</summary>
[Table("t_doc")]
public class Doc
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(100)")]
    public string Title { get; set; } = "";

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    public bool Deleted { get; set; }

    [Version]
    public int Version { get; set; } = 1;

    [UpdateIgnore]
    [Column(DbType = "VARCHAR(50)")]
    public string CreatedBy { get; set; } = "";

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>工单：多租户（示例只看 1 号租户）、更新时自动填的时间、加密的联系方式。</summary>
[Table("t_ticket")]
public class Ticket
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    /// <summary>插入时由租户拦截器填。</summary>
    public int TenantId { get; set; }

    [Column(DbType = "VARCHAR(100)")]
    public string Title { get; set; } = "";

    [Encryption(typeof(ReverseEncryption))]
    [Column(DbType = "VARCHAR(100)")]
    public string Contact { get; set; } = "";

    public DateTime? TouchedAt { get; set; }
}

/// <summary>门店：地址是值对象，在库里展开成几列（address_city、address_street、geo_lat、geo_lng）。</summary>
[Table("t_shop")]
public class Shop
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    [ValueObject]
    public ShopAddress Address { get; set; } = new();
}

/// <summary>门店地址（值对象），里面再嵌一个坐标。</summary>
public class ShopAddress
{
    [Column(DbType = "VARCHAR(20)")]
    public string City { get; set; } = "";

    [Column(DbType = "VARCHAR(100)")]
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

/// <summary>示例用的可逆"加密"：确定性的，所以能按密文比较。真实项目换成 AES 这类加密。</summary>
public sealed class ReverseEncryption : IEncryptionStrategy
{
    /// <inheritdoc />
    public object? Encrypt(Type entityType, string propertyName, object? plaintext) =>
        plaintext is string s ? "enc:" + new string(s.Reverse().ToArray()) : plaintext;

    /// <inheritdoc />
    public object? Decrypt(Type entityType, string propertyName, object? ciphertext) =>
        ciphertext is string s && s.StartsWith("enc:", StringComparison.Ordinal) ? new string(s[4..].Reverse().ToArray()) : ciphertext;
}
