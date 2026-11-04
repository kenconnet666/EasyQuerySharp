using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>翻译好的查询：SQL AST 与结果读取函数。</summary>
internal sealed record CompiledQuery<T>(SelectQuery Sql, Func<DbDataReader, T> Shaper);

/// <summary>
/// 把 <see cref="QueryState"/> 翻译成 SQL AST：建立表来源与作用域、加上逻辑删除与拦截器的条件、翻译条件 / 排序 / 投影。
/// 列表由调用方决定（实体、投影，或 COUNT / SUM 等聚合），所以分成 <see cref="Build"/> 与各终结方法。
/// </summary>
internal static class QueryCompiler
{
    /// <summary>
    /// 翻译 FROM / JOIN / WHERE / ORDER BY / 分页，不含 SELECT 列表。
    /// <paramref name="outer"/> 是外层查询的作用域：关联子查询里引用的外层参数由它解析。
    /// </summary>
    public static QueryFrame Build(QueryState state, IReadOnlyDictionary<ParameterExpression, SourceBinding>? outer = null)
    {
        var db = state.Db;
        var translator = new ExpressionTranslator(outer is null ? [] : new Dictionary<ParameterExpression, SourceBinding>(outer));
        var bindings = new List<SourceBinding>();
        SelectQuery? query = null;

        foreach (var source in state.Sources)
        {
            var binding = CreateBinding(db, source);
            bindings.Add(binding);
            var filters = Filters(db, state, binding);

            if (query is null)
            {
                query = new SelectQuery(binding.Table);
                query.Where.AddRange(filters);
                continue;
            }

            // 被连接表自己的过滤条件（逻辑删除等）写在 ON 的最前面（与 Java 版一致）
            SqlExpr? on = null;
            if (source.On is not null)
            {
                translator.Bind(source.On, bindings);
                on = translator.TranslatePredicate(source.On.Body);
            }
            var conditions = filters.Concat(on is null ? [] : [on]).ToList();
            query.Joins.Add(new JoinClause(source.Join!.Value, binding.Table, conditions.Count == 0 ? null : LogicalExpr.And(conditions)));
        }

        foreach (var where in state.Wheres)
        {
            translator.Bind(where, bindings);
            query!.Where.Add(translator.TranslatePredicate(where.Body));
        }
        if (state.GroupKey is { } groupKey) BuildGroup(query!, translator, bindings, groupKey, state);
        foreach (var order in state.OrderBys)
        {
            translator.Bind(order.Key, bindings);
            query!.OrderBy.Add(new OrderByItem(translator.Translate(order.Key.Body), order.Descending));
        }
        query!.Distinct = state.Distinct;
        query.Offset = state.Offset;
        query.Limit = state.Limit;
        return new QueryFrame(query, translator, bindings);
    }

    /// <summary>
    /// GROUP BY 与 HAVING：键是单个表达式或匿名类型（多列）；之后参数为 IGroup 的 lambda（Having / OrderBy / Select）绑定到分组。
    /// </summary>
    private static void BuildGroup(SelectQuery query, ExpressionTranslator translator, List<SourceBinding> bindings, LambdaExpression groupKey, QueryState state)
    {
        translator.Bind(groupKey, bindings);
        var body = ExpressionTranslator.StripConvert(groupKey.Body);
        List<SqlExpr> keys;
        List<string>? members = null;
        if (body is NewExpression { Members: not null } n)
        {
            keys = n.Arguments.Select(translator.Translate).ToList();
            members = n.Members.Select(m => m.Name).ToList();
        }
        else keys = [translator.Translate(groupKey.Body)];
        query.GroupBy.AddRange(keys);

        var element = bindings.Count == 1 ? bindings[0] : new TupleBinding(bindings);
        translator.Group = new GroupBinding(keys, members, element);
        foreach (var having in state.Havings)
        {
            translator.Bind(having, bindings);
            query.Having.Add(translator.TranslatePredicate(having.Body));
        }
    }

    /// <summary>完整翻译：按投影（没有时投影到第一个来源的实体）填入 SELECT 列表并生成读取函数。</summary>
    public static CompiledQuery<T> Compile<T>(QueryState state)
    {
        // 原生 SELECT 列表、或从原生 SQL 来源整体读取：列序号未知，按列名映射到实体
        var rawColumns = state.RawSelect ?? (state.Selector is null && state.Sources[0] is RawSqlSource ? "*" : null);
        if (rawColumns is not null)
        {
            var rawFrame = Build(state);
            rawFrame.Query.Columns.Add(new SelectColumn(new LiteralExpr(rawColumns), null));
            var entity = state.Db.Metadata.Get<T>();
            return new CompiledQuery<T>(rawFrame.Query, r => Materialization.EntityMaterializer.ReadByName<T>(r, entity));
        }
        var (frame, projection) = Project(state);
        return new CompiledQuery<T>(frame.Query, projection.Compile<T>());
    }

    /// <summary>翻译成带 SELECT 列表的 AST，不生成读取函数（用于子查询、派生表）。</summary>
    public static (SelectQuery Query, ProjectionCompiler Projection) CompileSelect(
        QueryState state, IReadOnlyDictionary<ParameterExpression, SourceBinding>? outer = null)
    {
        var (frame, projection) = Project(state, outer);
        return (frame.Query, projection);
    }

    private static (QueryFrame Frame, ProjectionCompiler Projection) Project(
        QueryState state, IReadOnlyDictionary<ParameterExpression, SourceBinding>? outer = null)
    {
        var frame = Build(state, outer);
        // 分组查询的投影总是写列别名（Java 版的 set / as 写法）
        var projection = new ProjectionCompiler(frame.Translator, state.Db.Options.NameConversion, alwaysAlias: state.GroupKey is not null);
        LambdaExpression selector;
        if (state.Selector is not null) selector = state.Selector;
        else
        {
            var root = Expression.Parameter(state.ElementType, "root");
            selector = Expression.Lambda(root, root);
        }
        frame.Translator.Bind(selector, frame.Bindings);
        // 先登记列，再把列表交给 AST；读取函数由调用方按需编译
        projection.Prepare(selector.Body);
        frame.Query.Columns.AddRange(projection.Columns);
        frame.Query.ExplicitProjection = state.Selector is not null;
        return (frame, projection);
    }

    private static SourceBinding CreateBinding(EasyDb db, QuerySource source)
    {
        switch (source)
        {
            case EntitySource e:
            {
                var table = new EntityTableSource(db.Metadata.Get(e.EntityType)) { ExplicitAlias = e.Alias, TableNameOverride = e.TableName };
                return new EntityBinding(table);
            }
            case SubQuerySource s:
            {
                var (inner, projection) = CompileSelect(s.Inner);
                var table = new DerivedTableSource(inner)
                {
                    ExplicitAlias = s.Alias,
                    ReserveExtraAlias = s.Join is not null && s.Inner.GroupKey is not null,
                };
                return new DerivedBinding(table, projection.MemberColumns, projection.Entity);
            }
            case RawSqlSource r:
            {
                var entity = db.Metadata.Get(r.EntityType);
                var table = new RawTableSource(r.Sql) { ExplicitAlias = r.Alias };
                return new DerivedBinding(table, entity.Columns.ToDictionary(c => c.PropertyName, c => c.Name), entity);
            }
            default:
                throw new NotSupportedException(source.GetType().Name);
        }
    }

    /// <summary>某个表来源自带的过滤条件：逻辑删除，以及拦截器给出的条件。</summary>
    private static List<SqlExpr> Filters(EasyDb db, QueryState state, SourceBinding binding)
    {
        var result = new List<SqlExpr>();
        if (binding is not EntityBinding { EntityTable: var table }) return result;
        if (!state.DisableLogicDelete && LogicDeleteFilter(table) is { } logicDelete) result.Add(logicDelete);
        result.AddRange(db.Interceptors.QueryFilters(table, state.DisabledInterceptors));
        return result;
    }

    /// <summary>逻辑删除的"未删除"条件；实体没有逻辑删除列时为 null。</summary>
    internal static SqlExpr? LogicDeleteFilter(EntityTableSource table)
    {
        var entity = table.Entity;
        if (entity.LogicDeleteColumn is not { } column) return null;
        return entity.LogicDelete!.Strategy switch
        {
            LogicDeleteStrategy.Boolean => new BinaryExpr(table.Column(column), SqlBinaryOperator.Equal, new ParameterExpr(false, typeof(bool))),
            LogicDeleteStrategy.DeletedTime => new IsNullExpr(table.Column(column), negated: false),
            _ => throw new NotSupportedException($"逻辑删除策略 {entity.LogicDelete.Strategy} 需要注册自定义策略"),
        };
    }
}

/// <summary>翻译到 SELECT 列表之前的中间结果。</summary>
internal sealed record QueryFrame(SelectQuery Query, ExpressionTranslator Translator, List<SourceBinding> Bindings);
