using ParityTests.CSharp;
using ParityTests.Java;
using ParityTests.Report;

// 用法（在仓库任意位置）：
//   dotnet run --project tools/ParityTests                      生成 docs/parity-tests.md
//   dotnet run --project tools/ParityTests -- --check           只校验（CI 用）：对照表是否最新、标注是否有误
//   dotnet run --project tools/ParityTests -- --scaffold QueryTest.java   为一个 Java 文件生成测试骨架
//   dotnet run --project tools/ParityTests -- --show nav.include          列出某能力（或某个用例 Id、文件）的 Java 用例与 SQL
//   公共选项：--java <easy-query 仓库路径>，默认 ../../IdeaProjects/easy-query（相对仓库根）

var repoRoot = FindRepoRoot();
var javaRepo = Option("--java") ?? Path.GetFullPath(Path.Combine(repoRoot, "..", "..", "IdeaProjects", "easy-query"));
var upstreamDir = Path.Combine(repoRoot, "tests", "EasyQuerySharp.Tests.Upstream");
var reportPath = Path.Combine(repoRoot, "docs", "parity-tests.md");
var snapshotPath = Path.Combine(repoRoot, "tools", "ParityTests", "java-tests.json");

// 有 Java 仓库时扫描并刷新快照；没有时（CI）读提交进仓库的快照
IReadOnlyList<JavaTest> java;
if (Directory.Exists(javaRepo))
{
    java = JavaTestScanner.Scan(javaRepo);
    if (!args.Contains("--check")) JavaTestSnapshot.Save(snapshotPath, java);
}
else java = JavaTestSnapshot.Load(snapshotPath);

if (Option("--scaffold") is { } file)
{
    var written = Scaffolder.Generate(java, file, upstreamDir);
    Console.WriteLine(written is null ? $"已存在，未覆盖：{file}" : $"已生成 {Path.GetRelativePath(repoRoot, written)}");
    return 0;
}

if (Option("--show") is { } what)
{
    foreach (var t in java.Where(t => t.Capability == what || t.Id == what || t.File == what))
    {
        Console.WriteLine($"==== {t.Id}  [{t.Capability}]");
        Console.WriteLine(t.Body.Trim());
        Console.WriteLine();
    }
    return 0;
}

var marks = UpstreamScanner.Scan(Path.Combine(repoRoot, "tests"), repoRoot);
var (rows, errors) = ParityReport.Merge(java, marks);
foreach (var e in errors) Console.Error.WriteLine(e);
var content = ParityReport.Render(rows).ReplaceLineEndings("\n");

var summary = string.Join(", ", rows.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));
if (args.Contains("--check"))
{
    var current = File.Exists(reportPath) ? File.ReadAllText(reportPath).ReplaceLineEndings("\n") : "";
    var stale = current != content;
    if (stale) Console.Error.WriteLine("docs/parity-tests.md 不是最新的：运行 dotnet run --project tools/ParityTests 重新生成");
    Console.WriteLine($"{rows.Count} 个用例：{summary}");
    return stale || errors.Count > 0 ? 1 : 0;
}

File.WriteAllText(reportPath, content);
Console.WriteLine($"已写入 docs/parity-tests.md：{rows.Count} 个用例：{summary}");
return errors.Count > 0 ? 1 : 0;

string? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "EasyQuerySharp.slnx"))) return dir.FullName;
    throw new InvalidOperationException("找不到仓库根（EasyQuerySharp.slnx），请在仓库里运行");
}
