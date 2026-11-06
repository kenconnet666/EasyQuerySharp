namespace EasyQuerySharp.Schema;

/// <summary>库里的一张表（<see cref="CodeFirst.ReadTablesAsync"/> 读出，当前 schema）。</summary>
/// <param name="Name">表名（不带 schema）。</param>
/// <param name="Comment">表注释；没有时为 null。</param>
/// <param name="Columns">列，按表里的顺序。</param>
/// <param name="PrimaryKey">主键列名，按表里的顺序；没有主键时为空。</param>
/// <param name="Indexes">索引（不含主键）。</param>
/// <param name="ForeignKeys">外键。</param>
public sealed record DbTableInfo(
    string Name, string? Comment, IReadOnlyList<DbColumnInfo> Columns, IReadOnlyList<string> PrimaryKey,
    IReadOnlyList<DbIndexInfo> Indexes, IReadOnlyList<DbForeignKeyInfo> ForeignKeys)
{
    /// <summary>按列名找列：先按原样比较，再忽略大小写（Oracle、达梦把不加引号的名字转成大写）；没有时返回 null。</summary>
    public DbColumnInfo? FindColumn(string name) =>
        Columns.FirstOrDefault(c => c.Name == name) ?? Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>库里的一列。</summary>
/// <param name="Name">列名。</param>
/// <param name="Type">库里的类型原文（如 <c>varchar(255)</c>、<c>character varying(255)</c>、<c>INTEGER</c>）。</param>
/// <param name="Nullable">是否可空。</param>
/// <param name="Default">DEFAULT 表达式原文；没有时为 null。</param>
/// <param name="Comment">列注释；没有时为 null。</param>
/// <param name="Generated">是否自增 / 标识列。</param>
public sealed record DbColumnInfo(string Name, string Type, bool Nullable, string? Default, string? Comment, bool Generated);

/// <summary>库里的一个索引（不含主键）。</summary>
/// <param name="Name">索引名。</param>
/// <param name="Columns">列名，按索引里的顺序。</param>
/// <param name="Unique">是否唯一索引。</param>
public sealed record DbIndexInfo(string Name, IReadOnlyList<string> Columns, bool Unique);

/// <summary>库里的一个外键。</summary>
/// <param name="Name">约束名（SQLite 的外键没有名字，按 <c>fk_表_序号</c> 命名）。</param>
/// <param name="Columns">本表的列。</param>
/// <param name="ReferencedTable">被引用的表。</param>
/// <param name="ReferencedColumns">被引用的列，与 <paramref name="Columns"/> 一一对应。</param>
public sealed record DbForeignKeyInfo(string Name, IReadOnlyList<string> Columns, string ReferencedTable, IReadOnlyList<string> ReferencedColumns);
