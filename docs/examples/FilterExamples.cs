using EasyQuerySharp.Filtering;
using EasyQuerySharp.Querying;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"查询 / 动态筛选"页的卡片：条件对象（FilterBy）与列表页请求（ToPageAsync(TableRequest)）。</summary>
public static class FilterExamples
{
    /// <summary>帖子的查询条件：有值的属性才成为条件。</summary>
    public sealed class PostQuery
    {
        [Filter]
        public string? Title { get; set; }

        [Filter(Property = "Author.Name")]
        public string? Author { get; set; }

        [Filter(FilterOp.GreaterThanOrEqual, Property = nameof(Post.Views))]
        public int? MinViews { get; set; }

        [Filter(FilterOp.In, Property = nameof(Post.Status))]
        public List<PostStatus>? Statuses { get; set; }
    }

    /// <summary>列表的一行。</summary>
    public sealed class PostRow
    {
        public string Title { get; set; } = "";

        public PostStatus Status { get; set; }

        public int Views { get; set; }

        public string? Author { get; set; }
    }

    [Example("query/filter", "按条件对象筛选：没填的属性不加条件，导航路径写成隐式 JOIN")]
    public static Task<List<PostRow>> FilterBy(EasyDb db) =>
        db.Query<Post>().FilterBy(new PostQuery { Author = "林", MinViews = 500 }).OrderBy(p => p.Id)
            .Select(p => new PostRow { Title = p.Title, Status = p.Status, Views = p.Views, Author = p.Author!.Name }).ToListAsync();

    [Example("query/filter", "列表页请求：表头筛选、关键字筛选、排序只认结果里的列")]
    public static Task<Page<PostRow>> TableRequest(EasyDb db)
    {
        var request = Filtering.TableRequest.Parse("page=1&pageSize=3&sort=-views&filter.status=published&search.title=的");
        return db.Query<Post>().Select(p => new PostRow { Title = p.Title, Status = p.Status, Views = p.Views, Author = p.Author!.Name }).ToPageAsync(request);
    }
}
