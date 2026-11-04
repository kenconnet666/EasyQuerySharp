namespace EasyQuerySharp;

/// <summary>
/// 写入影响的行数与期望不符（对应 Java 的 EasyQueryConcurrentException）：通常是乐观锁冲突，或者要更新的行已不存在。
/// </summary>
public sealed class ConcurrencyException(string message, long expectedRows, long actualRows) : Exception(message)
{
    /// <summary>期望影响的行数。</summary>
    public long ExpectedRows { get; } = expectedRows;

    /// <summary>实际影响的行数。</summary>
    public long ActualRows { get; } = actualRows;
}
