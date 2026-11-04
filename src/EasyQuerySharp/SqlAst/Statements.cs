namespace EasyQuerySharp.SqlAst;

/// <summary>JOIN 类型。</summary>
public enum JoinKind
{
    /// <summary>INNER JOIN</summary>
    Inner,
    /// <summary>LEFT JOIN</summary>
    Left,
    /// <summary>RIGHT JOIN</summary>
    Right,
    /// <summary>FULL JOIN</summary>
    Full,
    /// <summary>CROSS JOIN（没有 ON）</summary>
    Cross,
    /// <summary>逗号连接 <c>FROM a t , b t1</c>（对应 Java 的 from(T.class)），条件写在 WHERE 里</summary>
    Comma,
}

/// <summary>一个 JOIN 子句。</summary>
public sealed class JoinClause(JoinKind kind, TableSource table, SqlExpr? on)
{
    /// <summary>JOIN 类型。</summary>
    public JoinKind Kind { get; } = kind;

    /// <summary>被连接的表来源。</summary>
    public TableSource Table { get; } = table;

    /// <summary>ON 条件；CROSS JOIN 为 null。</summary>
    public SqlExpr? On { get; } = on;

    /// <summary>翻译时自动加上的 JOIN（导航的隐式 JOIN、分组合并），而不是查询上声明的 JOIN；别名编号排在声明的表之后。</summary>
    public bool Implicit { get; init; }
}

/// <summary>SELECT 列表里的一项。</summary>
public sealed class SelectColumn(SqlExpr expr, string? alias)
{
    /// <summary>列表达式。</summary>
    public SqlExpr Expr { get; } = expr;

    /// <summary>列别名；为 null 时不写 AS。</summary>
    public string? Alias { get; } = alias;
}

/// <summary>排序时 NULL 的位置。</summary>
public enum NullsOrder
{
    /// <summary>NULL 排在最前。</summary>
    First,

    /// <summary>NULL 排在最后。</summary>
    Last,
}

/// <summary>ORDER BY 里的一项。</summary>
public sealed class OrderByItem(SqlExpr expr, bool descending, bool raw = false, NullsOrder? nulls = null)
{
    /// <summary>NULL 的位置；为 null 时由数据库决定。写法由方言决定（见 <see cref="Dialects.SqlDialect.WriteOrderByItem"/>）。</summary>
    public NullsOrder? Nulls { get; } = nulls;

    /// <summary>排序表达式。</summary>
    public SqlExpr Expr { get; } = expr;

    /// <summary>是否降序。</summary>
    public bool Descending { get; } = descending;

    /// <summary>原生排序片段（自带方向，不再追加 ASC / DESC）。</summary>
    public bool Raw { get; } = raw;
}

/// <summary>一条 SELECT 语句（也用作子查询、派生表）。由查询编译器构建，渲染前不再修改。</summary>
public sealed class SelectQuery(TableSource from)
{
    /// <summary>FROM 的第一个表来源。</summary>
    public TableSource From { get; } = from;

    /// <summary>JOIN 子句，按书写顺序。</summary>
    public List<JoinClause> Joins { get; } = [];

    /// <summary>SELECT 列表；为空时渲染器会报错（编译器总会填入）。</summary>
    public List<SelectColumn> Columns { get; } = [];

    /// <summary>WHERE 条件，按 AND 连接。</summary>
    public List<SqlExpr> Where { get; } = [];

    /// <summary>GROUP BY。</summary>
    public List<SqlExpr> GroupBy { get; } = [];

    /// <summary>HAVING 条件，按 AND 连接。</summary>
    public List<SqlExpr> Having { get; } = [];

    /// <summary>ORDER BY。</summary>
    public List<OrderByItem> OrderBy { get; } = [];

    /// <summary>是否 DISTINCT。</summary>
    public bool Distinct { get; set; }

    /// <summary>跳过的行数。</summary>
    public long? Offset { get; set; }

    /// <summary>最多返回的行数。</summary>
    public long? Limit { get; set; }

    /// <summary>行锁；只有最外层查询的生效，作为子查询、派生表时忽略（与 Java 一致）。</summary>
    public RowLock Lock { get; set; }

    /// <summary>
    /// 是否有显式投影（select / 聚合选择器）。Java 版在单表查询上只有显式投影时才加别名 <c>t</c>，
    /// 默认的整实体查询与 COUNT(*) 不加。
    /// </summary>
    public bool ExplicitProjection { get; set; }

    /// <summary>全部表来源：FROM 与各 JOIN。</summary>
    public IEnumerable<TableSource> Tables => Joins.Select(j => j.Table).Prepend(From);
}

/// <summary>INSERT 语句：一行或多行。</summary>
public sealed class InsertStatement(EntityTableSource table, IReadOnlyList<ColumnExpr> columns, IReadOnlyList<IReadOnlyList<SqlExpr>> rows)
{
    /// <summary>目标表。</summary>
    public EntityTableSource Table { get; } = table;

    /// <summary>写入的列。</summary>
    public IReadOnlyList<ColumnExpr> Columns { get; } = columns;

    /// <summary>每行的值，与 <see cref="Columns"/> 一一对应。</summary>
    public IReadOnlyList<IReadOnlyList<SqlExpr>> Rows { get; } = rows;

    /// <summary>插入后返回的列（<c>RETURNING …</c>），用于回填自增键；不需要时为空。</summary>
    public IReadOnlyList<ColumnExpr> Returning { get; init; } = [];

    /// <summary>主键 / 唯一约束冲突时的处理（upsert）；普通插入为 null。写法由方言决定。</summary>
    public InsertConflict? Conflict { get; init; }
}

/// <summary>
/// 插入冲突的处理：<see cref="UpdateColumns"/> 为 null 表示忽略这一行，否则更新这些列（为空时等同普通插入）。
/// </summary>
/// <param name="ConflictColumns">约束列（PostgreSQL / SQLite 的 <c>ON CONFLICT (…)</c>）；可以为空。</param>
/// <param name="UpdateColumns">冲突时要更新的列。</param>
public sealed record InsertConflict(IReadOnlyList<ColumnExpr> ConflictColumns, IReadOnlyList<ColumnExpr>? UpdateColumns)
{
    /// <summary>是否忽略冲突的行。</summary>
    public bool Ignore => UpdateColumns is null;
}

/// <summary>UPDATE 语句。</summary>
public sealed class UpdateStatement(EntityTableSource table)
{
    /// <summary>目标表。</summary>
    public EntityTableSource Table { get; } = table;

    /// <summary>SET 子句，按书写顺序。</summary>
    public List<(ColumnExpr Column, SqlExpr Value)> Sets { get; } = [];

    /// <summary>WHERE 条件，按 AND 连接。</summary>
    public List<SqlExpr> Where { get; } = [];
}

/// <summary>DELETE 语句。</summary>
public sealed class DeleteStatement(EntityTableSource table)
{
    /// <summary>目标表。</summary>
    public EntityTableSource Table { get; } = table;

    /// <summary>WHERE 条件，按 AND 连接。</summary>
    public List<SqlExpr> Where { get; } = [];
}
