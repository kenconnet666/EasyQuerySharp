using System.Linq.Expressions;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 分组之后、投影之前的查询：可以加 HAVING、按组排序，最后 <see cref="Select{TResult}"/> 投影出每组一行。
/// lambda 的参数是 <see cref="IGroup{TKey,TElement}"/>：<c>g.Key</c> 是分组键，<c>g.Count()</c> / <c>g.Sum(e => e.X)</c> 是聚合。
/// </summary>
public sealed class GroupQuery<TKey, TElement>
{
    internal GroupQuery(QueryState state) => State = state;

    internal QueryState State { get; }

    /// <summary>HAVING 条件（多个之间是 AND）。</summary>
    public GroupQuery<TKey, TElement> Having(Expression<Func<IGroup<TKey, TElement>, bool>> predicate) =>
        new(State with { Havings = State.Havings.Add(predicate) });

    /// <summary><paramref name="condition"/> 为 true 时才加 HAVING 条件。</summary>
    public GroupQuery<TKey, TElement> HavingIf(bool condition, Expression<Func<IGroup<TKey, TElement>, bool>> predicate) =>
        condition ? Having(predicate) : this;

    /// <summary>按组追加升序排序（通常按键或聚合值）。</summary>
    public GroupQuery<TKey, TElement> OrderBy<TOrder>(Expression<Func<IGroup<TKey, TElement>, TOrder>> key) =>
        new(State with { OrderBys = State.OrderBys.Add(new OrderSpec(key, false)) });

    /// <summary>按组追加降序排序。</summary>
    public GroupQuery<TKey, TElement> OrderByDescending<TOrder>(Expression<Func<IGroup<TKey, TElement>, TOrder>> key) =>
        new(State with { OrderBys = State.OrderBys.Add(new OrderSpec(key, true)) });

    /// <summary>只取每组的键（对应 Java 的 groupBy(…) 之后直接 toList）：<c>SELECT 键 FROM … GROUP BY 键</c>。</summary>
    public Query<TKey> Keys()
    {
        var group = Expression.Parameter(typeof(IGroup<TKey, TElement>), "g");
        Expression key = Expression.Property(group, nameof(IGroup<TKey, TElement>.Key));
        // 多列键（匿名类型）：逐个成员取 g.Key.X 再构造回去
        if (State.GroupKey?.Body is NewExpression { Members: { } members } composite)
            key = Expression.New(composite.Constructor!, members.Select(m => Expression.MakeMemberAccess(key, m)), members);
        return new(State with { Selector = Expression.Lambda(key, group), ImplicitSelector = true });
    }

    /// <summary>每组投影成一行。分组查询的投影总是写列别名（与 Java 版一致）。</summary>
    public Query<TResult> Select<TResult>(Expression<Func<IGroup<TKey, TElement>, TResult>> selector) =>
        new(State with { Selector = selector });
}
