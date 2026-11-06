# ROADMAP

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。每项直接在 master 上做，做完提交推送，规则见 [CLAUDE.md](CLAUDE.md)。

本文只列还没做完的事。已完成能力的写法与限制见 [能力对照总表](docs/parity.md) 和各能力文档；2026-11-06 重新规划之前的版本见 git `0f0b76f:ROADMAP.md`。

## 目标

**1.0：成熟可用。**

- **对标**：Java 的 2404 个用例（上游 `sql-test` 2315 个、fork `sql-fork-test` 89 个），每个都有对应的 C# 测试，或者标为不适用并写明理由。基准冻结在 `tools/ParityTests/java-tests.json`。
- **按可发布的标准做**：公共 API 冻结、文档齐全、有基准对比、能打包。是否发布到 nuget.org 到阶段 5 再定。

设计依据：[0001 查询 API 路线](docs/decisions/0001-query-api-route.md)、[0002 对象树与 DTO](docs/decisions/0002-object-tree-and-dto.md)、[0003 测试对标](docs/decisions/0003-testing-and-parity.md)、[0004 聚合根保存](docs/decisions/0004-aggregate-save.md)、[架构](docs/design/architecture.md)。

## 现状（2026-11-06）

- 对标：已移植 2056、偏离 203、不适用 142、骨架 3、待做 0。
- 待做清零。骨架 3 都要实库：HANA 2、达梦行锁 1。
- 实库：本地和 CI 跑 SQLite、MySQL 8.4、PG 17、DuckDB（进程内）；CI 另有 MySQL 8.0.18、MariaDB、SQL Server、Oracle 任务。master 的 CI 全绿。

## 已定的取舍

**范围**

- 不做，对应的用例标为不适用：
  - search 扩展（`sql-search`，45 个）：这是第三方贡献的 URL 参数约定，由前端选运算符；同类需求用 `FilterBy` / `SortBy`。
  - SQL Server 2008 的 ROW_NUMBER 分页（13 个）：SQL Server 2008 已于 2019 年停止支持。
- 要做：分库分表、二级缓存、ClickHouse / TDengine / Db2、关联键匹配策略（Java 的 `relationPropertyStrategy`）。
- 暂不做，记进 [BACKLOG](docs/BACKLOG.md)：文档站、AOT 与元数据 / 物化器的 Source Generator、TS 版独有的能力（原 M9）。
- 发布到 nuget.org 暂缓（2026-11-06 定）：5.4 的许可证、包名前缀、版本与发布流程先不定，其余各项照常推进。

**架构**

- **方言留在核心包里**，只负责渲染 SQL。驱动由用户通过 `DbDataSource` 传入，核心不引用任何驱动。只有用到驱动专属 API（批量复制、PG 数组类型等）时，才单独出包。
- **方言分两档**：
  - 第一档，CI 跑全部实库用例：SQLite、MySQL 8.4 / 8.0.18（PolarDB 基线）/ MariaDB、PG 17、SQL Server、Oracle。
  - 第二档，比对 SQL 文本，再加实库冒烟：达梦、HANA、DuckDB、Kingbase / openGauss、ClickHouse、TDengine、Db2。容器起得来的进 CI，起不来的在本地验证。
- **生成器只保留 `[Projection]` 和分析器**。`[MapFrom<T>]` 的映射在运行时构造，编译期校验交给分析器。核心如实标注 `RequiresDynamicCode` / `RequiresUnreferencedCode`。
- **嵌套事务对齐 Java**：已经在事务里时再开事务，直接抛异常。
- **规划外的新公共概念先写决策文档**：关联键匹配策略、分库分表、二级缓存。
- **新依赖只进各自的包**：生成器引用 Microsoft.CodeAnalysis，依赖注入引用 Microsoft.Extensions 的抽象包，缓存引用 HybridCache，核心仍然零依赖。
- **第二档方言的测试驱动只进 TestKit**（2026-11-06 定）：达梦 DM.DmProvider、ClickHouse、Db2、TDengine 的 .NET 驱动只在测试里引用，用于实库冒烟；nuget.org 上没有的（HANA 的 Sap.Data.Hana）不引用。
- **集合导航取段之后聚合按 C# 语义**（2026-11-06 定）：`string.Join(",", u.Cards.OrderBy(..).Take(2).Select(..))` 先取段再聚合，不照 Java 把 LIMIT 写在聚合外面（Java 拼的是全部行，PG 上还会报错），M8BankTest test7 / test7_1 / test7_2 / test8 记偏离。
- **分库分表实现时补的公开成员保留**（2026-11-06 定）：`MaxShardNodes`、`DataSources`、`Shard<T>(IShardRule)`、`SqlStatement.DataSource`、`SqlDialect.DefaultNullsOrder`、`ShardingPage<T>`，记在 0006 的“实现中的补充”。

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
- [x] 2.2 Oracle：实库验证——CI 的 Oracle 23ai Free 任务跑完整的 LiveSuite（分库用例除外：测试用户没有建第二个 schema 的权限，`ShardDataSources = false`），全部通过；Java 的 Oracle 用例只比对 SQL，不需要 Oracle 夹具。已做：条件当值用（投影、比较的一侧、分组合并的 any / none 列）在 SQL Server、Oracle 上写成 `(CASE WHEN 条件 THEN ? ELSE ? END)`（Java 的 booleanSQLFunction），NumberFunctionsTest subQueryORACLE 移植。
- [x] 2.3 UPDATE / DELETE 里用对一导航（`write.join`）：SQL Server 原本已有；SQLite 写成 `UPDATE … AS t … FROM` / `DELETE … AS t WHERE EXISTS(…)`，Oracle 写成按 ROWID 对上的 `MERGE` / `DELETE … WHERE ROWID IN (…)`；`DateTrunc` 补上 SQLite（strftime）、SQL Server（DATETRUNC）、Oracle（TRUNC）。LiveSuite 加了两组用例，各库按同样的结果校验（SQL Server、Oracle 在 CI 上跑）。
- [~] 2.4 L3 `LiveSuite` 从现在的 4 组扩到 TS 的 23 组，`LiveSuiteOptions` 按方言裁剪能力；在第一档的每个库上全部通过（MySQL 8.0.18、MariaDB 各算一个库）。已移植 18 组（`LiveSuite.*.cs`，视图、列规则、code-first 与分表留到阶段 4），本地 SQLite / MySQL / PG / DuckDB 通过，其余库看 CI。顺手修了套件发现的问题：取消改为原样抛出、集合上排序后的 `string.Join` 把 ORDER BY 写进聚合、PG 的 DateDiff 截断、`Math.Log(x, b)` 的参数顺序、`Sql.Over` 等嵌套类不在客户端求值、`Take` 之后的聚合先包派生表、声明了条数的对一导航在查询里按分区取行、根上的自关联导航随聚合保存。
  - [x] 集合导航取段之后聚合（`string.Join(",", u.Cards.OrderBy(..).Take(2).Select(..))`、取段后的 Count / Sum 等）：先把这段行包成派生表再聚合，拼接按集合上的排序（Java 把 LIMIT 写在聚合外面、聚合的是全部行），M8BankTest test7 / test7_1 / test7_2 / test8 记偏离。

## 阶段 3 第二档方言

每个方言交付三样东西：方言类、在 `TestDatabases` 里登记、实库冒烟。容器起得来的再加一个 CI 任务。

- [~] 3.1 达梦：`DamengDialect` 继承 Oracle、`UseDameng`，`SqlOnly` 与文档快照加上达梦。缺实库冒烟（本机没有达梦库和 DM.DmProvider 驱动，`TestDatabases` 也还没登记）。
  - 覆盖的差异（对照 Java 的 sql-dameng）：带连接的更新写成 `UPDATE t INNER JOIN … SET`；转布尔 `CAST(x AS BIT)`；日期相差用 `CAST(终点 AS TIMESTAMP WITH TIME ZONE)-起点` 的间隔；不排序的 LISTAGG 不写 `WITHIN GROUP(ORDER BY 1)`；NULL 位置用 CASE 模拟（Java 只给 Oracle 写原生 NULLS LAST）。Java 没有覆盖、按 TS 版在 DM8 上实测的结果补的：截断到秒经过 `TO_CHAR` / `TO_TIMESTAMP`（DATE 只到日）、UTC 时间 `GETUTCDATE()`（没有 `SYS_EXTRACT_UTC`）、建表用 INT / BIGINT / BIT / DOUBLE / REAL / TIME 与 `IDENTITY(1, 1)`。
  - 顺手补齐 Oracle（达梦沿用）：拼接式 LIKE 写成 `LIKE ('%'||TO_CHAR(?)||'%')`；带组内排序的字符串聚合写成 `LISTAGG(..) WITHIN GROUP(ORDER BY …)`（原来落到 STRING_AGG），SQL Server 同样改成 `STRING_AGG(..) WITHIN GROUP (ORDER BY …)`。
  - 用例：DamengQueryTest 40、DamengForUpdateTest 7、UpdateJoinTest 3、MathQueryTest 1、QueryTest26 testAAA、ForUpdateDialectToSQLSmokeTest damengForUpdateToSQLSmoke 共 53 个：只渲染 SQL、逐字比对 Java；Java 只断言结果或只执行不断言的 21 个记偏离（实库验证留给冒烟）；testForUpdateBlocksConcurrentTransaction（两个事务争行锁）留骨架。testAAA：Java 的达梦删除不支持连接（抛异常），C# 沿用 Oracle 按 ROWID 删除。
  - 冒烟要验证：`UPDATE … INNER JOIN`、`DELETE … WHERE ROWID IN`、`GETUTCDATE()`、`IDENTITY(1, 1)`、日期相差与加减的结果、DmProvider 的参数占位（`:p0`）。MigrationDamengTest 断言的是 code-first 同步语句（注释、默认值、`execute immediate`）的 MD5，归到 4.1。
- [~] 3.2 SAP HANA：`HanaDialect` 直接继承标准写法（与 Oracle、PG 都只有部分相同），`options.UseHana(..)`；SQL 按 fork 用例核对，实库冒烟还没做。
  - [x] 方言：位置参数 `?`、`LIMIT … OFFSET`（只跳过时补 `LIMIT 2147483647`）、拼接逐对写 `((a || b) || c)`、`TO_NVARCHAR` 日期格式（格式串里的文字单独拼接）、条件当值写三值 CASE、`MERGE … FROM DUMMY` upsert、带导航的更新写按主键对上的 MERGE、删除写 EXISTS、EXISTS 子查询不带 LIMIT（`SqlDialect.LimitInExists`）、`CURRENT_IDENTITY_VALUE()` 取自增键、`FOR UPDATE IGNORE LOCKED`、`CREATE COLUMN TABLE` 与匿名块代替 IF [NOT] EXISTS、DateTrunc；
  - [x] `Sql.Hana.Text(..).Contains / Fuzzy / Score`（CONTAINS、FUZZY、SCORE）；
  - [x] fork 的 HanaSqlTest 6 个、HanaTextSqlTest 2 个、InsertSelectSqlTest#hanaUsesQuotedNames 移植；三个 live 文件与 JsonValueConverterLiveTest#hanaNclobColumn 共 14 个，其中 12 个按 SQL 文本移植、记偏离（实库验证留到冒烟），两个 LocalDateTime 往返用例要实库，留作骨架；
  - [!] 实库冒烟（2026-11-06 查过：本机没有 HANA 客户端，也没有构建过 hana 镜像；客户端要从 SAP 下载并接受许可协议，需要用户自己装）：驱动 Sap.Data.Hana 随 HANA 客户端安装、不在 nuget.org，TestDatabases 还没登记 HANA；起 infra/dev 的 hana profile 后补夹具，跑上面的 live 用例与 LiveSuite。HANA Express 要 12 GB 内存，进不了 GitHub 托管的 CI，只在本地验证。
- [x] 3.3 DuckDB（进程内运行，CI 直接跑）：`DuckDbDialect` 继承 PostgreSQL 方言只覆盖差异，`options.UseDuckDb(..)`；驱动 DuckDB.NET.Data.Full 只在 TestKit 里引用，核心仍零依赖。
  - 方言：参数写 `$p0`（DuckDB.NET 不认 `@`）；含 `%` / `_` 的 LIKE 值用 `STRPOS`；日期格式 `strftime`、加减 `date_add`、相差 `DATEDIFF`；`TRUNC` 不带位数；JSON 用 `json_array_length` / `json_exists`；没有行锁（`FOR UPDATE` 抛 NotSupportedException）；DuckDB 没有 IDENTITY，自增列建表时先 `CREATE SEQUENCE`、列默认 `nextval`，删表时一并删序列。
  - 顺手：PostgreSQL 的 `IndexOf` 原来写 `INSTR`（PG 没有这个函数），改成 `STRPOS`；`SqlDialect.ColumnDefinition` 多传所属实体。
  - 用例：Upstream 的 `DuckDb` 命名空间移植 17 个（`duckdb/` 的 16 个加 ForUpdateDialectToSQLSmokeTest 的 duckDB 一条）。readExcel1–4 只核对 SQL、记为偏离（read_xlsx 在 excel 扩展里，要联网安装，还要有 xlsx 文件；表函数用 `AsTableSegment` 替换表引用）；DuckDBMathQueryTest 与 PG 同样因 `Math.Pow` / `Atan2` 的参数是 Double 记为偏离；其余已移植。另有 `DuckDbDialectTest` 验证 Java 没覆盖到的写法（LIKE、序列、upsert、JSON）。
  - 实库：`DuckDbLiveSuite` 36 组通过（行锁一组跳过）；固件和套件都用临时文件并留一个连接不关（DuckDB.NET 在最后一个连接关闭时关库，反复开关慢 4 倍）。进程内运行，随 core 任务一起跑，不需要新的 CI 任务。
- [~] 3.4 PG 家族：Kingbase（MyKingbaseESTest，骨架 testKingbaseCte、GeneralFunctionsTest testDoubleConcat）、openGauss。
  - 已做：`KingbaseDialect` / `GaussDbDialect`（对应 Java 的 sql-kingbase-es、sql-gauss-db）继承 PG 方言，只覆盖 Java 与 PG 不同的写法：两者都把 PG 写成 `LIKE CONCAT(…)` 的包含 / 开头 / 结尾改成 STRPOS（值不转义）、条件当值写 CASE；Kingbase 另有 CONCAT 两两嵌套、日期加减 `CONCAT(n, ' day')::INTERVAL`、日期相差 `timestampdiff`、树查询起点层级 `0+0`（`SqlDialect.TreeDeepStart`）。`UseKingbase` / `UseGaussDb`，SqlOnly 登记 kingbase / gauss。testDoubleConcat、testKingbaseCte、ForUpdate 冒烟的 Kingbase / GaussDB 两行移植；其余差异在 `Extras/PgFamilyDialectTest` 按 Java 模板核对。
  - MyKingbaseESTest 名字是 Kingbase，实际连开发者本机 MySQL 的现成数据，测的是多对多带出：导航带排序时一条查完、IN 按分组大小拆成 OR、子数据按查询结果的顺序。C# 原来按中间表顺序挂子数据、还按分组分批，顺序是错的，已修（对齐 Java）；两个用例在 SQLite 内存库里造同规模数据移植，记偏离。
  - 待做：实库冒烟与 `TestDatabases` 登记。openGauss 有 profile（`--profile opengauss`），要先选定 .NET 驱动并确认能连上（openGauss 默认的 sha256 认证普通 Npgsql 是否支持待验证）；Kingbase 没有可用的容器镜像。Java 的两个模块里多表 DELETE 没覆盖 PG 的 `DELETE … USING`（退回 MySQL 式的 `DELETE t FROM … JOIN`），C# 沿用 PG 的写法，没有 Java 用例覆盖。
- [~] 3.5 ClickHouse（ClickHouseTest）、TDengine、Db2：Java 用例很少或没有，主要靠裁剪后的 L3 套件验证。方言类在核心里只渲染 SQL，`options.UseClickHouse / UseDb2 / UseTDengine(..)`，`SqlOnly` 里登记。
  - ClickHouse：反引号、`LIMIT … OFFSET`；更新、删除是 mutation `ALTER TABLE … UPDATE / DELETE WHERE …`（与 Java 一致；不能带别名、不能走导航，没有条件时补 `WHERE 1 = 1`）；没有 upsert、自增列、行锁，用到时抛 NotSupportedException（Java 照搬了 MySQL 的 `INSERT IGNORE` / `ON DUPLICATE KEY`，ClickHouse 执行不了）；函数用 `substringUTF8` / `positionUTF8`、`toYear` / `addDays` / `dateDiff`、`formatDateTime`（分钟是 `%i`）、`JSONExtract*`、`arrayStringConcat(groupArray(..))`；建表 `ENGINE = MergeTree ORDER BY (主键)`。
  - Db2：`OFFSET … ROWS FETCH NEXT … ROWS ONLY` / `FETCH FIRST`（与 Java 一致，跳过行数而没有排序时按分组键或第一个主键排序）；行锁 `FOR UPDATE WITH RS`（CS 隔离级别下游标离开的行就解锁）、`SKIP LOCKED DATA`，没有 NOWAIT；冲突处理写成 `MERGE … USING (VALUES …)`；插入后取 `IDENTITY_VAL_LOCAL()`；字符串函数按 `CODEUNITS32`，日期用带单位的时长、`TRUNC_TIMESTAMP`、`VARCHAR_FORMAT`，`LISTAGG` 的分隔符写成字面量，`ATAN2` 是 (x, y)。Java 的 DB2CastSQLFunction、多参数 CONCAT、`AT TIME ZONE` 的 UtcNow、`USING (SELECT ? AS …)` 的 MERGE 在 Db2 上执行不了，按 Db2 改写。
  - TDengine：反引号、`LIMIT … OFFSET`；没有 UPDATE（与 Java 一致）、upsert、自增列、行锁；删除不带别名、只按本表的列过滤；`TO_CHAR` / `TIMEDIFF` / `TIMETRUNCATE`，时长只能写字面量，所以日期加减不支持；建表时 DateTime 主键排第一列、列不写 NULL。
  - 公共：`SqlDialect.WriteUpdate` / `WriteDelete` 让方言改写不走连接的更新、删除；SQL Server 的转义 LIKE 抽成 `WriteEscapedLike`（Db2、ClickHouse、TDengine 共用）；`WriteMerge` 可覆盖。
  - 用例：ClickHouseTest 4 个、ForUpdateDialectToSQLSmokeTest 的 clickHouse / db2 两条移植（只渲染）；`Dialects/` 下另有 `ClickHouseDialectTest`、`Db2DialectTest`、`TDengineDialectTest` 核对分页、写入、连接、分组、函数表、行锁与建表的 SQL 文本。
  - 待做：实库冒烟。compose 已有 clickhouse / db2 / tdengine 三个 profile；要在 TestKit 里加测试用的驱动（ClickHouse.Client、Net.IBM.Data.Db2、TDengine.Connector，新依赖先说明）、`TestDatabases` 登记、LiveSuite 子类按能力裁剪（参照 TS 的 analyticSuite / tdengine live 测试），跑通后各方言的参数占位符、函数表再按实库修正。
- [x] 3.6 H2 的 74 个用例：H2 没有 .NET 驱动，改在 SQLite 共享内存库上跑（`Upstream/H2/`，`H2Fixture` 对应 H2BaseTest）。Java 的 H2 用例把关键字服务换成不加引号的 DefaultSQLKeyword，C# 同样用标识符不加引号的 SQLite 方言，63 个 SQL 与 Java 逐字一致、记为已移植（H2DocTest 用到的 doc 表 Java 的 H2 库里没有，这里建好并核对结果）。偏离 4 个：testDDL / testDDL2 / testTableCommentDDL 的建表语句按 SQLite 方言（没有注释），tree8 的递归 CTE 按 SQLite 写法（分支不加括号、不写列名清单）。不适用 4 个：H2ForUpdateTest 里断言 FOR UPDATE 文本与行锁等待的用例（SQLite 没有行锁，MySQL、PG 上已有同样的用例）。分表的 3 个（H2OrderTest ×2、H2QueryTest allTypeTestSharding1）留骨架，随 4.4 做。顺手补齐：`sbyte` 列有了默认列类型（原来建表报不支持）；TestKit 的参数文本支持 sbyte（Byte）、TimeSpan（Time），DateOnly / TimeOnly 按 Java 的 LocalDate / LocalTime 格式输出。
- [x] 3.7 跨方言的零散用例：ForUpdateDialectToSQLSmokeTest 剩下的 h2 一条记不适用（没有 H2 方言，SQLite 没有行锁），NullSafeSqlTest nullSafeEqualsPerDialect 按各方言移植（MySQL 一条与快照逐字一致，其余方言核对 C# 的写法）。同一批零散用例还移植了 QueryTest3 userBookTest、QueryTest22 testManyOrder、QueryTest26 testGenerateKeys、SQLiteTest testDDL / testDDL2、testmysql8 MyTest testaa；SQLite 在 SQL 里拼的 LIKE 改成 `('%'||v||'%')`（Java 的写法，`CONCAT` 要 SQLite 3.44）。

## 阶段 4 平台能力

- [x] 4.1 code-first：建表（注释、索引、外键）、只增不删的同步、`RenamedFrom`、`ToSql()`、读取库结构（MigrationTest 等 15 个，含 MigrationDamengTest）。
  - 已做：`[TableIndex]`（不写名字时 `表_列_idx` / `_uidx`，与 Java 一致）、对一导航上的 `[ForeignKey(Action = ..)]`（`本表_目标表_列_fk`），建表时外键排在全部建表之后（SQLite、DuckDB 写在建表语句里）；`[RenamedFrom]` 用在类与属性上；`db.CodeFirst.PlanSyncAsync(..)` 得出 `CodeFirstCommand`（`Statements`、`ToSql()`、`Differences`、`ExecuteAsync()`），`SyncTablesAsync(..)` 得出并执行，`PlanSync(types, tables)` 与保存下来的结构比较、不连库；`ReadTablesAsync()` / `TableExistsAsync(type)` 读当前 schema 的列、主键、索引、外键（各方言的系统表查询照 TS 版，TDengine 不支持）。同步只增不删：建缺的表，改表名列名，加缺的列（带注释），补缺的索引与外键；库里多出来的列、可空性不一致写进 `Differences`，不比较类型。
  - 方言：MySQL、PostgreSQL（Kingbase、GaussDB 沿用）、SQLite、DuckDB 的建表、加列、改名、索引、外键语句与 Java 逐字一致（MyMigrationBlog0 的 MySQL / PG 建表语句与 Java 断言的 MD5 相同），PG、SQLite、DuckDB 的列类型改成 Java 迁移解析器的（`INT4`、`BOOL`、`numeric(16,2)`；`integer`、`TEXT`）；Java 的几处错误按各库语法改正（PG 改列名后的注释漏了列名、SQLite 改列名多写类型、自增列写成 `NOT NULL AUTOINCREMENT`、PG / SQLite 新表名带 schema）。SQL Server 的注释是扩展属性（已有时不重复加）、改名用 `sp_rename`；Oracle、达梦、HANA 加列 `ADD (…)`；HANA、Db2、ClickHouse 改表名 `RENAME TABLE`；ClickHouse 的索引是跳数索引、没有外键。顺手修了计算列被当成真实列建进表里的问题。
  - 用例：MigrationTest ×4、pgsql/MigrationTest、migration/MigrationPgTest、SQLiteTest testDDL / testDDL2、QueryTest22 testManyOrder 已移植；migration/MigrationDamengTest（达梦沿用 TS 实测的写法，只核对 SQL）、QueryTest3 userBookTest（地址列没有 Java 端加密）、testmysql8/MyTest testaa（没有建库）记偏离；H2 的 testDDL2 随 SQLite 改成 Java 的格式改回已移植。另有 `Extras/CodeFirstTest` 核对各方言的写法，并在 SQLite、DuckDB 上实际建表、读结构、改名加列。MySQL、PG 上的用例要实库验证。
  - 列默认值 `[Column(DbDefault = "…")]`（Java 的 dbDefault，SQL 原文）：建表、加列时写上 `DEFAULT`，多行格式的方言写在 NULL / NOT NULL 后面（与 Java 一致），标准写法写在前面（Oracle 系要求），ClickHouse 写在后面，TDengine 报不支持；与 `Generated` 同时声明时报错。MySQL 改列名的 `CHANGE` 带上默认值与自增（Java 没带，改名后会丢）。MySQL8Test2 test 与 Java 的 MD5 一致、QueryTest26 testGenerateKeys 改用 code-first 建表，都记为已移植；LiveSuite 在各实库上验证插入时为 null 的列取默认值。用法见[建表与同步](docs/guide/code-first.md)。
- [x] 4.2 表名映射：
  - 查询级 `MapTable<T>("表名")` / `MapTable<T>(name => …)`、`InSchema("schema")`（所有查询类型都有）：这条查询翻译出的实体表——主表、联表、隐式 JOIN、集合导航的子查询与分组合并、取第几行、多对多中间表、Include 与 DTO 自动带出、嵌在里面的子查询——都按它换，子查询自己的映射优先，显式的 `AsTable` / `Sql.AsTable` 优先；
  - `WithTableLink`：Java 的 asTableLink，改写最近一张表前面的连接符（`LEFT JOIN` → `LEFT JOIN lateral`），写入时写在目标表名前面，查询、联表、写入都能用（走导航的更新 / 删除明确报不支持）；
  - fork TableNameMapping 7 个（mappedTableNameIsReadable 偏离：C# 的映射是查询上的不可变状态，没有作用域可读）、asTable 2 个、asSchema 1 个、asTableLink 4 个移植；QueryTest14 test2 / test3 是 Oracle / SQLite 的整段冒烟，偏离在 SQLite 的冲突写法、MERGE 约束列的顺序与列模式的 LIKE；
  - 顺手修了 MERGE（SQL Server、Oracle）没有约束列时拿自增主键作约束、引用 USING 里没有的列，改为与 Java 一样报错。
- [x] 4.3 `FilterBy` / `SortBy` 剩余的 6 个用例，以及 `Table(request)`：
  - `FilterOp.Contains` / `StartsWith` / `EndsWith` 改成与 `Sql.LikeContains` 等相同（Java 的 CONTAINS 等：`LIKE CONCAT(…)`，值里的通配符按字面），whereObject2 / 4 / 6、DocTest test1、ExampleTest sampleTest1 移植；
  - 声明了条数的对一导航（Java 的 OneToOne + limit + orderByProps，如"第一张卡"）在条件、投影、隐式子查询里按排序取那一行（原来是普通 LEFT JOIN，会匹配全部行），`FilterNavigation` 的条件写进它的 ON，MySQL8Test4 testFilter 移植；
  - `Table(request)` 写成 `ToPageAsync(TableRequest)`（C# 的终结方法命名）：`TableRequest.Parse` 读地址栏参数（与 TS 的 parseTableRequest 同一约定），`filter.列` IN、`search.列` 包含、`sort` 只认结果里的列，带了 `total` 不再计数；条件树与整天区间（TS 11b.3a）记进 BACKLOG。
- [x] 4.4 分库分表（ShardingTest 48 个，另有 H2 的 3 个骨架：H2OrderTest ×2、H2QueryTest allTypeTestSharding1），按 [0006](docs/decisions/0006-sharding.md) 实现，用法见 [分库分表指南](docs/guide/sharding.md)：
  - 规则：`options.Shard<T>(r => r.Mod(..) / ByYear / ByMonth / ByDay(..))`，可加 `ExtraKey(..)`、`DefaultOrder(..)`；自定义规则实现 `IShardRule`；数据源 `WithDefaultDataSourceName(..)` / `AddDataSource(..)`；`db.GetShardRule<T>()` 读规则。
  - 路由（`Sharding/`）在翻译好的 `SelectQuery` 与执行器之间：按所在查询的 WHERE 裁剪（AND 取交集、OR 取并集、IN 取各值并集；条件矛盾时空路由，不访问数据库；`MaxShardNodes` 上限）；最外层分片表的节点按数据源分组、按 `MaxUnionTables`（64）切块，一块一条 `(SELECT * FROM a UNION ALL SELECT * FROM b) t`；嵌套处内联成 UNION ALL（只能在一个数据源）。执行器接收 `SelectQuery` 的入口覆盖终结方法、Include、树查询；多条语句并行执行，k 路归并（隐藏排序列、`SqlDialect.DefaultNullsOrder`）、统一分页，count / any / sum / min / max 合并；跨单元的分组、去重、Avg、窗口、流式读取报 `NotSupportedException`。`ToShardingPageAsync` 按节点计数（`ShardingPage<T>.TotalLines`）。
  - 写入：插入按 `NodeOf`，按实体更新 / 删除同样，按条件逐节点串行；Set 里改分片键、`Returning`、`InsertSelect`、聚合保存里的分片实体报错。环境事务按数据源各开连接、依次提交（不是分布式事务）。code-first 按节点在各自的数据源建表、删表、同步。
  - 用例：ShardingTest 44 个移植，sharding40–42 偏离（每个分支只展开自己的节点：30 / 58 / 484，UNION ALL 两倍），test111 不适用；`ShardingFixture` 用 `easy-query-test` 加 2021 / 2022 / 2023 三个库，87 张表由分片的 code-first 建、种子经分片写入。H2OrderTest ×2、allTypeTestSharding1 在 SQLite 上移植（`MaxUnionTables = 1`，覆盖内存归并）。LiveSuite 加分片一组（路由、裁剪、UNION ALL、计数与分页合并、逐节点写入、两个库与跨库事务），SQLite / MySQL / PG / DuckDB 通过，Oracle 没有第二个库（`ShardDataSources = false`）。0006 没写到的细节记在它末尾的“实现中的补充”。
- [x] 4.5 二级缓存 `EasyQuerySharp.Caching`（只引用 Microsoft.Extensions.Caching.Hybrid 10.11.0），设计见 [0007](docs/decisions/0007-second-level-cache.md)；只做 kv、all 两种（Java 的 Multi 是注释掉的死代码）：
  - 核心新增写入监听：`IWriteListener` / `WriteEvent` / `WriteKind`、`EasyDbOptions.AddWriteListener`、`EasyDb.InTransaction`。渲染器给 INSERT / UPDATE / DELETE / INSERT … SELECT 盖上内部的写入标记，会话记下成功执行的语句，结束时按表合并通知一次，主键从语句里取（INSERT 的主键值，UPDATE / DELETE 顶层 AND 里主键的 `=` / `IN`，经转换器还原）；环境事务里推迟到提交之后，回滚丢弃；监听器的异常包成 `EasyQueryException`；没有监听器时什么也不记。各写入路径（按实体、按条件、批量、upsert、逻辑删除、`Returning()`、聚合保存、`InsertMap` / `UpdateMap`、`InsertInto`、事务）的用例在 `Extras/WriteListenerTest`。
  - 缓存包：`new EasyCache(hybridCache, o => o.Cache<T>())` + `options.UseCache(cache)`；`cache.Of(db).Kv<T>()`（`FindAsync` / `FindOrThrowAsync` / `FindManyAsync` / `ExistsAsync`，`Where`、拦截器开关进缓存键，`Filter` 在内存里筛）、`All<T>()`（主键索引 + 按主键取行，排序、`ToListAsync`、`ToIdsAsync`、`CountAsync`、`AnyAsync`、`ToPageAsync`）、`InvalidateAsync<T>(id?)`。键是表纪元下的行桶与索引桶，写入按主键删行与索引、主键不明时整表换纪元，`LateEvictDelay` 延迟双删；事务里的读绕过缓存。HybridCache 的行为先用 `Extras/HybridCacheProbeTest` 探测，实现中的两处调整记在 0007 末尾。
  - 用例：QueryTest25 的 test11a、test11a1 记偏离（Java 读 Caffeine 的存储布局换成行为断言；test11a1 的 IN 参数顺序），topicIntegerTest、xxx 移植，都要 MySQL 实库验证；C# 独有的 `Extras/CacheTest`（SQLite，两个实例经内存版 `IDistributedCache` 共享 L2）。用法见 [二级缓存指南](docs/guide/caching.md)；注册要在创建 `EasyDb` 时接上缓存、存储来自容器，DocsExamples 的卡片放不下，只写指南。
- [x] 4.6 依赖注入 `EasyQuerySharp.DependencyInjection`（只引用 Microsoft.Extensions.DependencyInjection.Abstractions 10.0.14）：`services.AddEasyQuery(o => ..)` / `AddEasyQuery((sp, o) => ..)` 把 `EasyDb` 注册成单例（线程安全，环境事务与追踪作用域借 AsyncLocal 跟随异步流程，每个请求本就隔离）；`AddEasyQuery("name", ..)` 注册成 keyed 单例，用 `[FromKeyedServices("name")]` 注入。对应 Java starter 的自动装配：容器里的 `IExecutionListener`、`IInterceptor` 在配置函数之前加入，配置函数没有指定数据源时取容器里的 `DbDataSource`（Npgsql / MySqlConnector 的 `AddXxxDataSource` 会登记它）；默认的库只取不带键的服务，命名的库只取以库名为键的服务。测试在 `tests/EasyQuerySharp.Tests.DependencyInjection`（真实容器 + SQLite），用法见 [依赖注入指南](docs/guide/dependency-injection.md)。DocsExamples 的卡片按方言比对 SQL，注册代码没有 SQL 可比，所以只写指南。

完成标准：2404 个用例的待做为 0。

## 阶段 5 发布准备

- [ ] 5.1 冻结公共 API：PublicApiAnalyzers 的基线转为已发布（Shipped），公开成员都有 XML 文档。
- [x] 5.2 基准对比：`benchmarks/EasyQuerySharp.Benchmarks` 在同一个 SQLite 文件库（WAL）上和 EF Core 10.0.14、Dapper 2.1.89、FreeSql 3.5.311、SqlSugar 5.1.4.221 比较查询翻译、物化、对象树三项（四个包只在基准项目里引用），GlobalSetup 先核对各 ORM 的结果一致，`--sql` 打印各家实际执行的 SQL。结果、环境与公平性说明见 [基准对比](docs/benchmarks.md)。
  - 结论：翻译结果不缓存。不缓存的翻译已是四家里最快（简单查询 6.6 µs、复杂 21 µs，EF Core 命中查询缓存是 26 / 50 µs），只占一条 10 行查询的约 8%。10 行查询与 EF Core 预编译查询相当；1000 行时逐列读取的装箱拖慢物化（ADO.NET 的 1.4–2.2 倍），记进 BACKLOG。
- [x] 5.3 文档：在 `docs/guide/` 下按能力写 Markdown 指南（用法、方言支持矩阵、限制、对应的 Java 方法名），代码片段和 SQL 由 DocsExamples 生成；另写 README、入门教程，以及从 Java / TS 迁移的指南。
  - [x] 已完成的能力都有指南（索引见 [docs/README.md](docs/README.md#指南)）：入门、实体与映射、列规则、查询、联表与导航、带出与对象树、函数、分组与聚合、写入、追踪与聚合保存、事务与行锁、原生 SQL、换表、动态筛选与列表页、方言、迁移，加上已有的依赖注入；根目录 README 重写。
  - [x] 指南与 README 里的代码不手写：`<!-- card: 页面/方法 sql=方言 -->` 区域由 DocsExamples 从卡片（Roslyn 截取方法体）和各方言的 SQL 重写，`<!-- snippet: 名字 -->` 区域取 `docs/examples` 或示例模型里标记的代码，`--check` 时过期或引用不存在都算失败。新增约 130 张卡片（`docs/examples` 下的新文件），卡片随生成器与分析器一起编译。
  - [x] code-first、分库分表、二级缓存的指南：[建表与同步](docs/guide/code-first.md)、[分库分表](docs/guide/sharding.md)、[二级缓存](docs/guide/caching.md)。
  - [x] 写文档时发现的问题都已修完：原生 SQL 分成只收插值字符串的 `SqlQueryAsync` / `ExecuteSqlAsync` / `FromSql` 与收普通字符串的 `*Raw`；聚合保存时外键为默认值的子对象不算已挂到别的父对象；带排序 / 条数的 UNION 分支在 SQLite、SQL Server 上包派生表；投影里集合的 Skip / Take；单值投影按值本身排序；声明了条数的对一导航判空；`PadLeft` / `PadRight` 在 SQLite、SQL Server 上模拟、MySQL 补填充字符（Java 的写法执行不了，记偏离）；`Convert.ToXxx` 的类型化重载；平铺只认 `[MapPath]`（改 0002 的文字）；直达的对多导航写在投影里时链上的条件、排序、取段、投影不再丢掉，经中间导航带出时按根父对象取段（原来按中间父对象）。
- [~] 5.4 打包：NuGet 元数据、Source Link、确定性构建，生成器和分析器随核心包一起分发。是否发布到 nuget.org 在这一步决定。打包已就绪；发布到 nuget.org 暂缓，下面几项先不定。
  - 已做：`src/Directory.Build.props` 是唯一的版本来源（`1.0.0-preview.1`，CI 打包传 `CiBuildNumber` 追加成 `-preview.1.ci.N`），统一写包元数据（作者、项目与仓库地址、发布说明链接、README、标签）、`.snupkg` 符号包、`PublishRepositoryUrl`，GitHub Actions 上开 `ContinuousIntegrationBuild`；Source Link、`Deterministic`、`EmbedUntrackedSources` 用 .NET SDK 的默认值。
  - 出三个包：`EasyQuerySharp`（`lib/net10.0`，生成器在 `analyzers/dotnet/cs`，没有包依赖）、`EasyQuerySharp.DependencyInjection`（依赖核心与 Microsoft.Extensions.DependencyInjection.Abstractions）、`EasyQuerySharp.Caching`（依赖核心与 Microsoft.Extensions.Caching.Hybrid）；核心的分析器随依赖传递，只装扩展包也有生成器。其余项目都不打包。CI 的 pack 任务打包、检查生成器在包里、上传 artifact，不推送。
  - 发布前待定（暂缓）：
    - 许可证：仓库还没有 LICENSE。Java 上游与 TS 版都是 Apache-2.0，本仓库搬了 Java 用例的 SQL 与建表脚本，选型要和它兼容。定了之后加 LICENSE 文件和 `PackageLicenseExpression`。
    - 包名与前缀：`EasyQuerySharp`、`EasyQuerySharp.DependencyInjection` 在 nuget.org 上还没人占用（2026-11-06 查过）。要不要用 nuget.org 账号申请保留 `EasyQuerySharp.*` 前缀；另外 nuget.org 上已有不相干的 `Korzh.EasyQuery.*`，名字相近。`Authors` 现在填的是 GitHub 账号 kenconnet666，要和发布用的 nuget.org 账号对上。
    - 版本与发布流程：首个公开版本是 `1.0.0-preview.1`，还是等 5.1 冻结 API 后直接发 1.0.0；发布由什么触发（例如打 tag），API key 放进仓库 secret，nupkg 与 snupkg 一起推送。
    - 包里的 README 用仓库根的 README.md（随 5.3 重写过）；里面的相对链接在 nuget.org 上打不开，发布前改成绝对链接。
