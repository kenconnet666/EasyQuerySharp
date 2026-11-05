using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>当前查询的分组绑定；不分组时为 null。参数类型是 <see cref="IGroup{TKey,TElement}"/> 的 lambda 绑定到它。</summary>
    public GroupBinding? Group { get; set; }

    internal static bool IsGroupType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGroup<,>)
        || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IGroup<,>));

    /// <summary><c>g.Key</c> 与 <c>g.Key.X</c>；不是分组键访问时返回 null。</summary>
    private SqlExpr? TryTranslateGroupKey(MemberExpression m)
    {
        if (m.Member.Name == nameof(IGroup<object, object>.Key) && m.Expression is not null && BindingOf(m.Expression) is GroupBinding g)
            return g.ScalarKey;
        if (m.Expression is MemberExpression { Member.Name: nameof(IGroup<object, object>.Key) } key && key.Expression is not null
            && BindingOf(key.Expression) is GroupBinding g2)
            return g2.KeyMember(m.Member.Name);
        return null;
    }

    /// <summary>
    /// 分组上的聚合：先沿调用链收集 <c>Where</c>（过滤）与 <c>Distinct</c>，再按最终的聚合方法生成函数。
    /// 过滤条件让值包进 <c>CASE WHEN … THEN 值 ELSE NULL END</c>（Java 版的 group.where / filter）。
    /// </summary>
    private SqlExpr TranslateGroupAggregate(MethodCallExpression call)
    {
        var filters = new List<LambdaExpression>();
        var distinct = false;
        Expression source = call.Object!;
        while (source is MethodCallExpression inner && IsGroupType(inner.Method.DeclaringType!))
        {
            if (inner.Method.Name == nameof(IGroup<object, object>.Where)) filters.Add((LambdaExpression)StripConvert(inner.Arguments[0]));
            else if (inner.Method.Name == nameof(IGroup<object, object>.Distinct)) distinct = true;
            else throw Unsupported(inner);
            source = inner.Object!;
        }
        if (BindingOf(source) is not GroupBinding group) throw Unsupported(call, "聚合只能作用在 GroupBy 的分组参数上");

        SqlExpr? filter = null;
        if (filters.Count > 0)
            filter = LogicalExpr.And(filters.AsEnumerable().Reverse().Select(f => ElementPredicate(f, group.Element)));

        SqlExpr Value(int index, SqlExpr otherwise)
        {
            var lambda = (LambdaExpression)StripConvert(call.Arguments[index]);
            _scope[lambda.Parameters[0]] = group.Element;
            var value = Translate(lambda.Body);
            return filter is null ? value : new CaseExpr([(filter, value)], otherwise);
        }

        switch (call.Method.Name)
        {
            // 无参计数：COUNT(*)；有条件时计 CASE WHEN 条件 THEN 1；去重时与 Java 一致写成 COUNT(DISTINCT (CASE …)) / COUNT(DISTINCT ?)
            case nameof(IGroup<object, object>.Count) when call.Arguments.Count == 0:
            {
                var one = new ParameterExpr(1, typeof(int));
                if (filter is null && !distinct) return new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]);
                SqlExpr counted = filter is null ? one : new CaseExpr([(filter, one)], LiteralExpr.Null);
                return new FunctionExpr(distinct ? SqlFunctionNames.CountDistinct : SqlFunctionNames.Count, [counted]);
            }
            // Count(predicate)：满足条件的行数，条件与组上的 Where 一起写进 CASE
            case nameof(IGroup<object, object>.Count) when !call.Method.IsGenericMethod:
            {
                var condition = ElementPredicate((LambdaExpression)StripConvert(call.Arguments[0]), group.Element);
                if (filter is not null) condition = LogicalExpr.And([filter, condition]);
                return new FunctionExpr(distinct ? SqlFunctionNames.CountDistinct : SqlFunctionNames.Count,
                    [new CaseExpr([(condition, new ParameterExpr(1, typeof(int)))], LiteralExpr.Null)]);
            }
            case nameof(IGroup<object, object>.Count):
                return new FunctionExpr(distinct ? SqlFunctionNames.CountDistinct : SqlFunctionNames.Count, [Value(0, LiteralExpr.Null)]);
            case nameof(IGroup<object, object>.Sum):
                return new FunctionExpr(distinct ? SqlFunctionNames.SumDistinct : SqlFunctionNames.Sum, [Value(0, new ParameterExpr(0L, typeof(long)))]);
            case nameof(IGroup<object, object>.Max):
                return new FunctionExpr(SqlFunctionNames.Max, [Value(0, LiteralExpr.Null)]);
            case nameof(IGroup<object, object>.Min):
                return new FunctionExpr(SqlFunctionNames.Min, [Value(0, LiteralExpr.Null)]);
            case nameof(IGroup<object, object>.Average):
                return new FunctionExpr(distinct ? SqlFunctionNames.AvgDistinct : SqlFunctionNames.Avg, [Value(0, LiteralExpr.Null)]);
            case nameof(IGroup<object, object>.Column):
                // 组内的列本身，不聚合（Java 的 groupTable()）
                return Value(0, LiteralExpr.Null);
            case nameof(IGroup<object, object>.StringJoin):
                return new FunctionExpr(distinct ? SqlFunctionNames.StringJoinDistinct : SqlFunctionNames.StringJoin,
                    [Value(0, LiteralExpr.Null), Translate(call.Arguments[1])]);
            default:
                throw Unsupported(call);
        }
    }

}
