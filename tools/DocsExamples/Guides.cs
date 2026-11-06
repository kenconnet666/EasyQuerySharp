using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EasyQuerySharp.Docs;

/// <summary>
/// 指南（docs/guide/*.md）里的代码与 SQL 由卡片生成，不手写第二份。指南里有两种区域，区域之间的内容整段重写：
/// <list type="bullet">
/// <item><c>&lt;!-- card: 页面/方法名 sql=mysql,pgsql --&gt;</c> … <c>&lt;!-- /card --&gt;</c>：卡片的方法体，再给 <c>sql=</c> 列出的每种方言接一段它发出的 SQL（不写 <c>sql=</c> 时只有代码）；</item>
/// <item><c>&lt;!-- snippet: 名字 --&gt;</c> … <c>&lt;!-- /snippet --&gt;</c>：docs/examples 里 <c>// &lt;snippet:名字&gt;</c> 与 <c>// &lt;/snippet:名字&gt;</c> 之间的代码，用于实体声明、配置这类不是卡片的代码。</item>
/// </list>
/// 这样指南里的每段代码都随 DocsExamples 一起编译、在 SQLite 上实跑，SQL 随渲染器更新。
/// </summary>
internal static partial class Guides
{
    private static readonly Dictionary<string, string> DialectTitles = new(StringComparer.Ordinal)
    {
        ["sqlite"] = "SQLite", ["mysql"] = "MySQL", ["pgsql"] = "PostgreSQL", ["mssql"] = "SQL Server", ["oracle"] = "Oracle", ["dameng"] = "达梦",
    };

    /// <summary>重写（或在 <paramref name="check"/> 时只校验）全部指南。<paramref name="sql"/> 是每张卡片在每种方言上发出的语句。</summary>
    public static void Sync(string root, IReadOnlyDictionary<string, Dictionary<string, string>> sql, bool check, List<string> problems)
    {
        var guides = Path.Combine(root, "docs", "guide");
        if (!Directory.Exists(guides)) return;
        var sources = Directory.GetFiles(Path.Combine(root, "docs", "examples"), "*.cs").Order(StringComparer.Ordinal).ToList();
        var cards = ReadCards(sources);
        var snippets = ReadSnippets(sources, problems);
        foreach (var path in Directory.GetFiles(guides, "*.md").Order(StringComparer.Ordinal))
        {
            var name = "docs/guide/" + Path.GetFileName(path);
            var original = File.ReadAllText(path).ReplaceLineEndings("\n");
            var text = CardRegion().Replace(original, m => RenderCard(m, name, cards, sql, problems));
            text = SnippetRegion().Replace(text, m => RenderSnippet(m, name, snippets, problems));
            if (text == original) continue;
            if (check) problems.Add($"指南过期：{name}（重新运行 tools/DocsExamples）");
            else File.WriteAllText(path, text);
        }
    }

    private static string RenderCard(Match m, string guide, Dictionary<string, string> cards, IReadOnlyDictionary<string, Dictionary<string, string>> sql, List<string> problems)
    {
        var id = m.Groups["id"].Value;
        if (!cards.TryGetValue(id, out var code) || !sql.TryGetValue(id, out var statements))
        {
            problems.Add($"{guide}：引用了不存在的卡片 {id}");
            return m.Value;
        }
        var text = new StringBuilder();
        text.Append(m.Groups["head"].Value).Append("```csharp\n").Append(code).Append("\n```\n");
        foreach (var dialect in m.Groups["sql"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!statements.TryGetValue(dialect, out var dialectSql))
            {
                problems.Add($"{guide}：卡片 {id} 没有在 {dialect} 上渲染（方言名写错，或卡片限定了 Only）");
                return m.Value;
            }
            text.Append("\n```sql\n-- ").Append(DialectTitles[dialect]).Append('\n').Append(dialectSql).Append("```\n");
        }
        return text.Append("<!-- /card -->").ToString();
    }

    private static string RenderSnippet(Match m, string guide, Dictionary<string, string> snippets, List<string> problems)
    {
        var name = m.Groups["name"].Value;
        if (snippets.TryGetValue(name, out var code)) return m.Groups["head"].Value + "```csharp\n" + code + "\n```\n<!-- /snippet -->";
        problems.Add($"{guide}：引用了不存在的代码片段 {name}（docs/examples 里要有 // <snippet:{name}>）");
        return m.Value;
    }

    // 卡片 id（页面/方法名）→ 方法体：表达式体写成一条语句，块体取大括号里面的语句
    private static Dictionary<string, string> ReadCards(IEnumerable<string> sources)
    {
        var cards = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(source).ReplaceLineEndings("\n"));
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var example = method.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => a.Name.ToString() is "Example" or "ExampleAttribute");
                if (example?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax page) continue;
                cards[page.Token.ValueText + "/" + method.Identifier.ValueText] = method.ExpressionBody is { } arrow
                    ? ExpressionBody(arrow.Expression, tree.GetLineSpan(arrow.Expression.Span).StartLinePosition.Character) + ";"
                    : Dedent(method.Body!.ToString()[1..^1].Split('\n'));
            }
        }
        return cards;
    }

    // 表达式的第一行从表达式起点开始，后面几行保留原样的缩进：按第一行所在的列一起往左移
    private static string ExpressionBody(ExpressionSyntax expression, int column)
    {
        var lines = expression.ToString().Split('\n');
        for (var i = 1; i < lines.Length; i++)
            lines[i] = lines[i][Math.Min(column, Indent(lines[i]))..];
        return StripMarkers(lines);
    }

    private static Dictionary<string, string> ReadSnippets(IEnumerable<string> sources, List<string> problems)
    {
        var snippets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (Match m in SnippetSource().Matches(File.ReadAllText(source).ReplaceLineEndings("\n")))
            {
                var name = m.Groups["name"].Value;
                if (!snippets.TryAdd(name, Dedent(m.Groups["body"].Value.Split('\n'))))
                    problems.Add($"代码片段 {name} 重名（docs/examples/{Path.GetFileName(source)}）");
            }
        }
        return snippets;
    }

    // 去掉首尾的空行，所有行一起往左移到最浅的缩进
    private static string Dedent(string[] lines)
    {
        static bool Blank(string line) => line.Trim().Length == 0;
        var kept = lines.SkipWhile(Blank).Reverse().SkipWhile(Blank).Reverse().ToList();
        var indent = kept.Where(l => !Blank(l)).Select(Indent).DefaultIfEmpty(0).Min();
        return StripMarkers(kept.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }

    // 嵌在卡片或片段里的片段标记不显示
    private static string StripMarkers(IEnumerable<string> lines) =>
        string.Join('\n', lines.Where(l => !MarkerLine().IsMatch(l)).Select(l => l.TrimEnd()));

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    [GeneratedRegex(@"(?<head><!-- card: (?<id>\S+?)(?: sql=(?<sql>[a-z,]+))? -->\n).*?<!-- /card -->", RegexOptions.Singleline)]
    private static partial Regex CardRegion();

    [GeneratedRegex(@"(?<head><!-- snippet: (?<name>[\w-]+) -->\n).*?<!-- /snippet -->", RegexOptions.Singleline)]
    private static partial Regex SnippetRegion();

    [GeneratedRegex(@"^[ ]*// <snippet:(?<name>[\w-]+)>\n(?<body>.*?)\n[ ]*// </snippet:\k<name>>", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex SnippetSource();

    [GeneratedRegex(@"^\s*// </?snippet:[\w-]+>\s*$")]
    private static partial Regex MarkerLine();
}
