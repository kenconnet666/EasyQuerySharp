using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/Blog2Entity（StarStr 是由 star 算出的计算列，对应 Blog2StarToStringColumnValueSQLConverter）。</summary>
[Table("t_blog")]
public class Blog2Entity : BaseEntity
{
    public string? Title { get; set; }

    public int? Star { get; set; }

    [Computed(nameof(StarStrSql))]
    public string? StarStr { get; set; }

    private static Expression<Func<Blog2Entity, string?>> StarStrSql => b => b.Star.ToString();
}

/// <summary>对应 Java：entity/BlogEntity2（star 读成 decimal）。</summary>
[Table("t_blog")]
public class BlogEntity2 : BaseEntity
{
    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? Url { get; set; }

    public decimal? Star { get; set; }
}
