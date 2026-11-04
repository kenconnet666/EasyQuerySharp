using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>一个映射到列的属性。由 <see cref="MetadataRegistry"/> 构建并缓存，运行期只读。</summary>
public sealed class ColumnMetadata
{
    internal ColumnMetadata(PropertyInfo property, string name)
    {
        Property = property;
        Name = name;
        ClrType = property.PropertyType;
        Getter = CompileGetter(property);
        Setter = property.CanWrite ? CompileSetter(property) : null;
    }

    /// <summary>对应的属性。</summary>
    public PropertyInfo Property { get; }

    /// <summary>属性名。</summary>
    public string PropertyName => Property.Name;

    /// <summary>列名（未加引号）。</summary>
    public string Name { get; }

    /// <summary>属性类型（可能是可空类型）。</summary>
    public Type ClrType { get; }

    /// <summary>是否主键的一部分。</summary>
    public bool IsPrimaryKey { get; internal init; }

    /// <summary>值是否由数据库生成（自增）。</summary>
    public bool IsGenerated { get; internal init; }

    /// <summary>默认查询是否选出。</summary>
    public bool AutoSelect { get; internal init; } = true;

    /// <summary>插入时是否忽略。</summary>
    public bool InsertIgnore { get; internal init; }

    /// <summary>按实体更新时是否忽略。</summary>
    public bool UpdateIgnore { get; internal init; }

    /// <summary>值转换器；没有时为 null。</summary>
    public IValueConverter? Converter { get; internal init; }

    /// <summary>code-first 用的数据库类型（显式声明时）。</summary>
    public string? DbType { get; internal init; }

    /// <summary>列注释。</summary>
    public string? Comment { get; internal init; }

    /// <summary>读属性值。</summary>
    public Func<object, object?> Getter { get; }

    /// <summary>写属性值；只读属性为 null。</summary>
    public Action<object, object?>? Setter { get; }

    /// <summary>取实体上的值，并经过值转换器转成数据库值。</summary>
    public object? GetProviderValue(object entity)
    {
        var value = Getter(entity);
        return Converter is null ? value : Converter.ToProvider(value);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Property.DeclaringType?.Name}.{PropertyName} → {Name}";

    private static Func<object, object?> CompileGetter(PropertyInfo property)
    {
        var obj = Expression.Parameter(typeof(object), "o");
        var body = Expression.Convert(Expression.Property(Expression.Convert(obj, property.DeclaringType!), property), typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, obj).Compile();
    }

    private static Action<object, object?> CompileSetter(PropertyInfo property)
    {
        var obj = Expression.Parameter(typeof(object), "o");
        var value = Expression.Parameter(typeof(object), "v");
        var body = Expression.Assign(
            Expression.Property(Expression.Convert(obj, property.DeclaringType!), property),
            Expression.Convert(value, property.PropertyType));
        return Expression.Lambda<Action<object, object?>>(body, obj, value).Compile();
    }
}
