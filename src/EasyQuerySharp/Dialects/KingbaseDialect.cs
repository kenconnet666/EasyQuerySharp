using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// 人大金仓 KingbaseES 方言（对应 Java 的 sql-kingbase-es）：PG 内核，只覆盖 Java 的 Kingbase 方言不同于 PostgreSQL 的部分——
/// CONCAT 两两嵌套、包含 / 开头 / 结尾写成 STRPOS、条件当值写成 CASE、日期加减用 CONCAT 拼间隔、日期相差用 timestampdiff、树查询起点层级写 <c>0+0</c>。
/// 驱动由调用方通过 <see cref="System.Data.Common.DbDataSource"/> 传入：<c>o.UseKingbase(dataSource)</c>。
/// </summary>
public class KingbaseDialect : PostgreSqlDialect
{
    /// <summary>共享实例。</summary>
    public static new KingbaseDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Name => "kingbase";

    /// <inheritdoc />
    public override string TreeDeepStart => "0+0";

    /// <summary>PG 写成 <c>LIKE CONCAT(…)</c> 的包含 / 开头 / 结尾改写成 STRPOS（与 Java 的 KingbaseESLikeSQLFunction 一致），其余同 PG。</summary>
    public override void WriteLike(SqlRenderer renderer, LikeExpr predicate)
    {
        if (!GaussDbDialect.WriteStrpos(renderer, predicate)) base.WriteLike(renderer, predicate);
    }

    /// <inheritdoc />
    protected override void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        base.RegisterFunctions(f);
        f[SqlFunctionNames.BooleanValue] = CaseBoolean;
        // Oracle 兼容模式下整数 / 整数得到小数；PG 模式下 TRUNC 不改变结果
        f[SqlFunctionNames.IntegerDivide] = IntegerDivideByTrunc;
        // Oracle 兼容模式的 CONCAT 只接受两个参数，多个参数两两嵌套：CONCAT(CONCAT(a,b),c)（与 Java 的 KingbaseESConcatSQLFunction 一致）
        f[SqlFunctionNames.Concat] = (r, a) =>
        {
            for (var i = 2; i < a.Count; i++) r.Append("CONCAT(");
            r.Append("CONCAT(").WriteList(a.Take(2).ToList(), ",").Append(")");
            for (var i = 2; i < a.Count; i++) r.Append(",").Write(a[i]).Append(")");
        };
        // 日期加减（Java 的 KingbaseESDateTime2PlusSQLFunction；加月取 plusMonths 的写法，逗号后没有空格）
        foreach (var unit in Enum.GetValues<DateUnit>())
        {
            var name = unit == DateUnit.Millisecond ? "milliseconds" : Lower(unit);
            var separator = unit == DateUnit.Month ? "," : ", ";
            f[SqlFunctionNames.DateAdd(unit)] = Template("(({0})::TIMESTAMP + CONCAT(({1})" + separator + "' " + name + "')::INTERVAL)");
        }
        // 日期相差（Java 的 KingbaseESDateTimeDuration2SQLFunction）；毫秒 Java 不支持，沿用 PG 的写法
        foreach (var unit in new[] { DateUnit.Day, DateUnit.Hour, DateUnit.Minute, DateUnit.Second })
            f[SqlFunctionNames.DateDiff(unit)] = Template("timestampdiff('" + unit.ToString().ToUpperInvariant() + "', {0}, {1})");
    }
}
