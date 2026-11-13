using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

// 分组合并下的集合聚合：链上的条件、值登记到 GroupJoinPlan，取一段行的窗口
internal sealed partial class ExpressionTranslator
{
    /// <summary>
    /// 分组合并下的集合聚合：链上的 Where 合成这个聚合的条件，Select 是值，Distinct 去重；
    /// <c>Any</c> / <c>!Any</c>（<paramref name="negated"/>）/ <c>All</c> / <c>Count</c> / <c>Sum</c> / <c>Max</c> / <c>Min</c> / <c>Average</c>。
    /// <c>Select(x =&gt; x.Col).Count()</c> 计的是非 NULL 的值（SQL 的 COUNT(列)，对应 Java 的 count(x -&gt; x.col)）。
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private SqlExpr TranslateGroupJoin(GroupJoinPlan plan, List<MethodCallExpression> chain, bool negated, SqlExpr? separator = null)
    {
        var t = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = plan.InnerContext };
        var element = plan.Element;
        var filters = new List<GroupFilter>();
        // 写了条件但被值过滤整个去掉时，计数仍计 1（与 Java 一致：COUNT(?) 而不是 COUNT(*)）
        var conditional = false;
        LambdaExpression? selector = null;
        var distinct = false;
        // 组内排序只对字符串聚合有意义（GROUP_CONCAT(x ORDER BY …)），其余聚合忽略
        var orders = new List<(LambdaExpression Key, bool Descending)>();
        // string.Join 时链上全是中间操作，没有终结方法
        foreach (var op in separator is null ? chain.Take(chain.Count - 1) : chain)
        {
            switch (op.Method.Name)
            {
                case nameof(Enumerable.Where):
                    AddFilter(Lambda(op.Arguments[1]), negate: false);
                    break;
                case nameof(Enumerable.Select):
                    selector = Lambda(op.Arguments[1]);
                    break;
                case nameof(Enumerable.Distinct):
                    distinct = true;
                    break;
                case nameof(Enumerable.OrderBy) or nameof(Enumerable.ThenBy) or nameof(Enumerable.OrderByDescending) or nameof(Enumerable.ThenByDescending):
                    orders.Add((Lambda(op.Arguments[1]), op.Method.Name.EndsWith("Descending", System.StringComparison.Ordinal)));
                    break;
                default:
                    throw Unsupported(op, "分组合并的集合导航上不支持这个方法");
            }
        }

        var terminal = chain[^1];
        var argument = separator is null && terminal.Arguments.Count > 1 ? Lambda(terminal.Arguments[1]) : null;
        GroupAggregate kind;
        LambdaExpression? valueLambda = null;
        switch (separator is null ? terminal.Method.Name : "StringJoin")
        {
            case "StringJoin":
                kind = GroupAggregate.Joining;
                valueLambda = selector ?? throw Unsupported(terminal, "string.Join 需要先 Select 出字符串，如 string.Join(\",\", u.Cards.Select(c => c.Code))");
                break;
            case nameof(Enumerable.Any):
                if (argument is not null) AddFilter(argument, negate: false);
                kind = negated ? GroupAggregate.None : GroupAggregate.Any;
                break;
            case nameof(Enumerable.All):
                // All(p) 等于"没有不满足 p 的"
                AddFilter(argument ?? throw Unsupported(terminal), negate: true);
                kind = negated ? GroupAggregate.Any : GroupAggregate.None;
                break;
            case nameof(Enumerable.Count) or nameof(Enumerable.LongCount):
                if (argument is not null) AddFilter(argument, negate: false);
                kind = GroupAggregate.Count;
                valueLambda = selector;
                break;
            case nameof(Enumerable.Sum) or nameof(Enumerable.Max) or nameof(Enumerable.Min) or nameof(Enumerable.Average):
                kind = terminal.Method.Name switch
                {
                    nameof(Enumerable.Sum) => GroupAggregate.Sum,
                    nameof(Enumerable.Max) => GroupAggregate.Max,
                    nameof(Enumerable.Min) => GroupAggregate.Min,
                    _ => GroupAggregate.Avg,
                };
                valueLambda = argument ?? selector ?? throw Unsupported(terminal, "聚合需要选择器，如 Sum(x => x.Price)");
                break;
            default:
                throw Unsupported(terminal, "分组合并的集合导航上不支持这个方法");
        }
        if (negated && kind is not (GroupAggregate.None or GroupAggregate.Any)) throw Unsupported(terminal, "只有 Any / All 可以取反");

        SqlExpr? value = null;
        if (valueLambda is not null)
        {
            t.Bind(valueLambda, [element]);
            value = t.Translate(StripConvert(valueLambda.Body));
        }
        var request = new GroupAggregateRequest(kind, value, filters, distinct, valueLambda is null ? "" : GroupJoinKey(valueLambda))
        {
            Separator = separator,
            Conditional = conditional,
        };
        if (kind == GroupAggregate.Joining && orders.Count > 0)
        {
            request = request with
            {
                Orders = orders.Select(o =>
                {
                    t.Bind(o.Key, [element]);
                    return t.OrderItem(o.Key.Body, o.Descending);
                }).ToList(),
                OrderKey = string.Join(",", orders.Select(o => (o.Descending ? "-" : "+") + GroupJoinKey(o.Key))),
            };
        }
        return plan.Reference(request);

        void AddFilter(LambdaExpression original, bool negate)
        {
            conditional = true;
            if (NavigationPredicate(original, plan.ValueFilter) is not { } predicate) return;
            if (negate)
            {
                filters.Add(new GroupFilter(NotSatisfying(t.ElementPredicate(predicate, element)), "!" + GroupJoinKey(predicate)));
                return;
            }
            // 按 AND 拆成合取项：几个聚合共有的合取项可以提到派生表的 WHERE
            foreach (var conjunct in Conjuncts(predicate.Body))
            {
                var part = Expression.Lambda(conjunct, predicate.Parameters);
                filters.Add(new GroupFilter(t.ElementPredicate(part, element), GroupJoinKey(part)));
            }
        }
    }

    private static IEnumerable<Expression> Conjuncts(Expression body) =>
        body is BinaryExpression { NodeType: ExpressionType.AndAlso } and ? Conjuncts(and.Left).Concat(Conjuncts(and.Right)) : [body];

    /// <summary>
    /// 分组合并时链上的"取一段行"：最后一个 Skip / Take 及其之前的 Where（取行前的条件）、OrderBy / ThenBy（组内排序）。
    /// 没有 Skip / Take 时返回 null，<paramref name="rest"/> 是整条链；否则 <paramref name="rest"/> 是取行之后的部分。
    /// </summary>
    private static GroupJoinWindow? WindowOf(List<MethodCallExpression> chain, out List<MethodCallExpression> rest)
    {
        var last = chain.FindLastIndex(op => op.Method.Name is nameof(Enumerable.Skip) or nameof(Enumerable.Take));
        rest = last < 0 ? chain : chain.Skip(last + 1).ToList();
        if (last < 0) return null;
        var filters = new List<LambdaExpression>();
        var orders = new List<(LambdaExpression, bool)>();
        long skip = 0;
        long? take = null;
        var key = new List<string>();
        foreach (var op in chain.Take(last + 1))
        {
            switch (op.Method.Name)
            {
                case nameof(Enumerable.Where):
                    filters.Add(Lambda(op.Arguments[1]));
                    break;
                case nameof(Enumerable.OrderBy) or nameof(Enumerable.ThenBy) or nameof(Enumerable.OrderByDescending) or nameof(Enumerable.ThenByDescending):
                    orders.Add((Lambda(op.Arguments[1]), op.Method.Name.EndsWith("Descending", System.StringComparison.Ordinal)));
                    break;
                case nameof(Enumerable.Skip):
                    skip = Convert.ToInt64(ClientEvaluator.Evaluate(op.Arguments[1]), System.Globalization.CultureInfo.InvariantCulture);
                    key.Add("skip:" + skip);
                    continue;
                case nameof(Enumerable.Take):
                    take = Convert.ToInt64(ClientEvaluator.Evaluate(op.Arguments[1]), System.Globalization.CultureInfo.InvariantCulture);
                    key.Add("take:" + take);
                    continue;
                default:
                    throw Unsupported(op, "取一段行之前只支持 Where / OrderBy / ThenBy");
            }
            key.Add(op.Method.Name + ":" + GroupJoinKey(Lambda(op.Arguments[1])));
        }
        return new GroupJoinWindow(filters, orders, skip, take, string.Join("|", key));
    }

    // 聚合去重用的结构键：lambda 的文本，参数按位置改名（x => x.A 与 y => y.A 视为相同）
    private static string GroupJoinKey(LambdaExpression lambda)
    {
        var body = lambda.Body;
        for (var i = 0; i < lambda.Parameters.Count; i++)
            body = new ParameterReplacer(lambda.Parameters[i], Expression.Parameter(lambda.Parameters[i].Type, "p" + i)).Visit(body);
        return body.ToString();
    }
}
