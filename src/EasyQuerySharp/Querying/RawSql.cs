using System.Text;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 把原生 SQL 变成可执行的语句：<c>?</c> 位置参数（Java 版的写法）或插值字符串的 <c>{值}</c> 都换成方言的占位符，值全部参数化。
/// 引号、反引号里的 <c>?</c> 不算占位符。
/// </summary>
internal static class RawSql
{
    /// <summary>位置参数：第 i 个 <c>?</c> 对应 <paramref name="parameters"/> 的第 i 个值。</summary>
    public static SqlStatement FromPositional(SqlDialect dialect, string sql, IReadOnlyList<object?> parameters)
    {
        var command = new StringBuilder(sql.Length + 16);
        var text = new StringBuilder(sql.Length);
        var values = new List<SqlParameterValue>();
        char? quote = null;
        foreach (var c in sql)
        {
            if (quote is not null)
            {
                if (c == quote) quote = null;
            }
            else if (c is '\'' or '"' or '`') quote = c;
            else if (c == '?')
            {
                if (values.Count >= parameters.Count)
                    throw new ArgumentException($"SQL 里的 ? 比参数多（参数 {parameters.Count} 个）：{sql}", nameof(parameters));
                var value = Value(parameters[values.Count]);
                command.Append(dialect.ParameterPlaceholder(values.Count, value));
                text.Append('?');
                values.Add(value);
                continue;
            }
            command.Append(c);
            text.Append(c);
        }
        if (values.Count != parameters.Count)
            throw new ArgumentException($"SQL 里有 {values.Count} 个 ?，但给了 {parameters.Count} 个参数：{sql}", nameof(parameters));
        return new SqlStatement(command.ToString(), text.ToString(), values);
    }

    /// <summary>插值字符串：<c>$"… where id = {id}"</c>，插值处全部参数化。</summary>
    public static SqlStatement FromInterpolated(SqlDialect dialect, FormattableString sql)
    {
        var args = sql.GetArguments();
        var placeholders = Enumerable.Range(0, args.Length).Select(i => (object)("\u0001" + i + "\u0002")).ToArray();
        var marked = string.Format(System.Globalization.CultureInfo.InvariantCulture, sql.Format, placeholders);
        var command = new StringBuilder();
        var text = new StringBuilder();
        var values = new List<SqlParameterValue>();
        var start = 0;
        for (var i = marked.IndexOf('\u0001'); i >= 0; i = marked.IndexOf('\u0001', start))
        {
            var end = marked.IndexOf('\u0002', i);
            var index = int.Parse(marked.AsSpan(i + 1, end - i - 1), System.Globalization.CultureInfo.InvariantCulture);
            command.Append(marked, start, i - start);
            text.Append(marked, start, i - start);
            AppendArgument(dialect, args[index], command, text, values);
            start = end + 1;
        }
        command.Append(marked, start, marked.Length - start);
        text.Append(marked, start, marked.Length - start);
        return new SqlStatement(command.ToString(), text.ToString(), values);
    }

    // 插值处：标识符按方言加引号；集合展开成 ?,?,?（空集合直接报错，避免生成 IN () 这种坏语句）；其余是一个参数
    private static void AppendArgument(SqlDialect dialect, object? arg, StringBuilder command, StringBuilder text, List<SqlParameterValue> values)
    {
        switch (arg)
        {
            case SqlIdentifier identifier:
                var quoted = dialect.QuoteIdentifier(identifier.Name);
                command.Append(quoted);
                text.Append(quoted);
                return;
            case System.Collections.IEnumerable items and not string and not byte[]:
                var count = 0;
                foreach (var item in items)
                {
                    if (count++ > 0)
                    {
                        command.Append(',');
                        text.Append(',');
                    }
                    AppendParameter(item);
                }
                if (count == 0) throw new ArgumentException("原生 SQL 里插入的集合是空的，会生成 IN () 这样的坏语句");
                return;
            default:
                AppendParameter(arg);
                return;
        }

        void AppendParameter(object? value)
        {
            var parameter = Value(value);
            command.Append(dialect.ParameterPlaceholder(values.Count, parameter));
            text.Append('?');
            values.Add(parameter);
        }
    }

    private static SqlParameterValue Value(object? value)
    {
        var p = ExpressionTranslator.Parameter(value, value?.GetType() ?? typeof(object));
        return new SqlParameterValue(p.Value, p.ClrType);
    }
}
