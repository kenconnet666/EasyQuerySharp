using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Querying.Includes;

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

    /// <summary>子对象投影（参数是目标实体）；为 null 时取整个实体。</summary>
    public LambdaExpression? ChildSelector { get; } = childSelector;

    /// <summary>子数据上的过滤条件（参数是目标实体）。</summary>
    public IReadOnlyList<LambdaExpression> ChildFilters { get; } = childFilters;

    /// <summary>是否集合（否则是单个对象）。</summary>
    public bool IsCollection { get; } = isCollection;

    /// <summary>子查询的配置（Include 的第二个参数）；为 null 时只按键过滤。</summary>
    public Func<QueryState, QueryState>? Configure { get; init; }

    /// <summary>主查询结果里父键列的序号（投影完成后填入）。</summary>
    public int[] ParentKeyOrdinals { get; set; } = [];

    /// <summary>本次执行收集到的父键 → 等待填充的目标（集合或单值的写回函数）。</summary>
    internal Dictionary<CompositeKey, List<Action<object?>>> Pending { get; } = [];

    /// <summary>
    /// 读一行时调用：登记这一行的父键，返回挂到父对象上的初始值（集合是空 List，之后填充；单值先返回 null，之后通过 setter 回填）。
    /// </summary>
    public object? Register(DbDataReader reader, Action<object?>? assign)
    {
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
