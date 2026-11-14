using System.Diagnostics.CodeAnalysis;
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

    /// <summary>按类型配置的逻辑删除（<see cref="EasyDbOptions.ApplyLogicDelete"/>），实体没有标注时生效。</summary>
    internal IReadOnlyList<EasyDbOptions.LogicDeleteRule> LogicDeleteRules { get; init; } = [];

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
        var selfColumns = Columns(self, attribute.SelfProperties);
        var targetColumns = Columns(target, attribute.TargetProperties);
        if (attribute.KeyMatch == KeyMatch.SelfCsv)
        {
            if (attribute.Relation != RelationType.ManyToMany || mapping is not null || attribute.DirectMapping.Length > 0
                || selfColumns.Count != 1 || targetColumns.Count != 1 || selfColumns[0].ClrType != typeof(string))
                throw new InvalidOperationException($"{self.Type.Name}.{property.Name} 的 KeyMatch.SelfCsv 要用在没有中间表的多对多导航上，本表一个 string 列存逗号分隔的目标键、目标一列");
        }
        else if (attribute.Relation == RelationType.ManyToMany && mapping is null)
            throw new InvalidOperationException($"{self.Type.Name}.{property.Name} 是多对多导航，需要指定 Mapping（中间表实体）");

        return new NavigationMetadata(property, attribute, self, target,
            selfColumns, targetColumns,
            mapping,
            mapping is null ? [] : attribute.SelfMappingProperties.Select(mapping.GetColumn).ToList(),
            mapping is null ? [] : attribute.TargetMappingProperties.Select(mapping.GetColumn).ToList(),
            ExtraFilterOf(self.Type, property, nameof(NavigateAttribute.ExtraFilter), attribute.ExtraFilter),
            ExtraFilterOf(self.Type, property, nameof(NavigateAttribute.ExtraMappingFilter), attribute.ExtraMappingFilter))
        {
            IncludeOn = property.GetCustomAttribute<IncludeOnAttribute>() is { } on ? (self.GetColumn(on.Property), on.Value) : null,
        };
    }

    /// <summary>
    /// 导航上的固定条件：本实体上的静态属性 / 字段，类型是 <c>Expression&lt;Func&lt;目标或中间表, bool&gt;&gt;</c>。
    /// 每次翻译时重新取值（对应 Java 的 NavigateExtraFilterStrategy 每次构建都调用），属性返回 null 表示这次不加条件。
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    private static Func<System.Linq.Expressions.LambdaExpression?>? ExtraFilterOf(Type owner, PropertyInfo navigation, string option, string? memberName)
    {
        if (memberName is null) return null;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        if (owner.GetProperty(memberName, flags) is { } property && typeof(System.Linq.Expressions.LambdaExpression).IsAssignableFrom(property.PropertyType))
            return () => (System.Linq.Expressions.LambdaExpression?)property.GetValue(null);
        if (owner.GetField(memberName, flags) is { } field && typeof(System.Linq.Expressions.LambdaExpression).IsAssignableFrom(field.FieldType))
            return () => (System.Linq.Expressions.LambdaExpression?)field.GetValue(null);
        throw new InvalidOperationException($"{owner.Name}.{navigation.Name} 的 {option} '{memberName}' 必须是类型为 Expression<Func<…, bool>> 的静态属性或字段");
    }

    /// <summary>集合类型的元素类型；不是集合时返回类型本身。</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
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
        // 无实体查询：表名由 db.Table(..) 给出，列在查询里按名字引用
        if (type == typeof(DynamicRow)) return new EntityMetadata(type, "", null, null, []);
        var table = type.GetCustomAttribute<TableAttribute>();
        var columns = new List<ColumnMetadata>();
        LogicDeleteAttribute? logicDelete = null;
        ColumnMetadata? logicDeleteColumn = null;
        ColumnMetadata? versionColumn = null;
        VersionAttribute? version = null;

        var ignored = table?.IgnoreProperties ?? [];
        var navigations = new Dictionary<string, (PropertyInfo, NavigateAttribute)>(StringComparer.Ordinal);
        var valueObjects = new Dictionary<string, ValueObjectMetadata>(StringComparer.Ordinal);
        foreach (var property in OrderedProperties(type))
        {
            if (property.GetCustomAttribute<NavigateAttribute>() is { } navigate)
            {
                navigations[property.Name] = (property, navigate);
                continue;
            }
            if (ignored.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) continue;
            if (property.GetCustomAttribute<ValueObjectAttribute>() is { } valueObject)
            {
                columns.AddRange(ExpandValueObject(type, [], property, valueObject, "", false, valueObjects));
                continue;
            }
            if (!IsMappable(property)) continue;
            var column = CreateColumn(type, [], property, NameConversion.Convert(property.Name), false);
            columns.Add(column);

            if (property.GetCustomAttribute<LogicDeleteAttribute>() is { } ld)
            {
                if (logicDelete is not null) throw new InvalidOperationException($"{type.Name} 声明了多个逻辑删除列");
                logicDelete = ld;
                logicDeleteColumn = column;
            }
            if (property.GetCustomAttribute<VersionAttribute>() is { } v)
            {
                if (versionColumn is not null) throw new InvalidOperationException($"{type.Name} 声明了多个版本列");
                version = v;
                versionColumn = column;
            }
        }
        if (logicDelete is null && LogicDeleteRules.FirstOrDefault(r => r.AppliesTo(type)) is { } rule)
        {
            logicDelete = rule.LogicDelete;
            logicDeleteColumn = columns.FirstOrDefault(c => c.Property.Name == rule.Property)
                ?? throw new InvalidOperationException($"{type.Name} 没有属性 {rule.Property}，无法作为逻辑删除列");
        }

        var tableName = table?.Name ?? NameConversion.Convert(type.Name);
        var entity = new EntityMetadata(type, tableName, table?.Schema, table?.Comment, columns)
        {
            NotFoundMessage = table?.NotFoundMessage,
            LogicDelete = logicDelete,
            LogicDeleteColumn = logicDeleteColumn,
            VersionColumn = versionColumn,
            VersionStrategy = version?.Strategy ?? VersionStrategy.Increment,
            NavigationProperties = navigations,
            ValueObjects = valueObjects,
            RenamedFrom = type.GetCustomAttribute<RenamedFromAttribute>()?.Name,
            Indexes = type.GetCustomAttributes<TableIndexAttribute>().Select(index => IndexOf(type, tableName, columns, index)).ToList(),
        };
        entity._foreignKeys = new Lazy<IReadOnlyList<ForeignKeyMetadata>>(() => ForeignKeysOf(entity));
        return entity;
    }

    // 不写名字时与 Java 一致：表名_列名…_idx，唯一索引 _uidx
    private static IndexMetadata IndexOf(Type type, string tableName, List<ColumnMetadata> columns, TableIndexAttribute index)
    {
        if (index.Properties.Length == 0) throw new InvalidOperationException($"{type.Name} 的 [TableIndex] 没有列");
        var parts = index.Properties.Select(p =>
        {
            var column = columns.FirstOrDefault(c => c.PropertyName == p)
                         ?? throw new InvalidOperationException($"{type.Name} 的 [TableIndex] 引用了 {p}，它不是映射到列的属性");
            return (column, index.Descending.Contains(p, StringComparer.Ordinal));
        }).ToList();
        var name = index.Name ?? $"{tableName}_{string.Join("_", parts.Select(p => p.column.Name))}_{(index.Unique ? "u" : "")}idx";
        return new IndexMetadata(name, index.Unique, parts, index.Comment);
    }

    // 外键名与 Java 一致：本表_目标表_目标列…_fk
    private IReadOnlyList<ForeignKeyMetadata> ForeignKeysOf(EntityMetadata entity)
    {
        var keys = new List<ForeignKeyMetadata>();
        foreach (var (name, (property, _)) in entity.NavigationProperties)
        {
            if (property.GetCustomAttribute<ForeignKeyAttribute>() is not { } foreignKey) continue;
            var nav = FindNavigation(entity, name)!;
            if (nav.Relation is not (RelationType.ManyToOne or RelationType.OneToOne) || nav.Mapping is not null)
                throw new InvalidOperationException($"{entity.Type.Name}.{name} 的 [ForeignKey] 只能用在对一导航上（本表的列引用目标表）");
            keys.Add(new ForeignKeyMetadata($"{entity.TableName}_{nav.Target.TableName}_{string.Join("_", nav.TargetColumns.Select(c => c.Name))}_fk",
                entity, nav.SelfColumns, nav.Target, nav.TargetColumns, foreignKey.Action));
        }
        return keys;
    }

    private static ColumnMetadata CreateColumn(Type entityType, IReadOnlyList<PropertyInfo> ownerPath, PropertyInfo property, string defaultName, bool inNullableValueObject)
    {
        var attr = property.GetCustomAttribute<ColumnAttribute>();
        var computed = property.GetCustomAttribute<ComputedAttribute>() is { } c ? ComputedExpression(entityType, property, c.ExpressionMember) : null;
        // 自增列的值已经由库生成，再写默认值是矛盾的声明（Java 建表时静默丢掉默认值）
        if (attr is { Generated: true, DbDefault: not null })
            throw new InvalidOperationException(
                $"{entityType.Name}.{property.Name} 不能同时声明 Generated 与 DbDefault：Generated 是自增列；有默认值的列插入时为 null 就不写，要一直不写用 [InsertIgnore]");
        return new ColumnMetadata(ownerPath, property, attr?.Name ?? defaultName)
        {
            IsPrimaryKey = attr?.PrimaryKey ?? false,
            IsGenerated = attr?.Generated ?? false,
            GeneratedSql = attr?.GeneratedSql,
            KeyGenerator = attr?.KeyGenerator is { } keyGenerator ? CreateKeyGenerator(keyGenerator, property) : null,
            AutoSelect = attr?.AutoSelect ?? true,
            InsertIgnore = property.IsDefined(typeof(InsertIgnoreAttribute)) || computed is not null,
            UpdateIgnore = property.IsDefined(typeof(UpdateIgnoreAttribute)) || computed is not null,
            Computed = computed,
            UpdateWhenTrackedChanged = property.GetCustomAttribute<UpdateIgnoreAttribute>()?.WhenTrackedChanged ?? false,
            Converter = CreateConverter(entityType, property, attr?.Converter),
            SqlConversion = attr?.SqlConversion is { } conversion ? CreateSqlConversion(conversion, property) : null,
            DbType = attr?.DbType,
            Comment = attr?.Comment,
            DbDefault = attr?.DbDefault,
            RenamedFrom = property.GetCustomAttribute<RenamedFromAttribute>()?.Name,
            InNullableValueObject = inNullableValueObject,
            MayBeNull = attr?.PrimaryKey != true && (inNullableValueObject || DeclaredNullable(property)),
        };
    }

    // 可空值类型；引用类型看可空标注（剪裁后的程序没有标注信息，读到 Unknown，按可空处理）
    private static bool DeclaredNullable(PropertyInfo property) =>
        property.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(property.PropertyType) is not null
            : new NullabilityInfoContext().Create(property).ReadState != NullabilityState.NotNull;

    // 值对象展开成本表上的列（嵌套的值对象递归展开）。前缀默认是外层前缀 + "属性的列名_"，显式的 Prefix 是完整前缀
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    private List<ColumnMetadata> ExpandValueObject(Type entityType, IReadOnlyList<PropertyInfo> ownerPath, PropertyInfo property, ValueObjectAttribute attribute,
        string outerPrefix, bool outerNullable, Dictionary<string, ValueObjectMetadata> valueObjects)
    {
        var type = property.PropertyType;
        var where = $"{entityType.Name}.{string.Join(".", ownerPath.Select(o => o.Name).Append(property.Name))}";
        if (!type.IsClass || ScalarTypes.IsScalar(type)) throw new InvalidOperationException($"{where} 是值对象，类型要是类（不能是结构体或标量）");
        if (type.GetConstructor(Type.EmptyTypes) is null) throw new InvalidOperationException($"{where} 是值对象，{type.Name} 要有无参构造函数");
        if (!property.CanWrite) throw new InvalidOperationException($"{where} 是值对象，属性要可写");

        List<PropertyInfo> path = [.. ownerPath, property];
        var prefix = attribute.Prefix ?? outerPrefix + NameConversion.Convert(property.Name) + "_";
        var nullable = outerNullable || new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
        var columns = new List<ColumnMetadata>();
        foreach (var member in OrderedProperties(type))
        {
            if (member.GetCustomAttribute<ValueObjectAttribute>() is { } nested)
            {
                columns.AddRange(ExpandValueObject(entityType, path, member, nested, prefix, nullable, valueObjects));
                continue;
            }
            if (!IsMappable(member)) continue;
            var column = CreateColumn(entityType, path, member, prefix + NameConversion.Convert(member.Name), nullable);
            if (column.IsPrimaryKey || column.IsGenerated) throw new InvalidOperationException($"{where}.{member.Name}：值对象里不能有主键或自增列");
            columns.Add(column);
        }
        if (columns.Count == 0) throw new InvalidOperationException($"{where} 是值对象，但 {type.Name} 没有可映射的属性");
        var shape = new EntityMetadata(type, "", null, null, columns.Select(c => c.WithOwnerPath(c.OwnerPath.Skip(path.Count).ToList())).ToList()) { IsValueObject = true };
        var key = string.Join(".", path.Select(o => o.Name));
        valueObjects[key] = new ValueObjectMetadata(key, property, columns, shape);
        return columns;
    }

    // 基类属性在前，同一类型内按声明顺序（MetadataToken 递增即声明顺序；Native AOT 没有元数据令牌，反射本身按元数据顺序返回）；派生类重写的属性只保留一次
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    internal static IEnumerable<PropertyInfo> OrderedProperties(Type type)
    {
        var chain = new Stack<Type>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType) chain.Push(t);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in chain)
        {
            var declared = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.HasMetadataToken() ? p.MetadataToken : 0);
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

    private static IKeyGenerator CreateKeyGenerator([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type generatorType, PropertyInfo property) =>
        typeof(IKeyGenerator).IsAssignableFrom(generatorType)
            ? (IKeyGenerator)(Activator.CreateInstance(generatorType) ?? throw new InvalidOperationException($"无法创建主键生成器 {generatorType.Name}"))
            : throw new InvalidOperationException($"{property.DeclaringType?.Name}.{property.Name} 的主键生成器 {generatorType.Name} 没有实现 IKeyGenerator");

    private static ColumnSqlConversion CreateSqlConversion([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type conversionType, PropertyInfo property) =>
        typeof(ColumnSqlConversion).IsAssignableFrom(conversionType) && Activator.CreateInstance(conversionType) is ColumnSqlConversion conversion
            ? conversion
            : throw new InvalidOperationException($"{property.DeclaringType?.Name}.{property.Name} 的 SqlConversion {conversionType.Name} 要继承 ColumnSqlConversion 并有无参构造函数");

    // 列的值转换：[Column(Converter = …)] 或 [Encryption(…)]（加密也是一种转换），两者不能同时用
    private static IValueConverter? CreateConverter(Type entityType, PropertyInfo property, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? converterType)
    {
        var encryption = property.GetCustomAttribute<Encryption.EncryptionAttribute>();
        if (encryption is null) return converterType is null ? DefaultConverter(property) : CreateConverter(converterType, property);
        if (converterType is not null)
            throw new InvalidOperationException($"{entityType.Name}.{property.Name} 不能同时声明转换器和加密");
        if (property.PropertyType != typeof(string))
            throw new InvalidOperationException($"{entityType.Name}.{property.Name} 加密列必须是 string");
        var strategy = EncryptionStrategies.GetOrAdd(encryption.Strategy, _ => CreateStrategy(encryption.Strategy));
        return new Encryption.EncryptionConverter(strategy, entityType, property.Name, encryption.SupportQueryLike);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Encryption.IEncryptionStrategy> EncryptionStrategies = new();

    private static Encryption.IEncryptionStrategy CreateStrategy([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type) =>
        Activator.CreateInstance(type) as Encryption.IEncryptionStrategy ?? throw new InvalidOperationException($"{type.Name} 没有实现 IEncryptionStrategy");

    // 没有声明转换器时的默认转换：枚举类型标了 [StoreAsName] 时按名字存
    [UnconditionalSuppressMessage("Trimming", "IL2076", Justification = AotJustification.GeneratedInstantiation)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.GeneratedInstantiation)]
    private static IValueConverter? DefaultConverter(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return type.IsEnum && type.IsDefined(typeof(StoreAsNameAttribute), inherit: false)
            ? CreateConverter(typeof(EnumNameConverter<>).MakeGenericType(type), property)
            : null;
    }

    private static IValueConverter CreateConverter([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type converterType, PropertyInfo property)
    {
        if (!typeof(IValueConverter).IsAssignableFrom(converterType))
            throw new InvalidOperationException($"{property.DeclaringType?.Name}.{property.Name} 的转换器 {converterType.Name} 没有实现 IValueConverter");
        return (IValueConverter)(Activator.CreateInstance(converterType)
                                 ?? throw new InvalidOperationException($"无法创建转换器 {converterType.Name}"));
    }

    // 计算列：实体上的静态属性 / 字段，值是参数为实体的 lambda
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    private static System.Linq.Expressions.LambdaExpression ComputedExpression(Type entityType, PropertyInfo property, string memberName)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        var value = entityType.GetProperty(memberName, flags)?.GetValue(null) ?? entityType.GetField(memberName, flags)?.GetValue(null);
        return value is System.Linq.Expressions.LambdaExpression { Parameters: [{ } parameter] } lambda && parameter.Type.IsAssignableFrom(entityType)
            ? lambda
            : throw new InvalidOperationException(
                $"{entityType.Name}.{property.Name} 的计算列表达式 {memberName} 必须是 {entityType.Name} 上的静态成员，类型为 Expression<Func<{entityType.Name}, {property.PropertyType.Name}>>");
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
