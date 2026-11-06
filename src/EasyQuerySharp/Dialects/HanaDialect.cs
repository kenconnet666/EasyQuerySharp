using System.Globalization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// SAP HANA 2.0 方言（对应 Java fork 的 HANA 方言）。标识符用双引号，占位符 <c>?</c>，分页 <c>LIMIT n OFFSET m</c>。
/// 驱动用 HANA 客户端自带的 ADO.NET 驱动（Sap.Data.Hana，不在 nuget.org 上）：<c>o.UseHana(dataSource)</c>。
/// HANA 与 Oracle、PostgreSQL 都只有部分相同，所以直接继承标准写法，差异如下：
/// <list type="bullet">
/// <item>拼接逐对写 <c>((a || b) || c)</c>；日期格式化用 <c>TO_NVARCHAR</c>，格式串里的文字单独拼接；条件当值写成三值的 CASE。</item>
/// <item>集合导航的 EXISTS 子查询不写 <c>LIMIT 1</c>（HANA 的关联子查询不支持 LIMIT）。</item>
/// <item>冲突处理写成 <c>MERGE … USING (SELECT … FROM DUMMY)</c>；带对一导航的更新写成按主键对上的 MERGE，删除写成 EXISTS。</item>
/// <item>插入后用 <c>CURRENT_IDENTITY_VALUE()</c> 在同一连接上取自增键（INSERT 没有 RETURNING）。</item>
/// <item>建表用 <c>CREATE COLUMN TABLE</c>；没有 IF [NOT] EXISTS，建表、删表包在匿名块里忽略"已存在 / 不存在"。</item>
/// <item>跳过被锁的行写 <c>FOR UPDATE IGNORE LOCKED</c>；没有递归 CTE。</item>
/// </list>
/// </summary>
public class HanaDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static HanaDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "hana";

    /// <summary>HANA 的客户端驱动只认位置参数 <c>?</c>。</summary>
    public override string ParameterPlaceholder(int index) => "?";

    /// <summary>INSERT 没有 RETURNING：在同一连接上取本会话最后生成的 IDENTITY。</summary>
    public override string? LastInsertIdQuery => "SELECT CURRENT_IDENTITY_VALUE() FROM DUMMY";

    /// <summary>SET 列不带表别名（标准写法；带别名的更新只出现在 MERGE 里）。</summary>
    public override bool QualifySetColumns => false;

    /// <summary>HANA 的关联子查询不支持 LIMIT，EXISTS 只看有没有行，去掉也不改变结果（与 Java fork 一致）。</summary>
    public override bool LimitInExists => false;

    /// <summary>HANA 没有 % 运算符：<c>MOD(a,b)</c>。</summary>
    public override void WriteModulo(SqlRenderer renderer, SqlExpr left, SqlExpr right) =>
        renderer.Append("MOD(").Write(left).Append(",").Write(right).Append(")");

    /// <summary>HANA 2.0 没有 WITH RECURSIVE（层级数据用 HIERARCHY 函数），树查询 AsTreeCte 不可用。</summary>
    public override string RecursiveWith => throw new NotSupportedException("SAP HANA 2.0 不支持递归 CTE（WITH RECURSIVE），树查询请改用 HIERARCHY 函数或逐层查询");

    /// <summary>OFFSET 前必须有 LIMIT：只跳过行数时写 <c>LIMIT 2147483647 OFFSET m</c>。</summary>
    public override void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Limit is null && query.Offset is not > 0) return;
        renderer.Append(" LIMIT ").Append((query.Limit ?? int.MaxValue).ToString(CultureInfo.InvariantCulture));
        if (query.Offset is > 0) renderer.Append(" OFFSET ").Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>跳过被锁的行是 <c>FOR UPDATE IGNORE LOCKED</c>，其余同标准写法。</summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock)
    {
        if (rowLock == RowLock.ForUpdateSkipLocked) renderer.Append(" FOR UPDATE IGNORE LOCKED");
        else base.WriteLock(renderer, rowLock);
    }

    /// <summary>值里含 % 或 _ 时转义后写 <c>x LIKE ? ESCAPE '\'</c>，值里的通配符按字面匹配。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        var text = EscapeLike((string)((ParameterExpr)predicate.Value).Value!);
        var pattern = predicate.Mode switch
        {
            LikeMode.StartsWith => text + "%",
            LikeMode.EndsWith => "%" + text,
            _ => "%" + text + "%",
        };
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE " : " LIKE ").Write(new ParameterExpr(pattern, typeof(string))).Append(" ESCAPE '\\'");
    }

    /// <summary>STRING_AGG 只收字符串、不支持 DISTINCT：<c>STRING_AGG(TO_NVARCHAR(x), ? ORDER BY …)</c>。</summary>
    public override void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join)
    {
        if (join.Distinct) throw new NotSupportedException("SAP HANA 的 STRING_AGG 不支持 DISTINCT");
        renderer.Append("STRING_AGG(TO_NVARCHAR(").Write(join.Value).Append("), ").Write(join.Separator).Append(" ");
        WriteOrderBy(renderer, join.Orders);
        renderer.Append(")");
    }

    /// <summary>
    /// 带对一导航的更新写成按主键对上的 MERGE（与 Java fork 逐字一致）：
    /// <c>MERGE INTO "a" t USING (SELECT t."id" AS "eq_key_0",值 AS "eq_value_0" FROM "a" t INNER JOIN "b" t1 ON … WHERE …) eq_src
    /// ON (t."id" = eq_src."eq_key_0") WHEN MATCHED THEN UPDATE SET t."c" = eq_src."eq_value_0"</c>。
    /// </summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        var table = update.Table;
        var keys = table.Entity.KeyColumns;
        if (keys.Count == 0) throw new NotSupportedException($"SAP HANA 的带连接更新按主键对上（MERGE），{table.Entity.Type.Name} 没有主键");
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

    /// <summary><c>DELETE FROM "a" t WHERE EXISTS(SELECT 1 FROM "b" t1 WHERE 各 ON 条件 AND 原条件)</c>：HANA 的 DELETE 没有 USING / JOIN。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTable(statement.Table).Append(" WHERE EXISTS(SELECT 1 FROM ");
        WriteCommaJoinedTables(renderer, statement.Joins, statement.Where);
        renderer.Append(")");
    }

    /// <inheritdoc />
    public override bool InsertConflictUsesMerge => true;

    /// <summary>MERGE 的来源查询从 DUMMY 取值。</summary>
    protected override string MergeSourceSuffix => " FROM DUMMY";

    /// <summary>HANA 的类型转换：日期用 TO_DATE / TO_TIME / TO_TIMESTAMP，文字用 TO_NVARCHAR。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "(CASE WHEN {0} IN ('1','true','TRUE') THEN TRUE ELSE FALSE END)",
        _ when targetType == typeof(char) => "SUBSTRING(TO_NVARCHAR({0}),1,1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "TO_TIME({0})",
        _ when targetType == typeof(DateOnly) => "TO_DATE({0})",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "TO_TIMESTAMP({0})",
        _ when targetType == typeof(decimal) => "CAST({0} AS DECIMAL(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "CAST({0} AS DOUBLE)",
        _ when targetType == typeof(long) || targetType == typeof(uint) || targetType == typeof(ulong) => "CAST({0} AS BIGINT)",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) || targetType == typeof(short) => "CAST({0} AS SMALLINT)",
        _ when IsInteger(targetType) => "CAST({0} AS INTEGER)",
        _ when targetType == typeof(Guid) || targetType == typeof(string) => "TO_NVARCHAR({0})",
        _ => base.CastTemplate(targetType),
    };

    /// <summary>HANA 的类型名：NVARCHAR、DOUBLE、TINYINT；没有带时区的时间类型。</summary>
    protected override string ClrColumnType(Type type, ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) => "NVARCHAR(255)",
        _ when type == typeof(byte) => "TINYINT",
        _ when type == typeof(double) => "DOUBLE",
        _ when type == typeof(Guid) => "NVARCHAR(36)",
        _ when type == typeof(DateTimeOffset) => throw new NotSupportedException($"SAP HANA 没有带时区的时间类型，{column} 请用 [Column(DbType = \"TIMESTAMP\")] 并统一存 UTC"),
        _ => base.ClrColumnType(type, column),
    };

    /// <summary>
    /// 列存表 <c>CREATE COLUMN TABLE</c>。HANA 没有 IF NOT EXISTS：放进匿名块执行，表已存在（错误 288）时什么也不做。
    /// </summary>
    protected override string CreateTableStatement(EntityMetadata entity, string body) =>
        IgnoringError(288, $"CREATE COLUMN TABLE {QualifiedTable(entity)} ({body})");

    /// <summary>HANA 没有 DROP TABLE IF EXISTS：放进匿名块执行，表不存在（错误 259）时什么也不做。</summary>
    public override string DropTableIfExistsSql(EntityMetadata entity) => IgnoringError(259, $"DROP TABLE {QualifiedTable(entity)}");

    /// <summary>改表名：<c>RENAME TABLE 旧表 TO "新表"</c>。</summary>
    internal override string RenameTableSql(EntityMetadata entity, string oldName) =>
        $"RENAME TABLE {QualifiedName(entity.Schema, oldName)} TO {QuoteIdentifier(entity.TableName)}";

    /// <summary>改列名：<c>RENAME COLUMN 表."旧列" TO "新列"</c>。</summary>
    internal override string RenameColumnSql(EntityMetadata entity, string oldName, ColumnMetadata column) =>
        $"RENAME COLUMN {QualifiedTable(entity)}.{QuoteIdentifier(oldName)} TO {QuoteIdentifier(column.Name)}";

    /// <summary>加列：<c>ALTER TABLE 表 ADD (列定义)</c>。</summary>
    internal override string AddColumnSql(EntityMetadata entity, ColumnMetadata column) =>
        $"ALTER TABLE {QualifiedTable(entity)} ADD ({ColumnDefinition(entity, column)})";

    /// <summary>读 SYS 系统视图（CURRENT_SCHEMA，同 TS 版）。</summary>
    internal override CatalogQueries CatalogQueries { get; } = new(
        "SELECT TABLE_NAME AS \"table_name\", COMMENTS AS \"comment\" FROM SYS.TABLES WHERE SCHEMA_NAME = CURRENT_SCHEMA ORDER BY TABLE_NAME",
        "SELECT c.TABLE_NAME AS \"table_name\", c.COLUMN_NAME AS \"column_name\", "
        + "CASE WHEN c.DATA_TYPE_NAME IN ('VARCHAR', 'NVARCHAR', 'VARBINARY') THEN c.DATA_TYPE_NAME || '(' || c.LENGTH || ')' "
        + "WHEN c.DATA_TYPE_NAME = 'DECIMAL' AND c.SCALE IS NOT NULL THEN 'DECIMAL(' || c.LENGTH || ', ' || c.SCALE || ')' ELSE c.DATA_TYPE_NAME END AS \"column_type\", "
        + "c.IS_NULLABLE AS \"is_nullable\", c.DEFAULT_VALUE AS \"column_default\", c.COMMENTS AS \"comment\", "
        + "CASE WHEN c.GENERATION_TYPE IS NULL THEN 0 ELSE 1 END AS \"is_generated\", "
        + "CASE WHEN EXISTS (SELECT 1 FROM SYS.CONSTRAINTS k WHERE k.SCHEMA_NAME = c.SCHEMA_NAME AND k.TABLE_NAME = c.TABLE_NAME "
        + "AND k.COLUMN_NAME = c.COLUMN_NAME AND k.IS_PRIMARY_KEY = 'TRUE') THEN 1 ELSE 0 END AS \"primary_key\" "
        + "FROM SYS.TABLE_COLUMNS c WHERE c.SCHEMA_NAME = CURRENT_SCHEMA ORDER BY c.TABLE_NAME, c.POSITION",
        "SELECT i.TABLE_NAME AS \"table_name\", i.INDEX_NAME AS \"index_name\", "
        + "CASE WHEN i.CONSTRAINT IN ('UNIQUE', 'NOT NULL UNIQUE') THEN 1 ELSE 0 END AS \"is_unique\", i.COLUMN_NAME AS \"column_name\" "
        + "FROM SYS.INDEX_COLUMNS i WHERE i.SCHEMA_NAME = CURRENT_SCHEMA AND (i.CONSTRAINT IS NULL OR i.CONSTRAINT <> 'PRIMARY KEY') "
        + "ORDER BY i.TABLE_NAME, i.INDEX_NAME, i.POSITION",
        "SELECT TABLE_NAME AS \"table_name\", CONSTRAINT_NAME AS \"constraint_name\", COLUMN_NAME AS \"column_name\", "
        + "REFERENCED_TABLE_NAME AS \"referenced_table\", REFERENCED_COLUMN_NAME AS \"referenced_column\" "
        + "FROM SYS.REFERENTIAL_CONSTRAINTS WHERE SCHEMA_NAME = CURRENT_SCHEMA ORDER BY TABLE_NAME, CONSTRAINT_NAME, POSITION");

    // 匿名块里动态执行一条 DDL，遇到指定的错误码直接结束（HANA 2.0 SPS 02 起支持 DO BEGIN … END）
    private static string IgnoringError(int errorCode, string ddl) =>
        $"DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE {errorCode} BEGIN END; EXEC '{ddl.Replace("'", "''", StringComparison.Ordinal)}'; END";

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 不能直接把条件当值选出来；三个分支保留 NULL（与 Java fork 一致）
        f[SqlFunctionNames.BooleanValue] = Template("(CASE WHEN {0} THEN TRUE WHEN NOT ({0}) THEN FALSE ELSE NULL END)");
        f[SqlFunctionNames.Concat] = PairwiseConcat;
        f[SqlFunctionNames.Length] = Call("LENGTH");
        f[SqlFunctionNames.SubString] = Call("SUBSTRING");
        // LOCATE(字符串,子串)；C# 的 IndexOf 是 0 起点，翻译时再减 1
        f[SqlFunctionNames.IndexOf] = Call("LOCATE");
        f[SqlFunctionNames.InCsv] = Template("(LOCATE(',' || {1} || ',', ',' || {0} || ',') > 0)");
        f[SqlFunctionNames.MaxColumns] = Call("GREATEST");
        f[SqlFunctionNames.MinColumns] = Call("LEAST");
        // 没有 TRUNC / LOG10：截断用 ROUND 的 ROUND_DOWN 模式，以 10 为底写 LOG(10,x)（底数在前，同基类的 LogBase）
        f[SqlFunctionNames.Truncate] = (r, a) =>
        {
            r.Append("ROUND(").Write(a[0]).Append(",");
            if (a.Count > 1) r.Write(a[1]);
            else r.Append("0");
            r.Append(",ROUND_DOWN)");
        };
        f[SqlFunctionNames.Log10] = Template("LOG(10,{0})");
        f[SqlFunctionNames.Random] = Template("RAND()");
        f[SqlFunctionNames.UtcNow] = Template("CURRENT_UTCTIMESTAMP");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG(TO_NVARCHAR(").Write(a[0]).Append("), ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (_, _) => throw new NotSupportedException("SAP HANA 的 STRING_AGG 不支持 DISTINCT");
        f[SqlFunctionNames.DateFormat] = WriteDateFormat;
        // 截断：按格式取出起点的文字再转回时间；季度、周在月、日的基础上往回推（周从周一起，WEEKDAY 周一为 0）
        const string day = "TO_TIMESTAMP(TO_NVARCHAR({0},'YYYY-MM-DD'),'YYYY-MM-DD')";
        const string month = "TO_TIMESTAMP(TO_NVARCHAR({0},'YYYY-MM')||'-01','YYYY-MM-DD')";
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Year)] = Template("TO_TIMESTAMP(TO_NVARCHAR({0},'YYYY')||'-01-01','YYYY-MM-DD')");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Quarter)] = Template("ADD_MONTHS(" + month + ",-MOD(MONTH({0})-1,3))");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Month)] = Template(month);
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Week)] = Template("ADD_DAYS(" + day + ",-WEEKDAY({0}))");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Day)] = Template(day);
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Hour)] = Template("TO_TIMESTAMP(TO_NVARCHAR({0},'YYYY-MM-DD HH24')||':00:00','YYYY-MM-DD HH24:MI:SS')");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Minute)] = Template("TO_TIMESTAMP(TO_NVARCHAR({0},'YYYY-MM-DD HH24:MI')||':00','YYYY-MM-DD HH24:MI:SS')");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Second)] = Template("TO_TIMESTAMP(TO_NVARCHAR({0},'YYYY-MM-DD HH24:MI:SS'),'YYYY-MM-DD HH24:MI:SS')");
        // 相差：SECONDS_BETWEEN(起点,终点) 折算后截断成整数
        DateFunctions(f,
            unit => unit switch
            {
                DateUnit.Year => "ADD_YEARS({0},{1})",
                DateUnit.Month => "ADD_MONTHS({0},{1})",
                DateUnit.Day => "ADD_DAYS({0},{1})",
                DateUnit.Hour => "ADD_SECONDS({0},({1})*3600)",
                DateUnit.Minute => "ADD_SECONDS({0},({1})*60)",
                DateUnit.Second => "ADD_SECONDS({0},{1})",
                _ => "ADD_NANO100({0},({1})*10000)",
            },
            part => part switch
            {
                DatePart.Year => "YEAR({0})",
                DatePart.Month => "MONTH({0})",
                DatePart.Day => "DAYOFMONTH({0})",
                DatePart.Hour => "HOUR({0})",
                DatePart.Minute => "MINUTE({0})",
                // SECOND 带小数部分
                DatePart.Second => "CAST(FLOOR(SECOND({0})) AS INTEGER)",
                DatePart.DayOfYear => "DAYOFYEAR({0})",
                DatePart.DayOfWeek => "MOD(WEEKDAY({0})+1,7)",
                _ => "(WEEKDAY({0})+1)",
            },
            unit => unit switch
            {
                DateUnit.Day => "CAST(SECONDS_BETWEEN({0},{1})/86400 AS BIGINT)",
                DateUnit.Hour => "CAST(SECONDS_BETWEEN({0},{1})/3600 AS BIGINT)",
                DateUnit.Minute => "CAST(SECONDS_BETWEEN({0},{1})/60 AS BIGINT)",
                DateUnit.Second => "SECONDS_BETWEEN({0},{1})",
                _ => "CAST(NANO100_BETWEEN({0},{1})/10000 AS BIGINT)",
            });
    }

    // HANA 的 CONCAT 只收两个参数，|| 逐对加括号（与 Java fork 一致）：((a || b) || c)
    private static void PairwiseConcat(SqlRenderer r, IReadOnlyList<SqlExpr> a)
    {
        r.Append(new string('(', a.Count - 1)).Write(a[0]);
        for (var i = 1; i < a.Count; i++) r.Append(" || ").Write(a[i]).Append(")");
    }

    // C# 格式符 → HANA 格式符（长的在前）
    private static readonly (string CSharp, string Hana)[] DateFormatTokens =
        [("yyyy", "YYYY"), ("yy", "YY"), ("MM", "MM"), ("dd", "DD"), ("HH", "HH24"), ("hh", "HH12"), ("mm", "MI"), ("ss", "SS"), ("fff", "FF3")];

    // 夹在两个格式符之间时留在格式串里的分隔符
    private const string DateFormatSeparators = "-/:., ";

    /// <summary>
    /// 日期格式化（与 Java fork 一致）：格式符和夹在格式符之间的分隔符写进 <c>TO_NVARCHAR(x,'…')</c>，其余文字单独作为字面量拼接，
    /// 如 <c>yyyy年MM月dd日 HH:mm</c> 写成 <c>(TO_NVARCHAR(x,'YYYY') || '年' || TO_NVARCHAR(x,'MM') || '月' || TO_NVARCHAR(x,'DD') || '日 ' || TO_NVARCHAR(x,'HH24:MI'))</c>。
    /// </summary>
    private static void WriteDateFormat(SqlRenderer r, IReadOnlyList<SqlExpr> a)
    {
        var format = ((LiteralExpr)a[1]).Sql;
        var parts = new List<(bool IsFormat, System.Text.StringBuilder Text)>();
        for (var i = 0; i < format.Length;)
        {
            var token = TokenAt(format, i);
            var inFormat = parts.Count > 0 && parts[^1].IsFormat;
            if (token is { } t)
            {
                if (!inFormat) parts.Add((true, new()));
                parts[^1].Text.Append(t.Hana);
                i += t.CSharp.Length;
                continue;
            }
            if (!(inFormat && DateFormatSeparators.Contains(format[i]) && TokenAt(format, i + 1) is not null) && (parts.Count == 0 || parts[^1].IsFormat))
                parts.Add((false, new()));
            parts[^1].Text.Append(format[i++]);
        }
        if (parts.Count > 1) r.Append("(");
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0) r.Append(" || ");
            var text = parts[i].Text.ToString();
            if (parts[i].IsFormat) r.Append("TO_NVARCHAR(").Write(a[0]).Append(",'" + text + "')");
            else r.Append("'" + text.Replace("'", "''", StringComparison.Ordinal) + "'");
        }
        if (parts.Count > 1) r.Append(")");
    }

    private static (string CSharp, string Hana)? TokenAt(string format, int index)
    {
        foreach (var token in DateFormatTokens)
            if (string.CompareOrdinal(format, index, token.CSharp, 0, token.CSharp.Length) == 0)
                return token;
        return null;
    }
}
