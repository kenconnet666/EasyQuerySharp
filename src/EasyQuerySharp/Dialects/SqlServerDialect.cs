using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// SQL Server 方言（2012 起的 OFFSET … FETCH 分页；2008 及更早不支持）。标识符用方括号。
/// 驱动用 Microsoft.Data.SqlClient：<c>o.Use(SqlServerDialect.Instance, SqlClientFactory.Instance.CreateDataSource(cs))</c>。
/// </summary>
public class SqlServerDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static SqlServerDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "mssql";

    /// <inheritdoc />
    public override string QuoteIdentifier(string identifier) => $"[{identifier}]";

    /// <inheritdoc />
    public override string RecursiveWith => "WITH ";

    /// <summary><c>STRING_AGG(x, ?) WITHIN GROUP (ORDER BY …)</c>（与 Java 的 MsSQLJoiningSQLFunction 一致）。</summary>
    public override void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join)
    {
        renderer.Append(join.Distinct ? "STRING_AGG(DISTINCT " : "STRING_AGG(").Write(join.Value).Append(", ").Write(join.Separator).Append(") WITHIN GROUP (");
        WriteOrderBy(renderer, join.Orders);
        renderer.Append(")");
    }

    /// <summary>
    /// 自增键用 <c>OUTPUT INSERTED.[id]</c> 随插入一起返回（单独再查 <c>SCOPE_IDENTITY()</c> 不在同一个作用域，拿到的是 NULL）。
    /// </summary>
    public override void WriteInsertOutput(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning)
    {
        renderer.Append(" OUTPUT ");
        for (var i = 0; i < returning.Count; i++)
        {
            if (i > 0) renderer.Append(",");
            renderer.Append("INSERTED." + QuoteIdentifier(((ColumnExpr)returning[i]).Name));
        }
    }

    /// <inheritdoc />
    public override void WriteInsertReturning(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning)
    {
    }

    /// <summary>没有 <c>CREATE TABLE IF NOT EXISTS</c>，用 <c>IF OBJECT_ID(…) IS NULL</c> 判断。</summary>
    protected override string CreateTableStatement(Metadata.EntityMetadata entity, string body) =>
        $"IF OBJECT_ID(N'{QualifiedTable(entity).Replace("'", "''")}', N'U') IS NULL CREATE TABLE {QualifiedTable(entity)} ({body})";

    /// <inheritdoc />
    protected override string IdentityClause => "IDENTITY(1,1)";

    /// <summary>Unicode 字面量 <c>N'…'</c>（库的排序规则不是 UTF-8 时，不带 N 的中文注释会变成问号）。</summary>
    private protected override string SqlString(string text) => "N" + base.SqlString(text);

    /// <summary>
    /// 表注释是扩展属性 MS_Description：<c>EXEC sp_addextendedproperty 'MS_Description', N'…', 'SCHEMA', N'dbo', 'TABLE', N'表'</c>，
    /// 已经有时不加（重复加会报错，建表语句在表已存在时跳过，注释也要能重复执行）。
    /// </summary>
    internal override string? TableCommentSql(Metadata.EntityMetadata entity) => entity.Comment is null ? null : Description(entity, entity.Comment, null);

    /// <summary>列注释同表注释，后面再加 <c>'COLUMN', N'列'</c>。</summary>
    internal override string? ColumnCommentSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        column.Comment is null ? null : Description(entity, column.Comment, column.Name);

    // 扩展属性要给出 schema 名：没写时是 dbo（与 Java 一致）；"库.dbo" 取最后一级
    private string Description(Metadata.EntityMetadata entity, string comment, string? column)
    {
        var level = $"'SCHEMA', {SqlString(entity.Schema?.Split('.')[^1] ?? "dbo")}, 'TABLE', {SqlString(entity.TableName)}";
        return $"IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty('MS_Description', {level}, {(column is null ? "NULL, NULL" : $"'COLUMN', {SqlString(column)}")})) "
               + $"EXEC sp_addextendedproperty 'MS_Description', {SqlString(comment)}, {level}{(column is null ? "" : $", 'COLUMN', {SqlString(column)}")}";
    }

    /// <summary>改表名：<c>EXEC sp_rename N'[dbo].[旧表]', N'新表'</c>。</summary>
    internal override string RenameTableSql(Metadata.EntityMetadata entity, string oldName) =>
        $"EXEC sp_rename {SqlString(QualifiedName(entity.Schema, oldName))}, {SqlString(entity.TableName)}";

    /// <summary>改列名：<c>EXEC sp_rename N'[表].[旧列]', N'新列', 'COLUMN'</c>。</summary>
    internal override string RenameColumnSql(Metadata.EntityMetadata entity, string oldName, Metadata.ColumnMetadata column) =>
        $"EXEC sp_rename {SqlString(QualifiedTable(entity) + "." + QuoteIdentifier(oldName))}, {SqlString(column.Name)}, 'COLUMN'";

    /// <summary>加列：<c>ALTER TABLE 表 ADD 列定义</c>（没有 COLUMN 关键字）。</summary>
    internal override string AddColumnSql(Metadata.EntityMetadata entity, Metadata.ColumnMetadata column) =>
        $"ALTER TABLE {QualifiedTable(entity)} ADD {ColumnDefinition(entity, column)}";

    /// <summary>读 sys 目录视图（当前库的默认 schema，同 TS 版）。</summary>
    internal override CatalogQueries CatalogQueries { get; } = new(
        "SELECT t.name AS table_name, CAST(p.value AS NVARCHAR(4000)) AS comment FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id "
        + "LEFT JOIN sys.extended_properties p ON p.major_id = t.object_id AND p.minor_id = 0 AND p.class = 1 AND p.name = 'MS_Description' "
        + "WHERE s.name = SCHEMA_NAME() ORDER BY t.name",
        "SELECT t.name AS table_name, c.name AS column_name, LOWER(ty.name) + CASE "
        + "WHEN ty.name IN ('nvarchar', 'nchar') THEN '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length / 2 AS VARCHAR(10)) END + ')' "
        + "WHEN ty.name IN ('varchar', 'char', 'varbinary', 'binary') THEN '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length AS VARCHAR(10)) END + ')' "
        + "WHEN ty.name IN ('decimal', 'numeric') THEN '(' + CAST(c.precision AS VARCHAR(10)) + ',' + CAST(c.scale AS VARCHAR(10)) + ')' "
        + "WHEN ty.name IN ('datetime2', 'time', 'datetimeoffset') THEN '(' + CAST(c.scale AS VARCHAR(10)) + ')' ELSE '' END AS column_type, "
        + "c.is_nullable AS is_nullable, OBJECT_DEFINITION(c.default_object_id) AS column_default, "
        + "(SELECT CAST(p.value AS NVARCHAR(4000)) FROM sys.extended_properties p WHERE p.major_id = t.object_id AND p.minor_id = c.column_id AND p.class = 1 "
        + "AND p.name = 'MS_Description') AS comment, c.is_identity AS is_generated, "
        + "CASE WHEN EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id "
        + "WHERE i.is_primary_key = 1 AND ic.object_id = t.object_id AND ic.column_id = c.column_id) THEN 1 ELSE 0 END AS primary_key "
        + "FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.columns c ON c.object_id = t.object_id "
        + "JOIN sys.types ty ON ty.user_type_id = c.user_type_id WHERE s.name = SCHEMA_NAME() ORDER BY t.name, c.column_id",
        "SELECT t.name AS table_name, i.name AS index_name, i.is_unique AS is_unique, c.name AS column_name "
        + "FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id "
        + "JOIN sys.indexes i ON i.object_id = t.object_id AND i.is_primary_key = 0 AND i.name IS NOT NULL "
        + "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id "
        + "JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id WHERE s.name = SCHEMA_NAME() ORDER BY t.name, i.name, ic.key_ordinal",
        "SELECT t.name AS table_name, fk.name AS constraint_name, c.name AS column_name, rt.name AS referenced_table, rc.name AS referenced_column "
        + "FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id "
        + "JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id "
        + "JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id "
        + "JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id "
        + "JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id "
        + "WHERE s.name = SCHEMA_NAME() ORDER BY t.name, fk.name, fkc.constraint_column_id");

    /// <summary>SQL Server 的类型名：BIT、DATETIME2、NVARCHAR、VARBINARY(MAX)（TIMESTAMP 在这里是行版本，不能用）。</summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) => "NVARCHAR(255)",
        _ when type == typeof(bool) => "BIT",
        _ when type == typeof(byte) => "TINYINT",
        _ when type == typeof(double) => "FLOAT",
        _ when type == typeof(DateTime) => "DATETIME2",
        _ when type == typeof(DateTimeOffset) => "DATETIMEOFFSET",
        _ when type == typeof(Guid) => "UNIQUEIDENTIFIER",
        _ when type == typeof(byte[]) => "VARBINARY(MAX)",
        _ => base.ClrColumnType(type, column),
    };


    /// <summary>SQL Server 没有 FOR UPDATE（用表提示 WITH (UPDLOCK, ROWLOCK)），NOWAIT / SKIP LOCKED 也一样，不能悄悄忽略（与 Java 一致）。</summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock) =>
        throw new NotSupportedException("SQL Server 不支持 FOR UPDATE 语法，请改用表提示 WITH (UPDLOCK, ROWLOCK)");

    /// <summary>没有跳过行数时只取前几行写成 <c>SELECT TOP n</c>（与 Java 一致，如 EXISTS 里的 <c>SELECT TOP 1 1</c>）。</summary>
    public override void WriteSelectHead(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Offset is null or <= 0 && query.Limit is { } limit) renderer.Append("TOP " + limit.ToString(CultureInfo.InvariantCulture) + " ");
    }

    /// <summary>
    /// 有跳过行数时写 <c>OFFSET … ROWS FETCH NEXT … ROWS ONLY</c>。SQL Server 要求有 ORDER BY：没有时按分组键，再不然按主键排序，
    /// 都没有时补 <c>ORDER BY (SELECT 1)</c>（与 Java 一致，Java 在最后一种情况下报错）。
    /// </summary>
    public override void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Offset is null or <= 0) return;
        if (query.OrderBy.Count == 0)
        {
            if (query.GroupBy.Count > 0) renderer.Append(" ORDER BY ").WriteList(query.GroupBy, ",");
            else if (query.From is EntityTableSource { Entity.KeyColumns: { Count: > 0 } keys } table)
                renderer.Append(" ORDER BY ").WriteList(keys.Select(k => (SqlExpr)table.Column(k)).ToList(), ",");
            else renderer.Append(" ORDER BY (SELECT 1)");
        }
        renderer.Append(" OFFSET ").Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture)).Append(" ROWS");
        if (query.Limit is { } limit) renderer.Append(" FETCH NEXT ").Append(limit.ToString(CultureInfo.InvariantCulture)).Append(" ROWS ONLY");
    }

    /// <inheritdoc />
    public override bool AliasedWriteUsesFrom => true;

    /// <summary>带别名或连接的更新：<c>UPDATE t SET t.[a] = ? FROM [表] t [JOIN …] WHERE …</c>（与 Java 一致）。</summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").Append(update.Table.Alias!).Append(" SET ").WriteSets(update.Sets).Append(" FROM ").WriteTable(update.Table);
        foreach (var join in update.Joins) renderer.WriteJoin(join);
        renderer.WriteWhere(update.Where);
    }

    /// <summary>带别名或连接的删除：<c>DELETE t FROM [表] t [JOIN …] WHERE …</c>（与 Java 一致）。</summary>
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        renderer.Append("DELETE ").Append(statement.Table.Alias!).Append(" FROM ").WriteTable(statement.Table);
        foreach (var join in statement.Joins) renderer.WriteJoin(join);
        renderer.WriteWhere(statement.Where);
    }

    /// <summary>
    /// 模式在 SQL 里拼（<c>Sql.LikeContains</c> 等）时值先转成 NVARCHAR(MAX) 再拼 %：<c>LIKE ('%'+CAST(? AS NVARCHAR(MAX))+'%')</c>（与 Java 的 MsSQLLikeSQLFunction 一致）；
    /// 值里含 % 或 _ 时转义后写 <c>ESCAPE '\'</c>，按字面匹配（SQL Server 的 LIKE 还把 <c>[</c> 当通配符，一并转义）。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue && predicate is { Pattern: FunctionExpr { Name: SqlFunctionNames.Concat }, Value: not null })
        {
            renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE (" : " LIKE (");
            if (predicate.Mode != LikeMode.StartsWith) renderer.Append("'%'+");
            renderer.Append("CAST(").Write(predicate.Value).Append(" AS NVARCHAR(MAX))");
            if (predicate.Mode != LikeMode.EndsWith) renderer.Append("+'%'");
            renderer.Append(")");
            return;
        }
        if (predicate.HasWildcardValue) WriteEscapedLike(renderer, predicate, " ESCAPE '\\'", text => EscapeLike(text).Replace("[", "\\["));
        else base.WriteLike(renderer, predicate);
    }

    /// <summary>DateTime 参数按 datetime2 传（驱动默认按 datetime，只精确到约 3 毫秒）。</summary>
    public override void ConfigureParameter(System.Data.Common.DbParameter parameter, SqlParameterValue value)
    {
        if ((Nullable.GetUnderlyingType(value.ClrType) ?? value.ClrType) == typeof(DateTime)) parameter.DbType = System.Data.DbType.DateTime2;
    }

    /// <inheritdoc />
    public override bool InsertConflictUsesMerge => true;

    /// <summary>SQL Server 的 MERGE 必须以分号结尾。</summary>
    protected override string MergeTerminator => ";";

    /// <summary>SQL Server 的类型转换（对应 Java 的 MsSQLCastSQLFunction）。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "(cast({0} as varchar) not in ('0','false'))",
        _ when targetType == typeof(byte) || targetType == typeof(sbyte) => "cast({0} as tinyint)",
        _ when targetType == typeof(char) => "substring(cast({0} as nvarchar),1,1)",
        _ when targetType == typeof(TimeOnly) || targetType == typeof(TimeSpan) => "cast({0} as time)",
        _ when targetType == typeof(DateOnly) => "cast({0} as date)",
        _ when targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) => "cast({0} as datetime)",
        _ when targetType == typeof(decimal) => "cast({0} as decimal(36,18))",
        _ when targetType == typeof(double) || targetType == typeof(float) => "cast({0} as decimal(32,16))",
        _ when targetType == typeof(short) || targetType == typeof(ushort) => "cast({0} as smallint)",
        _ when targetType == typeof(int) => "cast({0} as int)",
        _ when IsInteger(targetType) => "cast({0} as bigint)",
        _ when targetType == typeof(Guid) => "cast({0} as uniqueidentifier)",
        _ when targetType == typeof(string) => "cast({0} as nvarchar(max))",
        _ => base.CastTemplate(targetType),
    };

    // 几个值取最大 / 最小：(SELECT MAX(__col__) FROM (VALUES (a), (b)) AS t(__col__))（与 Java 的 MsSQLMaxMinColumnsSQLFunction 一致）
    private static FunctionWriter ValuesExtreme(string aggregate) => (r, a) =>
    {
        r.Append("(SELECT " + aggregate + "(__col__) FROM (VALUES ");
        for (var i = 0; i < a.Count; i++)
        {
            if (i > 0) r.Append(", ");
            r.Append("(").Write(a[i]).Append(")");
        }
        r.Append(") AS t(__col__))");
    };

    // 整串格式直接对应一个 CONVERT 样式
    private static readonly Dictionary<string, string> WholeDateFormats = new(StringComparer.Ordinal)
    {
        ["yyyy-MM-dd HH:mm:ss"] = "CONVERT(CHAR(19), {0}, 120)",
        ["yyyy-MM-dd HH:mm"] = "SUBSTRING(CONVERT(CHAR(19), {0}, 120), 1, 16)",
        ["yyyy-MM-dd HH"] = "SUBSTRING(CONVERT(CHAR(19), {0}, 120), 1, 13)",
        ["yyyy-MM-dd"] = "CONVERT(CHAR(10), {0}, 23)",
        ["yyyy-MM"] = "SUBSTRING(CONVERT(CHAR(10), {0}, 23), 1, 7)",
        ["yyyyMMdd"] = "CONVERT(CHAR(8), {0}, 112)",
        ["yyyyMM"] = "SUBSTRING(CONVERT(CHAR(8), {0}, 112), 1, 6)",
        ["yyyy"] = "SUBSTRING(CONVERT(CHAR(8), {0}, 112), 1, 4)",
        ["HH:mm:ss"] = "CONVERT(CHAR(8), {0}, 24)",
    };

    // 单个格式符：从 CONVERT 出来的 yyyyMMdd（112）、yyMMdd（12）、hh:mm:ss（24）里截取
    private static readonly Dictionary<string, string> DateFormatTokens = new(StringComparer.Ordinal)
    {
        ["yyyy"] = "SUBSTRING(CONVERT(CHAR(8), {0}, 112), 1, 4)",
        ["yy"] = "SUBSTRING(CONVERT(CHAR(6), {0}, 12), 1, 2)",
        ["MM"] = "SUBSTRING(CONVERT(CHAR(6), {0}, 12), 3, 2)",
        ["M"] = "CASE WHEN SUBSTRING(CONVERT(CHAR(6), {0}, 12), 3, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(6), {0}, 12), 4, 1) else SUBSTRING(CONVERT(CHAR(6), {0}, 12), 3, 2) END",
        ["dd"] = "SUBSTRING(CONVERT(CHAR(6), {0}, 12), 5, 2)",
        ["d"] = "CASE WHEN SUBSTRING(CONVERT(CHAR(6), {0}, 12), 5, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(6), {0}, 12), 6, 1) ELSE SUBSTRING(CONVERT(CHAR(6), {0}, 12), 5, 2) END",
        ["HH"] = "SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 2)",
        ["H"] = "CASE WHEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 2, 1) ELSE SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 2) END",
        ["hh"] = "CASE CAST(CASE WHEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 2, 1) ELSE SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 2) END as int) % 12 "
              + "WHEN 0 THEN '12' WHEN 1 THEN '01' WHEN 2 THEN '02' WHEN 3 THEN '03' WHEN 4 THEN '04' WHEN 5 THEN '05' WHEN 6 THEN '06' WHEN 7 THEN '07' WHEN 8 THEN '08' WHEN 9 THEN '09' WHEN 10 THEN '10' WHEN 11 THEN '11' END",
        ["h"] = "case cast(CASE WHEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 2, 1) ELSE SUBSTRING(CONVERT(CHAR(8), {0}, 24), 1, 2) END as int) % 12 "
             + "WHEN 0 THEN '12' WHEN 1 THEN '1' WHEN 2 THEN '2' WHEN 3 THEN '3' WHEN 4 THEN '4' WHEN 5 THEN '5' WHEN 6 THEN '6' WHEN 7 THEN '7' WHEN 8 THEN '8' WHEN 9 THEN '9' WHEN 10 THEN '10' WHEN 11 THEN '11' END",
        ["mm"] = "SUBSTRING(CONVERT(CHAR(8), {0}, 24), 4, 2)",
        ["m"] = "CASE WHEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 4, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 5, 1) else SUBSTRING(CONVERT(CHAR(8), {0}, 24), 4, 2) END",
        ["ss"] = "SUBSTRING(CONVERT(CHAR(8), {0}, 24), 7, 2)",
        ["s"] = "CASE WHEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 7, 1) = '0' THEN SUBSTRING(CONVERT(CHAR(8), {0}, 24), 8, 1) else SUBSTRING(CONVERT(CHAR(8), {0}, 24), 7, 2) END",
    };

    // 把格式串切成格式符和夹在中间的字面量（a 不是格式符，和字面量一样当参数）
    private static readonly System.Text.RegularExpressions.Regex DateFormatSegments =
        new("(yyyy|yy|MM|dd|HH|hh|mm|ss|[MdHhmsa]|(?:(?!yyyy|yy|MM|dd|HH|hh|mm|ss|[MdHhmsa]).)+)");

    /// <summary>
    /// 日期格式化（与 Java 的 MsSQLDateTimeFormatSQLFunction 一致）：常见的整串格式对应一个 CONVERT 样式；
    /// 其余的切成格式符与字面量，各自写成 SUBSTRING(CONVERT(…)) 或参数，用 <c>+</c> 连接。
    /// </summary>
    private static void WriteDateFormat(SqlRenderer renderer, IReadOnlyList<SqlExpr> args)
    {
        var format = ((LiteralExpr)args[1]).Sql;
        if (WholeDateFormats.TryGetValue(format, out var whole))
        {
            Template(whole)(renderer, args);
            return;
        }
        renderer.Append("(");
        var first = true;
        foreach (System.Text.RegularExpressions.Match segment in DateFormatSegments.Matches(format))
        {
            if (!first) renderer.Append(" + ");
            first = false;
            if (DateFormatTokens.TryGetValue(segment.Value, out var token)) Template(token)(renderer, args);
            else renderer.Write(new ParameterExpr(segment.Value, typeof(string)));
        }
        renderer.Append(")");
    }

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 用 + 拼接：列表为 NULL 时整体为 NULL，不会误匹配
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
        // SQL Server 2022 起的 DATETRUNC；周按 ISO 从周一起（与其余方言一致）
        foreach (var unit in Enum.GetValues<DateTruncUnit>())
            f[SqlFunctionNames.DateTrunc(unit)] = Template("DATETRUNC(" + (unit == DateTruncUnit.Week ? "iso_week" : unit.ToString().ToLowerInvariant()) + ", {0})");
        f[SqlFunctionNames.InCsv] = Template("(CHARINDEX(',' + CAST({0} AS NVARCHAR(MAX)) + ',', ',' + {1} + ',') > 0)");
        f[SqlFunctionNames.NullOrDefault] = Call("ISNULL");
        f[SqlFunctionNames.Length] = Call("LEN");
        f[SqlFunctionNames.Trim] = (r, a) => r.Append("LTRIM(RTRIM(").Write(a[0]).Append("))");
        // SUBSTRING 必须给长度：不给（Substring(start)）时取到末尾
        f[SqlFunctionNames.SubString] = (r, a) =>
        {
            if (a.Count > 2) r.Append("SUBSTRING(").WriteList(a, ",").Append(")");
            else r.Append("SUBSTRING(").WriteList(a, ",").Append(",2147483647)");
        };
        f[SqlFunctionNames.IndexOf] = Reordered("CHARINDEX", 1, 0);
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        f[SqlFunctionNames.Ln] = Call("LOG");
        f[SqlFunctionNames.LogBase] = Call("LOG");
        f[SqlFunctionNames.MaxColumns] = ValuesExtreme("MAX");
        f[SqlFunctionNames.MinColumns] = ValuesExtreme("MIN");
        f[SqlFunctionNames.DateFormat] = WriteDateFormat;
        f[SqlFunctionNames.Now] = Template("GETDATE()");
        f[SqlFunctionNames.Random] = Template("RAND()");
        f[SqlFunctionNames.UtcNow] = Template("GETUTCDATE()");
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "dateadd(second, ({1})/1000, {0})" : "dateadd(" + Lower(unit) + ", {1}, {0})",
            part => part switch
            {
                DatePart.DayOfYear => "datepart(dayofyear, {0})",
                DatePart.DayOfWeek => "(datepart(weekday, {0})-1)",
                DatePart.IsoDayOfWeek => "(CASE WHEN (datepart(weekday, {0})-1) = 0 THEN 7 ELSE (datepart(weekday, {0})-1) END)",
                _ => "datepart(" + Lower(part) + ", {0})",
            },
            unit => "datediff(" + Lower(unit) + ", {0}, {1})");
        // ROUND 必须带位数；第三个参数非 0 表示截断。不带位数的截断与 Java 一致写成 CASE（Java 的 Floor 也用它，对负数会得到截断而不是向下取整，这里的 Floor 仍是 FLOOR）
        f[SqlFunctionNames.Round] = WithDigits("ROUND");
        f[SqlFunctionNames.Truncate] = (r, a) =>
        {
            if (a.Count == 1)
            {
                Template("(CASE WHEN {0} >= 0 THEN FLOOR({0}) ELSE CEILING({0}) END)")(r, a);
                return;
            }
            r.Append("ROUND(").Write(a[0]).Append(",").Write(a[1]).Append(",1)");
        };
        // SQL Server 没有 ATAN2，用 ATAN 按象限补 π（与 Java 的 MsSQLMathSQLFunction 一致）
        f[SqlFunctionNames.Atan2] = Template("(CASE WHEN {1}<>0 THEN ATAN({0}/{1}) + CASE WHEN {1}<0 AND {0}>=0 THEN PI() WHEN {1}<0 AND {0}<0 THEN -PI() ELSE 0 END WHEN {1}=0 AND {0}>0 THEN PI()/2 WHEN {1}=0 AND {0}<0 THEN -PI()/2 ELSE NULL END)");
    }
}
