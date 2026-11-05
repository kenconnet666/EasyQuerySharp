using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EasyQuerySharp.Generator;

/// <summary>
/// 识别"查询表达式"：作为表达式树（<c>Expression&lt;…&gt;</c>）传给 EasyQuerySharp 方法的 lambda，
/// 如 <c>db.Query&lt;User&gt;().Where(u =&gt; …)</c>。只取最外层的那个，里面嵌套的 lambda 算同一棵树。
/// </summary>
internal static class QueryLambdas
{
    private const string CoreAssembly = "EasyQuerySharp";

    /// <summary><paramref name="lambda"/> 是不是最外层的查询表达式。</summary>
    public static bool IsOutermostQueryLambda(LambdaExpressionSyntax lambda, SemanticModel model) =>
        IsQueryLambda(lambda, model) && !lambda.Ancestors().OfType<LambdaExpressionSyntax>().Any(outer => IsQueryLambda(outer, model));

    /// <summary>节点所在的最外层查询表达式；不在查询表达式里时为 null。</summary>
    public static LambdaExpressionSyntax? Enclosing(SyntaxNode node, SemanticModel model) =>
        node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().LastOrDefault(l => IsQueryLambda(l, model));

    /// <summary>查询表达式里所有 lambda 声明的参数（含嵌套的 <c>u.Posts.Any(p =&gt; …)</c> 里的 p）。</summary>
    public static HashSet<IParameterSymbol> Parameters(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        var result = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        foreach (var inner in lambda.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>())
            if (model.GetSymbolInfo(inner).Symbol is IMethodSymbol method)
                foreach (var p in method.Parameters) result.Add(p);
        return result;
    }

    /// <summary>方法是否来自 EasyQuerySharp 核心程序集。</summary>
    public static bool IsCore(ISymbol symbol) => symbol.ContainingAssembly?.Name == CoreAssembly;

    private static bool IsQueryLambda(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        if (model.GetTypeInfo(lambda).ConvertedType is not INamedTypeSymbol { Name: "Expression", ContainingNamespace: var ns } || ns.ToDisplayString() != "System.Linq.Expressions")
            return false;
        return lambda.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } }
               && model.GetSymbolInfo(invocation) is var info
               && (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is IMethodSymbol method
               && IsCore(method);
    }
}
