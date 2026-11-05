using System.Globalization;
using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    // Sql.Over.Xxx(..., w => w.PartitionBy(...).OrderBy(...)) → WindowExpr
    private WindowExpr TranslateWindow(MethodCallExpression call)
    {
        var args = call.Arguments;
        var spec = (LambdaExpression)StripConvert(args[^1]);
        var values = args.Take(args.Count - 1).ToList();
        var (function, compact) = call.Method.Name switch
        {
            nameof(Sql.Over.RowNumber) => (Head("ROW_NUMBER()"), false),
            nameof(Sql.Over.Rank) => (Head("RANK()"), false),
            nameof(Sql.Over.DenseRank) => (Head("DENSE_RANK()"), false),
            nameof(Sql.Over.PercentRank) => (Head("PERCENT_RANK()"), false),
            nameof(Sql.Over.CumeDist) => (Head("CUME_DIST()"), false),
            nameof(Sql.Over.Ntile) => (Head("NTILE(" + IntLiteral(values[0]) + ")"), false),
            nameof(Sql.Over.Count) when values.Count == 0 => (Head("COUNT(*)"), false),
            nameof(Sql.Over.Count) or nameof(Sql.Over.Sum) or nameof(Sql.Over.Avg) or nameof(Sql.Over.Max) or nameof(Sql.Over.Min) =>
                (Head(call.Method.Name.ToUpperInvariant() + "({0})", values[0]), false),
            // 取值函数：偏移量写成字面量，默认值作为参数或列（与 Java 的 offset().prev / next 一致）
            nameof(Sql.Over.Lag) or nameof(Sql.Over.Lead) =>
                (Head(call.Method.Name.ToUpperInvariant() + "({0}, " + IntLiteral(values[1]) + (values.Count > 2 ? ", {1})" : ")"), [values[0], .. values.Skip(2)]), true),
            nameof(Sql.Over.FirstValue) => (Head("FIRST_VALUE({0})", values[0]), true),
            nameof(Sql.Over.LastValue) => (Head("LAST_VALUE({0})", values[0]), true),
            nameof(Sql.Over.NthValue) => (Head("NTH_VALUE({0}, " + IntLiteral(values[1]) + ")", values[0]), true),
            _ => throw Unsupported(call),
        };

        var partitions = new List<SqlExpr>();
        var orders = new List<OrderByItem>();
        string? frame = null;
        foreach (var step in WindowSteps(spec))
        {
            switch (step.Method.Name)
            {
                case nameof(Window.PartitionBy):
                    var keys = StripConvert(step.Arguments[0]) is NewArrayExpression array ? array.Expressions : [step.Arguments[0]];
                    partitions.AddRange(keys.Select(k => Translate(StripConvert(k))));
                    break;
                case nameof(Window.OrderBy):
                case nameof(Window.OrderByDescending):
                    orders.Add(OrderItem(step.Arguments[0], step.Method.Name == nameof(Window.OrderByDescending)));
                    break;
                case nameof(Window.Rows):
                case nameof(Window.Range):
                    var start = (WindowBound)ClientEvaluator.Evaluate(step.Arguments[0])!;
                    var end = (WindowBound)ClientEvaluator.Evaluate(step.Arguments[1])!;
                    frame = step.Method.Name.ToUpperInvariant() + " BETWEEN " + start.Sql + " AND " + end.Sql;
                    break;
                default:
                    throw Unsupported(step);
            }
        }
        return new WindowExpr(function, partitions, orders, frame, compact);

        FragmentExpr Head(string template, params Expression[] arguments) => new(template, arguments.Select(a => Translate(StripConvert(a))).ToList());
    }

    // w => w.A().B().C()：从参数往外依次返回 A、B、C
    private static List<MethodCallExpression> WindowSteps(LambdaExpression spec)
    {
        var steps = new List<MethodCallExpression>();
        var e = spec.Body;
        while (e is MethodCallExpression call && call.Method.DeclaringType == typeof(Window))
        {
            steps.Add(call);
            e = call.Object!;
        }
        if (e != spec.Parameters[0]) throw Unsupported(spec.Body, "窗口要写成 w => w.PartitionBy(...).OrderBy(...) 的链式调用");
        steps.Reverse();
        return steps;
    }

    /// <summary>
    /// 一个排序项（查询、窗口、集合导航里的排序共用）：<c>Sql.NullsFirst</c> / <c>Sql.NullsLast</c> 只标记 NULL 的位置，排序的是里面的值。
    /// 键里的参数要先绑定好。
    /// </summary>
    internal OrderByItem OrderItem(Expression key, bool descending, bool raw = false)
    {
        var stripped = StripConvert(key);
        if (stripped is MethodCallExpression { Method.Name: nameof(Sql.NullsFirst) or nameof(Sql.NullsLast) } mark && mark.Method.DeclaringType == typeof(Sql))
            return new OrderByItem(Translate(StripConvert(mark.Arguments[0])), descending, raw, mark.Method.Name == nameof(Sql.NullsLast) ? NullsOrder.Last : NullsOrder.First);
        return new OrderByItem(Translate(stripped), descending, raw);
    }

    private static string IntLiteral(Expression e) =>
        Convert.ToInt64(ClientEvaluator.Evaluate(e), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
}
