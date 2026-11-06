using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// 达梦 DM8 方言。兼容 Oracle 的大部分写法（双引号、ROWNUM 分页、MERGE、LISTAGG、FROM DUAL），继承 <see cref="OracleDialect"/>，
/// 只覆盖 Java 的 sql-dameng 与 sql-oracle 不同的部分：
/// <list type="bullet">
/// <item>带连接的 UPDATE 写成 <c>UPDATE "a" t INNER JOIN "b" t1 ON … SET …</c>（Oracle 要绕成 MERGE）；</item>
/// <item>转成布尔是 <c>CAST(x AS BIT)</c>，日期相差用 <c>CAST(终点 AS TIMESTAMP WITH TIME ZONE)-起点</c> 的间隔；</item>
/// <item>不排序的字符串聚合不写 <c>WITHIN GROUP(ORDER BY 1)</c>（DamengJoiningSQLFunction）；</item>
/// <item>排序的 NULL 位置用通用的 CASE 模拟（Java 没有给达梦覆盖 orderByNullsMode）。</item>
/// </list>
/// Java 没有覆盖、按 TS 版在 DM8 上实测补的：DATE 只有日期（与 Oracle 不同），截断到秒不能转成 DATE；没有 <c>SYS_EXTRACT_UTC</c>，UTC 时间用 <c>GETUTCDATE()</c>；
/// 加年 / 月用 <c>DATEADD</c>（<c>ADD_MONTHS</c> 返回 DATE，丢掉时间）；
/// 建表用 INT / BIGINT、布尔 BIT、浮点 DOUBLE / REAL、时间 TIME，自增列 <c>IDENTITY(1, 1)</c>。
/// 驱动用达梦的 DmProvider（NuGet 包 DM.DmProvider），把它的 DbDataSource 传给 <c>o.UseDameng(..)</c>。
/// </summary>
public class DamengDialect : OracleDialect
{
    /// <summary>共享实例。</summary>
    public static new DamengDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "dameng";

    /// <summary>达梦的类型名：INT / BIGINT（自增列要整数类型）、布尔 BIT（没有 SQL 的 BOOLEAN）、DOUBLE / REAL、TIME，其余同 Oracle。</summary>
    protected override string ClrColumnType(Type type, Metadata.ColumnMetadata column) => type switch
    {
        _ when type == typeof(int) || type.IsEnum => "INT",
        _ when type == typeof(long) => "BIGINT",
        _ when type == typeof(bool) => "BIT",
        _ when type == typeof(double) => "DOUBLE",
        _ when type == typeof(float) => "REAL",
        _ when type == typeof(TimeOnly) || type == typeof(TimeSpan) => "TIME",
        _ => base.ClrColumnType(type, column),
    };

    /// <inheritdoc />
    protected override string IdentityClause => "IDENTITY(1, 1)";

    /// <summary>读库结构时的列类型：达梦的 VARCHAR / DECIMAL / TIMESTAMP 带长度与精度（同 TS 版）。</summary>
    private protected override string IntrospectedType =>
        "LOWER(c.data_type) || CASE WHEN c.data_type IN ('VARCHAR', 'VARCHAR2', 'CHAR', 'NVARCHAR') THEN '(' || c.char_length || ')' "
        + "WHEN c.data_type IN ('DECIMAL', 'NUMBER', 'NUMERIC') AND c.data_precision IS NOT NULL THEN '(' || c.data_precision || ', ' || c.data_scale || ')' "
        + "WHEN c.data_type IN ('TIMESTAMP', 'TIME') THEN '(' || c.data_scale || ')' ELSE '' END";

    /// <summary>自增列记在 SYSCOLUMNS.INFO2 的最低位（达梦的 user_tab_columns 没有 identity_column）。</summary>
    private protected override string IntrospectedIdentity =>
        "(SELECT BITAND(sc.info2, 1) FROM syscolumns sc JOIN sysobjects so ON so.id = sc.id AND so.subtype$ = 'UTAB' AND so.schid = CURRENT_SCHID() "
        + "WHERE so.name = c.table_name AND sc.name = c.column_name)";

    /// <summary>Java 的 DamengSQLFuncImpl 没有覆盖 orderByNullsMode：NULL 位置沿用通用的 CASE 写法。</summary>
    protected override bool NativeNullsOrder => false;

    /// <summary><c>UPDATE "a" t INNER JOIN "b" t1 ON … SET t."c" = t1."d" WHERE …</c>（与 Java 逐字一致）。</summary>
    public override void WriteJoinedUpdate(SqlRenderer renderer, UpdateStatement update)
    {
        renderer.Append("UPDATE ").WriteTable(update.Table);
        foreach (var join in update.Joins) renderer.WriteJoin(join);
        renderer.Append(" SET ").WriteSets(update.Sets).WriteWhere(update.Where);
    }

    /// <summary>与 Java 的 DamengDateTimeDuration2SQLFunction 一致：终点转成带时区的时间戳再减起点，得到 DAY TO SECOND 间隔。</summary>
    protected override string DateDiffInterval => "(CAST({1} AS TIMESTAMP WITH TIME ZONE)-{0})";

    /// <summary>达梦的类型转换（对应 Java 的 DamengCastSQLFunction）：转成布尔写 BIT，长整数写 BIGINT（达梦有，Oracle 没有），其余同 Oracle。</summary>
    public override string CastTemplate(Type targetType) => targetType switch
    {
        _ when targetType == typeof(bool) => "CAST({0} AS BIT)",
        _ when targetType == typeof(long) || targetType == typeof(uint) || targetType == typeof(ulong) => "CAST({0} AS BIGINT)",
        _ => base.CastTemplate(targetType),
    };

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        // 与 Java 的 DamengJoiningSQLFunction 一致：不排序时不写 WITHIN GROUP
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("LISTAGG(TO_CHAR(").Write(a[0]).Append("), ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("LISTAGG(DISTINCT TO_CHAR(").Write(a[0]).Append("), ").Write(a[1]).Append(")");
        // 与 Java 一致：派生表与列名不加引号（Oracle 上要加，见 OracleDialect）
        f[SqlFunctionNames.MaxColumns] = UnionAllExtreme("MAX", " FROM DUAL");
        f[SqlFunctionNames.MinColumns] = UnionAllExtreme("MIN", " FROM DUAL");
        // DATE 只到日：去掉秒以下的部分要经过字符串
        f[SqlFunctionNames.DateTrunc(DateTruncUnit.Second)] = Template("TO_TIMESTAMP(TO_CHAR({0}, 'YYYY-MM-DD HH24:MI:SS'), 'YYYY-MM-DD HH24:MI:SS')");
        f[SqlFunctionNames.UtcNow] = Template("GETUTCDATE()");
        // Java 沿用 Oracle 的 add_months，但达梦的 ADD_MONTHS 返回只有日期的 DATE（实测丢掉时间）；DATEADD 保留时间，月末同 C# 截到当月最后一天
        f[SqlFunctionNames.DateAdd(DateUnit.Year)] = Template("DATEADD(YEAR,{1},{0})");
        f[SqlFunctionNames.DateAdd(DateUnit.Month)] = Template("DATEADD(MONTH,{1},{0})");
    }
}
