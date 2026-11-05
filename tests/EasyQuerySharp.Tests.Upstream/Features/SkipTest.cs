namespace EasyQuerySharp.Tests.Upstream.Features;

/// <summary>对应 Java：SkipTest.java（Java 异常类的构造函数、JVM 上的循环计时），C# 没有对应的公共 API。</summary>
public sealed class SkipTest
{
    [Fact, Upstream("SkipTest.java#testEasyQueryConcurrentException", NotApplicable = "测的是 Java 各异常类的构造函数；C# 用 BCL 异常加少数几个库异常，各自的构造在用到它们的用例里覆盖")]
    public void TestEasyQueryConcurrentException() { }

    [Fact, Upstream("SkipTest.java#test11", NotApplicable = "JVM 上数组与 HashMap 取值的循环计时，没有断言，与本库无关")]
    public void Test11() { }
}
