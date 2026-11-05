using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EasyQuerySharp.Generator;

/// <summary>
/// 查询表达式的检查（EQ0001、EQ0002）：在编译期指出运行时才会失败的写法。
/// 每个最外层查询 lambda 分析一次，里面嵌套的 lambda（<c>u.Posts.Any(p =&gt; …)</c>）一起看。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryExpressionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.UntranslatableMethod, Descriptors.NullPropagation);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
    }

    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
    {
        var lambda = (LambdaExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        if (!QueryLambdas.IsOutermostQueryLambda(lambda, model)) return;

        var parameters = QueryLambdas.Parameters(lambda, model);
        foreach (var node in lambda.DescendantNodes())
        {
            switch (node)
            {
                case InvocationExpressionSyntax invocation when IsUntranslatable(invocation, model, parameters, out var name):
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.UntranslatableMethod, invocation.GetLocation(), name));
                    break;
                // 嵌套的 ?. 只报最外层一次，它的改写建议已经把里面的一起改了
                case ConditionalAccessExpressionSyntax access when !access.Ancestors().TakeWhile(a => a != lambda).OfType<ConditionalAccessExpressionSyntax>().Any():
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.NullPropagation, access.OperatorToken.GetLocation(), NullForgiving.Rewrite(access).ToString()));
                    break;
            }
        }
    }

    // 用到查询参数、又不是 BCL / 核心库 / [SqlFunction] / [Projection] 的方法：翻译器认不得，运行时报"不支持翻译成 SQL"
    private static bool IsUntranslatable(InvocationExpressionSyntax invocation, SemanticModel model, System.Collections.Generic.HashSet<IParameterSymbol> parameters, out string name)
    {
        name = "";
        var info = model.GetSymbolInfo(invocation);
        if ((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is not IMethodSymbol method) return false;
        if (QueryLambdas.IsCore(method) || IsFramework(method) || HasSqlFunction(method)) return false;
        var usesParameter = invocation.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Any(id => model.GetSymbolInfo(id).Symbol is IParameterSymbol p && parameters.Contains(p));
        if (!usesParameter) return false;
        name = method.ContainingType.Name + "." + method.Name;
        return true;
    }

    private static bool IsFramework(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToDisplayString() ?? "";
        return ns == "System" || ns.StartsWith("System.", System.StringComparison.Ordinal) || ns.StartsWith("Microsoft.", System.StringComparison.Ordinal);
    }

    private static bool HasSqlFunction(IMethodSymbol method) =>
        (method.ReducedFrom ?? method).GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() is "EasyQuerySharp.SqlFunctionAttribute" or "EasyQuerySharp.ProjectionAttribute");
}
