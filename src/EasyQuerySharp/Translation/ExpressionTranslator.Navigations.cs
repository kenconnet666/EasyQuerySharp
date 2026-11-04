using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>当前 SELECT 的翻译上下文（导航要用）；写入命令等没有上下文时为 null，此时不支持导航。</summary>
    public TranslationContext? Context { get; set; }

    private NavigationMetadata? Navigation(EntityBinding owner, string member) => Context?.Metadata.FindNavigation(owner.Entity, member);

    /// <summary>对一导航 <c>x.User</c>：返回隐式 JOIN 的目标表绑定；不是对一导航时返回 null。</summary>
    private EntityBinding? ToOneNavigation(MemberExpression m)
    {
        if (m.Expression is null || BindingOf(m.Expression) is not EntityBinding owner) return null;
        if (Navigation(owner, m.Member.Name) is not { IsCollection: false } nav) return null;
        return Context!.JoinFor(owner, nav);
    }

    /// <summary>集合导航 <c>x.Cards</c>：返回所属表绑定与导航；不是集合导航时返回 null。</summary>
    private (EntityBinding Owner, NavigationMetadata Navigation)? CollectionNavigation(Expression e)
    {
        if (StripConvert(e) is not MemberExpression { Expression: { } inner } m || BindingOf(inner) is not EntityBinding owner) return null;
        return Navigation(owner, m.Member.Name) is { IsCollection: true } nav ? (owner, nav) : null;
    }

    /// <summary>
    /// 集合导航上的 LINQ 方法 → 隐式子查询：
    /// <c>Any(…)</c> → EXISTS、<c>All(p)</c> → NOT EXISTS(… AND NOT p)、<c>Count(…)</c> / <c>Sum</c> / <c>Max</c> / <c>Min</c> / <c>Average</c> → 标量子查询。
    /// 中间可以链 <c>Where</c>、<c>Select</c>、<c>Distinct</c>。
    /// </summary>
    private bool TryTranslateCollection(MethodCallExpression call, out SqlExpr result)
    {
        result = null!;
        if (call.Method.DeclaringType != typeof(Enumerable) || call.Arguments.Count == 0) return false;

        var chain = new List<MethodCallExpression>();
        Expression current = call;
        while (current is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Arguments.Count > 0)
        {
            chain.Add(mc);
            current = mc.Arguments[0];
        }
        if (CollectionNavigation(current) is not var (owner, nav)) return false;
        chain.Reverse();

        var sub = NewCollectionSubQuery(owner, nav, out var t, out var element);
        LambdaExpression? selector = null;
        var distinct = false;
        foreach (var op in chain.Take(chain.Count - 1))
        {
            switch (op.Method.Name)
            {
                case nameof(Enumerable.Where):
                    sub.Where.Add(t.ElementPredicate(Lambda(op.Arguments[1]), element));
                    break;
                case nameof(Enumerable.Select):
                    selector = Lambda(op.Arguments[1]);
                    break;
                case nameof(Enumerable.Distinct):
                    distinct = true;
                    break;
                default:
                    throw Unsupported(op, "集合导航上不支持这个方法");
            }
        }

        var terminal = chain[^1];
        var predicate = terminal.Arguments.Count > 1 && terminal.Method.Name is nameof(Enumerable.Any) or nameof(Enumerable.All)
            or nameof(Enumerable.Count) or nameof(Enumerable.LongCount)
            ? t.ElementPredicate(Lambda(terminal.Arguments[1]), element)
            : null;
        SqlExpr Value()
        {
            var lambda = terminal.Arguments.Count > 1 ? Lambda(terminal.Arguments[1]) : selector
                         ?? throw Unsupported(terminal, "聚合需要选择器，如 Sum(x => x.Price)");
            t.Bind(lambda, [element]);
            return t.Translate(lambda.Body);
        }

        switch (terminal.Method.Name)
        {
            case nameof(Enumerable.Any):
                if (predicate is not null) sub.Where.Add(predicate);
                sub.Columns.Add(new SelectColumn(LiteralExpr.One, null));
                result = new ExistsExpr(sub, negated: false);
                return true;
            case nameof(Enumerable.All):
                sub.Where.Add(Negate(predicate ?? throw Unsupported(terminal)));
                sub.Columns.Add(new SelectColumn(LiteralExpr.One, null));
                result = new ExistsExpr(sub, negated: true);
                return true;
            case nameof(Enumerable.Count) or nameof(Enumerable.LongCount):
                if (predicate is not null) sub.Where.Add(predicate);
                sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]), null));
                break;
            case nameof(Enumerable.Sum):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(distinct ? SqlFunctionNames.SumDistinct : SqlFunctionNames.Sum, [Value()]), null));
                break;
            case nameof(Enumerable.Max):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Max, [Value()]), null));
                break;
            case nameof(Enumerable.Min):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Min, [Value()]), null));
                break;
            case nameof(Enumerable.Average):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(distinct ? SqlFunctionNames.AvgDistinct : SqlFunctionNames.Avg, [Value()]), null));
                break;
            default:
                throw Unsupported(terminal, "集合导航上不支持这个方法");
        }
        result = new SubQueryExpr(sub);
        return true;
    }

    /// <summary><c>x.Cards.Count</c>（List 的 Count 属性）→ 计数子查询。</summary>
    private SqlExpr? TryTranslateCollectionCount(MemberExpression m)
    {
        if (m.Member.Name != "Count" || m.Expression is null || CollectionNavigation(m.Expression) is not var (owner, nav)) return null;
        var sub = NewCollectionSubQuery(owner, nav, out _, out _);
        sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]), null));
        return new SubQueryExpr(sub);
    }

    /// <summary>
    /// 集合导航的子查询骨架：目标表自带的过滤条件、与所属行的关联条件、导航的固定条件。
    /// 一对多关联写成 <c>目标.外键 = 所属.键</c>；多对多经中间表写成 EXISTS。
    /// </summary>
    private SelectQuery NewCollectionSubQuery(EntityBinding owner, NavigationMetadata nav, out ExpressionTranslator translator, out EntityBinding element)
    {
        var table = new EntityTableSource(nav.Target);
        var sub = new SelectQuery(table);
        var context = Context!.ForSubQuery(sub);
        translator = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = context };
        element = new EntityBinding(table);

        sub.Where.AddRange(context.TableFilters(table));
        if (nav.Mapping is { } mapping)
        {
            var mappingTable = new EntityTableSource(mapping);
            var link = new SelectQuery(mappingTable);
            link.Where.AddRange(context.TableFilters(mappingTable));
            for (var i = 0; i < nav.TargetMappingColumns.Count; i++)
                link.Where.Add(new BinaryExpr(mappingTable.Column(nav.TargetMappingColumns[i]), SqlBinaryOperator.Equal, table.Column(nav.TargetColumns[i])));
            for (var i = 0; i < nav.SelfMappingColumns.Count; i++)
                link.Where.Add(new BinaryExpr(mappingTable.Column(nav.SelfMappingColumns[i]), SqlBinaryOperator.Equal, owner.EntityTable.Column(nav.SelfColumns[i])));
            link.Columns.Add(new SelectColumn(LiteralExpr.One, null));
            sub.Where.Add(new ExistsExpr(link, negated: false));
        }
        else
        {
            for (var i = 0; i < nav.TargetColumns.Count; i++)
                sub.Where.Add(new BinaryExpr(table.Column(nav.TargetColumns[i]), SqlBinaryOperator.Equal, owner.EntityTable.Column(nav.SelfColumns[i])));
        }
        if (nav.ExtraFilter is { } extra) sub.Where.Add(translator.ElementPredicate(extra, element));
        return sub;
    }

    private SqlExpr ElementPredicate(LambdaExpression lambda, SourceBinding element)
    {
        _scope[lambda.Parameters[0]] = element;
        return TranslatePredicate(lambda.Body);
    }

    private static LambdaExpression Lambda(Expression e) =>
        StripConvert(e) as LambdaExpression ?? throw new NotSupportedException($"需要 lambda：{e}");
}
