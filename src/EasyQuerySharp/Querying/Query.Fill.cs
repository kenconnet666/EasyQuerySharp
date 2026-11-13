using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace EasyQuerySharp.Querying;

// 填充（对应 Java 的 fillMany / fillOne）：结果读出后，用一条按键过滤的查询取回关联数据，交给回调挂到每一行上。
// 与 Include 不同，关联不需要声明成导航，任意查询都行（包括分组后的 DTO）。
public sealed partial class Query<T>
{
    /// <summary>
    /// 一对多填充：<paramref name="source"/> 的结果按 <paramref name="childKey"/> 与每行的 <paramref name="parentKey"/> 匹配，
    /// 整组交给 <paramref name="assign"/>。<paramref name="assignEmpty"/> 为 false 时没有匹配的行不调用回调（对应 Java 的 consumeNull）。
    /// 来源带了 Take / Limit 时按每个键各取前 n 条（每个键一段，UNION ALL 合并）。
    /// <code>db.Query&lt;Province&gt;().FillMany(() =&gt; db.Query&lt;City&gt;(), c =&gt; c.ProvinceCode, p =&gt; p.Code, (p, cities) =&gt; p.Cities = cities)</code>
    /// </summary>
    public Query<T> FillMany<TChild, TKey>(Func<Query<TChild>> source, Expression<Func<TChild, TKey>> childKey, Func<T, TKey> parentKey,
        Action<T, List<TChild>> assign, bool assignEmpty = true) =>
        AddFill(source, childKey, parentKey, (row, children) =>
        {
            if (children.Count > 0 || assignEmpty) assign(row, children);
        });

    /// <summary>
    /// 一对一填充：匹配到的第一行交给 <paramref name="assign"/>；<paramref name="assignNull"/> 为 false 时没有匹配的行不调用回调。
    /// <code>db.Query&lt;City&gt;().FillOne(() =&gt; db.Query&lt;Province&gt;(), p =&gt; p.Code, c =&gt; c.ProvinceCode, (c, p) =&gt; c.Province = p)</code>
    /// </summary>
    public Query<T> FillOne<TChild, TKey>(Func<Query<TChild>> source, Expression<Func<TChild, TKey>> childKey, Func<T, TKey> parentKey,
        Action<T, TChild?> assign, bool assignNull = true) where TChild : class =>
        AddFill(source, childKey, parentKey, (row, children) =>
        {
            if (children.Count > 0 || assignNull) assign(row, children.Count > 0 ? children[0] : null);
        });

    private Query<T> AddFill<TChild, TKey>(Func<Query<TChild>> source, Expression<Func<TChild, TKey>> childKey, Func<T, TKey> parentKey,
        Action<T, List<TChild>> assign)
    {
        ArgumentNullException.ThrowIfNull(source);
        var childKeyOf = childKey.Compile();
        async Task Fill(IList rows, CancellationToken ct)
        {
            var parents = rows.Cast<T>().Where(r => r is not null).ToList();
            var keys = parents.Select(parentKey).Where(k => k is not null).Distinct().ToList();
            var children = keys.Count == 0 ? [] : await LoadChildren(source(), childKey, keys).ToListAsync(ct).ConfigureAwait(false);
            var byKey = children.Where(c => childKeyOf(c) is not null).ToLookup(childKeyOf);
            foreach (var parent in parents)
                assign(parent, parentKey(parent) is { } key ? [.. byKey[key]] : []);
        }
        return new(State with { Fills = State.Fills.Add(Fill) });
    }

    // 按键过滤关联查询：条件加在来源查询内部（分页之前）；来源有行数限制时每个键单独一段再 UNION ALL
    private static Query<TChild> LoadChildren<TChild, TKey>(Query<TChild> source, Expression<Func<TChild, TKey>> childKey, List<TKey> keys)
    {
        var state = source.State;
        if (state.Limit is null && state.Offset is null) return new Query<TChild>(state with { Wheres = state.Wheres.Add(KeyIn(childKey, keys)) });
        var parts = keys.Select(k => state with { Wheres = state.Wheres.Add(KeyEquals(childKey, k)) }).ToImmutableArray();
        return parts.Length == 1
            ? new Query<TChild>(parts[0])
            : new Query<TChild>(new QueryState(state.Db, [new UnionSource(parts, All: true) { IndependentAliases = true }]));
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression KeyIn<TChild, TKey>(Expression<Func<TChild, TKey>> childKey, List<TKey> keys) =>
        Expression.Lambda(Expression.Call(Expression.Constant(keys), typeof(List<TKey>).GetMethod(nameof(List<TKey>.Contains))!, childKey.Body), childKey.Parameters);

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression KeyEquals<TChild, TKey>(Expression<Func<TChild, TKey>> childKey, TKey key) =>
        Expression.Lambda(Expression.Equal(childKey.Body, Expression.Constant(key, typeof(TKey))), childKey.Parameters);
}
