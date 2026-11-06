using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using EasyQuerySharp.Benchmarks;

// 入口：dotnet run -c Release --project benchmarks/EasyQuerySharp.Benchmarks -- --filter *
// --sql：不跑基准，各 ORM 把每个基准的查询执行一次、核对结果，并打印执行的 SQL（核对各家 SQL 形状用）。
if (args is ["--sql"])
{
    OrmBenchmark[] benchmarks =
    [
        new TranslationBenchmarks { SqlLog = Console.WriteLine },
        new MaterializationBenchmarks { SqlLog = Console.WriteLine, Rows = 10 },
        new ObjectTreeBenchmarks { SqlLog = Console.WriteLine },
    ];
    foreach (var benchmark in benchmarks)
    {
        Console.WriteLine($"==== {benchmark.GetType().Name}");
        await benchmark.SetupAsync();
        await benchmark.CleanupAsync();
    }
    return;
}

// FreeSql、SqlSugar 在 NuGet 上发布的是 Debug 构建（DebuggableAttribute 关闭了 JIT 优化），用户拿到的就是这样，
// 所以照原样测，关掉 BenchmarkDotNet 对依赖程序集“必须是 Release”的校验
var config = DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator);
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
