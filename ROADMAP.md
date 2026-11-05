# ROADMAP

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。每项一个分支一个 PR，规则见 [CLAUDE.md](CLAUDE.md)。

**目标**：完整对标 easy-query（Java）。对标基准是上游 `sql-test` 的 2315 个用例加 fork `sql-fork-test` 的 89 个，共 2404 个，每个都要有对应的 C# 测试，状态由 `tools/ParityTests` 统计。同时参考 easy-query-ts 的写法、测试结构和文档站。

设计依据：
- [0001 查询 API 路线](docs/decisions/0001-query-api-route.md)
- [0002 对象树与 DTO](docs/decisions/0002-object-tree-and-dto.md)
- [0003 测试对标](docs/decisions/0003-testing-and-parity.md)
- [架构](docs/design/architecture.md)
- [文档站](docs/design/docs-site.md)
- [能力对照总表](docs/parity.md)

## 每个里程碑的完成标准

1. **用例**：对照表里，归在本里程碑的能力"待做"为 0。偏离和不适用的每一条都写了理由。
2. **实库**：L3 共用套件中对应的用例组在 SQLite、MySQL 8.4、PG 17 上全部通过。从 M6 开始，还要在该方言的 CI 任务里全部通过。
3. **文档**：每个能力有文档页和示例卡片，各方言的 SQL 快照已更新并提交。
4. **CI**：全部任务通过。

## 执行顺序

M0 → M1 → M2 → M3 → M4 → M5 → M6 → M7 → M8 → M10，M9 按需穿插。

- 文档卡片随各里程碑一起补，M8 只负责收尾和站点。
- 某一项进入待决状态时，跳到顺序里下一个不依赖它的项。
- 每完成一块功能顺手整理（拆文件、合并重复、删死代码），整理单独提交。

2026-11 定下的剩余阶段（按顺序）：

| 阶段 | 内容 |
|---|---|
| A 基础设施 | L3 `LiveSuite`（1.12）、表达式形状缓存与基准（1.7）、生成器与分析器项目、`DocsExamples` 卡片与快照（1.11）、示例模型（0.10） |
| B M1–M3 清零 | 基本查询、函数、多表组合的剩余用例，以及它们需要的新能力 |
| C M4 | 一对多展开、按 DTO 自动带出与 `[MapFrom]`、树与递归 CTE、多对多分组合并 |
| D M5 | 聚合根保存、`Changes`、`DbBatch`、INSERT … SELECT、枚举 / 值对象、SM4、MERGE |
| E M6 | 各方言实库验证与新方言、方言专属函数、RETURNING、H2 用例改到 SQLite |
| F M7 | 表名映射、分库分表、search、按对象筛选剩余、code-first、二级缓存、依赖注入 |

已定的取舍：
- **M8 文档站暂不做**；卡片与 SQL 快照照常随功能补。
- **SAP HANA 也进 CI**：用 HANA Express 容器并压低内存（`--agree-to-sap-license`，限制 `global_allocation_limit`、关闭不需要的服务），跑不起来时再退回只在本地验证。
- 生成器项目引用 Microsoft.CodeAnalysis、依赖注入与缓存包引用 Microsoft.Extensions 的抽象包：都只在各自的包里，核心包仍零依赖。
- M10 不发 NuGet；基准对比、AOT 验证、README 可以做。

## M0 地基

- [x] 0.1 解决方案布局：
  - `src/`、`tests/` 两个目录；
  - `Directory.Build.props`：.NET 10、可空引用类型、警告视为错误、分析器级别 latest-recommended；
  - 中央包版本管理：同时固定有漏洞的传递依赖版本；
  - `global.json`：测试走 Microsoft Testing Platform。
- [x] 0.2 本地数据库 `infra/dev`：
  - compose 项目名 `easy-query-sharp-dev`，端口统一 4xxxx；
  - 默认启动 MySQL 8.4 和 PG 17；其余数据库按 profile 启动：polardb、mariadb、mssql、oracle、dameng、opengauss、clickhouse、db2、tdengine、hana；
  - `db.sh` 首次运行时生成 `.local/db.env`。
- [x] 0.3 实库测试基础设施：
  - `TestDatabases` 登记各数据库，连接串用 `EQS_<KEY>_CS`；
  - `EQS_REQUIRE_DBS` 列出的库连不上时判失败；
  - 连通性自检已在七个库上实测通过：SQLite、MySQL 8.4 / 8.0.18、MariaDB、PG、SQL Server、Oracle。
- [x] 0.4 CI `.github/workflows/ci.yml`：
  - `core` 任务：SQLite、MySQL、PG，跑整个解决方案；
  - 另有 MySQL 8.0.18、MariaDB、SQL Server、Oracle 四个任务；
  - 每天定时运行一次；失败时上传 TRX 报告。
- [x] 0.5 设计文档：0001–0003、架构、文档站、能力对照总表、本路线图、`CLAUDE.md`。
- [x] 0.6 推到 GitHub，让 CI 首次跑通。需要建远程仓库，等用户操作。
- [x] 0.7 `tools/ParityTests`：
  - 扫描 Java 的 2404 个用例并归类；
  - 读出 C# 测试上的 `[Upstream]` 标注；
  - 生成 `docs/parity-tests.md`；
  - `--scaffold` 生成测试骨架，期望 SQL 和参数从 Java 原样搬过来；
  - `--check` 加进 CI。
- [~] 0.8 L1 项目 `tests/EasyQuerySharp.Tests`：
  - `SqlOnly` 会话，对应 Java 的 `EmptyDataSource`；
  - `Listen()` / `ListenAll()` 捕获 SQL；
  - `AssertSql` 和 `AssertParams`，参数按 Java 风格输出类型名，例如 `123xxx(String)`。
- [x] 0.9 L2 项目 `tests/EasyQuerySharp.Tests.Upstream`：
  - 移植 Java 的实体模型（先移 `BaseTest` 用到的那些）；
  - `MySqlFixture`：建表并写入种子数据，code-first 做好之前先执行 Java 的 SQL 脚本；
  - 对照表的初始状态是 2404 条全部待做。
- [ ] 0.10 示例模型 `samples/EasyQuerySharp.Samples`：TS 文档站的 17 张表和种子数据原样移植。

## M1 内核与单表

覆盖能力：`query.basic` 402、`query.page` 22、`sql.native` 50、`write.insert` / `write.update` / `write.delete` 共 115、`column.generated`。方言做 MySQL、SQLite、PostgreSQL，也就是本地必测的三个库。

- [~] 1.1 元数据：
  - 特性 `[Table]`、`[Column]`、`[Key]`、`[ColumnIgnore]`、`[InsertIgnore]`、`[UpdateIgnore]`；
  - 命名约定，默认驼峰转下划线；
  - Source Generator 产出 `EntityMetadata` 和 `DbDataReader` 物化器，运行时不反射。
- [x] 1.2 查询模型：
  - 不可变的 `IQuery<T>`；
  - `Where` / `WhereIf` / `OrderBy` / `Select` / `Skip` / `Take` / `Distinct`；
  - 终结方法 `ToListAsync` / `FirstAsync` / `FirstOrDefaultAsync` / `SingleAsync` / `AnyAsync` / `CountAsync` / `FindAsync`。
- [x] 1.3 表达式翻译：
  - 比较、逻辑、算术、null、三元、`??`；
  - 闭包参数提取，翻译路径上禁止 `Compile()`；
  - `Contains` 要识别三种形状：数组、`List<T>`、first-class span 的 `MemoryExtensions`；
  - `string` 的 `Contains` / `StartsWith` / `EndsWith`，以及 `Like`；
  - 不支持的写法报错时，消息里带上原始表达式。
- [x] 1.4 SQL AST、`Dialect` 基类、渲染器：
  - 输出与 Java 逐字对齐：单表不加别名，多表依次 `t`、`t1`…，逗号后不留空格，列顺序等于属性声明顺序；
  - `ParameterStyle` 支持 `Question` / `Named` / `Positional`。
- [x] 1.5 MySQL 方言（同时兼容 8.0.18）、SQLite 方言、PostgreSQL 方言。
- [x] 1.6 执行层：
  - `Session` / `Driver` 基于 `DbDataSource`；
  - 环境事务基于 `AsyncLocal`；
  - 执行事件，对接 `ILogger` 和 `ActivitySource`；
  - 全链路传递 `CancellationToken`。
- [x] 1.7 表达式形状缓存与基准（`benchmarks/EasyQuerySharp.Benchmarks`）。实测编译读取函数约 1 ms，占一条查询开销的大头，翻译 + 渲染只要十几 µs；所以缓存的是读取函数：按读取树的结构做键，引用类型常量（带出计划、换算委托）提成参数，同形状只编译一次（`ShaperCache`）。读 100 行实体从 2.2 ms 降到 1.2 ms（手写 ADO.NET 0.95 ms）；`ToSql` 不再编译读取函数。翻译结果本身暂不缓存，等基准显示有必要再做。
- [x] 1.8 写入：
  - `Insert` 支持返回自增键或整个实体；
  - `Update` 支持 `Set` 表达式和按主键整行更新；
  - `Delete` 没有条件时必须写 `.All()`；
  - 写入一律以 `ExecuteAsync` 结束。
- [x] 1.9 原生 SQL：`db.Sql<T>($"...")`（插值处理器，值自动参数化）和 `Sql.Raw<T>`。
- [x] 1.10 `ToPageAsync`。
- [ ] 1.11 `tools/DocsExamples` 首版：在 SQLite 上实跑、回放驱动、SQL 快照、`--check`；首批页面是"查询 / 基础""写入"。
- [x] 1.12 L3 `LiveSuite` 基类和 `LiveSuiteOptions`，首批用例组：queries、writes、native SQL（每个数据库一个薄子类，每个用例 code-first 重建表与种子；已在 SQLite、MySQL 8.4、PG 17 全部通过）。

## M2 表达式与函数

覆盖能力：`func.string` 128、`func.date` 96、`func.general` 53、`func.joining` 29、`func.json` 12、`func.math` 4、`expr.case` 27、`window` 38、`lock` 27，加上 fork 的 dateTrunc、NULLIF、null-safe 等值、窗口 frame、NoWait / SkipLocked。

- [x] 2.1 函数表机制：
  - `[SqlFunction]` 扩展方法；
  - BCL 方法映射：`string`、`Math`、`DateTime` / `DateOnly` / `DateTimeOffset`；
  - 用户的自定义函数和内置函数写法一样。
- [x] 2.2 字符串、数学、通用函数：`Sql.Greatest` / `Least` / `NullIf` / `EqualsNullSafe`。
- [~] 2.3 日期函数：格式串按方言翻译，加减、取部分、`DateDiff`、`DateTrunc`。
- [~] 2.4 `Sql.Case`，以及带过滤的聚合，例如 `g.Count(p => p.Paid)`。
- [~] 2.5 窗口函数：`Sql.Over.*` 系列，包括 frame、`Ntile` / `PercentRank` / `CumeDist`（API、渲染与 `Part<T,…>` 投影已完成；取值函数省略排序时沿用查询的 ORDER BY 待做）。
- [~] 2.6 字符串聚合 `StringJoin`，以及 JSON 取值。
- [x] 2.7 行锁：`ForUpdate` / `ForUpdateNoWait` / `ForUpdateSkipLocked`（Java 没有 FOR SHARE，不做）。H2、达梦、DB2、ClickHouse、DuckDB 的冒烟用例随 M6 方言一起移植。
- [~] 2.8 SQL Server 和 Oracle 方言的**渲染部分**，只做 SQL 比对：Java 的三组函数测试（约 100 个用例）在 MySQL、MSSQL、PG、Oracle 上各有一份。实库验证放到 M6。
- [x] 2.9 分析器首版（`src/EasyQuerySharp.Generator`，测试在 `tests/EasyQuerySharp.Tests.Generator`，说明见 [analyzers.md](docs/design/analyzers.md)；打进核心包随 M10 做）：
  - EQ0001：不支持翻译的方法；
  - EQ0002：表达式树里不能写的语法，给出改写建议。

## M3 多表与组合查询

覆盖能力：`query.join` 88、`query.group` 78、`query.union` 18、`query.cte` 11、`query.map` 11、`query.stream` 9、`query.reuse` 2，加上 fork 的 rollup / cube / grouping。

- [x] 3.1 显式 join：`IQuery<T1..T8>` 由仓库内的生成器产出；right join 之后，前面表的列按可空处理。
- [~] 3.2 `GroupBy` / `Having` / 投影里的 `g.Key`，以及 `Rollup` / `Cube` / `Grouping`。
- [x] 3.3 子查询：`In(subQuery)`、`Exists`、派生表 `AsSubQuery()`。
- [~] 3.4 `Union` / `UnionAll`（已完成），CTE：`AsCte(name)`（已完成），递归 CTE 随 4.10 的树一起做。
- [x] 3.5 流式读取 `ToAsyncEnumerable()`，无实体查询 `db.SqlQueryAsync<Dictionary<..>>`、`QuerySql<Dictionary<..>>(sql, params)` 与 `ToDictionariesAsync`。
- [ ] 3.6 LINQ 查询语法（`from … join … group … select`）的支持测试。

## M4 导航与对象树（[0002](docs/decisions/0002-object-tree-and-dto.md)）

覆盖能力：`nav.subquery` 142、`nav.groupJoin` 67、`nav.flat` 56、`nav.include` 37、`dto.autoInclude` 35、`tree` 25、`fill` 4。

- [x] 4.1 关系元数据：
  - `[Navigate]` 支持一对一、多对一、一对多、多对多（中间表）、直达路径、`ExtraFilter`；
  - 可空引用类型决定 JOIN 方式。
- [x] 4.2 隐式 join：条件、排序、投影里出现多对一导航时自动 JOIN。
- [~] 4.3 隐式子查询：`Any` / `!Any` / `Count` / `Sum` / `Max` / `Min` / `Average` / `Where(..).聚合`，以及 `GroupJoin()` 合并（一对多的分组合并已完成；多对多待做）。
- [~] 4.4 归一管线：内联器、规划器、按叶子往根构造的物化器。对多集合每层一次批量 IN 查询；IN 列表按方言上限分批，PG 用 `ANY`。
- [~] 4.5 投影里的嵌套集合：`Where` / `OrderBy` / `Take`（ROW_NUMBER）、`First` / `ElementAt`；`oneOfMany`（导航上的 `First` / `ElementAt` 已完成）。
- [ ] 4.6 可复用投影：`Project(map)`，以及 `[Projection]` 方法由生成器产出表达式。
- [ ] 4.7 `[MapFrom<T>]` 自动映射：平铺约定、`[MapPath]`、partial `Configure`；分析器 EQ0003 和 EQ0005。
- [~] 4.8 `Include(nav, q => …)` 可以嵌套；ExtraFilter 的分析器 EQ0004（Include、嵌套、多对多经中间表、ExtraFilter / ExtraMappingFilter 已完成；分析器待做）。
- [~] 4.9 填充：投影里写非导航的关联查询，规划器按键批量执行（`FillMany` / `FillOne` 已完成，带 Take 时按键 UNION ALL）。
- [ ] 4.10 树：`ToTreeAsync`、`DescendantsOf` / `AncestorsOf`（递归 CTE）；递归 DTO 由生成器诊断。
- [ ] 4.11 `ToAsyncEnumerable(batchSize)` 按批流式读取对象树；`.AsSnapshot()` 保证多次查询的一致性。

## M5 写入进阶与列规则

覆盖能力：`write.upsert` 13、`write.strategy` 19、`write.join` 10、`tracking` 62、`save.aggregate` 39、`interceptor` 3、`logicDelete` 10、`version` 13、`column.encryption` 8、`column.converter`，加上 fork 的 insert-select 和 JSON 列。

- [x] 5.1 拦截器：`QueryFilter<T>`、`EntityInterceptor<T>`（含按条件更新追加 SET）；默认关闭、按名启用 / 关闭。内置多租户、自动填充不做成库功能，用这两个基类几行即可写出（见 InterceptorTest）。
- [x] 5.2 逻辑删除：布尔、删除时间与自定义策略（`ILogicDeleteStrategy`）；可以按导航单独关闭（`u.Posts.DisableLogicDelete().Any(..)`）；`.Hard()`。
- [x] 5.3 乐观锁 `[Version]`：`WithVersion` / `IgnoreVersion`，按实体写入成功后写回新版本；冲突检测用 `ExecuteAsync(expectedRows)` 抛 `ConcurrencyException`。
- [~] 5.4 值转换器、枚举存储方式、值对象 `[ValueObject]`、JSON 列（`JsonValueConverter<T>` 已完成）。
- [~] 5.5 列加密：与 Java 的 AES / SM4 密文兼容，支持分段 LIKE（`[Encryption]` 与 AES 策略已完成；SM4 待做）。
- [~] 5.6 upsert：`OnConflictIgnore()` / `OnConflictUpdate()`（MySQL、PG、SQLite 已完成；SQL Server / Oracle 的 MERGE 待做）。
- [ ] 5.7 写入策略：按列策略只写非空列；批量写入用 `DbBatch`。
- [x] 5.8 UPDATE / DELETE 里用导航：集合导航翻译成关联子查询；对一导航生成 JOIN，MySQL 用多表语法，PostgreSQL 用 FROM / USING。SQL Server、Oracle、SQLite 的写法随 M6 实库验证再加（现在抛 NotSupportedException）。
- [ ] 5.9 INSERT … SELECT。
- [~] 5.10 追踪：`BeginTracking()` / `AsTracking()` 加差异更新、`WhereColumns` 用原始值（已完成）；不追踪时用 `Changes(before, after)`。
- [ ] 5.11 聚合根保存：显式声明级联路径、按差异增删改、`OrphanPolicy`、生成键回填、必须在事务里。

## M6 方言铺开

覆盖能力：`dialect.mssql` 38、`dialect.mssql2008` 13、`dialect.dameng` 51、`dialect.duckdb` 16、`dialect.clickhouse` 4、`dialect.kingbase` 2、`dialect.h2` 74（另行处理），加上 fork 的方言专属函数和 RETURNING。

每个方言是一个包，交付四样东西：方言类、驱动适配、在 `TestDatabases` 里登记、CI 任务（HANA 除外）。

- [ ] 6.1 SQL Server，包括 2008 的 ROW_NUMBER 分页。
- [ ] 6.2 Oracle。
- [ ] 6.3 达梦：方言继承 Oracle，覆盖差异部分。
- [ ] 6.4 MariaDB：MySQL 方言的变体。
- [ ] 6.5 openGauss 和人大金仓 PG 模式：与 PG 共用方言。
- [ ] 6.6 DuckDB（进程内，本地也可以跑）、ClickHouse、Db2、TDengine。
- [ ] 6.7 SAP HANA：只在本地验证。
- [ ] 6.8 方言静态入口：
  - `MySql.Json*` / `MySql.Match`；
  - `Pg.Jsonb*` / `Pg.Array` / `Pg.FullText`；
  - `Hana.Text`；
  - 用错方言时在渲染期报错。
- [ ] 6.9 `Returning()`，用于 PG 的 UPDATE / DELETE。
- [ ] 6.10 H2 的 74 个用例：和方言无关的改在 SQLite 上移植，标为偏离；只测 H2 方言本身的标为不适用。
- [ ] 6.11 L3 共用套件在每个方言上全部通过；`LiveSuiteOptions` 按方言裁剪能力。

## M7 平台能力

覆盖能力：`codeFirst` 17、`table.rename` 45、`table.schema` 19、`table.link` 13、`sharding` 48、`dto.object` 101、`search` 45。

- [~] 7.1 code-first：建表（注释、索引、外键）、只增不删的同步、`RenamedFrom`、`ToSql()`、读库结构。
- [ ] 7.2 查询级表名映射 `MapTable`：导航、隐式 join、include 都跟着变；`InSchema`、`WithTableLink`。
- [ ] 7.3 分表：按取模、按时间路由；按条件裁剪；库内 UNION ALL；写入路由。
- [ ] 7.4 分库：路由；在内存里归并排序和分页，`count` / `sum` / `min` / `max` 跨库合并。
- [~] 7.5 `FilterBy` / `SortBy`（已完成：条件类型、多属性 OR、区间、导航路径、集合 EXISTS、允许排序的属性）/ `Table(request)`，对应 Java 的 `whereObject` / `orderByObject`。
- [ ] 7.6 `EasyQuerySharp.Search`：Java search 扩展的参数约定。
- [ ] 7.7 二级缓存接口，提供本机和分布式两级。
- [ ] 7.8 `EasyQuerySharp.DependencyInjection`：`AddEasyQuery`，读写分离（replica）。

## M8 文档站

- [ ] 8.1 站点 `apps/docs`：读取 `examples.json`，提供方言切换、SQL 格式化、结果表格，可以复用 TS 文档站的组件。
- [ ] 8.2 页面结构对齐 TS：模型、查询、函数、导航、写入、列规则、运维；每页写清方言支持矩阵和对应的 Java 方法名。
- [ ] 8.3 卡片规模对齐 TS（约 500 张），每个公开 API 至少出现在一张卡片里。
- [ ] 8.4 CI 加一个文档任务：`DocsExamples --check`（快照没过期、卡片都能跑）和站点构建。

## M9 TS 版独有的能力（按需）

- [ ] 9.1 多态（判别列加子类）。
- [ ] 9.2 计算属性 `[Computed]`。
- [ ] 9.3 只读库 `AsReadOnly()`、开发期 N+1 检测。
- [ ] 9.4 用 interceptors 做编译期预翻译。等官方文档不再标为实验性再做，在此之前先依赖运行时缓存。
- [ ] 9.5 `dotnet eqs pull`：从库反向生成实体；多个模型程序集注册到同一个库。

## M10 发布

- [ ] 10.1 NuGet 打包：Source Link、确定性构建、包说明；生成器和核心包放在一起分发。
- [ ] 10.2 BenchmarkDotNet：和 EF Core、FreeSql、SqlSugar、Dapper 对比查询翻译、物化、对象树。
- [ ] 10.3 Native AOT 和裁剪验证：用一个示例应用发布 AOT。
- [ ] 10.4 README、入门教程、从 Java / TS 迁移的指南。
