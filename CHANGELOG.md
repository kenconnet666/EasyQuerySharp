# 更新日志

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。发布流程（`.github/workflows/release.yml`）取这里对应版本的一节作为 GitHub Release 的说明。

## [Unreleased]

### 修复

- 日期 `ToString(格式)`：MySQL、PostgreSQL、SQLite、DuckDB 等方言原来只转换 `yyyy` `MM` `dd` `HH` `mm` `ss`，`yy`、`M`、`d`、`tt` 等原样当文字输出，结果静默不对。现在各方言补齐能转换的格式符，转换不了的报 `NotSupportedException`；格式串里的文字按方言转义（`%`、TO_CHAR 里的英文字母），支持 .NET 的引号、`\` 转义与 `%` 前缀。

## [1.0.0] - 2026-11-09

首个正式版本：[easy-query](https://github.com/dromara/easy-query)（Java）的 C# 实现，公共 API 自此冻结。

### 包

- `EasyQuerySharp`：核心库，只依赖 BCL；`[Projection]` 生成器与分析器（EQ0001–EQ0007）随包分发。
- `EasyQuerySharp.DependencyInjection`：`services.AddEasyQuery(..)`，多个库按名字（keyed service）注入。
- `EasyQuerySharp.Caching`：二级缓存（按主键与按条件），存储用 HybridCache，经本库的写入自动失效。

### 能力

- 查询：POCO 实体加表达式树；导航属性直接写在条件和投影里，生成隐式 JOIN、隐式子查询；分组、窗口函数、CTE、子查询、联合、动态筛选与列表页（`ToPageAsync(TableRequest)`）。
- 对象树：Include、DTO（`Select<TDto>()`、`[Projection]`）、树查询与聚合保存走同一条投影管线。
- 写入：插入、按实体或按条件更新删除、upsert、批量、追踪差异更新、乐观锁、逻辑删除、加密列、拦截器。
- 平台：code-first 建表与只增不删的同步、表名映射与换表、分库分表、二级缓存、环境事务与行锁、原生 SQL（插值参数化）。
- 方言：SQLite、MySQL 8.0.18+ / 8.4、PolarDB、MariaDB、PostgreSQL、SQL Server、Oracle（第一档）；DuckDB、达梦、SAP HANA、人大金仓、GaussDB / openGauss、ClickHouse、Db2、TDengine（第二档）。

### 验证

- 对标 Java 版的 2404 个用例：已移植 2071、偏离 191、不适用 142，每条偏离与不适用都写了理由（见 [docs/parity-tests.md](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/parity-tests.md)）。
- CI 在 14 种数据库上跑实库测试：SQLite、DuckDB、MySQL 8.4 / 8.0.18、MariaDB、PostgreSQL、SQL Server、Oracle、达梦、openGauss、SAP HANA、ClickHouse、Db2、TDengine。人大金仓没有可用的容器镜像，只比对 SQL 文本。

[Unreleased]: https://github.com/kenconnet666/EasyQuerySharp/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.0.0
