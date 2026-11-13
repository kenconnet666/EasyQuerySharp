using System.Linq.Expressions;
using EasyQuerySharp.Materialization;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 内部拼出来的“值在列表里”条件：<c>values.Contains((object)x.Col)</c>，值先转成列的类型。翻译成 <c>IN (?, …)</c>，与使用者写的
/// <c>ids.Contains(x.Id)</c> 走同一条路。列表是 <c>object[]</c>：不按列的类型构造 <c>List&lt;int&gt;</c> 一类的泛型实例，Native AOT 下不需要运行时生成代码。
/// </summary>
internal static class ClientValues
{
    private static readonly System.Reflection.MethodInfo ContainsMethod = new Func<IEnumerable<object?>, object?, bool>(Enumerable.Contains).Method;

    public static Expression Contains(IEnumerable<object?> values, Expression item)
    {
        var target = Nullable.GetUnderlyingType(item.Type) ?? item.Type;
        object?[] converted = [.. values.Select(v => v is null ? null : ValueReader.ConvertTo(v, target))];
        return Expression.Call(ContainsMethod, Expression.Constant(converted), Expression.Convert(item, typeof(object)));
    }
}
