using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：dto/BlogQueryRequest（各种条件类型 + 排序）。</summary>
public class BlogQueryRequest : ISortRequest
{
    [Filter]
    public string? Title { get; set; }

    [Filter(Properties = ["Title", "Content"])]
    public string? Title2 { get; set; }

    [Filter(FilterOp.Equal, Properties = ["Id", "Content"])]
    public string? Title3 { get; set; }

    [Filter(Property = "Url")]
    public string? Content { get; set; }

    [Filter(FilterOp.Equal)]
    public int? Star { get; set; }

    [Filter(FilterOp.GreaterThanOrEqual, Property = "PublishTime")]
    public DateTime? PublishTimeBegin { get; set; }

    [Filter(FilterOp.LessThanOrEqual, Property = "PublishTime")]
    public DateTime? PublishTimeEnd { get; set; }

    [Filter(FilterOp.RangeClosed)]
    public DateTime[]? PublishTime { get; set; }

    [Filter(FilterOp.RangeClosed, Property = "PublishTime")]
    public List<DateTime>? PublishTimes { get; set; }

    [Filter(FilterOp.RangeOpen, Property = "PublishTime")]
    public List<DateTime>? PublishTimeOpen { get; set; }

    [Filter(FilterOp.RangeOpenClosed, Property = "PublishTime")]
    public List<DateTime>? PublishTimeOpenClosed { get; set; }

    [Filter(FilterOp.RangeClosedOpen, Property = "PublishTime")]
    public List<DateTime>? PublishTimeClosedOpen { get; set; }

    [Filter(FilterOp.GreaterThanOrEqual)]
    public decimal? Score { get; set; }

    [Filter(FilterOp.LessThanOrEqual)]
    public int? Status { get; set; }

    [Filter(FilterOp.GreaterThan)]
    public decimal? Order { get; set; }

    [Filter(FilterOp.NotEqual)]
    public bool? IsTop { get; set; }

    [Filter(FilterOp.In, Property = "Status")]
    public List<int> StatusList { get; set; } = [];

    [Filter(FilterOp.NotIn, Property = "Status")]
    public List<int> StatusNotList { get; set; } = [];

    public List<string> Orders { get; set; } = [];

    public void ConfigureSort(SortBuilder sort)
    {
        foreach (var order in Orders) sort.OrderBy(order);
    }
}

/// <summary>对应 Java：dto/TopicRequest（区间左开 + 只允许按 createTime 排序）。</summary>
public class TopicRequest : ISortRequest
{
    [Filter(FilterOp.GreaterThan, Property = "CreateTime")]
    public DateTime? CreateTimeBegin { get; set; }

    public List<string> Orders { get; set; } = [];

    public void ConfigureSort(SortBuilder sort)
    {
        sort.Allow("createTime");
        foreach (var order in Orders) sort.OrderBy(order);
    }
}

/// <summary>对应 Java：dto/BlogSortRequest（单个排序字段，方向为 null 时不排序）。</summary>
public class BlogSortRequest : ISortRequest
{
    public string? Sort { get; set; }

    public bool? Asc { get; set; }

    public void ConfigureSort(SortBuilder sort)
    {
        if (!string.IsNullOrWhiteSpace(Sort) && Asc is { } asc) sort.OrderBy(Sort, asc);
    }
}

/// <summary>对应 Java：dto/BlogSortMultiRequest / BlogSortJoinRequest（多个排序字段；JoinTable 为 true 时 createTime 用第二张表）。</summary>
public class BlogSortMultiRequest(bool joinTable = false) : ISortRequest
{
    public List<(string Property, bool Asc)> Orders { get; } = [];

    public void ConfigureSort(SortBuilder sort)
    {
        foreach (var (property, asc) in Orders) sort.OrderBy(property, asc, joinTable && property == "createTime" ? 1 : 0);
    }
}
