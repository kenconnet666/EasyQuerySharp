using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 按属性名把实体投影到 DTO（对应 Java 的 select(Dto.class)）：DTO 每个可写的标量属性，在实体上找同名（忽略大小写）的映射列。
/// 产出的是普通的成员初始化 lambda，与手写 <c>Select(o => new Dto { … })</c> 走同一条管线；
/// DTO 属性上的 <c>[Column("…")]</c> 决定列别名。以后由 Source Generator 在编译期产出同样的表达式（见 0002）。
/// </summary>
internal static class AutoProjection
{
    /// <summary>构造 <c>o => new TTarget { A = o.A, … }</c>；<paramref name="excluded"/> 里的属性名跳过（对应 Java 的 allFieldsExclude）。</summary>
    public static LambdaExpression Build(EntityMetadata source, Type target, IReadOnlySet<string>? excluded = null)
    {
        var parameter = Expression.Parameter(source.Type, "o");
        // Map：o => Sql.Columns(o)，键是列名
        if (target == typeof(Dictionary<string, object?>) && excluded is null)
            return Expression.Lambda(Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Columns))!.MakeGenericMethod(source.Type), parameter), parameter);
        var bindings = new List<MemberBinding>();
        foreach (var property in MetadataRegistry.OrderedProperties(target))
        {
            if (property.SetMethod is not { IsPublic: true } || property.IsDefined(typeof(ColumnIgnoreAttribute), inherit: true)) continue;
            if (excluded?.Contains(property.Name) == true) continue;
            var column = source.Columns.FirstOrDefault(c => string.Equals(c.PropertyName, property.Name, StringComparison.OrdinalIgnoreCase));
            if (column is null || !ScalarTypes.IsScalar(property.PropertyType)) continue;

            Expression value = Expression.Property(parameter, column.Property);
            if (value.Type != property.PropertyType) value = Expression.Convert(value, property.PropertyType);
            bindings.Add(Expression.Bind(property, value));
        }
        if (bindings.Count == 0) throw new InvalidOperationException($"{target.Name} 没有与 {source.Type.Name} 同名的属性，无法自动映射");
        return Expression.Lambda(Expression.MemberInit(Expression.New(target), bindings), parameter);
    }

    /// <summary>
    /// 从 <c>o => o.A</c> 或 <c>o => new { o.A, o.B }</c> 取出被排除的属性名。
    /// </summary>
    public static HashSet<string> MemberNames(LambdaExpression selector)
    {
        var body = Translation.ExpressionTranslator.StripConvert(selector.Body);
        IEnumerable<Expression> parts = body is NewExpression n ? n.Arguments : [body];
        return parts.Select(p => Translation.ExpressionTranslator.StripConvert(p) is MemberExpression m
                ? m.Member.Name
                : throw new ArgumentException($"列清单只能写属性访问，如 t => new {{ t.Id, t.Title }}：{selector}", nameof(selector)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
