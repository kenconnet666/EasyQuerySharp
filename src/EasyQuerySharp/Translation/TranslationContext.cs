using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 翻译一条 SELECT 时的共享状态：元数据、隐式 JOIN 的去处与缓存、每张表自带的过滤条件（逻辑删除、拦截器）。
/// 子查询用 <see cref="ForSubQuery"/> 派生自己的上下文（隐式 JOIN 加到子查询上），其余共享。
/// </summary>
internal sealed partial class TranslationContext(
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
