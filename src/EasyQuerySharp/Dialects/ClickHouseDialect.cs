using System.Globalization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// ClickHouse 方言（列式分析库）。标识符用反引号，分页 <c>LIMIT n OFFSET m</c>，查询按通用写法；写入按它的能力裁剪（与 Java 的 sql-clickhouse 一致）：
/// <list type="bullet">
/// <item>更新、删除是 mutation：<c>ALTER TABLE 表 UPDATE … WHERE …</c> / <c>ALTER TABLE 表 DELETE WHERE …</c>，不能带别名，不能走导航连接；</item>
/// <item>没有自增列、upsert、RETURNING、行锁，用到时抛 <see cref="NotSupportedException"/>；</item>
/// <item>建表 <c>ENGINE = MergeTree ORDER BY (主键)</c>，可空列由 <c>NULL</c> 修饰成 Nullable；</item>
/// <item>没有事务（<see cref="SupportsTransactions"/>）：一批写入逐条生效。</item>
/// </list>
/// 驱动（官方的 ClickHouse.Driver，HTTP 接口）由用户通过 <see cref="System.Data.Common.DbDataSource"/> 传入：<c>o.UseClickHouse(dataSource)</c>。
/// 连接串里要带三个会话设置（驱动认 <c>set_</c> 前缀）：<c>set_join_use_nulls=1</c>（LEFT JOIN 没匹配上的列是 NULL 而不是类型的默认值）、
/// <c>set_mutations_sync=2</c>（更新删除做完再返回）、<c>set_session_timezone=UTC</c>（驱动把 DateTime 参数按会话时区解析、读回时按 UTC，时区不是 UTC 时会差几个小时）。
/// </summary>
public class ClickHouseDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static ClickHouseDialect Instance { get; } = new();

    /// <inheritdoc />
    public override NullsOrder DefaultNullsOrder(bool descending) => NullsOrder.Last;

    /// <inheritdoc />
    public override string Name => "clickhouse";

    /// <inheritdoc />
    public override string QuoteIdentifier(string identifier) => $"`{identifier}`";

    /// <summary>ClickHouse 没有事务：一批写入逐条生效。</summary>
    public override bool SupportsTransactions => false;

    /// <summary>
    /// <c>@p0</c>（ClickHouse.Driver 按值推断类型，改写成 <c>{p0:类型}</c>）；DateTime 参数直接写成 <c>{p0:DateTime64(7)}</c>：
    /// 驱动推断出的 DateTime 只到秒，毫秒会丢。
    /// </summary>
    public override string ParameterPlaceholder(int index, SqlParameterValue parameter) =>
        (Nullable.GetUnderlyingType(parameter.ClrType) ?? parameter.ClrType) != typeof(DateTime) ? "@p" + index
        : Nullable.GetUnderlyingType(parameter.ClrType) is null ? $"{{p{index}:DateTime64(7)}}"
        : $"{{p{index}:Nullable(DateTime64(7))}}";

    /// <summary>ClickHouse 是分析型数据库，没有行级锁（与 Java 一致，不能悄悄忽略）。</summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock) =>
        throw new NotSupportedException("ClickHouse 不支持 FOR UPDATE 行锁：它是分析型数据库，没有行级锁");

    /// <summary><c>ALTER TABLE `t` UPDATE `c` = ? WHERE …</c>（与 Java 的 ClickHouseUpdateSQLExpression 一致）；mutation 必须带条件，没有条件时写 <c>WHERE 1 = 1</c>。</summary>
    public override void WriteUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        RejectAlias(update.Table);
        renderer.Append("ALTER TABLE ").WriteTableName(update.Table).Append(" UPDATE ").WriteSets(update.Sets);
        WriteMutationWhere(renderer, update.Where);
    }

    /// <summary><c>ALTER TABLE `t` DELETE WHERE …</c>（与 Java 的 ClickHouseDeleteSQLExpression 一致）。</summary>
    public override void WriteDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        RejectAlias(statement.Table);
        renderer.Append("ALTER TABLE ").WriteTableName(statement.Table).Append(" DELETE");
        WriteMutationWhere(renderer, statement.Where);
    }

    private static void WriteMutationWhere(SqlRenderer renderer, IReadOnlyList<SqlExpr> where)
    {
        if (where.Count > 0) renderer.WriteWhere(where);
        else renderer.Append(" WHERE 1 = 1");
    }

    // 条件里有关联子查询时目标表会分到别名，ALTER TABLE 不能写别名（Java 同样只支持单表）
    private static void RejectAlias(EntityTableSource table)
    {
        if (table.Alias is not null)
            throw new NotSupportedException("ClickHouse 的 ALTER TABLE … UPDATE / DELETE 不能带表别名：条件里不能用关联子查询，请改成 IN (子查询)");
    }

    /// <inheritdoc />
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update) =>
        throw new NotSupportedException("ClickHouse 的 ALTER TABLE … UPDATE 不能连接别的表：不能在 SET 或条件里用对一导航，请改写成 IN (子查询)");

    /// <inheritdoc />
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement) =>
        throw new NotSupportedException("ClickHouse 的 ALTER TABLE … DELETE 不能连接别的表：不能在条件里用对一导航，请改写成 IN (子查询)");

    /// <summary>没有 upsert 语法（Java 照搬了 MySQL 的 INSERT IGNORE / ON DUPLICATE KEY，ClickHouse 执行不了）；按主键去重用 ReplacingMergeTree 表引擎。</summary>
    public override void WriteInsertConflict(SqlRenderer renderer, InsertStatement insert) =>
        throw new NotSupportedException("ClickHouse 没有插入冲突处理（upsert）：按主键去重请用 ReplacingMergeTree 表引擎，再普通插入");

    /// <summary>没有自增列：主键要由应用给值（雪花 ID、UUID）。</summary>
    public override void WriteInsertReturning(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning) => throw NoIdentity();

    private static NotSupportedException NoIdentity() => new("ClickHouse 没有自增列：主键要由应用给值（雪花 ID、UUID 等）");

    /// <summary>值里含 % 或 _ 时转义后按字面匹配（反斜杠本来就是 ClickHouse LIKE 的转义符，不写 ESCAPE）。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (predicate.HasWildcardValue) WriteEscapedLike(renderer, predicate, "");
        else base.WriteLike(renderer, predicate);
    }

    /// <summary>带组内排序的字符串聚合：ClickHouse 的 groupArray 不保证顺序，没有对应写法。</summary>
    public override void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join) =>
        throw new NotSupportedException("ClickHouse 不支持带组内排序的字符串聚合（groupArray 不保证顺序）");

    /// <summary>
    /// 类型转换（对应 Java 的 ClickHouseCastSQLFunction，类型名改成 ClickHouse 区分大小写的写法：Int32、Float64、String、DateTime64）。
    /// </summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "(toString({0}) NOT IN ('0','false'))",
        _ when targetType == typeof(char) => "substringUTF8(toString({0}), 1, 1)",
        _ when targetType == typeof(DateOnly) => "CAST({0} AS Date32)",
        _ when targetType == typeof(DateTime) => "CAST({0} AS DateTime64(3))",
        _ when targetType == typeof(decimal) => "CAST({0} AS Decimal(38,18))",
        _ when targetType == typeof(double) => "CAST({0} AS Float64)",
        _ when targetType == typeof(float) => "CAST({0} AS Float32)",
        _ when IsInteger(targetType) => "CAST({0} AS " + IntegerType(targetType) + ")",
        _ when targetType == typeof(Guid) || targetType == typeof(string) => "toString({0})",
        _ => throw new NotSupportedException($"{Name} 不支持转换到 {targetType.Name}"),
    };

    private static string IntegerType(Type type) => type switch
    {
        _ when type == typeof(sbyte) => "Int8",
        _ when type == typeof(byte) => "UInt8",
        _ when type == typeof(short) => "Int16",
        _ when type == typeof(ushort) => "UInt16",
        _ when type == typeof(int) => "Int32",
        _ when type == typeof(uint) => "UInt32",
        _ when type == typeof(long) => "Int64",
        _ => "UInt64",
    };

    /// <summary>
    /// 建表：<c>CREATE TABLE IF NOT EXISTS `t` (…) ENGINE = MergeTree ORDER BY (`主键`)</c>，没有主键时 <c>ORDER BY tuple()</c>。
    /// MergeTree 的主键就是排序键，不写 PRIMARY KEY 约束。
    /// </summary>
    public override string CreateTableSql(EntityMetadata entity)
    {
        var columns = string.Join(", ", TableColumns(entity).Select(c => ColumnDefinition(entity, c)));
        var order = entity.KeyColumns.Count > 0 ? "(" + string.Join(",", entity.KeyColumns.Select(k => QuoteIdentifier(k.Name))) + ")" : "tuple()";
        return CreateTableStatement(entity, columns) + " ENGINE = MergeTree ORDER BY " + order;
    }

    /// <summary><c>`列` 类型 NULL|NOT NULL[ DEFAULT 默认值]</c>：ClickHouse 的默认值表达式要写在可空性后面。</summary>
    protected override string ColumnDefinition(EntityMetadata entity, ColumnMetadata column) =>
        column.IsGenerated
            ? throw NoIdentity()
            : $"{QuoteIdentifier(column.Name)} {ColumnType(column)} {(IsNullable(column) ? "NULL" : "NOT NULL")}{DefaultClause(column)}";

    /// <summary>表注释：<c>ALTER TABLE 表 MODIFY COMMENT '…'</c>（没有 COMMENT ON）。</summary>
    internal override string? TableCommentSql(EntityMetadata entity) =>
        entity.Comment is null ? null : $"ALTER TABLE {QualifiedTable(entity)} MODIFY COMMENT {SqlString(entity.Comment)}";

    /// <summary>列注释：<c>ALTER TABLE 表 COMMENT COLUMN 列 '…'</c>。</summary>
    internal override string? ColumnCommentSql(EntityMetadata entity, ColumnMetadata column) =>
        column.Comment is null ? null : $"ALTER TABLE {QualifiedTable(entity)} COMMENT COLUMN {QuoteIdentifier(column.Name)} {SqlString(column.Comment)}";

    /// <summary>
    /// 索引是跳数索引：<c>ALTER TABLE 表 ADD INDEX 名 (列, …) TYPE minmax GRANULARITY 1</c>（同 TS 版）；没有唯一索引，也不能指定升降序。
    /// </summary>
    internal override string CreateIndexSql(EntityMetadata entity, IndexMetadata index) => index.Unique
        ? throw new NotSupportedException($"ClickHouse 没有唯一索引：{entity.Type.Name} 的索引 {index.Name}")
        : $"ALTER TABLE {QualifiedTable(entity)} ADD INDEX {QuoteIdentifier(index.Name)} ({QuotedNames(index.Columns.Select(c => c.Column), ", ")}) TYPE minmax GRANULARITY 1";

    /// <summary>ClickHouse 没有外键。</summary>
    internal override string AddForeignKeySql(ForeignKeyMetadata foreignKey) =>
        throw new NotSupportedException($"ClickHouse 没有外键：{foreignKey.Entity.Type.Name} 上的 [ForeignKey] {foreignKey.Name}");

    /// <summary>改表名：<c>RENAME TABLE 旧表 TO 新表</c>。</summary>
    internal override string RenameTableSql(EntityMetadata entity, string oldName) =>
        $"RENAME TABLE {QualifiedName(entity.Schema, oldName)} TO {QualifiedTable(entity)}";

    /// <summary>读 system.tables / system.columns / system.data_skipping_indices（当前库，同 TS 版）；可空列的类型去掉 Nullable(…)，没有外键。</summary>
    internal override CatalogQueries CatalogQueries { get; } = new(
        "SELECT name AS table_name, comment FROM system.tables WHERE database = currentDatabase() AND NOT is_temporary AND engine NOT LIKE '%View' ORDER BY name",
        "SELECT table AS table_name, name AS column_name, replaceRegexpOne(type, '^Nullable\\\\((.*)\\\\)$', '\\\\1') AS column_type, "
        + "startsWith(type, 'Nullable(') AS is_nullable, if(default_kind = '', NULL, default_expression) AS column_default, "
        + "if(comment = '', NULL, comment) AS comment, 0 AS is_generated, is_in_sorting_key AS primary_key "
        + "FROM system.columns WHERE database = currentDatabase() ORDER BY table, position",
        "SELECT table AS table_name, name AS index_name, 0 AS is_unique, trim(BOTH ' ' FROM column_name) AS column_name "
        + "FROM system.data_skipping_indices ARRAY JOIN splitByChar(',', expr) AS column_name WHERE database = currentDatabase() ORDER BY table, name",
        null);

    /// <summary>ClickHouse 的类型名（区分大小写）：String、Int32、Bool、Float64、DateTime64(3)、Date32、UUID。</summary>
    protected override string ClrColumnType(Type type, ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) || type == typeof(byte[]) => "String",
        _ when type.IsEnum => "Int32",
        _ when IsInteger(type) => IntegerType(type),
        _ when type == typeof(bool) => "Bool",
        _ when type == typeof(double) => "Float64",
        _ when type == typeof(float) => "Float32",
        _ when type == typeof(DateTime) => "DateTime64(3)",
        _ when type == typeof(DateOnly) => "Date32",
        _ when type == typeof(Guid) => "UUID",
        _ => base.ClrColumnType(type, column),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 与 Java 的 ClickHouseBooleanSQLFunction 一致
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
        f[SqlFunctionNames.InCsv] = Template("has(splitByChar(',',{1}),toString({0}))");
        // 与 Java 的 ClickHouseMaxMinColumnsSQLFunction 一致
        f[SqlFunctionNames.MaxColumns] = Call("GREATEST");
        f[SqlFunctionNames.MinColumns] = Call("LEAST");
        // 字符串按字符处理用 UTF8 版本（不带 UTF8 的按字节）
        f[SqlFunctionNames.SubString] = Call("substringUTF8");
        f[SqlFunctionNames.PadLeft] = Pad("leftPadUTF8", null);
        f[SqlFunctionNames.PadRight] = Pad("rightPadUTF8", null);
        // C# 的 IndexOf 是 0 起点，翻译时再减 1
        f[SqlFunctionNames.IndexOf] = Call("positionUTF8");
        // log 只有一个参数
        f[SqlFunctionNames.LogBase] = Template("(log({0}) / log({1}))");
        f[SqlFunctionNames.Random] = Template("rand()");
        f[SqlFunctionNames.Now] = Template("NOW()");
        // 与 Java 的 ClickHouseUtcNowSQLFunction 一致
        f[SqlFunctionNames.UtcNow] = Template("NOW('UTC')");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("arrayStringConcat(groupArray(").Write(a[0]).Append("),").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("arrayStringConcat(groupUniqArray(").Write(a[0]).Append("),").Write(a[1]).Append(")");
        // JSON：键作为 JSONExtract* 的参数；数组下标从 1 开始
        f[SqlFunctionNames.JsonGet] = Template("JSONExtractRaw({0},'{1}')");
        f[SqlFunctionNames.JsonGetIndex] = JsonIndex("JSONExtractRaw");
        f[SqlFunctionNames.JsonText] = Template("JSONExtractString({0},'{1}')");
        f[SqlFunctionNames.JsonTextIndex] = JsonIndex("JSONExtractString");
        f[SqlFunctionNames.JsonLength] = Call("JSONLength");
        f[SqlFunctionNames.JsonContainsKey] = Template("JSONHas({0},'{1}')");
        // 周从周一起（date_trunc 的 week 是 ISO 周）；date_trunc 截到周及以上时返回 Date，统一转回 DateTime
        foreach (var unit in Enum.GetValues<DateTruncUnit>())
            f[SqlFunctionNames.DateTrunc(unit)] = Template("toDateTime(date_trunc('" + Lower(unit) + "',{0}))");
        // %i 是分钟（23.4 起 %M 是月份名）
        f[SqlFunctionNames.DateFormat] = DateFormat("formatDateTime({0},{1})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%i"), ("ss", "%S"));
        DateFunctions(f,
            unit => unit == DateUnit.Millisecond ? "addMilliseconds({0},{1})" : "add" + unit + "s({0},{1})",
            part => part switch
            {
                DatePart.Day => "toDayOfMonth({0})",
                // toDayOfWeek 周一为 1、周日为 7
                DatePart.DayOfWeek => "(toDayOfWeek({0}) % 7)",
                DatePart.IsoDayOfWeek => "toDayOfWeek({0})",
                _ => "to" + part + "({0})",
            },
            // dateDiff 数的是跨过的边界；按经过的秒数整除，与其余方言一样截断
            unit => unit switch
            {
                DateUnit.Millisecond => "dateDiff('millisecond',{0},{1})",
                DateUnit.Second => "dateDiff('second',{0},{1})",
                _ => "intDiv(dateDiff('second',{0},{1})," + (unit switch { DateUnit.Minute => 60, DateUnit.Hour => 3600, _ => 86400 }).ToString(CultureInfo.InvariantCulture) + ")",
            });
    }

    // JSONExtract* 的数组下标从 1 开始：C# 的 0 基下标是字面量，直接加 1
    private static FunctionWriter JsonIndex(string function) => (r, a) =>
        r.Append(function + "(").Write(a[0]).Append(",").Append((int.Parse(((LiteralExpr)a[1]).Sql, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture)).Append(")");
}
