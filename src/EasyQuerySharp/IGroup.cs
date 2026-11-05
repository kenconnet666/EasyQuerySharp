namespace EasyQuerySharp;

/// <summary>
/// 分组查询里的一组（对应 Java 的 group / groupTable）。只能写在 <c>GroupBy</c> 之后的 Having、OrderBy、Select 表达式里，
/// 由翻译器转成 GROUP BY 的键与聚合函数；运行时不会有实例。
/// </summary>
/// <typeparam name="TKey">分组键：单个值，或匿名类型 <c>new { a.X, a.Y }</c>。</typeparam>
/// <typeparam name="TElement">组内元素：单表时是实体，多表时是元组 <c>(T1, T2, …)</c>，用 <c>e.Item2.Score</c> 访问。</typeparam>
/// <example><code>
/// db.Query&lt;Blog&gt;()
///   .GroupBy(b => new { b.Status })
///   .Having(g => g.Count() > 1)
///   .Select(g => new { g.Key.Status, Total = g.Sum(b => b.Score), Titles = g.StringJoin(b => b.Title, ",") })
/// </code></example>
public interface IGroup<out TKey, TElement>
{
    /// <summary>分组键。</summary>
    TKey Key { get; }

    /// <summary>组内只统计满足条件的行（聚合里的值包进 CASE WHEN，对应 Java 的 group.where(…) / filter）。</summary>
    IGroup<TKey, TElement> Where(Func<TElement, bool> predicate);

    /// <summary>接下来的聚合去重（对应 Java 的 group.distinct()）。</summary>
    IGroup<TKey, TElement> Distinct();

    /// <summary><c>COUNT(*)</c>。</summary>
    long Count();

    /// <summary>
    /// 满足条件的行数：<c>COUNT(CASE WHEN 条件 THEN 1 ELSE NULL END)</c>，与 LINQ 的 <c>Count(predicate)</c> 一致（如 <c>g.Count(p =&gt; p.Paid)</c>）。
    /// 要数某列非空的个数用 <see cref="Count{TValue}"/>，布尔列写成 <c>g.Count&lt;bool?&gt;(p =&gt; p.Flag)</c>。
    /// </summary>
    long Count(Func<TElement, bool> predicate);

    /// <summary><c>COUNT(x)</c>：<paramref name="selector"/> 的值非空的行数。</summary>
    long Count<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>SUM(x)</c>。</summary>
    TValue Sum<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>MAX(x)</c>。</summary>
    TValue Max<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>MIN(x)</c>。</summary>
    TValue Min<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>AVG(x)</c>。</summary>
    TValue Average<TValue>(Func<TElement, TValue> selector);

    /// <summary>
    /// 组内的列本身，不聚合（对应 Java 的 groupTable()）：常用于按组内某列排序，<c>.OrderBy(g => g.Column(e => e.Item2.Order))</c>。
    /// 只有数据库允许非聚合列时可用（如 MySQL 关闭 ONLY_FULL_GROUP_BY，或该列函数依赖于分组键）。
    /// </summary>
    TValue Column<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>GROUPING(键)</c>：配合 <c>Rollup()</c> / <c>Cube()</c>，这一行里该键被汇总掉（小计、总计行）时为 1，否则为 0。</summary>
    int Grouping<TValue>(TValue key);

    /// <summary>字符串聚合：MySQL 的 GROUP_CONCAT、PG / SQL Server 的 STRING_AGG、Oracle 的 LISTAGG（对应 Java 的 joining）。</summary>
    string StringJoin<TValue>(Func<TElement, TValue> selector, string separator);
}
