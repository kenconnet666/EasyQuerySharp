using System.Text.RegularExpressions;

namespace ParityTests.Java;

/// <summary>Java 仓库里的一个 @Test 用例。</summary>
/// <param name="Id">对照用的标识：<c>相对路径#方法名</c>，fork 用例以 <c>fork/</c> 开头。</param>
/// <param name="File">相对路径（不含方法名）。</param>
/// <param name="Method">方法名。</param>
/// <param name="Body">去掉注释后的方法体。</param>
/// <param name="Capability">归入的能力 key。</param>
/// <param name="Sqls">方法体里断言的 SQL（按出现顺序，常量拼接处为 {…}）。</param>
/// <param name="Params">方法体里断言的参数文本，如 <c>123xxx(String)</c>。</param>
internal sealed record JavaTest(
    string Id,
    string File,
    string Method,
    string Body,
    string Capability,
    IReadOnlyList<string> Sqls,
    IReadOnlyList<string> Params);

/// <summary>扫描 easy-query 仓库的 sql-test 与 sql-fork-test，抽出全部 @Test 用例。</summary>
internal static partial class JavaTestScanner
{
    private static readonly (string Dir, string Prefix, string IdPrefix)[] Roots =
    [
        ("sql-test/src", "main/java/com/easy/query/test/", ""),
        ("sql-fork-test/src", "test/java/com/easy/query/fork/test/", "fork/"),
    ];

    /// <summary>扫描 <paramref name="javaRepo"/> 下的全部用例，按 Id 排序。</summary>
    public static IReadOnlyList<JavaTest> Scan(string javaRepo)
    {
        var result = new List<JavaTest>();
        foreach (var (dir, prefix, idPrefix) in Roots)
        {
            var root = Path.Combine(javaRepo, dir);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"找不到 Java 用例目录：{root}");

            foreach (var path in Directory.EnumerateFiles(root, "*.java", SearchOption.AllDirectories))
            {
                var source = JavaSource.StripComments(File.ReadAllText(path));
                if (!source.Contains("@Test")) continue;

                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.StartsWith(prefix, StringComparison.Ordinal)) relative = relative[prefix.Length..];
                var file = idPrefix + relative;

                foreach (var (method, body) in TestMethods(source))
                {
                    var strings = JavaSource.StringExpressions(body).Select(s => s.Value).ToList();
                    result.Add(new JavaTest(
                        $"{file}#{method}", file, method, body,
                        Capabilities.Classify(file, body),
                        strings.Where(IsSql).ToList(),
                        strings.Where(s => ParamText().IsMatch(s)).ToList()));
                }
            }
        }
        return result.OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<(string Method, string Body)> TestMethods(string source)
    {
        foreach (Match m in TestMarker().Matches(source))
        {
            var open = m.Index + m.Length;
            var close = JavaSource.MatchBrace(source, open);
            yield return (m.Groups["name"].Value, source[open..close]);
        }
    }

    private static bool IsSql(string s) => SqlStart().IsMatch(s);

    [GeneratedRegex(@"@Test\b(?:\s*\([^)]*\))?[\s\S]*?(?:public\s+)?void\s+(?<name>\w+)\s*\([^)]*\)[^{;]*\{")]
    private static partial Regex TestMarker();

    [GeneratedRegex(@"^\s*(SELECT|INSERT|UPDATE|DELETE|WITH|MERGE|CREATE|ALTER|DROP|REPLACE|TRUNCATE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SqlStart();

    // 参数文本：值(Java类型)，多个用逗号分隔，例如 123xxx(String),1(Integer)
    [GeneratedRegex(@"\((String|Integer|Long|Short|Byte|Boolean|BigDecimal|Double|Float|LocalDateTime|LocalDate|LocalTime|Date|Timestamp|UUID|Character|byte\[\]|null|Instant|OffsetDateTime|ZonedDateTime|[A-Z]\w*Enum)\)(,|$)")]
    private static partial Regex ParamText();
}
