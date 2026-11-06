# ROADMAP

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。每项直接在 master 上做，做完提交推送，规则见 [CLAUDE.md](CLAUDE.md)。

本文只列还没做完的事。已完成能力的写法与限制见 [能力对照总表](docs/parity.md) 和各能力文档；2026-11-06 重新规划之前的版本见 git `0f0b76f:ROADMAP.md`。

## 目标

**1.0：成熟可用。**

- **对标**：Java 的 2404 个用例（上游 `sql-test` 2315 个、fork `sql-fork-test` 89 个），每个都有对应的 C# 测试，或者标为不适用并写明理由。基准冻结在 `tools/ParityTests/java-tests.json`。
- **按可发布的标准做**：公共 API 冻结、文档齐全、有基准对比、能打包。是否发布到 nuget.org 到阶段 5 再定。

设计依据：[0001 查询 API 路线](docs/decisions/0001-query-api-route.md)、[0002 对象树与 DTO](docs/decisions/0002-object-tree-and-dto.md)、[0003 测试对标](docs/decisions/0003-testing-and-parity.md)、[0004 聚合根保存](docs/decisions/0004-aggregate-save.md)、[架构](docs/design/architecture.md)。

## 现状（2026-11-06）

- 对标：已移植 1859、偏离 143、不适用 136、骨架 2、待做 264。
- 待做的分布：方言 168，平台能力 82（分库分表 48、code-first 14、表名 / schema / link 14、按对象筛选 6），其余零散 14。
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
- [x] R.3 修正 API：`AsAsyncEnumerable`、`FromSql`、`ToRowsAsync` / `FirstRowOrDefaultAsync` 改名；删掉 `Limit` 与 `LimitSelect`；`SelectAutoInclude` 并入 `Select<TDto>()`；补 `UseSqlServer` / `UseOracle`；`EasyDbOptions` 交给 `EasyDb` 后冻结；分析器 EQ0007；公共 API 基线（`src/EasyQuerySharp/PublicAPI.*.txt`）。
- [x] R.4 落地删减：search 的 45 个、SQL Server 2008 的 13 个用例在 `tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs` 标为不适用；文档站、AOT、原 M9 已记进 BACKLOG。
- [x] R.5 文档对齐现状：`architecture.md` 按实际的模块、管线、抽象、分析器重写；0001–0003 按实现修订并改为已定（0002 的级联保存改指 0004）；`parity.md` 的 C# 写法按实际 API 刷新，状态列改成对应的阶段；旧 ROADMAP 里标 `[~]` 的项逐个核对，剩余内容已在阶段 1–4 或 BACKLOG 里。
- [x] R.6 复核偏离：224 条里 80 条只是 C# 写法不同、SQL 与 Java 逐字一致（flatElement 写成 Any、include2 写成嵌套 Include、字符串比较写成 `string.Compare`、Map 投影写成带 `[Column]` 的 DTO 等），改为已移植，理由留作注释；剩下 144 条是参数类型、别名编号、异常文本、C# 语义或 Java 自身的 bug 带来的真实差异。藏着功能缺口的几条记在 1.5、1.7、1.8。
- [x] R.7 代码走查：`QueryBase` 拆出终结方法（`QueryBase.Execution.cs`）与 `Page<T>`，`SqlDialect` 拆成通用渲染、upsert、函数表、DDL 四个分部文件；IN 翻译复用 `MetadataRegistry.ElementType`；`EasyDb` 构造函数标注 `RequiresUnreferencedCode` / `RequiresDynamicCode`；没有被调用的公开方法（`DecrementIf`、`UpdateColumnsBuilder.IsIncluded`）补上用例。编译器的未使用成员与多余 using 检查没有报出问题。

完成标准：文档与代码一致；对标表里的待做只剩各阶段计划内的项；CI 全绿。

## 阶段 1 核心收尾

- [x] 1.1 关联键匹配策略（Java 的 `relationPropertyStrategy`，决策 [0005](docs/decisions/0005-relation-key-match.md)）：`[Navigate(.., KeyMatch = KeyMatch.SelfCsv)]` 表示本表一列存逗号分隔的目标键，`Sql.InCsv` 按方言渲染（MySQL `FIND_IN_SET`、PG `= ANY(string_to_array(..))`、SQLite / Oracle `instr`、SQL Server `CHARINDEX`）；条件 / 隐式子查询、Include、DTO 自动带出、SelectMany 都能用，分组合并回退成子查询，取元素、按父对象取段报不支持。M8Test1 testRelationColumns / testRelationColumns1 移植。
- [x] 1.2 沿集合导航展开 `SelectMany`：按 LINQ 语义写成 INNER JOIN（多对多经中间表，导航的固定条件与逻辑删除写进 ON，保留重复），连着写的 SelectMany 和之后的 Where / OrderBy / Select 直接作用在最后连接的表上；父查询分页、去重、分组、投影过时先包成派生表；支持查询语法 `from a in q from b in a.Bs`。Java 的 testToListFlat1 记为偏离（Java 逐层带出再去重）。
- [x] 1.3 值对象 `[ValueObject]`（对应 TS 的 `embed()`；Java 的 `@ValueObject` 已弃用，没有用例）：几列合成实体上的嵌套对象，可嵌套、可换前缀；条件、隐式子查询、整体投影、按结构映射到 DTO、插入、`Set` 整个或部分写入、建表都支持。
- [x] 1.4 不追踪时的差异更新 `db.Update(after).Changes(before)`：只写两份之间不同的列，没有不同就不发语句；`WhereColumns` 的定位值取修改前的。
- [x] 1.5 批量写入 `.Batch()`（实体与字典的插入、更新、删除）：按 SQL 分组，组按首次出现的顺序、每组最多 1000 条（与 Java 一致）；驱动支持 `DbBatch` 时交给它（Npgsql 一组一个往返；MySqlConnector 对 MySQL 不省往返），否则同连接逐条执行（SQLite）；监听器每组一次，`SqlExecutionEvent.Statements` 是整组。不在事务里时每组在自己的事务里执行，整组成败一致，MySQL 也免了逐条提交的刷盘（1.8 万行回填自增键从 2 分 40 秒降到 5 秒）。回填自增键：语句自带 RETURNING / OUTPUT 的直接读，MySQL 在每条插入后跟一条 `LAST_INSERT_ID()`。5 条批量偏离改回已移植，InsertTest1 testInsert 移植；insertBatch 仍是偏离（Java 返回 JDBC 的 -3）。
- [x] 1.6 `Returning()`：按条件的更新 / 删除加 `.Returning()`（整行，`RETURNING *`）或 `.Returning(选择器)`（只取选择器用到的列，读回后在内存里投影），得到 `ReturningCommand`（`ToSql` / `ExecuteAsync`）；逻辑删除返回被标记的行。PostgreSQL、SQLite 支持，其余方言、走导航连接、按实体写入都拒绝。fork ReturningSqlTest ×2、ReturningLiveTest 移植。
- [x] 1.7 窗口取值函数（Lag / Lead / FirstValue / LastValue / NthValue）不带窗口或窗口没写排序时，沿用所在查询的 ORDER BY（翻译时的快照，与 Java 的 `offset()` 一致），查询没有排序时报错；这类查询深分页时不反转排序。nextTest3 改回已移植，nextTest2 由不适用改为偏离（只差异常文本）。
- [x] 1.8 零散用例：fork ProxySmokeTest（补 ForkCompany 与 ForkUser.Company）；whereSubQuery3、pgsql tree7_2 原本记的"功能缺口"复核后与 Java 一致，改回已移植；骨架 MySQLInsertIgnore2UpdateKey / 2（`new MySqlDialect { IgnoreConflictAsKeyUpdate = true }`，对应 Java 的 mysqlInsertIgnoreToUpdateKey）、NumberFunctionsTest subQueryMySQL；testDoubleConcat 用的是 Kingbase 方言，挪到 3.4。顺手修了 `Select<TDto>()` 遇到直达导航时拿本表键查目标表的错误（静默取错数据）。testOneToOne、testRelation 篇幅远超预计，拆到 1.9。
- [x] 1.9 带出与 DTO 自动带出补齐（RelationTest testOneToOne：一个方法约 80 段；QueryTestRelationTest testRelation：24 段）。按段移植（`QueryTestRelationTest.Relation.cs`），全部段移植后再标 Upstream；不能逐字对齐的段写偏离。
  - [x] G1 `Include(导航, 配置)` 之后再投影（`Select<TDto>()` 或手写投影）：配置并入投影里根实体同名导航的带出；
  - [x] G2 先分页再投影 `ToPageAsync(page, size, q => q.Select<TDto>())`（Java 的 `toPageSelectResult`）：这一页包成派生表，Include 留在外层；
  - [x] G4 DTO 属性上的 `[Navigate(OrderBy / Limit / Offset)]` 覆盖实体导航的排序与条数，`NavigateAttribute` 补 `Offset`（实体导航上同样生效）；
  - [x] G5 `IncludeLimitMode.UnionAll` 与 Java 逐字一致（每段取整个实体、单键写 `= ?`、投影写在外层不写 AS），根夹具按 Java 的 BaseTest 改用 UnionAll；
  - [x] QueryTestRelationTest testRelation：24 段全部移植（`QueryTestRelationTest.Relation.cs`），记为偏离（第 9 段 Java 多写 AS 与重复列；第 18、19 段在 MySQL 8.4 严格模式下只核对 SQL）。Java 的 `selectAutoInclude(DTO, extra)` 用 DTO 上的 `[MapExpression]` 表达；`ignoreNavigateConfigure` 对应新增的 `Query.IgnoreNavigationDefaults()`，DTO 上 `[Navigate]` 的排序与条数是默认值，带出配置自己写了就以配置为准；
  - [x] RelationTest testOneToOne：约 75 段写成 56 个测试（`RelationTest.OneToOne.cs`），记为偏离（include2 写成嵌套 Include、flatElement 链写成 SelectMany、平铺元素是实体时 Java 连它的导航一起带出）。补齐：同一条集合路径上的几个 `[MapPath]` 合并成一组带出查询（只取标量时只查这些列、单表不写别名，有实体时取实体）；对一路径放进列表属性；`Include` 之后投影回同一实体时照样带出（loadInclude 格式）；UnionAll 模式只有一个父键时直接 `IN (?) LIMIT n`。4 段 Skip（Java 的三项结构校验、联表后 `Select<TDto>()`）记进 BACKLOG。
## 阶段 2 第一档方言

- [x] 2.1 SQL Server：Upstream 加 Mssql 夹具（库 `mssql_eq`，UpperCamelCase 命名，银行种子抽成 `BankSeed` 与 Mysql8 夹具共用），MsSQLQueryTest、MsSQLUpdateJoinTest、MSSQLTest1、MSSQLMathQueryTest 共 38 个用例全部在实库上跑通（37 个已移植；testPropertyMath 记偏离：参数是 Double、Java 把 floor 写成截断用的 CASE）。补齐的功能：
  - 拼接式 LIKE：`Sql.LikeContains` 等写成 `LIKE ('%'+CAST(v AS NVARCHAR(MAX))+'%')`；
  - `DateFormat` 按 Java 写成 CONVERT / SUBSTRING 段（整串格式对应一个 CONVERT 样式，其余切成格式符与字面量参数），不再用 `FORMAT()`；
  - `Math.Round(x)` 写 `ROUND(x,0)`、`Atan2` 用 ATAN 加象限 CASE、不带位数的 `Truncate` 写 FLOOR / CEILING 的 CASE；
  - `AsTableSegment` 联表后可用（`Query<T1, T2..>`），导航上用 `Sql.AsTableSegment(x.User, ..)` / `x.Cards.AsTableSegment(..)`（`WITH(NOLOCK)` 一类表提示）；
  - `Sql.MaxColumns` / `Sql.MinColumns`：MySQL、PG 写 GREATEST / LEAST，SQLite 写多参数 MAX / MIN（Java 在 SQLite 上写 GREATEST，SQLite 没有），SQL Server 写 VALUES 派生表，Oracle 写 UNION ALL … FROM DUAL（达梦沿用）；
  - CI 的 mssql 任务拆成 live / upstream 两片，后者跑 Upstream 的 Mssql 命名空间。
- [ ] 2.2 Oracle：实库验证。已做：条件当值用（投影、比较的一侧、分组合并的 any / none 列）在 SQL Server、Oracle 上写成 `(CASE WHEN 条件 THEN ? ELSE ? END)`（Java 的 booleanSQLFunction），NumberFunctionsTest subQueryORACLE 移植。
- [x] 2.3 UPDATE / DELETE 里用对一导航（`write.join`）：SQL Server 原本已有；SQLite 写成 `UPDATE … AS t … FROM` / `DELETE … AS t WHERE EXISTS(…)`，Oracle 写成按 ROWID 对上的 `MERGE` / `DELETE … WHERE ROWID IN (…)`；`DateTrunc` 补上 SQLite（strftime）、SQL Server（DATETRUNC）、Oracle（TRUNC）。LiveSuite 加了两组用例，各库按同样的结果校验（SQL Server、Oracle 在 CI 上跑）。
- [ ] 2.4 L3 `LiveSuite` 从现在的 4 组扩到 TS 的 23 组，`LiveSuiteOptions` 按方言裁剪能力；在第一档的每个库上全部通过（MySQL 8.0.18、MariaDB 各算一个库）。

## 阶段 3 第二档方言

每个方言交付三样东西：方言类、在 `TestDatabases` 里登记、实库冒烟。容器起得来的再加一个 CI 任务。

- [ ] 3.1 达梦：方言继承 Oracle，覆盖差异部分。用例有 DamengQueryTest、DamengForUpdateTest、UpdateJoinTest、MathQueryTest、MigrationDamengTest，以及 QueryTest26 testAAA（达梦上用对一导航条件删除）。
- [ ] 3.2 SAP HANA：
  - `Sql.Hana.Text`；
  - fork 的 HanaSqlTest、HanaTextSqlTest、三个 live 用例，以及 InsertSelect / JsonValueConverter 的 HANA 用例；
  - HANA Express 压低内存后进 CI，跑不起来就退回本地验证。
- [ ] 3.3 DuckDB（进程内运行，CI 直接跑）：DuckDBQueryTest、AllTypeTest、DuckDBMathQueryTest，以及 duckdb 版的 M8Save4Test。
- [~] 3.4 PG 家族：Kingbase（MyKingbaseESTest，骨架 testKingbaseCte、GeneralFunctionsTest testDoubleConcat）、openGauss。
  - 已做：`KingbaseDialect` / `GaussDbDialect`（对应 Java 的 sql-kingbase-es、sql-gauss-db）继承 PG 方言，只覆盖 Java 与 PG 不同的写法：两者都把 PG 写成 `LIKE CONCAT(…)` 的包含 / 开头 / 结尾改成 STRPOS（值不转义）、条件当值写 CASE；Kingbase 另有 CONCAT 两两嵌套、日期加减 `CONCAT(n, ' day')::INTERVAL`、日期相差 `timestampdiff`、树查询起点层级 `0+0`（`SqlDialect.TreeDeepStart`）。`UseKingbase` / `UseGaussDb`，SqlOnly 登记 kingbase / gauss。testDoubleConcat、testKingbaseCte、ForUpdate 冒烟的 Kingbase / GaussDB 两行移植；其余差异在 `Extras/PgFamilyDialectTest` 按 Java 模板核对。
  - MyKingbaseESTest 名字是 Kingbase，实际连开发者本机 MySQL 的现成数据，测的是多对多带出：导航带排序时一条查完、IN 按分组大小拆成 OR、子数据按查询结果的顺序。C# 原来按中间表顺序挂子数据、还按分组分批，顺序是错的，已修（对齐 Java）；两个用例在 SQLite 内存库里造同规模数据移植，记偏离。
  - 待做：实库冒烟与 `TestDatabases` 登记。openGauss 有 profile（`--profile opengauss`），要先选定 .NET 驱动并确认能连上（openGauss 默认的 sha256 认证普通 Npgsql 是否支持待验证）；Kingbase 没有可用的容器镜像。Java 的两个模块里多表 DELETE 没覆盖 PG 的 `DELETE … USING`（退回 MySQL 式的 `DELETE t FROM … JOIN`），C# 沿用 PG 的写法，没有 Java 用例覆盖。
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
- [ ] 4.5 二级缓存 `EasyQuerySharp.Caching`，先写决策文档 0007（用例在 QueryTest25：test11a、test11a1、topicIntegerTest、xxx）：
  - 参照 Java 的 `sql-extension/sql-cache`：单实体、多实体、键值三种缓存查询，写入后让缓存失效；
  - 对接 `Microsoft.Extensions.Caching` 的抽象，支持本机和分布式两级。
- [ ] 4.6 依赖注入 `EasyQuerySharp.DependencyInjection`：`AddEasyQuery`，多个库按名字注入。

完成标准：2404 个用例的待做为 0。

## 阶段 5 发布准备

- [ ] 5.1 冻结公共 API：PublicApiAnalyzers 的基线转为已发布（Shipped），公开成员都有 XML 文档。
- [ ] 5.2 基准对比：用 BenchmarkDotNet 和 EF Core、Dapper、FreeSql、SqlSugar 比较查询翻译、物化、对象树三项；根据结果决定要不要缓存翻译结果。
- [ ] 5.3 文档：在 `docs/guide/` 下按能力写 Markdown 指南（用法、方言支持矩阵、限制、对应的 Java 方法名），代码片段和 SQL 由 DocsExamples 生成；另写 README、入门教程，以及从 Java / TS 迁移的指南。
- [ ] 5.4 打包：NuGet 元数据、Source Link、确定性构建，生成器和分析器随核心包一起分发。是否发布到 nuget.org 在这一步决定。
