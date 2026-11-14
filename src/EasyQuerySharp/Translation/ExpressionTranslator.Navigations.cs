using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>当前 SELECT 的翻译上下文（导航要用）；写入命令等没有上下文时为 null，此时不支持导航。</summary>
    public TranslationContext? Context { get; set; }

    private NavigationMetadata? Navigation(SourceBinding owner, string member) =>
        owner.OwnerEntity is { } entity ? Context?.Metadata.FindNavigation(entity, member) : null;

    /// <summary>所属表上名为 <paramref name="member"/> 的导航；不是导航时返回 null（投影编译器识别带出时用）。</summary>
    internal NavigationMetadata? FindNavigation(SourceBinding owner, string member) =>
        owner.OwnerEntity is { } entity ? Context?.Metadata.FindNavigation(entity, member) : null;

    /// <summary>
    /// 剥掉导航外面的配置标记（<see cref="Sql.DisableLogicDelete{T}(T)"/>、<see cref="Sql.AsTable{T}(T, string)"/>、<see cref="Sql.AsAlias{T}(T, string)"/>、
    /// <see cref="Sql.AsTableSegment{T}(T, Func{string, string?, string})"/> 及集合上的同名扩展），返回导航本身的表达式；没有标记时原样返回、<paramref name="options"/> 为默认值。
    /// </summary>
    private static Expression StripNavigationOptions(Expression e, out NavigationTableOptions options)
    {
        options = default;
        while (StripConvert(e) is MethodCallExpression { Method.Name: nameof(Sql.DisableLogicDelete) or nameof(Sql.AsTable) or nameof(Sql.AsAlias) or nameof(Sql.AsTableSegment) } call
               && call.Method.DeclaringType == typeof(Sql))
        {
            // 外层的标记先剥到，内层的同类标记不覆盖它
            options = call.Method.Name switch
            {
                nameof(Sql.DisableLogicDelete) => options with { NoLogicDelete = true },
                nameof(Sql.AsTable) => options with { TableName = options.TableName ?? ClientEvaluator.Evaluate(call.Arguments[1]) as string },
                nameof(Sql.AsAlias) => options with { Alias = options.Alias ?? ClientEvaluator.Evaluate(call.Arguments[1]) as string },
                _ => options with { Segment = options.Segment ?? ClientEvaluator.Evaluate(call.Arguments[1]) as Func<string, string?, string> },
            };
            e = call.Arguments[0];
        }
        return e;
    }

    /// <summary>对一导航 <c>x.User</c>：返回隐式 JOIN 的目标表绑定；不是对一导航时返回 null。</summary>
    private SourceBinding? ToOneNavigation(MemberExpression m, NavigationTableOptions options = default)
    {
        // 起点可以是实体表，也可以是整行实体的派生表（如集合导航的 First() 行）
        if (m.Expression is null || BindingOf(m.Expression) is not { OwnerEntity: { } entity } owner) return null;
        if (Context?.Metadata.FindNavigation(entity, m.Member.Name) is not { IsCollection: false } nav) return null;
        if (nav.Limit is not null && nav.DirectPath.Count == 0)
            return options == default
                ? RankedToOne(owner, nav)
                : throw Unsupported(m, $"声明了条数的对一导航 {nav} 不支持 Sql.DisableLogicDelete / Sql.AsTable / Sql.AsAlias / Sql.AsTableSegment");
        if (nav.DirectPath.Count == 0) return Context!.JoinFor(owner, nav, options);
        if (options != default) throw Unsupported(m, $"直达导航 {nav} 不支持 Sql.DisableLogicDelete / Sql.AsTable / Sql.AsAlias / Sql.AsTableSegment");
        // 直达导航：沿路径逐级 JOIN（与手写 x.A.B 共用同一组 JOIN）
        var current = Context!.JoinFor(owner, Context.Metadata.FindNavigation(entity, nav.DirectPath[0]) is { IsCollection: false } first
            ? first : throw Unsupported(m, $"直达路径 {nav} 上的 {nav.DirectPath[0]} 必须是对一导航"));
        foreach (var step in nav.DirectPath.Skip(1))
        {
            var hop = Navigation(current, step) is { IsCollection: false } n ? n : throw Unsupported(m, $"直达路径 {nav} 上的 {step} 必须是对一导航");
            current = Context!.JoinFor(current, hop);
        }
        return current;
    }

    /// <summary>集合导航 <c>x.Cards</c>：返回所属表绑定与导航；不是集合导航时返回 null。</summary>
    private (SourceBinding Owner, NavigationMetadata Navigation)? CollectionNavigation(Expression e)
    {
        if (StripConvert(e) is not MemberExpression { Expression: { } inner } m || BindingOf(inner) is not { } owner || owner.OwnerEntity is not { } entity) return null;
        return Context?.Metadata.FindNavigation(entity, m.Member.Name) is { IsCollection: true } nav ? (owner, nav) : null;
    }

    /// <summary>
    /// 集合导航上的 LINQ 方法 → 隐式子查询：
    /// <c>Any(…)</c> → EXISTS、<c>All(p)</c> → NOT EXISTS(… AND NOT p)、<c>Count(…)</c> / <c>Sum</c> / <c>Max</c> / <c>Min</c> / <c>Average</c> → 标量子查询。
    /// 中间可以链 <c>Where</c>、<c>Select</c>、<c>Distinct</c>、<c>Take</c>。
    /// </summary>
    private bool TryTranslateCollection(MethodCallExpression call, out SqlExpr result, bool negated = false)
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
        current = StripNavigationOptions(current, out var options);
        if (CollectionNavigation(current) is not var (owner, nav)) return false;
        chain.Reverse();

        // 声明了分组合并的导航：聚合登记到合并的派生表上
        if (Context?.GroupJoinFor(owner, nav, WindowOf(chain, out var rest), options) is { } plan)
        {
            result = TranslateGroupJoin(plan, rest, negated);
            return true;
        }
        // 取反只在分组合并时特殊处理（!Any → None 列），其余照常翻译后取反
        if (negated) return false;

        var scope = NewCollectionSubQuery(owner, nav, options);
        foreach (var op in chain.Take(chain.Count - 1)) scope.Apply(op);

        var terminal = chain[^1];
        // All 的条件不做值过滤（去掉后"全部满足"没有意义）
        var predicate = terminal.Arguments.Count > 1 && terminal.Method.Name is nameof(Enumerable.Any) or nameof(Enumerable.All)
            or nameof(Enumerable.Count) or nameof(Enumerable.LongCount)
            ? terminal.Method.Name == nameof(Enumerable.All)
                // All(p) 找不满足的行：C# 的语义下就是 !p 的翻译（p 是 NULL 的行也算不满足）
                ? scope.Predicate(CSharpNulls ? NegatedLambda(Lambda(terminal.Arguments[1])) : Lambda(terminal.Arguments[1]), filtered: false)
                : scope.Predicate(Lambda(terminal.Arguments[1]))
            : null;
        // Any / All 只看有没有行，LIMIT 留在 EXISTS 里不影响结果；计数与聚合只算取到的那段行
        if (terminal.Method.Name is not (nameof(Enumerable.Any) or nameof(Enumerable.All))) scope.BeforeAggregate();
        var (sub, t, element, selector, distinct) = (scope.Query, scope.Translator, scope.Element, scope.Selector, scope.Distinct);
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
                // 与 Java 一致：集合导航的 any 带 LIMIT 1
                sub.Limit ??= 1;
                result = new ExistsExpr(sub, negated: false) { FromNavigation = true };
                return true;
            case nameof(Enumerable.All):
                // 没有不满足的：NOT (EXISTS(… AND (NOT (p)) LIMIT 1))（与 Java 一致）
                if (predicate is null) throw Unsupported(terminal);
                sub.Where.Add(CSharpNulls ? predicate : NotSatisfying(predicate));
                sub.Columns.Add(new SelectColumn(LiteralExpr.One, null));
                sub.Limit ??= 1;
                result = new NotExpr(new ExistsExpr(sub, negated: false) { FromNavigation = true });
                return true;
            case nameof(Enumerable.Count) or nameof(Enumerable.LongCount):
                if (predicate is not null) sub.Where.Add(predicate);
                // Select(x => x.Col).Count() 计非 NULL 的值：COUNT(列) / COUNT(DISTINCT 列)（对应 Java 的 count(x -> x.col)）
                SqlExpr counted = LiteralExpr.Star;
                if (selector is not null && predicate is null)
                {
                    t.Bind(selector, [element]);
                    counted = t.Translate(StripConvert(selector.Body));
                }
                sub.Columns.Add(new SelectColumn(new FunctionExpr(distinct && selector is not null ? SqlFunctionNames.CountDistinct : SqlFunctionNames.Count, [counted]), null));
                break;
            case nameof(Enumerable.Sum):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(distinct ? SqlFunctionNames.SumDistinct : SqlFunctionNames.Sum, [Value()]), null));
                // 没有行时 SUM 是 NULL，与 Java 一致补成 0
                result = new FunctionExpr(SqlFunctionNames.NullOrDefault, [new SubQueryExpr(sub), new LiteralExpr("0")]);
                return true;
            case nameof(Enumerable.Max):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Max, [Value()]), null));
                break;
            case nameof(Enumerable.Min):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Min, [Value()]), null));
                break;
            case nameof(Enumerable.Average):
                sub.Columns.Add(new SelectColumn(new FunctionExpr(distinct ? SqlFunctionNames.AvgDistinct : SqlFunctionNames.Avg, [Value()]), null));
                // 结果可空时与 C# 一致：没有行是 null；不可空时（C# 会抛异常）与 Java 一致补成 0
                result = CSharpNulls && Nullable.GetUnderlyingType(terminal.Type) is not null
                    ? new SubQueryExpr(sub)
                    : new FunctionExpr(SqlFunctionNames.NullOrDefault, [new SubQueryExpr(sub), new LiteralExpr("0")]);
                return true;
            default:
                throw Unsupported(terminal, "集合导航上不支持这个方法");
        }
        result = new SubQueryExpr(sub);
        return true;
    }

    /// <summary>
    /// <c>string.Join(",", u.Cards.Where(…).Select(c =&gt; c.Code))</c> → 字符串聚合（对应 Java 的 joining(x -&gt; x.code(), ",")）：
    /// 分组合并时是派生表的一列，否则是 <c>(SELECT GROUP_CONCAT(code SEPARATOR ?) FROM … )</c> 相关子查询。
    /// </summary>
    private bool TryTranslateCollectionStringJoin(MethodCallExpression call, out SqlExpr result)
    {
        result = null!;
        if (call.Method.DeclaringType != typeof(string) || call.Method.Name != nameof(string.Join) || call.Arguments.Count != 2
            || !call.Method.GetParameters()[1].ParameterType.IsGenericType) return false;
        var chain = new List<MethodCallExpression>();
        var current = StripConvert(call.Arguments[1]);
        while (current is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Arguments.Count > 0)
        {
            chain.Add(mc);
            current = StripConvert(mc.Arguments[0]);
        }
        current = StripNavigationOptions(current, out var options);
        if (chain.Count == 0 || CollectionNavigation(current) is not var (owner, nav)) return false;
        chain.Reverse();
        var separator = Translate(call.Arguments[0]);

        if (Context?.GroupJoinFor(owner, nav, WindowOf(chain, out var rest), options) is { } plan)
        {
            result = TranslateGroupJoin(plan, rest, negated: false, separator);
            return true;
        }
        var scope = NewCollectionSubQuery(owner, nav, options);
        foreach (var op in chain) scope.Apply(op);
        scope.BeforeAggregate();
        if (scope.Selector is not { } selector) throw Unsupported(call, "string.Join 需要先 Select 出字符串");
        scope.Translator.Bind(selector, [scope.Element]);
        var value = scope.Translator.Translate(StripConvert(selector.Body));
        // 集合上的排序决定拼接顺序，写进聚合函数（子查询自己的 ORDER BY 不影响聚合）
        SqlExpr joined;
        if (scope.ElementOrder.Count > 0)
        {
            joined = new OrderedStringJoinExpr(value, separator, scope.Distinct, [.. scope.ElementOrder]);
            scope.Query.OrderBy.Clear();
        }
        else joined = new FunctionExpr(scope.Distinct ? SqlFunctionNames.StringJoinDistinct : SqlFunctionNames.StringJoin, [value, separator]);
        scope.Query.Columns.Add(new SelectColumn(joined, null));
        result = new SubQueryExpr(scope.Query);
        return true;
    }

    /// <summary><c>x.Cards.Count</c>（List 的 Count 属性）→ 计数子查询。</summary>
    private SqlExpr? TryTranslateCollectionCount(MemberExpression m)
    {
        if (m.Member.Name != "Count" || m.Expression is null || CollectionNavigation(m.Expression) is not var (owner, nav)) return null;
        if (Context?.GroupJoinFor(owner, nav) is { } plan) return plan.Reference(new GroupAggregateRequest(GroupAggregate.Count, null, [], false, ""));
        var sub = NewCollectionSubQuery(owner, nav).Query;
        sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]), null));
        return new SubQueryExpr(sub);
    }

    /// <summary>
    /// 集合导航的子查询骨架：目标表自带的过滤条件、与所属行的关联条件、导航的固定条件。
    /// 一对多关联写成 <c>目标.外键 = 所属.键</c>；多对多经中间表写成 EXISTS。
    /// </summary>
    private CollectionSubQuery NewCollectionSubQuery(SourceBinding owner, NavigationMetadata nav, NavigationTableOptions options = default)
    {
        var table = Context!.TableFor(nav.Target, options);
        var sub = new SelectQuery(table);
        var context = Context.ForSubQuery(sub);
        var translator = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = context };
        var element = new EntityBinding(table);

        sub.Where.AddRange(context.TableFilters(table, !options.NoLogicDelete));
        // 关联条件与导航的固定条件合成一组 (关联 AND 固定条件)（与 Java 一致）；没有固定条件时不加括号
        var relation = new List<SqlExpr>();
        if (nav.Mapping is { } mapping)
        {
            var mappingTable = context.TableFor(mapping);
            var link = new SelectQuery(mappingTable);
            link.Where.AddRange(context.TableFilters(mappingTable));
            for (var i = 0; i < nav.TargetMappingColumns.Count; i++)
                link.Where.Add(new BinaryExpr(mappingTable.Column(nav.TargetMappingColumns[i]), SqlBinaryOperator.Equal, table.Column(nav.TargetColumns[i])));
            for (var i = 0; i < nav.SelfMappingColumns.Count; i++)
                link.Where.Add(new BinaryExpr(mappingTable.Column(nav.SelfMappingColumns[i]), SqlBinaryOperator.Equal, owner.OwnerColumn(nav.SelfColumns[i])));
            if (nav.ExtraMappingFilter is { } mappingFilter) link.Where.Add(translator.ElementPredicate(mappingFilter, new EntityBinding(mappingTable)));
            link.Columns.Add(new SelectColumn(LiteralExpr.One, null));
            // 中间表的 EXISTS 也带 LIMIT 1（与 Java 一致）
            link.Limit = 1;
            relation.Add(new ExistsExpr(link, negated: false));
        }
        else if (nav.KeyMatch == KeyMatch.SelfCsv)
            relation.Add(new FunctionExpr(SqlFunctionNames.InCsv, [table.Column(nav.TargetColumns[0]), owner.OwnerColumn(nav.SelfColumns[0])]) { IsPredicate = true });
        else
        {
            var keys = nav.TargetColumns
                .Select((target, i) => (SqlExpr)new BinaryExpr(table.Column(target), SqlBinaryOperator.Equal, owner.OwnerColumn(nav.SelfColumns[i])))
                .ToList();
            // 复合键整体加括号（与 Java 一致）
            relation.Add(keys.Count == 1 ? keys[0] : LogicalExpr.Grouped(keys));
        }
        if (nav.ExtraFilter is { } extra)
        {
            relation.Add(translator.ElementPredicate(extra, element));
            sub.Where.Add(LogicalExpr.Grouped(relation));
        }
        else sub.Where.AddRange(relation);
        // 查询上声明的导航条件（FilterNavigation）跟在关联条件之后
        foreach (var filter in Context!.FiltersFor(nav)) sub.Where.Add(translator.ElementPredicate(filter, element));
        return new CollectionSubQuery(this, sub, translator, element);
    }

    /// <summary>
    /// 导航里的条件按传播下来的值过滤改写（UseValueFilter(…, includeNavigations: true)）；条件整个被去掉时返回 null。
    /// <paramref name="own"/> 是这个导航自己配置的值过滤（GroupJoin(…, q =&gt; q.UseValueFilter(…))），优先。
    /// </summary>
    private LambdaExpression? NavigationPredicate(LambdaExpression predicate, Filtering.ValueFilter own = Filtering.ValueFilter.All) =>
        Filtering.ValueFilterRewriter.Apply(predicate, own != Filtering.ValueFilter.All ? own : Context?.NavigationValueFilter ?? Filtering.ValueFilter.All);

    private SqlExpr ElementPredicate(LambdaExpression lambda, SourceBinding element)
    {
        _scope[lambda.Parameters[0]] = element;
        return TranslatePredicate(lambda.Body);
    }

    private static LambdaExpression Lambda(Expression e) =>
        StripConvert(e) as LambdaExpression ?? throw new NotSupportedException($"需要 lambda：{e}");
}
