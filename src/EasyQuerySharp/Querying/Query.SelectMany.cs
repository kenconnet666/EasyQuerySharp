using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

public sealed partial class Query<T>
{
    /// <summary>
    /// 沿集合导航展开（LINQ 的 SelectMany）：<c>db.Query&lt;A&gt;().SelectMany(a =&gt; a.Bs)</c> 是 <c>A INNER JOIN B</c> 后取 B 的行，
    /// 多对多经中间表连接，导航上的固定条件与逻辑删除写进 ON。可以连着写，之后照常 Where / Select / Distinct / 分页。
    /// 与 LINQ 一样保留重复（一个 B 属于多个 A 时出现多次），要去重加 <c>Distinct()</c>。
    /// 对应 Java 的 <c>toList(a -&gt; a.bs().flatElement()…)</c>（Java 用带出查询逐层取，再在内存里去重）。
    /// </summary>
    /// <example><code>db.Query&lt;User&gt;().Where(u =&gt; u.Name == "a").SelectMany(u =&gt; u.Roles).SelectMany(r =&gt; r.Menus).Select(m =&gt; m.Route).Distinct()</code></example>
    public Query<TResult> SelectMany<TResult>(Expression<Func<T, IEnumerable<TResult>?>> navigation) where TResult : class =>
        SelectMany(navigation, (Expression<Func<T, TResult, TResult>>)((_, element) => element));

    /// <summary>
    /// 沿集合导航展开后投影（LINQ 查询语法 <c>from a in q from b in a.Bs select …</c> 编译成的形式）：
    /// <paramref name="resultSelector"/> 同时拿到父行与元素，其余同 <see cref="SelectMany{TResult}(Expression{Func{T, IEnumerable{TResult}}})"/>。
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    public Query<TResult> SelectMany<TElement, TResult>(Expression<Func<T, IEnumerable<TElement>?>> navigation,
        Expression<Func<T, TElement, TResult>> resultSelector) where TElement : class
    {
        ArgumentNullException.ThrowIfNull(resultSelector);
        var nav = CollectionNavigation(navigation);
        // 接在上一个 SelectMany 后面时直接从它取出的表继续连接；否则从唯一的来源连接。
        // 父查询投影过、分页、去重或分组过时先包成派生表：LINQ 的语义是先取出这些父行再展开
        var picked = PickedElement;
        var state = picked is not null ? State
            : State.HasSelector || State.Offset is not null || State.Limit is not null || State.Distinct || State.GroupKey is not null ? State.Wrap()
            : State;
        var owner = picked ?? Expression.Parameter(typeof(T), "a");
        List<ParameterExpression> parameters = picked is null ? [owner] : [.. State.Selector!.Parameters];
        var element = Expression.Parameter(nav.Target.Type, "b" + parameters.Count);
        if (nav.Mapping is { } mapping)
        {
            var link = Expression.Parameter(mapping.Type, "m" + parameters.Count);
            parameters.Add(link);
            state = QueryStates.AddJoin(state, new EntitySource(mapping.Type) { Join = JoinKind.Inner,
                On = Expression.Lambda(KeysEqual(link, nav.SelfMappingColumns, owner, nav.SelfColumns, nav.ExtraMappingFilter), parameters) });
            parameters.Add(element);
            state = QueryStates.AddJoin(state, new EntitySource(nav.Target.Type) { Join = JoinKind.Inner,
                On = Expression.Lambda(KeysEqual(element, nav.TargetColumns, link, nav.TargetMappingColumns, nav.ExtraFilter), parameters) });
        }
        else
        {
            parameters.Add(element);
            state = QueryStates.AddJoin(state, new EntitySource(nav.Target.Type) { Join = JoinKind.Inner,
                On = Expression.Lambda(KeysEqual(element, nav.TargetColumns, owner, nav.SelfColumns, nav.ExtraFilter, nav.KeyMatch), parameters) });
        }
        var body = new ExpressionTranslator.ParameterReplacer(resultSelector.Parameters[0], owner).Visit(resultSelector.Body);
        body = new ExpressionTranslator.ParameterReplacer(resultSelector.Parameters[1], element).Visit(body);
        return new(state with { Selector = Expression.Lambda(body, parameters) });
    }

    // SelectMany 之后、还没有分页、去重、分组时，查询的元素就是某个连接表的整行（投影只是取出这个参数）：
    // 之后的条件、排序、投影、SelectMany 直接写在这个表上，不必把整个连接包成派生表
    private ParameterExpression? PickedElement =>
        State is { Selector: { Body: ParameterExpression picked }, GroupKey: null, Distinct: false, Offset: null, Limit: null, RawSelect: null }
        && State.Sources.Length > 1 && State.Includes.IsEmpty && State.Fills.IsEmpty
            ? picked
            : null;

    // 只有元素一个参数的 lambda，改写成连接上全部来源的参数（元素换成取出的那张表）
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private LambdaExpression OverJoinedSources(LambdaExpression lambda, ParameterExpression picked) =>
        Expression.Lambda(new ExpressionTranslator.ParameterReplacer(lambda.Parameters[0], picked).Visit(lambda.Body), State.Selector!.Parameters);

    private NavigationMetadata CollectionNavigation(LambdaExpression navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        if (ExpressionTranslator.StripConvert(navigation.Body) is not MemberExpression { Expression: ParameterExpression, Member: var member })
            throw new ArgumentException($"SelectMany 需要直接的集合导航属性，如 a => a.Bs：{navigation}", nameof(navigation));
        var nav = Db.Metadata.FindNavigation(Db.Metadata.Get<T>(), member.Name)
                  ?? throw new ArgumentException($"{typeof(T).Name}.{member.Name} 不是导航属性", nameof(navigation));
        if (!nav.IsCollection) throw new ArgumentException($"{nav} 不是集合导航：对一导航直接写 Select(a => a.{member.Name})", nameof(navigation));
        if (nav.DirectPath.Count > 0) throw new NotSupportedException($"SelectMany 不支持直达导航 {nav}：沿路径上的每一段导航分别 SelectMany");
        return nav;
    }

    // 每对关联列相等（两边类型不同时都转成可空的同一类型），再加上导航的固定条件（参数换成被连接的表）；
    // 键是逗号列表时写成 Sql.InCsv(目标键, 本表列)
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    private static Expression KeysEqual(ParameterExpression joined, IReadOnlyList<ColumnMetadata> joinedColumns,
        ParameterExpression other, IReadOnlyList<ColumnMetadata> otherColumns, LambdaExpression? extraFilter, KeyMatch match = KeyMatch.Equal)
    {
        Expression? condition = null;
        if (match == KeyMatch.SelfCsv)
            condition = Expression.Call(typeof(Sql).GetMethod(nameof(Sql.InCsv))!.MakeGenericMethod(joinedColumns[0].Property.PropertyType),
                Expression.Property(joined, joinedColumns[0].Property), Expression.Property(other, otherColumns[0].Property));
        else
            for (var i = 0; i < joinedColumns.Count; i++)
            {
                Expression left = Expression.Property(joined, joinedColumns[i].Property), right = Expression.Property(other, otherColumns[i].Property);
                // 一边是可空类型时两边都转成它（不在运行时构造 Nullable<T>：Native AOT 下值类型的实例化要编译期已有）
                if (left.Type != right.Type)
                {
                    var common = Nullable.GetUnderlyingType(left.Type) is not null ? left.Type : right.Type;
                    left = Expression.Convert(left, common);
                    right = Expression.Convert(right, common);
                }
                var equal = Expression.Equal(left, right);
                condition = condition is null ? equal : Expression.AndAlso(condition, equal);
            }
        if (extraFilter is not null)
            condition = Expression.AndAlso(condition!, new ExpressionTranslator.ParameterReplacer(extraFilter.Parameters[0], joined).Visit(extraFilter.Body));
        return condition!;
    }
}
