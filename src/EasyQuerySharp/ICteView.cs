using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Reflection;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp;

/// <summary>
/// 由一条查询定义的实体（对应 Java 的 EntityCteViewer）：查询它、或把它作为 JOIN 来源时，先写 <c>WITH `表名` AS (视图查询)</c>，
/// 再从这个名字读；表名按 <see cref="Metadata.TableAttribute"/> 或命名约定得到。实体上照常可以声明导航。
/// </summary>
/// <example><code>
/// public class ActiveUser : ICteView&lt;ActiveUser&gt;
/// {
///     public string? Id { get; set; }
///     public string? Name { get; set; }
///     public static Query&lt;ActiveUser&gt; View(EasyDb db) =&gt;
///         db.Query&lt;User&gt;().Where(u =&gt; u.Age == null).Select(u =&gt; new ActiveUser { Id = u.Id, Name = u.Name });
/// }
/// </code></example>
public interface ICteView<TSelf> where TSelf : class, ICteView<TSelf>
{
    /// <summary>定义这个实体的查询。</summary>
    static abstract Query<TSelf> View(EasyDb db);
}

/// <summary>找出实现了 <see cref="ICteView{TSelf}"/> 的实体并取它的视图查询。</summary>
internal static class CteViews
{
    private static readonly ConcurrentDictionary<Type, MethodInfo?> ViewMethods = new();

    /// <summary><paramref name="entityType"/> 的视图查询；不是视图实体时返回 null。</summary>
    public static QueryState? Of(EasyDb db, Type entityType) =>
        ViewMethods.GetOrAdd(entityType, Find) is { } method ? ((IQueryStateAccessor)method.Invoke(null, [db])!).State : null;

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = AotJustification.Rooted)]
    private static MethodInfo? Find(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICteView<>) && i.GetGenericArguments()[0] == type)
            ? type.GetMethod(nameof(ICteView<>.View), BindingFlags.Public | BindingFlags.Static, [typeof(EasyDb)])
            : null;
}
