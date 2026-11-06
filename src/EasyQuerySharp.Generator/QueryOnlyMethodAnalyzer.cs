using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EasyQuerySharp.Generator;

/// <summary>
/// 只能写在查询表达式里的方法在普通代码里被调用（EQ0007）：<c>Sql.*</c>（<c>Sql.Identifier</c> 除外）与 <c>[SqlFunction]</c> 方法
/// 只是给翻译器认的标记，直接执行会抛异常。写在任意一层转换成 <c>Expression&lt;…&gt;</c> 的 lambda 里、
/// <c>[Projection]</c> 方法体里（会被搬进表达式树）或 LINQ 查询语法里都不报。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryOnlyMethodAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.QueryOnlyMethod);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol called) return;
        var method = called.ReducedFrom ?? called;
        if (!IsQueryOnly(method) || InExpressionTree(invocation, model) || InProjectionMethod(invocation, model)) return;
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.QueryOnlyMethod, invocation.GetLocation(), method.ContainingType.Name + "." + method.Name));
    }

    private static bool IsQueryOnly(IMethodSymbol method)
    {
        if (method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "EasyQuerySharp.SqlFunctionAttribute")) return true;
        // Sql 与它的嵌套入口（Sql.Over、Sql.MySql …）；Sql.Identifier 在原生 SQL 的插值里用，本来就在查询外执行
        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
            if (type is { Name: "Sql", ContainingType: null } && type.ContainingNamespace.ToDisplayString() == "EasyQuerySharp" && QueryLambdas.IsCore(type))
                return method.Name != "Identifier";
        return false;
    }

    // 外层任意一个 lambda 转换成了表达式树：整棵树交给翻译器，不会执行；查询语法的子句同样编译成表达式树
    private static bool InExpressionTree(SyntaxNode node, SemanticModel model) =>
        node.Ancestors().Any(a => a is QueryExpressionSyntax
            || (a is LambdaExpressionSyntax lambda && model.GetTypeInfo(lambda).ConvertedType is INamedTypeSymbol { Name: "Expression", ContainingNamespace: var ns }
                && ns.ToDisplayString() == "System.Linq.Expressions"));

    private static bool InProjectionMethod(SyntaxNode node, SemanticModel model) =>
        node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } declaration
        && model.GetDeclaredSymbol(declaration) is { } method
        && method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "EasyQuerySharp.ProjectionAttribute");
}
