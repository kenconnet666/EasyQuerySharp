# 0002 对象树与 DTO 导航

状态：已定（2026-11-04；2026-11-06 按实现修订：DTO 的映射在运行时构造、带出配置的写法、树的 API，级联保存改由 [0004](0004-aggregate-save.md) 规定；平铺只认 `[MapPath]`，不按名字自动拆分，与 Java 一致）

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
| 按结构自动映射 | `public sealed class UserVo { long Id; string Name; List<PostVo> Posts; … }`，查询用 `Select<UserVo>()`；加 `[MapFrom<User>]` 时分析器在编译期校验成员 | 对应 `select(Dto.class)` 与 `selectAutoInclude`。映射表达式在运行时按 DTO 结构构造，与手写投影走同一条管线 |

底层机制是"表达式字段加标记方法"，不依赖生成器：

```csharp
public static readonly Expression<Func<Post, PostVo>> Map = p => new PostVo(p.Id, p.Title);
u.Posts.Project(PostVo.Map).ToList()     // 对多
u.Company!.Project(CompanyVo.Map)        // 对一
```

自动映射的约定：

- 同名属性直接对应，忽略大小写。
- 平铺：导航路径上的值用 `[MapPath("Company.Name")]` 写明（Java 的 `@NavigateFlat`），经过集合的路径映射成列表，如 `[MapPath("Posts.Title")] List<string> PostTitles`。不按名字猜路径：`CompanyName` 不会自动对应 `Company.Name`——Java 的 `selectAutoInclude` 只认 `@NavigateFlat`、TS 的投影也要写明路径；按名字拆分有歧义（`CompanyName` 也可能是 `CompanyNa.Me`），实体上加一个同名列还会悄悄改变映射。全是对一导航的路径默认走带出查询（同一导航上的几个平铺属性共用一条，单表不写别名，与 Java 的 `@NavigateFlat` 一致）；`[MapPath(.., Join = true)]` 改为主查询里隐式 JOIN（Java 的 `@NavigateJoin`）。带出查询每次最多按 `IncludeGroupSize`（默认 512，可按查询覆盖）个父键查；`WithConfigureArgument(arg)` 把参数传给各层带出配置。
- 嵌套 DTO：与导航同名的属性，类型是 DTO（或 `List<>` 里装着 DTO）时，沿着导航递归映射；类型就是实体本身时整个对象带出。
- 嵌套 DTO 的带出要加过滤、排序、条数限制（对应 `EXTRA_AUTO_INCLUDE_CONFIGURE`）时，DTO 实现 `IAutoIncludeConfigure<TEntity>`；根 DTO 实现时配置的是这条查询本身：

```csharp
public sealed class PostVo : IAutoIncludeConfigure<Post>
{
    public string? Title { get; set; }
    public static Query<Post> Configure(Query<Post> query) => query.Where(p => p.Published).OrderByDescending(p => p.CreatedAt).Take(3);
}
```
- 额外的计算列用 `[MapExpression(nameof(静态表达式))]`。

### 3. 执行策略：对一用 JOIN，对多分层批量查询

| 出现在投影里 | SQL |
|---|---|
| 对一导航的列 `u.Company.Name` | 主查询 JOIN：默认 LEFT JOIN，导航标了 `[Navigate(Required = true)]`（Java 的 required）时用 INNER JOIN |
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
2. **从叶子往根构造**：record 是不可变的，所以先把根行读进缓冲，再跑子查询，最后从叶子往根构造。构造代码在运行时编译，按形状缓存。
3. **IN 列表的参数上限**：键先去重，每条带出查询最多带 `IncludeGroupSize`（默认 512）个父键，超出时分成几条；`MaxInClauseSize` 再把单个 IN 拆成 OR（Oracle 的 `IN` 最多 1000 项）。两处例外与 Java 一致：多对多的中间表、导航上有排序的多对多只查一条，IN 按 `IncludeGroupSize` 拆成 `(… IN (…) OR … IN (…))`——分批会把一个父对象的子数据拆到几批里，各批分别排序就拼乱了；带排序的多对多按目标表查询结果的顺序挂到父对象上，不按中间表的顺序。
4. **流式读取**：投影里有带出时不能 `AsAsyncEnumerable()`（会报错，要用 `ToListAsync`）。按批流式读取对象树（每批跑一次子查询）记在 BACKLOG。
5. **一致性**：多次查询之间数据可能被改；需要时把整组查询放进同一个事务。`.AsSnapshot()` 记在 BACKLOG。
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

导航上的固定条件（`@Navigate(extraFilter)`）：C# 的特性参数里放不了 lambda，所以用 `nameof` 引用一个静态表达式，由分析器 EQ0004 校验类型：

```csharp
[Navigate(RelationType.OneToMany, ExtraFilter = nameof(PublishedOnly))]
public List<Post> Posts { get; set; } = [];
static Expression<Func<Post, bool>> PublishedOnly => p => p.Published;
```

### 6. 级联保存（savable）

见 [0004](0004-aggregate-save.md)：`db.Save(root).ExecuteAsync()` 按追踪快照的差异写出整个聚合，级联方式声明在导航上（`[Navigate(Cascade = …)]`），必须在追踪作用域与事务里执行。

### 7. 自关联树

- 整棵树：`db.Query<Menu>().Where(...).ToTreeAsync()`，查平铺的行，按实体上指向自身的集合导航在内存里拼成树（`toTreeList`）。
- 某节点的下级或上级：`AsTreeCte(o => { o.Up = true; o.LimitDeep = 2; }, childFilter: …)`，用递归 CTE（`asTreeCTE`）；节点键不是导航时写 `AsTreeCte(m => m.Id, m => m.ParentId)`（`asTreeCTECustom`）。之后照常 Where / Select，或 `ToTreeAsync()`。
- 自动映射遇到 `MenuVo(List<MenuVo> Children)` 这种引用自身的结构时不展开，交给 `ToTreeAsync()` 拼树。

## 后果

- 内联器、规划器、物化器是一条公共管线，Include、DTO、VO、树、保存都依赖它。
- Java 的 `selectAutoInclude`（35 个用例）、`@NavigateFlat` / `flatElement`（56 个）在对照表里标"写法不同"，但每个用例都要有对应的 C# 测试。
