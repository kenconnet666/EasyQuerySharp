using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace EasyQuerySharp.Materialization;

/// <summary>按元素类型新建 <c>List&lt;T&gt;</c>（集合导航、带出的子对象、平铺的值）。</summary>
internal static class TypedLists
{
    /// <summary>
    /// 元素类型都来自实体或 DTO 的属性：引用类型在 Native AOT 下共用规范实例化的代码；值类型元素的
    /// <c>List&lt;T&gt;</c> 由 EasyQuerySharp 的生成器在使用者的程序集里引用过一次（EntityRootGenerator）。
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "元素类型来自实体 / DTO 的属性，实例化在编译期已有，见方法说明")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "List<T> 的无参构造函数随实例化保留")]
    public static IList New(Type element) => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
}
