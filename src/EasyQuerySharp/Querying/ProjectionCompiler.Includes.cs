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
            plan = new IncludePlan(navigation, typeof(object?[]), null, [], isCollection: false) { FlatPaths = [] };
            _flatPlans[(navOwner, navigation)] = plan;
            _includes.Add((plan, navOwner));
        }
        plan.FlatPaths!.Add(path);
        return (plan, plan.FlatPaths.Count - 1);
    }

    /// <summary>
    /// 平铺带出的值数组里第 <paramref name="index"/> 项，转成父属性的类型；没有带出到（父键为空、子数据不存在）时为默认值。
    /// 父属性上声明了值转换器（<c>[Column(Converter = …)]</c>，Java 平铺属性上的 conversion）时读出后再转换。
    /// </summary>
    public static object? FlatValue(object? row, int index, Type type, IValueConverter? converter) =>
        row is object?[] values ? ValueReader.ConvertTo(converter is null ? values[index] : converter.FromProvider(values[index]), type) : null;

    // DTO 属性上声明的值转换器（同一类型共用一个实例）
    private static IValueConverter? DtoConverter(PropertyInfo property) =>
        property.GetCustomAttribute<ColumnAttribute>()?.Converter is { } type ? ConverterInstances.GetOrAdd(type, t => (IValueConverter)Activator.CreateInstance(t)!) : null;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IValueConverter> ConverterInstances = new();

    private IncludePlan? TryInclude(Expression value)
    {
        // 自动映射（SelectAutoInclude）产出的带出标记：导航、带出查询的配置、子对象的投影都在参数里
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
            var markerPlan = new IncludePlan(navigation, childSelector?.ReturnType ?? navigation.Target.Type, childSelector, [], navigation.IsCollection || via.Any(v => v.IsCollection))
            {
                Configure = (Func<QueryState, QueryState>?)((ConstantExpression)marker.Arguments[1 + offset]).Value,
                Via = via,
            };
            _includes.Add((markerPlan, navOwner));
            return markerPlan;
        }
        var inner = value;
        if (inner is MethodCallExpression { Method.Name: nameof(Enumerable.ToList) or nameof(Enumerable.ToArray) } materialize
            && materialize.Method.DeclaringType == typeof(Enumerable))
            inner = materialize.Arguments[0];

        // 从外往里拆：最外层可以是 Select，里面是 Where 与排序；排序在子查询上执行（按书写顺序）
        LambdaExpression? selector = null;
        var filters = new List<LambdaExpression>();
        var orders = new List<OrderSpec>();
        while (inner is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Enumerable) && mc.Arguments.Count == 2
               && mc.Method.Name is nameof(Enumerable.Select) or nameof(Enumerable.Where) or nameof(Enumerable.OrderBy) or nameof(Enumerable.OrderByDescending)
                   or nameof(Enumerable.ThenBy) or nameof(Enumerable.ThenByDescending))
        {
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

        if (inner is not MemberExpression { Expression: { } ownerExpr } navMember || translator.BindingOf(ownerExpr) is not EntityBinding owner) return null;
        if (translator.FindNavigation(owner, navMember.Member.Name) is not { } nav) return null;
        if (!nav.IsCollection && (selector is not null || filters.Count > 0 || orders.Count > 0 || inner != value)) return null;

        var plan = new IncludePlan(nav, selector?.ReturnType ?? nav.Target.Type, selector, filters, nav.IsCollection)
        {
            Configure = orders.Count == 0 ? null : state => state with { OrderBys = state.OrderBys.AddRange(orders) },
        };
        _includes.Add((plan, owner));
        return plan;
    }

    // 实体查询的 Include：实体读出后，集合导航先放一个空 List（主查询后填充），对一导航登记回填函数
    private Expression WithIncludes(Expression entity, EntityBinding owner, IReadOnlyList<IncludeSpec> includes)
    {
        var instance = Expression.Variable(entity.Type, "entity");
        var body = new List<Expression> { Expression.Assign(instance, entity) };
        foreach (var spec in includes)
        {
            var nav = translator.FindNavigation(owner, spec.Member) ?? throw new NotSupportedException($"{owner.Entity.Type.Name}.{spec.Member} 不是导航属性");
            var plan = IncludePlan.ForNavigation(translator.Context!.Metadata, nav, spec.Configure);
            _includes.Add((plan, owner));
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
