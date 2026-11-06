using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>SQLite 方言。驱动用 Microsoft.Data.Sqlite：<c>o.UseSqlite(SqliteFactory.Instance.CreateDataSource(cs))</c>。</summary>
public class SqliteDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static SqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "sqlite";

    // RETURNING 需要 3.35；用 last_insert_rowid() 兼容更旧的版本
    /// <inheritdoc />
    public override string? LastInsertIdQuery => "SELECT last_insert_rowid()";

    /// <summary>UPDATE / DELETE … RETURNING 需要 SQLite 3.35 起（Microsoft.Data.Sqlite 自带的版本满足）。</summary>
    public override bool SupportsWriteReturning => true;

    /// <summary>SQLite 的 SET 列不能带表别名。</summary>
    public override bool QualifySetColumns => false;

    /// <inheritdoc />
    public override bool ParenthesizeCompoundParts => false;

    /// <summary><c>UPDATE "a" AS t SET "c" = t1."d" FROM "b" t1 , "c" t2 WHERE 各 ON 条件 AND 原条件</c>（SQLite 3.33 起；目标表的别名要写 AS）。</summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").WriteTableName(update.Table).Append(" AS " + update.Table.Alias).Append(" SET ").WriteSets(update.Sets).Append(" FROM ");
        WriteCommaJoinedTables(renderer, update.Joins, update.Where);
    }

    /// <summary><c>DELETE FROM "a" AS t WHERE EXISTS(SELECT 1 FROM "b" t1 WHERE 各 ON 条件 AND 原条件)</c>：SQLite 的 DELETE 没有 USING / JOIN。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE FROM ").WriteTableName(statement.Table).Append(" AS " + statement.Table.Alias).Append(" WHERE EXISTS(SELECT 1 FROM ");
        WriteCommaJoinedTables(renderer, statement.Joins, statement.Where);
        renderer.Append(")");
    }

    /// <summary>SQLite 没有 ROLLUP / CUBE。</summary>
    public override void WriteGroupBy(SqlRenderer renderer, IReadOnlyList<SqlExpr> keys, GroupByMode mode)
    {
        if (mode != GroupByMode.Plain) throw new NotSupportedException($"SQLite 不支持 GROUP BY {mode.ToString().ToUpperInvariant()}");
        base.WriteGroupBy(renderer, keys, mode);
    }

    /// <summary>UPDATE / DELETE 的目标表别名必须写 AS（<c>UPDATE "t" AS t SET …</c>）。</summary>
    public override string WriteTargetAliasSeparator => " AS ";

    /// <summary>自增主键只能写成列定义里的 <c>INTEGER PRIMARY KEY AUTOINCREMENT</c>（整型必须是 INTEGER 才是 rowid 别名）。</summary>
    protected override string ColumnDefinition(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        InlinePrimaryKey(column) ? $"{QuoteIdentifier(column.Name)} INTEGER PRIMARY KEY AUTOINCREMENT" : base.ColumnDefinition(entity, column);

    /// <inheritdoc />
    protected override bool InlinePrimaryKey(Metadata.ColumnMetadata key) => key is { IsGenerated: true, IsPrimaryKey: true };

    /// <summary>
    /// SQLite 没有行锁语法（写事务锁整个库），FOR UPDATE 会是语法错误，所以直接拒绝。
    /// Java 版照样拼出 FOR UPDATE，执行时才失败，这里提前报错。
    /// </summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock) =>
        throw new NotSupportedException("SQLite 不支持 FOR UPDATE 行锁：写事务会锁住整个库，需要先占写锁时用 BEGIN IMMEDIATE");

    /// <summary>
    /// 值里含 % 或 _ 时，包含 / 开头 / 结尾改写成 INSTR，避免被当成通配符；模式在 SQL 里拼（列、<c>Sql.LikeContains</c> 等）时
    /// 写 <c>LIKE ('%'||v||'%')</c>（都与 Java 的 SQLiteLikeSQLFunction 一致；CONCAT 要 SQLite 3.44 起才有）。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue && predicate is { Pattern: FunctionExpr { Name: SqlFunctionNames.Concat }, Value: not null })
        {
            renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE (" : " LIKE (");
            if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%'||");
            renderer.Write(predicate.Value);
            if (predicate.Mode != LikeMode.EndsWith) renderer.Append("||'%'");
            renderer.Append(")");
            return;
        }
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        renderer.Append("INSTR(").Write(predicate.Operand).Append(",").Write(predicate.Value).Append(")");
        OracleDialect.WriteInstrComparison(renderer, predicate);
    }

    /// <summary>SQLite 的类型转换（对应 Java 的 SQLiteCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "({0} NOT IN ('0','false'))",
        _ when targetType == typeof(char) => "substr(cast({0} as character), 1, 1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "time({0})",
        _ when targetType == typeof(DateOnly) => "date({0})",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "datetime({0})",
        _ when targetType == typeof(decimal) => "cast({0} as decimal(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "cast({0} as double)",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) => "cast({0} as int2)",
        _ when targetType == typeof(short) || targetType == typeof(ushort) => "CAST({0} AS SMALLINT)",
        _ when IsInteger(targetType) => "CAST({0} AS SIGNED)",
        _ when targetType == typeof(Guid) => "substr(cast({0} as character), 1, 36)",
        _ when targetType == typeof(string) => "cast({0} as character)",
        _ => base.CastTemplate(targetType),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.Length] = Call("LENGTH");
        // 多参数的 MAX / MIN 是标量函数（SQLite 没有 GREATEST / LEAST，Java 版在这里写 GREATEST，在 SQLite 上执行不了）
        f[SqlFunctionNames.MaxColumns] = Call("MAX");
        f[SqlFunctionNames.MinColumns] = Call("MIN");
        f[SqlFunctionNames.NullSafeEquals] = Template("({0} IS {1})");
        // instr 精确匹配、区分大小写；LIKE 不区分 ASCII 大小写，键里的 % _ 还会被当通配符
        // 日期存成文本：按格式截断后仍是 'yyyy-MM-dd HH:mm:ss'；周从周一起（与 MySQL 的 WEEKDAY 一致）
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Year)] = Template("strftime('%Y-01-01 00:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Quarter)] = Template("datetime(strftime('%Y-%m-01', {0}), '-' || ((CAST(strftime('%m', {0}) AS INTEGER) - 1) % 3) || ' months')");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Month)] = Template("strftime('%Y-%m-01 00:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Week)] = Template("datetime(date({0}, '-' || ((CAST(strftime('%w', {0}) AS INTEGER) + 6) % 7) || ' days'))");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Day)] = Template("strftime('%Y-%m-%d 00:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Hour)] = Template("strftime('%Y-%m-%d %H:00:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Minute)] = Template("strftime('%Y-%m-%d %H:%M:00', {0})");
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Second)] = Template("strftime('%Y-%m-%d %H:%M:%S', {0})");
        f[SqlFunctionNames.InCsv] = Template("(instr(',' || {1} || ',', ',' || {0} || ',') > 0)");
        // JSON：json_extract 对标量返回 SQL 值、对对象 / 数组返回 JSON 文本，取值与取文本同一个写法
        f[SqlFunctionNames.JsonGet] = Template("json_extract({0},'$.{1}')");
        f[SqlFunctionNames.JsonGetIndex] = Template("json_extract({0},'$[{1}]')");
        f[SqlFunctionNames.JsonText] = Template("json_extract({0},'$.{1}')");
        f[SqlFunctionNames.JsonTextIndex] = Template("json_extract({0},'$[{1}]')");
        f[SqlFunctionNames.JsonLength] = Call("json_array_length");
        f[SqlFunctionNames.JsonContainsKey] = Template("(json_type({0},'$.{1}') IS NOT NULL)");
        f[SqlFunctionNames.DateFormat] = DateFormat("strftime({1}, {0})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%M"), ("ss", "%S"));
        f[SqlFunctionNames.StringJoin] = Call("GROUP_CONCAT");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("GROUP_CONCAT(DISTINCT ").Write(a[0]).Append(",").Write(a[1]).Append(")");
        f[SqlFunctionNames.Now] = Template("datetime(current_timestamp,'localtime')");
        f[SqlFunctionNames.UtcNow] = Template("CURRENT_TIMESTAMP");
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "datetime({0},(({1})/1000)||' seconds')" : "datetime({0},({1})||' " + Lower(unit) + "s')",
            part => part == DatePart.IsoDayOfWeek
                ? "(CASE WHEN CAST(strftime('%w',{0}) AS INTEGER) = 0 THEN 7 ELSE CAST(strftime('%w',{0}) AS INTEGER) END)"
                : "CAST(strftime('" + part switch
            {
                DatePart.Year => "%Y", DatePart.Month => "%m", DatePart.Day => "%d", DatePart.Hour => "%H", DatePart.Minute => "%M",
                DatePart.Second => "%S", DatePart.DayOfYear => "%j", _ => "%w",
            } + "',{0}) AS INTEGER)",
            unit => "(strftime('%s',{1})-strftime('%s',{0}))" + unit switch
            {
                DateUnit.Day => "/86400", DateUnit.Hour => "/3600", DateUnit.Minute => "/60", DateUnit.Second => "", _ => "*1000",
            });
    }
}
