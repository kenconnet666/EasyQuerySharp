namespace EasyQuerySharp.Metadata;

/// <summary>一个实体的映射信息：表、列、主键、逻辑删除列等。由 <see cref="MetadataRegistry"/> 构建并缓存。</summary>
public sealed class EntityMetadata
{
    private readonly Dictionary<string, ColumnMetadata> _byProperty;

    internal EntityMetadata(Type type, string tableName, string? schema, string? comment, IReadOnlyList<ColumnMetadata> columns)
    {
        Type = type;
        TableName = tableName;
        Schema = schema;
        Comment = comment;
        Columns = columns;
        KeyColumns = columns.Where(c => c.IsPrimaryKey).ToList();
        _byProperty = columns.ToDictionary(c => c.PropertyName, StringComparer.Ordinal);
        Factory = CreateFactory(type);
    }

    /// <summary>实体类型。</summary>
    public Type Type { get; }

    /// <summary>表名（未加引号）。</summary>
    public string TableName { get; }

    /// <summary>schema；没有时为 null。</summary>
    public string? Schema { get; }

    /// <summary>表注释。</summary>
    public string? Comment { get; }

    /// <summary>全部列，顺序为：基类属性在前，同一类型内按声明顺序（与 Java 版一致，决定 SELECT 列顺序）。</summary>
    public IReadOnlyList<ColumnMetadata> Columns { get; }

    /// <summary>主键列；没有主键时为空。</summary>
    public IReadOnlyList<ColumnMetadata> KeyColumns { get; }

    /// <summary>逻辑删除列；没有时为 null。</summary>
    public ColumnMetadata? LogicDeleteColumn { get; internal set; }

    /// <summary>逻辑删除的取值方式。</summary>
    public LogicDeleteAttribute? LogicDelete { get; internal set; }

    /// <summary>声明了 <see cref="NavigateAttribute"/> 的属性（未解析；解析后的信息由 <see cref="MetadataRegistry.FindNavigation"/> 给出）。</summary>
    internal IReadOnlyDictionary<string, (System.Reflection.PropertyInfo Property, NavigateAttribute Attribute)> NavigationProperties { get; init; } =
        new Dictionary<string, (System.Reflection.PropertyInfo, NavigateAttribute)>();

    /// <summary>创建实体实例（需要无参构造函数）。</summary>
    internal Func<object> Factory { get; }

    /// <summary>按属性名取列；不是映射列时返回 null。</summary>
    public ColumnMetadata? FindColumn(string propertyName) => _byProperty.GetValueOrDefault(propertyName);

    /// <summary>按属性名取列；不是映射列时抛出带上下文的异常。</summary>
    public ColumnMetadata GetColumn(string propertyName) =>
        FindColumn(propertyName) ?? throw new InvalidOperationException($"{Type.Name}.{propertyName} 不是映射到列的属性");

    /// <summary>取唯一的主键列；没有主键或是复合主键时抛出异常。</summary>
    public ColumnMetadata SingleKey => KeyColumns.Count == 1
        ? KeyColumns[0]
        : throw new InvalidOperationException($"{Type.Name} 需要且只能有一个主键列，实际有 {KeyColumns.Count} 个");

    /// <inheritdoc />
    public override string ToString() => $"{Type.Name} → {TableName}";

    private static Func<object> CreateFactory(Type type)
    {
        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor is null) return () => throw new InvalidOperationException($"{type.Name} 没有无参构造函数，不能按实体物化");
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<object>>(
            System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.New(ctor), typeof(object)));
        return lambda.Compile();
    }
}
