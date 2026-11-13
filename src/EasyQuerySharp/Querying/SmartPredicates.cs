using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 条件下推（对应 Java 的 SMART_PREDICATE）：投影之后再加的条件本该写在派生表外层；
/// 开了 <c>SmartPredicate()</c> 时，只用到某一张表直接投影出来的列的那些合取项，改写成对那张表的条件推进内层：
/// 第一张表写进内层 WHERE，左连接（及其他连接）的表写进它的 ON（不改变行数）。有一个合取项用到多张表、或投影值不是直接列时，整个条件留在外层。
/// </summary>
internal static class SmartPredicates
{
    /// <summary>尝试把 <paramref name="predicate"/>（参数是投影结果）的合取项推进 <paramref name="state"/> 唯一的派生表来源；返回留在外层的部分（全推进去时为 null）。</summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    public static (QueryState State, LambdaExpression? Remaining) PushDown(QueryState state, LambdaExpression predicate)
    {
        if (state.Sources is not [SubQuerySource { Inner: { Selector: { } selector } inner } sub]
            || inner.GroupKey is not null || inner.Offset is not null || inner.Limit is not null || inner.Distinct
            || Mapping(selector) is not { } mapping)
            return (state, predicate);

        // 全部合取项都能推进去才下推，有一项不行就整个留在外层（与 Java 一致：外层条件不拆开）
        var outer = predicate.Parameters[0];
        var pushes = new List<(int Index, Expression Body)>();
        foreach (var conjunct in Conjuncts(predicate.Body))
        {
            var rewriter = new Rewriter(outer, mapping);
            var body = rewriter.Visit(conjunct);
            if (rewriter.Failed || rewriter.Sources.Count != 1) return (state, predicate);
            pushes.Add((selector.Parameters.IndexOf(rewriter.Sources.Single()), body));
        }
        foreach (var (index, body) in pushes)
            inner = index == 0 ? inner with { Wheres = inner.Wheres.Add(Expression.Lambda(body, selector.Parameters)) } : AddToOn(inner, index, body, selector.Parameters);
        return (state with { Sources = [sub with { Inner = inner }] }, null);
    }

    // 第 index 个来源的 ON 后面追加条件（ON 的参数是前 index + 1 个来源）
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static QueryState AddToOn(QueryState inner, int index, Expression body, IReadOnlyList<ParameterExpression> parameters)
    {
        var source = inner.Sources[index];
        var onParameters = source.On?.Parameters ?? (IReadOnlyList<ParameterExpression>)parameters.Take(index + 1).ToList();
        var rebound = body;
        for (var i = 0; i <= index; i++) rebound = new ExpressionTranslator.ParameterReplacer(parameters[i], onParameters[i]).Visit(rebound);
        var on = source.On is null ? rebound : Expression.AndAlso(source.On.Body, rebound);
        return inner with { Sources = inner.Sources.SetItem(index, source with { On = Expression.Lambda(on, onParameters) }) };
    }

    private static IEnumerable<Expression> Conjuncts(Expression body) =>
        body is BinaryExpression { NodeType: ExpressionType.AndAlso } and ? Conjuncts(and.Left).Concat(Conjuncts(and.Right)) : [body];

    // 投影的成员 → 直接取自某个来源参数的成员（只收这种；计算出来的值不下推）
    private static Dictionary<MemberInfo, MemberExpression>? Mapping(LambdaExpression selector)
    {
        var result = new Dictionary<MemberInfo, MemberExpression>();
        void Add(MemberInfo member, Expression value)
        {
            if (ExpressionTranslator.StripConvert(value) is MemberExpression { Expression: ParameterExpression p } column && selector.Parameters.Contains(p))
                result[member] = column;
        }
        switch (selector.Body)
        {
            case MemberInitExpression init:
                foreach (var binding in init.Bindings.OfType<MemberAssignment>()) Add(binding.Member, binding.Expression);
                break;
            case NewExpression { Members: { } members } created:
                for (var i = 0; i < members.Count; i++) Add(members[i], created.Arguments[i]);
                break;
            default:
                return null;
        }
        return result;
    }

    // 把对投影成员的访问换成来源上的列；遇到没法换的成员或直接用到整个投影对象时失败
    private sealed class Rewriter(ParameterExpression outer, Dictionary<MemberInfo, MemberExpression> mapping) : ExpressionVisitor
    {
        public bool Failed { get; private set; }

        public HashSet<ParameterExpression> Sources { get; } = [];

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != outer) return base.VisitMember(node);
            if (!mapping.TryGetValue(node.Member, out var column))
            {
                Failed = true;
                return node;
            }
            Sources.Add((ParameterExpression)column.Expression!);
            return column.Type == node.Type ? column : Expression.Convert(column, node.Type);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == outer) Failed = true;
            return node;
        }
    }
}
