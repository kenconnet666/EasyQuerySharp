namespace EasyQuerySharp;

/// <summary>查询结果超过了 <c>MaxResultSize</c> 设定的行数（对应 Java 的 EasyQueryResultSizeLimitException）。</summary>
public sealed class ResultSizeLimitException(int limit) : Exception($"查询结果超过了允许的 {limit} 行")
{
    /// <summary>允许的最大行数。</summary>
    public int Limit { get; } = limit;
}
