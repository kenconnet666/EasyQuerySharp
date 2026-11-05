using BenchmarkDotNet.Running;

// 入口：dotnet run -c Release --project benchmarks/EasyQuerySharp.Benchmarks -- --filter *
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
