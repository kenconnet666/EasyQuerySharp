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
}

/// <summary>SELECT 列表里的一项。</summary>
public sealed class SelectColumn(SqlExpr expr, string? alias)
{
    /// <summary>列表达式。</summary>
    public SqlExpr Expr { get; } = expr;

    /// <summary>列别名；为 null 时不写 AS。</summary>
    public string? Alias { get; } = alias;
}

/// <summary>ORDER BY 里的一项。</summary>
public sealed class OrderByItem(SqlExpr expr, bool descending)
{
    /// <summary>排序表达式。</summary>
    public SqlExpr Expr { get; } = expr;

    /// <summary>是否降序。</summary>
    public bool Descending { get; } = descending;
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
