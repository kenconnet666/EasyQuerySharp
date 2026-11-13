using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Filtering;

/// <summary>
/// 把列表请求（<see cref="TableRequest"/>）的筛选与排序加到查询上（对应 TS 版的 Queryable.table）。键是结果里的成员名（忽略大小写，值对象写成路径 <c>address.city</c>），
/// 相当于白名单：请求碰不到结果以外的列，嵌套对象、一对多的列表不能筛也不能排。投影过的查询按投影里这个成员的表达式加条件、排序，
/// 不包派生表；分组查询的条件写进 HAVING。请求的排序排在查询自己的排序之前。
/// </summary>
internal static class TableFilter
{
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    public static QueryState Apply(QueryState state, TableRequest request)
    {
        if (request.Filters.Count == 0 && request.Searches.Count == 0 && request.Sorts.Count == 0) return state;
        var shape = Shape(state);
        var predicates = new List<Expression>();
        foreach (var (key, texts) in request.Filters)
        {
            var column = Column(shape, key, "筛选");
            predicates.Add(ClientValues.Contains([.. texts.Select(text => Read(column.Type, text, key))], column));
        }
        foreach (var (key, text) in request.Searches)
        {
            var column = Column(shape, key, "搜索");
            if (column.Type != typeof(string)) throw new ArgumentException($"不能按 '{key}' 搜索：只有文字列能搜索");
            predicates.Add(Expression.Call(column, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, Expression.Constant(text)));
        }
        if (predicates.Count > 0)
        {
            var predicate = Expression.Lambda(predicates.Aggregate(Expression.AndAlso), shape.Parameters);
            state = state.GroupKey is null ? state with { Wheres = state.Wheres.Add(predicate) } : state with { Havings = state.Havings.Add(predicate) };
        }
        if (request.Sorts.Count == 0) return state;
        var sorts = request.Sorts.Select(s => new OrderSpec(Expression.Lambda(Expression.Convert(Column(shape, s.Key, "排序"), typeof(object)), shape.Parameters), s.Descending));
        return state with { OrderBys = [.. sorts, .. state.OrderBys] };
    }

    // 结果的形状：投影（可复用投影先展开），没有投影时是第一个表来源本身；参数与查询的各个来源（分组后是分组）一一对应
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private static LambdaExpression Shape(QueryState state)
    {
        if (state.RawSelect is not null) throw new NotSupportedException("原生 SELECT 列表（SelectRaw）的查询不能按列表请求筛选、排序");
        if (state.Selector is { } selector) return ProjectionInliner.Expand(selector);
        var parameters = state.Sources.Select((s, i) => Expression.Parameter(QueryState.TypeOf(s), "t" + i)).ToList();
        return Expression.Lambda(parameters[0], parameters);
    }

    // 结果里的一列：沿成员路径取，投影里的成员折回它的表达式；不是标量时报错，并列出能用的列。
    // 路径中间只能经过值对象：经过导航或嵌套对象就碰到了结果以外的表（请求来自地址栏，不能让它探测关联表的列）
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = AotJustification.Rooted)]
    private static Expression Column(LambdaExpression shape, string key, string use)
    {
        var current = shape.Body;
        var segments = key.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var member = current.Type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                         ?? throw new ArgumentException($"不能按 '{key}' {use}：不是结果里的列（可用：{string.Join(", ", ScalarMembers(shape.Body.Type))}）");
            var access = Expression.MakeMemberAccess(current, member);
            current = ProjectionInliner.FoldMember(access) ?? access;
            if (i < segments.Length - 1 && !(current is MemberExpression { Member: var folded } && folded.GetCustomAttribute<ValueObjectAttribute>() is not null))
                throw new ArgumentException($"不能按 '{key}' {use}：{member.Name} 是嵌套对象或导航，不是值对象，只能用结果里的列（可用：{string.Join(", ", ScalarMembers(shape.Body.Type))}）");
        }
        return ScalarTypes.IsScalar(current.Type)
            ? current
            : throw new ArgumentException($"不能按 '{key}' {use}：它是嵌套对象或列表，不是列（可用：{string.Join(", ", ScalarMembers(shape.Body.Type))}）");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    private static IEnumerable<string> ScalarMembers(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => ScalarTypes.IsScalar(p.PropertyType)).Select(p => p.Name);

    // 地址栏里的文字按列的类型读：布尔认 true / false / 1 / 0，其余按列的类型转换，读不成时报错
    private static object? Read(Type type, string text, string key)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (target == typeof(bool))
                return text switch
                {
                    "1" => true,
                    "0" => false,
                    _ when bool.TryParse(text, out var flag) => flag,
                    _ => throw new FormatException(),
                };
            return ValueReader.ConvertTo(text, target);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new ArgumentException($"'{key}' 的值 '{text}' 不是 {target.Name}", ex);
        }
    }
}
