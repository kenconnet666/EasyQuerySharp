using System.Text.Json;

namespace ParityTests.Java;

/// <summary>
/// Java 用例索引的快照（Id、能力、SQL 摘要）：本地有 Java 仓库时生成并提交，CI 上没有 Java 仓库时用它校验对照表。
/// 方法体不进快照（只有生成骨架时才需要，那时一定在本地）。
/// </summary>
internal static class JavaTestSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private sealed record Entry(string Id, string Capability, string? Sql);

    public static void Save(string path, IReadOnlyList<JavaTest> tests)
    {
        var entries = tests.Select(t => new Entry(t.Id, t.Capability, t.Sqls.Count > 0 ? t.Sqls[0] : null));
        File.WriteAllText(path, JsonSerializer.Serialize(entries, Options).ReplaceLineEndings("\n") + "\n");
    }

    public static IReadOnlyList<JavaTest> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("没有 Java 仓库，也没有用例快照", path);
        var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path))!;
        return entries.Select(e =>
        {
            var hash = e.Id.LastIndexOf('#');
            return new JavaTest(e.Id, e.Id[..hash], e.Id[(hash + 1)..], "", e.Capability,
                e.Sql is null ? [] : [e.Sql], []);
        }).ToList();
    }
}
