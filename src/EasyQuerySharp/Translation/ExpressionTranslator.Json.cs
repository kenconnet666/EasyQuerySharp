using System.Globalization;
using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    // Sql.Json(x).Get(..).Text(..)：从 Sql.Json 往外逐级套函数；键与下标作为字面量写进 SQL
    private SqlExpr TranslateJson(MethodCallExpression call)
    {
        if (call.Method.DeclaringType == typeof(Sql)) return Translate(StripConvert(call.Arguments[0]));
        var node = Translate(call.Object!);
        var argument = call.Arguments.Count > 0 ? ClientEvaluator.Evaluate(call.Arguments[0]) : null;
        var byIndex = argument is int;
        var segment = argument switch
        {
            int index when index >= 0 => new LiteralExpr(index.ToString(CultureInfo.InvariantCulture)),
            string key when IsJsonKey(key) => new LiteralExpr(key),
            null => null,
            _ => throw Unsupported(call, "JSON 的键只能是字母、数字、下划线、$ 与中文，下标必须是非负整数"),
        };
        FunctionExpr Fn(string name) => new(name, [node, segment!]);
        switch (call.Method.Name)
        {
            case nameof(JsonNode.Get):
                return Fn(byIndex ? SqlFunctionNames.JsonGetIndex : SqlFunctionNames.JsonGet);
            case nameof(JsonNode.Text):
                return Fn(byIndex ? SqlFunctionNames.JsonTextIndex : SqlFunctionNames.JsonText);
            case nameof(JsonNode.Value):
                var target = call.Method.GetGenericArguments()[0];
                return (Nullable.GetUnderlyingType(target) ?? target) == typeof(string)
                    ? Fn(byIndex ? SqlFunctionNames.JsonTextIndex : SqlFunctionNames.JsonText)
                    : new CastExpr(Fn(byIndex ? SqlFunctionNames.JsonGetIndex : SqlFunctionNames.JsonGet), target);
            case nameof(JsonNode.Length):
                return new FunctionExpr(SqlFunctionNames.JsonLength, [node]);
            case nameof(JsonNode.ContainsKey):
                return new FunctionExpr(SqlFunctionNames.JsonContainsKey, [node, segment!]) { IsPredicate = true };
            default:
                throw Unsupported(call);
        }
    }

    // 键直接写进 SQL 字面量，只接受不会破坏引号的字符
    private static bool IsJsonKey(string key) =>
        key.Length > 0 && key.All(c => char.IsLetterOrDigit(c) || c is '_' or '$');
}
