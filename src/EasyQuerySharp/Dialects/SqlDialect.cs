using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>写一个函数调用的委托：参数已翻译好，用 <paramref name="r"/> 输出。</summary>
public delegate void FunctionWriter(SqlRenderer r, IReadOnlyList<SqlExpr> args);

/// <summary>
/// 方言：决定标识符引用、占位符、分页、函数等写法。只负责渲染，不依赖任何驱动。
/// 子类只覆盖与基类不同的部分，例如兼容库继承被兼容的方言（PolarDB 用 MySQL 方言）。
/// 函数写法集中在 <see cref="RegisterFunctions"/>：基类登记标准 SQL 的写法，子类覆盖差异。
/// </summary>
public abstract partial class SqlDialect
{
    private Dictionary<string, FunctionWriter>? _functions;

    /// <summary>方言名，用于报错与能力判断，如 <c>mysql</c>、<c>pgsql</c>。</summary>
    public abstract string Name { get; }

    /// <summary>给标识符加引号。默认用双引号（标准 SQL）。</summary>
    public virtual string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>驱动执行时的第 <paramref name="index"/> 个参数占位符（从 0 开始）；默认 <c>@p0</c>。</summary>
    public virtual string ParameterPlaceholder(int index) => "@p" + index;

    /// <summary>驱动参数对象的名字，与 <see cref="ParameterPlaceholder"/> 对应。</summary>
    public virtual string ParameterName(int index) => "p" + index;

    /// <summary>
    /// 插入后取自增键的方式：返回 null 表示在 INSERT 后加 <c>RETURNING 列</c> 直接读出；
    /// 否则是同一连接上紧接着执行的查询（如 MySQL 的 <c>SELECT LAST_INSERT_ID()</c>）。
    /// </summary>
    public virtual string? LastInsertIdQuery => null;

    /// <summary>驱动参数的补充设置（如 SQL Server 把 DateTime 按 datetime2 传）；默认不改。</summary>
    public virtual void ConfigureParameter(System.Data.Common.DbParameter parameter, SqlParameterValue value)
    {
    }

    /// <summary>写在 <c>SELECT [DISTINCT]</c> 与列清单之间的部分（SQL Server 的 <c>TOP n</c>）；默认不写。</summary>
    public virtual void WriteSelectHead(SqlRenderer renderer, SelectQuery query)
    {
    }

    /// <summary>写分页子句（追加在 ORDER BY 之后）。默认 <c>LIMIT n OFFSET m</c>。</summary>
    public virtual void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Limit is { } limit) renderer.Append(" LIMIT ").Append(limit.ToString(CultureInfo.InvariantCulture));
        if (query.Offset is > 0) renderer.Append(" OFFSET ").Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 写一条带分页的查询：<paramref name="writeUnpaged"/> 输出不含分页的完整 SELECT。
    /// 默认在其后追加 <see cref="WritePaging"/>；需要包一层的方言（Oracle 的 ROWNUM、SQL Server 2008 的 ROW_NUMBER）覆盖它。
    /// </summary>
    public virtual void WritePagedSelect(SqlRenderer renderer, SelectQuery query, Action writeUnpaged)
    {
        writeUnpaged();
        WritePaging(renderer, query);
    }

    /// <summary>EXISTS 子查询是否保留只取前几行的 LIMIT（集合导航的 Any 带 <c>LIMIT 1</c>，与 Java 一致）；HANA 的关联子查询不支持 LIMIT，覆盖成 false。</summary>
    public virtual bool LimitInExists => true;

    /// <summary>UNION 的各分支是否加括号 <c>( (q1)  UNION  (q2) )</c>；SQLite 不允许，覆盖成 false。</summary>
    public virtual bool ParenthesizeCompoundParts => true;

    /// <summary>递归 CTE 是否要在名字后写列名清单 <c>"cte" ("a","b") AS (…)</c>（Oracle 必须写）；默认不写。</summary>
    public virtual bool RecursiveCteColumnList => false;

    /// <summary>递归 CTE 的开头（树查询 AsTreeCte）：默认 <c>WITH RECURSIVE </c>；SQL Server、Oracle 不写 RECURSIVE。</summary>
    public virtual string RecursiveWith => "WITH RECURSIVE ";

    /// <summary>UPDATE 的 SET 列是否带表别名（<c>t.`a` = ?</c>）；PostgreSQL 不允许，覆盖成 false。</summary>
    public virtual bool QualifySetColumns => true;

    /// <summary>
    /// UPDATE / DELETE 的目标表带别名时（条件里有关联子查询等）不能直接写 <c>UPDATE 表 别名</c>，要改写成带 FROM 的形式，
    /// 交给 <see cref="WriteJoinedUpdate"/> / <see cref="WriteJoinedDelete"/>（SQL Server：<c>UPDATE t SET … FROM [表] t</c>、<c>DELETE t FROM [表] t</c>）。
    /// </summary>
    public virtual bool AliasedWriteUsesFrom => false;

    /// <summary>UPDATE / DELETE 目标表与别名之间的分隔；默认一个空格，SQLite 要求 <c> AS </c>。</summary>
    public virtual string WriteTargetAliasSeparator => " ";

    /// <summary>
    /// 写带连接的 UPDATE（SET 或条件里用到对一导航）。标准 SQL 没有统一写法，默认不支持；
    /// MySQL 写成 <c>UPDATE t INNER JOIN … SET …</c>，PostgreSQL 写成 <c>UPDATE t SET … FROM … WHERE</c>。
    /// </summary>
    public virtual void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update) =>
        throw new NotSupportedException($"{Name} 方言还不支持在 UPDATE 里使用对一导航（需要 JOIN），请改写成子查询");

    /// <summary>
    /// 连接的表逗号分隔，ON 条件按连接顺序并入 WHERE、排在原条件前面：<c>"b" t1 , "c" t2 WHERE ON… AND 原条件</c>。
    /// 写入时的导航连接都是 INNER JOIN，所以与 JOIN 写法等价（PostgreSQL 的 UPDATE … FROM / DELETE … USING、SQLite、EXISTS 子查询共用）。
    /// </summary>
    protected static void WriteCommaJoinedTables(SqlRenderer renderer, IReadOnlyList<JoinClause> joins, IReadOnlyList<SqlExpr> where)
    {
        for (var i = 0; i < joins.Count; i++)
        {
            if (i > 0) renderer.Append(" , ");
            renderer.WriteTable(joins[i].Table);
        }
        renderer.WriteWhere([.. joins.Where(j => j.On is not null).Select(j => j.On!), .. where]);
    }

    /// <summary>写带连接的 DELETE（条件里用到对一导航）。默认不支持；MySQL 写成 <c>DELETE t FROM … JOIN</c>，PostgreSQL 写成 <c>DELETE FROM … USING</c>。</summary>
    public virtual void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement) =>
        throw new NotSupportedException($"{Name} 方言还不支持在 DELETE 里使用对一导航（需要 JOIN），请改写成子查询");

    /// <summary>
    /// 写最外层查询末尾的行锁子句。默认是 MySQL 8、PostgreSQL、Oracle 共用的写法：
    /// <c> FOR UPDATE</c>、<c> FOR UPDATE NOWAIT</c>、<c> FOR UPDATE SKIP LOCKED</c>；不支持的方言覆盖成抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public virtual void WriteLock(SqlRenderer renderer, RowLock rowLock) => renderer.Append(rowLock switch
    {
        RowLock.ForUpdate => " FOR UPDATE",
        RowLock.ForUpdateNoWait => " FOR UPDATE NOWAIT",
        RowLock.ForUpdateSkipLocked => " FOR UPDATE SKIP LOCKED",
        _ => throw new ArgumentOutOfRangeException(nameof(rowLock), rowLock, null),
    });

    /// <summary>
    /// 写入某列的参数要包的类型转换（列声明了 <c>[Column(DbType = …)]</c> 时）：返回参数前后的文本，不需要时返回 null。
    /// 默认不转换；PG 的 json / jsonb 列写成 <c>(?)::jsonb</c>（驱动把字符串参数当 text 发送）。
    /// </summary>
    public virtual (string Before, string After)? ParameterWrap(string? columnDbType) => null;

    /// <summary>UPDATE / DELETE 能否带 <c>RETURNING</c> 返回受影响的行（PostgreSQL、SQLite）；默认不能，用到时渲染报不支持。</summary>
    public virtual bool SupportsWriteReturning => false;

    /// <summary>插入后读回生成列：写在列清单与 VALUES 之间的部分（SQL Server 的 <c>OUTPUT INSERTED.[id]</c>）；默认不写。</summary>
    public virtual void WriteInsertOutput(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning)
    {
    }

    /// <summary>插入后读回生成列：写在语句末尾的部分，默认 <c>RETURNING a,b</c>（PG、SQLite）。</summary>
    public virtual void WriteInsertReturning(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning) =>
        renderer.Append(" RETURNING ").WriteList(returning, ",");

    /// <summary>写 GROUP BY 之后的键。默认是标准写法 <c>ROLLUP(a,b)</c> / <c>CUBE(a,b)</c>；不支持的方言覆盖成抛出 <see cref="NotSupportedException"/>。</summary>
    public virtual void WriteGroupBy(SqlRenderer renderer, IReadOnlyList<SqlExpr> keys, GroupByMode mode)
    {
        if (mode == GroupByMode.Plain)
        {
            renderer.WriteList(keys, ",");
            return;
        }
        renderer.Append(mode == GroupByMode.Rollup ? "ROLLUP(" : "CUBE(").WriteList(keys, ",").Append(")");
    }

    /// <summary>写 LIKE。默认 <c>x LIKE ?</c>（模式里已拼好 %）；方言可按 <see cref="LikeExpr.Mode"/> 改写。</summary>
    public virtual void WriteLike(SqlRenderer renderer, LikeExpr predicate) =>
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE " : " LIKE ").Write(predicate.Pattern);

    /// <summary>LIKE 值里的转义：反斜杠、%、_ 前加反斜杠（与 Java 各方言的 LikeFunctionTemplateGenerator 一致）。</summary>
    protected static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// 指定了 NULL 位置时是否写原生的 <c>NULLS FIRST / LAST</c>（Java 的 OracleOrderByNullsModeSQLFunction）；
    /// 默认 false，用 CASE 模拟（Java 的 OrderByNullsModeSQLFunction，各库通用）。
    /// </summary>
    protected virtual bool NativeNullsOrder => false;

    /// <summary>
    /// 写 ORDER BY 的一项。指定了 NULL 位置时按 <see cref="NativeNullsOrder"/> 写成 <c>x ASC NULLS LAST</c>，
    /// 或 <c>CASE WHEN x IS NULL THEN 1 ELSE 0 END ASC,x ASC</c>；原生片段不追加方向。
    /// </summary>
    public virtual void WriteOrderByItem(SqlRenderer renderer, OrderByItem item)
    {
        var emulateNulls = item.Nulls is not null && !NativeNullsOrder;
        if (emulateNulls)
            renderer.Append("CASE WHEN ").Write(item.Expr).Append(item.Nulls == NullsOrder.Last ? " IS NULL THEN 1 ELSE 0 END ASC," : " IS NULL THEN 0 ELSE 1 END ASC,");
        renderer.Write(item.Expr);
        if (item.Raw) return;
        renderer.Append(item.Descending ? " DESC" : " ASC");
        if (item.Nulls is { } nulls && !emulateNulls) renderer.Append(nulls == NullsOrder.Last ? " NULLS LAST" : " NULLS FIRST");
    }

    /// <summary>取余 <c>(a % b)</c>；Oracle 系没有 % 运算符，写成 <c>MOD(a,b)</c>。</summary>
    public virtual void WriteModulo(SqlRenderer renderer, SqlExpr left, SqlExpr right) =>
        renderer.Append("(").Write(left).Append(" % ").Write(right).Append(")");

    /// <summary>
    /// 带组内排序的字符串聚合。默认是 PostgreSQL / SQLite 的写法：<c>STRING_AGG(x, ? ORDER BY …)</c>；
    /// MySQL 写成 <c>GROUP_CONCAT(x ORDER BY … SEPARATOR ?)</c>，SQL Server / Oracle 写成 <c>… WITHIN GROUP (ORDER BY …)</c>。
    /// </summary>
    public virtual void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join)
    {
        renderer.Append(join.Distinct ? "STRING_AGG(DISTINCT " : "STRING_AGG(").Write(join.Value).Append(", ").Write(join.Separator).Append(" ");
        WriteOrderBy(renderer, join.Orders);
        renderer.Append(")");
    }

    /// <summary>写 <c>ORDER BY a ASC,b DESC</c>（项之间不加空格，与 Java 一致）。</summary>
    protected void WriteOrderBy(SqlRenderer renderer, IReadOnlyList<OrderByItem> orders)
    {
        renderer.Append("ORDER BY ");
        for (var i = 0; i < orders.Count; i++)
        {
            if (i > 0) renderer.Append(",");
            WriteOrderByItem(renderer, orders[i]);
        }
    }

    /// <summary>带 schema 的表名。</summary>
    protected string QualifiedTable(Metadata.EntityMetadata entity) => QualifiedName(entity.Schema, entity.TableName);

    /// <summary>
    /// 带 schema 的表名：<c>`schema`.`table`</c>；schema 里的 <c>.</c> 分隔多级（如 SQL Server 的 <c>库.dbo</c>），各级分别加引号（与 Java 一致）。
    /// </summary>
    public string QualifiedName(string? schema, string table) =>
        schema is null ? QuoteIdentifier(table) : string.Join(".", schema.Split('.').Select(QuoteIdentifier)) + "." + QuoteIdentifier(table);

    /// <inheritdoc />
    public override string ToString() => Name;
}
