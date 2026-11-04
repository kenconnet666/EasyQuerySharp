namespace EasyQuerySharp.Tests.Live.Infrastructure;

/// <summary>
/// 读取实库测试的配置：优先取进程环境变量（CI 用），缺失时回退到仓库根的 <c>.local/db.env</c>（本地用，由 <c>infra/dev/db.sh</c> 生成）。
/// </summary>
public static class LocalEnvironment
{
    private static readonly Lazy<Dictionary<string, string>> DbEnv = new(LoadDbEnv);

    /// <summary>取一个配置值；环境变量和 <c>.local/db.env</c> 都没有时返回 null。</summary>
    public static string? Get(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        return DbEnv.Value.TryGetValue(name, out var local) && local.Length > 0 ? local : null;
    }

    private static Dictionary<string, string> LoadDbEnv()
    {
        var file = FindDbEnv();
        if (file is null) return [];

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            result[line[..eq].Trim()] = Unquote(line[(eq + 1)..].Trim());
        }
        return result;
    }

    // 从测试程序所在目录逐级向上找，测试在 bin/ 下运行
    private static string? FindDbEnv()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".local", "db.env");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // db.sh 给含分号的连接串加了单引号，便于 bash source；这里去掉
    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0] ? value[1..^1] : value;
}
