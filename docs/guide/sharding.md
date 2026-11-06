# 分库分表

一个实体的数据分在几张表（分表）或几个库（分库）里时，在 `EasyDbOptions` 上登记它的规则，查询按条件只查命中的表，写入按分片键落到各自的表。对应 Java 的分片（`ShardingInitializer` + `TableRoute` / `DataSourceRoute`），设计依据见 [0006](../decisions/0006-sharding.md)。示例卡片在 `docs/examples/ShardingExamples.cs`，各方言的 SQL 在 `docs/sql-snapshots/sharding.*.sql`。

## 登记规则与数据源

```csharp
var db = new EasyDb(o => o
    .UseMySql(ds2020)
    .WithDefaultDataSourceName("ds2020")              // 默认数据源的名字，不写是 ds0
    .AddDataSource("ds2021", ds2021)                  // 分库时其余的库按名字登记
    .Shard<Order>(r => r.Mod(t => t.Id, 3))           // t_order_0 … t_order_2
    .Shard<Log>(r => r
        .ByMonth(t => t.CreateTime, from: new(2020, 1, 1), to: () => new(2023, 4, 30))   // t_log_202001 … t_log_202304
        .ExtraKey(t => t.Id, id => MonthOfIdTail(id))                                     // id 末 6 位是月份
        .DefaultOrder(t => t.CreateTime, descending: true))
    .Shard<Bill>(r => r
        .ByYear(t => t.CreateTime, from: new(2020, 1, 1), dataSource: d => $"ds{d.Year}", splitTable: false)));  // 按年分库，各库表名不变
```

不分片的表都在默认数据源里。规则在创建 `EasyDb` 时建好，节点用到的数据源名没有登记时直接报错。

### 内置规则

| 规则 | 节点 | 能裁剪的条件 |
|---|---|---|
| `Mod(key, count, tailLength = 1, separator = "_", dataSource = null)` | `表名_0 … 表名_{count-1}`，尾巴左补 0 到 `tailLength` 位 | `=`、`IN` |
| `ByYear / ByMonth / ByDay(key, from, to = null, separator = "_", dataSource = null, splitTable = true)` | `表名_yyyy` / `_yyyyMM` / `_yyyyMMdd`，从 `from` 所在的一期到 `to`（默认现在）所在的一期 | `=`、`IN`、`> >= < <=`；`<` 恰好等于某期起点时不含那一期 |

- 取模的尾巴是键的字符串（不区分区域）按 Java `String.hashCode` 取模的绝对值，与 Java 写入的数据互通。
- 时间规则的 `to` 每次路由时求值，新的一期自动出现在节点里；对应的表要建好（见下面的 code-first）。`dataSource` 按每期的起点算数据源，`splitTable: false` 时只分库。
- `ExtraKey(key, toShardKey)`：额外分片键（Java 的 `@ShardingExtraTableKey`），它的 `=` / `IN` 值换算成主分片键的值再裁剪，换算不出时返回 null、不裁剪。
- `DefaultOrder(property, descending)`：查询没写排序、带了 `Take` / `Skip`、命中多个节点时补的排序（Java 的 `addPropertyDefaultUseDesc`），跨表取前几行的结果才确定。

### 自定义规则

实现 `IShardRule` 后用 `options.Shard<T>(rule)` 登记，可以照 Java 的路由类移植：

```csharp
public sealed class RegionRule : IShardRule
{
    public IReadOnlyList<ShardNode> Nodes { get; } = [new("ds_east", "t_shop"), new("ds_west", "t_shop")];
    public IReadOnlyCollection<string> KeyProperties { get; } = [nameof(Shop.Region)];
    public ShardNode NodeOf(object entity) => Nodes[((Shop)entity).Region == "west" ? 1 : 0];
    public IReadOnlyList<ShardNode> Prune(string propertyName, ShardOperator op, object? value) =>
        op == ShardOperator.Equal && value is string region ? [Nodes[region == "west" ? 1 : 0]] : Nodes;
}
```

`Prune` 只处理一个原子条件，AND（交集）、OR（并集）、IN（各值并集）由引擎组合；返回值必须是 `Nodes` 的子集，宁可少裁剪也不能裁错。`db.GetShardRule<T>()` 返回登记的规则，可以读节点与分片键。

## 查询

### 裁剪

查询翻译成 SQL 之后、执行之前，按每张分片表所在查询的 WHERE 裁剪：

- 分片键列与参数的比较交给规则；`IN`（值全是参数）取各值的并集；AND 取交集；OR 取并集，有一项不能裁剪时整体不约束；`<>`、`LIKE`、函数、与列比较不约束。
- 条件互相矛盾时是空路由：列表为空、计数为 0、`Any` 为 false、写入 0 行，不访问数据库。
- 一条语句命中的节点数超过 `EasyDbOptions.MaxShardNodes`（默认 128，Java 的 `maxShardingRouteCount`）时报错，防止条件漏写时扫遍全部分表。

### 同一数据源的分表合成一条语句

命中的节点按数据源分组，每组按 `EasyDbOptions.MaxUnionTables`（默认 64）切块，一块一条语句。一张表时照常换表名；几张时写成 UNION ALL 派生表（与 Java 的 `SHARDING_UNION_ALL` 逐字一致），排序、分页、分组、聚合都交给数据库：

```sql
-- db.Query<OrderLog>().Where(l => l.Id == "a1").SingleAsync()
SELECT `id`,`user_id`,`amount`,`created_at` FROM `t_order_log_2` WHERE `id` = ?
-- db.Query<OrderLog>().OrderByDescending(l => l.Amount).Take(2).Select(l => l.Id).ToListAsync()
SELECT t.`id` FROM (SELECT * FROM `t_order_log_0` UNION ALL SELECT * FROM `t_order_log_1` UNION ALL SELECT * FROM `t_order_log_2`) t ORDER BY t.`amount` DESC LIMIT 2
```

子查询、派生表、UNION 分支里的分片表同样按它所在查询的条件裁剪、内联成 UNION ALL，但只能落在一个数据源里。`MaxUnionTables = 1` 时每张表一条语句、在内存里归并（Java 的默认做法）。

### 跨数据源时在内存里归并

分到几条语句（几个数据源，或一个数据源超过 `MaxUnionTables` 张表）时各语句并行执行，再合并：

| 方法 | 每条语句 | 合并 |
|---|---|---|
| `ToListAsync` / `FirstAsync` / `Include` 的子查询 | 原查询；带 `Take` / `Skip` 时取前 offset + take 行 | 有排序时 k 路归并（排序列不在结果里时追加成隐藏列），否则按节点顺序拼接；再统一跳过、截取 |
| `ToPageAsync` | 计数与列表各一次 | 计数相加，列表同上 |
| `CountAsync` / `AnyAsync` | `COUNT(*)` / `LIMIT 1` | 相加 / 任一为真 |
| `SumAsync` / `MinAsync` / `MaxAsync` | 原聚合 | 相加（整数、decimal 精确）/ 取小 / 取大，忽略 NULL |

- 归并时字符串按序数比较（与 Java 的 `String.compareTo` 一致，不按数据库的排序规则），NULL 的位置按方言（MySQL、SQLite、SQL Server 升序在前，PostgreSQL、Oracle 升序在后，DuckDB 总在最后）。
- 跨语句时不支持分组、去重、聚合投影、窗口函数、`AverageAsync`、原生排序片段、流式读取（`AsAsyncEnumerable`、`ReadAsync`），抛 `NotSupportedException` 并说明改法：加分片键条件让查询落在一个数据源里，或调大 `MaxUnionTables`。
- 环境事务里同一数据源的语句共用一个连接，依次执行；不同数据源并行。任何一条失败时取消其余的，异常带上数据源与表名。

### ToSql 与监听器

`ToSql()` 不分片，输出逻辑表名（与 Java 一致）；监听器看到的是实际执行的语句，`SqlStatement.DataSource` 是执行它的数据源。

### 按节点计数的分页

`ToShardingPageAsync(page, size, totalLines)` 对应 Java 的 `toShardingPageResult`：最外层分片表命中的每个节点各计一次数，各节点的行数按路由顺序放在 `ShardingPage<T>.TotalLines` 里。翻页时把它传回，不小于 0 的项不再计数，小于 0 的项重新计数（例如按月分表时只重算当月）：

```csharp
var first = await db.Query<Log>().OrderBy(l => l.CreateTime).ToShardingPageAsync(1, 20);
var lines = first.TotalLines.ToArray();
lines[^1] = -1;
var second = await db.Query<Log>().OrderBy(l => l.CreateTime).ToShardingPageAsync(2, 20, lines);
```

Java 的顺序分页跳表与反向分页优化不做，结果相同。

## 写入

- 插入按 `NodeOf(实体)` 落到各自的表与数据源；分片键为 null 或不在节点范围里时报错。
- 按实体更新、删除同样按实体的节点；按条件更新、删除按 WHERE 裁剪出的节点逐个执行（串行），受影响的行数相加。
- 行不会迁移：`Set` 里改分片键直接报错；追踪的实体改了分片键再按实体更新也报错。要换分片键请先删除再插入。
- 不支持：`Returning()`、`InsertSelect`、聚合保存里的分片实体。
- 显式换表（`AsTable`、`MapTable`、`AsTableSegment`）的查询与写入不走分片。

## 事务

环境事务（`BeginTransaction`）在每个用到的数据源上各开一个连接与事务，用到哪个才开哪个；提交时依次提交。这不是分布式事务：中途提交失败时，已经提交的数据源不会回滚（与 Java 一致）。

## code-first

分片实体的 `CreateTablesAsync` / `DropTablesIfExistsAsync` / `SyncTablesAsync` / `TableExistsAsync` 按规则的每个节点，在节点所在的数据源上建表、删表、同步；默认的索引名、外键名跟着换成节点的表名。按时间分表时新一期的表不会自动建，定期调用 `SyncTablesAsync(typeof(Log))` 补上（只建缺的表）。离线的 `PlanSync(types, tables)` 只能比较默认数据源里的表。

## 不做

同一语句里两个分片实体的联查、嵌套处跨数据源的分片表；跨数据源的分组 / 去重 / 平均值 / 窗口 / 流式读取；分布式事务；分片键变化后的行迁移；按时间自动建表；季度、周分表；`LIKE` / `<>` 路由；运行时增删节点；读写分离。

## 对应的 Java 写法

| Java | C# |
|---|---|
| `ShardingInitializer.actualTableNameInit` + `TableRoute` / `DataSourceRoute` | `options.Shard<T>(r => r.Mod(..) / r.ByMonth(..))`，或实现 `IShardRule` |
| `AbstractShardingTableModInitializer` + `AbstractModTableRoute` | `Mod(key, count, tailLength)` |
| `AbstractShardingMonthInitializer` + `AbstractMonthTableRoute`（年、日同理） | `ByMonth(key, from, to)`（`ByYear`、`ByDay`） |
| `getExtraRouteFilter`、`@ShardingExtraTableKey` | `ExtraKey(key, toShardKey)` |
| `addPropertyDefaultUseDesc` | `DefaultOrder(property, descending: true)` |
| `setDefaultDataSourceName`、`ShardingDataSource` | `WithDefaultDataSourceName(..)`、`AddDataSource(name, source)` |
| `EasyBehaviorEnum.SHARDING_UNION_ALL` | 默认做法，`MaxUnionTables` 控制每条语句合并几张表 |
| `maxShardingRouteCount` | `MaxShardNodes` |
| `toShardingPageResult(page, size, totalLines)` | `ToShardingPageAsync(page, size, totalLines)` |
| `EntityMetadata.getShardingTablePropertyNames` 等 | `db.GetShardRule<T>()` 的 `KeyProperties`、`Nodes` |
| `paginationReverse`、`ascSequenceConfigure`、`useMaxShardingQueryLimit` | 不做（顺序分页与连接模式），结果相同 |
