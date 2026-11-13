using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EasyQuerySharp.Generator;

/// <summary>
/// 裁剪 / Native AOT 下 EasyQuerySharp 运行时要用、编译期又看不出来的东西（见 docs/decisions/0008-native-aot.md）：
/// <list type="bullet">
/// <item>实体、DTO、值对象、中间表的成员：EasyQuerySharp 反射读它们的属性、特性与构造函数，而使用者的代码往往只读属性、从不写，
/// 裁剪器看不到这些用法就会把 setter、构造函数删掉。生成的方法用 <c>[DynamicDependency]</c> 把成员全部保留。</item>
/// <item>按值类型实例化的泛型：属性是值类型的集合（<c>List&lt;decimal&gt;</c>）、按名字存的枚举（<c>EnumNameConverter&lt;Level&gt;</c>）。
/// 运行时按类型构造它们需要编译期已有这个实例化，生成的代码引用一次（不会执行）。</item>
/// </list>
/// 类型从这些地方找：EasyQuerySharp 的泛型方法与泛型类型的类型实参（<c>db.Query&lt;User&gt;()</c>、<c>Select&lt;UserDto&gt;()</c>、
/// 推断出来的 <c>db.Insert(user)</c>、<c>[MapFrom&lt;User&gt;]</c>）、标了 <c>[Table]</c> 的类，再顺着属性的类型
/// （导航、值对象、集合元素）与 <c>[Navigate(Mapping = …)]</c> 一路找全。JIT 下生成的代码什么也不做。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EntityRootGenerator : IIncrementalGenerator
{
    private const string TableAttribute = "EasyQuerySharp.Metadata.TableAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tables = context.SyntaxProvider.ForAttributeWithMetadataName(TableAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => Closure([(ITypeSymbol)ctx.TargetSymbol], ctx.SemanticModel.Compilation));
        var used = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is GenericNameSyntax || node is InvocationExpressionSyntax,
            static (ctx, ct) => Closure(TypeArguments(ctx.SemanticModel.GetSymbolInfo(ctx.Node, ct).Symbol), ctx.SemanticModel.Compilation));
        var all = tables.Collect().Combine(used.Collect());
        context.RegisterSourceOutput(all.Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "")),
            static (spc, input) => Emit(spc, input.Left.Left.Concat(input.Left.Right).SelectMany(t => t), input.Right));
    }

    // 符号来自 EasyQuerySharp 的包时取它的类型实参：泛型方法（含推断出来的）与泛型类型
    private static IEnumerable<ITypeSymbol> TypeArguments(ISymbol? symbol) => symbol switch
    {
        IMethodSymbol { IsGenericMethod: true } method when IsLibrary(method.ContainingAssembly) => method.TypeArguments,
        IMethodSymbol method when IsLibrary(method.ContainingAssembly) && method.ContainingType.IsGenericType => method.ContainingType.TypeArguments,
        INamedTypeSymbol { IsGenericType: true } type when IsLibrary(type.ContainingAssembly) => type.TypeArguments,
        _ => [],
    };

    // 按程序集认：使用者的命名空间也可能以 EasyQuerySharp 开头
    private static bool IsLibrary(IAssemblySymbol? assembly) =>
        assembly?.Name is "EasyQuerySharp" or "EasyQuerySharp.DependencyInjection" or "EasyQuerySharp.Caching";

    // 生成的一行：R 开头的保留成员（写进 DynamicDependency），E 开头的引用一次的表达式（写成 _ = 表达式;）
    private static string Root(string dependency) => "R" + dependency;

    private static string Reference(string expression) => "E" + expression;

    // 从起点出发，顺着属性类型与多对多中间表找全。保留成员：生成的类看得见它时写 typeof，看不见（private 的嵌套类型）时写文档 ID 格式的名字与程序集名；
    // 属性里的值类型集合与按名字存的枚举另外引用一次实例化。结果是字符串，可以比较，增量生成能缓存
    private static ImmutableArray<string> Closure(IEnumerable<ITypeSymbol> starts, Compilation compilation)
    {
        var found = new HashSet<string>();
        var result = new List<string>();
        var pending = new Stack<ITypeSymbol>(starts);
        while (pending.Count > 0)
        {
            if (Unwrap(pending.Pop()) is not { } type || !IsUserType(type)) continue;
            if (!found.Add(MetadataName(type))) continue;
            result.Add(Root(compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
                ? "typeof(" + Name(type) + ")"
                : "\"" + MetadataName(type) + "\", \"" + type.ContainingAssembly!.Name + "\""));
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic))
            {
                pending.Push(property.Type);
                result.AddRange(Instantiations(property.Type, compilation));
                foreach (var attribute in property.GetAttributes())
                foreach (var argument in attribute.NamedArguments)
                    if (argument.Key == "Mapping" && argument.Value.Value is ITypeSymbol mapping)
                        pending.Push(mapping);
            }
            if (type.BaseType is { } baseType) pending.Push(baseType);
        }
        return [.. result];
    }

    // 属性类型需要的值类型实例化：值类型元素的集合按 List<元素> 构造；枚举（可能按名字存）要 EnumNameConverter<枚举>
    private static IEnumerable<string> Instantiations(ITypeSymbol type, Compilation compilation)
    {
        if (Element(type) is { IsValueType: true } element && compilation.IsSymbolAccessibleWithin(element, compilation.Assembly))
            yield return Reference("new global::System.Collections.Generic.List<" + Name(element) + ">()");
        if ((Element(type) ?? type) is var single && NonNullable(single) is { TypeKind: TypeKind.Enum } enumType
            && compilation.IsSymbolAccessibleWithin(enumType, compilation.Assembly))
            yield return Reference("new global::EasyQuerySharp.Metadata.EnumNameConverter<" + Name(enumType) + ">()");
    }

    private static ITypeSymbol NonNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

    // 集合（数组、实现 IEnumerable<T> 的泛型类型，字符串除外）的元素类型；不是集合时为 null
    private static ITypeSymbol? Element(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => array.ElementType,
        INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } generic
            when generic.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                 || generic.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T) => generic.TypeArguments[0],
        _ => null,
    };

    // 可空值类型、数组、集合取元素类型
    private static INamedTypeSymbol? Unwrap(ITypeSymbol type) => type switch
    {
        INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable => Unwrap(nullable.TypeArguments[0]),
        _ when Element(type) is { } element => Unwrap(element),
        INamedTypeSymbol named => named,
        _ => null,
    };

    // 使用者自己的类与结构：不是框架、不是 EasyQuerySharp 自己的、不是匿名类型与泛型（匿名类型的成员由表达式树本身引用）
    private static bool IsUserType(INamedTypeSymbol type) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct
        && !type.IsAnonymousType && !type.IsGenericType && type.SpecialType == SpecialType.None
        && type.ContainingAssembly is { } assembly
        && !assembly.Name.StartsWith("System", System.StringComparison.Ordinal) && !assembly.Name.StartsWith("Microsoft", System.StringComparison.Ordinal)
        && assembly.Name != "netstandard" && assembly.Name != "mscorlib"
        && !IsLibrary(assembly);

    private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType) name = outer.MetadataName + "." + name;
        return type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + name : name;
    }

    // 类名带上程序集名：互相 InternalsVisibleTo 的程序集里各有一个，不能重名
    private static string Identifier(string text) => new(text.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    private static void Emit(SourceProductionContext context, IEnumerable<string> lines, string assemblyName)
    {
        var distinct = lines.Distinct().OrderBy(d => d, System.StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return;
        var source = new StringBuilder("// <auto-generated/>\n")
            .Append("namespace EasyQuerySharp.Generated;\n\n")
            .Append("/// <summary>裁剪 / Native AOT 下保留交给 EasyQuerySharp 的类型的成员，引用要用到的泛型实例化（EasyQuerySharp 的生成器产出）。</summary>\n")
            .Append("internal static class EasyQuerySharpRoots_").Append(Identifier(assemblyName)).Append("\n{\n")
            .Append("    [global::System.Runtime.CompilerServices.ModuleInitializer]\n");
        foreach (var line in distinct.Where(l => l[0] == 'R'))
            source.Append("    [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, ")
                .Append(line, 1, line.Length - 1).Append(")]\n");
        source.Append("    internal static void Root()\n    {\n");
        var references = distinct.Where(l => l[0] == 'E').ToList();
        if (references.Count > 0)
        {
            // 开关永远不会打开：只让编译器把这些实例化当成用到了
            source.Append("        if (!global::System.AppContext.TryGetSwitch(\"EasyQuerySharp.Generated.Run\", out var run) || !run) return;\n");
            foreach (var line in references) source.Append("        _ = ").Append(line, 1, line.Length - 1).Append(";\n");
        }
        source.Append("    }\n}\n");
        context.AddSource("EasyQuerySharpRoots.g.cs", source.ToString());
    }
}
