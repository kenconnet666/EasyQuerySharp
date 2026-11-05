using System.Collections.Immutable;

namespace EasyQuerySharp.Interceptors;

/// <summary>
/// 一条查询或写入上启用了哪些拦截器（对应 Java 的 useInterceptor / noInterceptor），不可变，按调用顺序累积：
/// <list type="bullet">
/// <item>默认：<see cref="IInterceptor.EnabledByDefault"/> 为 true 的全部生效；</item>
/// <item><c>DisableInterceptors()</c> 关闭全部（受保护的除外），并清掉之前单独启用的；之后再 <c>EnableInterceptor(name)</c> 只启用这一个；</item>
/// <item><c>EnableInterceptor(name)</c> 让默认关闭的拦截器也生效；<c>DisableInterceptor(name)</c> 关闭一个。</item>
/// </list>
/// </summary>
public sealed record InterceptorSelection
{
    /// <summary>默认选择。</summary>
    public static InterceptorSelection Default { get; } = new();

    private bool AllDisabled { get; init; }

    private ImmutableHashSet<string> Disabled { get; init; } = [];

    private ImmutableHashSet<string> Enabled { get; init; } = [];

    /// <summary>关闭一个。</summary>
    public InterceptorSelection Disable(string name) => this with { Disabled = Disabled.Add(name), Enabled = Enabled.Remove(name) };

    /// <summary>关闭全部（之前单独启用的也作废）。</summary>
    public InterceptorSelection DisableAll() => this with { AllDisabled = true, Enabled = [] };

    /// <summary>启用一个（包括默认关闭的）。</summary>
    public InterceptorSelection Enable(string name) => this with { Enabled = Enabled.Add(name), Disabled = Disabled.Remove(name) };

    /// <summary>恢复默认生效的全部。</summary>
    public InterceptorSelection EnableAll() => this with { AllDisabled = false };

    /// <summary>这个拦截器是否生效；受保护的（<see cref="IInterceptor.IsProtected"/>）不受"关闭全部"影响。</summary>
    public bool Applies(IInterceptor interceptor) =>
        Enabled.Contains(interceptor.Name)
        || (interceptor.EnabledByDefault && !Disabled.Contains(interceptor.Name) && (!AllDisabled || interceptor.IsProtected));
}
