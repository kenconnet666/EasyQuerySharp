using System.ComponentModel;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp;

/// <summary>
/// 把静态方法声明为可复用投影（见 0002）：方法体只有一个表达式，生成器在编译期产出对应的表达式树并登记，
/// 查询里调用它时内联成方法体，在内存里调用就是普通方法。
/// </summary>
/// <example><code>
/// public static partial class PostVos
/// {
///     [Projection]
///     public static PostVo ToVo(this Post p) =&gt; new(p.Id, p.Title, p.Author!.Name);
/// }
/// // 查询里：u.Posts!.Select(p =&gt; p.ToVo()).ToList()
/// </code></example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProjectionAttribute : Attribute;

/// <summary>
/// <see cref="ProjectionAttribute"/> 方法与其表达式的登记表。由生成器产出的模块初始化代码调用，使用者不必直接用它。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ProjectionRegistry
{
    private static readonly ConcurrentDictionary<MethodInfo, LambdaExpression> Expressions = new();

    /// <summary>登记方法 <paramref name="method"/> 对应的表达式（参数与方法参数一一对应）。</summary>
    public static void Register(Delegate method, LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(expression);
        Expressions[method.Method] = expression;
    }

    /// <summary>取方法登记的表达式；没有登记时为 null。</summary>
    internal static LambdaExpression? Find(MethodInfo method) =>
        Expressions.TryGetValue(method, out var expression) ? expression : null;
}
