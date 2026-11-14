# ROADMAP

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。每项直接在 master 上做，做完提交推送，规则见 [CLAUDE.md](CLAUDE.md)。

本文只列还没做完的事和仍然有效的取舍。已完成能力的写法与限制见[能力对照总表](docs/parity.md)和各[指南](docs/README.md#指南)，各版本的变化见 [CHANGELOG](CHANGELOG.md)。1.0 之前逐项的实现记录（整理轮 R、阶段 1–5）见 git `f85a133:ROADMAP.md`。

## 现状（2026-11-09）

- **1.0.0 已发布到 nuget.org**：`EasyQuerySharp`、`EasyQuerySharp.DependencyInjection`、`EasyQuerySharp.Caching`。公共 API 已冻结，发版步骤见 `.claude/skills/release`。
- 对标：Java 的 2404 个用例（上游 `sql-test` 2315、fork `sql-fork-test` 89），已移植 2071、偏离 191、不适用 142，待做 0。基准冻结在 `tools/ParityTests/java-tests.json`。
- 实库：本地跑 SQLite、DuckDB（进程内）和 PG 17；CI 另跑 MySQL 8.4 / 8.0.18、MariaDB、SQL Server、Oracle、达梦、openGauss、HANA、ClickHouse、Db2、TDengine，全部通过。没有实库验证的只剩 Kingbase。

## 待做

- [!] Kingbase 实库冒烟：没有可用的容器镜像（2026-11-13 试过 Docker Hub 上的社区镜像 xuxuclassmate/kingbase:v8r3，内置的试用授权已过期，启动即报 License file expired），要有能用的实例或有效的授权文件。`KingbaseDialect` 的 SQL 文本已按 Java 核对；有了实例后照 openGauss 的做法在 `TestDatabases` 登记、加 `KingbaseLiveSuite`、开 CI 任务。
- [ ] 跟进上游新增的用例：上游 main（3.3.1）的 `sql-test` 已有 2410 个用例，用 `ParityTests --rescan` 对比差异后另定范围。

其余想做但没排期的事记在 [BACKLOG](docs/BACKLOG.md)。

## 已定的取舍

**范围**

- 不做，对应的用例标为不适用：
  - search 扩展（`sql-search`，45 个）：第三方贡献的 URL 参数约定，由前端选运算符；同类需求用 `FilterBy` / `SortBy`。
  - SQL Server 2008 的 ROW_NUMBER 分页（13 个）：SQL Server 2008 已于 2019 年停止支持。
- 暂不做，记在 BACKLOG：文档站、编译期预编译查询（interceptors）、TS 版独有的能力。
- 对标基准冻结在现有快照，不随上游增长；fork 仓库不在本机，fork 的 89 个用例只存在于快照里。

**架构**

- **方言留在核心包里**，只负责渲染 SQL。驱动由用户通过 `DbDataSource` 传入，核心不引用任何驱动。只有用到驱动专属 API（批量复制、PG 数组类型等）时才单独出包。
- **方言分两档**：
  - 第一档，CI 跑全部实库用例：SQLite、MySQL 8.4 / 8.0.18（PolarDB 基线）/ MariaDB、PG 17、SQL Server、Oracle。
  - 第二档，比对 SQL 文本，再加实库冒烟：达梦、HANA、DuckDB、Kingbase / openGauss、ClickHouse、TDengine、Db2。
- **生成器只保留 `[Projection]` 和分析器**。`[MapFrom<T>]` 的映射在运行时构造，编译期校验交给分析器。核心如实标注 `RequiresDynamicCode` / `RequiresUnreferencedCode`。
- **嵌套事务对齐 Java**：已经在事务里时再开事务，直接抛异常。
- **规划外的新公共概念先写决策文档**（`docs/decisions/`）。
- **新依赖只进各自的包**：生成器引用 Microsoft.CodeAnalysis，依赖注入引用 Microsoft.Extensions 的抽象包，缓存引用 HybridCache，核心零依赖。第二档方言的测试驱动只进 TestKit；nuget.org 上没有的（HANA 的 Sap.Data.Hana）在运行时从本机装的客户端加载。
- **集合导航取段之后聚合按 C# 语义**：`string.Join(",", u.Cards.OrderBy(..).Take(2).Select(..))` 先取段再聚合，不照 Java 把 LIMIT 写在聚合外面（Java 拼的是全部行，PG 上还会报错），M8BankTest test7 / test7_1 / test7_2 / test8 记偏离。

**发布**

- nuget.org，Apache-2.0（与 Java 上游相同，`LICENSE` 与 `NOTICE` 在仓库根，NOTICE 随包分发）。推 `v*` tag 由 `.github/workflows/release.yml` 用可信发布自动发布，不存长期密钥、不设人工审批。
- `src/Directory.Build.props` 的 `VersionPrefix` 是下一个要发的版本；CI 打的包是 `-ci.N` 预览号。
- 版本号按语义化版本：新增公开成员（记在 `PublicAPI.Unshipped.txt`）至少升次版本位；改动或删除已发布的成员是破坏性变更，先说明理由。
