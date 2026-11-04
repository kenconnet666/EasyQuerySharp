namespace EasyQuerySharp.Metadata;

/// <summary>
/// 声明实体对应的表。不写时表名按命名约定由类名推出（默认驼峰转下划线）。
/// </summary>
/// <example><code>[Table("t_topic")] public class Topic { … }</code></example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TableAttribute(string? name = null) : Attribute
{
    /// <summary>表名；为 null 时按命名约定推出。</summary>
    public string? Name { get; } = name;

    /// <summary>所在 schema（MySQL 里是库名）；渲染为 <c>`schema`.`table`</c>。</summary>
    public string? Schema { get; init; }

    /// <summary>表注释，code-first 建表时使用。</summary>
    public string? Comment { get; init; }

    /// <summary>不映射的属性名（通常是基类里这张表没有的列，对应 Java 的 ignoreProperties）。</summary>
    public string[] IgnoreProperties { get; init; } = [];
}
