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

    // 同一实体的另一张表（分片的节点）：列、键、读取委托共用
    private EntityMetadata(EntityMetadata source, string tableName)
    {
        Type = source.Type;
        TableName = tableName;
        Schema = source.Schema;
        Comment = source.Comment;
        Columns = source.Columns;
        KeyColumns = source.KeyColumns;
        _byProperty = source._byProperty;
        Factory = source.Factory;
    }

    /// <summary>
    /// 同一实体换成表 <paramref name="tableName"/>（分库分表的节点，code-first 逐张建表、同步时用）：默认的索引名、外键名以表名开头，跟着换成新表名
    /// （PostgreSQL 等库的索引名在 schema 里唯一）；没有改名来源。
    /// </summary>
    internal EntityMetadata ForTable(string tableName)
    {
        string Rename(string name) => name.StartsWith(TableName + "_", StringComparison.Ordinal) ? tableName + name[TableName.Length..] : name;
        var copy = new EntityMetadata(this, tableName)
        {
            NotFoundMessage = NotFoundMessage,
            LogicDeleteColumn = LogicDeleteColumn,
            LogicDelete = LogicDelete,
            VersionColumn = VersionColumn,
            VersionStrategy = VersionStrategy,
            NavigationProperties = NavigationProperties,
            ValueObjects = ValueObjects,
            IsValueObject = IsValueObject,
            Indexes = [.. Indexes.Select(i => i with { Name = Rename(i.Name) })],
        };
        copy._foreignKeys = new Lazy<IReadOnlyList<ForeignKeyMetadata>>(() => [.. ForeignKeys.Select(f => f with { Name = Rename(f.Name), Entity = copy })]);
        return copy;
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

    /// <summary>查不到行时的默认消息（<see cref="TableAttribute.NotFoundMessage"/>）；没有配置时为 null。</summary>
    public string? NotFoundMessage { get; internal init; }

    /// <summary>逻辑删除列；没有时为 null。</summary>
    public ColumnMetadata? LogicDeleteColumn { get; internal set; }

    /// <summary>逻辑删除的取值方式。</summary>
    public LogicDeleteAttribute? LogicDelete { get; internal set; }

    /// <summary>乐观锁版本列；没有时为 null。</summary>
    public ColumnMetadata? VersionColumn { get; internal set; }

    /// <summary>版本号的取值方式（有版本列时有效）。</summary>
    public VersionStrategy VersionStrategy { get; internal set; }

    /// <summary>
    /// 版本 <paramref name="current"/> 的下一个版本。当前版本为 null 时抛出异常：没有当前版本就无法做乐观锁检查。
    /// </summary>
    internal object NextVersion(object? current)
    {
        var column = VersionColumn!;
        if (current is null) throw new InvalidOperationException($"{Type.Name}.{column.PropertyName} 是版本列，当前版本不能为 null");
        var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
        return VersionStrategy switch
        {
            VersionStrategy.NewGuid when type == typeof(System.Guid) => System.Guid.NewGuid(),
            VersionStrategy.NewGuid => System.Guid.NewGuid().ToString("N"),
            _ => Convert.ChangeType(Convert.ToInt64(current, System.Globalization.CultureInfo.InvariantCulture) + 1, type, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    /// <summary>声明了 <see cref="NavigateAttribute"/> 的属性（未解析；解析后的信息由 <see cref="MetadataRegistry.FindNavigation"/> 给出）。</summary>
    internal IReadOnlyDictionary<string, (System.Reflection.PropertyInfo Property, NavigateAttribute Attribute)> NavigationProperties { get; init; } =
        new Dictionary<string, (System.Reflection.PropertyInfo, NavigateAttribute)>();

    /// <summary>创建实体实例（需要无参构造函数）。</summary>
    internal Func<object> Factory { get; }

    /// <summary>值对象属性（见 <see cref="ValueObjectAttribute"/>），按从实体起的路径（<c>Address</c>、<c>Address.Geo</c>）。</summary>
    internal IReadOnlyDictionary<string, ValueObjectMetadata> ValueObjects { get; init; } = new Dictionary<string, ValueObjectMetadata>();

    /// <summary>这是值对象自己的形状（只用来物化单独投影出的值对象，没有表）。</summary>
    internal bool IsValueObject { get; init; }

    /// <summary>改名前的表名（<see cref="RenamedFromAttribute"/>）；code-first 同步时用。</summary>
    internal string? RenamedFrom { get; init; }

    /// <summary>表上的索引（<see cref="TableIndexAttribute"/>）。</summary>
    internal IReadOnlyList<IndexMetadata> Indexes { get; init; } = [];

    /// <summary>对一导航上声明的外键（<see cref="ForeignKeyAttribute"/>）；要解析目标实体，首次访问时才构建。</summary>
    internal IReadOnlyList<ForeignKeyMetadata> ForeignKeys => _foreignKeys?.Value ?? [];

    internal Lazy<IReadOnlyList<ForeignKeyMetadata>>? _foreignKeys;

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
