using System.Diagnostics;
using ParityTests.CSharp;
using ParityTests.Java;
using ParityTests.Report;

// 用法（在仓库任意位置）：
//   dotnet run --project tools/ParityTests                      生成 docs/parity-tests.md
//   dotnet run --project tools/ParityTests -- --check           只校验（CI 用）：对照表是否最新、标注是否有误
//   dotnet run --project tools/ParityTests -- --scaffold QueryTest.java   为一个 Java 文件生成测试骨架
//   dotnet run --project tools/ParityTests -- --show nav.include          列出某能力（或某个用例 Id、文件）的 Java 用例与 SQL
//   dotnet run --project tools/ParityTests -- --rescan          按 Java 仓库重新生成对标基准（只在决定移动基准时用）
//   公共选项：--java <easy-query 仓库路径>，默认 ../../IdeaProjects/easy-query（相对仓库根）
//
// 对照表与 --check 只看提交进仓库的基准快照：本机的 Java 仓库可能是别的版本（比如没有 fork 部分的上游），
// 只用来取方法体（--scaffold / --show）。

var repoRoot = FindRepoRoot();
var javaRepo = Option("--java") ?? Path.GetFullPath(Path.Combine(repoRoot, "..", "..", "IdeaProjects", "easy-query"));
var upstreamDir = Path.Combine(repoRoot, "tests", "EasyQuerySharp.Tests.Upstream");
var reportPath = Path.Combine(repoRoot, "docs", "parity-tests.md");
var snapshotPath = Path.Combine(repoRoot, "tools", "ParityTests", "java-tests.json");

var baseline = JavaTestSnapshot.Load(snapshotPath);

if (args.Contains("--rescan"))
{
    var scanned = JavaTestScanner.Scan(RequireJavaRepo());
    PrintBaselineChanges(baseline.Tests, scanned);
    baseline = new JavaBaseline(RepoVersion(javaRepo), scanned);
    JavaTestSnapshot.Save(snapshotPath, baseline.Source, scanned);
}

if (Option("--scaffold") is { } file)
{
    var written = Scaffolder.Generate(WithBodies(baseline.Tests), file, upstreamDir);
    Console.WriteLine(written is null ? $"已存在，未覆盖：{file}" : $"已生成 {Path.GetRelativePath(repoRoot, written)}");
    return 0;
}

if (Option("--show") is { } what)
{
    foreach (var t in WithBodies(baseline.Tests).Where(t => t.Capability == what || t.Id == what || t.File == what))
    {
        Console.WriteLine($"==== {t.Id}  [{t.Capability}]");
        Console.WriteLine(t.Body.Length > 0 ? t.Body.Trim() : "（本机 Java 仓库里没有这个用例，只有基准里的 SQL）\n" + string.Join("\n", t.Sqls));
        Console.WriteLine();
    }
    return 0;
}

var marks = UpstreamScanner.Scan(Path.Combine(repoRoot, "tests"), repoRoot);
var (rows, errors) = ParityReport.Merge(baseline.Tests, marks);
foreach (var e in errors) Console.Error.WriteLine(e);
var content = ParityReport.Render(rows, baseline.Source).ReplaceLineEndings("\n");

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

string RequireJavaRepo() =>
    Directory.Exists(javaRepo) ? javaRepo : throw new DirectoryNotFoundException($"找不到 Java 仓库 {javaRepo}，用 --java 指定");

// 基准里的用例配上本机 Java 仓库里的方法体与断言；本机没有的用例（版本不同、缺 fork）保持只有 SQL 摘要
IReadOnlyList<JavaTest> WithBodies(IReadOnlyList<JavaTest> tests)
{
    var local = JavaTestScanner.Scan(RequireJavaRepo(), requireAllRoots: false).ToDictionary(t => t.Id);
    return tests.Select(t => local.TryGetValue(t.Id, out var found) ? found with { Capability = t.Capability } : t).ToList();
}

static void PrintBaselineChanges(IReadOnlyList<JavaTest> before, IReadOnlyList<JavaTest> after)
{
    var old = before.Select(t => t.Id).ToHashSet();
    var now = after.Select(t => t.Id).ToHashSet();
    var added = now.Except(old).Order().ToList();
    var removed = old.Except(now).Order().ToList();
    Console.WriteLine($"基准变化：{before.Count} → {after.Count}，新增 {added.Count}、移除 {removed.Count}");
    foreach (var id in added) Console.WriteLine($"  + {id}");
    foreach (var id in removed) Console.WriteLine($"  - {id}");
}

static string RepoVersion(string repo)
{
    var info = new ProcessStartInfo("git", ["-C", repo, "log", "-1", "--format=%h %cs"]) { RedirectStandardOutput = true };
    using var git = Process.Start(info) ?? throw new InvalidOperationException("无法运行 git");
    var head = git.StandardOutput.ReadToEnd().Trim();
    git.WaitForExit();
    return git.ExitCode == 0 ? $"easy-query {head}" : throw new InvalidOperationException($"{repo} 不是 git 仓库，无法记录基准版本");
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "EasyQuerySharp.slnx"))) return dir.FullName;
    throw new InvalidOperationException("找不到仓库根（EasyQuerySharp.slnx），请在仓库里运行");
}
