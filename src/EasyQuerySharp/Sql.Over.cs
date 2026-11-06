namespace EasyQuerySharp;

public static partial class Sql
{
    /// <summary>
    /// 窗口函数（对应 Java 的 rowNumberOver() / countOver(x) 等与 offset(...).prev / next）。最后一个参数描述窗口：
    /// <code>
    /// Sql.Over.RowNumber(w =&gt; w.PartitionBy(b.Title).OrderBy(b.CreateTime))       // 每个标题内的序号
    /// Sql.Over.Sum(b.Star, w =&gt; w.PartitionBy(b.Title).OrderBy(b.CreateTime)
    ///     .Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow))           // 累计
    /// Sql.Over.Lag(b.Star, 1, w =&gt; w.OrderBy(b.CreateTime))                        // 上一行，没有为 NULL
    /// </code>
    /// 取值函数（Lag / Lead / FirstValue / LastValue / NthValue）的窗口没写排序、或者用不带窗口的重载时，沿用所在查询的 ORDER BY
    /// （与 Java 的 <c>offset()</c> 一致，取翻译时的排序）；所在查询没有排序时报错。
    /// </summary>
    public static class Over
    {
        /// <summary>分区内的行号，从 1 开始（<c>ROW_NUMBER()</c>）。</summary>
        public static long RowNumber(Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>排名，并列跳号（<c>RANK()</c>）。</summary>
        public static long Rank(Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>排名，并列不跳号（<c>DENSE_RANK()</c>）。</summary>
        public static long DenseRank(Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>相对排名 (rank - 1) / (行数 - 1)（<c>PERCENT_RANK()</c>）。</summary>
        public static double PercentRank(Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>累计分布：小于等于当前值的行占比（<c>CUME_DIST()</c>）。</summary>
        public static double CumeDist(Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>把分区分成 <paramref name="buckets"/> 桶，返回所在桶号（<c>NTILE(n)</c>）。</summary>
        public static long Ntile(int buckets, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口内的行数（<c>COUNT(*)</c>）。</summary>
        public static long Count(Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口内 <paramref name="value"/> 非 NULL 的个数。</summary>
        public static long Count<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口内求和。</summary>
        public static T Sum<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口内平均值。</summary>
        public static T Avg<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口内最大值。</summary>
        public static T Max<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口内最小值。</summary>
        public static T Min<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>往前第 <paramref name="offset"/> 行的值，没有时为 NULL（<c>LAG</c>）。</summary>
        public static T Lag<T>(T value, int offset, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，往前第 <paramref name="offset"/> 行的值，没有时为 NULL。</summary>
        public static T Lag<T>(T value, int offset) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，往前第 <paramref name="offset"/> 行的值，没有时取 <paramref name="defaultValue"/>。</summary>
        public static T Lag<T>(T value, int offset, T defaultValue) => throw OnlyInQuery();

        /// <summary>往前第 <paramref name="offset"/> 行的值，没有时取 <paramref name="defaultValue"/>。</summary>
        public static T Lag<T>(T value, int offset, T defaultValue, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>往后第 <paramref name="offset"/> 行的值，没有时为 NULL（<c>LEAD</c>）。</summary>
        public static T Lead<T>(T value, int offset, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，往后第 <paramref name="offset"/> 行的值，没有时为 NULL。</summary>
        public static T Lead<T>(T value, int offset) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，往后第 <paramref name="offset"/> 行的值，没有时取 <paramref name="defaultValue"/>。</summary>
        public static T Lead<T>(T value, int offset, T defaultValue) => throw OnlyInQuery();

        /// <summary>往后第 <paramref name="offset"/> 行的值，没有时取 <paramref name="defaultValue"/>。</summary>
        public static T Lead<T>(T value, int offset, T defaultValue, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>窗口第一行的值（<c>FIRST_VALUE</c>）。</summary>
        public static T FirstValue<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，第一行的值。</summary>
        public static T FirstValue<T>(T value) => throw OnlyInQuery();

        /// <summary>窗口最后一行的值（<c>LAST_VALUE</c>；默认 frame 只到当前行，要整个分区需写 Rows）。</summary>
        public static T LastValue<T>(T value, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，到当前行为止最后一行的值（默认 frame）。</summary>
        public static T LastValue<T>(T value) => throw OnlyInQuery();

        /// <summary>窗口第 <paramref name="n"/> 行的值（<c>NTH_VALUE</c>）。</summary>
        public static T NthValue<T>(T value, int n, Func<Window, Window> window) => throw OnlyInQuery();

        /// <summary>按所在查询的排序，第 <paramref name="n"/> 行的值。</summary>
        public static T NthValue<T>(T value, int n) => throw OnlyInQuery();
    }
}
