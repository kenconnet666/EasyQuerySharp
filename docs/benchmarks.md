# 基准对比

EasyQuerySharp 和 EF Core、Dapper、FreeSql、SqlSugar 在查询翻译、物化、对象树三项上的对比，以及翻译结果要不要缓存的结论。代码在 `benchmarks/EasyQuerySharp.Benchmarks`。

下面的数字是 2026-11-06 在一台同时跑着其它构建和测试的开发机上测的（CPU 占用 40%–80%），绝对值偏高、有噪声，适合看量级和相对快慢。先跑的一轮只有 3 次迭代、误差很大，这里列的是第二轮（15 次迭代，多数项的 99.9% 置信区间在 ±5%–15%）；两轮的大致排序一致，相差不到 20% 的项（如 1000 行实体里的 Dapper 与 EF Core 预编译、ADO.NET 与 FreeSql）先后有出入。

## 怎么跑

```powershell
# BenchmarkDotNet 会再调用 dotnet 构建基准，PATH 上的 dotnet 要能解析到 .NET 10 SDK
$env:PATH = "$HOME\.dotnet;$env:PATH"
dotnet run -c Release --project benchmarks/EasyQuerySharp.Benchmarks -- --filter '*' --job short --warmupCount 5 --iterationCount 15
# 只跑一类：--filter '*Translation*' / '*Materialization*' / '*ObjectTree*'
# 不跑基准：各 ORM 把每个查询执行一次、核对结果，并打印实际执行的 SQL
dotnet run -c Release --project benchmarks/EasyQuerySharp.Benchmarks -- --sql
```

全部跑完约 12 分钟。结果在 `BenchmarkDotNet.Artifacts/results`（不提交）。

## 环境

| 项 | 值 |
|---|---|
| CPU | AMD Ryzen 5 6600H，6 核 12 线程，3.3 GHz |
| 系统 | Windows 11（10.0.26300） |
| .NET | SDK 10.0.403，运行时 10.0.14，X64 RyuJIT x86-64-v3 |
| BenchmarkDotNet | 0.15.8；ShortRun（1 个进程）加 `--warmupCount 5 --iterationCount 15` |
| 数据库 | SQLite，Microsoft.Data.Sqlite 10.0.14，临时目录里的文件库，WAL 模式 |

| ORM | 包与版本 |
|---|---|
| EasyQuerySharp | 当前 master |
| EF Core | Microsoft.EntityFrameworkCore.Sqlite 10.0.14 |
| Dapper | Dapper 2.1.89 |
| FreeSql | FreeSql.Provider.SqliteCore 3.5.311（基于 Microsoft.Data.Sqlite） |
| SqlSugar | SqlSugarCore 5.1.4.221 |

对比用的四个包只在基准项目里引用，核心包仍然零依赖。

## 场景

数据：100 个客户，每个客户 20 个订单，每个订单 5 条明细；外键列有索引。订单实体 7 列（含一个可空文本列、一个日期列），DTO 4 列。

| 类别 | 查询 | 基线 |
|---|---|---|
| 翻译 Simple | `Where(o => o.Id > minId).OrderBy(o => o.Id).Take(10)`，读实体，只生成 SQL 不执行 | EasyQuerySharp |
| 翻译 Complex | 三个条件、两级排序、`Skip(20).Take(10)`、投影到 DTO，只生成 SQL | EasyQuerySharp |
| 物化 | 整条查询（翻译、执行、读取）：`WHERE id > @p ORDER BY id LIMIT n`，n = 10 / 1000，读成实体和 DTO | 手写 ADO.NET（按列序号读） |
| 对象树 一层 | 50 个订单，各带 5 条明细（共 250 条） | 手写 Dapper |
| 对象树 两层 | 10 个客户，各带 20 个订单、每个订单 5 条明细（共 200 个订单、1000 条明细） | 手写 Dapper |

只生成 SQL 的写法：EasyQuerySharp `ToSql()`，EF Core `ToQueryString()`，FreeSql `ToSql()`，SqlSugar `ToSql()`。手写 Dapper 的对象树是每层一条 `WHERE 外键 IN @ids`，按字典挂到父对象上。

### 怎么保证公平

- **同一组 POCO、同一个库**：表名、列名以 EasyQuerySharp 的特性为准，其它 ORM 用各自的代码配置对齐（EF Core 的 `OnModelCreating`、FreeSql 的 `Aop.ConfigEntity`、SqlSugar 的 `EntityService`）。GlobalSetup 里先让每个 ORM 把每个查询跑一遍，和基线逐行核对，不一致就中止，避免某个 ORM 因为映射没配对而读空、显得更快。
- **都生成命令参数**：条件里的值来自字段（闭包）。FreeSql 默认把值直接拼进 SQL，这里打开了 `UseGenerateCommandParameterWithLambda`。
- **EF Core**：查询都加 `AsNoTracking()`；复用同一个 `DbContext`（不追踪就不积累状态，相当于池化上下文）；物化另列 `EF.CompileAsyncQuery` 的预编译查询，对象树另列 `AsSplitQuery()`。
- **连接**：EasyQuerySharp、EF Core、SqlSugar、Dapper、ADO.NET 每次查询从 Microsoft.Data.Sqlite 的连接池取连接、用完归还（WAL 下约 2 µs）；FreeSql 自带连接池，连接常开。
- **WAL**：SQLite 默认的回滚日志模式下，每个读事务都要检查日志文件是否存在，在这台 Windows 机器上一条查询固定多出约 300 µs，会把各 ORM 之间的差别淹没，所以库用 WAL（服务端用 SQLite 的常规配置）。
- **FreeSql、SqlSugar 是 Debug 构建**：两者在 NuGet 上发布的程序集带 `DebuggableAttribute(DisableOptimizations)`，JIT 不优化它们自己的代码。用户拿到的就是这样，所以照原样测，并关掉了 BenchmarkDotNet 对此的校验。

### SQL 形状

`--sql` 打印的实际 SQL 里，条件、排序、选的列各家一样，差别在下面两处：

| | EasyQuerySharp | EF Core | FreeSql | SqlSugar |
|---|---|---|---|---|
| 分页 | `LIMIT 10`（字面量） | `LIMIT @p`（参数） | `limit 0,10` | `LIMIT 0,10` |
| 对象树 | 每层一条 `IN (?,?,…)`，每个父键一个参数 | 默认一条 LEFT JOIN；拆分查询每层一条，JOIN 父查询 | 每层一条 `IN (3,4,…)`，键写成字面量 | 同 FreeSql |

## 结果

比值 = 平均值 ÷ 同组基线的平均值；分配是每次操作在托管堆上分配的字节数。

### 翻译

| ORM | Simple | 比值 | 分配 | Complex | 比值 | 分配 |
|---|---:|---:|---:|---:|---:|---:|
| EasyQuerySharp | 6.6 µs | 1.00 | 12.9 KB | 20.7 µs | 1.00 | 24.3 KB |
| EF Core（命中查询缓存） | 25.6 µs | 3.89 | 11.2 KB | 49.8 µs | 2.40 | 22.2 KB |
| FreeSql | 34.9 µs | 5.30 | 9.9 KB | 146.3 µs | 7.06 | 33.4 KB |
| SqlSugar | 407.0 µs | 61.7 | 58.2 KB | 948.2 µs | 45.7 | 104.5 KB |

EasyQuerySharp 的 Simple 再拆开（一次性用反射调内部的 `QueryCompiler` 测的，不在基准项目里；和物化的表不是同一轮，整条查询的耗时因机器负载不同而偏低）：

| 测的范围 | 耗时 | 分配 |
|---|---:|---:|
| 建查询对象（C# 编译器生成的表达式树 + `Query<T>`） | 2.1 µs | 3.3 KB |
| 建查询对象 + 翻译成 SQL AST | 4.9 µs | 9.1 KB |
| 建查询对象 + 翻译 + 生成读取委托（命中 ShaperCache） | 6.8 µs | 12.2 KB |
| 建查询对象 + 翻译 + 渲染（`ToSql`） | 6.4 µs | 12.9 KB |
| 整条 10 行查询（`ToListAsync`） | 76 µs | 22.6 KB |

### 物化

| ORM | 实体 10 行 | 比值 | 实体 1000 行 | 比值 | 分配（1000 行） |
|---|---:|---:|---:|---:|---:|
| ADO.NET | 58.2 µs | 1.00 | 2.01 ms | 1.00 | 230 KB |
| Dapper | 70.5 µs | 1.21 | 2.54 ms | 1.27 | 348 KB |
| EasyQuerySharp | 106.4 µs | 1.83 | 2.86 ms | 1.43 | 388 KB |
| EF Core | 159.4 µs | 2.74 | 2.40 ms | 1.19 | 432 KB |
| EF Core（预编译） | 105.6 µs | 1.81 | 2.24 ms | 1.12 | 424 KB |
| FreeSql | 151.4 µs | 2.60 | 1.98 ms | 0.99 | 241 KB |
| SqlSugar | 670.2 µs | 11.5 | 5.70 ms | 2.84 | 440 KB |

| ORM | DTO 10 行 | 比值 | DTO 1000 行 | 比值 | 分配（1000 行） |
|---|---:|---:|---:|---:|---:|
| ADO.NET | 36.7 µs | 1.00 | 1.09 ms | 1.00 | 167 KB |
| Dapper | 45.0 µs | 1.23 | 1.74 ms | 1.59 | 238 KB |
| EasyQuerySharp | 78.7 µs | 2.15 | 2.36 ms | 2.16 | 264 KB |
| EF Core | 116.0 µs | 3.16 | 1.96 ms | 1.80 | 372 KB |
| EF Core（预编译） | 72.8 µs | 1.99 | 1.65 ms | 1.51 | 361 KB |
| FreeSql | 194.1 µs | 5.29 | 4.56 ms | 4.18 | 604 KB |
| SqlSugar | 533.3 µs | 14.5 | 4.76 ms | 4.36 | 340 KB |

### 对象树

| ORM | 一层 | 比值 | 分配 | 两层 | 比值 | 分配 |
|---|---:|---:|---:|---:|---:|---:|
| Dapper（手写） | 0.61 ms | 1.00 | 109 KB | 2.44 ms | 1.00 | 438 KB |
| EasyQuerySharp | 1.21 ms | 1.98 | 272 KB | 3.89 ms | 1.60 | 1088 KB |
| EF Core（单条 JOIN） | 1.67 ms | 2.72 | 192 KB | 24.30 ms | 9.98 | 964 KB |
| EF Core（拆分查询） | 1.03 ms | 1.69 | 170 KB | 3.17 ms | 1.30 | 653 KB |
| FreeSql | 1.78 ms | 2.91 | 315 KB | 4.45 ms | 1.83 | 1353 KB |
| SqlSugar | 1.67 ms | 2.73 | 244 KB | 4.09 ms | 1.68 | 823 KB |

## 结论

- **翻译**：EasyQuerySharp 每次都完整翻译，仍是四家里最快的，简单查询 6.6 µs、复杂查询 21 µs，不到 EF Core 命中查询缓存后开销的一半；FreeSql 慢 5–7 倍，SqlSugar 慢 45–60 倍（SqlSugar 一条 10 行查询的大部分时间花在翻译上）。
- **少量行**：读 10 行时 EasyQuerySharp 和 EF Core 预编译查询相当（实体 106 µs 对 106 µs，DTO 79 µs 对 73 µs），比 EF Core 普通查询快约三分之一，比 FreeSql 快 30%–60%。和手写 ADO.NET 的差距（约 40–50 µs）里，建查询对象、翻译、生成读取委托、渲染合起来约 8 µs，其余在执行与逐列读取上。
- **大量行**：读 1000 行时 EasyQuerySharp 的逐行开销最高，实体 1.43 倍、DTO 2.16 倍于 ADO.NET，落后于 EF Core 和 Dapper。原因在读值：每一列都走 `ValueReader.Read`（`GetValue` 装箱，再 `ConvertTo` 转类型），DTO 每行比 ADO.NET 多分配约 100 字节。FreeSql 实体读取最快（与 ADO.NET 持平），但它的 DTO 投影慢。
- **对象树**：EF Core 的拆分查询最快（一层 1.69 倍、两层 1.30 倍于手写 Dapper），EasyQuerySharp 次之（1.98 / 1.60 倍），FreeSql、SqlSugar 与它同量级、略慢；EF Core 默认的单条 JOIN 在两层时因为笛卡尔积膨胀，慢到 10 倍。EasyQuerySharp 两层的分配是手写的 2.5 倍，主要是逐列读取的装箱，以及子行按键挂回父对象时的中间对象（键、分组列表）。

## 翻译结果要不要缓存

**不缓存**，理由：

1. **占比小**：翻译、生成读取委托、渲染合起来约 6 µs，只占一条 10 行查询（76 µs）的约 8%，而这里还是没有网络往返的进程内 SQLite；连真实数据库时一次往返通常就是几百微秒，占比更小。
2. **能省的有限**：缓存命中时省掉的就是上面这约 6 µs，建查询对象的 2.1 µs 省不掉。缓存本身要对整棵表达式树算结构键、再把闭包里的值抽成参数：EF Core 走这一套，Simple 的翻译就要 26 µs，普通查询比预编译查询多 40–50 µs，都比 EasyQuerySharp 完整翻译一遍还慢。
3. **风险大**：键要覆盖 `QueryState` 的全部内容（Include 的配置委托、拦截器选择、换表、导航过滤、全局过滤器读取的上下文等），漏一项就是返回错误 SQL 的正确性问题。

更值得做的是逐行读取：按列的 CLR 类型生成强类型读取（`GetInt64` / `GetDouble` / `GetString` / `GetFieldValue<T>`），驱动返回的类型对不上时再退回 `ValueReader`，可以去掉每列一次的装箱。已记进 [BACKLOG](BACKLOG.md)。
