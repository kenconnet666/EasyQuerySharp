namespace EasyQuerySharp.Metadata;

/// <summary>
/// 属性值与数据库值的双向转换（对应 Java 的 ValueConverter）：写入前把属性值转成数据库值，读出后转回属性值。
/// 实现 <see cref="ValueConverter{TModel,TProvider}"/> 比直接实现本接口更方便。
/// </summary>
public interface IValueConverter
{
    /// <summary>属性的类型。</summary>
    Type ModelType { get; }

    /// <summary>数据库侧的类型。</summary>
    Type ProviderType { get; }

    /// <summary>属性值 → 数据库值。</summary>
    object? ToProvider(object? value);

    /// <summary>数据库值 → 属性值。</summary>
    object? FromProvider(object? value);
}

/// <summary>强类型的值转换器基类。null 不经过转换函数，原样传递。</summary>
public abstract class ValueConverter<TModel, TProvider> : IValueConverter
{
    /// <inheritdoc />
    public Type ModelType => typeof(TModel);

    /// <inheritdoc />
    public Type ProviderType => typeof(TProvider);

    /// <summary>属性值 → 数据库值。</summary>
    protected abstract TProvider ToProvider(TModel value);

    /// <summary>数据库值 → 属性值。</summary>
    protected abstract TModel FromProvider(TProvider value);

    object? IValueConverter.ToProvider(object? value) => value is null ? null : ToProvider((TModel)value);

    object? IValueConverter.FromProvider(object? value) => value is null ? null : FromProvider((TProvider)value);
}
