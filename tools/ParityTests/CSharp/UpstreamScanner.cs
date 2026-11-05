using System.Text.RegularExpressions;

namespace ParityTests.CSharp;

/// <summary>Java 用例在 C# 侧的状态。</summary>
internal enum PortStatus
{
    /// <summary>没有任何 C# 标注。</summary>
    Todo,
    /// <summary>有骨架，标了 Pending。</summary>
    Pending,
    /// <summary>已移植，SQL 与 Java 一致。</summary>
    Ported,
    /// <summary>已移植，SQL 或写法有说明过的差异。</summary>
    Deviation,
    /// <summary>不适用（写了理由）。</summary>
    NotApplicable,
}

/// <summary>C# 源码里的一处 <c>[Upstream]</c> 标注。</summary>
internal sealed record UpstreamMark(string JavaId, PortStatus Status, string? Reason, string Location);

/// <summary>用正则扫描测试项目里的 <c>[Upstream("…")]</c> 标注（标注写法由我们约定，正则足够，不引入 Roslyn）。</summary>
internal static partial class UpstreamScanner
{
    /// <summary>扫描 <paramref name="testsRoot"/> 下全部 .cs 文件。</summary>
    public static IReadOnlyList<UpstreamMark> Scan(string testsRoot, string repoRoot)
    {
        var marks = new List<UpstreamMark>();
        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            var text = File.ReadAllText(path);
            foreach (Match m in Mark().Matches(text))
            {
                // 注释（含 XML 文档注释里的示例）不算标注
                var lineStart = text.LastIndexOf('\n', m.Index) + 1;
                if (text.AsSpan(lineStart, m.Index - lineStart).TrimStart().StartsWith("//")) continue;
                var rest = m.Groups["rest"].Value;
                var line = text[..m.Index].Count(c => c == '\n') + 1;
                var location = $"{Path.GetRelativePath(repoRoot, path).Replace('\\', '/')}:{line}";
                marks.Add(Parse(m.Groups["id"].Value, rest, location, text));
            }
        }
        return marks;
    }

    private static UpstreamMark Parse(string id, string rest, string location, string fileText)
    {
        var na = Named(rest, "NotApplicable", fileText);
        if (na is not null) return new(id, PortStatus.NotApplicable, na, location);
        if (Regex.IsMatch(rest, @"Pending\s*=\s*true")) return new(id, PortStatus.Pending, null, location);
        var deviation = Named(rest, "Deviation", fileText);
        return deviation is not null
            ? new(id, PortStatus.Deviation, deviation, location)
            : new(id, PortStatus.Ported, null, location);
    }

    // 命名参数的值：字符串字面量，或同一文件里 const string 常量的名字（如 NotApplicable = JavaUtility）
    private static string? Named(string rest, string name, string fileText)
    {
        var m = Regex.Match(rest, name + @"\s*=\s*""((?:[^""\\]|\\.)*)""");
        if (m.Success) return m.Groups[1].Value;
        var constant = Regex.Match(rest, name + @"\s*=\s*(?<id>[A-Za-z_]\w*)");
        if (!constant.Success) return null;
        var id = constant.Groups["id"].Value;
        var definition = Regex.Match(fileText, @"const\s+string\s+" + id + @"\s*=\s*""((?:[^""\\]|\\.)*)""");
        return definition.Success ? definition.Groups[1].Value : throw new InvalidOperationException($"找不到 {name} 引用的常量 {id}（要定义在同一个文件里）");
    }

    // [Upstream("A.java#m")]、[Upstream("A.java#m", Deviation = "…")]、[assembly: Upstream(…)]
    [GeneratedRegex(@"\bUpstream\(\s*""(?<id>[^""]+)""(?<rest>(?:[^()""]|""(?:[^""\\]|\\.)*"")*)\)")]
    private static partial Regex Mark();
}
