using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 翻译一条 SELECT 时的共享状态：元数据、隐式 JOIN 的去处与缓存、每张表自带的过滤条件（逻辑删除、拦截器）。
/// 子查询用 <see cref="ForSubQuery"/> 派生自己的上下文（隐式 JOIN 加到子查询上），其余共享。
/// </summary>
internal sealed class TranslationContext(
    MetadataRegistry metadata, Func<EntityTableSource, IEnumerable<SqlExpr>> tableFilters, SelectQuery query, TranslationContext? parent = null)
{
    private readonly Dictionary<(TableSource, string), EntityBinding> _joins = [];

    /// <summary>元数据。</summary>
    public MetadataRegistry Metadata => metadata;

    /// <summary>隐式 JOIN 加到的查询。</summary>
    public SelectQuery Query => query;

    /// <summary>某张实体表自带的过滤条件（逻辑删除、查询过滤拦截器）。</summary>
    public IEnumerable<SqlExpr> TableFilters(EntityTableSource table) => tableFilters(table);

    /// <summary>为子查询派生上下文：共享元数据与过滤规则，隐式 JOIN 加到 <paramref name="subQuery"/>。</summary>
    public TranslationContext ForSubQuery(SelectQuery subQuery) => new(metadata, tableFilters, subQuery, this) { Db = Db };

    /// <summary>所属的 EasyDb（分组合并的 Configure 要构造目标实体的查询状态）。</summary>
    public EasyDb? Db { get; init; }

    /// <summary>本查询第一个表来源上显式声明的分组合并（Query.GroupJoin / GroupJoinIf）。</summary>
    public IReadOnlyList<GroupJoinSpec> GroupJoinSpecs { get; init; } = [];

    private readonly Dictionary<(TableSource, string), GroupJoinPlan> _groupJoins = [];

    /// <summary>
    /// 这个集合导航是否合并成分组 LEFT JOIN：第一个表来源上用 GroupJoin 声明的、或导航上标了 GroupJoin 的（GroupJoinIf(false) 可关闭）。
    /// 是的话返回（必要时创建）它的合并计划，派生表 JOIN 到所属表所在的查询。
    /// </summary>
    public GroupJoinPlan? GroupJoinFor(SourceBinding owner, NavigationMetadata navigation)
    {
        if (owner is not EntityBinding entityOwner) return null;
        if (parent is not null && !Owns(owner.Table)) return parent.GroupJoinFor(owner, navigation);
        var spec = GroupJoinSpecs.LastOrDefault(s => s.Member == navigation.Property.Name && s.OwnerType.IsAssignableFrom(navigation.Self.Type));
        if (spec is { Enabled: false } || (spec is null && !navigation.GroupJoin)) return null;
        var key = (owner.Table, navigation.Property.Name);
        if (_groupJoins.TryGetValue(key, out var existing)) return existing;
        var element = new EntityTableSource(navigation.Target);
        var inner = new SelectQuery(element);
        IReadOnlyList<ColumnExpr> groupKeys;
        if (navigation.Mapping is { } mapping)
        {
            // 多对多：目标表 INNER JOIN 中间表，按中间表里对应所属表的列分组（与 Java 一致，ON 里目标列在前）
            var link = new EntityTableSource(mapping);
            var on = navigation.TargetColumns
                .Select((c, i) => (SqlExpr)new BinaryExpr(element.Column(c), SqlBinaryOperator.Equal, link.Column(navigation.TargetMappingColumns[i])))
                .ToList();
            inner.Joins.Add(new JoinClause(JoinKind.Inner, link, on.Count == 1 ? on[0] : LogicalExpr.Grouped(on)) { Implicit = true });
            groupKeys = navigation.SelfMappingColumns.Select(link.Column).ToList();
        }
        else groupKeys = navigation.TargetColumns.Select(element.Column).ToList();
        var plan = new GroupJoinPlan(inner, element, groupKeys);
        plan.InnerContext = ForSubQuery(plan.Inner);
        foreach (var filter in TableFilters(element)) plan.AddBaseWhere(filter);
        if (spec?.Configure is { } configure && Db is not null)
        {
            var configured = configure(new Querying.QueryState(Db, [new Querying.EntitySource(navigation.Target.Type)]));
            var translator = new ExpressionTranslator([]) { Context = plan.InnerContext };
            foreach (var where in configured.Wheres)
            {
                translator.Bind(where, [new EntityBinding(element)]);
                plan.AddBaseWhere(translator.TranslatePredicate(where.Body));
            }
        }
        // 派生表在 Java 里此时创建，别名排在已有的 JOIN（Configure 条件里的导航）之后、之后才加的表之前
        plan.Table.AliasAfterJoins = plan.Inner.Joins.Count;
        // 多对多时 Java 多建了一张中间表的副本，派生表的别名再跳过一个
        plan.Table.ReserveExtraAlias = navigation.Mapping is not null;
        var keys = plan.KeyColumns
            .Select((k, i) => (SqlExpr)new BinaryExpr(k, SqlBinaryOperator.Equal, entityOwner.OwnerColumn(navigation.SelfColumns[i])))
            .ToList();
        query.Joins.Add(new JoinClause(navigation.Required ? JoinKind.Inner : JoinKind.Left, plan.Table, keys.Count == 1 ? keys[0] : LogicalExpr.Grouped(keys)) { Implicit = true });
        _groupJoins[key] = plan;
        return plan;
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
        foreach (var plan in _groupJoins.Values) plan.Complete();
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
        if (navigation.ExtraFilter is { } extra)
        {
            var translator = new ExpressionTranslator([]) { Context = this };
            translator.Bind(extra, [new EntityBinding(target)]);
            conditions.Add(translator.TranslatePredicate(extra.Body));
        }
        query.Joins.Add(new JoinClause(navigation.Required ? JoinKind.Inner : JoinKind.Left, target, LogicalExpr.And(conditions)) { Implicit = true });
        var binding = new EntityBinding(target);
        _joins[key] = binding;
        return binding;
    }

    // 表来源是不是本查询的 FROM / JOIN（含已加上的隐式 JOIN）
    private bool Owns(TableSource table) => query.From == table || query.Joins.Any(j => j.Table == table);
}
