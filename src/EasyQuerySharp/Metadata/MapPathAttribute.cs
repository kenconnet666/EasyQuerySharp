namespace EasyQuerySharp.Metadata;

/// <summary>
/// 自动映射（<c>SelectAutoInclude</c>）时这个 DTO 属性取导航路径上的值（对应 Java 的 <c>@NavigateFlat</c> / <c>@NavigateJoin</c>）：
/// <list type="bullet">
/// <item>路径全是对一导航：默认像 <c>@NavigateFlat</c> 那样走带出查询，同一导航上的几个平铺属性共用一条
/// <c>SELECT 列…, 键 AS __relation__键 FROM 目标表 WHERE 键 IN (…)</c>；<see cref="Join"/> 为 true 时像 <c>@NavigateJoin</c> 那样在主查询里 JOIN；</item>
/// <item>经过集合导航：结果是列表，按父对象分批查询。</item>
/// </list>
/// </summary>
/// <example><code>
/// [MapPath("User.Phone")] public string? UserPhone { get; set; }                 // 带出查询取用户的手机号
/// [MapPath("User.Name", Join = true)] public string? UserName { get; set; }      // 主查询 LEFT JOIN 用户表
/// [MapPath("BankCards.Code")] public List&lt;string?&gt;? Codes { get; set; }       // 对多：按用户分批查卡号
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MapPathAttribute(string path) : Attribute
{
    /// <summary>从源实体出发、用点分隔的属性路径（忽略大小写）。</summary>
    public string Path { get; } = path;

    /// <summary>对一路径在主查询里 JOIN 取值（Java 的 @NavigateJoin），而不是走带出查询。</summary>
    public bool Join { get; init; }
}
