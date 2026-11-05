using System.Text;
using ParityTests.Java;

namespace ParityTests.CSharp;

/// <summary>
/// 为一个 Java 测试文件生成 C# 测试骨架：每个 @Test 一个方法，标 <c>[Upstream(…, Pending = true)]</c> 并跳过，
/// 期望 SQL 与参数原样写进注释，Java 方法体也附在注释里，人只需补上 C# 查询。已存在的文件不覆盖。
/// </summary>
internal static class Scaffolder
{
    /// <summary>生成骨架文件，返回写入的路径；目标已存在时返回 null。</summary>
    public static string? Generate(IReadOnlyList<JavaTest> tests, string javaFile, string upstreamProjectDir)
    {
        var cases = tests.Where(t => t.File == javaFile).ToList();
        if (cases.Count == 0) throw new ArgumentException($"没有找到 Java 文件 {javaFile} 的用例");

        var (folder, className) = Split(javaFile);
        var path = Path.Combine(upstreamProjectDir, folder.Replace('/', Path.DirectorySeparatorChar), className + ".cs");
        if (File.Exists(path)) return null;

        var ns = "EasyQuerySharp.Tests.Upstream." + folder;
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>对应 Java：{javaFile}（{cases.Count} 个用例）。</summary>");
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
        foreach (var t in cases)
        {
            sb.AppendLine($"    [Fact(Skip = \"待移植\")]");
            sb.AppendLine($"    [Upstream(\"{t.Id}\", Pending = true)]");
            sb.AppendLine($"    public Task {Pascal(t.Method)}()");
            sb.AppendLine("    {");
            foreach (var sql in t.Sqls) sb.AppendLine($"        // 期望 SQL：{OneLine(sql)}");
            foreach (var p in t.Params) sb.AppendLine($"        // 期望参数：{p}");
            sb.AppendLine("        // Java 原文：");
            foreach (var line in Dedent(t.Body)) sb.AppendLine($"        // {line}");
            sb.AppendLine("        return Task.CompletedTask;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        sb.Length -= Environment.NewLine.Length;
        sb.AppendLine("}");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    // 测试项目的目录只有一层：Java 子包取第一段（mysql8 → Mysql8、fork/live → Fork）；
    // 根包按文件名分到 Write（增删改、版本）或 Queries，其它主题的文件生成后再手动挪到 Features 等目录
    private static (string Folder, string ClassName) Split(string javaFile)
    {
        var slash = javaFile.LastIndexOf('/');
        var name = Path.GetFileNameWithoutExtension(javaFile[(slash + 1)..]);
        var folder = slash >= 0
            ? Pascal(javaFile[..javaFile.IndexOf('/')])
            : name.StartsWith("Insert", StringComparison.Ordinal) || name.StartsWith("Update", StringComparison.Ordinal)
              || name.StartsWith("Delete", StringComparison.Ordinal) || name.StartsWith("Version", StringComparison.Ordinal)
                ? "Write"
                : "Queries";
        return (folder, Pascal(name));
    }

    /// <summary>Java 风格名字转成 C# 的帕斯卡命名（首字母大写，去掉下划线后的小写开头）。</summary>
    public static string Pascal(string name)
    {
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Concat(parts.Select((p, i) => i == 0 || char.IsDigit(p[0]) ? Upper(p) : "_" + Upper(p)));
        return joined.Length == 0 ? "_" : joined;
    }

    private static string Upper(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    private static string OneLine(string s) => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<string> Dedent(string body)
    {
        var lines = body.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var indent = lines.Where(l => l.Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return lines.Select(l => l.Length >= indent ? l[indent..] : l);
    }
}
