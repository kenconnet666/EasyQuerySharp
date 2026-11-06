# 文档索引

工作约定见仓库根的 [CLAUDE.md](../CLAUDE.md)，里程碑见 [ROADMAP.md](../ROADMAP.md)。

## 先读

| 文档 | 内容 |
|---|---|
| [ROADMAP.md](../ROADMAP.md) | 里程碑 M0–M10、工作项、每个里程碑的完成标准 |
| [parity.md](parity.md) | 能力对照总表：Java 的每种能力、用例数、计划的 C# 写法、所属里程碑 |
| parity-tests.md | 逐条用例对照，由 `tools/ParityTests` 生成，ROADMAP 0.7 完成后才有 |
| [BACKLOG.md](BACKLOG.md) | 记下但暂时不做的事 |
| [HANDOFF.md](HANDOFF.md) | 交接记录：换机器继续时先读 |

## 设计决定

- [0001 查询 API 路线](decisions/0001-query-api-route.md)：POCO 实体加表达式树，不复刻 Java Proxy；表达式树里不能写的语法和对策；性能对策。
- [0002 对象树与 DTO](decisions/0002-object-tree-and-dto.md)：所有写法归一成投影表达式；对一用 JOIN、对多分层批量查询；三种 DTO 写法；Include、级联保存、树。
- [0003 测试对标](decisions/0003-testing-and-parity.md)：逐条移植 Java 用例；SQL 文本与 Java 对齐；测试分四层；本地与 CI 的分工。
- [0004 聚合根保存](decisions/0004-aggregate-save.md)：按追踪快照的差异保存整个聚合；级联声明、导航分类、差异与语句顺序。

## 设计

- [架构](design/architecture.md)：模块、查询管线、核心抽象、用到的 C# 特性、分析器、扩展点。
- [文档站](design/docs-site.md)：示例模型、卡片写法、各方言 SQL 的生成方式和快照。

## 参考仓库

| 仓库 | 用途 |
|---|---|
| `C:\Users\lionheart\IdeaProjects\easy-query` | Java 原版（软 fork）。用例在 `sql-test`、`sql-fork-test`；fork 的规划在 `docs/fork/` |
| `C:\Users\lionheart\IdeaProjects\easy-query-ts` | TS 移植版。对照工具在 `tools/parity-tests.mjs`、`tools/capabilities.mjs`；共用实库套件在 `packages/test/src/suite.ts`；文档站在 `apps/docs`；设计决定在 `docs/decisions/` |
