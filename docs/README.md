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
- [0007 二级缓存](decisions/0007-second-level-cache.md)（草案）：显式的 kv / all 缓存包、核心的写入监听、HybridCache 上的键与桶、失效规则与一致性限制。

## 指南

按能力写的用法、限制与对应的 Java 写法（ROADMAP 5.3 补齐其余能力）。

- [依赖注入](guide/dependency-injection.md)：`services.AddEasyQuery(..)`，多个库按名字注入，从容器取数据源、监听器、拦截器，生命周期。
- [分库分表](guide/sharding.md)：登记规则与数据源、按条件裁剪、同库分表的 UNION ALL 与跨库归并、写入路由、事务、code-first、限制。

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
