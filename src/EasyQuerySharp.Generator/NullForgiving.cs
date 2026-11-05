using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EasyQuerySharp.Generator;

/// <summary>
/// 给 EQ0002 生成改写建议：查询表达式里的 <c>a?.b</c> 写成 <c>a!.b</c>（导航取不到时 SQL 里本来就是 NULL）。
/// 分析器程序集不能引用 Workspaces（RS1038），所以只在诊断消息里给出改写后的写法，不做自动修复。
/// </summary>
internal static class NullForgiving
{
    /// <summary><c>a?.b.c</c> → <c>a!.b.c</c>；<c>a?[i]</c> → <c>a![i]</c>。嵌套的 <c>a?.b?.c</c> 一次改完。</summary>
    public static ExpressionSyntax Rewrite(ConditionalAccessExpressionSyntax access)
    {
        var target = SyntaxFactory.PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, access.Expression.WithoutTrivia());
        return (ExpressionSyntax)new BindingReplacer(target).Visit(access.WhenNotNull)!;
    }

    // 把 WhenNotNull 里最前面的 .b / [i]（MemberBinding / ElementBinding）接到 a! 后面；里面再有 ?. 的继续改写
    private sealed class BindingReplacer(ExpressionSyntax target) : CSharpSyntaxRewriter
    {
        private bool _done;

        public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node) =>
            Rewrite(node.WithExpression((ExpressionSyntax)Visit(node.Expression)!));

        public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
        {
            if (_done) return node;
            _done = true;
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, target, node.Name);
        }

        public override SyntaxNode? VisitElementBindingExpression(ElementBindingExpressionSyntax node)
        {
            if (_done) return node;
            _done = true;
            return SyntaxFactory.ElementAccessExpression(target, node.ArgumentList);
        }
    }
}
