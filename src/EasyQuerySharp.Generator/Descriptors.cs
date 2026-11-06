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

    /// <summary>[MapFrom] DTO 的成员在实体上对不上。</summary>
    public static readonly DiagnosticDescriptor UnmappedMember = new(
        "EQ0003",
        "DTO 成员在实体上找不到对应",
        "{0}.{1} 在 {2} 上找不到对应：{3}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "[MapFrom<T>] 的 DTO 按名字（忽略大小写）对应实体的属性，[MapPath] 按路径逐段对应；对不上的成员在查询结果里永远是默认值。改名、加 [MapPath] / [MapExpression]，或标 [ColumnIgnore]。");

    /// <summary>导航的 ExtraFilter / ExtraMappingFilter 引用不对。</summary>
    public static readonly DiagnosticDescriptor InvalidExtraFilter = new(
        "EQ0004",
        "导航的固定条件引用不对",
        "导航 {0} 的 {1} = \"{2}\"：{3}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ExtraFilter 要引用本实体上返回 Expression<Func<目标, bool>> 的静态属性或字段，ExtraMappingFilter 的参数是中间表实体；用 nameof 引用，运行时才不会找不到。");

    /// <summary>对一导航在 DTO 里声明为非空，但关系可空。</summary>
    public static readonly DiagnosticDescriptor NonNullableOptionalNavigation = new(
        "EQ0005",
        "可空的对一导航映射到了非空的 DTO 成员",
        "{0}.{1} 声明为非空，但 {2}.{3} 是可空的对一导航（没有标 Required）：取不到时会是 null",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "对一导航没有标 Required 时按 LEFT JOIN 或带出查询取，可能取不到；DTO 成员声明成可空（加 ?），或在导航上标 Required = true。");

    /// <summary>[Projection] 方法的写法生成器处理不了。</summary>
    public static readonly DiagnosticDescriptor UnsupportedProjection = new(
        "EQ0006",
        "[Projection] 方法写法不受支持",
        "[Projection] 方法 {0} {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "生成器把 [Projection] 方法的方法体原样搬进一个表达式树 lambda：方法要是静态、非泛型，放在 partial 的非泛型类型里，方法体只有一个表达式（=> 表达式，或只有一句 return）。");

    /// <summary>只能写在查询表达式里的方法在普通代码里被调用。</summary>
    public static readonly DiagnosticDescriptor QueryOnlyMethod = new(
        "EQ0007",
        "只能在查询表达式里调用",
        "{0} 只能写在查询表达式里（传给 Where、Select 等的 lambda），在这里执行会抛出 InvalidOperationException",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Sql.* 与标了 [SqlFunction] 的方法只是给翻译器认的标记，没有可执行的实现；它们只能出现在会被翻译成 SQL 的表达式树里。");
}
