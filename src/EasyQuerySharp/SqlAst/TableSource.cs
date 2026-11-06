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

    /// <summary>
    /// 表前面的连接符改写（对应 Java 的 asTableLink）：参数是原来的连接符（<c>FROM</c>、<c>LEFT JOIN</c>…；写入的目标表是空串），
    /// 返回值替换它，如 <c>LEFT JOIN</c> → <c>LEFT JOIN LATERAL</c>；写入的目标表直接写在表名前面。为 null 时不改。
    /// </summary>
    public Func<string, string>? Link { get; init; }

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
    /// 分配别名前先多占几个编号。Java 版在一些地方多建匿名表（"分组后投影"的查询作为 JOIN 来源、取集合导航的某一行等），
    /// 外层看到的派生表别名因此跳号（如 t3 而不是 t2）；为了 SQL 逐字一致在这里复现。
    /// </summary>
    public int ReservedAliases { get; set; }

    /// <summary>
    /// 别名按创建顺序编号：先编内部 FROM 和前 N 个 JOIN，再编派生表自己，最后才是内部之后加上的 JOIN 与子查询。
    /// 分组合并的派生表在 Java 里一创建就占了编号，之后翻译聚合时才加的表排在它后面；为 null 时按默认的先内后外。
    /// </summary>
    public int? AliasAfterJoins { get; set; }
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

/// <summary>
/// 公共表表达式的引用：FROM / JOIN 里写 <c>`名字` t1</c>，定义 <c>WITH `名字` AS (SELECT …)</c> 由渲染器提到整条语句最前面
/// （对应 Java 的 toCteAs）。同名的定义只写一次；别名编号与派生表相同：先编内部，再编引用自己。
/// </summary>
public sealed class CteTableSource(string name, SelectQuery query) : TableSource
{
    /// <summary>CTE 的名字。</summary>
    public string Name { get; } = name;

    /// <summary>CTE 的定义。</summary>
    public SelectQuery Query { get; } = query;

    /// <summary>定义来自哪个查询对象（别名编号用：同一对象再作 CTE 时沿用它的编号）；为 null 时不区分。</summary>
    public object? DefinitionKey { get; init; }

    /// <summary>编号时在定义之后多占一个编号（分组后投影的查询作为 JOIN 来源时，与派生表的 <see cref="DerivedTableSource.ReservedAliases"/> 相同）。</summary>
    public bool ReserveExtraAlias { get; init; }

    /// <summary>
    /// 递归 CTE 的递归部分（树查询 AsTreeCte）：定义写成 <c>WITH RECURSIVE `名字` AS ( (基础)  UNION ALL  (递归) )</c>，
    /// 递归部分里按名字引用自己；为 null 时是普通 CTE。
    /// </summary>
    public SelectQuery? RecursivePart { get; init; }

    /// <summary>递归部分用 UNION ALL（默认）还是 UNION 连接。</summary>
    public bool UnionAll { get; init; } = true;

    /// <summary>定义编号完成时的位置，由 <see cref="Rendering.AliasAllocator"/> 写入：WITH 里按它排，定义里引用到的 CTE 在前。</summary>
    internal int DefinedAt { get; set; }
}
