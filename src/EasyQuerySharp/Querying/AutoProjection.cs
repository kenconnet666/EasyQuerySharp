using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 按属性名把实体投影到 DTO（对应 Java 的 select(Dto.class) / selectAutoInclude(Dto.class)）：DTO 每个可写的标量属性，在实体上找同名（忽略大小写）的映射列；
/// 带出导航时，与实体导航同名的属性按导航映射：集合是嵌套 DTO 的列表，对一是嵌套 DTO（都走带出查询），属性类型就是实体本身时取整个实体；
/// <see cref="MapPathAttribute"/> 指定导航路径上的值。
/// 产出的是普通的成员初始化 lambda，与手写 <c>Select(o => new Dto { … })</c> 走同一条管线；
/// DTO 属性上的 <c>[Column("…")]</c> 决定列别名。以后由 Source Generator 在编译期产出同样的表达式（见 0002）。
/// </summary>
internal static class AutoProjection
{
    /// <summary>构造 <c>o => new TTarget { A = o.A, … }</c>；<paramref name="excluded"/> 里的属性名跳过（对应 Java 的 allFieldsExclude）。</summary>
    public static LambdaExpression Build(EntityMetadata source, Type target, IReadOnlySet<string>? excluded = null)
    {
        // Map：o => Sql.Columns(o)，键是列名
        if (target == typeof(Dictionary<string, object?>) && excluded is null)
        {
            var parameter = Expression.Parameter(source.Type, "o");
            return Expression.Lambda(Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Columns))!.MakeGenericMethod(source.Type), parameter), parameter);
        }
        return Build(null, source, target, excluded, []);
    }

    /// <summary>按结构带出导航的自动映射（selectAutoInclude）：嵌套 DTO 沿同名导航递归映射；DTO 引用自身的集合（树）留给 ToTreeAsync 拼。</summary>
    public static LambdaExpression BuildWithIncludes(MetadataRegistry registry, EntityMetadata source, Type target) => Build(registry, source, target, null, []);

    // registry 为 null 时只映射列
    private static LambdaExpression Build(MetadataRegistry? registry, EntityMetadata source, Type target, IReadOnlySet<string>? excluded, HashSet<Type> visiting)
    {
        var parameter = Expression.Parameter(source.Type, "o");
        var bindings = new List<MemberBinding>();
        visiting.Add(target);
        foreach (var property in MetadataRegistry.OrderedProperties(target))
        {
            if (property.SetMethod is not { IsPublic: true } || property.IsDefined(typeof(ColumnIgnoreAttribute), inherit: true)) continue;
            if (excluded?.Contains(property.Name) == true) continue;
            var value = registry is null ? ColumnValue(source, parameter, property) : Value(registry, source, parameter, property, visiting);
            if (value is null) continue;
            if (value.Type != property.PropertyType) value = Expression.Convert(value, property.PropertyType);
            bindings.Add(Expression.Bind(property, value));
        }
        visiting.Remove(target);
        if (bindings.Count == 0) throw new InvalidOperationException($"{target.Name} 没有与 {source.Type.Name} 同名的属性，无法自动映射");
        return Expression.Lambda(Expression.MemberInit(Expression.New(target), bindings), parameter);
    }

    // 同名的列
    private static MemberExpression? ColumnValue(EntityMetadata source, Expression row, PropertyInfo property)
    {
        var column = source.Columns.FirstOrDefault(c => string.Equals(c.PropertyName, property.Name, StringComparison.OrdinalIgnoreCase));
        return column is null || !ScalarTypes.IsScalar(property.PropertyType) ? null : Expression.Property(row, column.Property);
    }

    private static Expression? Value(MetadataRegistry registry, EntityMetadata source, Expression row, PropertyInfo property, HashSet<Type> visiting)
    {
        if (property.GetCustomAttribute<MapPathAttribute>() is { } path) return PathValue(registry, source, row, property, path.Path);
        if (ColumnValue(source, row, property) is { } column) return column;
        var navName = source.NavigationProperties.Keys.FirstOrDefault(n => string.Equals(n, property.Name, StringComparison.OrdinalIgnoreCase));
        if (navName is null || registry.FindNavigation(source, navName) is not { } nav) return null;
        var navValue = Expression.Property(row, nav.Property);

        if (!nav.IsCollection)
        {
            // 属性就是实体本身：整个对象带出；否则映射成嵌套 DTO
            if (property.PropertyType.IsAssignableFrom(nav.Target.Type)) return navValue;
            if (visiting.Contains(property.PropertyType)) return null;
            return Include(nameof(AutoIncludeMarkers.One), nav, navValue, property.PropertyType, Build(registry, nav.Target, property.PropertyType, null, visiting));
        }
        var element = MetadataRegistry.ElementType(property.PropertyType);
        if (element == property.PropertyType || ScalarTypes.IsScalar(element)) return null;
        // 引用自身的集合（树形 DTO）：不展开，拼树时再挂
        if (visiting.Contains(element)) return null;
        var selector = element.IsAssignableFrom(nav.Target.Type) ? null : Build(registry, nav.Target, element, null, visiting);
        return Include(nameof(AutoIncludeMarkers.Many), nav, navValue, element, selector);
    }

    // AutoIncludeMarkers.One / Many(导航, 配置, 投影)：规划器据此建带出计划；嵌套 DTO 实现 IAutoIncludeConfigure 时带上它的配置
    private static MethodCallExpression Include(string marker, NavigationMetadata nav, Expression navValue, Type element, LambdaExpression? selector)
    {
        var resultType = selector?.ReturnType ?? nav.Target.Type;
        var method = typeof(AutoIncludeMarkers).GetMethod(marker)!.MakeGenericMethod(nav.Target.Type, resultType);
        return Expression.Call(method, navValue, Expression.Constant(ConfigureOf(element, nav.Target.Type), typeof(Func<QueryState, QueryState>)),
            Expression.Constant(selector, typeof(LambdaExpression)));
    }

    // DTO 实现了 IAutoIncludeConfigure<目标实体> 时，把它的 Configure 包成对查询状态的配置
    private static Func<QueryState, QueryState>? ConfigureOf(Type dto, Type entity)
    {
        var contract = typeof(IAutoIncludeConfigure<>).MakeGenericType(entity);
        if (!contract.IsAssignableFrom(dto)) return null;
        var method = dto.GetInterfaceMap(contract).TargetMethods.Single();
        var queryType = typeof(Query<>).MakeGenericType(entity);
        return state =>
        {
            var query = Activator.CreateInstance(queryType, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [state], null)!;
            return ((IQueryStateAccessor)method.Invoke(null, [query])!).State;
        };
    }

    // [MapPath("A.B.C")]：对一导航直接成员访问（隐式 JOIN）；遇到集合导航时，剩下的路径在集合元素上取，结果是列表
    private static Expression PathValue(MetadataRegistry registry, EntityMetadata source, Expression row, PropertyInfo property, string path)
    {
        var segments = path.Split('.');
        Expression current = row;
        var entity = source;
        for (var i = 0; i < segments.Length; i++)
        {
            var member = entity.Type.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                         ?? throw new InvalidOperationException($"{property.DeclaringType!.Name}.{property.Name} 的 MapPath：{entity.Type.Name} 上没有 {segments[i]}");
            var nav = registry.FindNavigation(entity, member.Name);
            if (nav is { IsCollection: true })
            {
                var rest = string.Join('.', segments.Skip(i + 1));
                if (rest.Length == 0) throw new InvalidOperationException($"{property.Name} 的 MapPath 以集合导航结尾，需要再指定元素上的属性");
                var element = Expression.Parameter(nav.Target.Type, "e");
                var selector = Expression.Lambda(PathValue(registry, nav.Target, element, property, rest), element);
                return Expression.Call(typeof(AutoIncludeMarkers).GetMethod(nameof(AutoIncludeMarkers.Many))!.MakeGenericMethod(nav.Target.Type, selector.ReturnType),
                    Expression.Property(current, member), Expression.Constant(null, typeof(Func<QueryState, QueryState>)), Expression.Constant(selector, typeof(LambdaExpression)));
            }
            current = Expression.Property(current, member);
            if (nav is not null) entity = nav.Target;
        }
        return current;
    }
}

/// <summary>
/// 自动映射产出的投影里标记"带出"的方法：只出现在表达式里，由投影规划器识别成带出计划（<see cref="ProjectionCompiler"/>），不会被调用。
/// </summary>
internal static class AutoIncludeMarkers
{
    /// <summary>对一导航带出，<paramref name="selector"/> 为 null 时取整个实体。</summary>
    public static TResult One<TEntity, TResult>(TEntity? navigation, Func<QueryState, QueryState>? configure, LambdaExpression? selector) =>
        throw new InvalidOperationException("只能出现在查询的投影里");

    /// <summary>集合导航带出成列表，<paramref name="selector"/> 为 null 时取整个实体。</summary>
    public static List<TResult> Many<TEntity, TResult>(IEnumerable<TEntity>? navigation, Func<QueryState, QueryState>? configure, LambdaExpression? selector) =>
        throw new InvalidOperationException("只能出现在查询的投影里");
}
