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
