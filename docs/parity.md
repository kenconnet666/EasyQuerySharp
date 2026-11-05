# 能力对照总表

本表按能力列出 easy-query（Java）的全部能力、计划的 C# 写法，以及排在哪个里程碑。逐条用例的对照见 `docs/parity-tests.md`，它由 `tools/ParityTests` 生成，M0 完成后才会有。

- **用例数**：上游 `sql-test` 共 2315 个用例。分类规则沿用 TS 版的 `tools/capabilities.mjs`：每个用例按方法体里用到的 API 归到一种能力。fork 的 `sql-fork-test` 还有 89 个用例，单独列在最后。
- **状态**：现在都是"待做"，各能力完成后由 `ParityTests` 改写成实际状态。
- **M 列**：所属里程碑，见 [ROADMAP](../ROADMAP.md)。

## 查询

| 能力 | key | 用例数 | C# 写法 | M |
|---|---|---|---|---|
| 基本查询：条件、排序、选列、分页 | `query.basic` | 402 | `db.Query<T>().Where(..).OrderBy(..).Select(..).Skip(n).Take(n).ToListAsync()`；`FirstAsync` / `SingleAsync` / `FindAsync(id)` / `AnyAsync` / `CountAsync`；`WhereIf` ；值过滤 `UseValueFilter(ValueFilter.NotNullOrBlank)`（对应 filterConfigure；`includeNavigations: true` 对应 DEFAULT_PROPAGATION_SUPPORTS，也作用于导航与子查询里的条件；与字面量 null 的 `==` / `!=` 是判空，不过滤）；投影里读出后在客户端换算 `Sql.ClientSide(x, v => ..)`（对应 valueConvert）；IN 列表按 `EasyDbOptions.MaxInClauseSize` 拆成 OR 组 | M1 |
| 分页结果 | `query.page` | 22 | `ToPageAsync(page, size)` 返回 `Page<T> { Rows, Total, PageIndex, PageSize }`；最后一页只取剩余条数；`EasyDbOptions.ReverseOffsetThreshold` 开启深分页反向排序（偏移过半时反转 ORDER BY 取数再倒序），单条查询 `DisableReversePaging()` 关闭 | M1 |
| 原生 SQL | `sql.native` | 50 | `db.SqlQueryAsync<T>($"...{param}")` / `db.ExecuteSqlAsync($"...")`，插值的值自动参数化，集合展开成 `?,?,?`（空集合报错），`Sql.Identifier("t_user")` 按方言加引号；行内片段用 `Sql.Raw<T>("... {0} ...", u.Col)`；自定义模板函数：静态方法标 `[SqlFunction("FIND_IN_SET({0},{1})")]`，在表达式里直接调用 | M1 |
| 显式 join | `query.join` | 88 | `.LeftJoin<Post>((u, p) => p.UserId == u.Id)`，后续 lambda 依次收到各表 `(u, p)`；right join 之后，前面表的列按可空处理 | M3 |
| 分组与 having | `query.group` | 78 | `.GroupBy(p => new { p.UserId }).Having(g => g.Count() > 3).Select(g => new { g.Key.UserId, Total = g.Sum(p => p.Amount) })`；按条件计数 `g.Count(p => p.Paid)`（COUNT(CASE WHEN …)）；`Rollup` / `Cube` / `Grouping()` | M3 |
| union / union all | `query.union` | 18 | `q1.Union(q2)` / `UnionAll` | M3 |
| CTE | `query.cte` | 11 | `q.AsCte("x")`（对应 toCteAs，默认名 `with_类型名`）：之后可以 Where / Select，或作为 JOIN 来源；同名定义只写一次，写在 `WITH .. AS (..) ,.. AS (..)` 里；递归 CTE 随树（4.10）实现 | M3 |
| 无实体的查询、toMap | `query.map` | 11 | `db.Sql<Dictionary<string, object?>>(...)`；投影成 Map：`Select(o => new Dictionary<string, object?> { { "id", o.Id } })`（逐键，总写 AS）、`Select<Dictionary<string, object?>>()`（全部列）、`new Dictionary<string, object?>()`（`SELECT *`）、`new Dictionary<string, object?>(Sql.Columns(b)) { { "xx", .. } }` / `Sql.ColumnsExcept(b, b.CreateTime)`（全部列再追加键）；`ToDictionaryAsync` | M3 |
| 流式读取 | `query.stream` | 9 | `await foreach (var x in q.AsAsyncEnumerable())`；按偏移分批 `q.OffsetChunkAsync(size, chunk => 前进行数, maxFetchSize)`（对应 offsetChunk） | M3 |
| 复用查询 | `query.reuse` | 2 | 查询对象不可变，可以直接复用 | M3 |

## 表达式与函数

| 能力 | key | 用例数 | C# 写法 | M |
|---|---|---|---|---|
| 字符串函数 | `func.string` | 128 | 优先映射 BCL：`Contains` / `StartsWith` / `Trim` / `ToUpper` / `Substring` / `IndexOf` / `Length` / `Replace` / `PadLeft`；没有 BCL 对应的用扩展方法：`Like` / `NotLike` / `Concat`；`Contains` 等对应 Java 的 like（`LIKE ?`，% 在参数里），`LikeContains` / `LikeStartsWith` / `LikeEndsWith` 对应 Java 的 contains / startsWith / endsWith（`LIKE CONCAT('%',?,'%')`） | M2 |
| 日期函数 | `func.date` | 96 | `AddDays` / `AddMonths` / `.Year` / `.DayOfWeek` / `ToString("yyyy-MM")`（格式串按方言翻译）/ `Sql.Now()` / `Sql.DateDiff`；`DateTime` / `DateOnly` / `DateTimeOffset` | M2 |
| 通用函数 | `func.general` | 53 | `??`（nullOrDefault）/ `Sql.Greatest` / `Sql.Least` / `Sql.NullIf` / `Sql.EqualsNullSafe` / `Sql.Random()`（RAND()、PG / SQLite 的 RANDOM()、Oracle 的 DBMS_RANDOM.VALUE()） | M2 |
| 字符串聚合 | `func.joining` | 29 | `g.StringJoin(p => p.Name, ",")`，集合导航上同样可用：`u.Posts.StringJoin(...)` | M2 |
| JSON 函数 | `func.json` | 12 | `Sql.Json(u.Attrs).Value<string>("$.a")`；方言专属的放在 `MySql.Json*` / `Pg.Jsonb*` 下 | M2 |
| 数学函数 | `func.math` | 4 | 映射 `Math.Abs` / `Round` / `Floor` / `Ceiling` / `Pow` / `Sqrt` / `Sign` / `Log` / `Exp` / `Truncate` | M2 |
| CASE WHEN | `expr.case` | 27 | 三元表达式；多分支用 `Sql.Case().When(..).Then(..).Else(..)`；聚合加过滤：`g.Count(p => p.Paid)` | M2 |
| 窗口函数 | `window` | 38 | `Sql.Over.RowNumber(w => w.PartitionBy(p => p.UserId).OrderBy(p => p.Id))`；`Lag` / `Lead` / `FirstValue` / `NthValue` / `Ntile`，支持 frame | M2 |
| 行锁 | `lock` | 27 | `.ForUpdate()` / `.ForUpdateNoWait()` / `.ForUpdateSkipLocked()`：只能在事务里、对单表调用，锁写在最外层（计数、Any、分页同样带上）；SQL Server、SQLite 渲染时抛出 NotSupportedException。Java 没有 FOR SHARE，不做 | M2 |

## 导航与对象树（[0002](decisions/0002-object-tree-and-dto.md)）

| 能力 | key | 用例数 | C# 写法 | M |
|---|---|---|---|---|
| 隐式子查询 any / none / count / sum … | `nav.subquery` | 142 | `u.Posts.Any(p => ..)` / `!u.Posts.Any(..)` / `u.Posts.Count(..)` / `u.Posts.Sum(p => p.Amount)` / `u.Posts.Where(..).Max(..)`；导航上的固定条件 `[Navigate(.., ExtraFilter = nameof(静态属性), ExtraMappingFilter = ..)]`（属性每次查询重新取值，返回 null 即不加），子查询里写成 `(关联 AND 固定条件)`；`u.Posts.DisableLogicDelete().Any(..)` 关闭这个子查询的逻辑删除 | M4 |
| 子查询改成分组 join | `nav.groupJoin` | 67 | `Query.GroupJoin(u => u.Posts, q => …)` 或导航上 `[Navigate(GroupJoin = true)]`；`GroupJoinAll()` 对应 ALL_SUB_QUERY_GROUP_JOIN。同一导航上的多个聚合共用一次分组 join，各聚合共有的条件提到派生表的 WHERE（`GroupJoinNoMerge()` 关闭）；多对多、嵌套（`q => q.GroupJoin(r => r.Menus)`）、Required 导航（INNER JOIN、不包 IFNULL） | M4 |
| 一对多展开 flatElement / elements | `nav.flat` | 56 | `flatElement()` 写成 `Any(..)`；`elements(i, j)` 写成 `OrderBy(..).Skip(i).Take(j - i + 1)`：聚合时 ORDER BY / LIMIT 留在子查询里，之后再加条件或 `Any` 时先把这段行包成派生表；分组合并（`GroupJoin`）时按 `ROW_NUMBER()` 编组内行号再取段，同样取法的聚合合并到同一张派生表。取第 i 个元素用 `ElementAt(i)` / `First()`（多对多经中间表编号）；链上没写排序时按导航的 `PartitionOrder`：主键（默认）、`None`（Java 的 IGNORE）、`Navigation`（按 `[Navigate(OrderBy = "OpenTime, Code desc nulls first")]`，Java 的 orderByProps + NAVIGATE） | M4 |
| include | `nav.include` | 37 | `.Include(o => o.Lines, q => q.Where(..).Include(l => l.Product))` | M4 |
| 按 DTO 结构自动带出 | `dto.autoInclude` | 35 | `[MapFrom<User>] partial record UserVo(..)` 加 `Select<UserVo>()`，编译期生成映射 | M4 |
| 按键填充 fillMany / fillOne | `fill` | 4 | 投影里写 `db.Query<OrderLine>().Where(l => l.OrderNo == o.No).ToList()`（不是导航的关联），规划器把它拆成按键批量查询 | M4 |
| 树 toTreeList / asTreeCTE | `tree` | 25 | `ToTreeAsync(m => m.Id, m => m.ParentId)` / `DescendantsOf(id)` / `AncestorsOf(id)` | M4 |

## 写入

| 能力 | key | 用例数 | C# 写法 | M |
|---|---|---|---|---|
| 插入 | `write.insert` | 44 | `db.Insert(entity).ExecuteAsync()` / `.ReturnIdAsync()` / `.ReturnEntityAsync()`；`db.Insert<T>().FromQuery(q)`（insert-select） | M1 |
| 更新 | `write.update` | 50 | `db.Update<T>().Set(u => new T { Age = u.Age + 1 }).Where(..).ExecuteAsync()`；`db.Update(entity)` 按主键整行更新（不写主键、版本列和生效的逻辑删除列）；`.SetColumns(t => new { t.Title, t.Stars })` 限定列并按这个顺序写，`.WhereColumns(..)` 换定位列；没有任何 SET 时抛出异常（Java 是静默返回 0）| M1 |
| 删除 | `write.delete` | 21 | `db.Delete<T>().Where(..).ExecuteAsync()`；没有条件时必须写 `.All()`；逻辑删除的实体要真删时用 `.Hard()`；条件里可以用集合导航（翻译成关联子查询，表带别名）；对一导航生成 JOIN（见 `write.join`） | M1 |
| upsert | `write.upsert` | 13 | `.OnConflict(u => u.Id).Merge(u => new { u.Name })` / `.Ignore()`，按方言渲染成 `ON DUPLICATE KEY` / `ON CONFLICT` / `MERGE` | M5 |
| 写哪些列、批量执行 | `write.strategy` | 19 | `.Columns(ColumnStrategy.NotNull)`；`.Batch()` 用驱动的批处理（`DbBatch`） | M5 |
| UPDATE / DELETE 里用导航 | `write.join` | 10 | `db.Update<Card>().Set(c => new Card { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("x"))`：SET 或条件里的对一导航生成 INNER JOIN（先翻译 SET 再翻译条件，别名按出现顺序）。MySQL：`UPDATE t INNER JOIN … SET` / `DELETE t FROM … JOIN`；PostgreSQL：`UPDATE … SET … FROM … WHERE` / `DELETE FROM … USING`；其余方言暂时抛出 NotSupportedException | M5 |
| 追踪与差异更新 | `tracking` | 62 | `q.AsTracking()` 加 `db.Update(entity)` 只更新改过的列；不追踪时用 `db.Update<T>().Changes(before, after)` | M5 |
| 聚合根保存 | `save.aggregate` | 39 | `db.Save(root).Cascade(o => o.Lines).ExecuteAsync()` | M5 |

## 列规则与扩展

| 能力 | key | 用例数 | C# 写法 | M |
|---|---|---|---|---|
| 拦截器 | `interceptor` | 3 | `QueryFilter<T>`（查询、更新、删除自动加条件，返回 null 这次不加）/ `EntityInterceptor<T>`（`BeforeInsert` / `BeforeUpdate` 改实体，`BeforeConditionUpdate` 给按条件更新追加 SET，用户已 Set 的列跳过）；`T` 可以是基类或接口；单条语句上 `.DisableInterceptor("名字")` / `.EnableInterceptor("名字")`，同名的拦截器一起开关；按实体更新限定了列时 `BeforeEntityUpdateColumns` 可以补列 | M5 |
| 逻辑删除 | `logicDelete` | 10 | `[LogicDelete(LogicDeleteStrategy.Boolean)]` / `DeletedTime`；自定义策略实现 `ILogicDeleteStrategy`（未删除条件 + 删除时写的列），`o.AddLogicDeleteStrategy("名字", 策略)` 注册后写 `[LogicDelete(LogicDeleteStrategy.Custom, StrategyName = "名字")]`。查询自动加条件，删除自动改成更新，按实体更新不写生效的逻辑删除列 | M5 |
| 乐观锁 | `version` | 13 | `[Version]`（整数列 +1；`[Version(VersionStrategy.NewGuid)]` 写新 Guid）。条件里加 `version = 当前版本`、SET 末尾写下一个版本；按实体写入取实体上的值并在成功后写回，按条件写入必须 `.WithVersion(v)` 或 `.IgnoreVersion()`。冲突时影响 0 行，用 `ExecuteAsync(1, "msg")` 抛 `ConcurrencyException`（与 Java 一样不自动抛） | M5 |
| 列加密 | `column.encryption` | 8 | `[Encryption(typeof(AesEncryption), SupportLike = true)]`，与 Java 版的密文兼容 | M5 |
| 值转换、枚举 | `column.converter` | 0 | `[Column(Converter = typeof(JsonConverter<Attrs>))]`；枚举默认按数字存，可以改成按名字 | M5 |
| 自增、生成列 | `column.generated` | 0 | `[Column(Generated = true)]` / 主键生成器 / 数据库计算列 | M1 |

用例数为 0 的两项，是因为 TS 的分类规则里，对应 Java 的目录在 `sql-test` 下没有用例，能力本身仍要做。

## 表与平台

| 能力 | key | 用例数 | C# 写法 | M |
|---|---|---|---|---|
| 建表与迁移（code-first） | `codeFirst` | 17 | `db.Schema.CreateTablesAsync<..>()` / `SyncAsync()`（只增不删，改名用 `RenamedFrom`）/ `ToSql()` | M7 |
| 换表名 asTable | `table.rename` | 45 | 单条查询里映射表名：`.MapTable<Post>("t_post_2025")`，导航、隐式 join、include 都跟着变（对应 fork 的 TableNameMapping） | M7 |
| 换 schema | `table.schema` | 19 | `.InSchema("tenant_a")` | M7 |
| 跨库链接 | `table.link` | 13 | `.WithTableLink("link")` | M7 |
| 分库分表 | `sharding` | 48 | `[ShardingTableKey]` 加路由基类；分表按取模或时间路由；分库时在内存里归并排序和分页 | M7 |
| 按对象筛选排序 | `dto.object` | 101 | `.FilterBy(request, rules)` / `.SortBy(sort, allowList)`（`SortBuilder.OrderBy(path, asc, nulls: NullsOrder.Last)`）/ `.Table(request)`；按路径动态取列 `Sql.Column<T>(c, "user.age")`（对应 anyColumn） | M7 |
| search 扩展 | `search` | 45 | `EasyQuerySharp.Search`：按 Java 的参数约定解析请求，交给 `Table()` | M7 |

## 方言

| 能力 | key | 用例数 | C# 驱动 | M |
|---|---|---|---|---|
| MySQL（8.0.18 / 8.4、PolarDB） | 归在上面各项 | — | MySqlConnector | M1 |
| SQLite | `dialect.sqlite`（归在 basic 里） | — | Microsoft.Data.Sqlite | M1 |
| PostgreSQL 17 | 归在上面各项（Java `pgsql/` 目录约 100 个） | — | Npgsql。本地必测，所以和 MySQL 一起做 | M1 |
| SQL Server | `dialect.mssql` | 38 | Microsoft.Data.SqlClient。函数的 SQL 渲染在 M2 做，实库在 M6 | M6 |
| SQL Server 2008（ROW_NUMBER 分页） | `dialect.mssql2008` | 13 | 同上，按版本渲染 | M6 |
| 达梦 | `dialect.dameng` | 51 | DM.DmProvider；方言继承 Oracle | M6 |
| 人大金仓 | `dialect.kingbase` | 2 | PG 模式与 PG 共用一个方言 | M6 |
| DuckDB | `dialect.duckdb` | 16 | DuckDB.NET | M6 |
| ClickHouse | `dialect.clickhouse` | 4 | ClickHouse.Client | M6 |
| H2 | `dialect.h2` | 74 | 没有 .NET 驱动。和方言无关的用例（树、CTE、基本类型）改在 SQLite 上移植，标为偏离；只测 H2 方言本身的用例标为不适用 | M6 |
| Oracle / DB2 / HANA / TDengine / openGauss | 归在上面各项 | — | Oracle.ManagedDataAccess.Core / Net.IBM.Data.Db2 / HANA ADO.NET / TDengine.Connector / Npgsql | M6 |

## fork 新增（`sql-fork-test`，89 个用例）

| 能力 | 来源 | C# 写法 | M |
|---|---|---|---|
| 方言静态入口加专属函数：MySQL JSON / 全文，PG jsonb / 数组 / 全文，HANA 全文 | fork decisions/0001、M1、M4 | `MySql.JsonContains(..)` / `MySql.Match(..).Against(..)` / `Pg.Jsonb(..).Contains(..)` / `Pg.Array(..).HasAny(..)` / `Hana.Text(..).Fuzzy(..)` | M6 |
| 窗口 frame、`ntile` / `percent_rank` / `cume_dist` | fork 3.4 | 并入 `window` | M2 |
| rollup / cube / grouping | fork 3.5 | 并入 `query.group` | M3 |
| NULLIF、null-safe 等值 | fork 3.6 | 并入 `func.general` | M2 |
| 日期截断 dateTrunc | fork 3.7 | `Sql.DateTrunc(DatePart.Month, p.CreatedAt)` | M2 |
| UPDATE / DELETE … RETURNING | fork 3.8 | `.Returning()`；方言不支持时渲染期报错 | M6 |
| forUpdate NoWait / SkipLocked | fork 3.9 | 并入 `lock` | M2 |
| 查询级表名映射 | fork 3.1、decisions/0003 | 并入 `table.rename` | M7 |
| 类型化 INSERT … SELECT | fork 3.2 | 并入 `write.insert` | M5 |
| JSON 列转换器 + 编解码接口 | fork 3.3 | 默认用 `System.Text.Json`，`[Column(Converter = typeof(JsonColumn<T>))]` | M5 |
| `DateTimeOffset` 等带时区的时间类型 | fork 1.4 | 原生支持；UTC 语义可以配置 | M1 |
| SAP HANA 方言 | fork M2 | `EasyQuerySharp.Hana` | M6 |
| 实库用例（live） | fork `live/` | 并入 L3 跨方言套件 | M6 |

## TS 版独有的能力（不在 Java 用例里，按需做）

| 能力 | TS 写法 | C# 计划 | M |
|---|---|---|---|
| 多态（判别联合） | `entity.union` | 判别列加子类：`[Discriminator("kind")]` + `abstract record Payment` | M9 |
| 值对象 | `embed()` | `[ValueObject]` 加 `[ComplexType]` 风格的扁平列（Java 也有 `@ValueObject`，并入 M5） | M5 |
| 计算属性 | `defineComputed` | `[Computed(nameof(Expr))]` 指向一个静态表达式 | M9 |
| 只读库 | `db.readonly()` | `db.AsReadOnly()`：写入直接抛错 | M9 |
| N+1 检测、OTel | observe | `ActivitySource`，开发期检测 N+1 并告警 | M9 |
| 预编译查询 | precompiled | 先用运行时缓存；之后用 interceptors 在编译期翻译 | M9 |
| 合并多个模型 | `mergeSchemas` / `db.mount` | 多个模型程序集注册到同一个 `EasyDb` | M9 |
| 从库反向生成实体 | `eqts pull` | `dotnet eqs pull`（dotnet tool） | M9 |
