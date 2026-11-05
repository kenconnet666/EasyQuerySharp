```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9818/25H2/2025Update/HudsonValley2)
Intel Xeon CPU E5-2698B v3 2.00GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method            | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------ |------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| TranslateOnly     |    23.10 μs |   168.70 μs |  9.247 μs |  0.02 |    0.01 | 0.4883 |  11.77 KB |        0.76 |
| ReadEntities      | 1,215.38 μs |   407.49 μs | 22.336 μs |  1.29 |    0.03 | 1.9531 |  48.46 KB |        3.14 |
| ReadProjection    | 1,270.42 μs | 1,406.57 μs | 77.099 μs |  1.34 |    0.08 |      - |  39.71 KB |        2.58 |
| HandWrittenAdoNet |   945.49 μs |   425.26 μs | 23.310 μs |  1.00 |    0.03 |      - |  15.41 KB |        1.00 |
