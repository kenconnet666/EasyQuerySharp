using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Filtering;

/// <summary>
/// 把条件对象 / 排序请求翻译成查询 lambda（对应 Java 的 DefaultWhereObjectQueryExecutor）。只拼表达式树，
/// 导航的隐式 JOIN、集合的 EXISTS 都交给普通的表达式翻译。条件的顺序与 Java 一致：按属性路径首次出现的顺序分组，
/// 同一路径上的条件按 DTO 属性的声明顺序；同一个集合导航下的条件合并进一个 <c>Any</c>。
/// </summary>
internal static class ObjectFilter
{
    /// <summary>条件对象 → 一个参数依次对应 <paramref name="sources"/> 的谓词 lambda；没有任何条件时返回 null。</summary>
    public static LambdaExpression? Where(object request, IReadOnlyList<Type> sources)
    {
        var parameters = sources.Select((t, i) => Expression.Parameter(t, i == 0 ? "t" : "t" + i)).ToArray();
        var root = new Node("", null, null);
        foreach (var property in MetadataRegistry.OrderedProperties(request.GetType()))
        {
            if (property.GetCustomAttribute<FilterAttribute>() is not { } filter || !property.CanRead) continue;
            var value = property.GetValue(request);
            if (IsEmpty(value, filter)) continue;
            AddCondition(root, parameters, property, filter, value!);
        }
        var predicates = new List<Expression>();
        Compose(root, predicates);
        return predicates.Count == 0 ? null : Expression.Lambda(predicates.Aggregate(Expression.AndAlso), parameters);
    }

    /// <summary>排序请求 → 排序键 lambda（参数依次对应 <paramref name="sources"/>）与方向。</summary>
    public static List<(LambdaExpression Key, bool Descending)> Sort(ISortRequest request, IReadOnlyList<Type> sources)
    {
        var builder = new SortBuilder();
        request.ConfigureSort(builder);
        var result = new List<(LambdaExpression, bool)>();
        foreach (var (property, ascending, table, nulls) in builder.Items)
        {
            var parameters = sources.Select((t, i) => Expression.Parameter(t, i == 0 ? "t" : "t" + i)).ToArray();
            Expression instance = Parameter(parameters, table, property);
            foreach (var segment in property.Split('.'))
            {
                var member = Member(instance.Type, segment) ?? throw new InvalidOperationException($"{instance.Type.Name} 上没有可排序的属性 {property}");
                instance = Expression.Property(instance, member);
            }
            Expression key = Expression.Convert(instance, typeof(object));
            // 指定了 NULL 的位置时包成 Sql.NullsFirst / NullsLast，与手写的排序走同一条翻译
            if (nulls is { } position)
                key = Expression.Call(typeof(Sql).GetMethod(position == SqlAst.NullsOrder.Last ? nameof(Sql.NullsLast) : nameof(Sql.NullsFirst))!.MakeGenericMethod(typeof(object)), key);
            result.Add((Expression.Lambda(key, parameters), !ascending));
        }
        return result;
    }

    // null、空集合不加条件；空白字符串除非允许
    private static bool IsEmpty(object? value, FilterAttribute filter) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s) && !filter.AllowEmptyStrings,
        IEnumerable items and not string => !items.Cast<object?>().Any(),
        _ => false,
    };

    private static void AddCondition(Node root, ParameterExpression[] parameters, PropertyInfo dtoProperty, FilterAttribute filter, object value)
    {
        var targets = filter.Properties is { Length: > 0 } names
            ? names.Select((name, i) => (Table: filter.Tables is { } tables && i < tables.Length ? tables[i] : 0, Path: name)).ToList()
            : [(filter.Table, filter.Property ?? dtoProperty.Name)];
        var op = filter.Op != FilterOp.Default ? filter.Op : dtoProperty.PropertyType == typeof(string) ? FilterOp.Like : FilterOp.Equal;

        // 条件挂在第一个目标的节点上；多个目标必须落在同一个作用域（同一个表或同一个集合导航）里
        Node? owner = null;
        Node? scope = null;
        var members = new List<Expression>();
        foreach (var (table, path) in targets)
        {
            var (leaf, member, leafScope) = Walk(root, parameters, table, path);
            owner ??= leaf;
            if (scope is not null && scope != leafScope)
                throw new InvalidOperationException($"{dtoProperty.Name} 的多个属性路径不在同一个表或集合导航里：{string.Join(", ", targets.Select(t => t.Path))}");
            scope = leafScope;
            members.Add(member);
        }
        var predicates = members.Select(m => Compare(m, op, value)).Where(p => p is not null).Select(p => p!).ToList();
        if (predicates.Count == 0) return;
        owner!.Predicates.Add(predicates.Count == 1 ? predicates[0] : predicates.Aggregate(Expression.OrElse));
    }

    // 沿路径走：对一导航直接取成员（隐式 JOIN），集合导航进入一个新的 Any 作用域
    private static (Node Leaf, Expression Member, Node Scope) Walk(Node root, ParameterExpression[] parameters, int table, string path)
    {
        Expression instance = Parameter(parameters, table, path);
        var node = root;
        var scope = root;
        var segments = path.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var member = Member(instance.Type, segments[i])
                         ?? throw new InvalidOperationException($"{instance.Type.Name} 上没有属性 {segments[i]}（路径 {path}）");
            var key = (i == 0 ? table + ":" : "") + member.Name;
            var last = i == segments.Length - 1;
            var elementType = last ? null : CollectionElement(member.PropertyType);
            var child = node.Children.FirstOrDefault(c => c.Key == key);
            if (child is null)
            {
                child = elementType is null
                    ? new Node(key, null, null)
                    : new Node(key, Expression.Property(instance, member), Expression.Parameter(elementType, "x" + elementType.Name));
                node.Children.Add(child);
            }
            if (child.Element is { } element)
            {
                instance = element;
                scope = child;
            }
            else
            {
                instance = Expression.Property(instance, member);
            }
            node = child;
        }
        return (node, instance, scope);
    }

    private static ParameterExpression Parameter(ParameterExpression[] parameters, int table, string path) =>
        table >= 0 && table < parameters.Length
            ? parameters[table]
            : throw new InvalidOperationException($"{path} 指定的表来源序号 {table} 超出范围（共 {parameters.Length} 个）");

    private static PropertyInfo? Member(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static Type? CollectionElement(Type type)
    {
        if (type == typeof(string)) return null;
        var element = MetadataRegistry.ElementType(type);
        return element == type ? null : element;
    }

    // 同一节点的条件在前，子节点依次在后；集合导航的子条件合成一个 Any
    private static void Compose(Node node, List<Expression> output)
    {
        output.AddRange(node.Predicates);
        foreach (var child in node.Children)
        {
            if (child.Element is null)
            {
                Compose(child, output);
                continue;
            }
            var inner = new List<Expression>();
            Compose(child, inner);
            // coll.Any(p)：集合导航的 EXISTS 子查询自带 LIMIT 1（与 Java 一致）
            var type = child.Element.Type;
            output.Add(inner.Count == 0
                ? Expression.Call(EnumerableMethod(nameof(Enumerable.Any), 1, type), child.Collection!)
                : Expression.Call(EnumerableMethod(nameof(Enumerable.Any), 2, type), child.Collection!, Expression.Lambda(inner.Aggregate(Expression.AndAlso), child.Element)));
        }
    }

    // Enumerable 上参数个数为 parameterCount 的同名泛型方法（Any 的两个重载）
    private static MethodInfo EnumerableMethod(string name, int parameterCount, Type element) =>
        typeof(Enumerable).GetMethods().First(m => m.Name == name && m.GetParameters().Length == parameterCount).MakeGenericMethod(element);

    private static Expression? Compare(Expression member, FilterOp op, object value)
    {
        switch (op)
        {
            case FilterOp.Equal: return Expression.Equal(member, Constant(value, member.Type));
            case FilterOp.NotEqual: return Expression.NotEqual(member, Constant(value, member.Type));
            case FilterOp.GreaterThan: return Expression.GreaterThan(member, Constant(value, member.Type));
            case FilterOp.GreaterThanOrEqual: return Expression.GreaterThanOrEqual(member, Constant(value, member.Type));
            case FilterOp.LessThan: return Expression.LessThan(member, Constant(value, member.Type));
            case FilterOp.LessThanOrEqual: return Expression.LessThanOrEqual(member, Constant(value, member.Type));
            case FilterOp.Like: return StringCall(member, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, value);
            case FilterOp.LikeStart: return StringCall(member, typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!, value);
            case FilterOp.LikeEnd: return StringCall(member, typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!, value);
            // Java 的 CONTAINS / STARTS_WITH / ENDS_WITH：值原样作参数，% 在 SQL 里拼，值里的通配符按字面匹配
            case FilterOp.Contains: return StringCall(member, typeof(Sql).GetMethod(nameof(Sql.LikeContains))!, value);
            case FilterOp.StartsWith: return StringCall(member, typeof(Sql).GetMethod(nameof(Sql.LikeStartsWith))!, value);
            case FilterOp.EndsWith: return StringCall(member, typeof(Sql).GetMethod(nameof(Sql.LikeEndsWith))!, value);
            case FilterOp.In or FilterOp.NotIn:
            {
                var list = TypedList(value, member.Type);
                var contains = Expression.Call(Expression.Constant(list), list.GetType().GetMethod(nameof(List<object>.Contains))!, member);
                return op == FilterOp.In ? contains : Expression.Not(contains);
            }
            case FilterOp.AnyEqual:
                return Sql.GroupExpression(((IEnumerable)value).Cast<object?>()
                    .Select(v => (Expression)Expression.Equal(member, Constant(v, member.Type))).Aggregate(Expression.OrElse));
            case FilterOp.RangeOpen or FilterOp.RangeClosed or FilterOp.RangeClosedOpen or FilterOp.RangeOpenClosed:
            {
                var bounds = ((IEnumerable)value).Cast<object?>().Take(2).ToList();
                if (bounds.Count == 0 || bounds[0] is null) return null;
                var lowerOpen = op is FilterOp.RangeOpen or FilterOp.RangeOpenClosed;
                var upperOpen = op is FilterOp.RangeOpen or FilterOp.RangeClosedOpen;
                Expression range = lowerOpen
                    ? Expression.GreaterThan(member, Constant(bounds[0], member.Type))
                    : Expression.GreaterThanOrEqual(member, Constant(bounds[0], member.Type));
                if (bounds.Count > 1 && bounds[1] is { } upper)
                    range = Expression.AndAlso(range, upperOpen
                        ? Expression.LessThan(member, Constant(upper, member.Type))
                        : Expression.LessThanOrEqual(member, Constant(upper, member.Type)));
                // 与 Java 一致：区间整体带括号
                return Sql.GroupExpression(range);
            }
            default:
                throw new NotSupportedException($"不支持的条件 {op}");
        }
    }

    // 实例方法 member.M(值)（string.Contains 等）或扩展方法 M(member, 值)（Sql.LikeContains 等）
    private static MethodCallExpression StringCall(Expression member, MethodInfo method, object value)
    {
        if (member.Type != typeof(string)) throw new InvalidOperationException($"{member} 不是字符串，不能用 LIKE 类条件");
        var text = Expression.Constant(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), typeof(string));
        return method.IsStatic ? Expression.Call(method, member, text) : Expression.Call(member, method, text);
    }

    private static ConstantExpression Constant(object? value, Type type) =>
        Expression.Constant(ValueReader.ConvertTo(value, Nullable.GetUnderlyingType(type) ?? type), type);

    private static IList TypedList(object values, Type memberType)
    {
        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(memberType))!;
        foreach (var v in (IEnumerable)values) list.Add(v is null ? null : ValueReader.ConvertTo(v, elementType));
        return list;
    }

    // 属性路径树上的一个节点：列、对一导航，或集合导航（Element 是 Any 的参数）
    private sealed class Node(string key, Expression? collection, ParameterExpression? element)
    {
        public string Key { get; } = key;

        public Expression? Collection { get; } = collection;

        public ParameterExpression? Element { get; } = element;

        public List<Node> Children { get; } = [];

        public List<Expression> Predicates { get; } = [];
    }
}
