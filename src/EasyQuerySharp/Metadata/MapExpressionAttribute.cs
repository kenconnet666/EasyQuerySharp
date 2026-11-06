namespace EasyQuerySharp.Metadata;

/// <summary>
/// 自动映射（<c>Select&lt;TDto&gt;()</c>）时这个 DTO 属性取一个表达式的值（对应 Java EXTRA_AUTO_INCLUDE_CONFIGURE 的 select）：
/// 指向 DTO 上的静态成员，类型是 <c>Expression&lt;Func&lt;源实体, 属性类型&gt;&gt;</c>，可以用导航、聚合、函数。
/// 列的顺序与 Java 一致：根查询里这些列写在最前面，带出查询里写在最后面。
/// </summary>
/// <example><code>
/// [MapExpression(nameof(CardCountSql))] public int? CardCount { get; set; }
/// static Expression&lt;Func&lt;SysUser, int?&gt;&gt; CardCountSql =&gt; u =&gt; u.BankCards!.Count();
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MapExpressionAttribute(string expressionMember) : Attribute
{
    /// <summary>提供表达式的静态成员名（属性或字段）。</summary>
    public string ExpressionMember { get; } = expressionMember;
}
