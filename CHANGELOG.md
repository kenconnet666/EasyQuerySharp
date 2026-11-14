# 更新日志

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。发布流程（`.github/workflows/release.yml`）取这里对应版本的一节作为 GitHub Release 的说明。

## [Unreleased]

## [1.1.0] - 2026-11-14

### 新增

- 支持裁剪与 Native AOT（核心、依赖注入、二级缓存三个包），见[裁剪与 Native AOT](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/guide/native-aot.md)：核心包自带的生成器保留实体、DTO、值对象的成员；`EasyDb`、`AddEasyQuery`、`EasyCache` 不再标 `RequiresUnreferencedCode` / `RequiresDynamicCode`。
- `JsonValueConverter<T>` 的受保护构造函数接受源生成的 `JsonTypeInfo<T>`；二级缓存的 `EasyCacheOptions.Json` 可以给源生成的 `JsonSerializerContext`。
- `db.TransactionAsync<T>(async () => …)`：回调式事务返回回调的结果。

### 改进

- 物化按列类型直接读（`GetInt32`、`GetString` 等），实体与 DTO 不再逐列装箱：读 1000 行的分配减少 20%–30%。

### 修复

- 带出按关联键把子对象挂回父对象时，从结果集读出的键先转成属性类型再比较：`LoadIncludeAsync` 在 SQLite（Guid 存成文本）上挂不上子对象，字节数组键（MySQL 的 BINARY）按引用比较、永远对不上。
- `await using var tx = db.BeginTransaction()` 释放时回滚失败（连接已断）不再盖住原来的异常；分库时一个数据源回滚失败，其余数据源照样回滚、连接照样释放。
- 列表请求（`ToPageAsync(TableRequest)`）的筛选、排序路径不能再顺着导航走到别的表（如实体查询上的 `filter.company.name`），只能走进值对象，与文档的白名单承诺一致。
- HANA、SQL Server：带隔离级别的事务（`BeginTransaction(IsolationLevel.Serializable)`）结束后，连接带着这个级别回到池里，下一个借到它的语句跟着用（两个驱动改的都是会话级别、归还时不复原）；现在事务结束后改回 READ COMMITTED（`SqlDialect.ResetIsolationSql`）。
- Oracle / 达梦的日期格式里多个不补零的格式符（`M/d`）写成连续的 `FM`，`FM` 是开关，后一个又补上了零；现在只在需要切换时写。
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

[Unreleased]: https://github.com/kenconnet666/EasyQuerySharp/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.1.0
[1.0.0]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.0.0
