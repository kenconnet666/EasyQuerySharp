using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// TDengine 3.x 方言（时序库，对应 Java 的 sql-tsdb）。标识符用反引号，分页 <c>LIMIT n OFFSET m</c>。时序库的模型：
/// <list type="bullet">
/// <item>每张表的第一列是 TIMESTAMP 主键：实体要有一个 <see cref="DateTime"/> 主键，建表时排在第一列（插入的列按属性顺序，主键属性也放在第一个），列不写 NULL / NOT NULL；</item>
/// <item>同一时间戳再插入一次就是覆盖：没有 UPDATE（与 Java 一致，抛 <see cref="NotSupportedException"/>），也没有冲突处理（upsert）；</item>
/// <item>删除写成 <c>DELETE FROM 表 WHERE …</c>，不带别名、不走导航；没有自增列、行锁、事务。</item>
/// </list>
/// 驱动（如 TDengine.Connector）由用户通过 <see cref="System.Data.Common.DbDataSource"/> 传入：<c>o.UseTDengine(dataSource)</c>。
/// </summary>
public class TDengineDialect : SqlDialect
{
    /// <summary>共享实例。</summary>
    public static TDengineDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "tdengine";

    /// <inheritdoc />
    public override string QuoteIdentifier(string identifier) => $"`{identifier}`";

    /// <summary>TDengine 没有行级锁。</summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock) =>
        throw new NotSupportedException("TDengine 不支持 FOR UPDATE 行锁");

    /// <summary>没有 UPDATE（与 Java 的 TSDBUpdateSQLExpression 一致）。</summary>
    public override void WriteUpdate(SqlRenderer renderer, UpdateStatement update) => throw NoUpdate();

    /// <inheritdoc />
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update) => throw NoUpdate();

    private static NotSupportedException NoUpdate() => new("TDengine 没有 UPDATE：按同一时间戳再插入一次，即覆盖给出的列");

    /// <summary><c>DELETE FROM `t` WHERE …</c>：只能按本表的列过滤（一般是时间范围），不能带别名。</summary>
    public override void WriteDelete(SqlRenderer renderer, DeleteStatement statement)
    {
        if (statement.Table.Alias is not null) throw NoJoinedDelete();
        base.WriteDelete(renderer, statement);
    }

    /// <inheritdoc />
    public override void WriteJoinedDelete(SqlRenderer renderer, DeleteStatement statement) => throw NoJoinedDelete();

    private static NotSupportedException NoJoinedDelete() => new("TDengine 的 DELETE 只能按本表的列过滤（一般是时间范围），不能用导航或关联子查询");

    /// <summary>同一时间戳再插入就是覆盖，没有冲突处理的语法。</summary>
    public override void WriteInsertConflict(SqlRenderer renderer, InsertStatement insert) =>
        throw new NotSupportedException("TDengine 没有插入冲突处理（upsert）：同一时间戳再插入一次就是覆盖，直接用普通插入");

    /// <summary>没有自增列：主键是行的时间戳。</summary>
    public override void WriteInsertReturning(SqlRenderer renderer, IReadOnlyList<SqlExpr> returning) => throw NoIdentity();

    private static NotSupportedException NoIdentity() => new("TDengine 没有自增列：主键是行的时间戳");

    /// <summary>值里含 % 或 _ 时转义后按字面匹配（反斜杠本来就是 TDengine LIKE 的转义符，不写 ESCAPE）。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (predicate.HasWildcardValue) WriteEscapedLike(renderer, predicate, "");
        else base.WriteLike(renderer, predicate);
    }

    /// <inheritdoc />
    public override void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join) =>
        throw new NotSupportedException("TDengine 不支持字符串聚合");

    /// <summary>TDengine 的 CAST 目标类型：BOOL、TINYINT … BIGINT（含 UNSIGNED）、FLOAT、DOUBLE、TIMESTAMP、VARCHAR(n)。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "CAST({0} AS BOOL)",
        _ when targetType == typeof(char) => "SUBSTR(CAST({0} AS VARCHAR(4096)), 1, 1)",
        _ when targetType == typeof(DateTime) => "CAST({0} AS TIMESTAMP)",
        _ when targetType == typeof(double) => "CAST({0} AS DOUBLE)",
        _ when targetType == typeof(float) => "CAST({0} AS FLOAT)",
        _ when IsInteger(targetType) => "CAST({0} AS " + IntegerType(targetType) + ")",
        _ when targetType == typeof(Guid) || targetType == typeof(string) => "CAST({0} AS VARCHAR(4096))",
        _ => throw new NotSupportedException($"{Name} 不支持转换到 {targetType.Name}"),
    };

    private static string IntegerType(Type type) => type switch
    {
        _ when type == typeof(sbyte) => "TINYINT",
        _ when type == typeof(byte) => "TINYINT UNSIGNED",
        _ when type == typeof(short) => "SMALLINT",
        _ when type == typeof(ushort) => "SMALLINT UNSIGNED",
        _ when type == typeof(int) => "INT",
        _ when type == typeof(uint) => "INT UNSIGNED",
        _ when type == typeof(long) => "BIGINT",
        _ => "BIGINT UNSIGNED",
    };

    /// <summary>建表：时间戳主键排在第一列，其余列只有名字与类型，没有主键约束。</summary>
    public override string CreateTableSql(EntityMetadata entity)
    {
        if (entity.KeyColumns is not [var key] || (Nullable.GetUnderlyingType(key.ClrType) ?? key.ClrType) != typeof(DateTime))
            throw new NotSupportedException($"TDengine 的表要有一个 DateTime 主键（行的时间戳）：{entity.Type.Name} 不满足");
        var columns = entity.Columns.Where(c => c != key).Prepend(key).Select(c => ColumnDefinition(entity, c));
        return CreateTableStatement(entity, string.Join(", ", columns));
    }

    /// <summary>列只有名字与类型：TDengine 的列不能声明 NOT NULL（时间戳主键本身不能为空）。</summary>
    protected override string ColumnDefinition(EntityMetadata entity, ColumnMetadata column) =>
        column.IsGenerated ? throw NoIdentity() : $"{QuoteIdentifier(column.Name)} {ColumnType(column)}";

    /// <summary>TDengine 的类型名：NCHAR（Unicode 字符串）、BOOL、INT、TINYINT UNSIGNED、DOUBLE、VARBINARY。</summary>
    protected override string ClrColumnType(Type type, ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) => "NCHAR(255)",
        _ when type == typeof(Guid) => "NCHAR(36)",
        _ when type.IsEnum => "INT",
        _ when IsInteger(type) => IntegerType(type),
        _ when type == typeof(bool) => "BOOL",
        _ when type == typeof(double) => "DOUBLE",
        _ when type == typeof(float) => "FLOAT",
        _ when type == typeof(byte[]) => "VARBINARY(4096)",
        _ => base.ClrColumnType(type, column),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 与 Java 的 TSDBMaxMinColumnsSQLFunction 一致
        f[SqlFunctionNames.MaxColumns] = Call("GREATEST");
        f[SqlFunctionNames.MinColumns] = Call("LEAST");
        // C# 的 IndexOf 是 0 起点，翻译时再减 1
        f[SqlFunctionNames.IndexOf] = Template("POSITION({1} IN {0})");
        f[SqlFunctionNames.Power] = Call("POW");
        // LOG(x, 底数)：底数在后
        f[SqlFunctionNames.LogBase] = Call("LOG");
        f[SqlFunctionNames.Truncate] = WithDigits("TRUNCATE");
        f[SqlFunctionNames.Random] = Template("RAND()");
        f[SqlFunctionNames.Now] = Template("NOW()");
        // 时间戳是绝对时刻（按 UTC 存），NOW() 即当前的 UTC 时刻；显示成哪个时区由客户端决定
        f[SqlFunctionNames.UtcNow] = Template("NOW()");
        foreach (var name in new[] { SqlFunctionNames.InCsv, SqlFunctionNames.StringJoin, SqlFunctionNames.StringJoinDistinct })
            f[name] = (_, _) => throw new NotSupportedException($"TDengine 方言不支持 {name}");
        // 时长只能写成字面量（ts + 1d），不能由参数给出，而且整数加减按库的时间精度换算
        foreach (var unit in Enum.GetValues<DateUnit>())
            f[SqlFunctionNames.DateAdd(unit)] = (_, _) => throw new NotSupportedException($"TDengine 不支持按参数加减时间（DateAdd {unit}）：时长只能写成字面量，如 ts + 1d");
        // 时长字面量：1d、1h、1m、1s、1a（毫秒）；TIMETRUNCATE 只能截到天及以下（周按 1970-01-01 对齐，不是周一）
        foreach (var (unit, duration) in new[] { (DateUnit.Day, "1d"), (DateUnit.Hour, "1h"), (DateUnit.Minute, "1m"), (DateUnit.Second, "1s"), (DateUnit.Millisecond, "1a") })
        {
            f[SqlFunctionNames.DateDiff(unit)] = Template("TIMEDIFF({1},{0}," + duration + ")");
            if (unit != DateUnit.Millisecond) f[SqlFunctionNames.DateTrunc(Enum.Parse<DateTruncUnit>(unit.ToString()))] = Template("TIMETRUNCATE({0}," + duration + ")");
        }
        f[SqlFunctionNames.DateFormat] = DateFormat("TO_CHAR({0},{1})", ("yyyy", "yyyy"), ("MM", "mm"), ("dd", "dd"), ("HH", "hh24"), ("mm", "mi"), ("ss", "ss"));
        foreach (var part in Enum.GetValues<DatePart>())
            f[SqlFunctionNames.DatePartOf(part)] = Template(part switch
            {
                // DAYOFWEEK 周日为 1，WEEKDAY 周一为 0
                DatePart.DayOfWeek => "(DAYOFWEEK({0})-1)",
                DatePart.IsoDayOfWeek => "(WEEKDAY({0})+1)",
                _ => "CAST(TO_CHAR({0},'" + part switch
                {
                    DatePart.Year => "yyyy", DatePart.Month => "mm", DatePart.Day => "dd", DatePart.Hour => "hh24", DatePart.Minute => "mi", DatePart.Second => "ss", _ => "ddd",
                } + "') AS INT)",
            });
    }
}
