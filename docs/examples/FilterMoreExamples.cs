using EasyQuerySharp.Filtering;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"动态筛选与列表页"页（query/filter 之外）：多属性 OR、区间、集合导航、动态排序、按路径取列。</summary>
public static class FilterMoreExamples
{
    /// <summary>帖子搜索条件。</summary>
    public sealed class PostSearch
    {
        [Filter(Properties = [nameof(Post.Title), "Author.Name"])]
        public string? Keyword { get; set; }

        [Filter(FilterOp.RangeClosed, Property = nameof(Post.PublishedAt))]
        public DateOnly?[]? Published { get; set; }

        [Filter(FilterOp.Equal, Property = "Tags.Name")]
        public string? Tag { get; set; }
    }

    [Example("filter", "多属性 OR、区间、集合导航上的条件（EXISTS）")]
    public static Task<List<string>> FilterOperators(EasyDb db) =>
        db.Query<Post>()
            .FilterBy(new PostSearch { Keyword = "林", Published = [new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)], Tag = "ORM" })
            .OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();

    /// <summary>帖子排序请求：只允许按阅读量、发布日期排。</summary>
    public sealed class PostSort : ISortRequest
    {
        public string? By { get; set; }

        public bool Descending { get; set; }

        public void ConfigureSort(SortBuilder sort)
        {
            sort.Allow(nameof(Post.Views), nameof(Post.PublishedAt));
            if (By is not null) sort.OrderBy(By, ascending: !Descending);
        }
    }

    [Example("filter", "动态排序：SortBy，只认允许的属性")]
    public static Task<List<string>> SortBy(EasyDb db) =>
        db.Query<Post>().SortBy(new PostSort { By = "Views", Descending = true }).Take(3).Select(p => p.Title).ToListAsync();

    [Example("filter", "按路径取列：Sql.Column")]
    public static Task<List<string>> ColumnByPath(EasyDb db)
    {
        var path = "Author.Age";
        return db.Query<Post>().Where(p => Sql.Column<int?>(p, path) >= 35).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
    }
}
