using System.Reflection;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>lambda 参数绑定到的表来源：负责把 <c>u.Name</c> 这样的成员访问翻译成列。</summary>
internal abstract class SourceBinding
{
    /// <summary>表来源。</summary>
    public abstract TableSource Table { get; }

    /// <summary>成员对应的 SQL 表达式；不是列时返回 null。</summary>
    public abstract SqlExpr? Member(MemberInfo member);
}

/// <summary>绑定到实体表：成员按元数据映射成列。</summary>
internal sealed class EntityBinding(EntityTableSource table) : SourceBinding
{
    /// <inheritdoc />
    public override TableSource Table => table;

    /// <summary>实体表。</summary>
    public EntityTableSource EntityTable => table;

    /// <summary>实体元数据。</summary>
    public EntityMetadata Entity => table.Entity;

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) =>
        table.Entity.FindColumn(member.Name) is { } column ? table.Column(column) : null;
}

/// <summary>
/// 绑定到派生表：成员按内层投影的列名映射。内层投影的是整个实体时带上实体元数据，
/// 外层的列仍能拿到值转换器，也能按实体整体物化。
/// </summary>
internal sealed class DerivedBinding(TableSource table, IReadOnlyDictionary<string, string> memberColumns, EntityMetadata? entity)
    : SourceBinding
{
    /// <inheritdoc />
    public override TableSource Table => table;

    /// <summary>成员名 → 派生表里的列名。</summary>
    public IReadOnlyDictionary<string, string> MemberColumns => memberColumns;

    /// <summary>内层投影的实体；不是整体实体时为 null。</summary>
    public EntityMetadata? Entity => entity;

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) =>
        memberColumns.TryGetValue(member.Name, out var column)
            ? new ColumnExpr(table, column, entity?.FindColumn(member.Name))
            : null;
}
