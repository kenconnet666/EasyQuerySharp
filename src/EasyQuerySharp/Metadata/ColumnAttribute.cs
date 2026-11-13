using System.Diagnostics.CodeAnalysis;

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

    /// <summary>
    /// 插入时这一列写成这段 SQL（对应 Java 的 generatedSQLColumnGenerator），如 <c>"mysqlNextId()"</c>、<c>"nextval('seq')"</c>；
    /// 通常与 <see cref="Generated"/> 一起用：值由数据库算出，实体上的值忽略。
    /// </summary>
    public string? GeneratedSql { get; init; }

    /// <summary>默认查询是否选出这一列；为 false 时只有显式选择才会出现在 SELECT 里（对应 Java 的 autoSelect）。</summary>
    public bool AutoSelect { get; init; } = true;

    /// <summary>主键生成器类型，需实现 <see cref="IKeyGenerator"/> 并有无参构造函数；插入时主键为 null 才生成。</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? KeyGenerator { get; init; }

    /// <summary>值转换器类型，需实现 <see cref="IValueConverter"/> 并有无参构造函数。</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? Converter { get; init; }

    /// <summary>列在 SQL 里的读写转换类型，需继承 <see cref="ColumnSqlConversion"/> 并有无参构造函数（如库端加解密）。</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? SqlConversion { get; init; }

    /// <summary>数据库类型，code-first 建表时使用，如 <c>varchar(200)</c>。</summary>
    public string? DbType { get; init; }

    /// <summary>列注释，code-first 建表时使用。</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 列的库端默认值，code-first 建表、加列时原样写在 <c>DEFAULT</c> 后面（对应 Java 的 dbDefault），是 SQL 文本而不是值：
    /// 字符串要自己带引号，如 <c>"''"</c>、<c>"CURRENT_TIMESTAMP(3)"</c>。只影响 DDL，插入时不会代替实体上的值；不能与 <see cref="Generated"/> 一起用。
    /// </summary>
    public string? DbDefault { get; init; }
}

/// <summary>属性不对应任何列（对应 Java 的 <c>@Column(exist = false)</c> / <c>@ColumnIgnore</c>）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ColumnIgnoreAttribute : Attribute;

/// <summary>插入时不写这一列。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InsertIgnoreAttribute : Attribute;

/// <summary>按实体更新时不写这一列（表达式更新里显式写的仍然生效）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UpdateIgnoreAttribute : Attribute
{
    /// <summary>
    /// 实体被追踪且这一列确实改了时仍然写（对应 Java 的 updateSetInTrackDiff）：整行更新时忽略、差异更新时照常写。
    /// </summary>
    public bool WhenTrackedChanged { get; init; }
}
