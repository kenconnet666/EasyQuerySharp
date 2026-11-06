namespace EasyQuerySharp.Querying;

/// <summary>分页结果。</summary>
/// <param name="Rows">当前页的行。</param>
/// <param name="Total">总行数。</param>
/// <param name="PageIndex">页码，从 1 开始。</param>
/// <param name="PageSize">每页行数。</param>
public sealed record Page<T>(IReadOnlyList<T> Rows, long Total, long PageIndex, long PageSize)
{
    /// <summary>总页数。</summary>
    public long PageCount => Total == 0 ? 0 : (Total + PageSize - 1) / PageSize;
}

/// <summary>分片查询的分页结果（<c>ToShardingPageAsync</c>，对应 Java 的 EasyShardingPageResult）。</summary>
/// <param name="Rows">当前页的行。</param>
/// <param name="Total">总行数（<paramref name="TotalLines"/> 之和）。</param>
/// <param name="PageIndex">页码，从 1 开始。</param>
/// <param name="PageSize">每页行数。</param>
/// <param name="TotalLines">最外层分片表命中的各节点的行数，按路由的顺序；翻页时传回，不小于 0 的项不再计数。</param>
public sealed record ShardingPage<T>(IReadOnlyList<T> Rows, long Total, long PageIndex, long PageSize, IReadOnlyList<long> TotalLines)
{
    /// <summary>总页数。</summary>
    public long PageCount => Total == 0 ? 0 : (Total + PageSize - 1) / PageSize;
}
