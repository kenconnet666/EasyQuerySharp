namespace EasyQuerySharp;

/// <summary>
/// 窗口定义（<c>OVER (…)</c> 的内容），只在查询表达式里用，作为 <see cref="Sql.Over"/> 各函数的最后一个参数：
/// <code>Sql.Over.RowNumber(w =&gt; w.PartitionBy(b.Title).OrderBy(b.CreateTime))</code>
/// <see cref="OrderBy{T}"/> / <see cref="OrderByDescending{T}"/> 每调用一次追加一个排序项；不写 PartitionBy 是整表一个窗口。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class Window
{
    private Window() { }

    /// <summary>按这些值分区（<c>PARTITION BY a, b</c>）。</summary>
    public Window PartitionBy(params object?[] keys) => throw Sql.OnlyInQuery();

    /// <summary>追加升序排序项；NULL 的位置可用 <see cref="Sql.NullsFirst{T}"/> / <see cref="Sql.NullsLast{T}"/> 包一层。</summary>
    public Window OrderBy<T>(T key) => throw Sql.OnlyInQuery();

    /// <summary>追加降序排序项。</summary>
    public Window OrderByDescending<T>(T key) => throw Sql.OnlyInQuery();

    /// <summary>按物理行取窗口：<c>ROWS BETWEEN start AND end</c>，如累计 <c>Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow)</c>。</summary>
    public Window Rows(WindowBound start, WindowBound end) => throw Sql.OnlyInQuery();

    /// <summary>按排序值取窗口（值相同的行同进同出）：<c>RANGE BETWEEN start AND end</c>。</summary>
    public Window Range(WindowBound start, WindowBound end) => throw Sql.OnlyInQuery();
}

/// <summary>窗口 frame 的一端。</summary>
public readonly record struct WindowBound
{
    private WindowBound(string sql) => Sql = sql;

    /// <summary>SQL 写法，如 <c>3 PRECEDING</c>。</summary>
    public string Sql { get; }

    /// <summary>分区第一行。</summary>
    public static WindowBound UnboundedPreceding { get; } = new("UNBOUNDED PRECEDING");

    /// <summary>当前行。</summary>
    public static WindowBound CurrentRow { get; } = new("CURRENT ROW");

    /// <summary>分区最后一行。</summary>
    public static WindowBound UnboundedFollowing { get; } = new("UNBOUNDED FOLLOWING");

    /// <summary>当前行之前第 <paramref name="rows"/> 行。</summary>
    public static WindowBound Preceding(int rows) => new(Offset(rows) + " PRECEDING");

    /// <summary>当前行之后第 <paramref name="rows"/> 行。</summary>
    public static WindowBound Following(int rows) => new(Offset(rows) + " FOLLOWING");

    // 偏移量直接写进 SQL，只接受非负整数
    private static string Offset(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        return rows.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
