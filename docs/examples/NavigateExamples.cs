using EasyQuerySharp.Metadata;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"联表与导航"页：显式 JOIN、对一导航的隐式 JOIN、集合导航的隐式子查询、分组合并、取第几个元素、多对多。</summary>
public static class NavigateExamples
{
    // ---- 显式 JOIN

    /// <summary>帖子标题与作者。</summary>
    public sealed record PostAuthor(string Title, string? Author);

    [Example("navigate", "显式 JOIN：后面的 lambda 依次收到每张表")]
    public static Task<List<PostAuthor>> LeftJoin(EasyDb db) =>
        db.Query<Post>()
            .LeftJoin<User>((p, u) => p.AuthorId == u.Id)
            .Where((p, u) => u.Age >= 30)
            .OrderBy((p, u) => p.Id)
            .Select((p, u) => new PostAuthor(p.Title, u.Name))
            .ToListAsync();

    /// <summary>每篇帖子的评论数。</summary>
    public sealed record PostComments(long PostId, long Count);

    /// <summary>帖子与评论数。</summary>
    public sealed record PostWithComments(string Title, long? Comments);

    [Example("navigate", "JOIN 一个子查询")]
    public static Task<List<PostWithComments>> JoinSubQuery(EasyDb db)
    {
        var counts = db.Query<Comment>().GroupBy(c => c.PostId).Select(g => new PostComments(g.Key, g.Count()));
        return db.Query<Post>()
            .InnerJoin(counts, (p, c) => p.Id == c.PostId)
            .OrderBy((p, c) => p.Id)
            .Select((p, c) => new PostWithComments(p.Title, c.Count))
            .ToListAsync();
    }

    [Example("navigate", "LINQ 查询语法：join … equals …")]
    public static Task<List<PostAuthor>> QuerySyntax(EasyDb db) =>
        (from p in db.Query<Post>()
         join u in db.Query<User>() on p.AuthorId equals u.Id
         where p.Views >= 2000
         select new PostAuthor(p.Title, u.Name)).ToListAsync();

    // ---- 对一导航

    /// <summary>资料与用户名。</summary>
    public sealed record ProfileOwner(string? Name, string Bio);

    [Example("navigate", "Required 的对一导航用 INNER JOIN")]
    public static Task<List<ProfileOwner>> RequiredToOne(EasyDb db) =>
        db.Query<Profile>().OrderBy(p => p.UserId).Take(3).Select(p => new ProfileOwner(p.User!.Name, p.Bio)).ToListAsync();

    /// <summary>员工与上级。</summary>
    public sealed record EmployeeManager(string Name, string? Manager, string? ManagersManager);

    [Example("navigate", "自关联与多级导航：每级一个 LEFT JOIN")]
    public static Task<List<EmployeeManager>> SelfReference(EasyDb db) =>
        db.Query<User>().Where(u => u.ManagerId != null).OrderBy(u => u.Id)
            .Select(u => new EmployeeManager(u.Name, u.Manager!.Name, u.Manager!.Manager!.Name)).ToListAsync();

    // ---- 集合导航：隐式子查询

    [Example("navigate", "Any / !Any：EXISTS 与 NOT EXISTS")]
    public static Task<List<string>> AnyNone(EasyDb db) =>
        db.Query<User>().Where(u => u.Posts!.Any(p => p.Views >= 2000) && !u.Comments!.Any()).Select(u => u.Name).ToListAsync();

    /// <summary>作者的帖子统计。</summary>
    public sealed record AuthorStats(string Name, long Posts, int Views, int? Best);

    [Example("navigate", "集合上的聚合：Count / Sum / Max 是标量子查询")]
    public static Task<List<AuthorStats>> CollectionAggregates(EasyDb db) =>
        db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
            .Select(u => new AuthorStats(u.Name, u.Posts!.Count(), u.Posts!.Sum(p => p.Views), u.Posts!.Max(p => (int?)p.Views)))
            .ToListAsync();

    [Example("navigate", "多对多：经中间表的 EXISTS")]
    public static Task<List<string>> ManyToMany(EasyDb db) =>
        db.Query<Post>().Where(p => p.Tags!.Any(t => t.Name == "架构")).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();

    /// <summary>作者与热门帖数（导航自带条件）。</summary>
    public sealed record AuthorHot(string Name, long Hot);

    [Example("navigate", "导航上的固定条件：[Navigate(ExtraFilter = …)]")]
    public static Task<List<AuthorHot>> ExtraFilter(EasyDb db) =>
        db.Query<User>().Where(u => u.HotPosts!.Any()).OrderBy(u => u.Id).Select(u => new AuthorHot(u.Name, u.HotPosts!.Count())).ToListAsync();

    [Example("navigate", "这条查询里给导航加条件：FilterNavigation")]
    public static Task<List<AuthorHot>> FilterNavigation(EasyDb db) =>
        db.Query<User>().FilterNavigation(u => u.Posts, p => p.Status == PostStatus.Published)
            .Where(u => u.Posts!.Any()).OrderBy(u => u.Id).Select(u => new AuthorHot(u.Name, u.Posts!.Count())).ToListAsync();

    [Example("navigate", "集合导航上的字符串聚合")]
    public static Task<List<string>> StringJoin(EasyDb db) =>
        db.Query<Post>().Where(p => p.Id <= 4).OrderBy(p => p.Id)
            .Select(p => p.Title + "：" + string.Join("、", p.Tags!.OrderBy(t => t.Id).Select(t => t.Name))).ToListAsync();

    [Example("navigate", "目标实体有逻辑删除：导航上关掉 DisableLogicDelete")]
    public static Task<List<AuthorHot>> NavigationLogicDelete(EasyDb db) =>
        db.Query<User>().Where(u => u.Id <= 2).OrderBy(u => u.Id).Select(u => new AuthorHot(u.Name, u.Docs!.DisableLogicDelete().Count())).ToListAsync();

    // ---- 分组合并

    [Example("navigate", "同一导航上的几个聚合合并成一次分组 JOIN：GroupJoin")]
    public static Task<List<AuthorStats>> GroupJoin(EasyDb db) =>
        db.Query<User>().GroupJoin(u => u.Posts).Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
            .Select(u => new AuthorStats(u.Name, u.Posts!.Count(), u.Posts!.Sum(p => p.Views), u.Posts!.Max(p => (int?)p.Views)))
            .ToListAsync();

    /// <summary>作者的帖子数与评论数。</summary>
    public sealed record AuthorActivity(string Name, long Posts, long Comments);

    [Example("navigate", "所有集合导航都合并：GroupJoinAll")]
    public static Task<List<AuthorActivity>> GroupJoinAll(EasyDb db) =>
        db.Query<User>().GroupJoinAll().Where(u => u.Id <= 3).OrderBy(u => u.Id)
            .Select(u => new AuthorActivity(u.Name, u.Posts!.Count(), u.Comments!.Count())).ToListAsync();

    // ---- 取第几个元素

    /// <summary>作者与最热的两篇。</summary>
    public sealed record TopPosts(string Name, string? Top, string? Second);

    [Example("navigate", "集合里的第几个：First() / ElementAt(n)")]
    public static Task<List<TopPosts>> Elements(EasyDb db) =>
        db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
            .Select(u => new TopPosts(u.Name, u.Posts!.OrderByDescending(p => p.Views).First().Title, u.Posts!.OrderByDescending(p => p.Views).ElementAt(1).Title))
            .ToListAsync();

    [Example("navigate", "条件里用第一个元素")]
    public static Task<List<string>> FirstInWhere(EasyDb db) =>
        db.Query<User>().Where(u => u.Posts!.OrderByDescending(p => p.Views).First().Views >= 2000).Select(u => u.Name).ToListAsync();

    // <snippet:navigate-declare>
    [Table("t_user")]
    public class Author
    {
        [Column(PrimaryKey = true)]
        public long Id { get; set; }

        public string Name { get; set; } = "";

        public long? CompanyId { get; set; }

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
        public Company? Company { get; set; }

        // 一对多：目标表的 author_id 对本表主键
        [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.AuthorId)], GroupJoin = true)]
        public List<Post>? Posts { get; set; }

        // 声明了排序和条数的对一导航：每个作者阅读量最高的那篇
        [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Post.AuthorId)], OrderBy = "Views desc", PartitionOrder = PartitionOrder.Navigation, Limit = 1)]
        public Post? TopPost { get; set; }
    }

    [Table("t_post")]
    public class Article
    {
        [Column(PrimaryKey = true)]
        public long Id { get; set; }

        public long AuthorId { get; set; }

        public string Title { get; set; } = "";

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)])]
        public Author? Author { get; set; }

        // 直达导航：等于 Author.Company
        [Navigate(RelationType.ManyToOne, DirectMapping = [nameof(Author), nameof(Author.Company)])]
        public Company? AuthorCompany { get; set; }
    }
    // </snippet:navigate-declare>

    /// <summary>作者与最热的一篇。</summary>
    public sealed record AuthorTop(string Name, string? Top, int? Views);

    [Example("navigate", "声明了条数的对一导航：按排序取那一行")]
    public static Task<List<AuthorTop>> RankedToOne(EasyDb db) =>
        db.Query<Author>().Where(a => a.TopPost!.Views >= 1000).OrderBy(a => a.Id)
            .Select(a => new AuthorTop(a.Name, a.TopPost!.Title, a.TopPost!.Views)).ToListAsync();

    /// <summary>文章与作者所在公司。</summary>
    public sealed record ArticleCompany(string Title, string? Company);

    [Example("navigate", "直达导航：[Navigate(DirectMapping = …)]")]
    public static Task<List<ArticleCompany>> DirectMapping(EasyDb db) =>
        db.Query<Article>().Where(a => a.AuthorCompany!.City == "上海").OrderBy(a => a.Id)
            .Select(a => new ArticleCompany(a.Title, a.AuthorCompany!.Name)).ToListAsync();
}
