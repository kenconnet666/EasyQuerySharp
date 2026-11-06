using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 一个映射到列的属性。由 <see cref="MetadataRegistry"/> 构建并缓存，运行期只读。
/// 值对象里的列（见 <see cref="ValueObjectAttribute"/>）从实体沿 <see cref="OwnerPath"/> 走到值对象，再取 <see cref="Property"/>。
/// </summary>
public sealed class ColumnMetadata
{
    internal ColumnMetadata(PropertyInfo property, string name) : this([], property, name)
    {
    }

    internal ColumnMetadata(IReadOnlyList<PropertyInfo> ownerPath, PropertyInfo property, string name)
    {
        OwnerPath = ownerPath;
        Property = property;
        PropertyName = string.Join(".", ownerPath.Select(o => o.Name).Append(property.Name));
        Name = name;
        ClrType = property.PropertyType;
        Getter = CompileGetter(ownerPath, property);
        Setter = property.CanWrite ? CompileSetter(ownerPath, property) : null;
    }

    /// <summary>对应的属性（值对象里的列是值对象上的属性）。</summary>
    public PropertyInfo Property { get; }

    /// <summary>从实体走到值对象经过的属性；普通列为空。</summary>
    public IReadOnlyList<PropertyInfo> OwnerPath { get; }

    /// <summary>属性名；值对象里的列是从实体起的路径，如 <c>Address.City</c>。</summary>
    public string PropertyName { get; }

    /// <summary>所在的值对象（或外层的值对象）本身可空：建表时这一列可空。</summary>
    internal bool InNullableValueObject { get; init; }

    /// <summary>列名（未加引号）。</summary>
    public string Name { get; }

    /// <summary>属性类型（可能是可空类型）。</summary>
    public Type ClrType { get; }

    /// <summary>是否主键的一部分。</summary>
    public bool IsPrimaryKey { get; internal init; }

    /// <summary>值是否由数据库生成（自增）。</summary>
    public bool IsGenerated { get; internal init; }

    /// <summary>主键生成器（<see cref="ColumnAttribute.KeyGenerator"/>）；为 null 时不生成。</summary>
    public IKeyGenerator? KeyGenerator { get; internal init; }

    /// <summary>插入时写的 SQL 片段（<see cref="ColumnAttribute.GeneratedSql"/>）；为 null 时按普通列处理。</summary>
    public string? GeneratedSql { get; internal init; }

    /// <summary>默认查询是否选出。</summary>
    public bool AutoSelect { get; internal init; } = true;

    /// <summary>计算列的表达式（参数是实体）；真实列为 null。见 <see cref="ComputedAttribute"/>。</summary>
    public System.Linq.Expressions.LambdaExpression? Computed { get; internal init; }

    /// <summary>插入时是否忽略。</summary>
    public bool InsertIgnore { get; internal init; }

    /// <summary>按实体更新时是否忽略。</summary>
    public bool UpdateIgnore { get; internal init; }

    /// <summary>按实体更新时忽略，但实体被追踪且这一列变了时仍然写（<see cref="UpdateIgnoreAttribute.WhenTrackedChanged"/>）。</summary>
    public bool UpdateWhenTrackedChanged { get; internal init; }

    /// <summary>值转换器；没有时为 null。</summary>
    public IValueConverter? Converter { get; internal init; }

    /// <summary>列在 SQL 里的读写转换；没有时为 null。见 <see cref="ColumnSqlConversion"/>。</summary>
    public ColumnSqlConversion? SqlConversion { get; internal init; }

    /// <summary>code-first 用的数据库类型（显式声明时）。</summary>
    public string? DbType { get; internal init; }

    /// <summary>列注释。</summary>
    public string? Comment { get; internal init; }

    /// <summary>改名前的列名（<see cref="RenamedFromAttribute"/>）；code-first 同步时用。</summary>
    internal string? RenamedFrom { get; init; }

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

    /// <summary>同一列换一条路径（单独物化值对象时，列相对值对象）。</summary>
    internal ColumnMetadata WithOwnerPath(IReadOnlyList<PropertyInfo> ownerPath) => new(ownerPath, Property, Name)
    {
        IsPrimaryKey = IsPrimaryKey, IsGenerated = IsGenerated, KeyGenerator = KeyGenerator, GeneratedSql = GeneratedSql, AutoSelect = AutoSelect,
        Computed = Computed, InsertIgnore = InsertIgnore, UpdateIgnore = UpdateIgnore, UpdateWhenTrackedChanged = UpdateWhenTrackedChanged,
        Converter = Converter, SqlConversion = SqlConversion, DbType = DbType, Comment = Comment, InNullableValueObject = InNullableValueObject,
        RenamedFrom = RenamedFrom,
    };

    /// <inheritdoc />
    public override string ToString() => $"{(OwnerPath.Count > 0 ? OwnerPath[0] : Property).DeclaringType?.Name}.{PropertyName} → {Name}";

    // 沿路径取值，路上的值对象为 null 时整列读成 null
    private static Func<object, object?> CompileGetter(IReadOnlyList<PropertyInfo> ownerPath, PropertyInfo property)
    {
        var obj = Expression.Parameter(typeof(object), "o");
        var root = Expression.Convert(obj, (ownerPath.Count > 0 ? ownerPath[0] : property).DeclaringType!);
        return Expression.Lambda<Func<object, object?>>(Read(root, 0), obj).Compile();

        Expression Read(Expression instance, int depth)
        {
            if (depth == ownerPath.Count) return Expression.Convert(Expression.Property(instance, property), typeof(object));
            var owner = Expression.Variable(ownerPath[depth].PropertyType, "owner" + depth);
            return Expression.Block([owner],
                Expression.Assign(owner, Expression.Property(instance, ownerPath[depth])),
                Expression.Condition(Expression.Equal(owner, Expression.Constant(null, owner.Type)),
                    Expression.Constant(null, typeof(object)), Read(owner, depth + 1)));
        }
    }

    // 沿路径赋值，路上的值对象为 null 时先用无参构造函数建出来
    private static Action<object, object?> CompileSetter(IReadOnlyList<PropertyInfo> ownerPath, PropertyInfo property)
    {
        var obj = Expression.Parameter(typeof(object), "o");
        var value = Expression.Parameter(typeof(object), "v");
        var variables = new List<ParameterExpression>();
        var steps = new List<Expression>();
        Expression current = Expression.Convert(obj, (ownerPath.Count > 0 ? ownerPath[0] : property).DeclaringType!);
        foreach (var owner in ownerPath)
        {
            var next = Expression.Variable(owner.PropertyType, owner.Name);
            variables.Add(next);
            steps.Add(Expression.Assign(next, Expression.Property(current, owner)));
            steps.Add(Expression.IfThen(Expression.Equal(next, Expression.Constant(null, owner.PropertyType)),
                Expression.Block(Expression.Assign(next, Expression.New(owner.PropertyType)), Expression.Assign(Expression.Property(current, owner), next))));
            current = next;
        }
        steps.Add(Expression.Assign(Expression.Property(current, property), Expression.Convert(value, property.PropertyType)));
        return Expression.Lambda<Action<object, object?>>(Expression.Block(variables, steps), obj, value).Compile();
    }
}
