using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace EasyQuerySharp;

/// <summary>
/// Native AOT 下运行时要用到、编译期又看不出来的东西，在这里显式引用一次，让编译器保留或生成。不会真的执行。
/// <list type="bullet">
/// <item>查询内部按来源个数拼的 lambda（联表的 ON、条件、排序键）：<c>Expression.Lambda(body, 参数)</c> 在运行时才定出
/// <c>Expression&lt;Func&lt;Company, User, Follow, bool&gt;&gt;</c> 这样的类型。Native AOT 只能从“同样形状”的共享模板实例化它：
/// 参数都是引用类型，结果是 <see cref="bool"/> 或 <see cref="object"/>。这里把 1–10 个参数的两种模板各用一次。</item>
/// <item>值类型的运算符：C# 把表达式树里 <c>decimal</c>、<c>DateOnly</c> 等的 <c>*</c>、<c>&gt;=</c> 编成不带方法的节点，
/// <see cref="Expression"/> 在运行时按反射找 <c>op_Multiply</c> 一类方法，裁剪后找不到就报“运算符没有定义”。</item>
/// </list>
/// </summary>
internal static class AotTemplates
{
    /// <summary>在 <see cref="EasyDb"/> 的构造函数里调用；开关永远不会打开，只为让编译器把这里当成可达的代码。</summary>
    public static void Reference()
    {
        if (!AppContext.TryGetSwitch("EasyQuerySharp.AotTemplates.Run", out var run) || !run) return;
        Keep();
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(decimal))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(DateTime))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(DateTimeOffset))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(DateOnly))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TimeOnly))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TimeSpan))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Guid))]
    private static void Keep()
    {
        var o = Expression.Parameter(typeof(object));
        Expression yes = Expression.Constant(true), self = o;
        _ = Expression.Lambda<Func<object, bool>>(yes, o);
        _ = Expression.Lambda<Func<object, object, bool>>(yes, o, o);
        _ = Expression.Lambda<Func<object, object, object, bool>>(yes, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, bool>>(yes, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, bool>>(yes, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, bool>>(yes, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, bool>>(yes, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object, bool>>(yes, o, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, bool>>(yes, o, o, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, bool>>(yes, o, o, o, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object>>(self, o);
        _ = Expression.Lambda<Func<object, object, object>>(self, o, o);
        _ = Expression.Lambda<Func<object, object, object, object>>(self, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object>>(self, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object>>(self, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object>>(self, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object>>(self, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object, object>>(self, o, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object>>(self, o, o, o, o, o, o, o, o, o);
        _ = Expression.Lambda<Func<object, object, object, object, object, object, object, object, object, object, object>>(self, o, o, o, o, o, o, o, o, o, o);
    }
}
