# 二级缓存

包 `EasyQuerySharp.Caching` 按主键（kv）和按条件的主键索引（all）缓存实体，存储是 `HybridCache`（`Microsoft.Extensions.Caching.Hybrid`）：每个应用实例一个本机层，可以再接一个分布式层（Redis 等）。经本库的写入自动让缓存失效，事务里在提交之后。对应 Java 的 `sql-extension/sql-cache`（`kvStorage` / `allStorage`）。设计与取舍见 [0007](../decisions/0007-second-level-cache.md)。

缓存是显式的：读缓存要调用 `cache.Of(db).Kv<T>()` / `All<T>()`，普通的 `db.Query<T>()` 不经过缓存。

## 接入

```csharp
// 分布式层可选：不接时只有本机层（单实例）
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnectionString);
builder.Services.AddHybridCache();

builder.Services.AddSingleton(sp => new EasyCache(sp.GetRequiredService<HybridCache>(), o => o
    .Cache<User>()
    .Cache<Province>()));
builder.Services.AddEasyQuery((sp, o) => o.UseMySql().UseCache(sp.GetRequiredService<EasyCache>()));
```

- 要缓存的实体集中登记在 `Cache<T>()`，只有登记过的实体能用 `Kv<T>()` / `All<T>()`。只支持单列主键。
- `UseCache(cache)` 把缓存登记成这个库的写入监听器。`cache.Of(db)` 会检查这一点：没有接上的库会直接报错，否则经它的写入不会失效。
- `EasyCache` 和 `EasyDb` 一样，整个应用一个实例。键里只有表名，几个库共用一个 `HybridCache` 时，各自的 `EasyCache` 要用不同的 `KeyPrefix`。
- `AddDistributedMemoryCache()` 登记的 `MemoryDistributedCache` 不算分布式层，`HybridCache` 会把它当作没有 L2。

## kv：按主键

```csharp
var users = cache.Of(db).Kv<User>();
var user = await users.FindAsync(7);                         // 先查缓存，没有再查库并写回
var some = await users.FindManyAsync(new[] { 1, 2, 3 });     // 没命中的合成一条 WHERE id IN (…)
var active = await users.Where(u => u.Enabled).FindAsync(7); // 条件是缓存键的一部分
var mine = await users.Filter(u => u.OwnerId == me).FindAsync(7);  // 在内存里再筛，不查库
```

| 方法 | 说明 | Java |
|---|---|---|
| `FindAsync(id)` | 一行；没有或不满足条件时为 null | `singleOrNull(id)` |
| `FindOrThrowAsync(id, message?)` | 没有时抛 `NotFoundException`，消息默认取 `[Table(NotFoundMessage = ..)]` | `singleNotNull(id)` |
| `FindManyAsync(ids)` | 按给出的顺序，没有的跳过 | `toList(ids)` |
| `ExistsAsync(id)` | 行存在且满足条件 | `any(id)` |
| `Where(..)` / `WhereIf(..)` | 条件，进缓存键 | `where(..)` |
| `EnableInterceptor(name)` / `DisableInterceptor(name)` / `DisableInterceptors()` / `EnableInterceptors()` | 拦截器开关，进缓存键 | `useInterceptor` / `noInterceptor` |
| `Filter(..)` | 内存里再筛，不进缓存键 | `filter(..)` |

- id 的类型是 `object`，先转成实体的主键类型：`FindAsync("88")` 在 `long` 主键上就是 88。
- 缓存键是"条件查询"的 SQL 与参数：条件、拦截器加的条件（逻辑删除、租户）都在里面，不同租户各缓存各的。同一个主键在不同条件下分别缓存，不满足条件的行记为"查无此行"。
- 查无此行也缓存，时间是 `NullExpiration`（默认 10 秒），防止反复查不存在的主键。
- 读出的实体是新的对象，不进追踪作用域。

## all：按条件的主键索引

```csharp
var provinces = cache.Of(db).All<Province>();
var list = await provinces.OrderBy(p => p.Code).ToListAsync();
var page = await provinces.Where(p => p.Name.StartsWith("浙")).OrderBy(p => p.Code).ToPageAsync(1, 20);
var ids = await provinces.ToIdsAsync();
```

- 第一次先查主键列（`SELECT t.id FROM t_province t WHERE 条件 ORDER BY ..`）缓存成索引，再按主键取行；行与 kv 在同一条件下共用。
- 条件和排序都进索引的缓存键；没有排序时按数据库返回的顺序。
- 另有 `CountAsync`、`AnyAsync`，以及 kv 的全部方法（`FindAsync` 只取这一行，不看索引）。
- `ToPageAsync` 没有 `Filter` 时先对索引分页、只取这一页的行（Java 先取全部行）；有 `Filter` 时取出全部行筛过再分页。
- 适合小表：配置、字典、省市。索引是整个主键列表，单个缓存项受 `HybridCache` 的 `MaximumPayloadBytes`（默认 1 MB）限制。

## 失效

经本库的每种写入（按实体、按条件、批量、upsert、逻辑删除、`Returning()`、聚合保存、`InsertMap` / `UpdateMap`、`InsertInto`）执行成功后，按表通知一次。主键从语句里取：

| 写入 | 动作 |
|---|---|
| 主键已知且不超过 256 个（按实体写入、条件里有主键的 `=` / `IN`） | 删这些行与这张表的索引 |
| 只有插入且主键不明（自增主键、`InsertInto`、`InsertMap`） | 只删索引 |
| 含更新 / 删除且主键不明（条件里没有主键、改了主键），或主键超过 256 个 | 整表失效（换新的表纪元，旧的缓存项随过期消亡） |

- 写入登记过的实体所在的表时失效，按实际的表名：同表的几个实体（如 `Topic` 与 `long` 主键的 `TopicInteger`）一并失效，它们的主键格式要一致。
- 事务里的写入在 `CommitAsync` 之后失效，回滚不失效；事务里的读绕过缓存（可能读到自己未提交的写入）。
- `LateEvictDelay` 设了时，隔这么久在后台再失效一次（延迟双删），挡住"读到旧值的慢请求在失效之后写回"。
- 绕过本库的写入（原生 SQL、触发器与级联、别的服务）不会自动失效，手动调用：

```csharp
await cache.Of(db).InvalidateAsync<User>(7);   // 这一行与索引
await cache.Of(db).InvalidateAsync<User>();    // 整表
```

## 配置

| 选项 | 默认 | 说明 |
|---|---|---|
| `KeyPrefix` | `EQS` | 键的前缀：`{前缀}:{表}:…` |
| `Expiration` | 5 分钟 | 缓存项的过期时间 |
| `LocalCacheExpiration` | 同 `Expiration` | 本机层的过期时间：别的实例写入后，本实例最多再旧这么久 |
| `NullExpiration` | 10 秒 | "查无此行"的缓存时间 |
| `LateEvictDelay` | 不做 | 写入后隔多久再失效一次 |
| `Json` | 默认选项 | 实体的 System.Text.Json 选项；只序列化实体自己的列，导航属性等总是排除 |

## 一致性与限制

- 经本库的写入，在 `ExecuteAsync`（事务里是 `CommitAsync`）返回之后，本实例与分布式层里已经没有旧值。
- 多实例时，别的实例的本机层最多再旧 `LocalCacheExpiration`：1.0 不带多实例的失效通知。要求更及时时调小 `LocalCacheExpiration`，或换带 backplane 的 `HybridCache` 实现。
- 缓存旁路（cache-aside）的竞态（读到旧值的请求在失效之后写回）靠 `LateEvictDelay` 与过期时间兜底。
- 只缓存实体自己的列，不缓存投影、DTO、聚合、对象树；只支持单列主键；主键类型要能与文本互转（字符串、整数、Guid、日期，或带转换器转成这些类型）。
- 不用 `HybridCache` 的标签：标签的失效时间记在进程内，有分布式层时别的实例看不到。
- 裁剪 / Native AOT 下要在 `EasyCacheOptions.Json` 里给源生成的 `JsonSerializerContext`，见[裁剪与 Native AOT](native-aot.md)。
- 分片实体（见[分库分表](sharding.md)）不能缓存：写入落在各分片的实际表上，缓存的失效对不上，`cache.Of(db)` 会抛 `NotSupportedException`。

## 写入监听

失效挂在核心的写入监听上，别的用途（审计、领域事件、搜索索引）也可以直接用：

```csharp
public sealed class AuditListener : IWriteListener
{
    public ValueTask OnWrittenAsync(WriteEvent e, CancellationToken cancellationToken)
    {
        // e.Table、e.Schema、e.EntityType、e.Kind（Insert / Update / Delete，可组合）、e.Keys（不能确定时为 null）
        return ValueTask.CompletedTask;
    }
}

var db = new EasyDb(o => o.UseMySql(dataSource).AddWriteListener(new AuditListener()));
```

- 一次执行（一个 `ExecuteAsync`、一次聚合保存）里同一张表的写入合并成一个事件；事务里推迟到提交之后，回滚不调用。
- 监听器抛出的异常包成 `EasyQueryException`（消息写明"数据库已写入"）交给调用方，不吞。
- `db.InTransaction` 告诉监听器和其他组件当前是否在事务里。

## 对应 Java

| Java | C# |
|---|---|
| `easyCacheClient.kvStorage(E.class)` | `cache.Of(db).Kv<E>()` |
| `easyCacheClient.allStorage(E.class)` | `cache.Of(db).All<E>()` |
| `singleOrNull` / `toList(ids)` / `any(id)` / `toList()` / `toPageResult` | `FindAsync` / `FindManyAsync` / `ExistsAsync` / `ToListAsync` / `ToPageAsync` |
| `toMap(ids)`、Multi 实体（注释掉的死代码） | 不做 |
| `EasyCacheManager`（使用方实现存储） | `HybridCache`（本机层 + 可选的 `IDistributedCache`） |
| `addTriggerListener` + 自己换算要删的键（提交前触发） | `UseCache(cache)` 自动失效（提交后）；通用的写入监听是 `IWriteListener` |
| `@CacheEntitySchema` 标在实体上 | `EasyCacheOptions.Cache<T>()` 集中登记 |
