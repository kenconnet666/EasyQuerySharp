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

    /// <summary><c>COUNT(x)</c>。</summary>
    long Count<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>SUM(x)</c>。</summary>
    TValue Sum<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>MAX(x)</c>。</summary>
    TValue Max<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>MIN(x)</c>。</summary>
    TValue Min<TValue>(Func<TElement, TValue> selector);

    /// <summary><c>AVG(x)</c>。</summary>
    TValue Average<TValue>(Func<TElement, TValue> selector);

    /// <summary>字符串聚合：MySQL 的 GROUP_CONCAT、PG / SQL Server 的 STRING_AGG、Oracle 的 LISTAGG（对应 Java 的 joining）。</summary>
    string StringJoin<TValue>(Func<TElement, TValue> selector, string separator);
}
