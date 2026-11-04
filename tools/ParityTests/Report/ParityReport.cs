using System.Text;
using ParityTests.CSharp;
using ParityTests.Java;

namespace ParityTests.Report;

/// <summary>一个 Java 用例与它在 C# 侧的状态。</summary>
internal sealed record ParityRow(JavaTest Java, PortStatus Status, UpstreamMark? Mark);

/// <summary>把 Java 用例与 C# 标注合并成对照表，并检查标注本身的错误。</summary>
internal static class ParityReport
{
    /// <summary>合并；返回对照行与标注错误（引用了不存在的用例、重复声明）。</summary>
    public static (IReadOnlyList<ParityRow> Rows, IReadOnlyList<string> Errors) Merge(
        IReadOnlyList<JavaTest> java, IReadOnlyList<UpstreamMark> marks)
    {
        var errors = new List<string>();
        var known = java.Select(j => j.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var m in marks.Where(m => !known.Contains(m.JavaId)))
            errors.Add($"{m.Location}: [Upstream(\"{m.JavaId}\")] 引用的 Java 用例不存在");

        var byId = new Dictionary<string, UpstreamMark>(StringComparer.Ordinal);
        foreach (var m in marks.Where(m => known.Contains(m.JavaId)))
        {
            if (byId.TryGetValue(m.JavaId, out var first))
                errors.Add($"{m.Location}: {m.JavaId} 已在 {first.Location} 声明过");
            else byId[m.JavaId] = m;
        }

        var rows = java.Select(j => byId.TryGetValue(j.Id, out var m)
            ? new ParityRow(j, m.Status, m)
            : new ParityRow(j, PortStatus.Todo, null)).ToList();
        return (rows, errors);
    }

    private static readonly (PortStatus Status, string Text)[] StatusText =
    [
        (PortStatus.Ported, "已移植"),
        (PortStatus.Deviation, "偏离"),
        (PortStatus.NotApplicable, "不适用"),
        (PortStatus.Pending, "骨架"),
        (PortStatus.Todo, "待做"),
    ];

    private static string Text(PortStatus s) => StatusText.First(x => x.Status == s).Text;

    /// <summary>生成 docs/parity-tests.md 的内容。</summary>
    public static string Render(IReadOnlyList<ParityRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Java 用例逐条对照");
        sb.AppendLine();
        sb.AppendLine("由 `tools/ParityTests` 生成，不要手改。状态来自 C# 测试上的 `[Upstream]` 标注，见 [0003](decisions/0003-testing-and-parity.md)。");
        sb.AppendLine($"对照的是 easy-query 的 `sql-test`（上游）与 `sql-fork-test`（fork，Id 以 `fork/` 开头），共 {rows.Count} 个用例。");
        sb.AppendLine();
        sb.AppendLine("合计：" + string.Join("、", StatusText.Select(s => $"{s.Text} {rows.Count(r => r.Status == s.Status)}")) + "。");
        sb.AppendLine();
        sb.AppendLine("## 按能力");
        sb.AppendLine();
        sb.AppendLine("| 能力 | key | 里程碑 | 用例 | " + string.Join(" | ", StatusText.Select(s => s.Text)) + " |");
        sb.AppendLine("| --- | --- | --- | --- | " + string.Join(" | ", StatusText.Select(_ => "---")) + " |");
        foreach (var group in Groups(rows))
        {
            var c = Capabilities.Get(group.Key);
            sb.AppendLine($"| {c.Title} | `{c.Key}` | {c.Milestone} | {group.Count()} | " +
                          string.Join(" | ", StatusText.Select(s => group.Count(r => r.Status == s.Status))) + " |");
        }
        sb.AppendLine();
        sb.AppendLine("## 逐条");
        foreach (var group in Groups(rows))
        {
            var c = Capabilities.Get(group.Key);
            sb.AppendLine();
            sb.AppendLine($"### {c.Title}（`{c.Key}`，{c.Milestone}）");
            sb.AppendLine();
            sb.AppendLine("| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var r in group)
            {
                var where = r.Mark is null ? "" : r.Mark.Reason is null ? r.Mark.Location : $"{r.Mark.Location}：{r.Mark.Reason}";
                var sql = r.Java.Sqls.Count == 0 ? "" : $"`{Cell(Summary(r.Java.Sqls[0]))}`";
                sb.AppendLine($"| {r.Java.Id} | {Text(r.Status)} | {Cell(where)} | {sql} |");
            }
        }
        return sb.ToString();
    }

    private static IEnumerable<IGrouping<string, ParityRow>> Groups(IReadOnlyList<ParityRow> rows) =>
        rows.GroupBy(r => r.Java.Capability)
            .OrderBy(g => Capabilities.Get(g.Key).Milestone, StringComparer.Ordinal)
            .ThenBy(g => g.Key, StringComparer.Ordinal);

    private static string Summary(string sql)
    {
        var flat = string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length > 100 ? flat[..100] + "…" : flat;
    }

    private static string Cell(string text) => text.Replace("|", "\\|").Replace("`", "'");
}
