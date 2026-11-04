using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.SqlAst;

/// <summary>
/// FROM / JOIN 里的一个表来源。别名在渲染前由 <see cref="Rendering.AliasAllocator"/> 统一分配：
/// 整条语句只有一个表来源时不加别名，否则按 <c>t</c>、<c>t1</c>、<c>t2</c>… 编号（与 Java 版一致）；用户指定的别名优先。
/// </summary>
public abstract class TableSource
{
    /// <summary>用户指定的别名（对应 Java 的 asAlias）；为 null 时由分配器决定。</summary>
    public string? ExplicitAlias { get; init; }

    /// <summary>渲染时使用的别名；为 null 表示不加别名。由 <see cref="Rendering.AliasAllocator"/> 写入。</summary>
    public string? Alias { get; internal set; }
}

/// <summary>实体对应的物理表。</summary>
public sealed class EntityTableSource(EntityMetadata entity) : TableSource
{
    /// <summary>实体元数据。</summary>
    public EntityMetadata Entity { get; } = entity;

    /// <summary>覆盖表名（对应 Java 的 asTable）；为 null 时用元数据里的表名。</summary>
    public string? TableNameOverride { get; init; }

    /// <summary>覆盖 schema（对应 Java 的 asSchema）；为 null 时用元数据里的 schema。</summary>
    public string? SchemaOverride { get; init; }

    /// <summary>实际使用的表名。</summary>
    public string TableName => TableNameOverride ?? Entity.TableName;

    /// <summary>实际使用的 schema。</summary>
    public string? Schema => SchemaOverride ?? Entity.Schema;

    /// <summary>按属性名取这张表上的列。</summary>
    public ColumnExpr Column(ColumnMetadata column) => new(this, column.Name, column);
}

/// <summary>派生表 <c>(SELECT …) t1</c>。</summary>
public sealed class DerivedTableSource(SelectQuery query) : TableSource
{
    /// <summary>内部查询。</summary>
    public SelectQuery Query { get; } = query;
}

/// <summary>原生 SQL 作为表来源：<c>(SELECT * FROM t_blog t) t</c>（对应 Java 的 queryable(sql, Class)）。</summary>
public sealed class RawTableSource(string sql) : TableSource
{
    /// <summary>原生 SQL（不含外层括号）。</summary>
    public string Sql { get; } = sql;
}
