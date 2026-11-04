using System.Linq.Expressions;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

// 集合导航聚合的分组合并（对应 Java 的 subQueryToGroupJoin / subQueryConfigure）。
public sealed partial class Query<T>
{
    /// <summary>
    /// 把这个集合导航上的聚合（Count / Sum / Max / Min / Average / Any / string.Join …）合并成一个分组 LEFT JOIN，
    /// 代替每个聚合一个相关子查询：<c>LEFT JOIN (SELECT uid AS __group_key1__, COUNT(*) AS __count2__, … GROUP BY uid) t2 ON t2.__group_key1__ = t.id</c>。
    /// 导航可以经过对一导航（<c>c =&gt; c.User!.Books</c>）。<paramref name="configure"/> 给分组子查询加条件（只取 Where），对这个导航上的全部聚合生效。
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
