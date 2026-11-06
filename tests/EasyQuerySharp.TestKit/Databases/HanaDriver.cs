using System.Data.Common;
using System.Reflection;

namespace EasyQuerySharp.TestKit.Databases;

/// <summary>
/// SAP HANA 的 ADO.NET 驱动（Sap.Data.Hana）随 HANA 客户端安装、不在 nuget.org 上，所以不引用包，运行时从客户端的 dotnetcore 目录加载。
/// 目录依次取 <c>EQS_HANA_DRIVER</c>、<c>HDBDOTNETCORE</c>（客户端安装程序设置的）、默认安装位置；找不到驱动时 HANA 按没有配置处理。
/// </summary>
internal static class HanaDriver
{
    private static readonly string[] DefaultDirectories =
        [@"C:\Program Files\sap\hdbclient\dotnetcore", "/usr/sap/hdbclient/dotnetcore"];

    /// <summary>登记用的 HANA 测试库：驱动加载不到时带上原因，测试跳过（在 <c>EQS_REQUIRE_DBS</c> 里时判失败）。</summary>
    public static TestDatabase Database()
    {
        var (factory, missing) = Load();
        return new TestDatabase("hana", "SAP HANA Express 2.0", factory ?? Unavailable.Instance, ParameterPrefix: ":",
            DatabaseKeyword: "Current Schema", MissingDriver: missing);
    }

    private static (DbProviderFactory? Factory, string? Missing) Load()
    {
        var directory = new[] { LocalEnvironment.Get("EQS_HANA_DRIVER"), Environment.GetEnvironmentVariable("HDBDOTNETCORE") }
            .Concat(DefaultDirectories)
            .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d));
        if (directory is null) return (null, "没有找到 HANA 客户端（安装 SAP HANA client，或用 EQS_HANA_DRIVER 指向它的 dotnetcore 目录）");

        var assembly = DriverAssembly(directory);
        if (assembly is null) return (null, $"{directory} 下没有适用于 .NET {Environment.Version.Major} 的 Sap.Data.Hana.Net.v*.dll");

        // 托管驱动按 HDBDOTNETCORE 找原生库 libadonetHDB；客户端装好后新开的进程才继承安装程序设的变量
        Environment.SetEnvironmentVariable("HDBDOTNETCORE", directory);
        try
        {
            var factory = Assembly.LoadFrom(assembly).GetType("Sap.Data.Hana.HanaFactory")
                ?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as DbProviderFactory;
            return factory is null ? (null, $"{assembly} 里没有 Sap.Data.Hana.HanaFactory.Instance") : (factory, null);
        }
        // 在静态初始化里抛出会让所有用到 TestDatabases 的测试都失败；只让 HANA 不可用
        catch (Exception ex) when (ex is IOException or BadImageFormatException)
        {
            return (null, $"加载 {assembly} 失败：{ex.Message}");
        }
    }

    // 客户端按目标框架分目录（v6.0、v8.0、v10.0）：取不高于当前运行时的最新一版
    private static string? DriverAssembly(string directory) =>
        Directory.EnumerateDirectories(directory, "v*")
            .Select(d => (Path: d, Version: Version.TryParse(Path.GetFileName(d)[1..], out var v) ? v : null))
            .Where(d => d.Version is not null && d.Version.Major <= Environment.Version.Major)
            .OrderByDescending(d => d.Version)
            .SelectMany(d => Directory.EnumerateFiles(d.Path, "Sap.Data.Hana.Net.v*.dll"))
            .FirstOrDefault();

    // 没有驱动时占位：不会被用到，TestDatabase 在取连接串时就跳过了
    private sealed class Unavailable : DbProviderFactory
    {
        public static readonly Unavailable Instance = new();
    }
}
