# 文档索引

工作约定见仓库根的 [CLAUDE.md](../CLAUDE.md)，路线图见 [ROADMAP.md](../ROADMAP.md)。

## 先读

| 文档 | 内容 |
|---|---|
| [ROADMAP.md](../ROADMAP.md) | 到 1.0 的剩余工作：已定的取舍、整理轮 R 与阶段 1–5、完成标准 |
| [parity.md](parity.md) | 能力对照总表：Java 的每种能力、用例数、C# 写法 |
| [parity-tests.md](parity-tests.md) | 逐条用例对照，由 `tools/ParityTests` 生成，不要手改 |
| [BACKLOG.md](BACKLOG.md) | 记下但暂时不做的事 |

## 设计决定

- [0001 查询 API 路线](decisions/0001-query-api-route.md)：POCO 实体加表达式树，不复刻 Java Proxy；表达式树里不能写的语法和对策；性能对策。
- [0002 对象树与 DTO](decisions/0002-object-tree-and-dto.md)：所有写法归一成投影表达式；对一用 JOIN、对多分层批量查询；三种 DTO 写法；Include、级联保存、树。
- [0003 测试对标](decisions/0003-testing-and-parity.md)：逐条移植 Java 用例；SQL 文本与 Java 对齐；测试分四层；本地与 CI 的分工。
- [0004 聚合根保存](decisions/0004-aggregate-save.md)：按追踪快照的差异保存整个聚合；级联声明、导航分类、差异与语句顺序。
- [0005 关联键的匹配方式](decisions/0005-relation-key-match.md)：`KeyMatch.SelfCsv`（本表一列存逗号分隔的目标键）与 `Sql.InCsv` 的各方言写法、三种场景与限制。
- [0006 分库分表](decisions/0006-sharding.md)：节点与规则、按 WHERE 裁剪、库内 UNION ALL 与跨库归并、写入路由、事务，以及 48 个 Java 用例的归属。
- [0007 二级缓存](decisions/0007-second-level-cache.md)：显式的 kv / all 缓存包、核心的写入监听、HybridCache 上的键与桶、失效规则与一致性限制。

## 指南

按能力写的用法、方言差异、限制与对应的 Java 写法。代码和 SQL 由 `tools/DocsExamples` 从 `docs/examples` 的卡片生成（在 SQLite 示例库上实跑），不手写。

| 指南 | 内容 |
|---|---|
| [入门](guide/getting-started.md) | 安装、选方言和驱动、定义实体、创建 EasyDb、第一条查询和写入 |
| [实体与映射](guide/entities.md) | `[Table]` / `[Column]`、命名约定、主键生成、枚举与值转换、JSON 列、值对象、计算列、关系的声明 |
| [列规则](guide/rules.md) | 逻辑删除、乐观锁、查询过滤与写入拦截器（多租户、自动填充）、列加密 |
| [查询](guide/queries.md) | 终结方法、条件、子查询、排序与分页（含深分页）、投影与 DTO（`Select<TDto>()`、`[MapPath]`、`[Projection]`）、UNION 与 CTE、流式读取 |
| [联表与导航](guide/joins-and-navigations.md) | 显式 JOIN、对一导航的隐式 JOIN、集合导航的隐式子查询、分组合并、取第几个元素、直达导航 |
| [带出与对象树](guide/include-and-trees.md) | `Include`、`IncludeLimitMode`、投影里的嵌套集合、按 DTO 结构自动带出、按键填充、树 |
| [函数](guide/functions.md) | 字符串、日期、数值、NULL 与 CASE、JSON、窗口函数、方言专属函数、自定义函数，各方言的支持 |
| [分组与聚合](guide/grouping.md) | `GroupBy` / `Having`、按条件聚合、字符串聚合、ROLLUP / CUBE、整表聚合 |
| [写入](guide/writes.md) | 插入、回填主键、upsert、按条件 / 按实体更新、删除、批量、`RETURNING`、走导航的更新删除 |
| [追踪与聚合保存](guide/save-and-tracking.md) | 追踪作用域、差异更新、`db.Save(根)` 的级联与行为开关 |
| [事务与行锁](guide/transactions.md) | 环境事务、回调式事务、不能嵌套、`ForUpdate` 各方言写法 |
| [原生 SQL](guide/raw-sql.md) | `SqlQueryAsync` / `ExecuteSqlAsync`（插值参数化）、`FromSql`、`Sql.Raw`、`ReadAsync` |
| [换表](guide/tables.md) | `AsTable`、`MapTable`、`InSchema`、`AsTableSegment`、`WithTableLink`，写入时换表 |
| [动态筛选与列表页](guide/filter.md) | `FilterBy` / `SortBy`、`ToPageAsync(TableRequest)` |
| [方言](guide/dialects.md) | 13 个方言的档位、实库验证情况、能力矩阵、自定义方言 |
| [从 Java / TS 迁移](guide/migration.md) | 写法差别，Java → C#、TS → C# 的 API 对照表 |
| [依赖注入](guide/dependency-injection.md) | `services.AddEasyQuery(..)`，多个库按名字注入，从容器取数据源、监听器、拦截器，生命周期 |
| [分库分表](guide/sharding.md) | 登记规则与数据源、按条件裁剪、同库分表的 UNION ALL 与跨库归并、写入路由、事务、code-first、限制 |
| [二级缓存](guide/caching.md) | `EasyQuerySharp.Caching` 的 kv / all 缓存、HybridCache 两级存储、写入后的自动失效与写入监听、一致性限制 |

建表与迁移（code-first，ROADMAP 4.1）的用法见 [实体与映射](guide/entities.md) 与 [方言](guide/dialects.md)，单独的指南待补。

## 设计

- [架构](design/architecture.md)：模块、查询管线、核心抽象、用到的 C# 特性、分析器、扩展点。
- [文档站](design/docs-site.md)：示例模型、卡片写法、各方言 SQL 的生成方式和快照。

## 性能

- [基准对比](benchmarks.md)：和 EF Core、Dapper、FreeSql、SqlSugar 比较查询翻译、物化、对象树三项；怎么跑、测试环境、结果，以及翻译结果要不要缓存的结论。

## 参考仓库

| 仓库 | 用途 |
|---|---|
| `C:\Users\Administrator\IdeaProjects\easy-query`（相对仓库根 `..\..\IdeaProjects\easy-query`） | Java 原版（软 fork）。用例在 `sql-test`、`sql-fork-test`；fork 的规划在 `docs/fork/`。本机这份是上游 dromara 的 main（3.3.1），没有 fork 部分：fork 的 89 个用例只在快照 `tools/ParityTests/java-tests.json` 里 |
| `C:\Users\Administrator\RiderProjects\easy-query-ts`（相对仓库根 `..\easy-query-ts`） | TS 移植版。对照工具在 `tools/parity-tests.mjs`、`tools/capabilities.mjs`；共用实库套件在 `packages/test/src/suite.ts`；文档站在 `apps/docs`；设计决定在 `docs/decisions/` |
