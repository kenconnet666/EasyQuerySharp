using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"带出与对象树"页：Include、投影里的嵌套集合、按 DTO 结构自动带出、按键填充、树。</summary>
public static class IncludeExamples
{
    // ---- Include

    [Example("include", "带出对多：每层一次 IN 查询，可以加条件和排序")]
    public static Task<List<User>> IncludeMany(EasyDb db) =>
        db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id)
            .Include(u => u.Posts, q => q.Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views))
            .ToListAsync();

    [Example("include", "带出对一与多对多")]
    public static Task<List<Post>> IncludeOneAndManyToMany(EasyDb db) =>
        db.Query<Post>().Where(p => p.Views >= 2000).OrderBy(p => p.Id)
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .ToListAsync();

    [Example("include", "嵌套带出：配置里再 Include")]
    public static Task<List<Order>> IncludeNested(EasyDb db) =>
        db.Query<Order>().Where(o => o.UserId == 1).OrderBy(o => o.Id)
            .Include(o => o.Items, q => q.Include(i => i.Product))
            .ToListAsync();

    [Example("include", "每个父对象只带出前几条：按父对象分区编行号")]
    public static Task<List<User>> IncludeTake(EasyDb db) =>
        db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
            .Include(u => u.Posts, q => q.OrderByDescending(p => p.Views).Take(1))
            .ToListAsync();

    /// <summary>（不是卡片）配置项写在创建 EasyDb 的地方，这里保证代码能编译。</summary>
    public static EasyDb IncludeUnionAllOptions(System.Data.Common.DbDataSource dataSource)
    {
        // <snippet:include-union-all>
        var db = new EasyDb(o =>
        {
            o.UseMySql(dataSource);
            o.IncludeLimitMode = IncludeLimitMode.UnionAll;   // 每个父对象一段 (… ORDER BY … LIMIT n)，UNION ALL 合并
        });
        // </snippet:include-union-all>
        return db;
    }

    [Example("include", "UnionAll 模式：每个父对象一段，UNION ALL 合并")]
    public static Task<List<User>> IncludeTakeUnionAll(EasyDb db) =>
        DocsDb.Reconfigure(db, o => o.IncludeLimitMode = IncludeLimitMode.UnionAll)
            .Query<User>().Where(u => u.Id <= 2).OrderBy(u => u.Id)
            .Include(u => u.Posts, q => q.OrderByDescending(p => p.Views).Take(1))
            .ToListAsync();

    [Example("include", "父键多时分批：IncludeGroupSize")]
    public static Task<List<Company>> IncludeGroupSize(EasyDb db) =>
        db.Query<Company>().OrderBy(c => c.Id).IncludeGroupSize(2).Include(c => c.Users).ToListAsync();

    [Example("include", "已经查出来的实体再带出：LoadIncludeAsync")]
    public static async Task<List<Post>> LoadInclude(EasyDb db)
    {
        var posts = await db.Query<Post>().Where(p => p.AuthorId == 1).ToListAsync();
        await db.LoadIncludeAsync(posts, p => p.Comments, q => q.Where(c => c.Likes >= 5));
        return posts;
    }

    // ---- 投影里的对象树

    /// <summary>帖子摘要。</summary>
    public sealed record PostItem(string Title, int Views);

    /// <summary>作者与帖子列表。</summary>
    public sealed record AuthorPosts(string Name, string? Company, List<PostItem> Posts);

    [Example("include", "投影里的嵌套集合：对一走 JOIN，对多每层一次查询")]
    public static Task<List<AuthorPosts>> NestedProjection(EasyDb db) =>
        db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id).Take(3)
            .Select(u => new AuthorPosts(u.Name, u.Company!.Name, u.Posts!.Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views).Select(p => new PostItem(p.Title, p.Views)).ToList()))
            .ToListAsync();

    [Example("include", "投影里的集合每个父对象取前几条：OrderBy + Take")]
    public static Task<List<AuthorPosts>> ProjectionTake(EasyDb db) =>
        db.Query<User>().Where(u => u.Id <= 3).OrderBy(u => u.Id)
            .Select(u => new AuthorPosts(u.Name, u.Company!.Name, u.Posts!.OrderByDescending(p => p.Views).Take(2).Select(p => new PostItem(p.Title, p.Views)).ToList()))
            .ToListAsync();

    /// <summary>公司 DTO。</summary>
    public sealed class CompanyVo
    {
        public string Name { get; set; } = "";

        public string City { get; set; } = "";
    }

    /// <summary>帖子 DTO：带出时只取已发布的、按阅读量取前两篇。</summary>
    public sealed class PostVo : IAutoIncludeConfigure<Post>
    {
        public string Title { get; set; } = "";

        public int Views { get; set; }

        public static Query<Post> Configure(Query<Post> query) =>
            query.Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views).Take(2);
    }

    /// <summary>用户 DTO：与导航同名的属性按导航带出。</summary>
    [MapFrom<User>]
    public sealed class UserVo
    {
        public string Name { get; set; } = "";

        public CompanyVo? Company { get; set; }

        public List<PostVo> Posts { get; set; } = [];
    }

    [Example("include", "按 DTO 结构自动带出：Select<TDto>()")]
    public static Task<List<UserVo>> AutoInclude(EasyDb db) =>
        db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id).Select<UserVo>().ToListAsync();

    /// <summary>评论 DTO。</summary>
    public sealed class CommentVo
    {
        public string Body { get; set; } = "";

        public int Likes { get; set; }
    }

    /// <summary>帖子与点赞最多的两条评论（DTO 属性上的 [Navigate] 覆盖排序与条数）。</summary>
    public sealed class PostWithTopComments
    {
        public string Title { get; set; } = "";

        [Navigate(RelationType.OneToMany, OrderBy = "Likes desc", Limit = 2)]
        public List<CommentVo> Comments { get; set; } = [];
    }

    [Example("include", "DTO 属性上的 [Navigate(OrderBy, Limit)]")]
    public static Task<List<PostWithTopComments>> DtoNavigateLimit(EasyDb db) =>
        db.Query<Post>().Where(p => p.Comments!.Any()).OrderBy(p => p.Id).Select<PostWithTopComments>().ToListAsync();

    // ---- 按键填充

    /// <summary>订单与明细（明细不是导航，按键填充）。</summary>
    public sealed class OrderSheet
    {
        public long Id { get; set; }

        public OrderStatus Status { get; set; }

        public List<OrderItem> Lines { get; set; } = [];
    }

    [Example("include", "按键填充：FillMany")]
    public static Task<List<OrderSheet>> FillMany(EasyDb db) =>
        db.Query<Order>().Where(o => o.UserId == 1).OrderBy(o => o.Id).Select<OrderSheet>()
            .FillMany(() => db.Query<OrderItem>(), i => i.OrderId, o => o.Id, (o, lines) => o.Lines = lines)
            .ToListAsync();

    // ---- 树

    /// <summary>部门的名字与层级。</summary>
    public sealed record DepartmentDepth(string Name, long? ParentId);

    [Example("include", "递归 CTE：某个节点和它的直接下级（LimitDeep = 1）")]
    public static Task<List<DepartmentDepth>> TreeCte(EasyDb db) =>
        db.Query<Department>().Where(d => d.Id == 1).AsTreeCte(o => o.LimitDeep = 1)
            .OrderBy(d => d.Id).Select(d => new DepartmentDepth(d.Name, d.ParentId)).ToListAsync();

    [Example("include", "往上找全部上级：Up = true")]
    public static Task<List<string>> TreeUp(EasyDb db) =>
        db.Query<Category>().Where(c => c.Name == "PostgreSQL").AsTreeCte(o => o.Up = true).OrderBy(c => c.Id).Select(c => c.Name).ToListAsync();

    [Example("include", "查出整张表再拼成树：ToTreeAsync")]
    public static async Task<object> ToTree(EasyDb db)
    {
        var roots = await db.Query<Category>().ToTreeAsync();
        static object Node(Category c) => new { c.Name, Children = c.Children!.Select(Node).ToList() };
        return roots.Select(Node).ToList();
    }
}
