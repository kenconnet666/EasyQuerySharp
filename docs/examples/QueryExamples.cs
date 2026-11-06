using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"查询"页（query/basics 之外）：条件的写法、子查询、排序与分页、投影到 DTO、合并与 CTE、流式读取。</summary>
public static partial class QueryExamples
{
    // ---- 条件

    [Example("query", "按主键查：WhereById / WhereByIds")]
    public static Task<List<string>> WhereByIds(EasyDb db) =>
        db.Query<User>().WhereByIds(new[] { 1L, 3L }).Select(u => u.Name).ToListAsync();

    [Example("query", "区间与多值：RangeClosed、In")]
    public static Task<List<string>> RangeAndIn(EasyDb db) =>
        db.Query<User>()
            .Where(u => u.Age!.Value.RangeClosed(25, 35) && u.Level.In(UserLevel.Senior, UserLevel.Lead))
            .OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();

    [Example("query", "IN 子查询与 EXISTS 子查询")]
    public static Task<List<string>> SubQueryConditions(EasyDb db) =>
        db.Query<User>()
            .Where(u => u.Id.In(db.Query<Post>().Where(p => p.Views >= 2000).Select(p => p.AuthorId)))
            .Where(u => Sql.Exists(db.Query<Comment>().Where(c => c.AuthorId == u.Id)))
            .OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();

    /// <summary>帖子与它比平均阅读量高多少。</summary>
    public sealed record AboveAverage(string Title, int Views, double Average);

    [Example("query", "标量子查询：Sql.SubQuery")]
    public static Task<List<AboveAverage>> ScalarSubQuery(EasyDb db) =>
        db.Query<Post>()
            .Where(p => p.Views > Sql.SubQuery(db.Query<Post>().Select(x => Sql.Avg((double)x.Views))))
            .OrderBy(p => p.Id)
            .Select(p => new AboveAverage(p.Title, p.Views, Sql.SubQuery(db.Query<Post>().Select(x => Sql.Avg((double)x.Views)))))
            .ToListAsync();

    [Example("query", "值为 null 或空白时跳过条件：UseValueFilter")]
    public static Task<List<string>> ValueFilter(EasyDb db)
    {
        string? name = null;
        var city = "杭州";
        return db.Query<User>().UseValueFilter(Filtering.ValueFilter.NotNullOrBlank)
            .Where(u => u.Name == name && u.Company!.City == city)
            .OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
    }

    // ---- 排序与分页

    /// <summary>名字与年龄。</summary>
    public sealed record NameAge(string Name, int? Age);

    [Example("query", "NULL 排在最后：Sql.NullsLast")]
    public static Task<List<NameAge>> NullsLast(EasyDb db) =>
        db.Query<User>().OrderByDescending(u => Sql.NullsLast(u.Age)).Take(3).Select(u => new NameAge(u.Name, u.Age)).ToListAsync();

    [Example("query", "条件成立时才排序：OrderByIf")]
    public static Task<List<string>> OrderByIf(EasyDb db)
    {
        var byViews = true;
        return db.Query<Post>().OrderByDescendingIf(byViews, p => p.Views).ThenBy(p => p.Id).Take(3).Select(p => p.Title).ToListAsync();
    }

    [Example("query", "跳过与取几行：Skip / Take")]
    public static Task<List<string>> SkipTake(EasyDb db) =>
        db.Query<Post>().OrderBy(p => p.Id).Skip(2).Take(3).Select(p => p.Title).ToListAsync();

    [Example("query", "深分页反向排序：偏移超过总数一半时倒过来取")]
    public static Task<Page<string>> ReversePaging(EasyDb db)
    {
        var reversing = DocsDb.Reconfigure(db, o => o.ReverseOffsetThreshold = 5);
        return reversing.Query<Post>().OrderBy(p => p.Id).Select(p => p.Title).ToPageAsync(4, 3);
    }

    // ---- 投影

    /// <summary>与实体同名的属性按名字取列。</summary>
    public sealed class UserRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = "";

        public int? Age { get; set; }
    }

    [Example("query", "按属性名映射到 DTO：Select<TDto>()")]
    public static Task<List<UserRow>> SelectDto(EasyDb db) =>
        db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id).Select<UserRow>().ToListAsync();

    [Example("query", "映射时去掉几列：SelectExcept<TDto>")]
    public static Task<List<UserRow>> SelectExcept(EasyDb db) =>
        db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id).SelectExcept<UserRow>(u => u.Age).ToListAsync();

    /// <summary>帖子行：作者名平铺（带出查询）、公司名平铺（JOIN）、标签名列表、评论数。</summary>
    public sealed class PostRow
    {
        public string Title { get; set; } = "";

        [MapPath("Author.Name")]
        public string? AuthorName { get; set; }

        [MapPath("Author.Company.Name", Join = true)]
        public string? CompanyName { get; set; }

        [MapPath("Tags.Name")]
        public List<string> TagNames { get; set; } = [];

        [MapExpression(nameof(CommentCount))]
        public long Comments { get; set; }

        private static Expression<Func<Post, long>> CommentCount => p => p.Comments!.Count();
    }

    [Example("query", "导航路径上的值：[MapPath] 平铺、[MapExpression] 额外的列")]
    public static Task<List<PostRow>> MapPath(EasyDb db) =>
        db.Query<Post>().Where(p => p.Id <= 3).OrderBy(p => p.Id).Select<PostRow>().ToListAsync();

    [Example("query", "可复用投影：[Projection] 方法")]
    public static Task<List<PostVo>> ProjectionMethod(EasyDb db) =>
        db.Query<Post>().Where(p => p.Views >= 2000).OrderBy(p => p.Id).Select(p => p.ToVo()).ToListAsync();

    /// <summary>帖子的投影结果。</summary>
    public sealed record PostVo(long Id, string Title, string? Author);

    // <snippet:query-projection>
    [Projection]
    public static PostVo ToVo(this Post p) => new(p.Id, p.Title, p.Author!.Name);
    // </snippet:query-projection>

    [Example("query", "实体去掉几列：Sql.Without")]
    public static Task<List<User>> Without(EasyDb db) =>
        db.Query<User>().Where(u => u.Id <= 2).Select(u => Sql.Without(u, u.JoinedAt, u.Level)).ToListAsync();

    [Example("query", "读成字典：Select<Dictionary<string, object?>>()")]
    public static Task<List<Dictionary<string, object?>>> SelectDictionary(EasyDb db) =>
        db.Query<Company>().OrderBy(c => c.Id).Take(2).Select<Dictionary<string, object?>>().ToListAsync();

    /// <summary>名字与职级的显示文字（读出后在客户端换算）。</summary>
    public sealed record NameLevel(string Name, string Level);

    [Example("query", "读出后在客户端换算：Sql.ClientSide")]
    public static Task<List<NameLevel>> ClientSide(EasyDb db) =>
        db.Query<User>().Where(u => u.Id <= 3).OrderBy(u => u.Id)
            .Select(u => new NameLevel(u.Name, Sql.ClientSide(u.Level, l => l == UserLevel.Lead ? "负责人" : "成员"))).ToListAsync();

    // ---- 合并、CTE、派生表

    /// <summary>名字与来源。</summary>
    public sealed record NamedRow(string Name, string Kind);

    [Example("query", "UNION ALL：结果继续排序、分页")]
    public static Task<List<NamedRow>> UnionAll(EasyDb db) =>
        db.Query<User>().Where(u => u.Age < 25).Select(u => new NamedRow(u.Name, "用户"))
            .UnionAll(db.Query<Company>().Where(c => c.City == "北京").Select(c => new NamedRow(c.Name, "公司")))
            .OrderBy(r => r.Name).Take(3).ToListAsync();

    /// <summary>作者与热门帖数。</summary>
    public sealed record AuthorHot(long AuthorId, long Hot);

    [Example("query", "CTE：AsCte 之后接着查")]
    public static Task<List<AuthorHot>> Cte(EasyDb db)
    {
        var hot = db.Query<Post>().Where(p => p.Views >= 1000).AsCte("hot_posts");
        return hot.GroupBy(p => p.AuthorId).Select(g => new AuthorHot(g.Key, g.Count())).OrderBy(r => r.AuthorId).ToListAsync();
    }

    [Example("query", "派生表：投影之后再过滤")]
    public static Task<List<AuthorHot>> DerivedTable(EasyDb db) =>
        db.Query<Post>().GroupBy(p => p.AuthorId).Select(g => new AuthorHot(g.Key, g.Count(p => p.Views >= 1000)))
            .Where(r => r.Hot >= 2).OrderBy(r => r.AuthorId).ToListAsync();

    // ---- 流式读取

    [Example("query", "流式读取：AsAsyncEnumerable 与 ToChunksAsync")]
    public static async Task<List<string>> Streaming(EasyDb db)
    {
        var names = new List<string>();
        await foreach (var name in db.Query<User>().OrderBy(u => u.Id).Select(u => u.Name).AsAsyncEnumerable())
            names.Add(name);
        await foreach (var chunk in db.Query<Tag>().OrderBy(t => t.Id).ToChunksAsync(4))
            names.Add($"{chunk.Count} 个标签");
        return names;
    }
}
