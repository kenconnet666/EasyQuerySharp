using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 翻译一条 SELECT 时的共享状态：元数据、隐式 JOIN 的去处与缓存、每张表自带的过滤条件（逻辑删除、拦截器）。
/// 子查询用 <see cref="ForSubQuery"/> 派生自己的上下文（隐式 JOIN 加到子查询上），其余共享。
/// </summary>
internal sealed class TranslationContext(
    MetadataRegistry metadata, Func<EntityTableSource, bool, IEnumerable<SqlExpr>> tableFilters, SelectQuery query, TranslationContext? parent = null)
{
    private readonly Dictionary<(TableSource, string), EntityBinding> _joins = [];

    /// <summary>元数据。</summary>
    public MetadataRegistry Metadata => metadata;

    /// <summary>隐式 JOIN 加到的查询。</summary>
    public SelectQuery Query => query;

    /// <summary>某张实体表自带的过滤条件（逻辑删除、查询过滤拦截器）；<paramref name="logicDelete"/> 为 false 时不带逻辑删除（导航上的 DisableLogicDelete()）。</summary>
    public IEnumerable<SqlExpr> TableFilters(EntityTableSource table, bool logicDelete = true) => tableFilters(table, logicDelete);

    /// <summary>为子查询派生上下文：共享元数据、过滤规则与分组合并开关，隐式 JOIN 加到 <paramref name="subQuery"/>。</summary>
    public TranslationContext ForSubQuery(SelectQuery subQuery, IReadOnlyList<GroupJoinSpec>? groupJoinSpecs = null) =>
        new(metadata, tableFilters, subQuery, this)
        {
            Db = Db, GroupJoinAll = GroupJoinAll, MergeGroupFilters = MergeGroupFilters, NavigationValueFilter = NavigationValueFilter, GroupJoinSpecs = groupJoinSpecs ?? [],
        };

    /// <summary>所属的 EasyDb（分组合并的 Configure 要构造目标实体的查询状态）。</summary>
    public EasyDb? Db { get; init; }

    /// <summary>本查询第一个表来源上显式声明的分组合并（Query.GroupJoin / GroupJoinIf）。</summary>
    public IReadOnlyList<GroupJoinSpec> GroupJoinSpecs { get; init; } = [];

    /// <summary>全部集合导航的聚合都合并成分组 JOIN（GroupJoinAll，对应 Java 的 ALL_SUB_QUERY_GROUP_JOIN），子查询里同样生效。</summary>
    public bool GroupJoinAll { get; init; }

    /// <summary>传播到导航条件与子查询里的值过滤（UseValueFilter(…, includeNavigations: true)），子查询里同样生效。</summary>
    public Filtering.ValueFilter NavigationValueFilter { get; init; }

    /// <summary>分组里各聚合共有的条件提到 WHERE（GroupJoinNoMerge 关闭，对应 Java 的 GROUP_JOIN_NOT_ALLOW_AUTO_MERGE）。</summary>
    public bool MergeGroupFilters { get; init; } = true;

    private readonly Dictionary<(TableSource, string), GroupJoinPlan> _groupJoins = [];

    /// <summary>
    /// 这个集合导航是否合并成分组 LEFT JOIN：第一个表来源上用 GroupJoin 声明的、导航上标了 GroupJoin 的、或开了 GroupJoinAll（GroupJoinIf(false) 可关闭）。
    /// 是的话返回（必要时创建）它的合并计划，派生表 JOIN 到所属表所在的查询。
    /// </summary>
    public GroupJoinPlan? GroupJoinFor(SourceBinding owner, NavigationMetadata navigation, GroupJoinWindow? window = null)
    {
        // 所属方可以是实体表，也可以是投影了整个实体的派生表（如 Part 的 Entity）
        if (owner.OwnerEntity is null) return null;
        if (parent is not null && !Owns(owner.Table)) return parent.GroupJoinFor(owner, navigation, window);
        var spec = GroupJoinSpecs.LastOrDefault(s => s.Member == navigation.Property.Name && s.OwnerType.IsAssignableFrom(navigation.Self.Type));
        if (spec is { Enabled: false } || (spec is null && !navigation.GroupJoin && !GroupJoinAll)) return null;
        // 同一导航上不同的取行方式各是一张派生表
        var key = (owner.Table, navigation.Property.Name + "|" + window?.Key);
        if (_groupJoins.TryGetValue(key, out var existing)) return existing;
        var configured = spec?.Configure is { } configure && Db is not null
            ? configure(new Querying.QueryState(Db, [new Querying.EntitySource(navigation.Target.Type)]))
            : null;
        var plan = window is null ? WholePlan(navigation, configured) : WindowedPlan(navigation, configured, window);
        plan.Required = navigation.Required;
        plan.MergeFilters = MergeGroupFilters;
        if (configured is not null) plan.ValueFilter = configured.ValueFilter;
        // 派生表在 Java 里此时创建，别名排在已有的 JOIN（Configure 条件里的导航）之后、之后才加的表之前
        plan.Table.AliasAfterJoins = plan.Inner.Joins.Count;
        // 多对多时 Java 多建了一张中间表的副本，派生表的别名再跳过一个
        plan.Table.ReservedAliases = navigation.Mapping is null ? 0 : 1;
        var keys = plan.KeyColumns
            .Select((k, i) => (SqlExpr)new BinaryExpr(k, SqlBinaryOperator.Equal, owner.OwnerColumn(navigation.SelfColumns[i])))
            .ToList();
        query.Joins.Add(new JoinClause(navigation.Required ? JoinKind.Inner : JoinKind.Left, plan.Table, keys.Count == 1 ? keys[0] : LogicalExpr.Grouped(keys)) { Implicit = true });
        _groupJoins[key] = plan;
        return plan;
    }

    // 整个集合分组：FROM 目标表（多对多时 INNER JOIN 中间表）GROUP BY 关联键
    private GroupJoinPlan WholePlan(NavigationMetadata navigation, Querying.QueryState? configured)
    {
        var element = new EntityTableSource(navigation.Target);
        var inner = new SelectQuery(element);
        var groupKeys = navigation.Mapping is { } mapping
            ? navigation.SelfMappingColumns.Select(JoinMapping(inner, element, navigation, mapping).Column).ToList()
            : navigation.TargetColumns.Select(element.Column).ToList();
        var plan = new GroupJoinPlan(inner, new EntityBinding(element), groupKeys);
        // 配置里再声明的 GroupJoin（嵌套导航）只在这个分组子查询里生效
        plan.InnerContext = ForSubQuery(plan.Inner, configured?.GroupJoins);
        foreach (var filter in TargetFilters(navigation, configured, element, plan.InnerContext)) plan.AddBaseWhere(filter);
        return plan;
    }

    /// <summary>多对多：<paramref name="inner"/> 里目标表 INNER JOIN 中间表（与 Java 一致，ON 里目标列在前），返回中间表。</summary>
    public EntityTableSource JoinMapping(SelectQuery inner, EntityTableSource element, NavigationMetadata navigation, EntityMetadata mapping)
    {
        var link = new EntityTableSource(mapping);
        var on = navigation.TargetColumns
            .Select((c, i) => (SqlExpr)new BinaryExpr(element.Column(c), SqlBinaryOperator.Equal, link.Column(navigation.TargetMappingColumns[i])))
            .ToList();
        if (navigation.ExtraMappingFilter is { } mappingFilter) on.Add(PredicateOn(mappingFilter, link));
        inner.Joins.Add(new JoinClause(JoinKind.Inner, link, on.Count == 1 ? on[0] : LogicalExpr.Grouped(on)) { Implicit = true });
        return link;
    }

    /// <summary>
    /// 先取一段行再分组（Java 的 <c>orderBy(…).elements(i, j)</c>）：每组按排序编行号，再按行号取段，然后 GROUP BY（与 Java 逐字一致）：
    /// <c>FROM (SELECT t2.列 AS 列,…,t2.__row__ AS __row__ FROM (SELECT t1.列 AS 列,…,(ROW_NUMBER() OVER (PARTITION BY 外键 ORDER BY …)) AS __row__
    /// FROM 目标 t1 WHERE …) t2 WHERE t2.__row__ &gt;= ? AND t2.__row__ &lt;= ?) t3</c>。
    /// </summary>
    private GroupJoinPlan WindowedPlan(NavigationMetadata navigation, Querying.QueryState? configured, GroupJoinWindow window)
    {
        if (navigation.Mapping is not null) throw new NotSupportedException($"{navigation} 是多对多导航，分组合并时不支持先取一段行");
        var element = new EntityTableSource(navigation.Target);
        var numbered = new SelectQuery(element);
        var context = ForSubQuery(numbered);
        numbered.Where.AddRange(TargetFilters(navigation, configured, element, context));
        var translator = new ExpressionTranslator([]) { Context = context };
        var binding = new EntityBinding(element);
        foreach (var filter in window.Filters)
        {
            translator.Bind(filter, [binding]);
            numbered.Where.Add(translator.TranslatePredicate(filter.Body));
        }
        // 组内排序：链上写的优先，其次是 GroupJoin(…, q => q.OrderBy(…)) 配的，都没有时按导航的 PartitionOrder
        IEnumerable<(LambdaExpression Key, bool Descending)> orderKeys = window.Orders.Count > 0 || configured is null
            ? window.Orders
            : configured.OrderBys.Select(o => (o.Key, o.Descending));
        var orders = orderKeys.Select(o =>
        {
            translator.Bind(o.Key, [binding]);
            return translator.OrderItem(o.Key.Body, o.Descending);
        }).ToList();
        if (orders.Count == 0) orders.AddRange(translator.DefaultPartitionOrder(navigation, element));

        var columns = navigation.Target.Columns.Where(c => c.AutoSelect).ToList();
        foreach (var c in columns) numbered.Columns.Add(new SelectColumn(element.Column(c), c.Name));
        numbered.Columns.Add(new SelectColumn(
            WindowExpr.RowNumber(navigation.TargetColumns.Select(c => (SqlExpr)element.Column(c)).ToList(), orders), "__row__"));

        var numberedTable = new DerivedTableSource(numbered);
        var ranged = new SelectQuery(numberedTable);
        foreach (var c in columns) ranged.Columns.Add(new SelectColumn(new ColumnExpr(numberedTable, c.Name, c), c.Name));
        var row = new ColumnExpr(numberedTable, "__row__");
        ranged.Columns.Add(new SelectColumn(row, "__row__"));
        ranged.Where.Add(new BinaryExpr(row, SqlBinaryOperator.GreaterThanOrEqual, new ParameterExpr(window.Skip + 1, typeof(long))));
        if (window.Take is { } take) ranged.Where.Add(new BinaryExpr(row, SqlBinaryOperator.LessThanOrEqual, new ParameterExpr(window.Skip + take, typeof(long))));

        var rangedTable = new DerivedTableSource(ranged);
        var plan = new GroupJoinPlan(new SelectQuery(rangedTable),
            new DerivedBinding(rangedTable, columns.ToDictionary(c => c.PropertyName, c => c.Name, StringComparer.Ordinal), navigation.Target),
            navigation.TargetColumns.Select(c => new ColumnExpr(rangedTable, c.Name, c)).ToList());
        plan.InnerContext = ForSubQuery(plan.Inner, configured?.GroupJoins);
        return plan;
    }

    // 目标表自带的过滤（逻辑删除、拦截器）、导航的固定条件、GroupJoin(…, q => q.Where(…)) 加的条件
    private List<SqlExpr> TargetFilters(NavigationMetadata navigation, Querying.QueryState? configured, EntityTableSource element, TranslationContext context)
    {
        var filters = TableFilters(element).ToList();
        if (navigation.ExtraFilter is { } extraFilter) filters.Add(context.PredicateOn(extraFilter, element));
        if (configured is null) return filters;
        var translator = new ExpressionTranslator([]) { Context = context };
        foreach (var where in configured.Wheres)
        {
            translator.Bind(where, [new EntityBinding(element)]);
            filters.Add(translator.TranslatePredicate(where.Body));
        }
        return filters;
    }

    private readonly Dictionary<(TableSource, string), DerivedBinding> _elementJoins = [];

    /// <summary>
    /// 集合导航里的某一行（First / ElementAt）JOIN 成的派生表：同一所属行上、同样的链（条件、排序、序号）只 JOIN 一次。
    /// 所属表在外层时 JOIN 加到外层。<paramref name="build"/> 在本查询的上下文里构造派生表、ON 条件与绑定。
    /// </summary>
    public DerivedBinding ElementJoinFor(SourceBinding owner, string key, bool required,
        Func<TranslationContext, (DerivedTableSource Table, SqlExpr On, DerivedBinding Binding)> build)
    {
        if (parent is not null && !Owns(owner.Table)) return parent.ElementJoinFor(owner, key, required, build);
        if (_elementJoins.TryGetValue((owner.Table, key), out var existing)) return existing;
        var (table, on, binding) = build(this);
        query.Joins.Add(new JoinClause(required ? JoinKind.Inner : JoinKind.Left, table, on) { Implicit = true });
        _elementJoins[(owner.Table, key)] = binding;
        return binding;
    }

    /// <summary>整条查询翻译完后调用：按登记的聚合生成各分组派生表的列。</summary>
    public void CompleteGroupJoins()
    {
        foreach (var plan in _groupJoins.Values)
        {
            // 嵌套的分组合并（分组子查询里再合并的导航）先收尾
            plan.InnerContext.CompleteGroupJoins();
            plan.Complete();
        }
    }

    /// <summary>
    /// 对一导航的隐式 JOIN：同一个来源上的同一个导航只连一次。ON 里先写目标表自带的过滤条件，再写 <c>目标键 = 本表键</c>（与 Java 版一致）；
    /// <see cref="NavigationMetadata.Required"/> 时用 INNER JOIN，否则 LEFT JOIN。
    /// </summary>
    public EntityBinding JoinFor(SourceBinding owner, NavigationMetadata navigation)
    {
        // 所属表在外层查询里（子查询引用外层的导航）：JOIN 加到外层（与 Java 一致）
        if (parent is not null && !Owns(owner.Table)) return parent.JoinFor(owner, navigation);
        var key = (owner.Table, navigation.Property.Name);
        if (_joins.TryGetValue(key, out var existing)) return existing;

        var target = new EntityTableSource(navigation.Target);
        var conditions = new List<SqlExpr>(TableFilters(target));
        var keys = navigation.SelfColumns
            .Select((self, i) => (SqlExpr)new BinaryExpr(target.Column(navigation.TargetColumns[i]), SqlBinaryOperator.Equal, owner.OwnerColumn(self)))
            .ToList();
        // 复合键整体加括号：ON (a = a AND b = b)
        conditions.Add(keys.Count == 1 ? keys[0] : LogicalExpr.Grouped(keys));
        if (navigation.ExtraFilter is { } extra) conditions.Add(PredicateOn(extra, target));
        query.Joins.Add(new JoinClause(navigation.Required ? JoinKind.Inner : JoinKind.Left, target, LogicalExpr.And(conditions)) { Implicit = true });
        var binding = new EntityBinding(target);
        _joins[key] = binding;
        return binding;
    }

    // 单参数条件（导航的固定条件等）绑定到指定的表后翻译
    private SqlExpr PredicateOn(LambdaExpression predicate, EntityTableSource table)
    {
        var translator = new ExpressionTranslator([]) { Context = this };
        translator.Bind(predicate, [new EntityBinding(table)]);
        return translator.TranslatePredicate(predicate.Body);
    }

    // 表来源是不是本查询的 FROM / JOIN（含已加上的隐式 JOIN）
    private bool Owns(TableSource table) => query.From == table || query.Joins.Any(j => j.Table == table);
}
