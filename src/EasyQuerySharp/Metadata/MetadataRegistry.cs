using System.Collections.Concurrent;
using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 实体元数据的构建与缓存。每个 <see cref="EasyDb"/> 持有一个（命名约定可能不同），线程安全。
/// </summary>
public sealed class MetadataRegistry(NameConversion nameConversion)
{
    private readonly ConcurrentDictionary<Type, EntityMetadata> _cache = new();

    /// <summary>当前的命名约定。</summary>
    public NameConversion NameConversion { get; } = nameConversion;

    /// <summary>取实体元数据（首次访问时构建）。</summary>
    public EntityMetadata Get(Type type) => _cache.GetOrAdd(type, Build);

    /// <summary>取实体元数据（首次访问时构建）。</summary>
    public EntityMetadata Get<T>() => Get(typeof(T));

    private EntityMetadata Build(Type type)
    {
        var table = type.GetCustomAttribute<TableAttribute>();
        var columns = new List<ColumnMetadata>();
        LogicDeleteAttribute? logicDelete = null;
        ColumnMetadata? logicDeleteColumn = null;

        var ignored = table?.IgnoreProperties ?? [];
        foreach (var property in OrderedProperties(type))
        {
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
        };
    }

    // 基类属性在前，同一类型内按声明顺序（MetadataToken 递增即声明顺序）；派生类重写的属性只保留一次
    private static IEnumerable<PropertyInfo> OrderedProperties(Type type)
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
