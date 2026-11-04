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

    /// <summary>整个表引用的写法（对应 Java 的 asTableSegment）：(带引号的表名, 别名) → 片段；为 null 时写 <c>表名 别名</c>。</summary>
    public Func<string, string?, string>? Segment { get; init; }

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

    /// <summary>
    /// 分配别名前先多占一个编号。Java 版把"分组后投影"的查询作为 JOIN 来源时，内部多一层匿名表，
    /// 外层看到的派生表别名因此跳过一个（如 t3 而不是 t2）；为了 SQL 逐字一致在这里复现。
    /// </summary>
    public bool ReserveExtraAlias { get; init; }
}

/// <summary>
/// 原生 SQL 作为表来源：<c>(SELECT * FROM t_blog t) t</c>（对应 Java 的 queryable(sql, Class, params)）。
/// SQL 里的 <c>?</c> 依次对应 <see cref="Parameters"/>。
/// </summary>
public sealed class RawTableSource(string sql, IReadOnlyList<object?>? parameters = null) : TableSource
{
    /// <summary>原生 SQL（不含外层括号）。</summary>
    public string Sql { get; } = sql;

    /// <summary><c>?</c> 占位依次对应的参数值。</summary>
    public IReadOnlyList<object?> Parameters { get; } = parameters ?? [];
}

/// <summary>
/// 集合运算作为派生表：<c>( (SELECT …)  UNION  (SELECT …) ) t2</c>（对应 Java 的 union / unionAll）。
/// 每个分支各自编号别名，外层别名排在全部分支之后。
/// </summary>
public sealed class UnionTableSource(IReadOnlyList<SelectQuery> parts, bool all) : TableSource
{
    /// <summary>参与运算的各个查询，至少两个。</summary>
    public IReadOnlyList<SelectQuery> Parts { get; } = parts;

    /// <summary>true 为 UNION ALL（保留重复行），false 为 UNION。</summary>
    public bool All { get; } = all;

    /// <summary>各分支各自编号别名（见 <c>UnionSource.IndependentAliases</c>）。</summary>
    public bool IndependentAliases { get; init; }
}
