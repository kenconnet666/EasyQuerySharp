using EasyQuerySharp.Querying;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"分组与聚合"页：GroupBy / Having、按条件聚合、字符串聚合、按导航分组、整表聚合。</summary>
public static class GroupExamples
{
    /// <summary>订单的件数与金额。</summary>
    public sealed record OrderTotal(long OrderId, long Lines, int Quantity, decimal Amount);

    [Example("group", "分组聚合：Count / Sum")]
    public static Task<List<OrderTotal>> GroupBy(EasyDb db) =>
        db.Query<OrderItem>().GroupBy(i => i.OrderId)
            .Select(g => new OrderTotal(g.Key, g.Count(), g.Sum(i => i.Quantity), g.Sum(i => i.Price * i.Quantity)))
            .OrderBy(r => r.OrderId).ToListAsync();

    [Example("group", "分组后的条件与排序：Having、按聚合排序")]
    public static Task<List<OrderTotal>> Having(EasyDb db) =>
        db.Query<OrderItem>().GroupBy(i => i.OrderId)
            .Having(g => g.Count() >= 2)
            .OrderByDescending(g => g.Sum(i => i.Price * i.Quantity))
            .Select(g => new OrderTotal(g.Key, g.Count(), g.Sum(i => i.Quantity), g.Sum(i => i.Price * i.Quantity)))
            .ToListAsync();

    /// <summary>作者、状态与篇数。</summary>
    public sealed record AuthorStatusCount(long AuthorId, PostStatus Status, long Posts);

    [Example("group", "多列分组：键是匿名对象")]
    public static Task<List<AuthorStatusCount>> MultipleKeys(EasyDb db) =>
        db.Query<Post>().Where(p => p.AuthorId <= 3).GroupBy(p => new { p.AuthorId, p.Status })
            .Select(g => new AuthorStatusCount(g.Key.AuthorId, g.Key.Status, g.Count()))
            .OrderBy(r => r.AuthorId).ToListAsync();

    /// <summary>公司与帖子统计。</summary>
    public sealed record CompanyPosts(string? Company, long Posts, long Drafts, int? Best, string Titles);

    [Example("group", "按导航分组、按条件计数、字符串聚合")]
    public static Task<List<CompanyPosts>> ByNavigation(EasyDb db) =>
        db.Query<Post>().Where(p => p.Author!.CompanyId != null).GroupBy(p => p.Author!.Company!.Name)
            .Select(g => new CompanyPosts(g.Key, g.Count(), g.Count(p => p.Status == PostStatus.Draft), g.Max(p => (int?)p.Views), g.StringJoin(p => p.Title, "、")))
            .OrderBy(r => r.Company).ToListAsync();

    /// <summary>城市与去重后的公司数。</summary>
    public sealed record CityCompanies(string City, long Companies, long Users);

    [Example("group", "去重计数：g.Distinct()")]
    public static Task<List<CityCompanies>> CountDistinct(EasyDb db) =>
        db.Query<User>().Where(u => u.CompanyId != null).GroupBy(u => u.Company!.City)
            .Select(g => new CityCompanies(g.Key, g.Distinct().Count(u => u.CompanyId), g.Count()))
            .OrderBy(r => r.City).ToListAsync();

    [Example("group", "只要分组键：Keys()")]
    public static Task<List<long>> Keys(EasyDb db) =>
        db.Query<Comment>().GroupBy(c => c.PostId).Having(g => g.Sum(c => c.Likes) >= 20).OrderBy(g => g.Key).Keys().ToListAsync();

    /// <summary>整表聚合的几个值。</summary>
    public sealed record Totals(int Views, int Max, double? Average, long Posts);

    [Example("group", "整表聚合：SumAsync / MaxAsync / AverageAsync / CountAsync")]
    public static async Task<Totals> Aggregates(EasyDb db)
    {
        var published = db.Query<Post>().Where(p => p.Status == PostStatus.Published);
        return new Totals(
            await published.SumAsync(p => p.Views),
            await published.MaxAsync(p => p.Views),
            await published.AverageAsync(p => p.Views),
            await published.CountAsync());
    }

    /// <summary>ROLLUP 的一行：小计行里被汇总掉的键是 NULL。</summary>
    public sealed record RollupRow(long? UserId, long? ProductId, bool Subtotal, decimal Amount);

    /// <summary>（不是卡片）ROLLUP 小计：SQLite 不支持，示例库上跑不了，这里保证代码能编译。</summary>
    public static Query<RollupRow> Rollup(EasyDb db) =>
        // <snippet:group-rollup>
        db.Query<OrderItem>().GroupBy(i => new { i.Order!.UserId, i.ProductId }).Rollup()
            .Select(g => new RollupRow(g.Key.UserId, g.Key.ProductId, g.Grouping(g.Key.ProductId) == 1, g.Sum(i => i.Price * i.Quantity)));
        // </snippet:group-rollup>
}
