namespace EasyQuerySharp.Metadata;

/// <summary>
/// 自动映射（<c>SelectAutoInclude</c>）时这个 DTO 属性取导航路径上的值（对应 Java 的 <c>@NavigateFlat</c>）：
/// 路径全是对一导航时是 JOIN 出来的一列；最后经过一个集合导航时是这个集合上那一列的列表（带出查询）。
/// </summary>
/// <example><code>
/// [MapPath("Bank.Name")] public string? BankName { get; set; }          // 对一：JOIN t_bank 取 name
/// [MapPath("BankCards.Code")] public List&lt;string?&gt;? Codes { get; set; } // 对多：按用户分批查卡号
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MapPathAttribute(string path) : Attribute
{
    /// <summary>从源实体出发、用点分隔的属性路径（忽略大小写）。</summary>
    public string Path { get; } = path;
}
