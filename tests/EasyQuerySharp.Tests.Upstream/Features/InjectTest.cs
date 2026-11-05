namespace EasyQuerySharp.Tests.Upstream.Features;

/// <summary>
/// 对应 Java：InjectTest.java（Java 版自带的简易依赖注入容器 ServiceCollection）。
/// C# 直接用 Microsoft.Extensions.DependencyInjection（见 EasyQuerySharp.DependencyInjection），不另造容器。
/// </summary>
public sealed class InjectTest
{
    private const string OwnContainer = "测的是 Java 自带的依赖注入容器；C# 用 Microsoft.Extensions.DependencyInjection，没有对应的内部容器";

    [Fact, Upstream("InjectTest.java#injectTest1", NotApplicable = OwnContainer)] public void InjectTest1() { }

    [Fact, Upstream("InjectTest.java#injectTest2", NotApplicable = OwnContainer)] public void InjectTest2() { }

    [Fact, Upstream("InjectTest.java#injectTest3", NotApplicable = OwnContainer)] public void InjectTest3() { }
}
