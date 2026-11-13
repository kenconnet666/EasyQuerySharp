namespace EasyQuerySharp;

/// <summary>
/// 裁剪 / Native AOT 分析器报出、经过核对可以压下的几类写法（UnconditionalSuppressMessage 的理由），见 docs/decisions/0008-native-aot.md。
/// 每一类都由 tests/EasyQuerySharp.Tests.Aot 在 Native AOT 下实际跑过。
/// </summary>
internal static class AotJustification
{
    /// <summary>反射读使用者的类型（实体、DTO、值对象、中间表）。</summary>
    public const string Rooted = "实体、DTO、值对象与中间表的成员由 EasyQuerySharp 的生成器（EntityRootGenerator）用 DynamicDependency 保留";

    /// <summary>按类型取泛型标记方法的 MethodInfo。</summary>
    public const string Marker = "标记方法只出现在交给翻译器的表达式树里，不会执行，不需要为它生成代码";

    /// <summary>用非泛型的 Expression.Lambda 拼 lambda（运行时定出委托类型）。</summary>
    public const string Lambda = "拼出来的 lambda 只交给翻译器、不编译；委托类型的共享实例化由 AotTemplates 保留";

    /// <summary>按实体类型构造泛型类型。</summary>
    public const string ReferenceGeneric = "类型实参是实体、DTO 等引用类型，Native AOT 用共享的规范实例化";

    /// <summary>按值类型构造泛型（值类型元素的 List、按名字存的枚举的 EnumNameConverter）。</summary>
    public const string GeneratedInstantiation = "值类型的实例化由 EasyQuerySharp 的生成器（EntityRootGenerator）在使用者的程序集里引用过一次，编译期已有";

    /// <summary>按名字取 BCL 方法。</summary>
    public const string TrimmedBcl = "裁剪掉的 BCL 方法取到 null 时跳过：查询里本来也写不出没被调用的方法";

    /// <summary>只在能生成代码时（JIT）走到的分支。</summary>
    public const string DynamicOnly = "只在 RuntimeFeature.IsDynamicCodeSupported 为 true（JIT）时走到，Native AOT 下走不带泛型的分支";
}
