using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 翻译一条 SELECT 时的共享状态：元数据、隐式 JOIN 的去处与缓存、每张表自带的过滤条件（逻辑删除、拦截器）。
/// 子查询用 <see cref="ForSubQuery"/> 派生自己的上下文（隐式 JOIN 加到子查询上），其余共享。
/// </summary>
internal sealed class TranslationContext(MetadataRegistry metadata, Func<EntityTableSource, IEnumerable<SqlExpr>> tableFilters, SelectQuery query)
{
    private readonly Dictionary<(TableSource, string), EntityBinding> _joins = [];

    /// <summary>元数据。</summary>
    public MetadataRegistry Metadata => metadata;

    /// <summary>隐式 JOIN 加到的查询。</summary>
    public SelectQuery Query => query;

    /// <summary>某张实体表自带的过滤条件（逻辑删除、查询过滤拦截器）。</summary>
    public IEnumerable<SqlExpr> TableFilters(EntityTableSource table) => tableFilters(table);

    /// <summary>为子查询派生上下文：共享元数据与过滤规则，隐式 JOIN 加到 <paramref name="subQuery"/>。</summary>
    public TranslationContext ForSubQuery(SelectQuery subQuery) => new(metadata, tableFilters, subQuery);

    /// <summary>
    /// 对一导航的隐式 JOIN：同一个来源上的同一个导航只连一次。ON 里先写目标表自带的过滤条件，再写 <c>目标键 = 本表键</c>（与 Java 版一致）；
    /// <see cref="NavigationMetadata.Required"/> 时用 INNER JOIN，否则 LEFT JOIN。
    /// </summary>
    public EntityBinding JoinFor(EntityBinding owner, NavigationMetadata navigation)
    {
        var key = (owner.Table, navigation.Property.Name);
        if (_joins.TryGetValue(key, out var existing)) return existing;

        var target = new EntityTableSource(navigation.Target);
        var conditions = new List<SqlExpr>(TableFilters(target));
        for (var i = 0; i < navigation.SelfColumns.Count; i++)
            conditions.Add(new BinaryExpr(target.Column(navigation.TargetColumns[i]), SqlBinaryOperator.Equal, owner.EntityTable.Column(navigation.SelfColumns[i])));
        if (navigation.ExtraFilter is { } extra)
        {
            var translator = new ExpressionTranslator([]) { Context = this };
            translator.Bind(extra, [new EntityBinding(target)]);
            conditions.Add(translator.TranslatePredicate(extra.Body));
        }
        query.Joins.Add(new JoinClause(navigation.Required ? JoinKind.Inner : JoinKind.Left, target, LogicalExpr.And(conditions)));
        var binding = new EntityBinding(target);
        _joins[key] = binding;
        return binding;
    }
}
