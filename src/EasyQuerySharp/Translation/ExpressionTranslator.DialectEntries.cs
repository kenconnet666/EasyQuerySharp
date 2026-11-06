using System.Collections;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>方言入口（<c>Sql.MySql.*</c> / <c>Sql.Pg.*</c> / <c>Sql.Hana.*</c>）上的方法。</summary>
    private static bool IsDialectEntry(Type declaring) =>
        (declaring.IsGenericType ? declaring.GetGenericTypeDefinition() : declaring) is var d
        && (d == typeof(MySqlJson<>) || d == typeof(MySqlMatch) || d == typeof(PgJsonb<>) || d == typeof(PgArray) || d == typeof(PgFullText) || d == typeof(HanaText));

    // Sql.MySql.Json(col).Contains(..) 等：按 Java fork 的模板展开成片段，标上要求的方言（渲染期检查）
    private FragmentExpr TranslateDialectEntry(MethodCallExpression call)
    {
        if (call.Object is not MethodCallExpression { Method.DeclaringType: { } entry } view)
            throw Unsupported(call, "方言函数要直接写在 Sql.MySql.Xxx(列) / Sql.Pg.Xxx(列) / Sql.Hana.Xxx(列) 上");
        var name = $"Sql.{entry.Name}.{view.Method.Name}";
        var dialect = entry == typeof(Sql.MySql) ? typeof(MySqlDialect) : entry == typeof(Sql.Hana) ? typeof(HanaDialect) : typeof(PostgreSqlDialect);
        var args = new List<SqlExpr>();
        string template;
        if (view.Method.Name is nameof(Sql.MySql.Match) or nameof(Sql.Hana.Text))
        {
            args.AddRange(Items(view.Arguments[0]));
            if (args.Count == 0) throw Unsupported(call, $"{name} 至少要有一列");
            template = view.Method.Name == nameof(Sql.Hana.Text) ? HanaText(call, args) : MySqlMatch(call, args);
        }
        else
        {
            args.Add(Translate(StripConvert(view.Arguments[0])));
            template = (view.Method.Name, call.Method.Name) switch
            {
                (nameof(Sql.MySql.Json), _) => MySqlJson(call, args),
                (nameof(Sql.Pg.Jsonb), _) => PgJsonb(call, args),
                (nameof(Sql.Pg.Array), _) => PgArray(call, args),
                (nameof(Sql.Pg.FullText), _) => PgFullText(call, args),
                _ => throw Unsupported(call),
            };
        }
        return new FragmentExpr(template, args, isPredicate: call.Type == typeof(bool)) { RequiredDialect = dialect, EntryName = name };
    }

    private string MySqlJson(MethodCallExpression call, List<SqlExpr> args)
    {
        switch (call.Method.Name, call.Arguments.Count)
        {
            case (nameof(MySqlJson<object>.Contains), 1):
                return "JSON_CONTAINS({0}," + Next(args, call.Arguments[0]) + ")";
            case (nameof(MySqlJson<object>.Contains), 2):
                return "JSON_CONTAINS({0}," + Next(args, call.Arguments[0]) + "," + Next(args, call.Arguments[1]) + ")";
            case (nameof(MySqlJson<object>.Overlaps), _):
                return "JSON_OVERLAPS({0}," + Next(args, call.Arguments[0]) + ")";
            case (nameof(MySqlJson<object>.HasMember), 1):
                return Next(args, call.Arguments[0]) + " MEMBER OF({0})";
            case (nameof(MySqlJson<object>.HasMember), 2):
                return Next(args, call.Arguments[0]) + " MEMBER OF(JSON_EXTRACT({0}," + Next(args, call.Arguments[1]) + "))";
            case (nameof(MySqlJson<object>.ContainsPath) or nameof(MySqlJson<object>.ContainsAllPaths), _):
                var mode = call.Method.Name == nameof(MySqlJson<object>.ContainsPath) ? "one" : "all";
                var paths = NextAll(args, call.Arguments[0]);
                if (paths.Count == 0) throw Unsupported(call, "ContainsPath 至少要有一个 path");
                return "JSON_CONTAINS_PATH({0},'" + mode + "'," + string.Join(",", paths) + ")";
            case (nameof(MySqlJson<object>.Edit), _):
                var template = "{0}";
                foreach (var step in EditSteps(call, typeof(MySqlJsonEdit)))
                {
                    template = step.Method.Name switch
                    {
                        nameof(MySqlJsonEdit.Set) => "JSON_SET(" + template + "," + Next(args, step.Arguments[0]) + "," + Next(args, step.Arguments[1]) + ")",
                        nameof(MySqlJsonEdit.SetJson) => "JSON_SET(" + template + "," + Next(args, step.Arguments[0]) + ",CAST(" + Next(args, step.Arguments[1]) + " AS JSON))",
                        nameof(MySqlJsonEdit.ArrayAppend) => "JSON_ARRAY_APPEND(" + template + "," + Next(args, step.Arguments[0]) + "," + Next(args, step.Arguments[1]) + ")",
                        nameof(MySqlJsonEdit.Remove) => "JSON_REMOVE(" + template + "," + string.Join(",", NonEmpty(step, NextAll(args, step.Arguments[0]))) + ")",
                        _ => throw Unsupported(step),
                    };
                }
                return template;
            default:
                throw Unsupported(call);
        }
    }

    private string MySqlMatch(MethodCallExpression call, List<SqlExpr> args)
    {
        var columns = string.Join(",", Enumerable.Range(0, args.Count).Select(i => "{" + i + "}"));
        var mode = call.Arguments.Count > 1 ? (MySqlMatchMode)ClientEvaluator.Evaluate(call.Arguments[1])! : MySqlMatchMode.NaturalLanguage;
        var modeSql = mode switch
        {
            MySqlMatchMode.Boolean => " IN BOOLEAN MODE",
            MySqlMatchMode.QueryExpansion => " WITH QUERY EXPANSION",
            _ => " IN NATURAL LANGUAGE MODE",
        };
        return "MATCH(" + columns + ") AGAINST(" + Next(args, call.Arguments[0]) + modeSql + ")";
    }

    // 多列时列清单加括号：CONTAINS((a,b),?)；模糊检索的阈值写进 SQL（HANA 的 FUZZY 只收字面量）
    private string HanaText(MethodCallExpression call, List<SqlExpr> args)
    {
        if (call.Method.Name == nameof(EasyQuerySharp.HanaText.Score)) return "SCORE()";
        var columns = string.Join(",", Enumerable.Range(0, args.Count).Select(i => "{" + i + "}"));
        if (args.Count > 1) columns = "(" + columns + ")";
        var query = Next(args, call.Arguments[0]);
        if (call.Method.Name == nameof(EasyQuerySharp.HanaText.Contains)) return "CONTAINS(" + columns + "," + query + ")";
        var threshold = Convert.ToDouble(ClientEvaluator.Evaluate(call.Arguments[1]), System.Globalization.CultureInfo.InvariantCulture);
        if (threshold is not (>= 0 and <= 1))
            throw new ArgumentException($"Sql.Hana.Text(..).Fuzzy 的阈值要在 0 到 1 之间，实际是 {threshold}");
        return "CONTAINS(" + columns + "," + query + ",FUZZY(" + threshold.ToString(System.Globalization.CultureInfo.InvariantCulture) + "))";
    }

    private string PgJsonb(MethodCallExpression call, List<SqlExpr> args)
    {
        switch (call.Method.Name)
        {
            case nameof(PgJsonb<object>.Contains):
                return "({0})::jsonb @> (" + Next(args, call.Arguments[0]) + ")::jsonb";
            case nameof(PgJsonb<object>.ContainedBy):
                return "({0})::jsonb <@ (" + Next(args, call.Arguments[0]) + ")::jsonb";
            case nameof(PgJsonb<object>.HasKey):
                return "jsonb_exists(({0})::jsonb," + Next(args, call.Arguments[0]) + ")";
            case nameof(PgJsonb<object>.HasAnyKey) or nameof(PgJsonb<object>.HasAllKeys):
                var function = call.Method.Name == nameof(PgJsonb<object>.HasAnyKey) ? "jsonb_exists_any" : "jsonb_exists_all";
                return function + "(({0})::jsonb,ARRAY[" + string.Join(",", NonEmpty(call, NextAll(args, call.Arguments[0]))) + "]::text[])";
            case nameof(PgJsonb<object>.Edit):
                var template = "({0})::jsonb";
                foreach (var step in EditSteps(call, typeof(PgJsonbEdit)))
                {
                    template = step.Method.Name switch
                    {
                        nameof(PgJsonbEdit.Set) => "jsonb_set(" + template + ",(" + NextValue(args, PgPath(step)) + ")::text[],(" + Next(args, step.Arguments[1]) + ")::jsonb,true)",
                        nameof(PgJsonbEdit.Remove) => "(" + template + " #- (" + NextValue(args, PgPath(step)) + ")::text[])",
                        nameof(PgJsonbEdit.Merge) => "(" + template + " || (" + Next(args, step.Arguments[0]) + ")::jsonb)",
                        _ => throw Unsupported(step),
                    };
                }
                return template;
            default:
                throw Unsupported(call);
        }
    }

    private string PgArray(MethodCallExpression call, List<SqlExpr> args)
    {
        switch (call.Method.Name)
        {
            case nameof(EasyQuerySharp.PgArray.Has):
                return Next(args, call.Arguments[0]) + " = ANY({0})";
            case nameof(EasyQuerySharp.PgArray.HasAll) or nameof(EasyQuerySharp.PgArray.HasAny):
                var joiner = call.Method.Name == nameof(EasyQuerySharp.PgArray.HasAll) ? " AND " : " OR ";
                return "(" + string.Join(joiner, NonEmpty(call, NextAll(args, call.Arguments[0])).Select(v => v + " = ANY({0})")) + ")";
            case nameof(EasyQuerySharp.PgArray.Length):
                return "cardinality({0})";
            default:
                throw Unsupported(call);
        }
    }

    private string PgFullText(MethodCallExpression call, List<SqlExpr> args)
    {
        var config = call.Arguments.Count > 1 ? (string?)ClientEvaluator.Evaluate(call.Arguments[0]) : "simple";
        // 配置名直接写进 SQL（要与表达式索引逐字一致才走索引），只接受标识符
        if (config is null || !Regex.IsMatch(config, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new ArgumentException($"全文检索的配置名必须是标识符：{config}");
        var query = Next(args, call.Arguments[^1]);
        return call.Method.Name == nameof(EasyQuerySharp.PgFullText.Matches)
            ? $"to_tsvector('{config}',{{0}}) @@ websearch_to_tsquery('{config}',{query})"
            : $"ts_rank(to_tsvector('{config}',{{0}}),websearch_to_tsquery('{config}',{query}))";
    }

    // 编辑的 lambda：e => e.A().B()，从参数往外依次返回各步
    private static List<MethodCallExpression> EditSteps(MethodCallExpression call, Type editType)
    {
        var lambda = (LambdaExpression)StripConvert(call.Arguments[0]);
        var steps = new List<MethodCallExpression>();
        var e = lambda.Body;
        while (e is MethodCallExpression step && step.Method.DeclaringType == editType)
        {
            steps.Add(step);
            e = step.Object!;
        }
        if (e != lambda.Parameters[0]) throw Unsupported(lambda.Body, "编辑要写成 e => e.Set(..).Remove(..) 的链式调用");
        if (steps.Count == 0) throw Unsupported(lambda.Body, "编辑没有任何修改");
        steps.Reverse();
        return steps;
    }

    // PG 的路径数组参数：a.b → {a,b}
    private static string PgPath(MethodCallExpression step)
    {
        var path = ClientEvaluator.Evaluate(step.Arguments[0]) as string;
        if (string.IsNullOrWhiteSpace(path)) throw Unsupported(step, "jsonb 的 path 不能为空");
        return "{" + string.Join(",", path.Split('.')) + "}";
    }

    // 追加一个参数（值或列），返回它在模板里的占位
    private string Next(List<SqlExpr> args, Expression argument)
    {
        args.Add(Translate(StripConvert(argument)));
        return "{" + (args.Count - 1) + "}";
    }

    private static string NextValue(List<SqlExpr> args, object? value)
    {
        args.Add(Parameter(value, value?.GetType() ?? typeof(object)));
        return "{" + (args.Count - 1) + "}";
    }

    // params 数组的每一项各追加一个参数
    private List<string> NextAll(List<SqlExpr> args, Expression array) => Items(array).Select(item =>
    {
        args.Add(item);
        return "{" + (args.Count - 1) + "}";
    }).ToList();

    // params 参数：字面数组逐项翻译（可以是列），否则在客户端求值后逐项作为参数
    private List<SqlExpr> Items(Expression array)
    {
        if (StripConvert(array) is NewArrayExpression literal) return literal.Expressions.Select(e => Translate(StripConvert(e))).ToList();
        return ((IEnumerable?)ClientEvaluator.Evaluate(array) ?? Array.Empty<object>()).Cast<object?>()
            .Select(v => (SqlExpr)Parameter(v, v?.GetType() ?? typeof(object))).ToList();
    }

    private static List<string> NonEmpty(Expression owner, List<string> items) =>
        items.Count > 0 ? items : throw Unsupported(owner, "至少要有一个值");
}
