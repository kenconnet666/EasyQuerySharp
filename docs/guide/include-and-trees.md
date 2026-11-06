# 带出与对象树

把关联数据一起读出来，有四种写法，最后都归一成同一种投影、走同一个规划器（[0002](../decisions/0002-object-tree-and-dto.md)）：

| 写法 | 结果 | 适合 |
|---|---|---|
| `Include(导航, 配置)` | 实体，导航属性被填上 | 读出来要修改、再[保存整个聚合](save-and-tracking.md) |
| 投影里嵌套集合 `u.Posts!.Select(..).ToList()` | 任意形状 | 一次性的页面数据 |
| `Select<TDto>()` | 按 DTO 的结构递归带出 | 固定的 VO / DTO |
| `FillMany` / `FillOne` | 按键把另一条查询的结果填进来 | 没有声明导航的关系 |

执行策略相同：对一导航在主查询里 JOIN，对多导航每层一条 `WHERE 外键 IN (父键…)` 的查询，不会 N+1；父键去重，每条最多 `IncludeGroupSize`（默认 512）个，超出时分批。

## Include

`Include(导航)` 带出一层；第二个参数配置带出查询，可以加条件、排序、条数，里面再 `Include` 就是下一层：

<!-- card: include/IncludeMany sql=mysql -->
```csharp
db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id)
    .Include(u => u.Posts, q => q.Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views))
    .ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `company_id` = ? ORDER BY `id` ASC;
-- params: [1]
SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at` FROM `t_post` t WHERE t.`status` = ? AND t.`author_id` IN (?,?,?,?) ORDER BY t.`views` DESC;
-- params: ['published', 1, 2, 7, 8]
```
<!-- /card -->

<!-- card: include/IncludeOneAndManyToMany sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Views >= 2000).OrderBy(p => p.Id)
    .Include(p => p.Author)
    .Include(p => p.Tags)
    .ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`author_id`,`category_id`,`title`,`status`,`views`,`published_at` FROM `t_post` WHERE `views` >= ? ORDER BY `id` ASC;
-- params: [2000]
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE t.`id` IN (?,?,?);
-- params: [2, 3, 9]
SELECT `post_id`,`tag_id` FROM `t_post_tag` WHERE `post_id` IN (?,?,?);
-- params: [3, 4, 9]
SELECT t.`id`,t.`name` FROM `t_tag` t WHERE t.`id` IN (?,?);
-- params: [3, 2]
```
<!-- /card -->

<!-- card: include/IncludeNested sql=mysql -->
```csharp
db.Query<Order>().Where(o => o.UserId == 1).OrderBy(o => o.Id)
    .Include(o => o.Items, q => q.Include(i => i.Product))
    .ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`user_id`,`status`,`created_at` FROM `t_order` WHERE `user_id` = ? ORDER BY `id` ASC;
-- params: [1]
SELECT t.`order_id`,t.`product_id`,t.`quantity`,t.`price` FROM `t_order_item` t WHERE t.`order_id` IN (?,?);
-- params: [1, 2]
SELECT t.`id`,t.`name`,t.`price`,t.`stock` FROM `t_product` t WHERE t.`id` IN (?,?,?);
-- params: [1, 6, 3]
```
<!-- /card -->

### 每个父对象只取几条

配置里 `Take(n)` / `Skip(n)` 作用在每个父对象上（不是整个结果）。默认（`IncludeLimitMode.Partition`）一条查询按外键分区编行号：

<!-- card: include/IncludeTake sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
    .Include(u => u.Posts, q => q.OrderByDescending(p => p.Views).Take(1))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
SELECT t2.`id` AS `id`,t2.`author_id` AS `author_id`,t2.`category_id` AS `category_id`,t2.`title` AS `title`,t2.`status` AS `status`,t2.`views` AS `views`,t2.`published_at` AS `published_at` FROM (SELECT t1.`id` AS `id`,t1.`author_id` AS `author_id`,t1.`category_id` AS `category_id`,t1.`title` AS `title`,t1.`status` AS `status`,t1.`views` AS `views`,t1.`published_at` AS `published_at` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t.`author_id` ORDER BY t.`views` DESC)) AS `__row__` FROM `t_post` t WHERE t.`author_id` IN (?,?,?,?,?,?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2;
-- params: [1, 2, 3, 4, 5, 7, 8, 9, 10, 1, 1]
```
<!-- /card -->

`EasyDbOptions.IncludeLimitMode = IncludeLimitMode.UnionAll` 时每个父对象一段带 LIMIT 的子查询、`UNION ALL` 合并（对应 Java 的 `includeLimitMode`），用于不支持窗口函数的库：

<!-- snippet: include-union-all -->
```csharp
var db = new EasyDb(o =>
{
    o.UseMySql(dataSource);
    o.IncludeLimitMode = IncludeLimitMode.UnionAll;   // 每个父对象一段 (… ORDER BY … LIMIT n)，UNION ALL 合并
});
```
<!-- /snippet -->

分支的写法按方言：默认与 Java 一致写成 `(… LIMIT n)`；SQLite 的分支不能加括号、也不能带 ORDER BY / LIMIT，写成 `SELECT * FROM (…)`；SQL Server 括号里不能带 ORDER BY，写成 `(SELECT * FROM (…) t)`：

<!-- card: include/IncludeTakeUnionAll sql=mysql,sqlite -->
```csharp
DocsDb.Reconfigure(db, o => o.IncludeLimitMode = IncludeLimitMode.UnionAll)
    .Query<User>().Where(u => u.Id <= 2).OrderBy(u => u.Id)
    .Include(u => u.Posts, q => q.OrderByDescending(p => p.Views).Take(1))
    .ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `id` <= ? ORDER BY `id` ASC;
-- params: [2]
SELECT t1.`id`,t1.`author_id`,t1.`category_id`,t1.`title`,t1.`status`,t1.`views`,t1.`published_at` FROM ( (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at` FROM `t_post` t WHERE t.`author_id` = ? ORDER BY t.`views` DESC LIMIT 1)  UNION ALL  (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at` FROM `t_post` t WHERE t.`author_id` = ? ORDER BY t.`views` DESC LIMIT 1) ) t1;
-- params: [1, 2]
```

```sql
-- SQLite
SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "id" <= ? ORDER BY "id" ASC;
-- params: [2]
SELECT t1."id",t1."author_id",t1."category_id",t1."title",t1."status",t1."views",t1."published_at" FROM ( SELECT * FROM (SELECT t."id",t."author_id",t."category_id",t."title",t."status",t."views",t."published_at" FROM "t_post" t WHERE t."author_id" = ? ORDER BY t."views" DESC LIMIT 1)  UNION ALL  SELECT * FROM (SELECT t."id",t."author_id",t."category_id",t."title",t."status",t."views",t."published_at" FROM "t_post" t WHERE t."author_id" = ? ORDER BY t."views" DESC LIMIT 1) ) t1;
-- params: [1, 2]
```
<!-- /card -->

导航上声明的 `[Navigate(OrderBy = "…", Limit = n, Offset = m)]` 是带出时的默认排序和条数；带出查询的配置里写 `q.IgnoreNavigationDefaults()` 时不用它们（自己写了排序、条数时本来就以自己的为准）。

### 分批与后补

<!-- card: include/IncludeGroupSize sql=mysql -->
```csharp
db.Query<Company>().OrderBy(c => c.Id).IncludeGroupSize(2).Include(c => c.Users).ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`city` FROM `t_company` ORDER BY `id` ASC;
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE t.`company_id` IN (?,?);
-- params: [1, 2]
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE t.`company_id` IN (?,?);
-- params: [3, 4]
```
<!-- /card -->

已经查出来的实体，用 `db.LoadIncludeAsync(实体, 导航, 配置)` 补上导航：

<!-- card: include/LoadInclude sql=mysql -->
```csharp
var posts = await db.Query<Post>().Where(p => p.AuthorId == 1).ToListAsync();
await db.LoadIncludeAsync(posts, p => p.Comments, q => q.Where(c => c.Likes >= 5));
return posts;
```

```sql
-- MySQL
SELECT `id`,`author_id`,`category_id`,`title`,`status`,`views`,`published_at` FROM `t_post` WHERE `author_id` = ?;
-- params: [1]
SELECT `id`,`post_id`,`author_id`,`parent_id`,`body`,`likes` FROM `t_comment` WHERE `likes` >= ? AND `post_id` IN (?,?);
-- params: [5, 1, 2]
```
<!-- /card -->

`[IncludeOn(属性, 值)]` 标在导航上：只有父对象的这个属性等于这个值时才带出，其余父对象的导航保持 null，适合按类型字段挂不同扩展表的模型（对应 Java 的 `@IncludeOnProperty`）。

## 投影里的对象树

投影里写集合导航的 `Select(..).ToList()`，可以先 `Where` / `OrderBy`；对一导航的列走主查询的 JOIN：

<!-- card: include/NestedProjection sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id).Take(3)
    .Select(u => new AuthorPosts(u.Name, u.Company!.Name, u.Posts!.Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views).Select(p => new PostItem(p.Title, p.Views)).ToList()))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t2.`name` AS `company`,t.`id` AS `__relation__id` FROM `t_user` t LEFT JOIN `t_company` t2 ON t2.`id` = t.`company_id` WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC LIMIT 3;
SELECT t.`title`,t.`views`,t.`author_id` AS `__relation__authorId` FROM `t_post` t WHERE t.`status` = ? AND t.`author_id` IN (?,?,?) ORDER BY t.`views` DESC;
-- params: ['published', 1, 2, 3]
```
<!-- /card -->

需要隐藏的键（父主键、子外键）由规划器自动补上（`__relation__…` 列），物化后丢掉。

## 按 DTO 结构自动带出

`Select<TDto>()`：同名属性取列；与导航同名的属性，类型是嵌套 DTO（或 `List<嵌套 DTO>`）时沿导航递归映射，类型就是实体本身时整个实体带出。嵌套 DTO 实现 `IAutoIncludeConfigure<实体>` 时，用它的 `Configure` 配置带出查询（对应 Java DTO 上的 `EXTRA_AUTO_INCLUDE_CONFIGURE`）：

<!-- card: include/AutoInclude sql=mysql -->
```csharp
db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id).Select<UserVo>().ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`company_id` AS `__relation__companyId`,t.`id` AS `__relation__id` FROM `t_user` t WHERE t.`company_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
SELECT t.`name`,t.`city`,t.`id` AS `__relation__id` FROM `t_company` t WHERE t.`id` IN (?);
-- params: [1]
SELECT t2.`title` AS `title`,t2.`views` AS `views`,t2.`author_id` AS `__relation__authorId` FROM (SELECT t1.`id` AS `id`,t1.`author_id` AS `author_id`,t1.`category_id` AS `category_id`,t1.`title` AS `title`,t1.`status` AS `status`,t1.`views` AS `views`,t1.`published_at` AS `published_at` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t.`author_id` ORDER BY t.`views` DESC)) AS `__row__` FROM `t_post` t WHERE t.`status` = ? AND t.`author_id` IN (?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2;
-- params: ['published', 1, 2, 7, 8, 1, 2]
```
<!-- /card -->

DTO 属性上的 `[Navigate(OrderBy, Limit, Offset)]` 覆盖实体导航上声明的排序与条数，只对这次投影生效：

<!-- card: include/DtoNavigateLimit sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Comments!.Any()).OrderBy(p => p.Id).Select<PostWithTopComments>().ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`id` AS `__relation__id` FROM `t_post` t WHERE EXISTS(SELECT 1 FROM `t_comment` t1 WHERE t1.`post_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
SELECT t2.`body` AS `body`,t2.`likes` AS `likes`,t2.`post_id` AS `__relation__postId` FROM (SELECT t1.`id` AS `id`,t1.`post_id` AS `post_id`,t1.`author_id` AS `author_id`,t1.`parent_id` AS `parent_id`,t1.`body` AS `body`,t1.`likes` AS `likes` FROM (SELECT t.`id`,t.`post_id`,t.`author_id`,t.`parent_id`,t.`body`,t.`likes`,(ROW_NUMBER() OVER (PARTITION BY t.`post_id` ORDER BY t.`likes` DESC)) AS `__row__` FROM `t_comment` t WHERE t.`post_id` IN (?,?,?,?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2;
-- params: [1, 3, 4, 6, 7, 9, 11, 1, 2]
```
<!-- /card -->

- 平铺导航路径上的值用 `[MapPath]`，额外的计算列用 `[MapExpression]`，见[查询](queries.md#映射到-dto)。
- `WithConfigureArgument(arg)` 把一个参数传给各层的 `Configure`（在里面读 `query.ConfigureArgument`）。
- 先分页再投影：`ToPageAsync(page, size, q => q.Select<TDto>())`，带出只对这一页执行：

<!-- card: query/basics/PageThenProject sql=mysql -->
```csharp
db.Query<Post>().OrderBy(p => p.Id).ToPageAsync(1, 2, q => q.Select<PostAndAuthor>());
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_post`;
SELECT t1.`title`,t1.`author_id` AS `__relation__authorId` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at` FROM `t_post` t ORDER BY t.`id` ASC LIMIT 2) t1;
SELECT t.`name`,t.`id` AS `__relation__id` FROM `t_user` t WHERE t.`id` IN (?);
-- params: [1]
```
<!-- /card -->

- `Include(导航, 配置)` 之后再投影，配置并入投影里同名导航的带出。

## 按键填充

两边没有声明导航时，用 `FillMany` / `FillOne` 按键把另一条查询的结果填进当前结果（对应 Java 的 `fillMany` / `fillOne`）：

<!-- card: include/FillMany sql=mysql -->
```csharp
db.Query<Order>().Where(o => o.UserId == 1).OrderBy(o => o.Id).Select<OrderSheet>()
    .FillMany(() => db.Query<OrderItem>(), i => i.OrderId, o => o.Id, (o, lines) => o.Lines = lines)
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`status` FROM `t_order` t WHERE t.`user_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
SELECT `order_id`,`product_id`,`quantity`,`price` FROM `t_order_item` WHERE `order_id` IN (?,?);
-- params: [1, 2]
```
<!-- /card -->

## 树

自关联的实体（`Department.Children`、`Category.Children`）：

- `AsTreeCte(o => …)`：用递归 CTE 从当前查询的行出发取全部下级（`o.Up = true` 取全部上级），`o.LimitDeep = n` 只取到第 n 层，`childFilter` 只过滤递归走到的行。节点键不是导航时用 `AsTreeCte(c => c.Id, c => c.ParentId)`。之后照常 Where / Select。
- `ToTreeAsync()`：读出后按父子键拼成树，子节点挂到指向自身的集合导航上。

<!-- card: include/TreeCte sql=mysql -->
```csharp
db.Query<Department>().Where(d => d.Id == 1).AsTreeCte(o => o.LimitDeep = 1)
    .OrderBy(d => d.Id).Select(d => new DepartmentDepth(d.Name, d.ParentId)).ToListAsync();
```

```sql
-- MySQL
WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`company_id`,t1.`parent_id`,t1.`name` FROM `t_department` t1 WHERE t1.`id` = ?)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`company_id`,t3.`parent_id`,t3.`name` FROM `as_tree_cte` t2 INNER JOIN `t_department` t3 ON t3.`parent_id` = t2.`id`) ) SELECT t.`name`,t.`parent_id` FROM `as_tree_cte` t WHERE t.`cte_deep` <= ? ORDER BY t.`id` ASC;
-- params: [1, 1]
```
<!-- /card -->

<!-- card: include/TreeUp sql=mysql,oracle -->
```csharp
db.Query<Category>().Where(c => c.Name == "PostgreSQL").AsTreeCte(o => o.Up = true).OrderBy(c => c.Id).Select(c => c.Name).ToListAsync();
```

```sql
-- MySQL
WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`parent_id`,t1.`name` FROM `t_category` t1 WHERE t1.`name` = ?)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`parent_id`,t3.`name` FROM `as_tree_cte` t2 INNER JOIN `t_category` t3 ON t3.`id` = t2.`parent_id`) ) SELECT t.`name` FROM `as_tree_cte` t ORDER BY t.`id` ASC;
-- params: ['PostgreSQL']
```

```sql
-- Oracle
WITH "as_tree_cte" ("cte_deep","id","parent_id","name") AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "t_category" t1 WHERE t1."name" = ?)  UNION ALL  (SELECT t2."cte_deep" + 1 AS "cte_deep",t3."id",t3."parent_id",t3."name" FROM "as_tree_cte" t2 INNER JOIN "t_category" t3 ON t3."id" = t2."parent_id") ) SELECT t."name" FROM "as_tree_cte" t ORDER BY t."id" ASC;
-- params: ['PostgreSQL']
```
<!-- /card -->

<!-- card: query/basics/DepartmentTree sql=mysql -->
```csharp
var roots = await db.Query<Department>().Where(d => d.Name == "研发部").AsTreeCte().ToTreeAsync();
static object Node(Department d) => new { d.Name, Children = d.Children!.Select(Node).ToList() };
return roots.Select(Node).ToList();
```

```sql
-- MySQL
WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`company_id`,t1.`parent_id`,t1.`name` FROM `t_department` t1 WHERE t1.`name` = ?)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`company_id`,t3.`parent_id`,t3.`name` FROM `as_tree_cte` t2 INNER JOIN `t_department` t3 ON t3.`parent_id` = t2.`id`) ) SELECT t.`id`,t.`company_id`,t.`parent_id`,t.`name`,t.`cte_deep` FROM `as_tree_cte` t;
-- params: ['研发部']
```
<!-- /card -->

<!-- card: include/ToTree sql=mysql -->
```csharp
var roots = await db.Query<Category>().ToTreeAsync();
static object Node(Category c) => new { c.Name, Children = c.Children!.Select(Node).ToList() };
return roots.Select(Node).ToList();
```

```sql
-- MySQL
SELECT `id`,`parent_id`,`name` FROM `t_category`;
```
<!-- /card -->

## 限制

- 投影里的集合不支持 `Take` / `Skip`（`u.Posts!.OrderBy(..).Take(2).Select(..).ToList()` 报不支持）：每个父对象取几条用 `Include(.., q => q.Take(n))`、DTO 属性上的 `[Navigate(Limit = n)]` 或 `IAutoIncludeConfigure`。
- 投影里有带出时不能流式读取（`AsAsyncEnumerable()` 报错），要用 `ToListAsync()`。
- 多次查询之间数据可能被改，需要一致的快照时把整组查询放进同一个事务。
- 不做"一条 SQL 用 JSON 聚合出整棵树"，理由见 [0002](../decisions/0002-object-tree-and-dto.md#3-执行策略对一用-join对多分层批量查询)。
- 自动映射遇到引用自身的 DTO（`MenuVo.Children : List<MenuVo>`）不展开，交给 `ToTreeAsync()` 拼树。
- 递归 CTE 需要数据库支持：HANA 没有递归 CTE。

## 对应 Java

| Java | C# |
|---|---|
| `include(o -> o.bankCards())` / `includes(..)` | `Include(o => o.BankCards)` |
| `include(o -> o.bankCards(), q -> q.where(..).limit(2))` | `Include(o => o.BankCards, q => q.Where(..).Take(2))` |
| include 里再 include / `include2` | 配置里再 `Include` |
| `includeLimitMode`、`relationGroupSize` | `EasyDbOptions.IncludeLimitMode`、`IncludeGroupSize` / `.IncludeGroupSize(n)` |
| `selectAutoInclude(Dto.class)` | `Select<TDto>()` |
| DTO 上的 `EXTRA_AUTO_INCLUDE_CONFIGURE` | DTO 实现 `IAutoIncludeConfigure<TEntity>` |
| DTO 上的 `@Navigate(orderByProps, limit, offset)` / `ignoreNavigateConfigure` | DTO 属性上的 `[Navigate(OrderBy, Limit, Offset)]` / `IgnoreNavigationDefaults()` |
| `@NavigateFlat` / `@NavigateJoin` | `[MapPath("A.B")]` / `[MapPath("A.B", Join = true)]` |
| `configure(arg)` 传给带出配置 | `WithConfigureArgument(arg)` |
| `toPageSelectResult` | `ToPageAsync(page, size, q => q.Select<TDto>())` |
| `fillMany` / `fillOne` | `FillMany` / `FillOne` |
| `@IncludeOnProperty` | `[IncludeOn(属性, 值)]` |
| `asTreeCTE(..)` / `asTreeCTECustom(..)` / `toTreeList()` | `AsTreeCte(..)` / `AsTreeCte(self, parent)` / `ToTreeAsync()` |
| `TreeCTEConfigurer`：`setUp`、`setLimitDeep`、`setChildFilter` | `TreeCteOptions`：`Up`、`LimitDeep`；`childFilter` 参数 |
