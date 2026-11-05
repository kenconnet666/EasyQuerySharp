using Microsoft.CodeAnalysis;

namespace EasyQuerySharp.Generator;

/// <summary>全部诊断的定义。编号 EQ0001 起，说明写在 docs/design/analyzers.md。</summary>
internal static class Descriptors
{
    private const string Category = "EasyQuerySharp";

    /// <summary>查询表达式里调用了用到查询参数、又不能翻译成 SQL 的方法。</summary>
    public static readonly DiagnosticDescriptor UntranslatableMethod = new(
        "EQ0001",
        "方法不能翻译成 SQL",
        "查询表达式里的 {0} 用到了查询参数，但不能翻译成 SQL：给静态方法标 [SqlFunction(\"模板\")]，或者在查询外算好再传进来",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "查询表达式会被翻译成 SQL。只有常用的 BCL 方法、Sql.* 与标了 [SqlFunction] 的方法能翻译；不引用查询参数的调用会在客户端先算好，不受影响。");

    /// <summary>查询表达式里用了空条件运算符 ?.（表达式树不支持）。</summary>
    public static readonly DiagnosticDescriptor NullPropagation = new(
        "EQ0002",
        "查询表达式里不能用空条件运算符",
        "表达式树不支持 ?.，改写成 {0}：导航取不到时 SQL 里本来就是 NULL",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "查询里的导航翻译成 LEFT JOIN 或子查询，取不到时值就是 NULL，不需要 ?. 来防空引用；写成 !. 即可。");

    /// <summary>[Projection] 方法的写法生成器处理不了。</summary>
    public static readonly DiagnosticDescriptor UnsupportedProjection = new(
        "EQ0006",
        "[Projection] 方法写法不受支持",
        "[Projection] 方法 {0} {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "生成器把 [Projection] 方法的方法体原样搬进一个表达式树 lambda：方法要是静态、非泛型，放在 partial 的非泛型类型里，方法体只有一个表达式（=> 表达式，或只有一句 return）。");
}
