namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：TestUpdateFlatAndPredicate.java / TestDeleteFlatAndPredicate.java。
/// 两份测的都是 Java 表达式树的内部结构：从 UPDATE / DELETE 的 WHERE 里取出平铺的 AND 条件（getFlatAndPredicates），
/// 再提取主键的等值 / IN 值（Java 用于分片路由与缓存失效）。C# 不暴露 SQL 表达式树，这些用例不适用。
/// </summary>
public sealed class FlatAndPredicateTest
{
    private const string Internal = "测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树";

    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey", NotApplicable = Internal)] public void UpdateExpressionKey() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey2", NotApplicable = Internal)] public void UpdateExpressionKey2() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey3", NotApplicable = Internal)] public void UpdateExpressionKey3() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey4", NotApplicable = Internal)] public void UpdateExpressionKey4() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey5", NotApplicable = Internal)] public void UpdateExpressionKey5() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey6", NotApplicable = Internal)] public void UpdateExpressionKey6() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey7", NotApplicable = Internal)] public void UpdateExpressionKey7() { }
    [Fact, Upstream("TestUpdateFlatAndPredicate.java#testExpressionKey8", NotApplicable = Internal)] public void UpdateExpressionKey8() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey", NotApplicable = Internal)] public void DeleteExpressionKey() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey2", NotApplicable = Internal)] public void DeleteExpressionKey2() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey3", NotApplicable = Internal)] public void DeleteExpressionKey3() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey4", NotApplicable = Internal)] public void DeleteExpressionKey4() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey5", NotApplicable = Internal)] public void DeleteExpressionKey5() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey6", NotApplicable = Internal)] public void DeleteExpressionKey6() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey7", NotApplicable = Internal)] public void DeleteExpressionKey7() { }
    [Fact, Upstream("TestDeleteFlatAndPredicate.java#testExpressionKey8", NotApplicable = Internal)] public void DeleteExpressionKey8() { }
}
