# BACKLOG

记下来但暂时不做的事，一行一条，写清楚触发条件或理由。正确性 bug 不记在这里，要直接修。

- 同步版本的终结方法（`ToList()` 等）：第一期只做异步。有真实需求时再加，避免 API 面积翻倍。
- `IQueryable<T>` 适配（`AsQueryable()`）：有 OData、AutoMapper `ProjectTo` 这类集成需求时再做。
- 浏览器里运行文档卡片（Blazor WASM + SQLite WASM）：第一版只展示预先算好的结果。
- 和 TS 文档站合并成一个站，同一张卡片切换两种代码：两边的卡片都稳定以后再评估。
- 方言能力做进类型系统（参考 TS 的 `Adapter<C>`）：先用运行时检查加分析器。
- 一条 SQL 用 JSON 聚合出整棵对象树：不做，理由见 [0002](decisions/0002-object-tree-and-dto.md)。
- 标量投影之后再聚合（`q.Select(x => x.Score ?? 0m).SumAsync(v => v)`）：选择器参数绑定到单列派生表还不支持，多表时直接用 `SumAsync((a, b) => …)`；有真实需求时再补。
- 对象树按批流式读取（`AsAsyncEnumerable(batchSize)`）与 `.AsSnapshot()`（旧 ROADMAP 4.11）：没有对应的 Java 用例，有真实需求时再做。
- 文档站（旧 M8：`apps/docs` 读 examples.json，方言切换、结果表格，约 500 张卡片）：1.0 先用 `docs/guide` 下的 Markdown 指南，要对外发布时再做站点。
- AOT 与裁剪（旧 1.1 / 9.4 / 10.3）：元数据与物化器的 Source Generator、用 interceptors 在编译期预翻译查询。表达式树查询在 NativeAOT 下只能解释执行，物化已接近手写 ADO.NET（读 100 行 1.2 ms，手写 0.95 ms）；等有 AOT 部署需求、且 interceptors 不再标为实验性时再做。
- TS 版独有的能力（旧 M9）：多态（判别列加子类）、只读库 `AsReadOnly()`、开发期 N+1 检测、`dotnet eqs pull` 从库反向生成实体、多个模型程序集注册到同一个库。不在 Java 对标范围内，有真实需求时再做。
- 跟进上游新增的用例：对标基准冻结在 `sql-test` 2315 个（上游 main 3.3.1 已到 2410 个）。1.0 之后另起工作项，用 `ParityTests --rescan` 对比差异。
- 带出查询的父键在 PG 上写成 `= ANY(?)`，只占一个参数（旧 ROADMAP 4.4）：现在按 `IncludeGroupSize` 分批 IN，够用；基准显示参数绑定是瓶颈时再做。
- 投影里直接写非导航的关联查询（`Select(o => new { o.No, Lines = db.Query<Line>().Where(l => l.OrderNo == o.No).ToList() })`），规划器自动按键批量（旧 ROADMAP 4.9）：现在用 `FillMany` / `FillOne` 表达，Java 的 fill 用例都已覆盖。
- 任意关联条件的导航（TS 的 `r.manyOn(.., (a, b) => b.id.inCsv(a.ids))`）：查询时本表自连接成派生表按主键对上，带出要在内存里求逆；现在只有 `KeyMatch.SelfCsv`（0005）。
- 逗号列表键的带出在 MySQL 上是 `FIND_IN_SET(列,?)`，用不上索引：目标表大时可以改成拆好的 `IN (…)`；现在与 Java 一致。
- SQL Server 的 `Sql.LikeContains` / `LikeStartsWith` / `LikeEndsWith` 遇到含 `%` 或 `_` 的常量值：Java 的 MsSQLLikeSQLFunction 写成 `CHARINDEX(值,列) > 0` 一类，C# 沿用 `LIKE ? ESCAPE '\'`（语义相同、SQL 不同，没有 Java 用例覆盖）；要逐字对齐时再改。
- Java 的带出与 DTO 结构校验：一对一带出时关联值重复报错、`@NavigateFlat` 同一路径既取对象又取它的属性报错、自动带出的关联属性要同时出现在 `@Navigate` 与 `@NavigateFlat` 里（RelationTest#testOneToOne 的三段 Skip）。C# 里这些写法各自独立带出、结果正确，有用户被误用坑到时再加校验。
- 联表查询上投影到 DTO（`Query<T1, T2>.Select<TDto>()`，Java 的 `selectAutoInclude(X.class, (s, t2) -> Select.of(t2.xx().as(..)))`）：现在联表后只能手写投影；RelationTest#testOneToOne 一段 Skip，有真实需求时再做。
- 日期 `ToString(format)` 只转换 `yyyy` / `MM` / `dd` / `HH` / `mm` / `ss` 六个格式符，`yy`、`M`、`d`、`H`、`h`、`m`、`s`、`tt` 等单字母格式符在各方言里都原样当文字输出（Java 的 DuckDB、MySQL 等方言有对应的转换）：结果静默不对，要补的话各方言一起补。
- DuckDB 的日期相差用 `DATEDIFF`（与 Java 一致），数的是跨过的边界：03:59 到 04:01 算 1 小时；`(a - b).TotalHours` 在 .NET 里是整段时长（0 小时）。要对齐 .NET 时改用 `date_sub`。
- HANA 的 `Sql.Json(..)` 没有登记（渲染时报不支持）：HANA 2.0 有 `JSON_VALUE` / `JSON_QUERY`，没有数组长度函数；fork 只用 NCLOB 存 JSON、没有 HANA 的 JSON 函数用例，有需求时再补。
- Oracle、HANA 没有 `%` 运算符：C# 的 `a % b` 各方言都写成 `(a % b)`，在这两个库上执行时报语法错误；要支持时由方言改写成 `MOD(a,b)`。
- 文档卡片的 SQL 快照（`tools/DocsExamples`）只渲染第一档的 5 个方言；第二档方言（DuckDB 等）要不要进快照，到阶段 5 写指南时再定。
