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

    /// <summary>子对象类型（实体或 DTO）。</summary>
    public Type ChildType { get; } = childType;

    /// <summary>子对象投影（参数是目标实体）；为 null 时取整个实体。平铺带出时由各平铺路径拼成 <c>e =&gt; new object?[] { 路径1, 路径2, … }</c>。</summary>
    public LambdaExpression? ChildSelector => FlatPaths is null ? childSelector : FlatSelector();

    /// <summary>
    /// 平铺带出（<c>[MapPath]</c> 的对一路径，Java 的 ）：同一导航上的各个平铺路径（参数是目标实体），共用一条带出查询；不是平铺时为 null。
    /// 子对象是按路径顺序的值数组，各父属性从里面取自己的那一项。
    /// </summary>
    public List<LambdaExpression>? FlatPaths { get; init; }

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

    /// <summary>经过的导航来自直达导航（<see cref="NavigationMetadata.DirectPath"/>）：中间表按 Java 直达导航的格式查（见 IncludeRunner）。</summary>
    public bool DirectVia { get; init; }

    /// <summary>平铺带出（Java 的 @NavigateFlat：平铺值或经中间导航的路径）：父键列在投影里排在关联键之后。</summary>
    public bool IsFlat => FlatPaths is not null || (Via.Count > 0 && !DirectVia);

    /// <summary>
    /// 带出整个导航（Include / LoadIncludeAsync）的计划；直达导航展开成沿路径的各段导航，最后一段是带出的导航。
    /// </summary>
    public static IncludePlan ForNavigation(MetadataRegistry registry, NavigationMetadata navigation, Func<QueryState, QueryState>? configure)
    {
        if (navigation.DirectPath.Count == 0)
            return new IncludePlan(navigation, navigation.Target.Type, null, [], navigation.IsCollection) { Configure = configure };
        var chain = new List<NavigationMetadata>();
        var entity = navigation.Self;
        foreach (var name in navigation.DirectPath)
        {
            var step = registry.FindNavigation(entity, name) ?? throw new InvalidOperationException($"直达导航 {navigation} 的路径上 {entity.Type.Name}.{name} 不是导航属性");
            chain.Add(step);
            entity = step.Target;
        }
        return new IncludePlan(chain[^1], chain[^1].Target.Type, null, [], navigation.IsCollection)
        {
            Configure = configure,
            Via = chain[..^1],
            DirectVia = true,
        };
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
    public object? Register(DbDataReader reader, Action<object?>? assign)
    {
        // 不满足带出条件的父对象：导航保持 null（与 Java 一致，集合也不给空列表）
        if (ConditionOrdinal is { } ordinal && !Applies(reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal))) return null;
        var key = CompositeKey.Read(reader, ParentKeyOrdinals);
        if (IsCollection)
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
        if (IsCollection)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ChildType))!;
            property.SetValue(entity, list);
            if (key is not null) Add(key.Value, child => list.Add(child));
        }
        else if (key is not null) Add(key.Value, child => property.SetValue(entity, child));
    }

    private void Add(CompositeKey key, Action<object?> sink)
    {
        if (!Pending.TryGetValue(key, out var sinks)) Pending[key] = sinks = [];
        sinks.Add(sink);
    }
}

/// <summary>由一到多列组成的键，可作字典键；任一列为 NULL 时整个键视为不存在。</summary>
internal readonly record struct CompositeKey(object First, object? Second, object? Third)
{
    public static CompositeKey? Read(DbDataReader reader, int[] ordinals)
    {
        var values = new object?[3];
        for (var i = 0; i < ordinals.Length; i++)
        {
            if (reader.IsDBNull(ordinals[i])) return null;
            values[i] = Normalize(reader.GetValue(ordinals[i]));
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

    // 父表与子表的同一个键可能被驱动读成不同的整数类型（int / long / decimal），统一成 decimal 再比较
    private static object Normalize(object value) =>
        Type.GetTypeCode(value.GetType()) is >= TypeCode.SByte and <= TypeCode.Decimal
            ? Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture)
            : value;

    /// <summary>键的各列值。</summary>
    public IEnumerable<object> Values(int count) => new[] { First, Second, Third }.Take(count).Select(v => v!);
}

/// <summary>
/// 实体查询上的一处 <c>Include</c>：导航成员名，以及对子查询的配置（条件、排序、嵌套 Include），
/// 配置作用在以目标实体为来源的查询状态上。
/// </summary>
internal sealed record IncludeSpec(string Member, Func<QueryState, QueryState>? Configure);
