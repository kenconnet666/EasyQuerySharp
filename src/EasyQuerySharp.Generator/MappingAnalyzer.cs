using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EasyQuerySharp.Generator;

/// <summary>
/// 映射与导航声明的编译期检查：<c>[MapFrom&lt;T&gt;]</c> DTO 的成员能否对上实体（EQ0003）、可空的对一导航映射到非空成员（EQ0005），
/// <c>[Navigate]</c> 的 ExtraFilter / ExtraMappingFilter 引用的成员（EQ0004）。规则与运行时的自动映射、元数据解析一致。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MappingAnalyzer : DiagnosticAnalyzer
{
    private const string MapFrom = "EasyQuerySharp.Metadata.MapFromAttribute<TEntity>";
    private const string MapPath = "EasyQuerySharp.Metadata.MapPathAttribute";
    private const string MapExpression = "EasyQuerySharp.Metadata.MapExpressionAttribute";
    private const string ColumnIgnore = "EasyQuerySharp.Metadata.ColumnIgnoreAttribute";
    private const string Navigate = "EasyQuerySharp.Metadata.NavigateAttribute";

    // RelationType 里对一的两个值：OneToOne、ManyToOne
    private const int OneToOne = 0;
    private const int ManyToOne = 1;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.UnmappedMember, Descriptors.InvalidExtraFilter, Descriptors.NonNullableOptionalNavigation);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeDto, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeNavigation, SymbolKind.Property);
    }

    private static void AnalyzeDto(SymbolAnalysisContext context)
    {
        var dto = (INamedTypeSymbol)context.Symbol;
        var mapFrom = dto.GetAttributes().FirstOrDefault(a => a.AttributeClass?.OriginalDefinition.ToDisplayString() == MapFrom);
        if (mapFrom?.AttributeClass?.TypeArguments.FirstOrDefault() is not INamedTypeSymbol entity) return;

        foreach (var property in dto.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.SetMethod is not { DeclaredAccessibility: Accessibility.Public }
                || Has(property, ColumnIgnore) || Has(property, MapExpression)) continue;
            var location = property.Locations.FirstOrDefault();
            if (Attribute(property, MapPath) is { } path)
            {
                if (path.ConstructorArguments.FirstOrDefault().Value is string text && ResolvePath(entity, text) is { } problem)
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.UnmappedMember, location, dto.Name, property.Name, entity.Name, problem));
                continue;
            }
            if (FindProperty(entity, property.Name) is not { } source)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.UnmappedMember, location, dto.Name, property.Name, entity.Name,
                    $"没有名为 {property.Name} 的属性（忽略大小写）；改名，或用 [MapPath] / [MapExpression] 指定，或标 [ColumnIgnore]"));
                continue;
            }
            if (IsOptionalToOne(source) && property.Type.IsReferenceType && property.NullableAnnotation == NullableAnnotation.NotAnnotated)
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.NonNullableOptionalNavigation, location, dto.Name, property.Name, entity.Name, source.Name));
        }
    }

    // 按 [MapPath] 的路径逐段在实体上找属性，经过集合时取元素类型；对不上时返回原因
    private static string? ResolvePath(ITypeSymbol entity, string path)
    {
        var current = entity;
        foreach (var segment in path.Split('.'))
        {
            if (FindProperty(current, segment) is not { } member) return $"路径 {path} 上 {current.Name} 没有 {segment}";
            current = ElementType(member.Type);
        }
        return null;
    }

    private static void AnalyzeNavigation(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (Attribute(property, Navigate) is not { } navigate) return;
        Check(context, property, navigate, "ExtraFilter", ElementType(property.Type));
        if (Named(navigate, "Mapping") is ITypeSymbol mapping) Check(context, property, navigate, "ExtraMappingFilter", mapping);
    }

    // 引用的成员要是本实体上的静态属性或字段，类型是 Expression<Func<参数, bool>>
    private static void Check(SymbolAnalysisContext context, IPropertySymbol property, AttributeData navigate, string argument, ITypeSymbol parameter)
    {
        if (Named(navigate, argument) is not string name) return;
        var member = property.ContainingType.GetMembers(name).FirstOrDefault(m => m is IPropertySymbol or IFieldSymbol);
        string? problem;
        if (member is null) problem = $"{property.ContainingType.Name} 上没有这个成员";
        else if (!member.IsStatic) problem = "要是静态属性或字段";
        else if (!IsPredicateOf(member is IPropertySymbol p ? p.Type : ((IFieldSymbol)member).Type, parameter))
            problem = $"类型要是 Expression<Func<{parameter.Name}, bool>>";
        else problem = null;
        if (problem is not null)
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.InvalidExtraFilter, property.Locations.FirstOrDefault(), property.Name, argument, name, problem));
    }

    // Expression<Func<parameter, bool>>
    private static bool IsPredicateOf(ITypeSymbol type, ITypeSymbol parameter)
    {
        if (type is not INamedTypeSymbol { Name: "Expression" } expression || expression.TypeArguments.Length != 1) return false;
        if (expression.TypeArguments[0] is not INamedTypeSymbol { Name: "Func" } func || func.TypeArguments.Length != 2) return false;
        return SymbolEqualityComparer.Default.Equals(func.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.None), parameter.WithNullableAnnotation(NullableAnnotation.None))
               && func.TypeArguments[1].SpecialType == SpecialType.System_Boolean;
    }

    // 没有标 Required 的对一导航，且实体上声明为可空
    private static bool IsOptionalToOne(IPropertySymbol property) =>
        Attribute(property, Navigate) is { } navigate
        && navigate.ConstructorArguments.FirstOrDefault().Value is int relation && (relation == OneToOne || relation == ManyToOne)
        && Named(navigate, "Required") is not true
        && property.NullableAnnotation == NullableAnnotation.Annotated;

    // 同名属性（忽略大小写），含基类
    private static IPropertySymbol? FindProperty(ITypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var found = current.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => !p.IsStatic && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (found is not null) return found;
        }
        return null;
    }

    // List<T> / IEnumerable<T> 等集合取元素类型，字符串与其余类型原样
    private static ITypeSymbol ElementType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String || type is not INamedTypeSymbol named) return type;
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T) return named.TypeArguments[0];
        var enumerable = named.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        return enumerable is null ? type : enumerable.TypeArguments[0];
    }

    private static object? Named(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
            if (argument.Key == name) return argument.Value.Value;
        return null;
    }

    private static AttributeData? Attribute(ISymbol symbol, string name) => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

    private static bool Has(ISymbol symbol, string name) => Attribute(symbol, name) is not null;
}
