using System.Globalization;

namespace EasyQuerySharp.Filtering;

/// <summary>
/// 列表页的请求（对应 TS 版的 parseTableRequest，契约同组件库 useTableQuery 的地址栏参数），交给 <c>query.ToPageAsync(request)</c>：
/// <code>GET /orders?page=2&amp;pageSize=20&amp;sort=-amount,name&amp;total=368&amp;filter.status=draft&amp;filter.status=paid&amp;search.supplier=启航&amp;keyword=泵</code>
/// 表头的多选筛选（<c>filter.列</c>，IN）、关键字筛选（<c>search.列</c>，包含）、排序（<c>sort</c>，减号是倒序）只认结果里的列；
/// 其余参数（查询栏的条件）原样放在 <see cref="Params"/>，由调用方自己写成条件。前端已知总数时（只翻页、换排序）带上 <c>total</c>，不再执行 COUNT。
/// </summary>
public sealed record TableRequest
{
    /// <summary>每页条数的上限：超过时报错，不悄悄少给。</summary>
    public const int MaxPageSize = 2000;

    /// <summary>页码，从 1 开始。</summary>
    public long Page { get; init; } = 1;

    /// <summary>每页条数。</summary>
    public long PageSize { get; init; } = 10;

    /// <summary>排序，前面的优先；排在查询自己的排序之前。</summary>
    public IReadOnlyList<TableSort> Sorts { get; init; } = [];

    /// <summary>前端已知的总数（只翻页、换排序时带来）；有它时不再数行数。</summary>
    public long? Total { get; init; }

    /// <summary>表头的多选筛选 <c>filter.列</c>：列的值在其中（IN）。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Filters { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>表头的关键字筛选 <c>search.列</c>：文字包含（LIKE %值%）。</summary>
    public IReadOnlyDictionary<string, string> Searches { get; init; } = new Dictionary<string, string>();

    /// <summary>其余参数（查询栏的条件），同名参数按出现顺序收齐。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Params { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>解析地址栏的查询串（可以带开头的 <c>?</c>），如 ASP.NET Core 的 <c>Request.QueryString.Value</c>；页码、条数、总数不对时报错。</summary>
    public static TableRequest Parse(string? queryString) =>
        Parse((queryString ?? "").TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(pair =>
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            return separator < 0
                ? KeyValuePair.Create(Unescape(pair), (string?)"")
                : KeyValuePair.Create(Unescape(pair[..separator]), (string?)Unescape(pair[(separator + 1)..]));
        }));

    /// <summary>解析已经拆好的参数（同名参数多次出现）；页码、条数、总数不对时报错。</summary>
    public static TableRequest Parse(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var all = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (key, value) in parameters)
        {
            if (value is null) continue;
            if (!all.TryGetValue(key, out var values)) all[key] = values = [];
            values.Add(value);
        }
        string? One(string key) => all.TryGetValue(key, out var values) ? values[0] : null;

        var pageSize = Integer(One("pageSize") ?? "10", "pageSize", 1);
        if (pageSize > MaxPageSize) throw new ArgumentException($"pageSize 不能超过 {MaxPageSize}，实际是 {pageSize}");
        var filters = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var searches = new Dictionary<string, string>(StringComparer.Ordinal);
        var rest = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, values) in all)
        {
            if (key.StartsWith("filter.", StringComparison.Ordinal))
            {
                if (values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList() is { Count: > 0 } kept) filters[key["filter.".Length..]] = kept;
            }
            else if (key.StartsWith("search.", StringComparison.Ordinal))
            {
                if (values[0].Trim() is { Length: > 0 } text) searches[key["search.".Length..]] = text;
            }
            else if (key is not ("page" or "pageSize" or "sort" or "total")) rest[key] = values;
        }
        return new TableRequest
        {
            Page = Integer(One("page") ?? "1", "page", 1),
            PageSize = pageSize,
            Total = One("total") is { Length: > 0 } total ? Integer(total, "total", 0) : null,
            Sorts = ParseSort(One("sort") ?? ""),
            Filters = filters,
            Searches = searches,
            Params = rest,
        };
    }

    /// <summary>解析排序参数：<c>-amount,name</c>（减号是倒序），或 <c>amount desc, name asc</c>。</summary>
    private static List<TableSort> ParseSort(string sort) =>
        [.. (sort ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item =>
        {
            var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts[0].StartsWith('-')
                ? new TableSort(parts[0][1..], true)
                : new TableSort(parts[0], parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase));
        })];

    private static long Integer(string text, string name, long min) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min
            ? value
            : throw new ArgumentException($"{name} 必须是不小于 {min} 的整数，实际是 '{text}'");

    // 地址栏编码：%XX 与表示空格的 +
    private static string Unescape(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}

/// <summary>列表请求里的一项排序：结果里的列名（忽略大小写），是否倒序。</summary>
public readonly record struct TableSort(string Key, bool Descending);
