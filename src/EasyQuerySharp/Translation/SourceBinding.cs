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

    /// <summary>能做导航起点时的实体元数据（实体表，或投影整个实体的派生表）；否则为 null。</summary>
    public virtual EntityMetadata? OwnerEntity => null;

    /// <summary>作为导航起点时，本行上某一列的表达式。</summary>
    public virtual SqlExpr OwnerColumn(ColumnMetadata column) =>
        Member(column.Property) ?? throw new InvalidOperationException($"{GetType().Name} 上没有导航需要的列 {column.Name}");
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
    public override EntityMetadata? OwnerEntity => table.Entity;

    /// <inheritdoc />
    public override SqlExpr OwnerColumn(ColumnMetadata column) => table.Column(column);

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) =>
        table.Entity.FindColumn(member.Name) is { } column ? table.Column(column) : null;

    /// <summary>实体上的值对象 <c>x.Address</c>；不是值对象时为 null。</summary>
    public ValueObjectBinding? ValueObject(string member) =>
        Entity.ValueObjects.TryGetValue(member, out var valueObject) ? new ValueObjectBinding(this, valueObject) : null;
}

/// <summary>实体上的值对象 <c>x.Address</c>（见 <see cref="ValueObjectAttribute"/>）：它的成员是实体表上的列，嵌套的值对象再往下取。</summary>
internal sealed class ValueObjectBinding(EntityBinding owner, ValueObjectMetadata valueObject) : SourceBinding
{
    /// <inheritdoc />
    public override TableSource Table => owner.Table;

    /// <summary>值对象所在的实体表。</summary>
    public EntityBinding Owner => owner;

    /// <summary>值对象的元数据。</summary>
    public ValueObjectMetadata Metadata => valueObject;

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) =>
        owner.Entity.FindColumn(valueObject.Path + "." + member.Name) is { } column ? owner.EntityTable.Column(column) : null;

    /// <summary>嵌套的值对象 <c>x.Address.Geo</c>；不是值对象时为 null。</summary>
    public ValueObjectBinding? ValueObject(string member) => owner.ValueObject(valueObject.Path + "." + member);
}

/// <summary>
/// 分组参数 <c>g</c> 的绑定：<c>g.Key</c> / <c>g.Key.X</c> 翻译成分组键表达式，聚合里的元素 lambda 绑定到 <see cref="Element"/>。
/// 它不是表来源，<see cref="Table"/> 不可用。
/// </summary>
internal sealed class GroupBinding(IReadOnlyList<SqlExpr> keys, IReadOnlyList<string>? keyMembers, SourceBinding element) : SourceBinding
{
    /// <inheritdoc />
    public override TableSource Table => throw new InvalidOperationException("分组参数不是表来源");

    /// <summary>组内元素的绑定：单表时是那张表，多表时是 <see cref="TupleBinding"/>。</summary>
    public SourceBinding Element => element;

    /// <summary>单值键（<c>g.Key</c>）。</summary>
    public SqlExpr ScalarKey => keyMembers is null
        ? keys[0]
        : throw new NotSupportedException("分组键是多列（匿名类型），请访问它的成员，如 g.Key.Id");

    /// <summary>多列键的成员（<c>g.Key.X</c>）。</summary>
    public SqlExpr KeyMember(string name)
    {
        var index = keyMembers?.ToList().IndexOf(name) ?? -1;
        return index >= 0 ? keys[index] : throw new NotSupportedException($"分组键里没有成员 {name}");
    }

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) => null;
}

/// <summary>多表元素 <c>(T1, T2, …)</c> 的绑定：<c>e.Item2</c> 对应第二个表来源。</summary>
internal sealed class TupleBinding(IReadOnlyList<SourceBinding> items) : SourceBinding
{
    /// <inheritdoc />
    public override TableSource Table => throw new InvalidOperationException("元组不是表来源");

    /// <summary>按 ItemN 取对应的表来源绑定。</summary>
    public SourceBinding? Item(string memberName) =>
        memberName.StartsWith("Item", StringComparison.Ordinal) && int.TryParse(memberName.AsSpan(4), out var n) && n >= 1 && n <= items.Count
            ? items[n - 1]
            : null;

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) => null;
}

/// <summary>
/// 绑定到派生表：成员按内层投影的列名映射。内层投影的是整个实体时带上实体元数据，
/// 外层的列仍能拿到值转换器，也能按实体整体物化。
/// </summary>
internal sealed class DerivedBinding(
    TableSource table, IReadOnlyDictionary<string, string> memberColumns, EntityMetadata? entity, IReadOnlySet<string>? aliasedMembers = null,
    IReadOnlyDictionary<string, EntityMetadata>? nestedEntities = null)
    : SourceBinding
{
    /// <summary>
    /// 内层投影里嵌着的整个实体（<c>new { b, Rn = … }</c> 的 <c>b</c>、<c>Part</c> 的 <c>Entity</c>）：外层 <c>x.b.Star</c> 经它找列。
    /// 成员列以 <c>b.Star</c> 的形式登记；嵌着的是 DTO（如 <c>Part&lt;Dto, long&gt;</c>）时同样按前缀找列，只是没有实体元数据。
    /// 没有这个成员时返回 null。
    /// </summary>
    public DerivedBinding? Nested(string memberName)
    {
        var prefix = memberName + ".";
        var columns = memberColumns.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key[prefix.Length..], kv => kv.Value, StringComparer.Ordinal);
        return columns.Count == 0 ? null : new DerivedBinding(table, columns, nestedEntities?.GetValueOrDefault(memberName));
    }

    /// <summary>内层投影里嵌着的整个实体：成员名 → 实体元数据。</summary>
    public IReadOnlyDictionary<string, EntityMetadata> NestedEntities => nestedEntities ?? new Dictionary<string, EntityMetadata>();

    private static readonly HashSet<string> None = [];

    /// <summary>内层投影里写了 AS 的成员（外层再投影时同样写 AS）。</summary>
    public IReadOnlySet<string> AliasedMembers => aliasedMembers ?? None;

    /// <inheritdoc />
    public override TableSource Table => table;

    /// <summary>成员名 → 派生表里的列名。</summary>
    public IReadOnlyDictionary<string, string> MemberColumns => memberColumns;

    /// <summary>内层投影的实体；不是整体实体时为 null。</summary>
    public EntityMetadata? Entity => entity;

    /// <summary>
    /// 内层投影成 DTO、而 DTO 上声明了 [Navigate] 时，DTO 的元数据：外层可以经 DTO 上的导航隐式 JOIN（对应 Java 在 VO 上写 ）。
    /// </summary>
    public EntityMetadata? NavigationOwner { get; init; }

    /// <inheritdoc />
    public override EntityMetadata? OwnerEntity => entity ?? NavigationOwner;

    /// <inheritdoc />
    public override SqlExpr? Member(MemberInfo member) =>
        memberColumns.TryGetValue(member.Name, out var column)
            ? new ColumnExpr(table, column, entity?.FindColumn(member.Name)) { KeepAlias = AliasedMembers.Contains(member.Name) }
            : null;
}
