using System.Diagnostics.CodeAnalysis;
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
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    public static LambdaExpression Build(EntityMetadata source, Type target, IReadOnlySet<string>? excluded = null)
    {
        // Map：o => Sql.Columns(o)，键是列名
        if (target == typeof(Dictionary<string, object?>) && excluded is null)
        {
            var parameter = Expression.Parameter(source.Type, "o");
            return Expression.Lambda(Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Columns))!.MakeGenericMethod(source.Type), parameter), parameter);
        }
        return Build(null, source, target, excluded, [], root: true);
    }


    /// <summary>DTO 上 <see cref="MapFromAttribute{TEntity}"/> 声明的实体类型；没有声明时为 null。</summary>
    public static Type? MapFromEntity(Type dto) =>
        dto.GetCustomAttributes(inherit: false).Select(a => a.GetType())
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(MapFromAttribute<>))?.GetGenericArguments()[0];
    /// <summary>按结构带出导航的自动映射（selectAutoInclude）：嵌套 DTO 沿同名导航递归映射；DTO 引用自身的集合（树）留给 ToTreeAsync 拼。</summary>
    public static LambdaExpression BuildWithIncludes(MetadataRegistry registry, EntityMetadata source, Type target) => Build(registry, source, target, null, [], root: true);

    // registry 为 null 时只映射列。[MapExpression] 与对一路径的 [MapPath] 列与 Java 一致：根查询放最前面（Java 先应用额外的 select），带出查询放最后面
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = AotJustification.Rooted)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression Build(MetadataRegistry? registry, EntityMetadata source, Type target, IReadOnlySet<string>? excluded, HashSet<Type> visiting, bool root)
    {
        var parameter = Expression.Parameter(source.Type, "o");
        var bindings = new List<MemberBinding>();
        var expressions = new List<MemberBinding>();
        visiting.Add(target);
        foreach (var property in MetadataRegistry.OrderedProperties(target))
        {
            if (property.SetMethod is not { IsPublic: true } || property.IsDefined(typeof(ColumnIgnoreAttribute), inherit: true)) continue;
            if (excluded?.Contains(property.Name) == true) continue;
            var mapped = registry is null ? null : MappedExpression(source, parameter, property);
            var value = mapped ?? (registry is null ? ColumnValue(source, parameter, property) : Value(registry, source, parameter, property, visiting));
            if (value is null) continue;
            // 沿对一导航 JOIN 平铺的列（Java 的 @NavigateJoin）与 [MapExpression] 一样算额外的列
            var joined = property.IsDefined(typeof(MapPathAttribute)) && !(value is MethodCallExpression call && call.Method.DeclaringType == typeof(AutoIncludeMarkers));
            var extra = mapped is not null || joined;
            if (value.Type != property.PropertyType) value = Expression.Convert(value, property.PropertyType);
            (extra ? expressions : bindings).Add(Expression.Bind(property, value));
        }
        visiting.Remove(target);
        bindings = root ? [.. expressions, .. bindings] : [.. bindings, .. expressions];
        if (bindings.Count == 0) throw new InvalidOperationException($"{target.Name} 没有与 {source.Type.Name} 同名的属性，无法自动映射");
        return Expression.Lambda(Expression.MemberInit(Expression.New(target), bindings), parameter);
    }

    // [MapExpression(nameof(静态成员))]：成员是 Expression<Func<源实体, 值>>，参数换成当前行
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    private static Expression? MappedExpression(EntityMetadata source, ParameterExpression row, PropertyInfo property)
    {
        if (property.GetCustomAttribute<MapExpressionAttribute>() is not { } attribute) return null;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        var dto = property.DeclaringType!;
        var value = dto.GetProperty(attribute.ExpressionMember, flags)?.GetValue(null) ?? dto.GetField(attribute.ExpressionMember, flags)?.GetValue(null);
        if (value is not LambdaExpression { Parameters: [{ } parameter] } lambda || !parameter.Type.IsAssignableFrom(source.Type))
            throw new InvalidOperationException($"{dto.Name}.{property.Name} 的 MapExpression 需要静态成员 {attribute.ExpressionMember}，类型是 Expression<Func<{source.Type.Name}, …>>");
        return new Translation.ExpressionTranslator.ParameterReplacer(parameter, row).Visit(lambda.Body);
    }

    // 同名的列；带值转换器的列（如 JSON 对象列）属性类型兼容时同样映射，读取时照样转换；同名同类型的值对象整个映射
    private static MemberExpression? ColumnValue(EntityMetadata source, Expression row, PropertyInfo property)
    {
        if (source.ValueObjects.Values.FirstOrDefault(v => v.Property.DeclaringType!.IsAssignableFrom(source.Type)
                && string.Equals(v.Property.Name, property.Name, StringComparison.OrdinalIgnoreCase)) is { } valueObject)
            return property.PropertyType.IsAssignableFrom(valueObject.Property.PropertyType) ? Expression.Property(row, valueObject.Property) : null;
        var column = source.Columns.FirstOrDefault(c => string.Equals(c.PropertyName, property.Name, StringComparison.OrdinalIgnoreCase));
        if (column is null) return null;
        var mappable = ScalarTypes.IsScalar(property.PropertyType) || (column.Converter is not null && property.PropertyType.IsAssignableFrom(column.ClrType));
        return mappable ? Expression.Property(row, column.Property) : null;
    }

    private static Expression? Value(MetadataRegistry registry, EntityMetadata source, Expression row, PropertyInfo property, HashSet<Type> visiting)
    {
        if (property.GetCustomAttribute<MapPathAttribute>() is { } path) return PathValue(registry, source, row, property, path.Path, path.Join, visiting);
        if (ColumnValue(source, row, property) is { } column) return column;
        var navName = source.NavigationProperties.Keys.FirstOrDefault(n => string.Equals(n, property.Name, StringComparison.OrdinalIgnoreCase));
        if (navName is null || registry.FindNavigation(source, navName) is not { } nav) return null;
        var navValue = Expression.Property(row, nav.Property);

        if (!nav.IsCollection)
        {
            // 属性就是实体本身：整个对象带出；否则映射成嵌套 DTO
            if (property.PropertyType.IsAssignableFrom(nav.Target.Type)) return navValue;
            if (visiting.Contains(property.PropertyType)) return null;
            return Include(nameof(AutoIncludeMarkers.One), nav, navValue, property, property.PropertyType, Build(registry, nav.Target, property.PropertyType, null, visiting, root: false));
        }
        var element = MetadataRegistry.ElementType(property.PropertyType);
        if (element == property.PropertyType || ScalarTypes.IsScalar(element)) return null;
        // 引用自身的集合（树形 DTO）：不展开，拼树时再挂
        if (visiting.Contains(element)) return null;
        var selector = element.IsAssignableFrom(nav.Target.Type) ? null : Build(registry, nav.Target, element, null, visiting, root: false);
        return Include(nameof(AutoIncludeMarkers.Many), nav, navValue, property, element, selector);
    }

    // AutoIncludeMarkers.One / Many(导航, 配置, 投影)：规划器据此建带出计划。嵌套 DTO 实现 IAutoIncludeConfigure 时先按它配置，
    // DTO 属性上的 [Navigate] 再补上它没写的排序与条数
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = AotJustification.Marker)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    private static MethodCallExpression Include(string marker, NavigationMetadata nav, Expression navValue, PropertyInfo property, Type element, LambdaExpression? selector)
    {
        var resultType = selector?.ReturnType ?? nav.Target.Type;
        var method = typeof(AutoIncludeMarkers).GetMethod(marker)!.MakeGenericMethod(nav.Target.Type, resultType);
        var overrides = NavigateOverrides(property, nav.Target.Type);
        var configure = ConfigureOf(element, nav.Target.Type);
        var combined = overrides is null ? configure : configure is null ? overrides : state => overrides(configure(state));
        return Expression.Call(method, navValue, Expression.Constant(combined, typeof(Func<QueryState, QueryState>)),
            Expression.Constant(selector, typeof(LambdaExpression)));
    }

    // DTO 属性上的 [Navigate(OrderBy / Limit / Offset)]（Java DTO 上的 @Navigate）是这次投影的默认值，替代实体导航上声明的；
    // 带出查询自己写了排序、条数（IAutoIncludeConfigure）时以那边为准（Java 要另写 ignoreNavigateConfigure）
    private static Func<QueryState, QueryState>? NavigateOverrides(PropertyInfo property, Type target)
    {
        if (property.GetCustomAttribute<NavigateAttribute>() is not { } attribute) return null;
        var orders = NavigationMetadata.ParseOrder(attribute.OrderBy, target);
        if (orders.Count == 0 && attribute.Limit <= 0 && attribute.Offset <= 0) return null;
        return state => state.IgnoreNavigationDefaults ? state : state with
        {
            OrderBys = orders.Count == 0 || !state.OrderBys.IsEmpty ? state.OrderBys : [.. orders.Select(o => new OrderSpec(o.Key, o.Descending))],
            Limit = state.Limit ?? (attribute.Limit > 0 ? attribute.Limit : null),
            Offset = state.Offset ?? (attribute.Offset > 0 ? attribute.Offset : null),
        };
    }

    // DTO 实现了 IAutoIncludeConfigure<目标实体> 时，把它的 Configure 包成对查询状态的配置
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.ReferenceGeneric)]
    public static Func<QueryState, QueryState>? ConfigureOf(Type dto, Type entity)
    {
        var contract = typeof(IAutoIncludeConfigure<>).MakeGenericType(entity);
        if (!contract.IsAssignableFrom(dto)) return null;
        // 按签名找实现（含显式实现）：Native AOT 下 GetInterfaceMap 不认静态抽象成员
        var queryType = typeof(Query<>).MakeGenericType(entity);
        var method = dto.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Single(m => m.Name.EndsWith(nameof(IAutoIncludeConfigure<object>.Configure), StringComparison.Ordinal) && m.ReturnType == queryType
                         && m.GetParameters() is [{ ParameterType: var p }] && p == queryType);
        return state =>
        {
            var query = Activator.CreateInstance(queryType, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [state], null)!;
            return ((IQueryStateAccessor)method.Invoke(null, [query])!).State;
        };
    }

    // [MapPath("A.B.C")]：遇到集合导航时，剩下的路径在集合元素上取，结果是列表；全是对一导航时默认走带出查询（Flat 标记），
    // join 为 true（[MapPath(.., Join = true)]，或已经在带出查询里面）时直接成员访问（隐式 JOIN）
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    private static Expression PathValue(MetadataRegistry registry, EntityMetadata source, Expression row, PropertyInfo property, string path, bool join,
        HashSet<Type> visiting)
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
                // 剩下的全是导航（如 BankCards.User、Areas.Builds，最后可以再取一个标量如 Roles.Resources.PermissionMark）：
                // 逐段查中间表，带出最后一个导航的目标（实体、嵌套 DTO 或那个标量），结果拉平成一个列表
                if (NavigationChain(registry, nav.Target, segments[(i + 1)..], out var scalar) is [_, ..] chain)
                    return ThroughValue(registry, Expression.Property(current, member), chain, scalar, property, visiting);
                // 元素上取值：不经过别的导航的路径（Through 的 path 为空）
                var element = Expression.Parameter(nav.Target.Type, "e");
                var selector = Expression.Lambda(PathValue(registry, nav.Target, element, property, rest, join: true, visiting), element);
                return Expression.Call(typeof(AutoIncludeMarkers).GetMethod(nameof(AutoIncludeMarkers.Through))!.MakeGenericMethod(member.PropertyType, property.PropertyType),
                    Expression.Property(current, member), Expression.Constant(Array.Empty<string>()), Expression.Constant(null, typeof(Func<QueryState, QueryState>)),
                    Expression.Constant(selector, typeof(LambdaExpression)));
            }
            // 对一导航之后全是导航（如 Direct2.Direct3）：同样逐段查中间表，带出最后一个导航的目标
            if (nav is { IsCollection: false } && !join && NavigationChain(registry, nav.Target, segments[(i + 1)..], out var toOneScalar) is [_, ..] toOneChain && toOneScalar is null)
                return ThroughValue(registry, Expression.Property(current, member), toOneChain, null, property, visiting);
            // 对一导航、后面不再经过集合：剩下的路径在目标实体上取，整段走一条带出查询
            if (nav is { IsCollection: false } && !join && !HasCollection(registry, nav.Target, segments.Skip(i + 1)))
            {
                var target = Expression.Parameter(nav.Target.Type, "e");
                var rest = segments.Length == i + 1 ? (Expression)target : PathValue(registry, nav.Target, target, property, string.Join('.', segments.Skip(i + 1)), join: true, visiting);
                var flat = Expression.Lambda(rest, target);
                // 列表属性（Java 对一路径上的 @NavigateFlat List）：值放进只有一项的列表，由 FlatValue 包装
                var valueType = !ScalarTypes.IsScalar(property.PropertyType) && MetadataRegistry.ElementType(property.PropertyType) is var item && item != property.PropertyType
                                && item.IsAssignableFrom(flat.ReturnType)
                    ? property.PropertyType
                    : flat.ReturnType;
                return Expression.Call(typeof(AutoIncludeMarkers).GetMethod(nameof(AutoIncludeMarkers.Flat))!.MakeGenericMethod(nav.Target.Type, valueType),
                    Expression.Property(current, member), Expression.Constant(flat, typeof(LambdaExpression)));
            }
            current = Expression.Property(current, member);
            if (nav is not null) entity = nav.Target;
        }
        return current;
    }

    // AutoIncludeMarkers.Through(第一个导航, 后续导航名, 配置, 投影)：最后一个导航的目标映射成属性的元素类型（实体本身或嵌套 DTO）
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Marker)]
    private static Expression ThroughValue(MetadataRegistry registry, Expression navigation, List<NavigationMetadata> chain, PropertyInfo? scalar, PropertyInfo property,
        HashSet<Type> visiting)
    {
        var last = chain[^1].Target;
        var element = MetadataRegistry.ElementType(property.PropertyType);
        LambdaExpression? selector;
        if (scalar is not null)
        {
            var e = Expression.Parameter(last.Type, "e");
            selector = Expression.Lambda(Expression.Property(e, scalar), e);
        }
        else selector = element.IsAssignableFrom(last.Type) ? null : Build(registry, last, element, null, visiting, root: false);
        var method = typeof(AutoIncludeMarkers).GetMethod(nameof(AutoIncludeMarkers.Through))!.MakeGenericMethod(navigation.Type, property.PropertyType);
        return Expression.Call(method, navigation, Expression.Constant(chain.Select(n => n.Property.Name).ToArray()),
            Expression.Constant(ConfigureOf(element, last.Type), typeof(Func<QueryState, QueryState>)), Expression.Constant(selector, typeof(LambdaExpression)));
    }

    // 剩下的路径段全是导航（最后一段可以是标量属性，由 scalar 带回）时依次返回导航，否则为 null
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    private static List<NavigationMetadata>? NavigationChain(MetadataRegistry registry, EntityMetadata entity, IReadOnlyList<string> segments, out PropertyInfo? scalar)
    {
        scalar = null;
        var chain = new List<NavigationMetadata>();
        for (var i = 0; i < segments.Count; i++)
        {
            var member = entity.Type.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (member is null) return null;
            if (registry.FindNavigation(entity, member.Name) is not { } nav)
            {
                if (i != segments.Count - 1 || chain.Count == 0) return null;
                scalar = member;
                break;
            }
            chain.Add(nav);
            entity = nav.Target;
        }
        return chain;
    }

    // 剩下的路径段里有没有集合导航
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    private static bool HasCollection(MetadataRegistry registry, EntityMetadata entity, IEnumerable<string> segments)
    {
        foreach (var segment in segments)
        {
            var member = entity.Type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (member is null || registry.FindNavigation(entity, member.Name) is not { } nav) return false;
            if (nav.IsCollection) return true;
            entity = nav.Target;
        }
        return false;
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

    /// <summary>对一导航上平铺的一个值（<c>[MapPath]</c>）：同一导航上的几个平铺属性共用一条带出查询，<paramref name="path"/> 的参数是导航的目标实体。</summary>
    public static TValue Flat<TEntity, TValue>(TEntity? navigation, LambdaExpression path) =>
        throw new InvalidOperationException("只能出现在查询的投影里");

    /// <summary>集合导航带出成列表，<paramref name="selector"/> 为 null 时取整个实体。</summary>
    public static List<TResult> Many<TEntity, TResult>(IEnumerable<TEntity>? navigation, Func<QueryState, QueryState>? configure, LambdaExpression? selector) =>
        throw new InvalidOperationException("只能出现在查询的投影里");

    /// <summary>
    /// 沿导航路径走到的实体（<c>[MapPath("BankCards.User")]</c>、<c>[MapPath("Direct2.Direct3")]</c>）带出：<paramref name="path"/> 是第一个导航之后依次经过的导航名，
    /// 逐段查中间表换键；经过集合时是列表。<paramref name="selector"/> 的参数是最后一个导航的目标实体，为 null 时取整个实体。
    /// </summary>
    public static TValue Through<TNavigation, TValue>(TNavigation navigation, string[] path, Func<QueryState, QueryState>? configure, LambdaExpression? selector) =>
        throw new InvalidOperationException("只能出现在查询的投影里");

    /// <summary>Include 之后投影回同一实体时补上的导航（<paramref name="navigation"/> 是根实体的导航）：带出查询的单表不写别名（Java 此时按 loadInclude 的格式）。</summary>
    public static TNavigation Loaded<TNavigation>(TNavigation navigation) =>
        throw new InvalidOperationException("只能出现在查询的投影里");
}
