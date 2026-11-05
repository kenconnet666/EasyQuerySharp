using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>当前 SELECT 的翻译上下文（导航要用）；写入命令等没有上下文时为 null，此时不支持导航。</summary>
    public TranslationContext? Context { get; set; }

    private NavigationMetadata? Navigation(EntityBinding owner, string member) => Context?.Metadata.FindNavigation(owner.Entity, member);

    /// <summary>所属表上名为 <paramref name="member"/> 的导航；不是导航时返回 null（投影编译器识别带出时用）。</summary>
    internal NavigationMetadata? FindNavigation(EntityBinding owner, string member) => Navigation(owner, member);

    /// <summary>对一导航 <c>x.User</c>：返回隐式 JOIN 的目标表绑定；不是对一导航时返回 null。</summary>
    private EntityBinding? ToOneNavigation(MemberExpression m)
    {
        // 起点可以是实体表，也可以是整行实体的派生表（如集合导航的 First() 行）
        if (m.Expression is null || BindingOf(m.Expression) is not { OwnerEntity: { } entity } owner) return null;
        if (Context?.Metadata.FindNavigation(entity, m.Member.Name) is not { IsCollection: false } nav) return null;
        if (nav.DirectPath.Count == 0) return Context!.JoinFor(owner, nav);
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
        var logicDelete = true;
        if (current is MethodCallExpression { Method.Name: nameof(Sql.DisableLogicDelete) } disable && disable.Method.DeclaringType == typeof(Sql))
        {
            logicDelete = false;
            current = disable.Arguments[0];
        }
        if (CollectionNavigation(current) is not var (owner, nav)) return false;
        chain.Reverse();

        // 声明了分组合并的导航：聚合登记到合并的派生表上
        if (Context?.GroupJoinFor(owner, nav, WindowOf(chain, out var rest)) is { } plan)
        {
            result = TranslateGroupJoin(plan, rest, negated);
            return true;
        }
        // 取反只在分组合并时特殊处理（!Any → None 列），其余照常翻译后取反
        if (negated) return false;

        var scope = NewCollectionSubQuery(owner, nav, logicDelete);
        foreach (var op in chain.Take(chain.Count - 1)) scope.Apply(op);

        var terminal = chain[^1];
        var predicate = terminal.Arguments.Count > 1 && terminal.Method.Name is nameof(Enumerable.Any) or nameof(Enumerable.All)
            or nameof(Enumerable.Count) or nameof(Enumerable.LongCount)
            ? scope.Predicate(Lambda(terminal.Arguments[1]))
            : null;
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
                sub.Where.Add(Negate(predicate ?? throw Unsupported(terminal)));
                sub.Columns.Add(new SelectColumn(LiteralExpr.One, null));
                result = new ExistsExpr(sub, negated: true);
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
                result = new FunctionExpr(SqlFunctionNames.NullOrDefault, [new SubQueryExpr(sub), new LiteralExpr("0")]);
                return true;
            default:
                throw Unsupported(terminal, "集合导航上不支持这个方法");
        }
        result = new SubQueryExpr(sub);
        return true;
    }

    /// <summary>
    /// 分组合并下的集合聚合：链上的 Where 合成这个聚合的条件，Select 是值，Distinct 去重；
    /// <c>Any</c> / <c>!Any</c>（<paramref name="negated"/>）/ <c>All</c> / <c>Count</c> / <c>Sum</c> / <c>Max</c> / <c>Min</c> / <c>Average</c>。
    /// <c>Select(x =&gt; x.Col).Count()</c> 计的是非 NULL 的值（SQL 的 COUNT(列)，对应 Java 的 count(x -&gt; x.col)）。
    /// </summary>
    private SqlExpr TranslateGroupJoin(GroupJoinPlan plan, List<MethodCallExpression> chain, bool negated, SqlExpr? separator = null)
    {
        var t = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = plan.InnerContext };
        var element = plan.Element;
        var filters = new List<SqlExpr>();
        var filterKeys = new List<string>();
        LambdaExpression? selector = null;
        var distinct = false;
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
        var filter = filters.Count == 0 ? null : LogicalExpr.And(filters);
        return plan.Reference(kind, value, filter, distinct, string.Join(" && ", filterKeys), valueLambda is null ? "" : GroupJoinKey(valueLambda), separator);

        void AddFilter(LambdaExpression original, bool negate)
        {
            if (Filtering.ValueFilterRewriter.Apply(original, plan.ValueFilter) is not { } predicate) return;
            var sql = t.ElementPredicate(predicate, element);
            filters.Add(negate ? Negate(sql) : sql);
            filterKeys.Add((negate ? "!" : "") + GroupJoinKey(predicate));
        }
    }

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
        if (chain.Count == 0 || CollectionNavigation(current) is not var (owner, nav)) return false;
        chain.Reverse();
        var separator = Translate(call.Arguments[0]);

        if (Context?.GroupJoinFor(owner, nav, WindowOf(chain, out var rest)) is { } plan)
        {
            result = TranslateGroupJoin(plan, rest, negated: false, separator);
            return true;
        }
        var scope = NewCollectionSubQuery(owner, nav);
        foreach (var op in chain) scope.Apply(op);
        if (scope.Selector is not { } selector) throw Unsupported(call, "string.Join 需要先 Select 出字符串");
        scope.Translator.Bind(selector, [scope.Element]);
        scope.Query.Columns.Add(new SelectColumn(new FunctionExpr(scope.Distinct ? SqlFunctionNames.StringJoinDistinct : SqlFunctionNames.StringJoin,
            [scope.Translator.Translate(StripConvert(selector.Body)), separator]), null));
        result = new SubQueryExpr(scope.Query);
        return true;
    }

    /// <summary>
    /// 集合导航里的某一行：<c>u.Cards.Where(…).OrderBy(…).First()</c> / <c>ElementAt(n)</c> → 派生表
    /// <c>(SELECT 目标列…, (ROW_NUMBER() OVER (PARTITION BY 外键 ORDER BY …)) AS __row__ FROM 目标 WHERE …) tN</c>，
    /// <c>ON (tN.外键 = 所属.键 AND tN.__row__ = n+1)</c>（对应 Java 的 first() / element(n)）。之后可以继续取列、走对一导航。
    /// 没有写排序时按目标表主键升序。
    /// </summary>
    private DerivedBinding? ElementBinding(MethodCallExpression call)
    {
        if (call.Method.DeclaringType != typeof(Enumerable) || Context is null
            || call.Method.Name is not (nameof(Enumerable.First) or nameof(Enumerable.FirstOrDefault) or nameof(Enumerable.ElementAt) or nameof(Enumerable.ElementAtOrDefault)))
            return null;
        var chain = new List<MethodCallExpression>();
        var current = call.Arguments[0];
        while (current is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Arguments.Count > 0)
        {
            chain.Add(mc);
            current = mc.Arguments[0];
        }
        if (CollectionNavigation(current) is not var (owner, nav)) return null;
        if (nav.Mapping is not null) throw Unsupported(call, "多对多导航不支持取第几行");
        chain.Reverse();
        var elementAt = call.Method.Name.StartsWith(nameof(Enumerable.ElementAt), System.StringComparison.Ordinal);
        var index = elementAt ? Convert.ToInt32(ClientEvaluator.Evaluate(call.Arguments[1]), System.Globalization.CultureInfo.InvariantCulture) : 0;
        var predicate = !elementAt && call.Arguments.Count > 1 ? Lambda(call.Arguments[1]) : null;
        var key = string.Join("|", chain.Select(op => op.Method.Name + ":" + (op.Arguments.Count > 1 ? GroupJoinKey(Lambda(op.Arguments[1])) : "")))
                  + "|" + (predicate is null ? "" : GroupJoinKey(predicate)) + "|" + nav.Property.Name + "#" + index;
        return Context.ElementJoinFor(owner, key, nav.Required, context =>
        {
            var element = new EntityTableSource(nav.Target);
            var inner = new SelectQuery(element);
            var t = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = context.ForSubQuery(inner) };
            var binding = new EntityBinding(element);
            inner.Where.AddRange(context.TableFilters(element));
            var orders = new List<OrderByItem>();
            foreach (var op in chain)
            {
                switch (op.Method.Name)
                {
                    case nameof(Enumerable.Where):
                        inner.Where.Add(t.ElementPredicate(Lambda(op.Arguments[1]), binding));
                        break;
                    case nameof(Enumerable.OrderBy) or nameof(Enumerable.ThenBy) or nameof(Enumerable.OrderByDescending) or nameof(Enumerable.ThenByDescending):
                        var keyLambda = Lambda(op.Arguments[1]);
                        t.Bind(keyLambda, [binding]);
                        orders.Add(new OrderByItem(t.Translate(StripConvert(keyLambda.Body)), op.Method.Name.EndsWith("Descending", System.StringComparison.Ordinal)));
                        break;
                    default:
                        throw Unsupported(op, "取第几行之前只支持 Where / OrderBy / ThenBy");
                }
            }
            if (predicate is not null) inner.Where.Add(t.ElementPredicate(predicate, binding));
            if (orders.Count == 0) orders.AddRange(nav.Target.KeyColumns.Select(k => new OrderByItem(element.Column(k), false)));

            var columns = nav.Target.Columns.Where(c => c.AutoSelect).ToList();
            foreach (var c in columns) inner.Columns.Add(new SelectColumn(element.Column(c), null));
            var row = new WindowExpr(new FragmentExpr("ROW_NUMBER()", []), nav.TargetColumns.Select(c => (SqlExpr)element.Column(c)).ToList(), orders, null, compact: false);
            inner.Columns.Add(new SelectColumn(row, "__row__"));
            // Java 在这里多一层匿名表，派生表的别名跳过一个编号
            var table = new DerivedTableSource(inner) { ReserveExtraAlias = true };
            var on = nav.TargetColumns
                .Select((target, i) => (SqlExpr)new BinaryExpr(new ColumnExpr(table, target.Name, target), SqlBinaryOperator.Equal, owner.OwnerColumn(nav.SelfColumns[i])))
                .Append(new BinaryExpr(new ColumnExpr(table, "__row__"), SqlBinaryOperator.Equal, new ParameterExpr(index + 1, typeof(int))))
                .ToList();
            return (table, LogicalExpr.Grouped(on), new DerivedBinding(table, columns.ToDictionary(c => c.PropertyName, c => c.Name), nav.Target));
        });
    }

    /// <summary><c>x.Cards.Count</c>（List 的 Count 属性）→ 计数子查询。</summary>
    private SqlExpr? TryTranslateCollectionCount(MemberExpression m)
    {
        if (m.Member.Name != "Count" || m.Expression is null || CollectionNavigation(m.Expression) is not var (owner, nav)) return null;
        if (Context?.GroupJoinFor(owner, nav) is { } plan) return plan.Reference(GroupAggregate.Count, null, null, false, "", "");
        var sub = NewCollectionSubQuery(owner, nav).Query;
        sub.Columns.Add(new SelectColumn(new FunctionExpr(SqlFunctionNames.Count, [LiteralExpr.Star]), null));
        return new SubQueryExpr(sub);
    }

    /// <summary>
    /// 集合导航的子查询骨架：目标表自带的过滤条件、与所属行的关联条件、导航的固定条件。
    /// 一对多关联写成 <c>目标.外键 = 所属.键</c>；多对多经中间表写成 EXISTS。
    /// </summary>
    private CollectionSubQuery NewCollectionSubQuery(SourceBinding owner, NavigationMetadata nav, bool logicDelete = true)
    {
        var table = new EntityTableSource(nav.Target);
        var sub = new SelectQuery(table);
        var context = Context!.ForSubQuery(sub);
        var translator = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(_scope)) { Context = context };
        var element = new EntityBinding(table);

        sub.Where.AddRange(context.TableFilters(table, logicDelete));
        // 关联条件与导航的固定条件合成一组 (关联 AND 固定条件)（与 Java 一致）；没有固定条件时不加括号
        var relation = new List<SqlExpr>();
        if (nav.Mapping is { } mapping)
        {
            var mappingTable = new EntityTableSource(mapping);
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
        return new CollectionSubQuery(this, sub, translator, element);
    }

    private SqlExpr ElementPredicate(LambdaExpression lambda, SourceBinding element)
    {
        _scope[lambda.Parameters[0]] = element;
        return TranslatePredicate(lambda.Body);
    }

    private static LambdaExpression Lambda(Expression e) =>
        StripConvert(e) as LambdaExpression ?? throw new NotSupportedException($"需要 lambda：{e}");
}
