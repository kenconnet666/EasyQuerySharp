using EasyQuerySharp.Materialization;
using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 投影里的一处导航带出（对应 Java 的 include / 投影里的 set(nav, …)）：主查询读完后，按收集到的父键用一条
/// <c>WHERE 子键 IN (…)</c> 查询取回子数据，再挂到父对象上。每层一次查询，没有 N+1（见 0002）。
/// </summary>
internal sealed class IncludePlan(NavigationMetadata navigation, Type childType, LambdaExpression? childSelector,
    IReadOnlyList<LambdaExpression> childFilters, bool isCollection)
{
    /// <summary>导航。</summary>
    public NavigationMetadata Navigation { get; } = navigation;

    /// <summary>子对象类型（实体或 DTO；平铺带出是值数组，取实体的平铺带出是目标实体）。</summary>
    public Type ChildType => FlatPaths is null ? childType : EntityRows ? Navigation.Target.Type : typeof(object?[]);

    /// <summary>子对象投影（参数是目标实体）；为 null 时取整个实体。平铺带出时由各平铺路径拼成 <c>e =&gt; new object?[] { 路径1, 路径2, … }</c>。</summary>
    public LambdaExpression? ChildSelector => FlatPaths is null ? childSelector : EntityRows ? null : FlatSelector();

    /// <summary>
    /// 平铺带出（Java 的 @NavigateFlat：<c>[MapPath]</c> 的对一路径，以及经过集合的同一条路径上的几个属性）：同一路径上的各个值（参数是目标实体），
    /// 共用一组带出查询；不是平铺时为 null。子对象是按路径顺序的值数组，各父属性从里面取自己的那一项（<see cref="FlatItem"/>）。
    /// </summary>
    public List<LambdaExpression>? FlatPaths { get; init; }

    /// <summary>
    /// 平铺路径只有目标实体本身与它的列（<c>[MapPath("Teachers.Classes")]</c> 与 <c>[MapPath("Teachers.Classes.Name")]</c>）：有一项是整个实体时，
    /// 子查询取实体，各项在内存里从实体上取（与 Java 一致，只查一遍实体）。
    /// </summary>
    public bool AllowEntityRows { get; init; }

    private bool EntityRows => AllowEntityRows && FlatPaths!.Any(p => p.Body == p.Parameters[0]);

    /// <summary>子查询只取平铺的值（不是实体或 DTO）：与 Java 的 @NavigateFlat 一样，单表不写别名。</summary>
    public bool FlatColumns => FlatPaths is not null && !EntityRows;

    private Func<object?, object?>[]? _itemGetters;

    /// <summary>平铺带出的一行里第 <paramref name="index"/> 项。</summary>
    public object? FlatItem(object? child, int index)
    {
        if (child is object?[] values) return values[index];
        _itemGetters ??= FlatPaths!.Select(Getter).ToArray();
        return child is null ? null : _itemGetters[index](child);

        static Func<object?, object?> Getter(LambdaExpression path)
        {
            var row = Expression.Parameter(typeof(object), "row");
            var body = new Translation.ExpressionTranslator.ParameterReplacer(path.Parameters[0], Expression.Convert(row, path.Parameters[0].Type)).Visit(path.Body);
            return Expression.Lambda<Func<object?, object?>>(Expression.Convert(body, typeof(object)), row).Compile();
        }
    }

    private LambdaExpression FlatSelector()
    {
        var e = Expression.Parameter(Navigation.Target.Type, "e");
        var values = FlatPaths!.Select(p => (Expression)Expression.Convert(new Translation.ExpressionTranslator.ParameterReplacer(p.Parameters[0], e).Visit(p.Body), typeof(object)));
        return Expression.Lambda(Expression.NewArrayInit(typeof(object), values), e);
    }

    /// <summary>
    /// 到达 <see cref="Navigation"/> 之前依次经过的导航（<c>[MapPath("BankCards.User")]</c> 里的 BankCards）：先逐段查中间表取出下一段的键，
    /// 再按最后一段的键查子数据（与 Java 的平铺路径一致）。不经过时为空。
    /// </summary>
    public IReadOnlyList<NavigationMetadata> Via { get; init; } = [];

    /// <summary>
    /// 已在内存里的实体上带出（LoadIncludeAsync），或 Include 之后投影回同一实体时补上的带出：单表的子查询不写别名（与 Java 的 loadInclude 一致）。
    /// </summary>
    public bool Unaliased { get; init; }

    /// <summary>经过的导航来自直达导航（<see cref="NavigationMetadata.DirectPath"/>）：中间表按 Java 直达导航的格式查（见 IncludeRunner）。</summary>
    public bool DirectVia { get; init; }

    /// <summary>平铺带出（Java 的 @NavigateFlat：平铺值或经中间导航的路径）：父键列在投影里排在关联键之后。</summary>
    public bool IsFlat => FlatPaths is not null || (Via.Count > 0 && !DirectVia);

    /// <summary>
    /// 带出整个导航（Include / LoadIncludeAsync）的计划；直达导航展开成沿路径的各段导航，最后一段是带出的导航。
    /// </summary>
    public static IncludePlan ForNavigation(MetadataRegistry registry, NavigationMetadata navigation, Func<QueryState, QueryState>? configure, bool unaliased = false)
    {
        if (navigation.DirectPath.Count == 0)
            return new IncludePlan(navigation, navigation.Target.Type, null, [], navigation.IsCollection) { Configure = configure, Unaliased = unaliased };
        var chain = DirectChain(registry, navigation);
        return new IncludePlan(chain[^1], chain[^1].Target.Type, null, [], navigation.IsCollection)
        {
            Configure = configure,
            Unaliased = unaliased,
            Via = chain[..^1],
            DirectVia = true,
        };
    }

    /// <summary>直达导航沿 <see cref="NavigationMetadata.DirectPath"/> 展开成的各段导航，最后一段到达目标。</summary>
    public static List<NavigationMetadata> DirectChain(MetadataRegistry registry, NavigationMetadata navigation)
    {
        var chain = new List<NavigationMetadata>();
        var entity = navigation.Self;
        foreach (var name in navigation.DirectPath)
        {
            var step = registry.FindNavigation(entity, name) ?? throw new InvalidOperationException($"直达导航 {navigation} 的路径上 {entity.Type.Name}.{name} 不是导航属性");
            chain.Add(step);
            entity = step.Target;
        }
        return chain;
    }

    /// <summary>父对象上的关联列：经过中间导航时是第一段导航的自身列。</summary>
    public IReadOnlyList<ColumnMetadata> ParentColumns => Via.Count > 0 ? Via[0].SelfColumns : Navigation.SelfColumns;

    /// <summary>子数据上的过滤条件（参数是目标实体）。</summary>
    public IReadOnlyList<LambdaExpression> ChildFilters { get; } = childFilters;

    /// <summary>是否集合（否则是单个对象）。</summary>
    public bool IsCollection { get; } = isCollection;

    /// <summary>子查询的配置（Include 的第二个参数）；为 null 时只按键过滤。</summary>
    public Func<QueryState, QueryState>? Configure { get; init; }

    /// <summary>主查询结果里父键列的序号（投影完成后填入）。</summary>
    public int[] ParentKeyOrdinals { get; set; } = [];

    /// <summary>导航有 <see cref="NavigationMetadata.IncludeOn"/> 时，主查询结果里条件列的序号（投影完成后填入）。</summary>
    public int? ConditionOrdinal { get; set; }

    // 父对象满足带出条件（没有条件时总是满足）：值按条件列的类型比较
    private bool Applies(object? value) =>
        Navigation.IncludeOn is not { } on || Equals(Normalize(value, on.Column), Normalize(on.Value, on.Column));

    private static object? Normalize(object? value, ColumnMetadata column) =>
        Materialization.ValueReader.ConvertTo(column.Converter is { } converter && value is not null && !column.ClrType.IsInstanceOfType(value) ? converter.FromProvider(value) : value, column.ClrType);

    /// <summary>本次执行收集到的父键 → 等待填充的目标（集合或单值的写回函数）。</summary>
    internal Dictionary<CompositeKey, List<Action<object?>>> Pending { get; } = [];

    /// <summary>
    /// 读一行时调用：登记这一行的父键，返回挂到父对象上的初始值（集合是空 List，之后填充；单值先返回 null，之后通过 setter 回填）。
    /// </summary>
    public object? Register(DbDataReader reader, Action<object?>? assign) => Register(reader, assign, owner: null);

    /// <summary>实体的 Include：同 <see cref="Register(DbDataReader, Action{object?})"/>，另外记下父实体（追踪查询据此登记"已加载的导航"，见 0004）。</summary>
    public object? Register(DbDataReader reader, Action<object?>? assign, object? owner)
    {
        // 不满足带出条件的父对象：导航保持 null（与 Java 一致，集合也不给空列表）
        if (ConditionOrdinal is { } ordinal && !Applies(reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal))) return null;
        var key = CompositeKey.Read(reader, ParentKeyOrdinals, ParentColumns);
        if (owner is not null && key is not null) AddOwner(key.Value, owner);
        // 集合没给写回函数时是子对象本身的列表；给了时（平铺的列表）由写回函数逐个收下
        if (IsCollection && assign is null)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ChildType))!;
            if (key is not null) Add(key.Value, child => list.Add(child));
            return list;
        }
        if (key is not null && assign is not null) Add(key.Value, assign);
        return null;
    }

    /// <summary>已经在内存里的实体（LoadIncludeAsync）：按导航的自身列取父键，集合属性先放一个空 List，对一属性登记回填。</summary>
    public void RegisterLoaded(object entity, System.Reflection.PropertyInfo property)
    {
        if (Navigation.IncludeOn is { } on && !Applies(on.Column.Getter(entity))) return;
        var key = CompositeKey.Of(ParentColumns.Select(c => c.Property.GetValue(entity)));
        if (key is not null) AddOwner(key.Value, entity);
        if (IsCollection)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ChildType))!;
            property.SetValue(entity, list);
            if (key is not null) Add(key.Value, child => list.Add(child));
        }
        else if (key is not null) Add(key.Value, child => property.SetValue(entity, child));
    }

    /// <summary>本次执行收集到的父键 → 父实体（只有实体的 Include 记录）。</summary>
    internal Dictionary<CompositeKey, List<object>> Owners { get; } = [];

    private void AddOwner(CompositeKey key, object owner)
    {
        if (!Owners.TryGetValue(key, out var owners)) Owners[key] = owners = [];
        owners.Add(owner);
    }

    private void Add(CompositeKey key, Action<object?> sink)
    {
        if (!Pending.TryGetValue(key, out var sinks)) Pending[key] = sinks = [];
        sinks.Add(sink);
    }
}

/// <summary>
/// 由一到多列组成的键，可作字典键；任一列为 NULL 时整个键视为不存在。
/// 从结果集读出的值先按列转成属性类型，与内存里实体上的值可比较（SQLite 把 Guid 存成文本、MySQL 的 BINARY(16) 读成字节）。
/// </summary>
internal readonly record struct CompositeKey(object First, object? Second, object? Third)
{
    public static CompositeKey? Read(DbDataReader reader, int[] ordinals, IReadOnlyList<ColumnMetadata> columns)
    {
        var values = new object?[3];
        for (var i = 0; i < ordinals.Length; i++)
        {
            if (reader.IsDBNull(ordinals[i])) return null;
            var raw = reader.GetValue(ordinals[i]);
            var column = columns[i];
            var value = column.Converter is null
                ? ValueReader.ConvertTo(raw, column.ClrType)
                : column.Converter.FromProvider(ValueReader.ConvertTo(raw, column.Converter.ProviderType));
            if (value is null) return null;
            values[i] = Normalize(value);
        }
        return new CompositeKey(values[0]!, values[1], values[2]);
    }

    /// <summary>由内存里的值组成键（与从结果集读出的键可比较）；任一值为 null 时返回 null。</summary>
    public static CompositeKey? Of(IEnumerable<object?> values)
    {
        var normalized = new object?[3];
        var i = 0;
        foreach (var value in values)
        {
            if (value is null) return null;
            normalized[i++] = Normalize(value);
        }
        return new CompositeKey(normalized[0]!, normalized[1], normalized[2]);
    }

    // 父表与子表的同一个键可能是不同的整数类型（int / long / decimal），统一成 decimal 再比较；字节数组按内容比较
    private static object Normalize(object value) => value switch
    {
        byte[] bytes => new Bytes(bytes),
        _ when Type.GetTypeCode(value.GetType()) is >= TypeCode.SByte and <= TypeCode.Decimal => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
        _ => value,
    };

    /// <summary>键里的一个值还原成可以作参数的值（字节数组键在字典里包了一层）。</summary>
    public static object Unwrap(object value) => value is Bytes bytes ? bytes.Value : value;

    /// <summary>键的各列值（可以作参数）。</summary>
    public IEnumerable<object> Values(int count) => new[] { First, Second, Third }.Take(count).Select(v => Unwrap(v!));

    private sealed class Bytes(byte[] value)
    {
        public byte[] Value { get; } = value;
        public override bool Equals(object? obj) => obj is Bytes other && Value.AsSpan().SequenceEqual(other.Value);
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.AddBytes(Value);
            return hash.ToHashCode();
        }
    }
}

/// <summary>
/// 实体查询上的一处 <c>Include</c>：导航成员名，以及对子查询的配置（条件、排序、嵌套 Include），
/// 配置作用在以目标实体为来源的查询状态上。
/// </summary>
internal sealed record IncludeSpec(string Member, Func<QueryState, QueryState>? Configure);
