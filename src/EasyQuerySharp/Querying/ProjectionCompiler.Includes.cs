using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

// 投影里的带出：集合导航、对一导航整个对象、平铺值与经中间导航的路径，登记成 IncludePlan，主查询读完后由 IncludeRunner 执行
internal sealed partial class ProjectionCompiler
{
    private static readonly MethodInfo RegisterMethod = typeof(IncludePlan).GetMethod(nameof(IncludePlan.Register), [typeof(DbDataReader), typeof(Action<object?>)])!;
    private static readonly MethodInfo RegisterOwnedMethod = typeof(IncludePlan).GetMethod(nameof(IncludePlan.Register), [typeof(DbDataReader), typeof(Action<object?>), typeof(object)])!;

    /// <summary>
    /// 一个成员的值：集合导航（<c>u.Cards</c>、<c>u.Cards.Where(…).Select(…).ToList()</c>）是带出，先放一个空 List，主查询后填充；
    /// 整个对一导航对象（<c>c.Bank</c>）也是带出（单独的 IN 查询），构造完父对象后回填；其余照常翻译。
    /// </summary>
    private Expression ShapeMember(Expression value, MemberInfo? member, List<(IncludePlan Plan, MemberInfo Member, int? FlatIndex)> toOne)
    {
        // 平铺的对一路径：同一导航共用一条带出查询，父属性回填时取自己那一项
        if (member is not null && TryFlat(value) is var (flatPlan, index))
        {
            toOne.Add((flatPlan, member, index));
            return Expression.Default(value.Type);
        }
        // 经过集合的路径：同一条路径上的几个属性共用一组带出查询，各自从每个子对象取自己那一项
        if (member is not null && TryFlatPath(value) is var (pathPlan, pathIndex, element))
            return Expression.Call(FlatListMethod.MakeGenericMethod(element), Expression.Constant(pathPlan), _reader, Expression.Constant(pathIndex));
        if (member is not null && TryInclude(value) is { } plan)
        {
            if (plan.IsCollection)
                return Expression.Call(Expression.Constant(plan), RegisterMethod, _reader,
                    Expression.Constant(null, typeof(Action<object?>)));
            toOne.Add((plan, member, null));
            return Expression.Default(value.Type);
        }
        return Shape(value, member, false);
    }

    // AutoIncludeMarkers.Flat(导航, 路径)：按（所属来源, 导航）找共用的平铺带出计划，登记这条路径，返回它在值数组里的位置
    private (IncludePlan Plan, int Index)? TryFlat(Expression value)
    {
        if (ExpressionTranslator.StripConvert(value) is not MethodCallExpression { Method.Name: nameof(AutoIncludeMarkers.Flat) } marker
            || marker.Method.DeclaringType != typeof(AutoIncludeMarkers))
            return null;
        if (marker.Arguments[0] is not MemberExpression { Expression: { } owner } nav || translator.BindingOf(owner) is not { OwnerEntity: not null } navOwner
            || translator.FindNavigation(navOwner, nav.Member.Name) is not { IsCollection: false } navigation)
            throw new NotSupportedException($"平铺的路径只能从实体的对一导航出发：{marker.Arguments[0]}");
        var path = (LambdaExpression)((ConstantExpression)marker.Arguments[1]).Value!;
        if (!_flatPlans.TryGetValue((navOwner, navigation), out var plan))
        {
            plan = new IncludePlan(navigation, typeof(object?[]), null, [], isCollection: false) { FlatPaths = [], Configure = Merged(navOwner, nav.Member.Name, null) };
            _flatPlans[(navOwner, navigation)] = plan;
            _includes.Add((plan, navOwner));
        }
        plan.FlatPaths!.Add(path);
        return (plan, plan.FlatPaths.Count - 1);
    }

    // AutoIncludeMarkers.Through(第一个导航, 后续导航名, 配置, 投影) 经过集合、取的是目标实体本身或标量（Java 的 @NavigateFlat 列表）：
    // 按（所属来源, 路径）找共用的平铺计划，登记这一项；投影成 DTO、带配置的照常各自带出
    private (IncludePlan Plan, int Index, Type Element)? TryFlatPath(Expression value)
    {
        if (ExpressionTranslator.StripConvert(value) is not MethodCallExpression { Method.Name: nameof(AutoIncludeMarkers.Through) } marker
            || marker.Method.DeclaringType != typeof(AutoIncludeMarkers) || ((ConstantExpression)marker.Arguments[2]).Value is not null)
            return null;
        if (MetadataRegistry.ElementType(marker.Type) is var element && element == marker.Type) return null;
        if (marker.Arguments[0] is not MemberExpression { Expression: { } owner } first || translator.BindingOf(owner) is not { OwnerEntity: not null } navOwner
            || translator.FindNavigation(navOwner, first.Member.Name) is not { } navigation)
            return null;
        var names = (string[])((ConstantExpression)marker.Arguments[1]).Value!;
        var via = new List<NavigationMetadata>();
        foreach (var name in names)
        {
            via.Add(navigation);
            navigation = translator.Context!.Metadata.FindNavigation(navigation.Target, name) ?? throw new NotSupportedException($"{navigation.Target.Type.Name}.{name} 不是导航属性");
        }
        if (!navigation.IsCollection && !via.Any(v => v.IsCollection)) return null;

        LambdaExpression path;
        if ((LambdaExpression?)((ConstantExpression)marker.Arguments[3]).Value is { } selector)
        {
            if (!ScalarTypes.IsScalar(selector.ReturnType)) return null;
            path = selector;
        }
        else
        {
            var e = Expression.Parameter(navigation.Target.Type, "e");
            path = Expression.Lambda(e, e);
        }
        // 只有实体本身与它的列的一组可以改成取实体；带 JOIN 的路径（e.Class.Name）单独一组
        var simple = path.Body == path.Parameters[0] || path.Body is MemberExpression { Expression: ParameterExpression };
        var key = (navOwner, first.Member.Name + "." + string.Join('.', names), simple);
        if (!_pathPlans.TryGetValue(key, out var plan))
        {
            plan = new IncludePlan(navigation, typeof(object?[]), null, [], isCollection: true) { FlatPaths = [], AllowEntityRows = simple, Via = via };
            _pathPlans[key] = plan;
            _includes.Add((plan, navOwner));
        }
        plan.FlatPaths!.Add(path);
        return (plan, plan.FlatPaths.Count - 1, element);
    }

    private static readonly MethodInfo FlatListMethod = typeof(ProjectionCompiler).GetMethod(nameof(FlatList))!;

    /// <summary>经过集合的平铺属性的值：登记父键，子对象读回后取第 <paramref name="index"/> 项放进列表。</summary>
    public static List<T> FlatList<T>(IncludePlan plan, DbDataReader reader, int index)
    {
        var list = new List<T>();
        plan.Register(reader, child => list.Add((T)ValueReader.ConvertTo(plan.FlatItem(child, index), typeof(T))!));
        return list;
    }

    /// <summary>
    /// 平铺带出的值数组里第 <paramref name="index"/> 项，转成父属性的类型；没有带出到（父键为空、子数据不存在）时为默认值。
    /// 父属性上声明了值转换器（<c>[Column(Converter = …)]</c>，Java 平铺属性上的 conversion）时读出后再转换；父属性是列表时放进只有这一项的列表。
    /// </summary>
    public static object? FlatValue(object? row, int index, Type type, IValueConverter? converter)
    {
        if (row is not object?[] values) return null;
        var value = converter is null ? values[index] : converter.FromProvider(values[index]);
        if (ScalarTypes.IsScalar(type) || type.IsInstanceOfType(value) || MetadataRegistry.ElementType(type) is var item && item == type) return ValueReader.ConvertTo(value, type);
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
        if (value is not null) list.Add(ValueReader.ConvertTo(value, item));
        return list;
    }

    // DTO 属性上声明的值转换器（同一类型共用一个实例）
    private static IValueConverter? DtoConverter(PropertyInfo property) =>
        property.GetCustomAttribute<ColumnAttribute>()?.Converter is { } type ? ConverterInstances.GetOrAdd(type, t => (IValueConverter)Activator.CreateInstance(t)!) : null;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IValueConverter> ConverterInstances = new();

    private IncludePlan? TryInclude(Expression value)
    {
        var unaliased = false;
        if (ExpressionTranslator.StripConvert(value) is MethodCallExpression { Method.Name: nameof(AutoIncludeMarkers.Loaded) } loaded && loaded.Method.DeclaringType == typeof(AutoIncludeMarkers))
        {
            value = loaded.Arguments[0];
            unaliased = true;
        }
        // 自动映射（Select<TDto>()）产出的带出标记：导航、带出查询的配置、子对象的投影都在参数里
        if (ExpressionTranslator.StripConvert(value) is MethodCallExpression marker && marker.Method.DeclaringType == typeof(AutoIncludeMarkers))
        {
            if (marker.Arguments[0] is not MemberExpression { Expression: { } markerOwner } markerNav || translator.BindingOf(markerOwner) is not { OwnerEntity: not null } navOwner
                || translator.FindNavigation(navOwner, markerNav.Member.Name) is not { } navigation)
                throw new NotSupportedException($"自动映射的带出只能从实体的导航出发：{marker.Arguments[0]}");
            // Through(第一个导航, 后续导航名, 配置, 投影)：经过的导航记在 Via 里，带出的是最后一个导航
            var through = marker.Method.Name == nameof(AutoIncludeMarkers.Through);
            var via = new List<NavigationMetadata>();
            if (through)
                foreach (var name in (string[])((ConstantExpression)marker.Arguments[1]).Value!)
                {
                    via.Add(navigation);
                    navigation = translator.Context!.Metadata.FindNavigation(navigation.Target, name)
                                 ?? throw new NotSupportedException($"{navigation.Target.Type.Name}.{name} 不是导航属性");
                }
            var offset = through ? 1 : 0;
            var childSelector = (LambdaExpression?)((ConstantExpression)marker.Arguments[2 + offset]).Value;
            var isCollection = navigation.IsCollection || via.Any(v => v.IsCollection);
            var configure = (Func<QueryState, QueryState>?)((ConstantExpression)marker.Arguments[1 + offset]).Value;
            if (!through) configure = Merged(navOwner, markerNav.Member.Name, configure);
            // 直达导航（DirectMapping）展开成沿路径的各段，与 Include 一致；否则会拿本表的键去查目标表
            var direct = !through && navigation.DirectPath.Count > 0;
            if (direct)
            {
                var chain = IncludePlan.DirectChain(translator.Context!.Metadata, navigation);
                via.AddRange(chain[..^1]);
                navigation = chain[^1];
            }
            var markerPlan = new IncludePlan(navigation, childSelector?.ReturnType ?? navigation.Target.Type, childSelector, [], isCollection)
            {
                Configure = configure,
                Via = via,
                DirectVia = direct,
            };
            _includes.Add((markerPlan, navOwner));
            return markerPlan;
        }
        var inner = value;
        if (inner is MethodCallExpression { Method.Name: nameof(Enumerable.ToList) or nameof(Enumerable.ToArray) } materialize
            && materialize.Method.DeclaringType == typeof(Enumerable))
            inner = materialize.Arguments[0];

        // 从外往里拆：最外层可以是 Select，里面是 Where 与排序；排序在子查询上执行（按书写顺序）。
        // Skip / Take 写在 Where、排序之后（Select 前后都行），带出时每个父对象各取一段（与 Include 里的 Take 相同）
        LambdaExpression? selector = null;
        var filters = new List<LambdaExpression>();
        var orders = new List<OrderSpec>();
        var paging = new List<(bool Skip, long Count)>();
        while (inner is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Arguments.Count == 2
               && mc.Method.Name is nameof(Enumerable.Select) or nameof(Enumerable.Where) or nameof(Enumerable.OrderBy) or nameof(Enumerable.OrderByDescending)
                   or nameof(Enumerable.ThenBy) or nameof(Enumerable.ThenByDescending) or nameof(Enumerable.Skip) or nameof(Enumerable.Take))
        {
            if (mc.Method.Name is nameof(Enumerable.Skip) or nameof(Enumerable.Take))
            {
                if (mc.Arguments[1].Type != typeof(int)) return null;
                if (filters.Count > 0 || orders.Count > 0)
                    throw new NotSupportedException($"投影里的集合先 Skip / Take 再 Where / 排序不支持，把 Skip / Take 写在 Where、OrderBy 之后：{value}");
                paging.Insert(0, (mc.Method.Name == nameof(Enumerable.Skip), PagingCount(mc)));
                inner = mc.Arguments[0];
                continue;
            }
            var lambda = (LambdaExpression)ExpressionTranslator.StripConvert(mc.Arguments[1]);
            switch (mc.Method.Name)
            {
                case nameof(Enumerable.Select):
                    if (selector is not null || filters.Count > 0 || orders.Count > 0) return null;
                    selector = lambda;
                    break;
                case nameof(Enumerable.Where):
                    filters.Insert(0, lambda);
                    break;
                default:
                    orders.Insert(0, new OrderSpec(lambda, mc.Method.Name.EndsWith("Descending", StringComparison.Ordinal)));
                    break;
            }
            inner = mc.Arguments[0];
        }

        // 所属的可以是实体表，也可以是按实体读的派生表（父集合取了段时，子对象的投影从取好段的派生表取列）
        if (inner is not MemberExpression { Expression: { } ownerExpr } navMember || translator.BindingOf(ownerExpr) is not { OwnerEntity: not null } owner) return null;
        if (translator.FindNavigation(owner, navMember.Member.Name) is not { } nav) return null;
        if (!nav.IsCollection && (selector is not null || filters.Count > 0 || orders.Count > 0 || paging.Count > 0 || inner != value)) return null;
        if (nav.DirectPath.Count > 0)
        {
            var direct = IncludePlan.ForNavigation(translator.Context!.Metadata, nav, Merged(owner, nav.Property.Name, null), unaliased);
            _includes.Add((direct, owner));
            return direct;
        }

        var (skip, take) = Range(paging);
        var plan = new IncludePlan(nav, selector?.ReturnType ?? nav.Target.Type, selector, filters, nav.IsCollection)
        {
            Configure = Merged(owner, nav.Property.Name, orders.Count == 0 && paging.Count == 0 ? null
                : state => state with { OrderBys = state.OrderBys.AddRange(orders), Offset = skip ?? state.Offset, Limit = take ?? state.Limit }),
            Unaliased = unaliased,
        };
        _includes.Add((plan, owner));
        return plan;
    }

    // Skip / Take 的条数在客户端求值（常量或捕获的变量），不能引用投影里的行
    private static long PagingCount(MethodCallExpression call) =>
        ClientEvaluator.MustTranslate(call.Arguments[1])
            ? throw new NotSupportedException($"投影里集合的 {call.Method.Name} 只能用常量或变量作条数，不能引用查询里的列：{call}")
            : Convert.ToInt64(ClientEvaluator.Evaluate(call.Arguments[1]), System.Globalization.CultureInfo.InvariantCulture);

    // 依次作用的 Skip / Take 合成一段：跳过几条、最多取几条（Take(5).Skip(2) 是跳过 2 条取 3 条）
    private static (long? Offset, long? Limit) Range(List<(bool Skip, long Count)> paging)
    {
        if (paging.Count == 0) return (null, null);
        long offset = 0;
        long? limit = null;
        foreach (var (skip, count) in paging)
        {
            if (skip)
            {
                offset += Math.Max(count, 0);
                if (limit is { } l) limit = Math.Max(l - Math.Max(count, 0), 0);
            }
            else limit = limit is { } l ? Math.Min(l, Math.Max(count, 0)) : Math.Max(count, 0);
        }
        return (offset == 0 ? null : offset, limit);
    }

    // Include(导航, 配置) 之后又投影：根实体上同名导航的带出先按投影自带的配置，再按 Include 的（调用处在后）
    private Func<QueryState, QueryState>? Merged(SourceBinding owner, string navigation, Func<QueryState, QueryState>? own) =>
        owner == _root && _includeConfigs.TryGetValue(navigation, out var configure)
            ? own is null ? configure : state => configure(own(state))
            : own;

    // 实体查询的 Include：实体读出后，集合导航先放一个空 List（主查询后填充），对一导航登记回填函数
    // 实体可以来自实体表，也可以来自按实体读的派生表（带出的子数据按父对象取了段时，再往下的 Include）
    private Expression WithIncludes(Expression entity, (SourceBinding Binding, EntityMetadata Entity) owner, IReadOnlyList<IncludeSpec> includes)
    {
        var instance = Expression.Variable(entity.Type, "entity");
        var body = new List<Expression> { Expression.Assign(instance, entity) };
        foreach (var spec in includes)
        {
            var nav = translator.FindNavigation(owner.Binding, spec.Member) ?? throw new NotSupportedException($"{owner.Entity.Type.Name}.{spec.Member} 不是导航属性");
            var plan = IncludePlan.ForNavigation(translator.Context!.Metadata, nav, spec.Configure);
            _includes.Add((plan, owner.Binding));
            var property = owner.Entity.Type.GetProperty(spec.Member)!;
            // 带上父实体：追踪查询据此登记"已加载的导航"（聚合保存只处理加载过的导航，见 0004）
            var register = RegisterOwnedMethod;
            var owned = Expression.Convert(instance, typeof(object));
            if (nav.IsCollection)
            {
                body.Add(Expression.Assign(Expression.Property(instance, property),
                    Expression.Convert(Expression.Call(Expression.Constant(plan), register, _reader, Expression.Constant(null, typeof(Action<object?>)), owned), property.PropertyType)));
                continue;
            }
            var child = Expression.Parameter(typeof(object), "child");
            var assign = Expression.Lambda<Action<object?>>(Expression.Assign(Expression.Property(instance, property), Expression.Convert(child, property.PropertyType)), child);
            body.Add(Expression.Call(Expression.Constant(plan), register, _reader, assign, owned));
        }
        body.Add(instance);
        return Expression.Block([instance], body);
    }

    // 对一带出：父对象构造完后登记回填函数（构造函数参数 / init 属性都能通过反射赋值）
    private Expression RegisterToOne(Expression created, List<(IncludePlan Plan, MemberInfo Member, int? FlatIndex)> toOne)
    {
        if (toOne.Count == 0) return created;
        var instance = Expression.Variable(created.Type, "parent");
        var body = new List<Expression> { Expression.Assign(instance, created) };
        foreach (var (plan, member, flatIndex) in toOne)
        {
            var property = member as PropertyInfo ?? created.Type.GetProperty(member.Name)
                           ?? throw new NotSupportedException($"{created.Type.Name}.{member.Name} 不是可回填的属性");
            var value = Expression.Parameter(typeof(object), "child");
            Expression assigned = flatIndex is { } index
                ? Expression.Call(typeof(ProjectionCompiler).GetMethod(nameof(FlatValue))!, value, Expression.Constant(index), Expression.Constant(property.PropertyType),
                    Expression.Constant(DtoConverter(property), typeof(IValueConverter)))
                : value;
            var assign = Expression.Lambda<Action<object?>>(
                Expression.Call(Expression.Constant(property), typeof(PropertyInfo).GetMethod(nameof(PropertyInfo.SetValue), [typeof(object), typeof(object)])!,
                    Expression.Convert(instance, typeof(object)), assigned),
                value);
            body.Add(Expression.Call(Expression.Constant(plan), RegisterMethod, _reader, assign));
        }
        body.Add(instance);
        return Expression.Block([instance], body);
    }
}
