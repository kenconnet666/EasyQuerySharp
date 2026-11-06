using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 归一的内联器（见架构文档）：把 <see cref="Sql.Project{TSource, TResult}(TSource, Expression{Func{TSource, TResult}})"/> 与
/// <see cref="ProjectionAttribute"/> 方法展开成它们的方法体，之后的翻译、规划只看到普通的投影表达式。
/// </summary>
internal sealed class ProjectionInliner : ExpressionVisitor
{
    private static readonly ProjectionInliner Instance = new();

    /// <summary>展开 <paramref name="expression"/> 里所有的可复用投影；没有时原样返回。</summary>
    public static T Expand<T>(T expression) where T : Expression => (T)Instance.Visit(expression);

    /// <summary>
    /// 展开单个调用（翻译器遇到没有预先展开的调用时用）：是可复用投影时返回展开后的表达式，否则 null。
    /// </summary>
    public static Expression? TryExpand(MethodCallExpression call) => Inline(call) is { } inlined ? Instance.Visit(inlined) : null;

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node) => Inline(node) is { } inlined ? Visit(inlined) : base.VisitMethodCall(node);


    /// <summary>
    /// 取投影结果上的成员（<c>c.Bank!.ToVo().Name</c>）：先内联投影，再把 <c>new BankVo(b.Id, b.Name).Name</c> 折回构造参数 / 成员赋值 <c>b.Name</c>。
    /// 不涉及投影、也折不了时返回 null。
    /// </summary>
    public static Expression? FoldMember(MemberExpression member)
    {
        var inner = member.Expression;
        var expanded = inner is MethodCallExpression call && Inline(call) is { } inlined ? inlined : null;
        var source = expanded ?? inner;
        return Fold(source, member.Member) ?? (expanded is null ? null : Expression.MakeMemberAccess(expanded, member.Member));
    }

    // new T(..) / new T { .. } 上取成员：匿名类型按成员顺序，记录类型按同名构造参数，成员初始化按赋值
    private static Expression? Fold(Expression? source, MemberInfo member)
    {
        switch (source)
        {
            case MemberInitExpression init:
                return init.Bindings.OfType<MemberAssignment>().FirstOrDefault(b => b.Member.Name == member.Name)?.Expression ?? Fold(init.NewExpression, member);
            case NewExpression { Constructor: { } constructor } created:
                if (created.Members is { } members)
                    for (var i = 0; i < members.Count; i++)
                        if (members[i].Name == member.Name) return created.Arguments[i];
                var parameters = constructor.GetParameters();
                for (var i = 0; i < parameters.Length; i++)
                    if (string.Equals(parameters[i].Name, member.Name, StringComparison.OrdinalIgnoreCase)) return created.Arguments[i];
                return null;
            default:
                return null;
        }
    }
    private static Expression? Inline(MethodCallExpression call)
    {
        if (call.Method.DeclaringType == typeof(Sql) && call.Method.Name == nameof(Sql.Project))
        {
            var map = (LambdaExpression)ClientEvaluator.Evaluate(call.Arguments[1])!;
            // 集合：等同于 Select(map)；单个值：方法体里的参数换成这个值
            if (call.Method.ReturnType.IsGenericType && call.Method.ReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return Expression.Call(SelectMethod.MakeGenericMethod(map.Parameters[0].Type, map.ReturnType), call.Arguments[0], map);
            return new ExpressionTranslator.ParameterReplacer(map.Parameters[0], call.Arguments[0]).Visit(map.Body);
        }
        if (!call.Method.IsDefined(typeof(ProjectionAttribute), inherit: false)) return null;
        var expression = ProjectionRegistry.Find(call.Method)
                         ?? throw new InvalidOperationException(
                             $"{call.Method.DeclaringType?.Name}.{call.Method.Name} 标了 [Projection]，但没有登记它的表达式：所在项目没有运行生成器。生成器随 EasyQuerySharp 包分发；从源码引用核心的项目要以 Analyzer 方式引用 EasyQuerySharp.Generator");
        var arguments = call.Object is null ? call.Arguments : [call.Object, .. call.Arguments];
        Expression body = expression.Body;
        for (var i = 0; i < expression.Parameters.Count; i++) body = new ExpressionTranslator.ParameterReplacer(expression.Parameters[i], arguments[i]).Visit(body);
        return body;
    }

    private static readonly MethodInfo SelectMethod = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.Select) && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);
}
