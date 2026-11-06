using System.Text;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 解析好的原生 SQL：<see cref="Segments"/> 之间依次夹着 <see cref="Parameters"/>（片段比参数多一个）。
/// 整条执行（<see cref="RawSql.ToStatement"/>）与作为派生表（<see cref="RawTableSource"/>）共用这一种形式。
/// </summary>
internal sealed record RawSqlText(IReadOnlyList<string> Segments, IReadOnlyList<ParameterExpr> Parameters)
{
    /// <summary>参数位置写成 <c>?</c> 的文本（日志、异常消息）。</summary>
    public string Text => string.Join('?', Segments);
}

/// <summary>
/// 原生 SQL 的两种写法：插值字符串（<c>$"… where id = {id}"</c>，插值处全部参数化，<see cref="EasyDb.SqlQueryAsync{T}"/> 等）
/// 与 <c>?</c> 位置参数（Java 版的写法，<c>…Raw</c> 方法）。引号、反引号里的 <c>?</c> 不算占位符。
/// </summary>
internal static class RawSql
{
    /// <summary>位置参数：第 i 个 <c>?</c> 对应 <paramref name="parameters"/> 的第 i 个值。</summary>
    public static RawSqlText Positional(string sql, IReadOnlyList<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var segments = new List<string>();
        var values = new List<ParameterExpr>();
        var segment = new StringBuilder(sql.Length);
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
                segments.Add(segment.ToString());
                segment.Clear();
                values.Add(Value(parameters[values.Count]));
                continue;
            }
            segment.Append(c);
        }
        if (values.Count != parameters.Count)
            throw new ArgumentException($"SQL 里有 {values.Count} 个 ?，但给了 {parameters.Count} 个参数：{sql}", nameof(parameters));
        segments.Add(segment.ToString());
        return new RawSqlText(segments, values);
    }

    /// <summary>
    /// 插值字符串：插值处全部参数化；<see cref="SqlIdentifier"/> 按方言加引号写进文本，集合展开成 <c>?,?,?</c>
    /// （空集合直接报错，避免生成 <c>IN ()</c> 这种坏语句）。字面文本里的 <c>?</c> 原样保留（如 PG 的 jsonb 运算符）。
    /// </summary>
    public static RawSqlText Interpolated(SqlDialect dialect, FormattableString sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var args = sql.GetArguments();
        // 先把插值处换成标记再格式化：{{ }} 的转义交给 string.Format
        var placeholders = Enumerable.Range(0, args.Length).Select(i => (object)("\u0001" + i + "\u0002")).ToArray();
        var marked = string.Format(System.Globalization.CultureInfo.InvariantCulture, sql.Format, placeholders);
        var segments = new List<string>();
        var values = new List<ParameterExpr>();
        var segment = new StringBuilder();
        var start = 0;
        for (var i = marked.IndexOf('\u0001'); i >= 0; i = marked.IndexOf('\u0001', start))
        {
            var end = marked.IndexOf('\u0002', i);
            var index = int.Parse(marked.AsSpan(i + 1, end - i - 1), System.Globalization.CultureInfo.InvariantCulture);
            segment.Append(marked, start, i - start);
            AppendArgument(args[index]);
            start = end + 1;
        }
        segment.Append(marked, start, marked.Length - start);
        segments.Add(segment.ToString());
        return new RawSqlText(segments, values);

        void AppendArgument(object? arg)
        {
            switch (arg)
            {
                case SqlIdentifier identifier:
                    segment.Append(dialect.QuoteIdentifier(identifier.Name));
                    return;
                case System.Collections.IEnumerable items and not string and not byte[]:
                    var count = 0;
                    foreach (var item in items)
                    {
                        if (count++ > 0) segment.Append(',');
                        AppendParameter(item);
                    }
                    if (count == 0) throw new ArgumentException("原生 SQL 里插入的集合是空的，会生成 IN () 这样的坏语句");
                    return;
                default:
                    AppendParameter(arg);
                    return;
            }
        }

        void AppendParameter(object? value)
        {
            segments.Add(segment.ToString());
            segment.Clear();
            values.Add(Value(value));
        }
    }

    /// <summary>整条执行的语句：参数处写方言的占位符，<see cref="SqlStatement.Text"/> 写 <c>?</c>。</summary>
    public static SqlStatement ToStatement(SqlDialect dialect, RawSqlText raw)
    {
        var command = new StringBuilder(raw.Segments[0]);
        for (var i = 0; i < raw.Parameters.Count; i++) command.Append(dialect.ParameterPlaceholder(i)).Append(raw.Segments[i + 1]);
        return new SqlStatement(command.ToString(), raw.Text, raw.Parameters.Select(p => new SqlParameterValue(p.Value, p.ClrType)).ToList());
    }

    private static ParameterExpr Value(object? value) => ExpressionTranslator.Parameter(value, value?.GetType() ?? typeof(object));
}
