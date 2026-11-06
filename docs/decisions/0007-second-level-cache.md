# 0007 二级缓存

状态：已定（2026-11-06），对应 ROADMAP 4.5。

## 背景

### Java 的 sql-cache

`sql-extension/sql-cache` 约 2500 行，其中 Multi（多实体）整套被注释掉，实际可用的只有两种，都是显式调用、不挂在普通查询上：

- `kvStorage(E.class)`：按主键缓存单行。`singleOrNull(id)` / `toList(ids)` / `toMap(ids)` / `any(id)`，可加 `where(..)`、`useInterceptor` / `noInterceptor`，以及内存里再筛的 `filter(..)`。先逐个 id 查缓存，没命中的合成一条 `WHERE 条件 AND id IN (没命中的)` 查库再写回；查不到的 id 缓存空值（默认 10 秒，防穿透）。
- `allStorage(E.class)`：在 kv 之上多缓存一份“索引”（同条件下 `SELECT id FROM t` 的 id 集合）。`toList()` = 取索引 + 按 id 取行；`toPageResult` 在内存里分页。
- 存储抽象 `EasyCacheManager` 只给接口；测试工程用 Redisson hash + Caffeine。键 `CACHE:{类简名}:{id}` 是一个 hash，field 是条件键（单表时是 where 段 SQL 与参数的 JSON，带关联表时是整条 SQL 加参数的 MD5）。
- **失效不是自动的**：核心在写入后触发 `TriggerEvent`，使用方用 `addTriggerListener` 接上、自己换算要删的键。事件不感知事务（提交前触发）；all 实体只在 INSERT / DELETE 时删索引。

### 四个 Java 用例（QueryTest25）

都连 MySQL 与 Redis。`test11a`（kv，BlogEntity，断言六段 SQL、不重复发 SQL、`filter`、Caffeine 里的条件键与 JSON、关联条件缓存键的 MD5）；`test11a1`（all，Topic，`singleOrNull` 一条 `IN (?)`、`toList()` 两条、Caffeine 里的行与索引 JSON）；`topicIntegerTest`（同表 Long 主键，无断言）；`xxx`（`toPageResult` 两次，无断言，后半是无关的 EXISTS 子查询）。

### TS 版

`packages/cache` 已有：`afterWrite` 删键、`afterCommit` 延迟双删，`CacheStore`（memory / tiered）与 `CacheTransport` 由使用方接；all 模式缓存整个列表；“代数”守卫防止读到旧值后在失效之后写回。

### 约束

缓存不另开查询管线（0001、0002）；核心零依赖，缓存包只能引用 Microsoft.Extensions 的包；核心为它新增的公共概念要在这里定。

## 决定

### 1. 范围与包

- 新包 `EasyQuerySharp.Caching`，只做 kv 与 all。读缓存是显式 API（`cache.Of(db).Kv<T>()`），不是挂在 `Query` 上的透明缓存。
- 读库只用公开的 `db.Query<T>()`（拦截器、逻辑删除、租户过滤自然生效），读侧不需要核心钩子。
- 依赖 `Microsoft.Extensions.Caching.Hybrid`（10.x）。Java 的 Multi 不做，ROADMAP 4.5 的文案同步改为“kv、all 两种”。

### 2. 核心新增：写入监听

```csharp
public interface IWriteListener
{
    /// 写入成功后调用；在事务里推迟到提交成功之后，回滚不调用。
    ValueTask OnWrittenAsync(WriteEvent e, CancellationToken cancellationToken);
}

[Flags] public enum WriteKind { Insert = 1, Update = 2, Delete = 4 }   // upsert = Insert | Update；逻辑删除是 Update

public sealed class WriteEvent
{
    public EasyDb Db { get; }
    public string? Schema { get; }
    public string Table { get; }                  // 实际写入的表（AsTable 之后）
    public Type? EntityType { get; }              // InsertMap / UpdateMap 为 null
    public WriteKind Kind { get; }
    public IReadOnlyList<object>? Keys { get; }   // 受影响行的主键；不能确定时为 null
}
```

另有 `EasyDbOptions.AddWriteListener(..)`、`EasyDb.InTransaction`。

- **单一收口**：`SqlRenderer` 给写语句盖上内部的写入标记（表、种类、取主键的函数），`SqlSession` 记下成功执行的语句，会话结束时按表合并通知；各命令不改，批量、`Returning()`、聚合保存、upsert、`InsertMap` 自动覆盖。没有监听器时零开销。
- **事务**：在环境事务里时挂到提交后回调，回滚丢弃。
- **异常**：监听器抛出的异常包成 `EasyQueryException`（消息写明“数据库已写入，监听器失败”），不吞。
- **主键提取**：INSERT 取语句里的单列主键值（自增主键为 null）；UPDATE / DELETE 取 WHERE 顶层 AND 里单列主键的 `=` / `IN`，有 OR、改主键、复合主键时为 null。值经列的转换器还原成属性类型。
- 不覆盖原生 SQL、触发器与级联、别的服务的写入（缓存提供手动失效）。

### 3. 存储：HybridCache，不用标签

直接绑定 `HybridCache`（单实现的接口不加），只用 `GetOrCreateAsync` / `SetAsync` / `RemoveAsync`。不用标签：标签失效时间存在进程内的字典里，有 L2 时别的实例后来的失效看不到，字典还只增不减。实体用 System.Text.Json 序列化（排除导航属性），读出的实体不追踪。

### 4. 键与桶

```
{前缀}:{表}:epoch               表纪元（随机令牌），整表失效时换新
{前缀}:{表}:{纪元}:r:{主键}      行桶：{ 变体键 → CachedItem }
{前缀}:{表}:{纪元}:i            索引桶（all）：{ 变体键 → 主键列表 }
变体键 = {实体类型全名}|{指纹}    指纹 = 条件查询 ToStatement() 的文本与参数的 SHA-256 前 16 字节
CachedItem = { string? Json, long ExpiresAt }   // Json 为 null 表示这个条件下查无此行
```

键里用实际表名，同表的几个实体共用行桶、一并失效。整表失效 = 换新纪元，旧桶随过期消亡，对“失效后旧值写回旧纪元”天然免疫。

### 5. 公共 API

```csharp
public sealed class EasyCacheOptions
{
    public string KeyPrefix { get; set; } = "EQS";
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan? LocalCacheExpiration { get; set; }
    public TimeSpan NullExpiration { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan? LateEvictDelay { get; set; }
    public JsonSerializerOptions? Json { get; set; }
    public EasyCacheOptions Cache<T>() where T : class;     // 登记缓存的实体
}

public sealed class EasyCache : IWriteListener
{
    public EasyCache(HybridCache store, Action<EasyCacheOptions> configure);
    public CacheClient Of(EasyDb db);
}

public static EasyDbOptions UseCache(this EasyDbOptions options, EasyCache cache);

public sealed class CacheClient
{
    public KvCache<T> Kv<T>() where T : class;
    public AllCache<T> All<T>() where T : class;
    public Task InvalidateAsync<T>(object? id = null, CancellationToken cancellationToken = default) where T : class;
}
```

`KvCache<T>` / `AllCache<T>` 是不可变的链式构建器：`Where`、`WhereIf`、`EnableInterceptor` / `DisableInterceptor(s)`（进指纹），`Filter`（内存里再筛），`FindAsync`、`FindOrThrowAsync`、`FindManyAsync`、`ExistsAsync`；All 另有排序与 `ToListAsync`、`ToIdsAsync`、`CountAsync`、`AnyAsync`、`ToPageAsync`。id 用 `object`，经 `ValueReader.ConvertTo` 转成主键类型。

### 6. 读路径

- 事务里的读绕过缓存（可能读到自己未提交的写入）。
- 行路径：取表纪元，逐 id 探测行桶；未命中的合成一次 `WHERE 条件 AND id IN (…)`（每批最多 1000 个）查库，查到的行与查无此行的 id 分别写回，最后按给出的顺序组装再 `Filter`。
- 索引路径（All）：索引桶 + `GetOrCreateAsync`（工厂是同条件下只取主键列），再走行路径。`ToPageAsync` 没有 `Filter` 时先对索引分页再只取这一页（Java 先取全部行）。

### 7. 失效

| 事件 | 动作 |
| --- | --- |
| 主键已知且不超过 256 个 | 删这些行桶与索引桶 |
| 只有 Insert 且主键不明（自增、InsertSelect） | 只删索引桶 |
| 含 Update / Delete 且主键不明，或主键超过 256 个 | 换新表纪元 |

配了 `LateEvictDelay` 时延迟再做一次。所有 Update / Delete 都清索引桶（Java 的 UPDATE 不清，带条件的索引会读到旧成员）。手动入口 `InvalidateAsync<T>(id)` / `InvalidateAsync<T>()`。

### 8. 一致性与限制

- 经本库的写入，在 `ExecuteAsync`（事务里是 `CommitAsync`）返回后，本实例与 L2 里不再有旧值；其他实例的本机层最多再旧 `LocalCacheExpiration`。1.0 不带多实例失效通知。
- 缓存旁路的竞态靠 `LateEvictDelay` 与过期时间兜底；绕过本库的写入不自动失效。
- 只缓存实体自己的列；只支持单列主键；同表不同实体的主键格式要一致；All 适合小表。

### 9. 四个 Java 用例

| 用例 | 状态 |
| --- | --- |
| `test11a` | 偏离：SQL 与参数逐字搬；Caffeine 里的条件键、JSON、MD5 键是 Java 私有的存储布局，换成行为断言 |
| `test11a1` | 偏离：`IN` 的 101 个参数顺序不同（HashSet 与索引顺序），断言个数与集合相等；存储布局换成行为断言 |
| `topicIntegerTest` | 移植，补上行数与 `FindAsync("88")` 的断言 |
| `xxx` | 移植 |

C# 独有的用例覆盖每种写入的失效、主键提取的各种 WHERE 形状、事务、同表两个实体、两实例经共享 L2、空值缓存、整表纪元。

## 不做

- 透明的查询缓存，投影 / DTO / 聚合 / 分页 / 对象树结果的缓存。
- Java 的 Multi、`toMap`、复合主键；多实例本机层的失效通知；写入前预读受影响的行；自造的存储接口与 Redis 驱动。

## 已定的取舍

1. 存储直接绑定 `HybridCache`，缓存包带上它的传递依赖（Caching.Abstractions、Caching.Memory、Options、Logging.Abstractions、System.Text.Json）。
2. 核心新增公共概念 `IWriteListener` / `WriteEvent` / `WriteKind` / `AddWriteListener` / `EasyDb.InTransaction`；监听器异常抛出（消息写明数据库已写入）。
3. 主键不明的条件写入整表失效，不做写前预读。
4. 事务里的读绕过缓存。
5. 1.0 不自带多实例本机层失效通知，靠 `LocalCacheExpiration` 兜底，或换带 backplane 的 HybridCache 实现。
6. 默认值：`Expiration` 5 分钟、`LocalCacheExpiration` 同 `Expiration`、`NullExpiration` 10 秒、`LateEvictDelay` 关闭。
7. 缓存的实体集中登记，不加特性；id 用 `object`。
8. 与 Java 的行为差异照第 5–7 节：All 分页先分页再取行、UPDATE 也清索引、All 增加排序。
9. ROADMAP 4.5 的文案是“kv、all 两种”（Java 的 Multi 是注释掉的死代码）。
10. L2 的测试先用内存版 `IDistributedCache` 模拟两实例，不做真 Redis 冒烟。

## 实现中的补充

HybridCache（10.11.0）的行为先在 `Extras/HybridCacheProbeTest` 里探测：`DisableUnderlyingData` 的探测在没有时返回 default、不调用工厂也不写入；`RemoveAsync` 清掉自己的本机层与 L2（别的实例的本机层还在）；`SetAsync` 同时写 L2。与调研不同、据此调整的有三处：

- **工厂里不查库**：`GetOrCreateAsync` 的工厂在不带调用方 ExecutionContext 的工作项里执行，AsyncLocal 不流动（按当前用户取租户的拦截器、环境事务、`SqlCapture` 都看不到），在工厂里查库会把别的租户的数据写进这个租户的缓存键。所以索引桶与行桶一样是"探测、没有就查库、`SetAsync`"（§6 写的是索引桶走 `GetOrCreateAsync`），同一实例里同时的未命中不再合并成一次查询；只有表纪元（工厂只生成随机令牌）用 `GetOrCreateAsync`。
- **本机层的过期时间取放进本机层的那次调用的选项**，包括从 L2 读进本机层的探测；探测因此也带上 `Expiration` / `LocalCacheExpiration`，否则别的实例读进来的值用默认的 5 分钟。
- **`MemoryDistributedCache` 不算 L2**（HybridCache 把它当作没有分布式层），测试用自写的进程内 `IDistributedCache` 模拟共享的 Redis，指南里写明 `AddDistributedMemoryCache()` 没有 L2 的效果。

其余细化：

- 桶存成 JSON 字符串（HybridCache 内置的字符串序列化），不受使用方配置的序列化器影响，本机层直接存不可变的字符串。表纪元在 L2 上存一天（本机层仍按 `LocalCacheExpiration`），桶不会因为纪元先过期而整表失效。
- 键里的表是 `schema.表`（有 schema 时）；主键文本用经转换器之后的数据库值、不变区域格式化（日期用 `O`），索引存这些文本，读出时还原成主键类型。
- 主键提取：顶层 AND 里全由主键 `=` / `IN` 组成的 OR（按 `MaxInClauseSize` 拆开的 IN）取并集；冲突时更新的 upsert 只有在约束列就是单列主键、且数据库只在写出的约束列上处理冲突时（不是 MySQL 的 ON DUPLICATE KEY，它在任何唯一键上冲突都更新）才给出主键，否则为 null。
- 不在事务里逐条执行时，中途失败之前已经自动提交的写入照样通知再抛出原来的异常；批量写入自己的本地事务回滚了就不通知。一次通知里每个监听器、每张表都会试到，失败的异常最后一起包成一个 `EasyQueryException`。
- 缓存的失效不理会调用方的取消令牌（数据库已经写入，半途而废会留下旧值）。`EasyDbOptions` 另有公开的 `WriteListeners`（与 `Listeners` 对称），`cache.Of(db)` 据此检查这个库接上了缓存；`KvCache` / `AllCache` 的拦截器开关与核心查询一样是四个（另有 `EnableInterceptors()`）。
