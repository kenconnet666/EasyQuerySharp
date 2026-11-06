using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>
/// 对应 Java 的 dameng/DamengBaseTest：达梦方言、UpperUnderlined 命名（<c>CreateTime</c> → <c>CREATE_TIME</c>）。
/// 达梦是第二档方言：用例只渲染 SQL、不连库（没有数据源），逐字比对 Java 的 SQL；
/// Java 只断言查询结果或只执行不断言的用例，这里核对 C# 渲染出的语句并记为偏离，实库验证在 ROADMAP 3.1 的冒烟里做。
/// </summary>
public abstract class DamengTest
{
    /// <summary>只生成 SQL 的达梦 EasyDb（对应 Java 的 entityQuery）。</summary>
    protected static EasyDb Db { get; } = new(o =>
    {
        o.UseDameng();
        o.NameConversion = NameConversion.UpperUnderlined;
    });
}
