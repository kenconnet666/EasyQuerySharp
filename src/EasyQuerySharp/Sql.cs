using System.Diagnostics.CodeAnalysis;

namespace EasyQuerySharp;

/// <summary>
/// 只能写在查询表达式里的 SQL 标记方法：翻译器认得它们，运行时直接调用会抛出 <see cref="InvalidOperationException"/>。
/// 列上的函数做成扩展方法（扩展属性写不进表达式树，见 0001）。
/// </summary>
[SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "参数只在表达式树里被翻译")]
public static class Sql
{
    /// <summary>
    /// 原样的 LIKE：<paramref name="pattern"/> 里的 % 与 _ 由调用方自己写。
    /// 一般写法用 <see cref="string.Contains(string)"/>（<c>%v%</c>）、<see cref="string.StartsWith(string)"/>（<c>v%</c>）、<see cref="string.EndsWith(string)"/>（<c>%v</c>）。
    /// </summary>
    public static bool Like(this string? value, string pattern) => throw OnlyInQuery();

    /// <summary>原样的 NOT LIKE。</summary>
    public static bool NotLike(this string? value, string pattern) => throw OnlyInQuery();

    /// <summary>
    /// 原生 SQL 片段，<c>{0}</c>、<c>{1}</c>… 依次替换为参数（列或值，值会参数化）；对应 Java 的 sqlSegment。
    /// </summary>
    /// <example><code>.Select(b => Sql.Raw&lt;long&gt;("rank() over(order by {0} desc)", b.Stars))</code></example>
    public static T Raw<T>(string template, params object?[] arguments) => throw OnlyInQuery();

    /// <summary>值在给定集合里（<c>IN (…)</c>）；与 <c>ids.Contains(x)</c> 等价，写法更接近 SQL。</summary>
    public static bool In<T>(this T value, params T[] values) => throw OnlyInQuery();

    /// <summary>值不在给定集合里（<c>NOT IN (…)</c>）。</summary>
    public static bool NotIn<T>(this T value, params T[] values) => throw OnlyInQuery();

    internal static InvalidOperationException OnlyInQuery() =>
        new("这个方法只能写在 EasyQuerySharp 的查询表达式里，由翻译器转成 SQL，不能直接调用");
}
