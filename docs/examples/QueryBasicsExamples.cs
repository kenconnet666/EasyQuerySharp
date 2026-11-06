using EasyQuerySharp.Querying;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"查询 / 基础"页的卡片：终结方法、选列、条件、排序与分页。与 TS 版 query/basics.examples.ts 同名的卡片写法对应。</summary>
public static class QueryBasicsExamples
{
    // ---- 终结方法

    [Example("query/basics", "查不到时返回 null")]
    public static async Task<object> FirstMissing(EasyDb db)
    {
        var first = await db.Query<User>().Where(u => u.Name == "不存在的人").FirstOrDefaultAsync();
        var found = await db.Query<User>().FindAsync(999L);
        return new { FirstIsNull = first is null, FoundIsNull = found is null };
    }

    [Example("query/basics", "查不到时抛 NotFoundException")]
    public static async Task<object> FindThrows(EasyDb db)
    {
        try
        {
            await db.Query<User>().FindOrThrowAsync(999L, "用户不存在");
            return "found";
        }
        catch (NotFoundException ex)
        {
            return ex.Message;
        }
    }

    [Example("query/basics", "无实体的表：按列名查询")]
    public static async Task<object> DynamicTable(EasyDb db)
    {
        var rows = await db.Table("t_user").Where(u => u.Get<int>("age") >= 30).OrderBy(u => u.Get<int>("age"))
            .SelectColumns("name", "age").Take(3).ToListAsync();
        return rows.Select(r => new { Name = r.Get<string>("name"), Age = r.Get<int>("age") }).ToList();
    }

    [Example("query/basics", "树：取全部下级并拼成树")]
    public static async Task<object> DepartmentTree(EasyDb db)
    {
        var roots = await db.Query<Department>().Where(d => d.Name == "研发部").AsTreeCte().ToTreeAsync();
        static object Node(Department d) => new { d.Name, Children = d.Children!.Select(Node).ToList() };
        return roots.Select(Node).ToList();
    }

    [Example("query/basics", "按条件计数与判断存在")]
    public static async Task<object> CountAndAny(EasyDb db) => new
    {
        Adults = await db.Query<User>().Where(u => u.Age >= 18).CountAsync(),
        HasDrafts = await db.Query<Post>().Where(p => p.Status == PostStatus.Draft).AnyAsync(),
    };

    // ---- 选列

    /// <summary>投影的结果。</summary>
    public sealed record PostCard(string Title, int NextViews, bool IsHot);

    [Example("query/basics", "选表达式")]
    public static Task<List<PostCard>> SelectExpressions(EasyDb db) =>
        db.Query<Post>().Where(p => p.Status == PostStatus.Published)
            .Select(p => new PostCard(p.Title.ToUpper(), p.Views + 1, p.Views >= 2000))
            .Take(3).ToListAsync();

    /// <summary>带作者信息的帖子。</summary>
    public sealed record PostWithAuthor(string Title, int Views, string? Author, string? Company);

    [Example("query/basics", "经导航取关联表的列")]
    public static Task<List<PostWithAuthor>> SelectNavigation(EasyDb db) =>
        db.Query<Post>().OrderBy(p => p.Id).Take(2)
            .Select(p => new PostWithAuthor(p.Title, p.Views, p.Author!.Name, p.Author!.Company!.Name))
            .ToListAsync();

    /// <summary>门店与它的坐标。</summary>
    public sealed record ShopPoint(string Name, GeoPoint Geo);

    [Example("query/basics", "值对象：按成员过滤、整体投影")]
    public static Task<List<ShopPoint>> ValueObjects(EasyDb db) =>
        db.Query<Shop>().Where(s => s.Address.City == "杭州" && s.Address.Street != null)
            .Select(s => new ShopPoint(s.Name, s.Address.Geo)).ToListAsync();

    [Example("query/basics", "沿集合导航展开")]
    public static Task<List<string>> FlattenNavigations(EasyDb db) =>
        db.Query<Company>().Where(c => c.Id == 1).SelectMany(c => c.Users).SelectMany(u => u.Following)
            .OrderBy(u => u.Name).Select(u => u.Name).Distinct().ToListAsync();

    /// <summary>去重的结果行。</summary>
    public sealed record AuthorStatus(long AuthorId, PostStatus Status);

    [Example("query/basics", "去重")]
    public static Task<List<AuthorStatus>> DistinctRows(EasyDb db) =>
        db.Query<Post>().Select(p => new AuthorStatus(p.AuthorId, p.Status)).Distinct().OrderBy(r => r.AuthorId).Take(4).ToListAsync();

    // ---- 条件

    [Example("query/basics", "多个 Where 之间是 AND")]
    public static Task<List<string>> WhereChain(EasyDb db) =>
        db.Query<User>().Where(u => u.Age >= 18).Where(u => u.CompanyId == 1).Select(u => u.Name).ToListAsync();

    /// <summary>评论与评论人。</summary>
    public sealed record CommentBy(string Body, string? By);

    [Example("query/basics", "两列比较：作者自己的评论")]
    public static Task<List<CommentBy>> CompareColumns(EasyDb db) =>
        db.Query<Comment>().Where(c => c.AuthorId == c.Post!.AuthorId).Select(c => new CommentBy(c.Body, c.Author!.Name)).ToListAsync();

    [Example("query/basics", "LIKE 家族")]
    public static Task<List<string>> LikeFamily(EasyDb db) =>
        db.Query<Post>().Where(p => p.Title.Contains("缓存") || p.Title.StartsWith("把") || p.Title.EndsWith("入门")).Select(p => p.Title).ToListAsync();

    [Example("query/basics", "值里的通配符按字面匹配")]
    public static Task<List<string>> LikeLiteral(EasyDb db) =>
        db.Query<Post>().Where(p => p.Title.Contains("N+1")).Select(p => p.Title).ToListAsync();

    [Example("query/basics", "IN 与 NOT IN")]
    public static Task<List<string>> InList(EasyDb db)
    {
        long[] companies = [1, 3];
        return db.Query<User>().Where(u => companies.Contains(u.CompanyId!.Value) && !new[] { UserLevel.Lead }.Contains(u.Level)).Select(u => u.Name).ToListAsync();
    }

    [Example("query/basics", "可空列与 NULL")]
    public static Task<List<string>> NullChecks(EasyDb db) =>
        db.Query<User>().Where(u => u.Age == null || u.CompanyId == null).Select(u => u.Name).ToListAsync();

    [Example("query/basics", "条件成立时才加的 Where")]
    public static Task<List<string>> WhereIf(EasyDb db)
    {
        string? keyword = "数据";
        int? minViews = null;
        return db.Query<Post>()
            .WhereIf(keyword is not null, p => p.Title.Contains(keyword!))
            .WhereIf(minViews is not null, p => p.Views >= minViews!.Value)
            .Select(p => p.Title).ToListAsync();
    }

    /// <summary>可复用的投影：作者名片。</summary>
    public sealed record AuthorCard(string Name, int? Age);

    /// <summary>投影写成表达式字段，多处共用；查询里用 Project 套到导航上，等同于把它写在原地。</summary>
    private static readonly System.Linq.Expressions.Expression<Func<User, AuthorCard>> AuthorMap = u => new AuthorCard(u.Name, u.Age);

    [Example("query/basics", "可复用投影：Project(map)")]
    public static Task<List<AuthorCard>> ProjectMap(EasyDb db) =>
        db.Query<Post>().Where(p => p.Status == PostStatus.Published).OrderBy(p => p.Id).Take(3).Select(p => p.Author!.Project(AuthorMap)).ToListAsync();

    // ---- 排序与分页

    /// <summary>名字与年龄。</summary>
    public sealed record NameAge(string Name, int? Age);

    [Example("query/basics", "多列排序")]
    public static Task<List<NameAge>> OrderByColumns(EasyDb db) =>
        db.Query<User>().OrderByDescending(u => u.Level).ThenBy(u => u.Age).Select(u => new NameAge(u.Name, u.Age)).ToListAsync();

    [Example("query/basics", "分页：总数与当前页")]
    public static Task<Page<string>> Paging(EasyDb db) =>
        db.Query<Post>().Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views).Select(p => p.Title).ToPageAsync(2, 4);
}
