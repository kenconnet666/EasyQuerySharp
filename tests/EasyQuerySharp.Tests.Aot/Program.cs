using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Docs;
using EasyQuerySharp.Samples;
using Microsoft.Data.Sqlite;

// 每张卡片一行：卡片 id、OK 加结果，或 ERROR 加异常。JIT 与 Native AOT 两次的输出逐行比较（见项目文件的说明）。
// 参数：输出文件的路径（不给时写到标准输出）。
var cards = Probe.Cards();

var output = new StringBuilder();
var file = Path.Combine(Path.GetTempPath(), $"eqs_aot_{Environment.ProcessId}.db");
var failed = 0;
foreach (var (method, attribute) in cards)
{
    var id = attribute!.Page + "/" + method.Name;
    string line;
    try
    {
        SqliteConnection.ClearAllPools();
        File.Delete(file);
        var db = SampleDb.Create(SqliteDialect.Instance, SqliteFactory.Instance.CreateDataSource($"Data Source={file};Pooling=False"));
        await SampleDb.ResetAsync(db);
        var task = (Task)method.Invoke(null, [db])!;
        await task;
        line = "OK\t" + Probe.Dump(Probe.ResultOf(task), 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }
    catch (Exception ex)
    {
        var inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
        while (inner is TypeInitializationException { InnerException: { } cause }) inner = cause;
        line = "ERROR\t" + inner.GetType().Name + ": " + inner.Message.ReplaceLineEndings(" ");
        // 失败时附上 EasyQuerySharp 里最近的几帧（写到 stderr，不进比较的输出）
        Console.Error.WriteLine(id + ": " + string.Join(" <- ", (inner.StackTrace ?? "").Split('\n')
            .Where(f => f.Contains("EasyQuerySharp", StringComparison.Ordinal)).Take(4).Select(f => f.Trim())));
        if (!attribute.Unsupported.Split(',', StringSplitOptions.TrimEntries).Contains("sqlite")) failed++;
    }
    output.Append(id).Append('\t').Append(line).Append('\n');
}
File.Delete(file);
output.Append($"cards: {cards.Count}, errors: {failed}\n");

if (args.Length > 0) await File.WriteAllTextAsync(args[0], output.ToString());
else Console.Write(output);
return 0;

/// <summary>按反射找卡片、取结果、逐层输出（放在普通类里：顶层语句里的 lambda 算在 Main 上，压制警告的特性管不到）。</summary>
internal static class Probe
{
    // 这个程序集整体保留（项目文件的 TrimmerRootAssembly），按反射找卡片是安全的
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "本程序集整体保留")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "本程序集整体保留")]
    public static List<(MethodInfo Method, ExampleAttribute? Attribute)> Cards() =>
        typeof(ExampleAttribute).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<ExampleAttribute>()))
            .Where(c => c.Attribute is { Only: null or "sqlite" })
            .OrderBy(c => c.Method.DeclaringType!.FullName, StringComparer.Ordinal).ThenBy(c => c.Method.Name, StringComparer.Ordinal)
            .ToList();

    // 卡片返回 Task<T>：经反射取 Result（保留 Task<> 的 Result，Native AOT 下才取得到）
    [DynamicDependency("get_Result", typeof(Task<>))]
    // 分页结果是 EasyQuerySharp 的类型，只有这里的输出按反射读它
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(EasyQuerySharp.Querying.Page<>))]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "上面的 DynamicDependency 保留了 Result")]
    public static object? ResultOf(Task task) => task.GetType().GetProperty("Result")?.GetValue(task);

    // 结果按值逐层写出：集合写成 [..]，对象写成 {属性=值,..}（按属性名排序），浮点与日期用不变区域的格式。
    // 卡片的结果类型在本程序集（整体保留）或示例模型里（由 EasyQuerySharp 的生成器保留）
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "结果类型的成员已保留，见上")]
    public static string Dump(object? value, int depth, HashSet<object> seen)
    {
        if (value is null) return "null";
        if (depth > 8) return "…";
        switch (value)
        {
            case string s:
                return "\"" + s + "\"";
            case DateTime d:
                return d.ToString("O", CultureInfo.InvariantCulture);
            case IFormattable f when value.GetType().IsPrimitive || value is decimal or Guid or DateOnly or TimeOnly or TimeSpan or DateTimeOffset || value.GetType().IsEnum:
                return f.ToString(null, CultureInfo.InvariantCulture);
            case bool b:
                return b ? "true" : "false";
            case byte[] bytes:
                return Convert.ToHexString(bytes);
        }
        if (!value.GetType().IsValueType && !seen.Add(value)) return "^";
        if (value is IDictionary dictionary)
            return "{" + string.Join(",", dictionary.Keys.Cast<object>().Select(k => k.ToString()!).Order(StringComparer.Ordinal)
                .Select(k => k + "=" + Dump(dictionary[k], depth + 1, seen))) + "}";
        if (value is IEnumerable items)
            return "[" + string.Join(",", items.Cast<object?>().Select(i => Dump(i, depth + 1, seen))) + "]";
        var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead).OrderBy(p => p.Name, StringComparer.Ordinal);
        return "{" + string.Join(",", properties.Select(p => p.Name + "=" + Dump(p.GetValue(value), depth + 1, seen))) + "}";
    }
}
