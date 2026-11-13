# BACKLOG

记下来但暂时不做的事，一行一条，写清楚触发条件或理由。正确性 bug 不记在这里，要直接修。

- 同步版本的终结方法（`ToList()` 等）：第一期只做异步。有真实需求时再加，避免 API 面积翻倍。
- `IQueryable<T>` 适配（`AsQueryable()`）：有 OData、AutoMapper `ProjectTo` 这类集成需求时再做。
- 浏览器里运行文档卡片（Blazor WASM + SQLite WASM）：第一版只展示预先算好的结果。
- 和 TS 文档站合并成一个站，同一张卡片切换两种代码：两边的卡片都稳定以后再评估。
- 方言能力做进类型系统（参考 TS 的 `Adapter<C>`）：先用运行时检查加分析器。
- 插值或拼接的字符串传给 `FromSqlRaw` / `SqlQueryRawAsync` / `ExecuteSqlRawAsync` 时编译期警告（EF Core 10 的 EF1002）：现在靠方法名区分两组 API，插值字符串不会落到 Raw 一组；有人误把插值写进 Raw 方法时再加分析器。
- 一条 SQL 用 JSON 聚合出整棵对象树：不做，理由见 [0002](decisions/0002-object-tree-and-dto.md)。
- 标量投影之后再聚合（`q.Select(x => x.Score ?? 0m).SumAsync(v => v)`）：选择器参数绑定到单列派生表还不支持，多表时直接用 `SumAsync((a, b) => …)`；有真实需求时再补。
- 对象树按批流式读取（`AsAsyncEnumerable(batchSize)`）与 `.AsSnapshot()`：没有对应的 Java 用例，有真实需求时再做。
- 文档站（`apps/docs` 读卡片生成的 examples.json，方言切换、结果表格，可复用 TS 文档站的组件）：现在用 `docs/guide` 下的 Markdown 指南，要做站点时再做。
- AOT 与裁剪（旧 1.1 / 9.4 / 10.3）：元数据与物化器的 Source Generator、用 interceptors 在编译期预翻译查询。表达式树查询在 NativeAOT 下只能解释执行；JIT 下一条 10 行查询已与 EF Core 预编译查询相当（见 [基准对比](benchmarks.md)）。等有 AOT 部署需求、且 interceptors 不再标为实验性时再做。
- TS 版独有的能力（旧 M9）：多态（判别列加子类）、只读库 `AsReadOnly()`、开发期 N+1 检测、`dotnet eqs pull` 从库反向生成实体、多个模型程序集注册到同一个库。不在 Java 对标范围内，有真实需求时再做。
- 带出查询的父键在 PG 上写成 `= ANY(?)`，只占一个参数：现在按 `IncludeGroupSize` 分批 IN，够用；基准显示参数绑定是瓶颈时再做。
- 投影里直接写非导航的关联查询（`Select(o => new { o.No, Lines = db.Query<Line>().Where(l => l.OrderNo == o.No).ToList() })`），规划器自动按键批量：现在用 `FillMany` / `FillOne` 表达，Java 的 fill 用例都已覆盖。
- 任意关联条件的导航（TS 的 `r.manyOn(.., (a, b) => b.id.inCsv(a.ids))`）：查询时本表自连接成派生表按主键对上，带出要在内存里求逆；现在只有 `KeyMatch.SelfCsv`（0005）。
- 逗号列表键的带出在 MySQL 上是 `FIND_IN_SET(列,?)`，用不上索引：目标表大时可以改成拆好的 `IN (…)`；现在与 Java 一致。
- SQL Server 的 `Sql.LikeContains` / `LikeStartsWith` / `LikeEndsWith` 遇到含 `%` 或 `_` 的常量值：Java 的 MsSQLLikeSQLFunction 写成 `CHARINDEX(值,列) > 0` 一类，C# 沿用 `LIKE ? ESCAPE '\'`（语义相同、SQL 不同，没有 Java 用例覆盖）；要逐字对齐时再改。
- Java 的带出与 DTO 结构校验：一对一带出时关联值重复报错、`@NavigateFlat` 同一路径既取对象又取它的属性报错、自动带出的关联属性要同时出现在 `@Navigate` 与 `@NavigateFlat` 里（RelationTest#testOneToOne 的三段 Skip）。C# 里这些写法各自独立带出、结果正确，有用户被误用坑到时再加校验。
- 联表查询上投影到 DTO（`Query<T1, T2>.Select<TDto>()`，Java 的 `selectAutoInclude(X.class, (s, t2) -> Select.of(t2.xx().as(..)))`）：现在联表后只能手写投影；RelationTest#testOneToOne 一段 Skip，有真实需求时再做。
- DuckDB 的日期相差用 `DATEDIFF`（与 Java 一致），数的是跨过的边界：03:59 到 04:01 算 1 小时；`(a - b).TotalHours` 在 .NET 里是整段时长（0 小时）。要对齐 .NET 时改用 `date_sub`。
- HANA 的 `Sql.Json(..)` 没有登记（渲染时报不支持）：HANA 2.0 有 `JSON_VALUE` / `JSON_QUERY`，没有数组长度函数；fork 只用 NCLOB 存 JSON、没有 HANA 的 JSON 函数用例，有需求时再补。
- 文档卡片的 SQL 快照（`tools/DocsExamples`）只渲染第一档的 5 个方言加达梦；其余第二档方言（DuckDB 等）的 SQL 文本由 `tests` 里的 `XxxDialectTest` 核对，指南里的方言差异写在各页的"方言差异"表里。第二档的实库冒烟已经做完，要不要进快照等有人按方言查文档时再定（回放的磁带还不支持输出参数，Oracle / 达梦取自增键的卡片标了 Unsupported）。
- 列表页请求的条件树与整天区间（TS 11b.3a：FilterBar 高级筛选的 `condition` JSON，日期时间列上只写日期的区间按整天、按时区算）以及 `filterable` / `sortable` 白名单选项：`ToPageAsync(TableRequest)` 现在只做表头筛选、关键字、排序，白名单就是结果里的列；有前端用到时再做。
- 数据库级的换表（TS 的 `db.mapTables(..)` / `db.inSchema(..)`，一个 `EasyDb` 上所有查询与写入都换）：现在是查询级的 `MapTable` / `InSchema`，写入用 `AsTable` / `AsSchema`；多租户分库要整库切换时再做。
- code-first 读库结构与同步只看当前 schema（MySQL 是当前库）：实体声明了别的 schema 时按表名和当前 schema 里的表比较；要跨 schema 同步时再给各方言的系统表查询加 schema 条件。
- code-first 同步不比较列类型（只比较列在不在、可空性）：各库读出的类型写法不同（`character varying(255)` 与 `VARCHAR(255)`、`int(11)` 与 `INT`），要比较得先按方言归一（TS 版的 `sameType`）；有需求时再做。
- code-first 不建库（Java 的 `createDatabaseIfNotExists`）：库由连接串决定，建库属于部署，要从连接串拆出服务器与库名、各方言写法不同；有需求时再做。
- 分片查询跨单元的平均值：可以改写成各单元的 `SUM` 与 `COUNT` 再合并（Java 的 GroupAvgBehaviorEnum），0006 没做，现在报 `NotSupportedException` 让调用方自己算；有需求时再做，跨单元的分组同理。
- 分片查询的并发上限（Java 的 `maxShardingQueryLimit`）：不在事务里时每个单元各取一个连接并行执行，`ToShardingPageAsync` 按节点计数时一个节点一个连接；连接池吃紧时再加按数据源的并发上限。
- 二级缓存的多实例本机层失效通知（TS 的 CacheTransport、Java 的 Redis 发布订阅）：1.0 靠 `LocalCacheExpiration` 兜底（0007 §8），要更及时时再做，或等带 backplane 的 HybridCache 实现。
- 二级缓存同一实例里同时的未命中各查一次库：HybridCache 的工厂里不能查库（AsyncLocal 不流动，见 0007 末尾），要合并时自己按键做单飞；小表的 all 一般用不上。
- 依赖注入包自动接上容器里的 `IWriteListener`（像 `IExecutionListener` 那样）：现在在配置函数里写 `o.UseCache(sp.GetRequiredService<EasyCache>())`；有第二种写入监听器的需求时再加。
