namespace EasyQuerySharp.Metadata;

/// <summary>
/// 表或列改名前的名字（对应 Java 的 <c>@Table(oldName)</c> / <c>@Column(oldName)</c>）：code-first 同步时库里没有新名字、
/// 只有旧名字，就把它改成新名字，而不是新建表或加列。
/// </summary>
/// <example><code>
/// [Table("t_topic2"), RenamedFrom("t_topic")] public class NewTopic { … }
/// [RenamedFrom("title")] public string? Abc { get; set; }
/// </code></example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class RenamedFromAttribute(string name) : Attribute
{
    /// <summary>改名前的表名或列名。</summary>
    public string Name { get; } = name;
}

/// <summary>
/// 表上的索引（对应 Java 的 <c>@TableIndex</c>）：code-first 建表时一起建，同步时补上库里没有的。
/// 不写 <see cref="Name"/> 时索引名是 <c>表名_列名…_idx</c>（唯一索引 <c>_uidx</c>，与 Java 一致）。
/// </summary>
/// <example><code>
/// [TableIndex(nameof(Column1), nameof(Column2))]
/// [TableIndex(nameof(Column2), Unique = true)]
/// public class M8TestIndex { … }
/// </code></example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class TableIndexAttribute(params string[] properties) : Attribute
{
    /// <summary>索引的列（属性名，按顺序）。</summary>
    public string[] Properties { get; } = properties;

    /// <summary>索引名；为 null 时按 <c>表名_列名…_idx</c> 生成。</summary>
    public string? Name { get; init; }

    /// <summary>是否唯一索引。</summary>
    public bool Unique { get; init; }

    /// <summary>降序的列（属性名），其余升序。</summary>
    public string[] Descending { get; init; } = [];

    /// <summary>索引注释（MySQL 写进 <c>COMMENT</c>，PostgreSQL 写成 <c>COMMENT ON INDEX</c>，其余方言忽略）。</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// 在对一导航上建外键（对应 Java 的 <c>@ForeignKey</c>）：本表的关联列引用目标表的关联列（默认是目标的主键）。
/// 外键名是 <c>本表_目标表_目标列…_fk</c>（与 Java 一致）。
/// </summary>
/// <example><code>
/// [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(UserId)]), ForeignKey(Action = "ON DELETE CASCADE")]
/// public User? User { get; set; }
/// </code></example>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class ForeignKeyAttribute : Attribute
{
    /// <summary>接在约束后面的引用动作，原样写入，如 <c>ON DELETE CASCADE</c>。</summary>
    public string? Action { get; init; }
}
