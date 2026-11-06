# ROADMAP

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。每项一个分支一个 PR，规则见 [CLAUDE.md](CLAUDE.md)。

本文只列还没做完的事。已完成能力的写法与限制见 [能力对照总表](docs/parity.md) 和各能力文档；2026-11-06 重新规划之前的版本见 git `0f0b76f:ROADMAP.md`。

## 目标

**1.0：成熟可用。**

- **对标**：Java 的 2404 个用例（上游 `sql-test` 2315 个、fork `sql-fork-test` 89 个），每个都有对应的 C# 测试，或者标为不适用并写明理由。基准冻结在 `tools/ParityTests/java-tests.json`。
- **按可发布的标准做**：公共 API 冻结、文档齐全、有基准对比、能打包。是否发布到 nuget.org 到阶段 5 再定。

设计依据：[0001 查询 API 路线](docs/decisions/0001-query-api-route.md)、[0002 对象树与 DTO](docs/decisions/0002-object-tree-and-dto.md)、[0003 测试对标](docs/decisions/0003-testing-and-parity.md)、[0004 聚合根保存](docs/decisions/0004-aggregate-save.md)、[架构](docs/design/architecture.md)。

## 现状（2026-11-06）

- 对标：已移植 1726、偏离 224、不适用 78、骨架 6、待做 370。
- 待做的分布：方言 222，平台能力 128（分库分表 48、search 45、code-first 15、表名 / schema / link 14、按对象筛选 6），其余零散 20。
- 实库：本地和 CI 跑 SQLite、MySQL 8.4、PG 17；CI 另有 MySQL 8.0.18、MariaDB、SQL Server、Oracle 任务。master 的 CI 全绿。

## 已定的取舍

**范围**

- 不做，对应的用例标为不适用：
  - search 扩展（`sql-search`，45 个）：这是第三方贡献的 URL 参数约定，由前端选运算符；同类需求用 `FilterBy` / `SortBy`。
  - SQL Server 2008 的 ROW_NUMBER 分页（13 个）：SQL Server 2008 已于 2019 年停止支持。
- 要做：分库分表、二级缓存、ClickHouse / TDengine / Db2、关联键匹配策略（Java 的 `relationPropertyStrategy`）。
- 暂不做，记进 [BACKLOG](docs/BACKLOG.md)：文档站、AOT 与元数据 / 物化器的 Source Generator、TS 版独有的能力（原 M9）。

**架构**

- **方言留在核心包里**，只负责渲染 SQL。驱动由用户通过 `DbDataSource` 传入，核心不引用任何驱动。只有用到驱动专属 API（批量复制、PG 数组类型等）时，才单独出包。
- **方言分两档**：
  - 第一档，CI 跑全部实库用例：SQLite、MySQL 8.4 / 8.0.18（PolarDB 基线）/ MariaDB、PG 17、SQL Server、Oracle。
  - 第二档，比对 SQL 文本，再加实库冒烟：达梦、HANA、DuckDB、Kingbase / openGauss、ClickHouse、TDengine、Db2。容器起得来的进 CI，起不来的在本地验证。
- **生成器只保留 `[Projection]` 和分析器**。`[MapFrom<T>]` 的映射在运行时构造，编译期校验交给分析器。核心如实标注 `RequiresDynamicCode` / `RequiresUnreferencedCode`。
- **嵌套事务对齐 Java**：已经在事务里时再开事务，直接抛异常。
- **规划外的新公共概念先写决策文档**：关联键匹配策略、分库分表、二级缓存。
- **新依赖只进各自的包**：生成器引用 Microsoft.CodeAnalysis，依赖注入和缓存引用 Microsoft.Extensions 的抽象包，核心仍然零依赖。

**对标基准**

- 冻结在现有快照，不随上游增长。上游 main（3.3.1）的 `sql-test` 已经有 2410 个用例，跟进上游另起工作项。
- fork 仓库不在本机，fork 的 89 个用例只存在于快照里。

## 每个阶段的完成标准

1. **用例**：归在本阶段的待做为 0；偏离和不适用每一条都写了理由。
2. **实库**：对应的用例组在相关数据库上通过；第一档方言要在 CI 里通过。
3. **文档**：每个能力都有示例卡片，各方言的 SQL 快照已更新并提交。
4. **CI**：全部任务通过。

## 执行顺序

R → 1 → 2 → 3 → 4 → 5。

- 某一项进入待决状态时，跳到顺序里下一个不依赖它的项。
- 每完成一块功能顺手整理（拆文件、合并重复、删死代码），整理单独提交。
- 示例卡片和 SQL 快照随功能一起补。

## R 整理（不加新功能）

- [x] R.1 冻结对标基准：`tools/ParityTests` 的对照表和 `--check` 只读快照，快照记下来源（现有基准取自 fork，commit 未记录）；`--scaffold` / `--show` 从本机 Java 仓库取方法体，缺 fork 部分也能用；`--rescan` 才改写快照。
- [x] R.2 正确性问题：环境事务改成每个库一个可变 holder，结束时清空（原来在 async 方法里赋值不回流，已结束的事务一直挂在调用方流程上）；已在事务里再开事务抛异常（对齐 Java）；追踪作用域的共享状态加锁，可以并行查询。
- [ ] R.3 修正 API（1.0 之前一次改完）：
  - `ToAsyncEnumerable()` 改名 `AsAsyncEnumerable()`；
  - `db.QuerySql<T>(..)`（返回可组合的查询）改名 `db.FromSql<T>(..)`；
  - `Select<TDto>()` 和 `SelectAutoInclude<TDto>()` 合并成 `Select<TDto>()`：DTO 里与导航同名的成员自动带出，`[MapFrom<T>]` 只用作编译期校验；
  - `ToDictionariesAsync()` / `FirstDictionaryOrDefaultAsync()` 改名 `ToRowsAsync()` / `FirstRowOrDefaultAsync()`；
  - 删掉 `Limit(offset, rows)`；`LimitSelect` 能用 `Skip` / `Take` 加 `AsSubQuery()` 表达的话也删掉；
  - 补上 `UseSqlServer` / `UseOracle`，和 `UseMySql` 等入口一致；
  - 构造 `EasyDb` 时冻结 `EasyDbOptions`，之后再修改就抛异常；
  - 新增分析器 EQ0007：只能在查询里用的方法（`Sql` 上的扩展方法等）在表达式树之外调用时，编译报错；
  - 引入 `Microsoft.CodeAnalysis.PublicApiAnalyzers`（只在开发期用），建立公共 API 基线。
- [x] R.4 落地删减：search 的 45 个、SQL Server 2008 的 13 个用例在 `tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs` 标为不适用；文档站、AOT、原 M9 已记进 BACKLOG。
- [x] R.5 文档对齐现状：`architecture.md` 按实际的模块、管线、抽象、分析器重写；0001–0003 按实现修订并改为已定（0002 的级联保存改指 0004）；`parity.md` 的 C# 写法按实际 API 刷新，状态列改成对应的阶段；旧 ROADMAP 里标 `[~]` 的项逐个核对，剩余内容已在阶段 1–4 或 BACKLOG 里。
- [x] R.6 复核偏离：224 条里 80 条只是 C# 写法不同、SQL 与 Java 逐字一致（flatElement 写成 Any、include2 写成嵌套 Include、字符串比较写成 `string.Compare`、Map 投影写成带 `[Column]` 的 DTO 等），改为已移植，理由留作注释；剩下 144 条是参数类型、别名编号、异常文本、C# 语义或 Java 自身的 bug 带来的真实差异。藏着功能缺口的几条记在 1.5、1.7、1.8。
- [ ] R.7 代码走查：找出重复代码、死代码和过长的文件（`QueryBase`、`SqlDialect`、`SqlRenderer`、`SavePlan`），整理单独提交。

完成标准：文档与代码一致；对标表里的待做只剩各阶段计划内的项；CI 全绿。

## 阶段 1 核心收尾

- [ ] 1.1 关联键匹配策略（Java 的 `relationPropertyStrategy`）：
  - 先写决策文档 0005；
  - 在导航上声明匹配策略，内置"逗号分隔 id"的实现，并按方言渲染（MySQL 用 `FIND_IN_SET`，PG 用 `= ANY(string_to_array(..))`，SQLite 拼接逗号后用 LIKE）；
  - 隐式子查询、分组合并、Include、DTO 自动带出都能用；聚合保存不支持；
  - 用例：M8Test1 testRelationColumns / testRelationColumns1。
- [ ] 1.2 沿导航拉平取列表：用 LINQ 的 `SelectMany`（`q.SelectMany(a => a.Bs).SelectMany(b => b.Cs).Select(c => c.Name)`）。用例是 QueryTest25 testToListFlat1 / test11a 和 RelationTest testOneToOne；做完后复核 nav.flat 的偏离能不能改回已移植。
- [ ] 1.3 值对象 `[ValueObject]`。
- [ ] 1.4 不追踪时的差异更新 `db.Update<T>().Changes(before, after)`。
- [ ] 1.5 批量写入走 `DbBatch`。做完后复核按"每个实体一条语句"记的偏离：DeleteTest testBatchDelete、InsertTest testIncrement、UpdateTest updateTestBatch / updateTestBatch1 / updateTestBatch2。
- [ ] 1.6 `Returning()`：PG 的 UPDATE / DELETE … RETURNING，其余方言在渲染期报错（fork ReturningSqlTest、ReturningLiveTest）。
- [ ] 1.7 窗口取值函数省略排序时，沿用查询的 ORDER BY；做完后 MySQL8Test3 nextTest3 的偏离改回已移植。
- [ ] 1.8 零散用例：
  - QueryTest25 test11a1 / topicIntegerTest / xxx、QueryTest26 testAAA；
  - QueryTestRelationTest testRelation（Include 的配置并入平铺带出、分页后再按段取）；
  - fork ProxySmokeTest；
  - 偏离里的功能缺口：M8BankTest whereSubQuery3（与集合导航同表的显式 EXISTS 也并进分组合并）、pgsql QueryTest19 tree7_2（树查询的层级列投影进 DTO 的同名属性）；
  - 骨架：GeneralFunctionsTest testDoubleConcat / MySQLInsertIgnore2UpdateKey / MySQLInsertIgnore2UpdateKey2，NumberFunctionsTest subQueryMySQL。

## 阶段 2 第一档方言

- [ ] 2.1 SQL Server：
  - Upstream 夹具改用 UpperCamelCase 命名；
  - 移植 MsSQLQueryTest、MsSQLUpdateJoinTest、MSSQLTest1、MSSQLMathQueryTest 共 38 个用例；
  - CI 的 mssql 任务加跑 Upstream 的 Mssql 命名空间。
- [ ] 2.2 Oracle：实库验证；补骨架 NumberFunctionsTest subQueryORACLE。
- [ ] 2.3 UPDATE / DELETE 里用对一导航（`write.join`），在 SQL Server、Oracle、SQLite 上补齐（现在抛 NotSupportedException）；`DateTrunc` 也在这三个方言上补齐。
- [ ] 2.4 L3 `LiveSuite` 从现在的 4 组扩到 TS 的 23 组，`LiveSuiteOptions` 按方言裁剪能力；在第一档的每个库上全部通过（MySQL 8.0.18、MariaDB 各算一个库）。

## 阶段 3 第二档方言

每个方言交付三样东西：方言类、在 `TestDatabases` 里登记、实库冒烟。容器起得来的再加一个 CI 任务。

- [ ] 3.1 达梦：方言继承 Oracle，覆盖差异部分。用例有 DamengQueryTest、DamengForUpdateTest、UpdateJoinTest、MathQueryTest、MigrationDamengTest。
- [ ] 3.2 SAP HANA：
  - `Sql.Hana.Text`；
  - fork 的 HanaSqlTest、HanaTextSqlTest、三个 live 用例，以及 InsertSelect / JsonValueConverter 的 HANA 用例；
  - HANA Express 压低内存后进 CI，跑不起来就退回本地验证。
- [ ] 3.3 DuckDB（进程内运行，CI 直接跑）：DuckDBQueryTest、AllTypeTest、DuckDBMathQueryTest，以及 duckdb 版的 M8Save4Test。
- [ ] 3.4 PG 家族：Kingbase（MyKingbaseESTest，骨架 testKingbaseCte）、openGauss。
- [ ] 3.5 ClickHouse（ClickHouseTest）、TDengine、Db2：Java 用例很少或没有，主要靠裁剪后的 L3 套件验证。
- [ ] 3.6 H2 的 74 个用例：与方言无关的改到 SQLite 上移植，标为偏离；只测 H2 方言本身的标为不适用。
- [ ] 3.7 跨方言的零散用例：ForUpdateDialectToSQLSmokeTest 剩下的方言、NullSafeSqlTest nullSafeEqualsPerDialect。

## 阶段 4 平台能力

- [ ] 4.1 code-first：建表（注释、索引、外键）、只增不删的同步、`RenamedFrom`、`ToSql()`、读取库结构（MigrationTest 等 15 个）。
- [ ] 4.2 表名映射：查询级 `MapTable`（导航、隐式 join、include 跟着一起变；fork TableNameMapping 的 7 个用例）、`AsTable` 剩余的用例、`InSchema`、`WithTableLink`。
- [ ] 4.3 `FilterBy` / `SortBy` 剩余的 6 个用例，以及 `Table(request)`。
- [ ] 4.4 分库分表（ShardingTest 48 个），先写决策文档 0006：
  - 分表：按取模或按时间路由、按条件裁剪、库内 UNION ALL、写入路由；
  - 分库：路由，在内存里做归并排序和分页，跨库合并 `count` / `sum` / `min` / `max`。
- [ ] 4.5 二级缓存 `EasyQuerySharp.Caching`，先写决策文档 0007：
  - 参照 Java 的 `sql-extension/sql-cache`：单实体、多实体、键值三种缓存查询，写入后让缓存失效；
  - 对接 `Microsoft.Extensions.Caching` 的抽象，支持本机和分布式两级。
- [ ] 4.6 依赖注入 `EasyQuerySharp.DependencyInjection`：`AddEasyQuery`，多个库按名字注入。

完成标准：2404 个用例的待做为 0。

## 阶段 5 发布准备

- [ ] 5.1 冻结公共 API：PublicApiAnalyzers 的基线转为已发布（Shipped），公开成员都有 XML 文档。
- [ ] 5.2 基准对比：用 BenchmarkDotNet 和 EF Core、Dapper、FreeSql、SqlSugar 比较查询翻译、物化、对象树三项；根据结果决定要不要缓存翻译结果。
- [ ] 5.3 文档：在 `docs/guide/` 下按能力写 Markdown 指南（用法、方言支持矩阵、限制、对应的 Java 方法名），代码片段和 SQL 由 DocsExamples 生成；另写 README、入门教程，以及从 Java / TS 迁移的指南。
- [ ] 5.4 打包：NuGet 元数据、Source Link、确定性构建，生成器和分析器随核心包一起分发。是否发布到 nuget.org 在这一步决定。
