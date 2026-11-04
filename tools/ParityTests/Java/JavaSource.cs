using System.Text;

namespace ParityTests.Java;

/// <summary>Java 源码的最小词法处理：去注释、配对花括号、拼接字符串字面量。只为抽取测试方法服务，不是完整的 Java 解析器。</summary>
internal static class JavaSource
{
    /// <summary>去掉注释（保留字符串和字符字面量里的 // 与 /*）。</summary>
    public static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '"' or '\'')
            {
                var end = SkipLiteral(source, i);
                sb.Append(source, i, end - i + 1);
                i = end;
            }
            else if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                sb.Append('\n');
            }
            else if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                // 保留换行，行号近似不变
                var block = close < 0 ? source[i..] : source[i..(close + 2)];
                sb.Append('\n', block.Count(ch => ch == '\n'));
                i = close < 0 ? source.Length : close + 1;
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>从 <paramref name="open"/>（一个 '{' 之后）开始找到配对的 '}'，返回其下标。</summary>
    public static int MatchBrace(string source, int open)
    {
        var depth = 1;
        for (var i = open; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '"' or '\'') i = SkipLiteral(source, i);
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return source.Length - 1;
    }

    /// <summary>
    /// 抽出所有"字符串表达式"：用 + 连接的字面量序列合成一个值，中间夹着的非字面量（常量、方法调用）写成 <c>{…}</c>。
    /// 返回值与它在源码里的起始位置。
    /// </summary>
    public static IEnumerable<(string Value, int Start)> StringExpressions(string body)
    {
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '\'') { i = SkipLiteral(body, i); continue; }
            if (body[i] != '"') continue;

            var start = i;
            var sb = new StringBuilder();
            while (true)
            {
                var end = SkipLiteral(body, i);
                sb.Append(Unescape(body[(i + 1)..end]));
                i = end + 1;
                // 看后面是否 + "..." 或 + 标识符 + "..."
                var j = SkipSpace(body, i);
                if (j >= body.Length || body[j] != '+') break;
                j = SkipSpace(body, j + 1);
                if (j < body.Length && body[j] == '"') { i = j; continue; }
                var operand = ReadOperand(body, j);
                var after = SkipSpace(body, j + operand);
                if (operand == 0 || after >= body.Length || body[after] != '+') break;
                sb.Append("{…}");
                i = SkipSpace(body, after + 1);
                if (i >= body.Length || body[i] != '"') break;
            }
            yield return (sb.ToString(), start);
            i--;
        }
    }

    private static int SkipSpace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }

    // 读一个简单操作数：标识符、点号链、带括号的调用（括号内配对）
    private static int ReadOperand(string s, int start)
    {
        var i = start;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsLetterOrDigit(c) || c is '_' or '.' or '$') i++;
            else if (c == '(')
            {
                var depth = 1;
                for (i++; i < s.Length && depth > 0; i++)
                {
                    if (s[i] is '"' or '\'') i = SkipLiteral(s, i);
                    else if (s[i] == '(') depth++;
                    else if (s[i] == ')') depth--;
                }
            }
            else break;
        }
        return i - start;
    }

    /// <summary>从引号处跳到配对的结束引号（处理转义与文本块 """）。</summary>
    private static int SkipLiteral(string s, int i)
    {
        var quote = s[i];
        if (quote == '"' && i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"')
        {
            var close = s.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
            return close < 0 ? s.Length - 1 : close + 2;
        }
        for (i++; i < s.Length; i++)
        {
            if (s[i] == '\\') i++;
            else if (s[i] == quote || s[i] == '\n') return i;
        }
        return s.Length - 1;
    }

    private static string Unescape(string raw) => raw
        .Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
}
