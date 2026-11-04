namespace EasyQuerySharp.Metadata;

/// <summary>
/// 声明属性对应的列。不写时列名按命名约定由属性名推出，其余取默认值。
/// </summary>
/// <example><code>[Column(PrimaryKey = true)] public string Id { get; set; }</code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ColumnAttribute(string? name = null) : Attribute
{
    /// <summary>列名；为 null 时按命名约定推出。</summary>
    public string? Name { get; } = name;

    /// <summary>是否主键（可多个，组成复合主键）。</summary>
    public bool PrimaryKey { get; init; }

    /// <summary>值由数据库生成（自增等）：插入时不写这一列，插入后可回填。</summary>
    public bool Generated { get; init; }

    /// <summary>默认查询是否选出这一列；为 false 时只有显式选择才会出现在 SELECT 里（对应 Java 的 autoSelect）。</summary>
    public bool AutoSelect { get; init; } = true;

    /// <summary>值转换器类型，需实现 <see cref="IValueConverter"/> 并有无参构造函数。</summary>
    public Type? Converter { get; init; }

    /// <summary>数据库类型，code-first 建表时使用，如 <c>varchar(200)</c>。</summary>
    public string? DbType { get; init; }

    /// <summary>列注释，code-first 建表时使用。</summary>
    public string? Comment { get; init; }
}

/// <summary>属性不对应任何列（对应 Java 的 <c>@Column(exist = false)</c> / <c>@ColumnIgnore</c>）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ColumnIgnoreAttribute : Attribute;

/// <summary>插入时不写这一列。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InsertIgnoreAttribute : Attribute;

/// <summary>按实体更新时不写这一列（表达式更新里显式写的仍然生效）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UpdateIgnoreAttribute : Attribute;
