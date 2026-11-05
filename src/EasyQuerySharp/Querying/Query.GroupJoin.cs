using System.Linq.Expressions;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

// 集合导航聚合的分组合并（对应 Java 的 subQueryToGroupJoin / subQueryConfigure）。
public sealed partial class Query<T>
{
    /// <summary>
    /// 把这个集合导航上的聚合（Count / Sum / Max / Min / Average / Any / string.Join …）合并成一个分组 LEFT JOIN，
    /// 代替每个聚合一个相关子查询：<c>LEFT JOIN (SELECT uid AS __group_key1__, COUNT(*) AS __count2__, … GROUP BY uid) t2 ON t2.__group_key1__ = t.id</c>。
    /// 导航可以经过对一导航（<c>c =&gt; c.User!.Books</c>）。<paramref name="configure"/> 给分组子查询加条件（Where），对这个导航上的全部聚合生效；聚合前先取一段行（<c>Skip</c> / <c>Take</c>）而链上没写排序时，按这里的 OrderBy 编组内行号。
    /// <code>db.Query&lt;DocUser&gt;().GroupJoin(u =&gt; u.BankCards).Where(u =&gt; u.BankCards!.Count(c =&gt; c.Type == "储蓄") &gt; 1)</code>
    /// </summary>
    public Query<T> GroupJoin<TChild>(Expression<Func<T, IEnumerable<TChild>?>> navigation, Func<Query<TChild>, Query<TChild>>? configure = null) =>
        new(GroupJoinSpecs.Add(State, navigation, true, configure));

    /// <summary>
    /// <paramref name="condition"/> 为 true 时合并；为 false 时显式按相关子查询翻译（导航上标了 <c>GroupJoin = true</c> 也一样）。
    /// </summary>
    public Query<T> GroupJoinIf<TChild>(bool condition, Expression<Func<T, IEnumerable<TChild>?>> navigation, Func<Query<TChild>, Query<TChild>>? configure = null) =>
        new(GroupJoinSpecs.Add(State, navigation, condition, configure));
}

/// <summary>各查询类型共用的 GroupJoin 声明：导航按（所属实体类型, 属性名）登记。</summary>
internal static class GroupJoinSpecs
{
    public static QueryState Add<TChild>(QueryState state, LambdaExpression navigation, bool enabled, Func<Query<TChild>, Query<TChild>>? configure)
    {
        if (ExpressionTranslator.StripConvert(navigation.Body) is not MemberExpression { Expression: { } owner } member)
            throw new ArgumentException($"GroupJoin 需要集合导航属性，如 u => u.Posts：{navigation}", nameof(navigation));
        Func<QueryState, QueryState>? apply = configure is null ? null : s => configure(new Query<TChild>(s)).State;
        return state with { GroupJoins = state.GroupJoins.Add(new GroupJoinSpec(owner.Type, member.Member.Name, enabled, apply)) };
    }
}

// 导航级的固定条件（对应 Java 的 nav().filter(..)）
public sealed partial class Query<T>
{
    /// <summary>
    /// 给对一导航加固定条件：这次查询里这个导航的 JOIN 都带上它（写在 ON 的键条件之后），没在别处用到也照样连接（与 Java 的 nav().filter(..) 一致）。
    /// <code>db.Query&lt;BankCard&gt;().FilterNavigation(c =&gt; c.Bank, b =&gt; b.Name!.Contains("工商"))
    /// // … INNER JOIN t_bank t1 ON t1.id = t.bank_id AND t1.name LIKE ?</code>
    /// </summary>
    public Query<T> FilterNavigation<TTarget>(Expression<Func<T, TTarget?>> navigation, Expression<Func<TTarget, bool>> predicate) where TTarget : class =>
        new(NavigationFilterSpecs.Add(State, navigation, predicate));

    /// <summary>给集合导航加固定条件：它的隐式子查询（Any / Count …）与分组合并都带上它。</summary>
    public Query<T> FilterNavigation<TChild>(Expression<Func<T, IEnumerable<TChild>?>> navigation, Expression<Func<TChild, bool>> predicate) =>
        new(NavigationFilterSpecs.Add(State, navigation, predicate));
}

internal static class NavigationFilterSpecs
{
    public static QueryState Add(QueryState state, LambdaExpression navigation, LambdaExpression predicate)
    {
        if (ExpressionTranslator.StripConvert(navigation.Body) is not MemberExpression { Expression: ParameterExpression owner } member)
            throw new ArgumentException($"FilterNavigation 需要直接的导航属性，如 c => c.Bank：{navigation}", nameof(navigation));
        return state with { NavigationFilters = state.NavigationFilters.Add(new NavigationFilterSpec(owner.Type, member.Member.Name, predicate)) };
    }
}
