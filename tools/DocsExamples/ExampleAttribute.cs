namespace EasyQuerySharp.Docs;

/// <summary>
/// 把一个静态方法登记成文档卡片。方法签名是 <c>static Task&lt;T&gt; 名字(EasyDb db)</c>，返回卡片的结果；
/// 卡片 id 是 <c>页面/方法名</c>，页面如 <c>query/basics</c>。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExampleAttribute(string page, string title) : Attribute
{
    /// <summary>所在页面，如 <c>query/basics</c>。</summary>
    public string Page { get; } = page;

    /// <summary>卡片标题。</summary>
    public string Title { get; } = title;

    /// <summary>只在这个方言上有意义（用了方言专属函数）：其余方言不渲染。为 null 时所有方言都渲染。</summary>
    public string? Only { get; init; }

    /// <summary>已知还不支持的方言（逗号分隔，如 <c>mssql,oracle</c>）：快照里照样写出报错，但不算校验失败。</summary>
    public string Unsupported { get; init; } = "";
}
