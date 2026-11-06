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

    /// <summary>值里含 % 或 _ 时转义后写 <c>ESCAPE '\'</c>，按字面匹配（SQL Server 的 LIKE 还把 <c>[</c> 当通配符，一并转义）。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue || predicate.Value is not ParameterExpr { Value: string text })
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        var escaped = EscapeLike(text).Replace("[", "\\[");
        var pattern = predicate.Mode switch
        {
            LikeMode.StartsWith => escaped + "%",
            LikeMode.EndsWith => "%" + escaped,
            _ => "%" + escaped + "%",
        };
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE " : " LIKE ").Write(new ParameterExpr(pattern, typeof(string))).Append(" ESCAPE '\\'");
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

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 用 + 拼接：列表为 NULL 时整体为 NULL，不会误匹配
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
        f[SqlFunctionNames.InCsv] = Template("(CHARINDEX(',' + CAST({0} AS NVARCHAR(MAX)) + ',', ',' + {1} + ',') > 0)");
        f[SqlFunctionNames.NullOrDefault] = Call("ISNULL");
        f[SqlFunctionNames.Length] = Call("LEN");
        f[SqlFunctionNames.Trim] = (r, a) => r.Append("LTRIM(RTRIM(").Write(a[0]).Append("))");
        f[SqlFunctionNames.SubString] = Call("SUBSTRING");
        f[SqlFunctionNames.IndexOf] = Reordered("CHARINDEX", 1, 0);
        f[SqlFunctionNames.Ceiling] = Call("CEILING");
        f[SqlFunctionNames.Ln] = Call("LOG");
        f[SqlFunctionNames.DateFormat] = DateFormat("FORMAT({0},{1})");
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
        // ROUND 的第三个参数非 0 表示截断
        f[SqlFunctionNames.Truncate] = (r, a) =>
        {
            r.Append("ROUND(").Write(a[0]).Append(",");
            if (a.Count > 1) r.Write(a[1]);
            else r.Append("0");
            r.Append(",1)");
        };
    }
}
