using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

// 集合导航的分组合并：按导航建分组派生表（整个集合 / 先取一段行），整条查询翻译完后生成列并下推键条件
internal sealed partial class TranslationContext
{
    private readonly Dictionary<(TableSource, string), GroupJoinPlan> _groupJoins = [];

    /// <summary>
    /// 这个集合导航是否合并成分组 LEFT JOIN：第一个表来源上用 GroupJoin 声明的、导航上标了 GroupJoin 的、或开了 GroupJoinAll（GroupJoinIf(false) 可关闭）。
    /// 是的话返回（必要时创建）它的合并计划，派生表 JOIN 到所属表所在的查询。
    /// </summary>
    public GroupJoinPlan? GroupJoinFor(SourceBinding owner, NavigationMetadata navigation, GroupJoinWindow? window = null, NavigationTableOptions options = default)
    {
        // 所属方可以是实体表，也可以是投影了整个实体的派生表（如 Part 的 Entity）
        if (owner.OwnerEntity is null) return null;
        if (parent is not null && !Owns(owner.Table)) return parent.GroupJoinFor(owner, navigation, window, options);
        var spec = GroupJoinSpecs.LastOrDefault(s => s.Member == navigation.Property.Name && s.OwnerType.IsAssignableFrom(navigation.Self.Type));
        if (spec is { Enabled: false } || (spec is null && !navigation.GroupJoin && !GroupJoinAll)) return null;
        // 同一导航上不同的取行方式各是一张派生表
        var key = (owner.Table, navigation.Property.Name + "|" + window?.Key + options.Key);
        if (_groupJoins.TryGetValue(key, out var existing)) return existing;
        var configured = spec?.Configure is { } configure && Db is not null
            ? configure(new Querying.QueryState(Db, [new Querying.EntitySource(navigation.Target.Type)]))
            : null;
        // 导航上的表配置与 GroupJoin(…, q => q.AsTable(..) / AsAlias(..) / DisableLogicDelete()) 配的合在一起
        var source = configured?.Sources[0];
        options = new NavigationTableOptions(options.NoLogicDelete || configured?.DisableLogicDelete == true,
            options.TableName ?? (source as Querying.EntitySource)?.TableName, options.Alias ?? source?.Alias);
        var plan = window is null ? WholePlan(navigation, configured, options) : WindowedPlan(navigation, configured, window, options);
        plan.Required = navigation.Required;
        plan.MergeFilters = MergeGroupFilters;
        if (configured is not null) plan.ValueFilter = configured.ValueFilter;
        // 派生表在 Java 里此时创建，别名排在已有的 JOIN（Configure 条件里的导航）之后、之后才加的表之前
        plan.Table.AliasAfterJoins = plan.Inner.Joins.Count;
        // 多对多时 Java 多建了一张中间表的副本，派生表的别名再跳过一个
        plan.Table.ReservedAliases = navigation.Mapping is null ? 0 : 1;
        plan.OwnerColumns = navigation.SelfColumns.Select(owner.OwnerColumn).OfType<ColumnExpr>().ToList();
        var keys = plan.KeyColumns
            .Select((k, i) => (SqlExpr)new BinaryExpr(k, SqlBinaryOperator.Equal, owner.OwnerColumn(navigation.SelfColumns[i])))
            .ToList();
        query.Joins.Add(new JoinClause(navigation.Required ? JoinKind.Inner : JoinKind.Left, plan.Table, keys.Count == 1 ? keys[0] : LogicalExpr.Grouped(keys)) { Implicit = true, AfterWhere = WhereTranslated });
        _groupJoins[key] = plan;
        return plan;
    }

    // 整个集合分组：FROM 目标表（多对多时 INNER JOIN 中间表）GROUP BY 关联键
    private GroupJoinPlan WholePlan(NavigationMetadata navigation, Querying.QueryState? configured, NavigationTableOptions options)
    {
        var element = options.TableFor(navigation.Target);
        var inner = new SelectQuery(element);
        var groupKeys = navigation.Mapping is { } mapping
            ? navigation.SelfMappingColumns.Select(JoinMapping(inner, element, navigation, mapping).Column).ToList()
            : navigation.TargetColumns.Select(element.Column).ToList();
        var plan = new GroupJoinPlan(inner, new EntityBinding(element), groupKeys);
        // 配置里再声明的 GroupJoin（嵌套导航）只在这个分组子查询里生效
        plan.InnerContext = ForSubQuery(plan.Inner, configured?.GroupJoins);
        foreach (var filter in TargetFilters(navigation, configured, element, plan.InnerContext, options)) plan.AddBaseWhere(filter);
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
    /// 多对多时编号的那层 INNER JOIN 中间表，按中间表的外键分区，并把它带出来叫 <c>__group_keyN__</c>；
    /// 这时 Java 内层的实体列不写 AS、取段那层不再带出 <c>__row__</c>。
    /// </summary>
    private GroupJoinPlan WindowedPlan(NavigationMetadata navigation, Querying.QueryState? configured, GroupJoinWindow window, NavigationTableOptions options)
    {
        var element = options.TableFor(navigation.Target);
        var numbered = new SelectQuery(element);
        var link = navigation.Mapping is { } mapping ? JoinMapping(numbered, element, navigation, mapping) : null;
        var context = ForSubQuery(numbered);
        numbered.Where.AddRange(TargetFilters(navigation, configured, element, context, options));
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
        foreach (var c in columns) numbered.Columns.Add(new SelectColumn(element.Column(c), link is null ? c.Name : null));
        // 分区键：一对多是目标表的外键，多对多是中间表里对应所属表的列（带出来作分组键）
        var partition = link is null
            ? navigation.TargetColumns.Select(c => (SqlExpr)element.Column(c)).ToList()
            : navigation.SelfMappingColumns.Select(c => (SqlExpr)link.Column(c)).ToList();
        var groupKeys = link is null ? [] : partition.Select((_, i) => "__group_key" + (i + 1) + "__").ToList();
        for (var i = 0; i < groupKeys.Count; i++) numbered.Columns.Add(new SelectColumn(partition[i], groupKeys[i]));
        numbered.Columns.Add(new SelectColumn(WindowExpr.RowNumber(partition, orders), "__row__"));

        var numberedTable = new DerivedTableSource(numbered);
        var ranged = new SelectQuery(numberedTable);
        foreach (var c in columns) ranged.Columns.Add(new SelectColumn(new ColumnExpr(numberedTable, c.Name, c), c.Name));
        foreach (var key in groupKeys) ranged.Columns.Add(new SelectColumn(new ColumnExpr(numberedTable, key), key));
        var row = new ColumnExpr(numberedTable, "__row__");
        if (link is null) ranged.Columns.Add(new SelectColumn(row, "__row__"));
        ranged.Where.Add(new BinaryExpr(row, SqlBinaryOperator.GreaterThanOrEqual, new ParameterExpr(window.Skip + 1, typeof(long))));
        if (window.Take is { } take) ranged.Where.Add(new BinaryExpr(row, SqlBinaryOperator.LessThanOrEqual, new ParameterExpr(window.Skip + take, typeof(long))));

        var rangedTable = new DerivedTableSource(ranged);
        var plan = new GroupJoinPlan(new SelectQuery(rangedTable),
            new DerivedBinding(rangedTable, columns.ToDictionary(c => c.PropertyName, c => c.Name, StringComparer.Ordinal), navigation.Target),
            link is null
                ? navigation.TargetColumns.Select(c => new ColumnExpr(rangedTable, c.Name, c)).ToList()
                : groupKeys.Select(k => new ColumnExpr(rangedTable, k)).ToList());
        plan.InnerContext = ForSubQuery(plan.Inner, configured?.GroupJoins);
        return plan;
    }

    // 目标表自带的过滤（逻辑删除、拦截器）、导航的固定条件、GroupJoin(…, q => q.Where(…)) 加的条件
    private List<SqlExpr> TargetFilters(NavigationMetadata navigation, Querying.QueryState? configured, EntityTableSource element, TranslationContext context,
        NavigationTableOptions options)
    {
        var filters = TableFilters(element, !options.NoLogicDelete).ToList();
        if (navigation.ExtraFilter is { } extraFilter) filters.Add(context.PredicateOn(extraFilter, element));
        filters.AddRange(FiltersFor(navigation).Select(f => context.PredicateOn(f, element)));
        if (configured is null) return filters;
        var translator = new ExpressionTranslator([]) { Context = context };
        foreach (var where in configured.Wheres)
        {
            translator.Bind(where, [new EntityBinding(element)]);
            filters.Add(translator.TranslatePredicate(where.Body));
        }
        return filters;
    }

    /// <summary>整条查询翻译完后调用：按登记的聚合生成各分组派生表的列。</summary>
    public void CompleteGroupJoins()
    {
        foreach (var plan in _groupJoins.Values)
        {
            plan.Complete();
            // 整个集合分组时，所属查询里对关联列的条件下推进来（取一段行的分组先编号，不下推）
            if (plan.Element is EntityBinding) plan.PushDownKeyPredicates(query.Where);
            // 嵌套的分组合并（分组子查询里再合并的导航）在外层收尾之后：它要用外层分组子查询的 WHERE 下推
            plan.InnerContext.CompleteGroupJoins();
        }
    }
}
