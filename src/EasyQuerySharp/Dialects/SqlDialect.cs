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

    /// <summary>
    /// 驱动执行时的第 <paramref name="index"/> 个参数占位符（从 0 开始），<paramref name="parameter"/> 是它的值与 CLR 类型；默认 <c>@p0</c>。
    /// 参数类型要写明时方言在这里写（ClickHouse 的 <c>{p0:DateTime64(7)}</c>、Db2 的 <c>CAST(@p0 AS INTEGER)</c>）。
    /// </summary>
    public virtual string ParameterPlaceholder(int index, SqlParameterValue parameter) => "@p" + index;

    /// <summary>
    /// 参数值是否直接写进 SQL（字面量由 <see cref="ParameterPlaceholder"/> 给出），执行时不绑定参数；默认 false。
    /// <see cref="SqlStatement.Text"/> 仍用 <c>?</c> 占位、<see cref="SqlStatement.Parameters"/> 仍记下各值，日志与跨方言比对不受影响。
    /// </summary>
    public virtual bool InlineParameters => false;

    /// <summary>驱动参数对象的名字，与 <see cref="ParameterPlaceholder"/> 对应。</summary>
    public virtual string ParameterName(int index) => "p" + index;

    /// <summary>
    /// 插入后取自增键的方式：返回 null 表示在 INSERT 后加 <c>RETURNING 列</c> 直接读出；
    /// 否则是同一连接上紧接着执行的查询（如 MySQL 的 <c>SELECT LAST_INSERT_ID()</c>）。
    /// </summary>
    public virtual string? LastInsertIdQuery => null;

    /// <summary>交给驱动的参数值：驱动不认的 CLR 类型在这里换成它认的（Oracle 的 Guid、DateOnly、TimeOnly）；默认原样。</summary>
    public virtual object? ParameterValue(SqlParameterValue parameter) => parameter.Value;

    /// <summary>驱动参数的补充设置（如 SQL Server 把 DateTime 按 datetime2 传）；默认不改。</summary>
    public virtual void ConfigureParameter(System.Data.Common.DbParameter parameter, SqlParameterValue value)
    {
    }

    /// <summary>写在 <c>SELECT [DISTINCT]</c> 与列清单之间的部分（SQL Server 的 <c>TOP n</c>）；默认不写。</summary>
    public virtual void WriteSelectHead(SqlRenderer renderer, SelectQuery query)
    {
    }

    /// <summary>写分页子句（追加在 ORDER BY 之后）。默认 <c>LIMIT n OFFSET m</c>；只跳过行数时 LIMIT 写 <see cref="OffsetOnlyLimit"/>（为 null 时不写）。</summary>
    public virtual void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Limit is { } limit) renderer.Append(" LIMIT ").Append(limit.ToString(CultureInfo.InvariantCulture));
        else if (query.Offset is > 0 && OffsetOnlyLimit is { } all) renderer.Append(" LIMIT ").Append(all);
        if (query.Offset is > 0) renderer.Append(" OFFSET ").Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// OFFSET 前必须有 LIMIT 的方言（MySQL、SQLite、HANA）在只跳过行数（<c>Skip(n)</c> 不带 <c>Take</c>）时写的"不限条数"；默认 null，只写 OFFSET。
    /// </summary>
    public virtual string? OffsetOnlyLimit => null;

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

    /// <summary>写 UNION 的一个分支：默认加括号 <c>(q)</c>（与 Java 一致，分支可以自带 ORDER BY 与分页）。</summary>
    public virtual void WriteCompoundPart(SqlRenderer renderer, SelectQuery part) => renderer.Append("(").WriteSelect(part).Append(")");

    /// <summary>分支自带排序或分页（如带出按父对象取段的 UNION ALL 模式）：不允许这样写的方言把它包成派生表。</summary>
    protected static bool IsOrderedOrPaged(SelectQuery part) => part.OrderBy.Count > 0 || part.Limit is not null || part.Offset is not null;

    /// <summary>递归 CTE 是否要在名字后写列名清单 <c>"cte" ("a","b") AS (…)</c>（Oracle 必须写）；默认不写。</summary>
    public virtual bool RecursiveCteColumnList => false;

    /// <summary>
    /// 树查询（AsTreeCte）的递归部分是否把子节点表写成逗号连接、连接条件并入 WHERE；默认写 <c>INNER JOIN … ON</c>（与 Java 一致）。
    /// Db2 的递归部分不能有带 ON 的显式连接（SQL0345N），覆盖成 true。
    /// </summary>
    public virtual bool RecursiveCteCommaJoin => false;

    /// <summary>递归 CTE 的开头（树查询 AsTreeCte）：默认 <c>WITH RECURSIVE </c>；SQL Server、Oracle 不写 RECURSIVE。</summary>
    public virtual string RecursiveWith => "WITH RECURSIVE ";

    /// <summary>树查询（AsTreeCte）起点行的层级值：默认 <c>0</c>；Kingbase 写 <c>0+0</c>（与 Java 的 KingbaseESCTERecursiveProvider 一致）。</summary>
    public virtual string TreeDeepStart => "0";

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
    /// 写不走连接的 UPDATE（RETURNING 之前的部分）。默认 <c>UPDATE 表 [别名] SET … WHERE …</c>；
    /// ClickHouse 写成 <c>ALTER TABLE 表 UPDATE … WHERE …</c>，没有 UPDATE 的库（TDengine）覆盖成抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public virtual void WriteUpdate(SqlRenderer renderer, UpdateStatement update) =>
        renderer.Append("UPDATE ").WriteWriteTarget(update.Table).Append(" SET ").WriteSets(update.Sets).WriteWhere(update.Where);

    /// <summary>写不走连接的 DELETE（RETURNING 之前的部分）。默认 <c>DELETE FROM 表 [别名] WHERE …</c>；ClickHouse 写成 <c>ALTER TABLE 表 DELETE WHERE …</c>。</summary>
    public virtual void WriteDelete(SqlRenderer renderer, DeleteStatement statement) =>
        renderer.Append("DELETE FROM ").WriteWriteTarget(statement.Table).WriteWhere(statement.Where);

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

    /// <summary>
    /// 按主键对上的 MERGE 写带连接的 UPDATE（没有 UPDATE … FROM / JOIN 的库：HANA、Db2）：
    /// <c>MERGE INTO "a" t USING (SELECT t."id" AS "eq_key_0",新值 AS "eq_value_0" FROM "a" t INNER JOIN … WHERE …) eq_src
    /// ON (t."id" = eq_src."eq_key_0") WHEN MATCHED THEN UPDATE SET t."c" = eq_src."eq_value_0"</c>。
    /// </summary>
    protected void WriteJoinedUpdateAsMerge(SqlRenderer renderer, UpdateStatement update)
    {
        var table = update.Table;
        var keys = table.Entity.KeyColumns;
        if (keys.Count == 0) throw new NotSupportedException($"{Name} 的带连接更新按主键对上（MERGE），{table.Entity.Type.Name} 没有主键");
        renderer.Append("MERGE INTO ").WriteTable(table).Append(" USING (SELECT ");
        for (var i = 0; i < keys.Count; i++) renderer.Append(i > 0 ? "," : "").Write(table.Column(keys[i])).Append(" AS " + QuoteIdentifier($"eq_key_{i}"));
        for (var i = 0; i < update.Sets.Count; i++) renderer.Append(",").Write(update.Sets[i].Value).Append(" AS " + QuoteIdentifier($"eq_value_{i}"));
        renderer.Append(" FROM ").WriteTable(table);
        foreach (var join in update.Joins) renderer.WriteJoin(join);
        renderer.WriteWhere(update.Where).Append(") eq_src ON (")
            .Append(string.Join(" AND ", keys.Select((k, i) => $"{table.Alias}.{QuoteIdentifier(k.Name)} = eq_src.{QuoteIdentifier($"eq_key_{i}")}")))
            .Append(") WHEN MATCHED THEN UPDATE SET ")
            .Append(string.Join(",", update.Sets.Select((s, i) => $"{table.Alias}.{QuoteIdentifier(s.Column.Name)} = eq_src.{QuoteIdentifier($"eq_value_{i}")}")));
    }

    /// <summary>EXISTS 子查询写带连接的 DELETE（DELETE 没有 USING / JOIN 的库：HANA、Db2）：<c>DELETE FROM "a" t WHERE EXISTS(SELECT 1 FROM "b" t1 WHERE 各 ON 条件 AND 原条件)</c>。</summary>
    protected static void WriteJoinedDeleteAsExists(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTable(statement.Table).Append(" WHERE EXISTS(SELECT 1 FROM ");
        WriteCommaJoinedTables(renderer, statement.Joins, statement.Where);
        renderer.Append(")");
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

    /// <summary>
    /// 是否有事务；ClickHouse、TDengine 没有（驱动的 BeginTransaction 直接报不支持）。没有事务时一批写入逐条执行、各自生效（中途失败不回滚），
    /// 显式开启的事务在执行第一条语句时报 <see cref="NotSupportedException"/>。
    /// </summary>
    public virtual bool SupportsTransactions => true;

    /// <summary>
    /// 带隔离级别的事务结束后、连接还回池之前执行的语句，把隔离级别改回默认；为 null 时不执行。
    /// 隔离级别是会话级、驱动归还连接时又不复原的库（HANA、SQL Server）要它，否则下一个借到这个连接的语句跟着用上一个事务的级别。
    /// </summary>
    public virtual string? ResetIsolationSql => null;

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
    /// 值里含 % 或 _ 时按字面匹配：值转义后拼好 % 作为参数，写 <c>x LIKE ?</c> 再接 <paramref name="escapeClause"/>
    /// （<c> ESCAPE '\'</c>；反斜杠本来就是转义符的库传空串）。<paramref name="escape"/> 默认是 <see cref="EscapeLike"/>，还有别的通配符的库（SQL Server 的 <c>[</c>）自己传。
    /// </summary>
    protected static void WriteEscapedLike(SqlRenderer renderer, LikeExpr predicate, string escapeClause, Func<string, string>? escape = null)
    {
        var escaped = (escape ?? EscapeLike)((string)((ParameterExpr)predicate.Value!).Value!);
        var pattern = predicate.Mode switch
        {
            LikeMode.StartsWith => escaped + "%",
            LikeMode.EndsWith => "%" + escaped,
            _ => "%" + escaped + "%",
        };
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE " : " LIKE ").Write(new ParameterExpr(pattern, typeof(string))).Append(escapeClause);
    }

    /// <summary>
    /// 指定了 NULL 位置时是否写原生的 <c>NULLS FIRST / LAST</c>（Java 的 OracleOrderByNullsModeSQLFunction）；
    /// 默认 false，用 CASE 模拟（Java 的 OrderByNullsModeSQLFunction，各库通用）。
    /// </summary>
    protected virtual bool NativeNullsOrder => false;

    /// <summary>
    /// 没有指定 NULL 位置时，数据库把 NULL 排在哪里（分库分表跨单元归并时按它在内存里排序）。
    /// 默认 NULL 最小：升序在前、降序在后（MySQL、SQLite、SQL Server、HANA）；PostgreSQL、Oracle、Db2 相反；DuckDB、ClickHouse 总在最后。
    /// </summary>
    public virtual NullsOrder DefaultNullsOrder(bool descending) => descending ? NullsOrder.Last : NullsOrder.First;

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
