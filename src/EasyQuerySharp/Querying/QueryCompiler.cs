using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>翻译好的查询：SQL AST、结果读取函数、投影里的带出，以及调用方要求的关联键列序号。</summary>
internal sealed record CompiledQuery<T>(SelectQuery Sql, Func<DbDataReader, T> Shaper)
{
    /// <summary>投影里的导航带出。</summary>
    public IReadOnlyList<IncludePlan> Includes { get; init; } = [];

    /// <summary>关联键在结果里的列序号（作为带出的子查询时用）。</summary>
    public int[] RelationOrdinals { get; init; } = [];
}

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
    public static QueryFrame Build(QueryState state, IReadOnlyDictionary<ParameterExpression, SourceBinding>? outer = null, TranslationContext? outerContext = null)
    {
        var db = state.Db;
        var translator = new ExpressionTranslator(outer is null ? [] : new Dictionary<ParameterExpression, SourceBinding>(outer));
        var bindings = new List<SourceBinding>();
        SelectQuery? query = null;

        foreach (var source in state.Sources)
        {
            var binding = CreateBinding(db, source);
            bindings.Add(binding);
            var filters = Filters(db, state, binding, source.NoLogicDelete);

            if (query is null)
            {
                query = new SelectQuery(binding.Table);
                query.Where.AddRange(filters);
                translator.Context = new TranslationContext(db.Metadata, (table, logicDelete) => Filters(db, state, new EntityBinding(table), noLogicDelete: !logicDelete), query, outerContext)
                {
                    Db = db,
                    GroupJoinSpecs = state.GroupJoins,
                    GroupJoinAll = state.GroupJoinAll || outerContext?.GroupJoinAll == true,
                    MergeGroupFilters = !state.GroupJoinNoMerge && outerContext?.MergeGroupFilters != false,
                    NavigationValueFilter = state.NavigationValueFilter != Filtering.ValueFilter.All ? state.NavigationValueFilter : outerContext?.NavigationValueFilter ?? Filtering.ValueFilter.All,
                };
                continue;
            }

            // 被连接表自己的过滤条件（逻辑删除等）写在 ON 的最前面（与 Java 版一致）
            SqlExpr? on = null;
            if (source.On is not null)
            {
                translator.Bind(source.On, bindings);
                on = translator.TranslatePredicate(source.On.Body);
            }
            // 逗号连接没有 ON，表自己的过滤条件并入 WHERE
            if (source.Join == JoinKind.Comma)
            {
                query.Where.AddRange(filters);
                query.Joins.Add(new JoinClause(JoinKind.Comma, binding.Table, null));
                continue;
            }
            var conditions = filters.Concat(on is null ? [] : [on]).ToList();
            query.Joins.Add(new JoinClause(source.Join!.Value, binding.Table, conditions.Count == 0 ? null : LogicalExpr.And(conditions)));
        }

        // 外层传播下来的值过滤也作用于子查询自己的条件（本查询的条件在 Where 时已按自己的值过滤处理过）
        var propagated = outerContext?.NavigationValueFilter ?? Filtering.ValueFilter.All;
        foreach (var original in state.Wheres)
        {
            if (Filtering.ValueFilterRewriter.Apply(original, propagated) is not { } where) continue;
            translator.Bind(where, bindings);
            query!.Where.Add(translator.TranslatePredicate(where.Body));
        }
        // Java 按创建顺序编别名：之后（分组、排序、投影里）才加的隐式 JOIN 排在条件里的子查询之后
        if (translator.Context is { } context) context.WhereTranslated = true;
        if (state.GroupKey is { } groupKey) BuildGroup(query!, translator, bindings, groupKey, state);
        foreach (var order in state.OrderBys)
        {
            translator.Bind(order.Key, bindings);
            query!.OrderBy.Add(translator.OrderItem(order.Key.Body, order.Descending, order.Raw));
        }
        query!.Distinct = state.Distinct;
        query.Offset = state.Offset;
        query.Limit = state.Limit;
        query.Lock = state.Lock;
        translator.Context?.CompleteGroupJoins();
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
    public static CompiledQuery<T> Compile<T>(QueryState state, IReadOnlyList<ColumnMetadata>? relationKeys = null)
    {
        if (RawSelect(state) is { } raw)
        {
            // 标量（如 QuerySql<int>("select 1")）取第一列，其余按列名映射
            if (ScalarTypes.IsScalar(typeof(T)))
                return new CompiledQuery<T>(raw, r => Materialization.ValueReader.Read<T>(r, 0, null));
            if (typeof(T) == typeof(Dictionary<string, object?>) || typeof(T) == typeof(DynamicRow))
                return new CompiledQuery<T>(raw, r => (T)(object)Materialization.EntityMaterializer.ReadDictionary(r));
            var entity = state.Db.Metadata.Get<T>();
            return new CompiledQuery<T>(raw, r => Materialization.EntityMaterializer.ReadByName<T>(r, entity));
        }
        var (frame, projection) = Project(state, relationKeys: relationKeys);
        return new CompiledQuery<T>(frame.Query, projection.Compile<T>())
        {
            Includes = projection.Includes,
            RelationOrdinals = projection.RelationOrdinals,
        };
    }

    /// <summary>只翻译成 SQL AST，不编译读取函数（<c>ToSql</c> / <c>ToStatement</c> 用，编译读取函数是翻译里最贵的一步）。</summary>
    public static SelectQuery CompileSql(QueryState state) => RawSelect(state) ?? Project(state).Frame.Query;

    // 原生 SELECT 列表、或从原生 SQL 来源整体读取：列序号未知，读的时候按列名映射；不是这两种时返回 null
    private static SelectQuery? RawSelect(QueryState state)
    {
        var rawColumns = state.RawSelect ?? (state.Selector is null && state.Sources[0] is RawSqlSource ? "*" : null);
        if (rawColumns is null) return null;
        var frame = Build(state);
        frame.Query.Columns.Add(new SelectColumn(new LiteralExpr(rawColumns), null));
        return frame.Query;
    }

    /// <summary>翻译成带 SELECT 列表的 AST，不生成读取函数（用于子查询、派生表）。</summary>
    public static (SelectQuery Query, ProjectionCompiler Projection) CompileSelect(
        QueryState state, IReadOnlyDictionary<ParameterExpression, SourceBinding>? outer = null, TranslationContext? outerContext = null)
    {
        var (frame, projection) = Project(state, outer, outerContext: outerContext);
        return (frame.Query, projection);
    }

    private static (QueryFrame Frame, ProjectionCompiler Projection) Project(
        QueryState state, IReadOnlyDictionary<ParameterExpression, SourceBinding>? outer = null, IReadOnlyList<ColumnMetadata>? relationKeys = null,
        TranslationContext? outerContext = null)
    {
        var frame = Build(state, outer, outerContext);
        // 分组查询的投影总是写列别名（Java 版的 set / as 写法）
        var projection = new ProjectionCompiler(frame.Translator, state.Db.Options.NameConversion, alwaysAlias: state.GroupKey is not null, neverAlias: state.ImplicitSelector);
        LambdaExpression selector;
        if (state.Selector is not null) selector = state.Selector;
        else
        {
            var root = Expression.Parameter(state.ElementType, "root");
            selector = Expression.Lambda(root, root);
        }
        frame.Translator.Bind(selector, frame.Bindings);
        // 先登记列，再把列表交给 AST；读取函数由调用方按需编译
        projection.Prepare(selector.Body, frame.Bindings[0], relationKeys, state.Selector is null ? (IReadOnlyList<IncludeSpec>)state.Includes : null);
        frame.Query.Columns.AddRange(projection.Columns);
        // 分组合并的派生表要等整条查询（含投影）翻译完才知道全部聚合
        frame.Translator.Context?.CompleteGroupJoins();
        // 无实体的表（db.Table）单表选列时不加别名（与 Java 的 Map 查询一致）；联表、UNION 时照常编号
        frame.Query.ExplicitProjection = (state.Selector is not null && !state.ImplicitSelector && !state.IsDynamicTable) || state.ForceAlias;
        return (frame, projection);
    }

    private static SourceBinding CreateBinding(EasyDb db, QuerySource source)
    {
        switch (source)
        {
            case EntitySource e:
            {
                var table = new EntityTableSource(db.Metadata.Get(e.EntityType)) { ExplicitAlias = e.Alias, TableNameOverride = e.TableName, Segment = e.TableSegment };
                return new EntityBinding(table);
            }
            case SubQuerySource s:
            {
                var (inner, projection) = CompileSelect(s.Inner);
                var table = new DerivedTableSource(inner)
                {
                    ExplicitAlias = s.Alias,
                    // 投影过的查询作为 JOIN 来源：Java 内部多一层匿名表，别名跳过一个
                    ReservedAliases = s.Join is not null && (s.Inner.GroupKey is not null || s.Inner is { HasSelector: true, ImplicitSelector: false }) ? 1 : 0,
                };
                return new DerivedBinding(table, projection.MemberColumns, projection.Entity, projection.AliasedMembers, projection.NestedEntities);
            }
            case CteSource c:
            {
                var (inner, projection) = CompileSelect(c.Inner);
                var table = new CteTableSource(c.Name, inner) { ExplicitAlias = c.Alias, ReserveExtraAlias = c.Join is not null && c.Inner.GroupKey is not null };
                return new DerivedBinding(table, projection.MemberColumns, projection.Entity, projection.AliasedMembers, projection.NestedEntities);
            }
            case UnionSource u:
            {
                var parts = u.Parts.Select(p => CompileSelect(p)).ToList();
                var table = new UnionTableSource(parts.Select(p => p.Query).ToList(), u.All) { ExplicitAlias = u.Alias, IndependentAliases = u.IndependentAliases };
                // 外层按第一条查询的投影访问列（与 SQL 的 UNION 语义一致：列名取第一个分支）
                return new DerivedBinding(table, parts[0].Projection.MemberColumns, parts[0].Projection.Entity, parts[0].Projection.AliasedMembers, parts[0].Projection.NestedEntities);
            }
            case RawSqlSource r:
            {
                var entity = db.Metadata.Get(r.EntityType);
                var table = new RawTableSource(r.Sql, r.Parameters) { ExplicitAlias = r.Alias };
                return new DerivedBinding(table, entity.Columns.ToDictionary(c => c.PropertyName, c => c.Name), entity);
            }
            default:
                throw new NotSupportedException(source.GetType().Name);
        }
    }

    /// <summary>某个表来源自带的过滤条件：逻辑删除，以及拦截器给出的条件。</summary>
    private static List<SqlExpr> Filters(EasyDb db, QueryState state, SourceBinding binding, bool noLogicDelete = false)
    {
        var logicDelete = !state.DisableLogicDelete && !noLogicDelete;
        return binding switch
        {
            EntityBinding { EntityTable: var table } => TableFilters(db, table, logicDelete, state.Interceptors),
            // 整个实体的 CTE 引用：Java 把它当实体表，同样加逻辑删除条件
            DerivedBinding { Table: CteTableSource cte, Entity: { LogicDeleteColumn: { } column } entity } when logicDelete =>
                [LogicDeleteStrategies.Resolve(db.Options, entity).NotDeleted(new ColumnExpr(cte, column.Name, column))],
            _ => [],
        };
    }

    /// <summary>一张实体表自带的条件：逻辑删除（<paramref name="logicDelete"/> 为 true 时），再是拦截器的过滤条件。查询与写入共用。</summary>
    internal static List<SqlExpr> TableFilters(EasyDb db, EntityTableSource table, bool logicDelete, Interceptors.InterceptorSelection interceptors)
    {
        var result = new List<SqlExpr>();
        if (logicDelete && LogicDeleteFilter(db, table) is { } filter) result.Add(filter);
        result.AddRange(db.Interceptors.QueryFilters(table, interceptors));
        return result;
    }

    /// <summary>逻辑删除的"未删除"条件；实体没有逻辑删除列时为 null。</summary>
    internal static SqlExpr? LogicDeleteFilter(EasyDb db, EntityTableSource table) =>
        table.Entity.LogicDeleteColumn is { } column
            ? LogicDeleteStrategies.Resolve(db.Options, table.Entity).NotDeleted(table.Column(column))
            : null;
}

/// <summary>翻译到 SELECT 列表之前的中间结果。</summary>
internal sealed record QueryFrame(SelectQuery Query, ExpressionTranslator Translator, List<SourceBinding> Bindings);
