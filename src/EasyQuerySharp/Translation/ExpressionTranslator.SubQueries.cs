using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Querying;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>
    /// 把表达式里的查询（如 <c>db.Query&lt;Blog&gt;().Where(b => b.Id == t.Id)</c>）还原成 <see cref="QueryState"/>：
    /// 不依赖参数的部分在客户端求值；依赖外层参数的调用（关联子查询）在客户端"回放"——
    /// 用反射在真实的查询对象上依次调用这些方法，lambda 原样传入（它们引用的外层参数在翻译子查询时由外层作用域解析）。
    /// </summary>
    internal static QueryState ReplayQuery(Expression expression)
    {
        if (!ClientEvaluator.MustTranslate(expression)) return StateOf(ClientEvaluator.Evaluate(expression), expression);
        if (StripConvert(expression) is not MethodCallExpression call || call.Object is null)
            throw Unsupported(expression, "子查询只能由查询对象的方法链构成");

        var target = QueryObject(ReplayQuery(call.Object), call.Object.Type);
        var args = call.Arguments.Select(a => StripConvert(a) is LambdaExpression l ? l : ClientEvaluator.Evaluate(a)).ToArray();
        object? result;
        try
        {
            result = call.Method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
        return StateOf(result, expression);
    }

    /// <summary>翻译一个子查询：外层的参数绑定对子查询可见。</summary>
    internal (SelectQuery Query, ProjectionCompiler Projection) TranslateSubQuery(Expression expression)
        => QueryCompiler.CompileSelect(ReplayQuery(expression), _scope, Context);

    private SqlExpr TranslateExists(Expression queryExpression, bool negated)
    {
        var frame = QueryCompiler.Build(ReplayQuery(queryExpression) with { Selector = null }, _scope, Context);
        frame.Query.Columns.Add(new SelectColumn(LiteralExpr.One, null));
        return new ExistsExpr(frame.Query, negated);
    }

    private static QueryState StateOf(object? value, Expression source) =>
        value is IQueryStateAccessor q ? q.State : throw Unsupported(source, "需要一个 EasyQuerySharp 查询对象");

    // 由状态重建与表达式类型相同的查询对象（各查询类型都有 internal 的 (QueryState) 构造函数）
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = AotJustification.Rooted)]
    private static object QueryObject(QueryState state, Type type) =>
        Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [state], null)
        ?? throw new InvalidOperationException($"无法创建 {type.Name}");
}
