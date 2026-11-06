# 能力对照总表

本表按能力列出 easy-query（Java）的全部能力和 C# 的写法。逐条用例的对照见 [parity-tests.md](parity-tests.md)，由 `tools/ParityTests` 生成。

- **用例数**：上游 `sql-test` 共 2315 个用例，分类规则沿用 TS 版的 `tools/capabilities.mjs`，每个用例按方法体里用到的 API 归到一种能力。fork 的 `sql-fork-test` 还有 89 个用例，单独列在最后。基准冻结在 `tools/ParityTests/java-tests.json`。
- **C# 写法**：已经做好的写的是实际 API；还没做的标"（计划）"。
- **状态**：`完成` 表示这个能力本身已经做好（个别依赖方言或别的阶段的用例可能还没移植，见 parity-tests.md）；`阶段 N` 是 [ROADMAP](../ROADMAP.md) 里剩余工作所在的阶段；`不做` 与 `BACKLOG` 的理由见 ROADMAP 的"已定的取舍"和 [BACKLOG](BACKLOG.md)。

## 查询

| 能力 | key | 用例数 | C# 写法 | 状态 |
|---|---|---|---|---|
| 基本查询：条件、排序、选列、分页 | `query.basic` | 435 | `db.Query<T>().Where(..).OrderBy(..).Select(..).Skip(n).Take(n).ToListAsync()`；`FirstAsync` / `SingleAsync` / `FindAsync(id)` / `AnyAsync` / `CountAsync`；要求有结果：`FirstAsync` / `SingleAsync` / `FindOrThrowAsync(id)` / `EnsureAnyAsync` 查不到时抛 `NotFoundException`（可传消息或异常工厂，默认消息取 `[Table(NotFoundMessage = ..)]`，对应 firstNotNull / singleNotNull / findNotNull / required 与 @EasyAssertMessage）；`WhereIf`；值过滤 `UseValueFilter(ValueFilter.NotNullOrBlank)`（对应 filterConfigure；`includeNavigations: true` 对应 DEFAULT_PROPAGATION_SUPPORTS）；投影里读出后在客户端换算 `Sql.ClientSide(x, v => ..)`（对应 valueConvert）；IN 列表按 `EasyDbOptions.MaxInClauseSize` 拆成 OR 组；实体去掉几列 `Sql.Without(u, u.Password)`（selectIgnores） | 阶段 1 |
| 分页结果 | `query.page` | 22 | `ToPageAsync(page, size)` 返回 `Page<T> { Rows, Total, PageIndex, PageSize }`；`ToPageAsync(page, size, selector)` 先分页再投影；`EasyDbOptions.ReverseOffsetThreshold` 开启深分页反向排序，单条查询 `DisableReversePaging()` 关闭 | 阶段 1 |
| 原生 SQL | `sql.native` | 50 | `db.SqlQueryAsync<T>($"...{param}")` / `db.ExecuteSqlAsync($"...")`，插值的值自动参数化，集合展开成 `?,?,?`（空集合报错），`Sql.Identifier("t_user")` 按方言加引号；可以继续组合的原生 SQL 来源 `db.FromSql<T>(sql, params)`；行内片段 `Sql.Raw<T>("... {0} ...", u.Col)`；自定义模板函数：静态方法标 `[SqlFunction("FIND_IN_SET({0},{1})")]` | 完成 |
| 显式 join | `query.join` | 88 | `.LeftJoin<Post>((u, p) => p.UserId == u.Id)`，后续 lambda 依次收到各表 `(u, p)`；`InnerJoin` / `RightJoin` / `CrossJoin` / `From<T2>()`；JOIN 子查询 `LeftJoin(subQuery, on)`；LINQ 查询语法 `join … equals …` | 完成 |
| 分组与 having | `query.group` | 83 | `.GroupBy(p => new { p.UserId }).Having(g => g.Count() > 3).Select(g => new { g.Key.UserId, Total = g.Sum(p => p.Amount) })`；按条件计数 `g.Count(p => p.Paid)`；`Rollup()` / `Cube()` / `g.Grouping(键)` | 完成 |
| union / union all | `query.union` | 18 | `q1.Union(q2)` / `UnionAll`，结果作为派生表继续 Where / Select / 分页 | 完成 |
| CTE | `query.cte` | 11 | `q.AsCte("x")`（对应 toCteAs，默认名 `with_类型名`）：之后可以 Where / Select，或作为 JOIN 来源；视图实体实现 `ICteView` 时隐式 JOIN 读它的 CTE | 完成 |
| 无实体的查询、toMap | `query.map` | 11 | 无实体的表 `db.Table("t_topic")`（对应 mapQueryable）：列用 `t.Get<T>("列名")`，不投影时 `SELECT *` 读成 `DynamicRow`；`SelectColumns("a", "b")`；投影成 Map：`Select(o => new Dictionary<string, object?> { { "id", o.Id } })`、`Select<Dictionary<string, object?>>()`、`Sql.Columns(b)` / `Sql.ColumnsExcept(b, ..)`；读成行 `ToRowsAsync()` / `FirstRowOrDefaultAsync()` | 完成 |
| 流式读取 | `query.stream` | 9 | `await foreach (var x in q.AsAsyncEnumerable())`；分块 `ToChunksAsync(size)`；按偏移分批 `OffsetChunkAsync(size, chunk => 前进行数, maxFetchSize)` | 完成 |
| 复用查询 | `query.reuse` | 2 | 查询对象不可变，可以直接复用 | 完成 |

## 表达式与函数

| 能力 | key | 用例数 | C# 写法 | 状态 |
|---|---|---|---|---|
| 字符串函数 | `func.string` | 131 | 优先映射 BCL：`Contains` / `StartsWith` / `Trim` / `ToUpper` / `Substring` / `IndexOf` / `Length` / `Replace` / `PadLeft`；没有 BCL 对应的用扩展方法：`Like` / `NotLike`；`Contains` 等对应 Java 的 like（`LIKE ?`，% 在参数里），`LikeContains` / `LikeStartsWith` / `LikeEndsWith` 对应 Java 的 contains / startsWith / endsWith（`LIKE CONCAT('%',?,'%')`） | 阶段 1 |
| 日期函数 | `func.date` | 101 | `AddDays` / `AddMonths` / `.Year` / `.DayOfWeek` / `ToString("yyyy-MM")`（格式串按方言翻译）/ `Sql.Now()`；日期相减 `(end - start).TotalDays` / `.Days` / `.TotalHours`（对应 Java 的 duration）；`DateTime` / `DateOnly` / `DateTimeOffset`；日期截断 `p.CreatedAt.DateTrunc(DateTruncUnit.Month)`（MySQL、PG、SQLite、SQL Server 2022 的 DATETRUNC、Oracle 的 TRUNC；周从周一起） | 完成 |
| 通用函数 | `func.general` | 59 | `??`（nullOrDefault）/ `x.NullIf(y)` / `x.EqualsNullSafe(y)`（按方言写 `<=>` / `IS NOT DISTINCT FROM` / `IS`）/ `Sql.Random()` / `Sql.MaxColumns(a, b, c)`、`Sql.MinColumns(..)`（一行里几个值取最大 / 最小：MySQL、PG 的 GREATEST / LEAST，SQLite 的多参数 MAX / MIN，SQL Server 的 VALUES，Oracle 的 UNION ALL 派生表） | 阶段 3 |
| 字符串聚合 | `func.joining` | 29 | `g.StringJoin(p => p.Name, ",")`，集合导航上写 `string.Join(",", u.Posts.Select(p => p.Name))`；链上的 `OrderBy` / `ThenBy` 写进聚合 | 完成 |
| JSON 函数 | `func.json` | 12 | `Sql.Json(u.Attrs).Get / Text / Value<T> / Length / ContainsKey`；方言专属的在 `Sql.MySql.Json(..)` / `Sql.Pg.Jsonb(..)` 下 | 完成 |
| 数学函数 | `func.math` | 4 | 映射 `Math.Abs` / `Round` / `Floor` / `Ceiling` / `Pow` / `Sqrt` / `Sign` / `Log` / `Exp` / `Truncate` | 完成 |
| CASE WHEN | `expr.case` | 27 | 三元表达式（多分支写成嵌套三元），渲染成 `CASE WHEN … THEN … ELSE … END`；聚合加过滤：`g.Count(p => p.Paid)` | 完成 |
| 窗口函数 | `window` | 44 | `Sql.Over.RowNumber(w => w.PartitionBy(p.UserId).OrderBy(p.Id))`；`Lag` / `Lead` / `FirstValue` / `NthValue` / `Ntile` / `PercentRank` / `CumeDist`，支持 frame；取值函数不带窗口或窗口没写排序时沿用查询的 ORDER BY（`Sql.Over.Lag(p.Views, 1)`），查询没有排序时报错 | 完成 |
| 行锁 | `lock` | 28 | `.ForUpdate()` / `.ForUpdateNoWait()` / `.ForUpdateSkipLocked()`：只能在事务里、对单表调用；SQL Server、SQLite 渲染时抛 NotSupportedException。Java 没有 FOR SHARE，不做；其余方言的冒烟用例随阶段 2、3 | 阶段 2、3 |

## 导航与对象树（[0002](decisions/0002-object-tree-and-dto.md)）

| 能力 | key | 用例数 | C# 写法 | 状态 |
|---|---|---|---|---|
| 隐式子查询 any / none / count / sum … | `nav.subquery` | 142 | `u.Posts.Any(p => ..)` / `!u.Posts.Any(..)` / `u.Posts.Count(..)` / `u.Posts.Sum(p => p.Amount)` / `u.Posts.Where(..).Max(..)`；导航上的固定条件 `[Navigate(.., ExtraFilter = nameof(静态属性), ExtraMappingFilter = ..)]`；`u.Posts.DisableLogicDelete()` / `AsTable(..)` / `AsAlias(..)`，对一导航写成 `Sql.DisableLogicDelete(p.Author)` 等；查询级的导航条件 `FilterNavigation(c => c.Bank, b => ..)`；键是逗号列表的导航 `[Navigate(.., KeyMatch = KeyMatch.SelfCsv)]`（Java 的 relationPropertyStrategy，见 [0005](decisions/0005-relation-key-match.md)）与 `Sql.InCsv(值, 列表)` | 完成 |
| 子查询改成分组 join | `nav.groupJoin` | 67 | `Query.GroupJoin(u => u.Posts, q => …)` 或导航上 `[Navigate(GroupJoin = true)]`；`GroupJoinAll()`；`GroupJoinNoMerge()`；多对多、嵌套、Required 导航；主查询里对关联列的等值 / IN 条件下推进分组子查询 | 完成 |
| 一对多展开 flatElement / elements | `nav.flat` | 56 | `flatElement()` 写成 `Any(..)`；`elements(i, j)` 写成 `OrderBy(..).Skip(i).Take(j - i + 1)`；取第 i 个元素 `ElementAt(i)` / `First()`；没写排序时按导航的 `PartitionOrder`；沿集合导航展开 `q.SelectMany(a => a.Bs).SelectMany(b => b.Cs)`（INNER JOIN，多对多经中间表，保留重复；对应 Java 的 `toList(a -> a.bs().flatElement()…)`，Java 逐层带出再去重） | 完成 |
| include | `nav.include` | 37 | `.Include(o => o.Lines, q => q.Where(..).Include(l => l.Product))`；`db.LoadIncludeAsync(entities, nav)`；`IncludeLimitMode`、`IncludeGroupSize`；导航声明的 `OrderBy` / `Limit` / `Offset`；`[IncludeOn(property, value)]`；`Include` 之后再投影时配置并入投影里同名导航的带出 | 完成 |
| 按 DTO 结构自动带出 | `dto.autoInclude` | 36 | `Select<UserVo>()`：同名属性取列，与导航同名的属性按导航带出；`[MapPath("A.B")]` 平铺（`Join = true` 走主查询 JOIN）、`[MapExpression]` 额外列、嵌套 DTO 实现 `IAutoIncludeConfigure<T>` 配置带出查询、`WithConfigureArgument(arg)`；DTO 属性上的 `[Navigate(OrderBy / Limit / Offset)]` 覆盖实体导航的排序与条数；先分页再投影 `ToPageAsync(page, size, q => q.Select<TDto>())`；`[MapFrom<User>]` 让分析器在编译期校验成员 | 阶段 1 |
| 按键填充 fillMany / fillOne | `fill` | 4 | `FillMany(() => db.Query<Line>(), l => l.OrderNo, o => o.No, ..)` / `FillOne(..)`：按键批量查询，带 Take 时按键 UNION ALL | 完成 |
| 树 toTreeList / asTreeCTE | `tree` | 25 | `q.AsTreeCte(o => { o.Up = true; o.LimitDeep = 2; }, childFilter: c => ..)`、`AsTreeCte(c => c.Id, c => c.ParentId)`（asTreeCTECustom）；`ToTreeAsync()` 拼成树 | 完成 |

## 写入

| 能力 | key | 用例数 | C# 写法 | 状态 |
|---|---|---|---|---|
| 插入 | `write.insert` | 50 | `db.Insert(entities).ExecuteAsync()`，`.FillGeneratedKeys()` 回填自增键；字典写入 `db.InsertMap(table, rows)`；INSERT … SELECT：`q.InsertInto(table).Column(name, value)`（HANA 的用例随阶段 3） | 完成 |
| 更新 | `write.update` | 59 | `db.Update<T>().Set(u => new T { Age = u.Age + 1 }).Where(..).ExecuteAsync()`；`db.Update(entity)` 按主键整行更新；`.Columns(..)` 限定列、`.WhereColumns(..)` 换定位列；字典 `db.UpdateMap(..)`；没有任何 SET 时抛异常（Java 是静默返回 0） | 完成 |
| 删除 | `write.delete` | 25 | `db.Delete<T>().Where(..).ExecuteAsync()`；没有条件时必须写 `.All()`；逻辑删除的实体要真删时用 `.Hard()`；条件里可以用集合导航和对一导航 | 阶段 1 |
| upsert | `write.upsert` | 13 | `.OnConflictIgnore(冲突列?)` / `.OnConflictUpdate(更新列?, 冲突列?)`：MySQL（INSERT IGNORE / ON DUPLICATE KEY UPDATE）、PG 与 SQLite（ON CONFLICT）、SQL Server 与 Oracle（单行 MERGE）；MySQL 上忽略冲突也可以写成 `ON DUPLICATE KEY UPDATE 主键 = VALUES(主键)`（`new MySqlDialect { IgnoreConflictAsKeyUpdate = true }`） | 完成 |
| 写哪些列、批量执行 | `write.strategy` | 26 | `.Columns(ColumnStrategy.OnlyNotNull)`，全局默认 `EasyDbOptions.UpdateStrategy`；`.Batch()` 按 SQL 分组一次发出（驱动支持时走 `DbBatch`），不在事务里时每组一个事务 | 完成 |
| UPDATE / DELETE 里用导航 | `write.join` | 10 | `db.Update<Card>().Set(c => new Card { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("x"))`：对一导航生成 JOIN。MySQL：`UPDATE t INNER JOIN … SET` / `DELETE t FROM … JOIN`；PostgreSQL：`UPDATE … FROM` / `DELETE … USING`；SQL Server：`UPDATE t SET … FROM … JOIN` / `DELETE t FROM … JOIN`；SQLite：`UPDATE … AS t … FROM` / `DELETE … AS t WHERE EXISTS(…)`；Oracle：按 ROWID 的 `MERGE` / `DELETE … WHERE ROWID IN (…)` | 完成 |
| 追踪与差异更新 | `tracking` | 62 | `using var tracking = db.BeginTracking();` 后查出的实体被追踪，`db.Update(entity)` 只更新改过的列；`AsTracking()` / `AsNoTracking()`；`db.GetTrackedChanges(entity)`；不追踪时 `db.Update(after).Changes(before)` 只写两份之间不同的列 | 完成 |
| 聚合根保存 | `save.aggregate` | 39 | `db.Save(root).ExecuteAsync()`（必须在追踪作用域与事务里）；级联声明 `[Navigate(Cascade = Cascade.Delete)]`；`.With(SaveBehavior.X)`、`.SavePath(..)`、`.RemoveRoot()`、`[SaveKey]`，见 [0004](decisions/0004-aggregate-save.md) | 完成 |

## 列规则与扩展

| 能力 | key | 用例数 | C# 写法 | 状态 |
|---|---|---|---|---|
| 拦截器 | `interceptor` | 3 | `QueryFilter<T>`（查询、更新、删除自动加条件）/ `EntityInterceptor<T>`（`BeforeInsert` / `BeforeUpdate` 改实体，`BeforeConditionUpdate` 给按条件更新追加 SET）；`options.AddInterceptor(..)`；单条语句上 `.DisableInterceptor("名字")` / `.EnableInterceptor("名字")` | 完成 |
| 逻辑删除 | `logicDelete` | 10 | `[LogicDelete(LogicDeleteStrategy.Boolean)]` / `DeletedTime` / `DeletedTimestamp`；自定义策略实现 `ILogicDeleteStrategy`，`options.AddLogicDeleteStrategy("名字", 策略)`；不改实体时 `options.ApplyLogicDelete(..)` | 完成 |
| 乐观锁 | `version` | 13 | `[Version]`（整数列 +1；`VersionStrategy.NewGuid`）；按条件写入必须 `.WithVersion(v)` 或 `.IgnoreVersion()`；冲突时影响 0 行，用 `ExecuteAsync(1, "msg")` 抛 `ConcurrencyException` | 完成 |
| 列加密 | `column.encryption` | 8 | `[Encryption(typeof(AesBase64EncryptionStrategy))]` / `Sm4HexEncryptionStrategy`，与 Java 的密文兼容，支持分段 LIKE；库端加解密用 `[Column(SqlConversion = typeof(…))]` | 完成 |
| 值转换、枚举 | `column.converter` | 9 | `[Column(Converter = typeof(JsonValueConverter<Attrs>))]`；枚举默认按数字存，`[StoreAsName]` 或 `EnumNameConverter<T>` 按名字存；值对象 `[ValueObject]` 见下面 TS 版独有的能力 | 阶段 3 |
| 自增、生成列 | `column.generated` | 0 | `[Column(Generated = true)]` / `[Column(KeyGenerator = typeof(…))]` / `GeneratedSql` | 完成 |

用例数为 0 的一项，是因为 TS 的分类规则里，对应 Java 的目录在 `sql-test` 下没有用例，能力本身仍要做。

## 表与平台

| 能力 | key | 用例数 | C# 写法 | 状态 |
|---|---|---|---|---|
| 建表与迁移（code-first） | `codeFirst` | 17 | `db.CodeFirst.CreateTablesAsync(..)` / `DropTablesIfExistsAsync(..)` / `CreateTableSql(type)`；只增不删的同步、`RenamedFrom`、读库结构（计划） | 阶段 4 |
| 换表名 asTable | `table.rename` | 52 | `.AsTable("t_post_2025")` / `.AsTable(name => name + "_2025")` / `AsTableSegment(..)`（整个表引用的写法，如 SQL Server 的 `WITH(NOLOCK)`；单表、联表后最近的一张表、导航上的 `Sql.AsTableSegment(u.User, ..)` 都能用）；写入同样 `AsTable` / `AsSchema`；查询级的 `MapTable<Post>(..)`（导航、隐式 join、include 跟着变，fork 的 TableNameMapping）（计划） | 阶段 4 |
| 换 schema | `table.schema` | 25 | 写入 `.AsSchema("tenant_a")`；查询级的写法（计划） | 阶段 4 |
| 跨库链接 | `table.link` | 13 | `.WithTableLink("link")`（计划） | 阶段 4 |
| 分库分表 | `sharding` | 48 | 分表路由（取模、时间）、条件裁剪、库内 UNION ALL、写入路由；分库时在内存里归并排序和分页（计划，先写决策文档 0006） | 阶段 4 |
| 按对象筛选排序 | `dto.object` | 101 | `.FilterBy(request)`（属性上的 `[Filter]` 说明条件类型、多属性 OR、区间、导航路径、集合 EXISTS）/ `.SortBy(sort)`（允许排序的属性）；按路径动态取列 `Sql.Column<T>(c, "user.age")`（对应 anyColumn）；`Table(request)`（计划） | 阶段 4 |
| search 扩展 | `search` | 45 | — | 不做 |

## 方言

| 方言 | key | 用例数 | C# 驱动 | 状态 |
|---|---|---|---|---|
| MySQL（8.0.18 / 8.4、PolarDB、MariaDB） | 归在上面各项 | — | MySqlConnector；`options.UseMySql(..)` | 第一档，完成 |
| SQLite | 归在上面各项 | — | Microsoft.Data.Sqlite；`options.UseSqlite(..)` | 第一档，完成 |
| PostgreSQL 17 | 归在上面各项（Java `pgsql/` 目录约 100 个） | — | Npgsql；`options.UsePostgreSql(..)` | 第一档，完成 |
| SQL Server | `dialect.mssql` | 38 | Microsoft.Data.SqlClient；`options.UseSqlServer(..)`；Upstream 的 38 个 mssql 用例（库 `mssql_eq`）在实库跑通，CI 有独立的 upstream 分片；日期格式按 Java 写成 CONVERT / SUBSTRING 段，拼接式 LIKE 先 `CAST(.. AS NVARCHAR(MAX))`，`Math.Round` / `Atan2` / `Truncate` 按 SQL Server 改写 | 第一档，阶段 2（2.1 完成） |
| Oracle | 归在上面各项 | — | Oracle.ManagedDataAccess.Core；`options.UseOracle(..)` | 第一档，阶段 2 |
| SQL Server 2008（ROW_NUMBER 分页） | `dialect.mssql2008` | 13 | — | 不做 |
| 达梦 | `dialect.dameng` | 51 | DM.DmProvider；方言继承 Oracle | 第二档，阶段 3 |
| 人大金仓 / openGauss | `dialect.kingbase` | 2 | Npgsql；与 PG 共用方言 | 第二档，阶段 3 |
| DuckDB | `dialect.duckdb` | 16 | DuckDB.NET | 第二档，阶段 3 |
| ClickHouse | `dialect.clickhouse` | 4 | ClickHouse.Client | 第二档，阶段 3 |
| SAP HANA | `dialect.hana` | 18 | SAP HANA ADO.NET 驱动 | 第二档，阶段 3 |
| Db2 / TDengine | 没有用例 | — | Net.IBM.Data.Db2 / TDengine.Connector | 第二档，阶段 3 |
| H2 | `dialect.h2` | 74 | 没有 .NET 驱动：和方言无关的用例改在 SQLite 上移植（偏离），只测 H2 方言本身的标不适用 | 阶段 3 |

## fork 新增（`sql-fork-test`，89 个用例）

| 能力 | 来源 | C# 写法 | 状态 |
|---|---|---|---|
| 方言静态入口加专属函数 | fork decisions/0001、M1、M4 | `Sql.MySql.Json(..).Contains / HasMember / Overlaps / ContainsPath / Edit(..)`、`Sql.MySql.Match(..).Against(..)`；`Sql.Pg.Jsonb(..)`、`Sql.Pg.Array(..)`、`Sql.Pg.FullText(..)`；`Sql.Hana.Text(..)`（阶段 3） | 阶段 3 |
| 窗口 frame、`ntile` / `percent_rank` / `cume_dist` | fork 3.4 | 并入 `window` | 完成 |
| rollup / cube / grouping | fork 3.5 | 并入 `query.group` | 完成 |
| NULLIF、null-safe 等值 | fork 3.6 | 并入 `func.general` | 完成 |
| 日期截断 dateTrunc | fork 3.7 | `x.DateTrunc(DateTruncUnit.Month)` | 完成 |
| UPDATE / DELETE … RETURNING | fork 3.8 | `.Where(..).Returning()` / `.Returning(p => new { p.Id, p.Title })` 返回受影响的行（整行时 `RETURNING *`，带选择器只取用到的列）；PostgreSQL、SQLite 支持，其他库渲染时报不支持 | 完成 |
| forUpdate NoWait / SkipLocked | fork 3.9 | 并入 `lock` | 完成 |
| 查询级表名映射 | fork 3.1、decisions/0003 | 并入 `table.rename` | 阶段 4 |
| 类型化 INSERT … SELECT | fork 3.2 | 并入 `write.insert` | 完成 |
| JSON 列转换器 + 编解码接口 | fork 3.3 | `JsonValueConverter<T>`（`System.Text.Json`） | 完成 |
| `DateTimeOffset` 等带时区的时间类型 | fork 1.4 | 原生支持 | 完成 |
| SAP HANA 方言 | fork M2 | 见方言表 | 阶段 3 |
| 实库用例（live） | fork `live/` | 并入 L3 跨方言套件 | 阶段 2、3 |

## TS 版独有的能力（不在 Java 用例里）

| 能力 | TS 写法 | C# | 状态 |
|---|---|---|---|
| 值对象 | `embed()` | `[ValueObject]`：几列合成实体上的嵌套对象，列名默认"属性_子属性"，`Prefix` 换前缀，可以嵌套；`s.Address.City` 是一列，`s.Address == null` 是列全为 NULL；可整体投影、按结构映射到 DTO、`Set` 整个或部分写入（Java 的 `@ValueObject` 已弃用） | 完成 |
| 计算属性 | `defineComputed` | `[Computed(nameof(静态表达式))]` | 完成 |
| 多态（判别联合） | `entity.union` | 判别列加子类 | BACKLOG |
| 只读库 | `db.readonly()` | `db.AsReadOnly()` | BACKLOG |
| N+1 检测 | observe | 开发期检测 N+1 并告警；`ActivitySource("EasyQuerySharp")` 已有 | BACKLOG |
| 预编译查询 | precompiled | interceptors 在编译期翻译 | BACKLOG |
| 合并多个模型 | `mergeSchemas` / `db.mount` | 多个模型程序集注册到同一个 `EasyDb` | BACKLOG |
| 从库反向生成实体 | `eqts pull` | `dotnet eqs pull` | BACKLOG |
