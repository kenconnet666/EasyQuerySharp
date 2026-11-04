namespace EasyQuerySharp.Metadata;

/// <summary>
/// 计算列：表里没有这一列，值由同一实体的其它列算出（对应 Java 的 sqlConversion 且 isRealColumn = false）。
/// <paramref name="expressionMember"/> 是实体上的静态属性或字段名，类型为 <c>Expression&lt;Func&lt;实体, 值&gt;&gt;</c>。
/// 查询时就地展开：SELECT 里写成 <c>表达式 AS 列名</c>，条件、排序里写成表达式本身；插入、更新不写这一列。
/// </summary>
/// <example><code>
/// [Computed(nameof(StarStrSql))] public string? StarStr { get; set; }
/// static Expression&lt;Func&lt;Blog2Entity, string?&gt;&gt; StarStrSql =&gt; b =&gt; b.Star.ToString();
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComputedAttribute(string expressionMember) : Attribute
{
    /// <summary>提供表达式的静态成员名。</summary>
    public string ExpressionMember { get; } = expressionMember;
}
