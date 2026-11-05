# 0002 对象树与 DTO 导航

状态：提议（2026-11-04）

## 背景

easy-query 里"带出关联数据"有好几条路：

- Java：`include` / `includes` 带出实体导航；`selectAutoInclude(DTO.class)` 按 DTO 结构自动带出，DTO 里嵌套类加 `@Navigate`，额外条件写在静态字段 `EXTRA_AUTO_INCLUDE_CONFIGURE` 里；`@NavigateFlat` 把导航上的列平铺进 DTO；`toTreeList` / `asTreeCTE` 处理自关联树；`savable` 级联保存。各自是独立的代码路径，映射在运行时反射完成。
- TS：统一成两样东西，一是 `defineView`（可嵌套、可复用的投影），二是 `toList` 嵌套。ROADMAP 19.2 里试过"一条 SQL 用 JSON 聚合出整棵树"，做完又撤回了。理由是每层 1+1 次查询，没有 N+1，耗时随数据量线性增长；JSON 聚合让结果变大、数据库里的聚合变重，换来的往返节省不值得维护两套写法。

## 决定

### 1. 所有写法归一成投影表达式

内联投影、可复用投影、自动映射 DTO、实体 `Include`，最后都展开成**同一棵纯投影表达式**，交给同一个规划器。

```
写法（4 种） ──内联器──▶ 投影表达式 ──规划器──▶ 执行计划树 ──执行器──▶ 物化（从叶子往根构造）
```

### 2. DTO 有三种写法

| 写法 | 例子 | 适合 |
|---|---|---|
| 内联 | `Select(u => new { u.Id, Posts = u.Posts.Select(p => new { p.Title }).ToList() })` | 一次性查询 |
| 可复用投影（主推） | `[Projection] public static PostVo ToVo(this Post p) => new(p.Id, p.Title, p.Author.Name);`，查询里写 `u.Posts.Select(p => p.ToVo())` | 多处共用的 VO。它就是普通 C# 方法，内存里的对象也能调，能写单元测试；生成器产出对应的表达式 |
| 按结构自动映射 | `[MapFrom<User>] public partial record UserVo(long Id, string Name, string? CompanyName, List<PostVo> Posts);`，查询用 `Select<UserVo>()` | 对应 `selectAutoInclude`。映射表达式**编译期**生成，字段对不上直接报编译错误 |

底层机制是"表达式字段加标记方法"，不依赖生成器：

```csharp
public static readonly Expression<Func<Post, PostVo>> Map = p => new PostVo(p.Id, p.Title);
u.Posts.Project(PostVo.Map).ToList()     // 对多
u.Company!.Project(CompanyVo.Map)        // 对一
```

自动映射的约定：

- 同名属性直接对应，忽略大小写。
- 平铺：`CompanyName` 对应 `Company.Name`（`@NavigateFlat`）；`List<string> PostTitles` 对应 `Posts.Select(p => p.Title)`。路径有歧义时用 `[MapPath("Company.Name")]` 指定。全是对一导航的路径默认走带出查询（同一导航上的几个平铺属性共用一条，单表不写别名，与 Java 的 `@NavigateFlat` 一致）；`[MapPath(.., Join = true)]` 改为主查询里隐式 JOIN（Java 的 `@NavigateJoin`）。带出查询每次最多按 `IncludeGroupSize`（默认 512，可按查询覆盖）个父键查；`WithConfigureArgument(arg)` 把参数传给各层带出配置。
- 嵌套 DTO：属性类型是 `[MapFrom<Post>]` 的 DTO，或者 `List<>` 里装着这样的 DTO，就沿着导航递归映射。
- 个别成员要加过滤、排序、条数限制（对应 `EXTRA_AUTO_INCLUDE_CONFIGURE`），用 partial 配置方法只覆盖这一个成员：

```csharp
static partial void Configure(MapConfig<User, UserVo> m)
    => m.For(x => x.Posts, posts => posts.Where(p => p.Published).OrderByDescending(p => p.CreatedAt).Take(3));
```

### 3. 执行策略：对一用 JOIN，对多分层批量查询

| 出现在投影里 | SQL |
|---|---|
| 对一导航的列 `u.Company.Name` | 主查询 JOIN。可空引用类型决定 JOIN 方式：`Company?` 用 LEFT；非空且外键非空用 INNER |
| 对一导航整个对象 `CompanyVo?` | 同上；以主键列是否为 NULL 判断这个对象是不是 null |
| 对多集合 `.Select(..).ToList()` | 每层一次查询，`WHERE fk IN (父键)` |
| 对多集合加 `OrderBy` + `Take(n)` | `ROW_NUMBER() OVER (PARTITION BY fk ORDER BY …)` 派生表（对应 `partitionOrder` / `limit`） |
| 多对多 | 子查询 JOIN 中间表，把父键一起查出来 |
| 集合上的聚合 `Count` / `Sum` / `Any` | 默认关联子查询；`GroupJoin()` 标记后，同一导航上的多个聚合合并成一次分组 join（`subQueryToGroupJoin`） |
| 对多里"取一条当对一"（最新的一条） | `ROW_NUMBER` 派生表加 LEFT JOIN（TS 的 `oneOfMany`） |

**不做**一条 SQL 用 JSON 聚合出整棵树，理由同 TS 的 ROADMAP 19.2。

探针（`Tree.cs`）拆出的计划：

```
查询 User  JOIN: Company
  └─ Posts: 按父键批量 WHERE fk IN (...) + Where → OrderByDescending → 每个父行前 3 条（ROW_NUMBER）
     查询 Post  JOIN: Author  标量子查询: Comments.Count
```

### 4. 实现上必须做到的细节

1. **隐藏的键列**：拼装要用父主键和子表外键，DTO 没选这些列时，规划器自动补上，物化完再丢掉。
2. **从叶子往根构造**：record 是不可变的，所以先把根行读进缓冲，再跑子查询，最后从叶子往根构造。由生成器产出构造代码。
3. **IN 列表的参数上限**：键先去重，再按方言上限分批（SQL Server 2100 个参数、Oracle 的 `IN` 最多 1000 项）；PG 用 `= ANY(@arr)`，只占一个参数。
4. **流式读取**：`ToAsyncEnumerable(batchSize: 500)` 按批读根行，每批跑一次子查询。不能一次一行，否则就是 N+1。
5. **一致性**：多次查询之间数据可能被改。`.AsSnapshot()` 把整组查询放进同一个事务或快照里执行。
6. **分页**：只作用在根查询上，所以 `ToPageAsync` 的分页数和总数天然正确，计数查询不带子查询。

### 5. 实体对象树（Include）

读数据给前端看，主推 DTO 投影。加载聚合来修改再保存时，才用 Include。写法参考 Java 的嵌套配置，不学 EF 的 `ThenInclude` 链：

```csharp
await db.Query<Order>()
    .Include(o => o.Lines, q => q.Where(l => !l.Deleted).OrderBy(l => l.No).Include(l => l.Product))
    .Include(o => o.Customer)
    .FirstAsync(o => o.Id == id, ct);
```

内部当作"全列的实体投影"，走同一个规划器。

导航上的固定条件（`@Navigate(extraFilter)`）：C# 的特性参数里放不了 lambda，所以用 `nameof` 引用一个静态表达式，由生成器校验类型：

```csharp
[Navigate(RelationType.OneToMany, ExtraFilter = nameof(PublishedOnly))]
public List<Post> Posts { get; set; } = [];
static Expression<Func<Post, bool>> PublishedOnly => p => p.Published;
```

### 6. 级联保存（savable）

```csharp
await db.Save(order).Cascade(o => o.Lines).Cascade(o => o.Lines, l => l.Attachments).ExecuteAsync(ct);
```

- 级联路径必须**显式声明**，没声明的导航一律不碰，防止误删子表。
- 按主键比较差异：比较的基准是已追踪的快照，没有追踪时先查出现有子表的键；据此决定新增、修改还是删除。不在集合里的子行，按导航上配置的 `OrphanPolicy`（`Delete` / `Detach` / `Ignore`）处理。
- 父表生成的主键回填到子表外键；能用 `RETURNING` / `OUTPUT` 的方言批量回填。
- 必须在事务里执行，不在事务里直接报错（对齐 Java）。

### 7. 自关联树

- 整棵树：`db.Query<Menu>().Where(...).ToTreeAsync(m => m.Id, m => m.ParentId)`，查平铺的行，在内存里拼成树（`toTreeList`）。
- 某节点的下级或上级：`DescendantsOf(id)` / `AncestorsOf(id)`，用递归 CTE（`asTreeCTE`）。
- 自动映射遇到 `MenuVo(List<MenuVo> Children)` 这种递归结构时，生成器报诊断，要求指定 `[MapTree(MaxDepth = n)]` 或改用 `ToTreeAsync`，不能无限展开。

## 后果

- 内联器、规划器、物化器是一条公共管线，Include、DTO、VO、树、保存都依赖它，要排在第二期最前面。
- Java 的 `selectAutoInclude`（35 个用例）、`@NavigateFlat` / `flatElement`（56 个）在对照表里标"写法不同"，但每个用例都要有对应的 C# 测试。
