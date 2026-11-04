using System.Collections.Concurrent;
using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 实体元数据的构建与缓存。每个 <see cref="EasyDb"/> 持有一个（命名约定可能不同），线程安全。
/// </summary>
public sealed class MetadataRegistry(NameConversion nameConversion)
{
    private readonly ConcurrentDictionary<Type, EntityMetadata> _cache = new();
    private readonly ConcurrentDictionary<(Type, string), NavigationMetadata?> _navigations = new();

    /// <summary>当前的命名约定。</summary>
    public NameConversion NameConversion { get; } = nameConversion;

    /// <summary>取实体元数据（首次访问时构建）。</summary>
    public EntityMetadata Get(Type type) => _cache.GetOrAdd(type, Build);

    /// <summary>取实体元数据（首次访问时构建）。</summary>
    public EntityMetadata Get<T>() => Get(typeof(T));

    /// <summary>取实体上名为 <paramref name="property"/> 的导航；不是导航时返回 null。</summary>
    public NavigationMetadata? FindNavigation(EntityMetadata entity, string property) =>
        _navigations.GetOrAdd((entity.Type, property), key => entity.NavigationProperties.TryGetValue(key.Item2, out var nav)
            ? ResolveNavigation(entity, nav.Property, nav.Attribute)
            : null);

    private NavigationMetadata ResolveNavigation(EntityMetadata self, PropertyInfo property, NavigateAttribute attribute)
    {
        var target = Get(ElementType(property.PropertyType));
        IReadOnlyList<ColumnMetadata> Columns(EntityMetadata entity, string[] names) =>
            names.Length == 0 ? entity.KeyColumns : names.Select(entity.GetColumn).ToList();

        var mapping = attribute.Mapping is null ? null : Get(attribute.Mapping);
        if (attribute.Relation == RelationType.ManyToMany && mapping is null)
            throw new InvalidOperationException($"{self.Type.Name}.{property.Name} 是多对多导航，需要指定 Mapping（中间表实体）");

        return new NavigationMetadata(property, attribute, self, target,
            Columns(self, attribute.SelfProperties), Columns(target, attribute.TargetProperties),
            mapping,
            mapping is null ? [] : attribute.SelfMappingProperties.Select(mapping.GetColumn).ToList(),
            mapping is null ? [] : attribute.TargetMappingProperties.Select(mapping.GetColumn).ToList(),
            ExtraFilterOf(self.Type, property, attribute.ExtraFilter));
    }

    // 导航上的固定条件：本实体上的静态属性 / 字段，类型是 Expression<Func<目标, bool>>
    private static System.Linq.Expressions.LambdaExpression? ExtraFilterOf(Type owner, PropertyInfo navigation, string? memberName)
    {
        if (memberName is null) return null;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var value = owner.GetProperty(memberName, flags)?.GetValue(null) ?? owner.GetField(memberName, flags)?.GetValue(null);
        return value as System.Linq.Expressions.LambdaExpression
               ?? throw new InvalidOperationException($"{owner.Name}.{navigation.Name} 的 ExtraFilter '{memberName}' 必须是返回 Expression<Func<目标, bool>> 的静态成员");
    }

    /// <summary>集合类型的元素类型；不是集合时返回类型本身。</summary>
    internal static Type ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType()!;
        if (type == typeof(string)) return type;
        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0] ?? type;
    }

    private EntityMetadata Build(Type type)
    {
        var table = type.GetCustomAttribute<TableAttribute>();
        var columns = new List<ColumnMetadata>();
        LogicDeleteAttribute? logicDelete = null;
        ColumnMetadata? logicDeleteColumn = null;

        var ignored = table?.IgnoreProperties ?? [];
        var navigations = new Dictionary<string, (PropertyInfo, NavigateAttribute)>(StringComparer.Ordinal);
        foreach (var property in OrderedProperties(type))
        {
            if (property.GetCustomAttribute<NavigateAttribute>() is { } navigate)
            {
                navigations[property.Name] = (property, navigate);
                continue;
            }
            if (!IsMappable(property) || ignored.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) continue;
            var attr = property.GetCustomAttribute<ColumnAttribute>();
            var column = new ColumnMetadata(property, attr?.Name ?? NameConversion.Convert(property.Name))
            {
                IsPrimaryKey = attr?.PrimaryKey ?? false,
                IsGenerated = attr?.Generated ?? false,
                AutoSelect = attr?.AutoSelect ?? true,
                InsertIgnore = property.IsDefined(typeof(InsertIgnoreAttribute)),
                UpdateIgnore = property.IsDefined(typeof(UpdateIgnoreAttribute)),
                Converter = attr?.Converter is { } converterType ? CreateConverter(converterType, property) : null,
                DbType = attr?.DbType,
                Comment = attr?.Comment,
            };
            columns.Add(column);

            if (property.GetCustomAttribute<LogicDeleteAttribute>() is { } ld)
            {
                if (logicDelete is not null) throw new InvalidOperationException($"{type.Name} 声明了多个逻辑删除列");
                logicDelete = ld;
                logicDeleteColumn = column;
            }
        }

        return new EntityMetadata(type, table?.Name ?? NameConversion.Convert(type.Name), table?.Schema, table?.Comment, columns)
        {
            LogicDelete = logicDelete,
            LogicDeleteColumn = logicDeleteColumn,
            NavigationProperties = navigations,
        };
    }

    // 基类属性在前，同一类型内按声明顺序（MetadataToken 递增即声明顺序）；派生类重写的属性只保留一次
    internal static IEnumerable<PropertyInfo> OrderedProperties(Type type)
    {
        var chain = new Stack<Type>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType) chain.Push(t);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in chain)
        {
            var declared = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.MetadataToken);
            foreach (var p in declared)
                if (seen.Add(p.Name)) yield return type.GetProperty(p.Name, BindingFlags.Public | BindingFlags.Instance) ?? p;
        }
    }

    // 只映射可读写的公共实例属性；导航属性与集合不是列
    private static bool IsMappable(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0 || !property.CanRead) return false;
        if (property.IsDefined(typeof(ColumnIgnoreAttribute))) return false;
        if (property.IsDefined(typeof(ColumnAttribute))) return true;
        return ScalarTypes.IsScalar(property.PropertyType);
    }

    private static IValueConverter CreateConverter(Type converterType, PropertyInfo property)
    {
        if (!typeof(IValueConverter).IsAssignableFrom(converterType))
            throw new InvalidOperationException($"{property.DeclaringType?.Name}.{property.Name} 的转换器 {converterType.Name} 没有实现 IValueConverter");
        return (IValueConverter)(Activator.CreateInstance(converterType)
                                 ?? throw new InvalidOperationException($"无法创建转换器 {converterType.Name}"));
    }
}

/// <summary>哪些 CLR 类型可以直接映射成列。</summary>
internal static class ScalarTypes
{
    private static readonly HashSet<Type> Known =
    [
        typeof(string), typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(Guid), typeof(byte[]),
    ];

    /// <summary>是否标量（含可空、枚举）。</summary>
    public static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsEnum || Known.Contains(t);
    }
}
