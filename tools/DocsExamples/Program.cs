using System.Globalization;
using System.Reflection;
using System.Text;
using EasyQuerySharp;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Docs;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Samples;
using Microsoft.Data.Sqlite;

// 文档卡片：每张卡片在 SQLite 上实跑并录下结果，再用录下的结果在其余方言上各跑一遍，记下各方言的 SQL。
// 用法：dotnet run --project tools/DocsExamples            写入 docs/sql-snapshots
//       dotnet run --project tools/DocsExamples -- --check 只校验（快照过期、SQLite 上失败、方言意外报错都算失败）

var check = args.Contains("--check");
var root = FindRepositoryRoot();
var snapshots = Path.Combine(root, "docs", "sql-snapshots");
(string Name, SqlDialect Dialect)[] dialects =
[
    ("sqlite", SqliteDialect.Instance),
    ("mysql", MySqlDialect.Instance),
    ("pgsql", PostgreSqlDialect.Instance),
    ("mssql", SqlServerDialect.Instance),
    ("oracle", OracleDialect.Instance),
];

var cards = typeof(ExampleAttribute).Assembly.GetTypes()
    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
    .Select(m => (Method: m, Attribute: m.GetCustomAttribute<ExampleAttribute>()))
    .Where(c => c.Attribute is not null)
    .OrderBy(c => c.Method.DeclaringType!.FullName, StringComparer.Ordinal).ThenBy(c => c.Method.MetadataToken)
    .ToList();

var problems = new List<string>();
var outputs = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
var file = Path.Combine(Path.GetTempPath(), $"eqs_docs_{Environment.ProcessId}.db");
try
{
    foreach (var (method, attribute) in cards)
    {
        var id = attribute!.Page + "/" + method.Name;
        var recording = await RunOnSqliteAsync(method, file);
        if (recording.Error is not null) problems.Add($"{id}：SQLite 上执行失败：{recording.Error}");
        foreach (var (name, dialect) in dialects)
        {
            if (attribute.Only is not null && attribute.Only != name) continue;
            var run = name == "sqlite" ? recording.Run : await ReplayAsync(method, dialect, recording.Tape);
            if (run.Error is not null && !attribute.Unsupported.Split(',', StringSplitOptions.TrimEntries).Contains(name))
                problems.Add($"{id}：{name} 上报错：{run.Error}");
            var page = attribute.Page.Replace('/', '.') + "." + name + ".sql";
            if (!outputs.TryGetValue(page, out var text)) outputs[page] = text = new StringBuilder();
            Append(text, method.Name, attribute.Title, run);
        }
    }
}
finally
{
    SqliteConnection.ClearAllPools();
    File.Delete(file);
}

Directory.CreateDirectory(snapshots);
foreach (var (page, text) in outputs)
{
    var path = Path.Combine(snapshots, page);
    var content = text.ToString().ReplaceLineEndings("\n");
    if (check)
    {
        if (!File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings("\n") != content) problems.Add($"快照过期：docs/sql-snapshots/{page}（重新运行 tools/DocsExamples）");
    }
    else await File.WriteAllTextAsync(path, content);
}
foreach (var stale in Directory.GetFiles(snapshots, "*.sql").Where(f => !outputs.ContainsKey(Path.GetFileName(f))))
{
    if (check) problems.Add($"多余的快照：{Path.GetFileName(stale)}");
    else File.Delete(stale);
}

Console.WriteLine($"{cards.Count} 张卡片，{outputs.Count} 个快照文件{(check ? "（校验）" : "")}");
foreach (var p in problems) Console.Error.WriteLine(p);
return problems.Count == 0 ? 0 : 1;

// 在新建的示例库上实跑一张卡片，录下结果
static async Task<(CardRun Run, Tape Tape, string? Error)> RunOnSqliteAsync(MethodInfo method, string file)
{
    var cs = $"Data Source={file};Pooling=False";
    await SampleDb.ResetAsync(SampleDb.Create(SqliteDialect.Instance, SqliteFactory.Instance.CreateDataSource(cs)));
    await using var connection = new SqliteConnection(cs);
    await connection.OpenAsync();
    var tape = Tape.Record(connection);
    var run = await RunAsync(method, SqliteDialect.Instance, tape);
    return (run, tape, run.Error);
}

// 用录下的结果在另一种方言上跑同一张卡片
static Task<CardRun> ReplayAsync(MethodInfo method, SqlDialect dialect, Tape recording) => RunAsync(method, dialect, recording.Replay());

static async Task<CardRun> RunAsync(MethodInfo method, SqlDialect dialect, Tape tape)
{
    var capture = new Capture();
    var db = SampleDb.Create(dialect, tape.DataSource, o => o.AddListener(capture));
    try
    {
        var task = (Task)method.Invoke(null, [db])!;
        await task;
        return new CardRun(capture.Statements, null);
    }
    catch (Exception ex)
    {
        var inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
        return new CardRun(capture.Statements, inner.Message.ReplaceLineEndings(" "));
    }
}

// 每张卡片：-- 名字：标题，然后每条语句和参数；出错时最后一行写 -- error
static void Append(StringBuilder text, string name, string title, CardRun run)
{
    text.Append("-- ").Append(name).Append('：').Append(title).Append('\n');
    foreach (var statement in run.Statements)
    {
        text.Append(statement.Text).Append(";\n");
        if (statement.Parameters.Count > 0) text.Append("-- params: [").AppendJoin(", ", statement.Parameters.Select(p => Format(p.Value))).Append("]\n");
    }
    if (run.Error is not null) text.Append("-- error: ").Append(run.Error).Append('\n');
    text.Append('\n');
}

static string Format(object? value) => value switch
{
    null => "NULL",
    string s => "'" + s.Replace("'", "''") + "'",
    bool b => b ? "true" : "false",
    DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture).TrimEnd('.') + "'",
    DateOnly d => "'" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'",
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString() ?? "",
};

static string FindRepositoryRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "EasyQuerySharp.slnx"))) return dir.FullName;
    throw new InvalidOperationException("找不到仓库根目录（EasyQuerySharp.slnx）");
}

/// <summary>一张卡片在一种方言上的执行：发出的语句，出错时的消息。</summary>
internal sealed record CardRun(IReadOnlyList<SqlStatement> Statements, string? Error);

/// <summary>记下执行的每条语句。</summary>
internal sealed class Capture : IExecutionListener
{
    public List<SqlStatement> Statements { get; } = [];

    public void OnExecuting(SqlExecutionEvent e) => Statements.Add(e.Statement);
}
