using System.Text.Json;

namespace ParityTests.Java;

/// <summary>对标基准：取自哪个 Java 仓库版本，以及当时的全部用例（不含方法体）。</summary>
internal sealed record JavaBaseline(string Source, IReadOnlyList<JavaTest> Tests);

/// <summary>
/// 对标基准的快照（来源说明加每个用例的 Id、能力、SQL 摘要），提交进仓库。对照表与 <c>--check</c> 只看它，
/// 只有显式 <c>--rescan</c> 才按 Java 仓库改写。方法体不进快照（只有生成骨架、查看用例时才需要，那时从本机 Java 仓库取）。
/// </summary>
internal static class JavaTestSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private sealed record Entry(string Id, string Capability, string? Sql);

    private sealed record Document(string Source, List<Entry> Tests);

    public static void Save(string path, string source, IReadOnlyList<JavaTest> tests)
    {
        var entries = tests.Select(t => new Entry(t.Id, t.Capability, t.Sqls.Count > 0 ? t.Sqls[0] : null)).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(new Document(source, entries), Options).ReplaceLineEndings("\n") + "\n");
    }

    public static JavaBaseline Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("找不到对标基准快照，先用 --rescan 生成", path);
        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path))!;
        var tests = document.Tests.Select(e =>
        {
            var hash = e.Id.LastIndexOf('#');
            return new JavaTest(e.Id, e.Id[..hash], e.Id[(hash + 1)..], "", e.Capability,
                e.Sql is null ? [] : [e.Sql], []);
        }).ToList();
        return new JavaBaseline(document.Source, tests);
    }
}
