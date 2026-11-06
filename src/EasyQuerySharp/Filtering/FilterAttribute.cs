namespace EasyQuerySharp.Filtering;

/// <summary>
/// 条件对象（查询请求 DTO）上的属性如何变成查询条件（对应 Java 的 @EasyWhereCondition），配合 <c>query.FilterBy(request)</c> 使用。
/// 属性值为 null、空白字符串（除非 <see cref="AllowEmptyStrings"/>）、空集合时不加条件。
/// </summary>
/// <example><code>
/// public class BlogQuery
/// {
///     [Filter] public string? Title { get; set; }                                         // title LIKE '%x%'
///     [Filter(FilterOp.GreaterThanOrEqual, Property = "PublishTime")] public DateTime? From { get; set; }
///     [Filter(FilterOp.In, Property = "Status")] public List&lt;int&gt; Statuses { get; set; } = [];
///     [Filter(Properties = ["Title", "Content"])] public string? Keyword { get; set; }     // (title LIKE ? OR content LIKE ?)
///     [Filter(Property = "Bank.Name")] public string? BankName { get; set; }             // 导航：隐式 JOIN；集合导航：EXISTS
/// }
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterAttribute(FilterOp op = FilterOp.Default) : Attribute
{
    /// <summary>比较方式；<see cref="FilterOp.Default"/> 时字符串用 LIKE，其余用等于。</summary>
    public FilterOp Op { get; } = op;

    /// <summary>对应实体上的属性（可以是导航路径 <c>Bank.Name</c>，忽略大小写）；为 null 时用 DTO 属性自己的名字。</summary>
    public string? Property { get; init; }

    /// <summary>多个属性之间是 OR（对应 Java 的 MULTI_OR + propNames）；设置后忽略 <see cref="Property"/>。</summary>
    public string[]? Properties { get; init; }

    /// <summary>作用在第几个表来源上（FROM 是 0，每 JOIN 一个加 1；对应 Java 的 tableIndex）。</summary>
    public int Table { get; init; }

    /// <summary><see cref="Properties"/> 各自的表来源，缺省为 0（对应 Java 的 tablesIndex）。</summary>
    public int[]? Tables { get; init; }

    /// <summary>空白字符串也加条件（默认不加）。</summary>
    public bool AllowEmptyStrings { get; init; }
}

/// <summary>条件对象上属性的比较方式（对应 Java 的 EasyWhereCondition.Condition）。</summary>
public enum FilterOp
{
    /// <summary>字符串用 <see cref="Like"/>，其余用 <see cref="Equal"/>。</summary>
    Default,

    /// <summary><c>=</c></summary>
    Equal,

    /// <summary><c>&lt;&gt;</c></summary>
    NotEqual,

    /// <summary><c>&gt;</c>（Java 的 GREATER_THAN / RANGE_LEFT_OPEN）</summary>
    GreaterThan,

    /// <summary><c>&gt;=</c>（Java 的 GREATER_THAN_EQUAL / RANGE_LEFT_CLOSED）</summary>
    GreaterThanOrEqual,

    /// <summary><c>&lt;</c>（Java 的 LESS_THAN / RANGE_RIGHT_OPEN）</summary>
    LessThan,

    /// <summary><c>&lt;=</c>（Java 的 LESS_THAN_EQUAL / RANGE_RIGHT_CLOSED）</summary>
    LessThanOrEqual,

    /// <summary><c>LIKE '%v%'</c></summary>
    Like,

    /// <summary><c>LIKE 'v%'</c>（Java 的 LIKE_MATCH_LEFT）</summary>
    LikeStart,

    /// <summary><c>LIKE '%v'</c>（Java 的 LIKE_MATCH_RIGHT）</summary>
    LikeEnd,

    /// <summary>包含（Java 的 CONTAINS，同 <see cref="Sql.LikeContains"/>）：<c>LIKE CONCAT('%',?,'%')</c>，值里的 % 与 _ 按字面匹配。</summary>
    Contains,

    /// <summary>以值开头（Java 的 STARTS_WITH，同 <see cref="Sql.LikeStartsWith"/>）：<c>LIKE CONCAT(?,'%')</c>。</summary>
    StartsWith,

    /// <summary>以值结尾（Java 的 ENDS_WITH，同 <see cref="Sql.LikeEndsWith"/>）：<c>LIKE CONCAT('%',?)</c>。</summary>
    EndsWith,

    /// <summary><c>IN (…)</c>，值是集合。</summary>
    In,

    /// <summary><c>NOT IN (…)</c>，值是集合。</summary>
    NotIn,

    /// <summary><c>(a = ? OR a = ?)</c>，值是集合（Java 的 COLLECTION_EQUAL_OR）。</summary>
    AnyEqual,

    /// <summary>开区间 <c>(a &gt; ? AND a &lt; ?)</c>，值是两个元素的集合，第二个缺省时只比较下限。</summary>
    RangeOpen,

    /// <summary>闭区间 <c>(a &gt;= ? AND a &lt;= ?)</c>。</summary>
    RangeClosed,

    /// <summary>左闭右开 <c>(a &gt;= ? AND a &lt; ?)</c>。</summary>
    RangeClosedOpen,

    /// <summary>左开右闭 <c>(a &gt; ? AND a &lt;= ?)</c>。</summary>
    RangeOpenClosed,
}

/// <summary>
/// 动态排序请求（对应 Java 的 ObjectSort）：<c>query.SortBy(request)</c> 时调用 <see cref="ConfigureSort"/>，
/// 在 <see cref="SortBuilder"/> 上按属性名追加排序。
/// </summary>
public interface ISortRequest
{
    /// <summary>追加排序。</summary>
    void ConfigureSort(SortBuilder sort);
}

/// <summary>按属性名追加排序，可以限定允许排序的属性（防止前端传入任意列）。</summary>
public sealed class SortBuilder
{
    private readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase);

    internal List<(string Property, bool Ascending, int Table, SqlAst.NullsOrder? Nulls)> Items { get; } = [];

    /// <summary>只允许按这些属性排序；不调用时允许实体上的任何列。</summary>
    public SortBuilder Allow(params string[] properties)
    {
        _allowed.UnionWith(properties);
        return this;
    }

    /// <summary>追加一个排序；<paramref name="table"/> 是表来源序号（FROM 为 0），<paramref name="nulls"/> 指定 NULL 的位置（对应 Java 的 OrderByModeEnum）。</summary>
    public SortBuilder OrderBy(string property, bool ascending = true, int table = 0, SqlAst.NullsOrder? nulls = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(property);
        if (_allowed.Count > 0 && !_allowed.Contains(property))
            throw new InvalidOperationException($"不允许按 {property} 排序，允许的属性：{string.Join(", ", _allowed)}");
        Items.Add((property, ascending, table, nulls));
        return this;
    }
}
