using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// DuckDB 方言（进程内的分析型数据库，1.5 实测）。SQL 与 PostgreSQL 接近，继承 PostgreSQL 方言，只覆盖差异。
/// 驱动用 DuckDB.NET：<c>o.UseDuckDb(DuckDBClientFactory.Instance.CreateDataSource("Data Source=my.duckdb"))</c>。
/// 没有行锁（<c>FOR UPDATE</c> 直接报错）；自增列用序列实现（DuckDB 不支持 IDENTITY）。
/// </summary>
public class DuckDbDialect : PostgreSqlDialect
{
    /// <summary>共享实例。</summary>
    public new static DuckDbDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "duckdb";

    /// <summary>DuckDB.NET 认 <c>$name</c> / <c>$1</c> 形式的参数，不认 <c>@name</c>。</summary>
    public override string ParameterPlaceholder(int index) => "$p" + index;

    /// <summary>DuckDB 是分析型数据库，没有行级锁（与 Java 一致，不能悄悄忽略）。</summary>
    public override void WriteLock(SqlRenderer renderer, RowLock rowLock) =>
        throw new NotSupportedException("DuckDB 不支持 FOR UPDATE 行锁：它是分析型数据库，没有行级锁");

    /// <summary>
    /// 值里含 % 或 _ 时用 <c>STRPOS</c> 比较，值按字面匹配、不写 ESCAPE（与 Java 的 DuckDBSQLLikeSQLFunction 一致）：
    /// <c>STRPOS(列,值) = 1</c>、<c>STRPOS(列,值) = (CHAR_LENGTH(列) - CHAR_LENGTH(值) + 1)</c>、<c>STRPOS(列,值) &gt; 0</c>，取反时外面套 <c>(NOT (…))</c>。
    /// 其余情况同 PostgreSQL。
    /// </summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!predicate.HasWildcardValue)
        {
            base.WriteLike(renderer, predicate);
            return;
        }
        if (predicate.Negated) renderer.Append("(NOT (");
        renderer.Append("STRPOS(").Write(predicate.Operand).Append(",").Write(predicate.Value).Append(")");
        switch (predicate.Mode)
        {
            case LikeMode.StartsWith:
                renderer.Append(" = 1");
                break;
            case LikeMode.EndsWith:
                renderer.Append(" = (CHAR_LENGTH(").Write(predicate.Operand).Append(") - CHAR_LENGTH(").Write(predicate.Value).Append(") + 1)");
                break;
            default:
                renderer.Append(" > 0");
                break;
        }
        if (predicate.Negated) renderer.Append("))");
    }

    /// <summary>带组内排序的字符串聚合：值先转成 TEXT（与 Java 的 DuckDBSQLJoiningSQLFunction 一致，DuckDB 不会把数字隐式转成字符串）。</summary>
    public override void WriteOrderedStringJoin(SqlRenderer renderer, OrderedStringJoinExpr join)
    {
        renderer.Append(join.Distinct ? "STRING_AGG(DISTINCT (" : "STRING_AGG((").Write(join.Value).Append(")::TEXT, ").Write(join.Separator).Append(" ");
        WriteOrderBy(renderer, join.Orders);
        renderer.Append(")");
    }

    /// <summary>
    /// json 列的参数不转换：DuckDB 没有 jsonb，字符串参数写入 JSON 列时隐式转换（PostgreSQL 的 <c>(?)::jsonb</c> 在这里是错误）。
    /// </summary>
    public override (string Before, string After)? ParameterWrap(string? columnDbType) => null;

    /// <summary>
    /// 类型名与 Java 的 DuckDBSQLMigrationEntityParser 一致：VARCHAR、DOUBLE、UUID、BLOB；
    /// DuckDB 有无符号整型，C# 的 byte / ushort / uint / ulong 与 sbyte 各取同宽的类型。
    /// </summary>
    protected override string ClrColumnType(Type type, ColumnMetadata column) => type switch
    {
        _ when type == typeof(string) => "VARCHAR",
        _ when type == typeof(sbyte) => "TINYINT",
        _ when type == typeof(byte) => "UTINYINT",
        _ when type == typeof(ushort) => "USMALLINT",
        _ when type == typeof(uint) => "UINTEGER",
        _ when type == typeof(ulong) => "UBIGINT",
        _ when type == typeof(double) => "DOUBLE",
        _ when type == typeof(Guid) => "UUID",
        _ when type == typeof(byte[]) => "BLOB",
        _ => base.ClrColumnType(type, column),
    };

    /// <summary>自增列用序列：先 <c>CREATE SEQUENCE</c>，列的默认值取 <c>nextval</c>（DuckDB 不支持 IDENTITY）。</summary>
    public override string CreateTableSql(EntityMetadata entity) =>
        string.Concat(entity.Columns.Where(c => c.IsGenerated).Select(c => $"CREATE SEQUENCE IF NOT EXISTS {SequenceName(entity, c)}; ")) + base.CreateTableSql(entity);

    /// <inheritdoc />
    protected override string ColumnDefinition(EntityMetadata entity, ColumnMetadata column) => column.IsGenerated
        ? $"{QuoteIdentifier(column.Name)} {ColumnType(column)} DEFAULT nextval('{SequenceName(entity, column)}') NOT NULL"
        : base.ColumnDefinition(entity, column);

    /// <summary>先删表再删它的自增序列（序列被表引用时不能先删）。</summary>
    public override string DropTableIfExistsSql(EntityMetadata entity) =>
        base.DropTableIfExistsSql(entity) + string.Concat(entity.Columns.Where(c => c.IsGenerated).Select(c => $"; DROP SEQUENCE IF EXISTS {SequenceName(entity, c)}"));

    // 序列名只用字母数字下划线，不必加引号（nextval 的参数是字符串，里面再加引号要转义）
    private static string SequenceName(EntityMetadata entity, ColumnMetadata column) =>
        string.Concat($"{entity.Schema}_{entity.TableName}_{column.Name}_seq".Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '_'));

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 与 Java 的 DuckDBSQLMathSQLFunction 一致：TRUNC 不带小数位
        f[SqlFunctionNames.Truncate] = (r, a) =>
        {
            r.Append("TRUNC(").Write(a[0]);
            if (a.Count > 1) r.Append(",").Write(a[1]);
            r.Append(")");
        };
        f[SqlFunctionNames.PadLeft] = Pad("LPAD", "' '");
        f[SqlFunctionNames.PadRight] = Pad("RPAD", "' '");
        // C# 的 IndexOf 是 0 起点，翻译时再减 1（与 Java 的 DuckDBSQLIndexOfSQLFunction 一致）
        f[SqlFunctionNames.IndexOf] = Call("STRPOS");
        // JSON：DuckDB 的 JSON 扩展没有 PostgreSQL 的 jsonb_*，用 json_array_length / json_exists（Java 照搬了 jsonb 函数，在 DuckDB 上不存在）
        f[SqlFunctionNames.JsonLength] = Call("json_array_length");
        f[SqlFunctionNames.JsonContainsKey] = Template("json_exists({0}, '$.{1}')");
        f[SqlFunctionNames.DateFormat] = DateFormat("strftime({0}, {1})", ("yyyy", "%Y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%M"), ("ss", "%S"));
        // 日期加减、相差（取部分沿用 PostgreSQL 的 extract）。加减把数量拼上单位转成 interval，毫秒用 milliseconds（Java 写成 microsecond，数量差 1000 倍）；
        // DATEDIFF 数跨过的边界（03:59 到 04:01 算 1 小时），与 Java 一致，不是 .NET 的整段时长
        foreach (var unit in Enum.GetValues<DateUnit>())
        {
            f[SqlFunctionNames.DateAdd(unit)] = Template("date_add({0},cast(({1})||' " + Lower(unit) + "s' as interval))");
            f[SqlFunctionNames.DateDiff(unit)] = Template("DATEDIFF('" + Lower(unit) + "', {0}, {1})");
        }
    }
}
