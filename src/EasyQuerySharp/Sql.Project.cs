using System.Linq.Expressions;

namespace EasyQuerySharp;

// 可复用投影的标记方法（见 0002）：投影写成表达式字段，查询里按对一 / 对多套用，内联成同一棵投影表达式
public static partial class Sql
{
    /// <summary>
    /// 对一导航（或任意值）套用投影 <paramref name="map"/>：<c>Select(u =&gt; new { Company = u.Company!.Project(CompanyVo.Map) })</c>。
    /// 等同于把 <paramref name="map"/> 的方法体写在这里。
    /// </summary>
    public static TResult Project<TSource, TResult>(this TSource source, Expression<Func<TSource, TResult>> map) => throw OnlyInQuery();

    /// <summary>
    /// 集合导航的每个元素套用投影 <paramref name="map"/>：<c>u.Posts!.Project(PostVo.Map).ToList()</c>，等同于 <c>Select(map)</c>。
    /// </summary>
    public static IEnumerable<TResult> Project<TSource, TResult>(this IEnumerable<TSource> source, Expression<Func<TSource, TResult>> map) =>
        throw OnlyInQuery();
}
