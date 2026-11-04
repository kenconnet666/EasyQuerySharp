namespace EasyQuerySharp.TestKit;

/// <summary>
/// 标出 C# 测试对应的 Java 用例（<c>文件#方法</c>，fork 用例以 <c>fork/</c> 开头），由 tools/ParityTests 统计对照进度。
/// 见 docs/decisions/0003-testing-and-parity.md。
/// </summary>
/// <example><code>
/// [Fact, Upstream("QueryTest.java#query0")]
/// [Fact, Upstream("QueryTest.java#query3", Deviation = "投影别名用成员名而不是 value1")]
/// [assembly: Upstream("h2/H2QueryTest.java#test1", NotApplicable = "H2 没有 .NET 驱动")]
/// </code></example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class UpstreamAttribute(string javaId) : Attribute
{
    /// <summary>Java 用例标识。</summary>
    public string JavaId { get; } = javaId;

    /// <summary>与 Java 的差异说明（SQL 或写法不同但能力对应）。</summary>
    public string? Deviation { get; init; }

    /// <summary>不适用的理由。</summary>
    public string? NotApplicable { get; init; }

    /// <summary>骨架，尚未移植。</summary>
    public bool Pending { get; init; }
}
