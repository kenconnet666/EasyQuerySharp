# 0006 分库分表

状态：已定（2026-11-06）。

## 背景

Java 的分库分表是一套完整的客户端分片中间件：每个实体配一个初始化器（声明有哪些实际表、分在哪些数据源）和一个路由类（从 WHERE 里分片键上的条件裁剪出要查的节点），执行时每个节点一条语句，数据源之间并行，结果在内存里流式归并排序、分页、分组、聚合。代码量约一万行（`sharding` 5553、`merge` 3670、`rewrite` 652、`route` 452）。

对标的是 `ShardingTest` 的 48 个用例（文件里 57 个 `@Test`，9 个被注释掉）：

- 连 MySQL 的四个库（`easy-query-test`、`…2021`、`…2022`、`…2023`），四个实体：取模分表（`TopicSharding`，3 张，`String.hashCode() % 3`）、按月分表（`TopicShardingTime`，40 张，另有“id 末 6 位是月份”的额外分片键、默认降序属性 `createTime`）、按年分库（`TopicShardingDataSource`）、分库加分表（`TopicShardingDataSourceTime`）。
- 只有 7 个用例断言 SQL：4 个监听器捕获 `(SELECT * FROM t_0 UNION ALL SELECT * FROM t_2) t`，3 个 `toSQL()`（输出逻辑表名，不分片）；其余 41 个只断言结果。
- 用例依赖共享的种子数据，改过数据的用例要还原。
- 40–42 三个用例的期望值来自 Java 的实现缺陷：对两个分片查询做 `union` 时把两边的路由做笛卡尔积，行数是 `(2n−1)×正确行数`（90 / 290 / 15972，正确值 30 / 58 / 484）。

TS 版只做了：单库分表在库里用 `UNION ALL` 派生表（排序、分页、聚合交给数据库），分库在内存里归并排序、分页，聚合只支持 `count`/`sum`/`min`/`max`，不支持跨库分组和事务。

本项目的约束：对象树、Include、DTO 最终都归一成同一棵投影表达式交给同一个规划器（0001、0002）。分片不在翻译层另开一条路，只在“已经翻译好的语句”和“执行器”之间工作；核心不引入第三方依赖。

## 决定

### 1. 节点和规则

- **节点** `ShardNode(string DataSource, string Table)`：一张物理表所在的数据源和表名。不分库的实体，数据源就是默认数据源。
- **规则** `IShardRule`：一个实体一个规则，知道有哪些节点、一行写到哪个节点、一个原子条件可能命中哪些节点。Java 的初始化器与路由类总是成对出现，这里合成一个接口：

```csharp
public interface IShardRule
{
    IReadOnlyList<ShardNode> Nodes { get; }                          // 全部节点，时间规则按 to 实时计算
    IReadOnlyCollection<string> KeyProperties { get; }               // 分片键与额外键
    ShardNode NodeOf(object entity);                                 // 写入落点；算不出时抛带实体信息的异常
    IReadOnlyList<ShardNode> Prune(string property, ShardOperator op, object? value); // 帮不上裁剪时返回 Nodes
    ShardOrder? DefaultOrder => null;                                // 无排序、带 Take、命中多个节点时补的排序
}
```

`Prune` 只处理单个原子条件，AND / OR 的组合由引擎做（与 Java 的 `routeFilter` 一致，自定义规则可以照 Java 移植）。

### 2. 声明

规则登记在 `EasyDbOptions` 上，用表达式指定属性，不加新特性（节点范围、数据源名这类运行时信息不适合写在实体上）。多个数据源按名字登记，原来的数据源是默认数据源：

```csharp
var db = new EasyDb(o => o
    .UseMySql(ds2020)
    .WithDefaultDataSourceName("ds2020")
    .AddDataSource("ds2021", ds2021)
    .Shard<TopicSharding>(r => r.Mod(t => t.Id, 3))
    .Shard<TopicShardingTime>(r => r
        .ByMonth(t => t.CreateTime, from: new(2020, 1, 1), to: () => new(2023, 4, 30))
        .ExtraKey(t => t.Id, id => MonthOfIdTail(id))
        .DefaultOrder(t => t.CreateTime, descending: true))
    .Shard<TopicShardingDataSource>(r => r
        .ByYear(t => t.CreateTime, from: new(2020, 1, 1), dataSource: d => $"ds{d.Year}", splitTable: false)));
```

内置规则：

| 规则 | 节点 | 路由 |
| --- | --- | --- |
| `Mod(key, count, tailLength = 1, separator = "_", dataSource = null)` | `表名_0 … 表名_{count-1}` | `=`、`IN` 能裁剪；尾巴是 Java `String.hashCode` 取绝对值再取模（键先转成不区分区域的字符串），与 Java 写入的数据互通 |
| `ByYear / ByMonth / ByDay(key, from, to?, …, dataSource = null, splitTable = true)` | `表名_yyyy` / `_yyyyMM` / `_yyyyMMdd` | `=`、`IN`、`> >= < <=` 能裁剪；`<` 恰好等于某期起点时不含那一期 |

`ExtraKey` 把额外键的 `=` / `IN` 值换算成主分片键的值（算不出返回 null，不裁剪）。季度、周分表 1.0 不做。`db.GetShardRule<T>()` 返回规则，用于元数据断言。

### 3. 路由：从语句的 WHERE 裁剪

路由在查询翻译成 `SelectQuery` 之后进行，不碰翻译层。对一个分片的表来源，取它所在查询的 `Where` 顶层各项：

- 分片键列与参数的比较 → `rule.Prune`；`IN`（值全是参数）→ 各值的并集；AND → 交集；OR → 并集，有一项不能裁剪则整体不约束；其余（`<>`、`LIKE`、函数、非常量）不约束。结果总是全部节点的子集，只会少裁剪，不会裁错。
- 额外键与主键各自裁剪后取交集。
- 条件互相矛盾 → 空路由：列表为空、count 为 0、`Any` 为 false、写入 0 行，不访问数据库。节点数超过上限时报错。

### 4. 查询执行：一个入口，按单元拆

执行器新增接收 `SelectQuery` 的入口，终结方法、`IncludeRunner`、树查询、`ReturningCommand` 都改走它，于是主查询、Include、聚合、分页一次覆盖。

1. 收集语句里的分片表来源，区分最外层 FROM / JOIN 与嵌套处（子查询、派生表、UNION 分支）。
2. 每处按它所在查询的 WHERE 单独裁剪。
3. 最外层分片表的节点按数据源分组，每组按 `MaxUnionTables` 切块，一块一个单元：1 张表照常 `FROM 表名`；多张表写成 `FROM (SELECT * FROM a UNION ALL SELECT * FROM b) t`（成员按表名排序，与 Java 的 `SHARDING_UNION_ALL` 逐字一致）。
4. 单个单元直接执行，没有归并；多个单元并行执行再归并。语句带上数据源名，执行器据此选连接。
5. 嵌套处的分片表必须落在一个数据源里，同样内联成 UNION ALL；跨数据源报错。
6. 非分片表只在默认数据源；单元不在默认数据源而联了非分片表时报错。
7. 同一语句最外层出现两个分片实体 → 不支持（Java 靠笛卡尔积）。

`ToSql()` 不分片，输出逻辑表名（与 Java 一致）；监听器看到的是实际执行的语句。

### 5. 多单元的归并

| 方法 | 每个单元 | 归并 |
| --- | --- | --- |
| `ToListAsync` / `First` | 原查询；有 Take / Skip 时取前 offset + take 行 | 有排序时 k 路归并，否则按单元拼接；再统一 skip / take |
| `ToPageAsync` | 计数与列表各一次 | 计数相加，列表同上 |
| `CountAsync` / `AnyAsync` | `COUNT(*)` / `LIMIT 1` | 求和 / 任一为真 |
| `Sum` / `Min` / `Max` | 原聚合 | 相加（decimal 精确）/ 取小 / 取大，忽略 NULL |

排序列追加成隐藏列；NULL 的位置由方言属性决定。多单元时不支持分组、去重、`Avg`、窗口函数、流式读取，抛 `NotSupportedException` 写明改法。`ToShardingPageAsync(page, size, totalLines?)` 对应 `toShardingPageResult`，不做 Java 的顺序分页跳表与反向分页优化（结果一致）。

### 6. 写入

插入按 `NodeOf(entity)` 分组；按实体更新 / 删除同样；按条件更新 / 删除用 WHERE 裁剪后逐节点串行执行、行数相加。`Set` 里出现分片键报错（行不会迁移）。不支持 `Returning()`、`InsertSelect`、聚合保存里的分片实体。显式 `AsTable(..)` 的来源不走分片。

### 7. 事务与连接

环境事务内部改成“数据源名 → (连接, 事务)”，用到哪个数据源才打开哪个；提交依次进行，不是分布式事务（文档写明）。事务内同一数据源串行，不同数据源并行。任何单元失败取消其余单元，异常带数据源与表名。

### 8. code-first 与夹具

`CodeFirst` 遇到分片实体按每个节点在对应数据源建表。夹具在本地 MySQL 上建三个附加库、87 张表，种子通过分片写入路径插入。L3 套件另加一组小的分片用例（SQLite 两个文件库、MySQL、PG）。

### 9. 48 个用例的归属

| 组 | 数量 | 状态 |
| --- | --- | --- |
| 取模分表（sharding1–8、28、shardingTest50/51/51_1、Map/Map1/Map2/Map3） | 17 | 移植 |
| 时间分表（sharding9–12、26、27、29–39、45–47） | 20 | 移植 |
| 仅分库（sharding43、44）、分库加分表（sharding18–21） | 6 | 移植 |
| 元数据（shardingTest48/49） | 2 | 移植，改读 `GetShardRule<T>()` |
| sharding40–42 | 3 | 偏离：每个分支只展开自己的节点，期望 30 / 58 / 484 |
| test111 | 1 | 不适用：只打印元数据 |

H2OrderTest 的 2 个与 H2QueryTest#allTypeTestSharding1 复用同一套实现。

## 不做

- 同一语句里两个分片实体的联查，分片实体在嵌套处跨数据源。
- 多单元时的分组 / 去重 / Avg / 窗口 / 流式读取；顺序分页跳表、反向分页、两种连接模式。
- 分布式事务；分片键变化后的行迁移；按时间自动建下一期的表（只提供建表 API）。
- 季度、周分表，`LIKE` / `<>` 路由，运行时增删实际表；读写分离。

## 已定的取舍

1. 同库多表默认在库内 `UNION ALL`（`MaxUnionTables` 默认 64）；Java 默认的“每表一条语句、内存归并”靠 `MaxUnionTables = 1`。
2. 声明集中登记在 `EasyDbOptions.Shard<T>(..)`，不加 Java 那样的 `@ShardingTableKey` 特性。
3. 事务按数据源各开各的、依次提交，不是原子的（与 Java 一致），文档写明。
4. 取模默认用 Java 的字符串哈希，与 Java 已写入的数据互通。
5. sharding40–42 记为偏离，期望值用正确语义（30 / 58 / 484）。

## 实现中的补充

0006 没写到、或写得不够细的地方，按最接近 Java 又不损正确性的做法定下（2026-11-06）：

1. **节点上限**：`EasyDbOptions.MaxShardNodes`，默认 128（Java 的 `maxShardingRouteCount`），一条语句裁剪后命中的节点（最外层加嵌套处）超过它时报错。Java 只检查按条件的写入，这里查询也检查，防止条件漏写时扫遍全部分表；可以调大。
2. **默认数据源的名字**：`DefaultDataSourceName` 默认 `ds0`（Java 的默认值），`WithDefaultDataSourceName(..)` 改；`AddDataSource` 登记的在 `EasyDbOptions.DataSources`。自定义规则用 `Shard<T>(IShardRule)` 登记。
3. **语句带数据源**：`SqlStatement.DataSource`（init，null 是默认数据源），监听器能看到语句在哪个库执行；执行失败的异常消息带上数据源。
4. **默认排序**：0006 写的是“带 Take”，实现里带 `Skip` 也补（只跳过不取时跨表的顺序同样不确定）；分组、去重的查询不补。
5. **跨单元不能合并的形状**：除了分组、去重、Avg、窗口、流式读取，聚合投影（没有 GROUP BY 的 `Select(g => Sql.Count())` 这类）与原生排序片段（`OrderByRaw`）同样报 `NotSupportedException`；`ReadAsync`（自己读结果集）按流式读取处理。
6. **NULL 的位置**：`SqlDialect.DefaultNullsOrder(descending)`（公共的虚方法，方言可覆盖）：默认 NULL 最小，PostgreSQL 系、Oracle 系、Db2 相反，DuckDB、ClickHouse 总在最后。排序项指定了 `NullsFirst / NullsLast` 时按指定的。字符串按序数比较（Java 的 `String.compareTo`），可能与数据库的排序规则不同。
7. **隐藏列**：排序列已在结果里时直接用，否则追加成别名 `eqs_order_N` 的隐藏列；读取函数拿到的是只露出原有列的读取器，按整行读的 Map 投影也看不到隐藏列。
8. **SUM 的合并**：整数精确相加（DuckDB 的 SUM 返回 HUGEINT），有浮点时按 double，其余按 decimal。
9. **`ToShardingPageAsync` 的返回值**：`ShardingPage<T>`（`Rows`、`Total`、`PageIndex`、`PageSize`、`TotalLines`），`TotalLines` 按路由顺序（数据源分组、规则里的顺序）每个节点一项；传回时项数必须相同，小于 0 的项重新计数。最外层没有分片表时只有一项。
10. **嵌套处的空路由**：嵌套处的分片表条件互相矛盾时，任取所在数据源的一张分表（它的条件本来就查不到行）；流式读取、`ReadAsync` 的空路由同样任取一张。
11. **按实体更新改了分片键**：追踪的实体改了分片键时报错（行按旧值所在的表定位，不会迁移）；不追踪时无从得知，按当前值落到的表执行。
12. **取模的哈希**：按 Java `AbstractModTableRoute` 的 `Math.abs(hashCode % mod)`；ShardingTest 的 TopicShardingTableRoute 没有取绝对值，用例里的键都是数字串、哈希为正，结果相同。
13. **code-first**：节点的默认索引名、外键名以节点表名开头（PostgreSQL 等库的索引名在 schema 里唯一）；离线的 `PlanSync(types, tables)` 只能比较默认数据源里的表，节点在别的数据源时报错。
14. **夹具**：`ShardingFixture` 是类级固件（程序集固件里的 `MySqlFixture` 先重建 `easy-query-test`），种子用 `Batch()` 批量写入；H2 的三个分表用例在 SQLite 上用 `MaxUnionTables = 1`（Java 的每表一条语句），顺带覆盖内存归并。
15. **sharding40–42**：UNION 去重后 30 / 58 / 484 行，UNION ALL 是两倍（Java 的第二个断言误写成了第一个列表）。
