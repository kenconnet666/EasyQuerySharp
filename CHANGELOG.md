# 更新日志

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。发布流程（`.github/workflows/release.yml`）取这里对应版本的一节作为 GitHub Release 的说明。

## [Unreleased]

## [1.2.1] - 2026-11-14

### 修复

- SQLite 上的日期加减（`AddDays` 等）与转成 `DateTime` 丢掉毫秒，`AddMilliseconds` 不到一秒的部分整个丢掉，带毫秒的值加减后与参数比较对不上；现在保留毫秒，文本与 Microsoft.Data.Sqlite 存的一致。

## [1.2.0] - 2026-11-14

### 新增

- 终结方法 `AnyAsync` / `CountAsync` / `FirstAsync` / `FirstOrDefaultAsync` / `SingleAsync` / `SingleOrDefaultAsync` 可以直接带条件；新增 `AllAsync(条件)`、`ToArrayAsync()`、`ToDictionaryAsync(key)` / `ToDictionaryAsync(key, value)`。
- 查询表达式里可以写 `Math.Max` / `Min` / `Clamp`、`d.Date`、`DateOnly.FromDateTime(d)`、可空值的 `GetValueOrDefault()` / `GetValueOrDefault(v)`。
- 集合导航上可以写 `List` 的 `Exists(..)`、`Select(..).Contains(v)`、`MaxBy` / `MinBy`、排序后的 `Last()` / `LastOrDefault()`。

### 变更

- 两个整数相除与 C# 一样向零截断：MySQL 写 `DIV`，Oracle、达梦、DuckDB、openGauss、Kingbase 写 `TRUNC(a / b)`，HANA、ClickHouse、TDengine 减掉余数再除；原来这些库上 `/` 得到小数。PG、SQL Server、SQLite、Db2 的 SQL 不变。要小数结果时先转换一边（`(decimal)a / b`）。
- 条件按 C# 的语义处理 NULL：可空列的 `!=`、取反（`!(a > b)`、`== false`）包含值为 NULL 的行，两边都可空的 `==` 在都是 NULL 时成立，`Contains` 的值列表里的 null 匹配 NULL（原来 `NOT IN` 遇到 null 一行都不剩），集合导航的 `All(p)` 把 `p` 是 NULL 的行算作不满足。新增 `EasyDbOptions.UseRelationalNulls`，设为 true 时按 SQL 原样比较（与 Java 版一致）。
- 字符串拼接与 C# 一样把 null 当空串：MySQL、HANA、Db2、ClickHouse、TDengine 上可能为 NULL 的项包一层 `COALESCE(x, '')`（原来整个结果是 NULL）；可空值没有值时 `ToString()` 是空串。
- 聚合没有行时与 C# 一致：`SumAsync` 得 0（原来结果类型可空时是 null），`MaxAsync` / `MinAsync` 的结果类型不可空时抛 `InvalidOperationException`（原来是 0）；分组里可空值的 `Sum` 全是 NULL 时得 0；集合导航上可空的 `Average` 没有行时是 null（原来是 0）。`UseRelationalNulls` 时保持原来的行为。

### 修复

- 带 `StringComparison` 的 `Contains` / `StartsWith` / `EndsWith` / `string.Equals` 原来丢掉比较方式写成普通的 `LIKE` / `=`（大小写是否敏感取决于库的排序规则，PG 上忽略大小写的比较查不到）；忽略大小写时现在两边转小写再比较。`string.Equals(a, b, StringComparison)` 与 `object.Equals(a, b)` 原来报不支持。
- 枚举的 `ToString()` 原来翻译成数字（按数值存）或库里的文字（按名字存、`[EnumMember]` 改过名字时与成员名不同），现在与 C# 一样是成员名；可空枚举没有值时是空串。
- `Math.Round(x, MidpointRounding)` 把舍入方式当成小数位数写进 `ROUND`，结果不对；现在报不支持。
- SQLite 上的 `Math.Truncate(x)` 写成 SQLite 没有的两参数 `TRUNC(x,0)`，现在写 `trunc(x)`。
- TDengine 上的 `??`、`GetValueOrDefault` 写成 3.3 没有的 `COALESCE`，现在写 `CASE WHEN x IS NOT NULL THEN x ELSE y END`。

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

[Unreleased]: https://github.com/kenconnet666/EasyQuerySharp/compare/v1.2.1...HEAD
[1.2.1]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.2.1
[1.2.0]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.2.0
[1.1.0]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.1.0
[1.0.0]: https://github.com/kenconnet666/EasyQuerySharp/releases/tag/v1.0.0
