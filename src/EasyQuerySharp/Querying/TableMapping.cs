using System.Collections.Immutable;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 查询级的换表（<c>MapTable</c> / <c>InSchema</c>，对应 Java fork 的 TableNameMapping）：这条查询翻译出的每张实体表——主表、联表、
/// 导航的隐式 JOIN 与子查询、分组合并、带出查询，以及嵌在里面的子查询——按实体换表名，或整体换 schema。
/// 显式写的表名（<c>AsTable</c>、<c>Sql.AsTable</c>）优先；嵌套的查询自己的映射优先于外层的。
/// </summary>
internal sealed record TableMapping(ImmutableDictionary<Type, string> Names, string? Schema)
{
    /// <summary>不换表。</summary>
    public static TableMapping None { get; } = new(ImmutableDictionary<Type, string>.Empty, null);

    /// <summary>没有任何映射。</summary>
    public bool IsEmpty => Names.IsEmpty && Schema is null;

    /// <summary>叠在外层查询的映射上：同一实体以本层的为准，本层没换 schema 时沿用外层的。</summary>
    public TableMapping Over(TableMapping outer) =>
        outer.IsEmpty ? this : IsEmpty ? outer : new(outer.Names.SetItems(Names), Schema ?? outer.Schema);

    /// <summary>
    /// 实体的表来源：<paramref name="tableName"/>（显式换的表名）优先，其次是映射的表名；schema 取映射的（没有时用实体自己的）。
    /// </summary>
    public EntityTableSource Table(EntityMetadata entity, string? tableName = null, string? alias = null,
        Func<string, string?, string>? segment = null, Func<string, string>? link = null) =>
        new(entity)
        {
            TableNameOverride = tableName ?? Names.GetValueOrDefault(entity.Type), SchemaOverride = Schema, ExplicitAlias = alias, Segment = segment, Link = link,
        };
}
