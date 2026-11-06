# 分组与聚合

`GroupBy(键)` 之后得到 `GroupQuery<TKey, TElement>`：`Having` 过滤组，`OrderBy` 按键或聚合排序，`Select(g => …)` 投影，投影之后就是普通的查询（可以再 `Where`、分页、作为子查询或 JOIN 来源）。组 `g` 上的成员只能写在这几个 lambda 里，由翻译器转成 GROUP BY 和聚合函数。

| 写法 | SQL |
|---|---|
| `g.Key`；多列时 `GroupBy(x => new { x.A, x.B })`，`g.Key.A` | 分组键 |
| `g.Count()` / `g.Count(x => 条件)` / `g.Count(x => x.列)` | `COUNT(*)` / `COUNT(CASE WHEN 条件 THEN 1 END)` / `COUNT(列)` |
| `g.Sum(..)` / `Max` / `Min` / `Average` | 聚合 |
| `g.Distinct().Count(x => x.列)` 等 | `COUNT(DISTINCT 列)` |
| `g.Where(x => 条件).Sum(..)` | 组内只统计满足条件的行（值包进 CASE WHEN） |
| `g.StringJoin(x => x.列, "、")` | 字符串聚合（`GROUP_CONCAT` / `STRING_AGG` / `LISTAGG`） |
| `g.Column(x => x.列)` | 组内的列本身，不聚合（只在数据库允许时可用） |
| `g.Grouping(g.Key.A)` | 配合 `Rollup()` / `Cube()`：小计行里被汇总掉的键为 1 |

<!-- card: group/GroupBy sql=mysql -->
```csharp
db.Query<OrderItem>().GroupBy(i => i.OrderId)
    .Select(g => new OrderTotal(g.Key, g.Count(), g.Sum(i => i.Quantity), g.Sum(i => i.Price * i.Quantity)))
    .OrderBy(r => r.OrderId).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`order_id` AS `order_id`,t1.`lines` AS `lines`,t1.`quantity` AS `quantity`,t1.`amount` AS `amount` FROM (SELECT t.`order_id` AS `order_id`,COUNT(*) AS `lines`,SUM(t.`quantity`) AS `quantity`,SUM((t.`price` * t.`quantity`)) AS `amount` FROM `t_order_item` t GROUP BY t.`order_id`) t1 ORDER BY t1.`order_id` ASC;
```
<!-- /card -->

<!-- card: group/Having sql=mysql -->
```csharp
db.Query<OrderItem>().GroupBy(i => i.OrderId)
    .Having(g => g.Count() >= 2)
    .OrderByDescending(g => g.Sum(i => i.Price * i.Quantity))
    .Select(g => new OrderTotal(g.Key, g.Count(), g.Sum(i => i.Quantity), g.Sum(i => i.Price * i.Quantity)))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`order_id` AS `order_id`,COUNT(*) AS `lines`,SUM(t.`quantity`) AS `quantity`,SUM((t.`price` * t.`quantity`)) AS `amount` FROM `t_order_item` t GROUP BY t.`order_id` HAVING COUNT(*) >= ? ORDER BY SUM((t.`price` * t.`quantity`)) DESC;
-- params: [2]
```
<!-- /card -->

<!-- card: group/MultipleKeys sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.AuthorId <= 3).GroupBy(p => new { p.AuthorId, p.Status })
    .Select(g => new AuthorStatusCount(g.Key.AuthorId, g.Key.Status, g.Count()))
    .OrderBy(r => r.AuthorId).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`author_id` AS `author_id`,t1.`status` AS `status`,t1.`posts` AS `posts` FROM (SELECT t.`author_id` AS `author_id`,t.`status` AS `status`,COUNT(*) AS `posts` FROM `t_post` t WHERE t.`author_id` <= ? GROUP BY t.`author_id`,t.`status`) t1 ORDER BY t1.`author_id` ASC;
-- params: [3]
```
<!-- /card -->

键和聚合里都能用导航（隐式 JOIN）：

<!-- card: group/ByNavigation sql=mysql,pgsql,mssql,oracle -->
```csharp
db.Query<Post>().Where(p => p.Author!.CompanyId != null).GroupBy(p => p.Author!.Company!.Name)
    .Select(g => new CompanyPosts(g.Key, g.Count(), g.Count(p => p.Status == PostStatus.Draft), g.Max(p => (int?)p.Views), g.StringJoin(p => p.Title, "、")))
    .OrderBy(r => r.Company).ToListAsync();
```

```sql
-- MySQL
SELECT t3.`company` AS `company`,t3.`posts` AS `posts`,t3.`drafts` AS `drafts`,t3.`best` AS `best`,t3.`titles` AS `titles` FROM (SELECT t2.`name` AS `company`,COUNT(*) AS `posts`,COUNT((CASE WHEN t.`status` = ? THEN ? ELSE NULL END)) AS `drafts`,MAX(t.`views`) AS `best`,GROUP_CONCAT(t.`title` SEPARATOR ?) AS `titles` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` LEFT JOIN `t_company` t2 ON t2.`id` = t1.`company_id` WHERE t1.`company_id` IS NOT NULL GROUP BY t2.`name`) t3 ORDER BY t3.`company` ASC;
-- params: ['draft', 1, '、']
```

```sql
-- PostgreSQL
SELECT t3."company" AS "company",t3."posts" AS "posts",t3."drafts" AS "drafts",t3."best" AS "best",t3."titles" AS "titles" FROM (SELECT t2."name" AS "company",COUNT(*) AS "posts",COUNT((CASE WHEN t."status" = ? THEN ? ELSE NULL END)) AS "drafts",MAX(t."views") AS "best",STRING_AGG((t."title")::TEXT, ?) AS "titles" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" LEFT JOIN "t_company" t2 ON t2."id" = t1."company_id" WHERE t1."company_id" IS NOT NULL GROUP BY t2."name") t3 ORDER BY t3."company" ASC;
-- params: ['draft', 1, '、']
```

```sql
-- SQL Server
SELECT t3.[company] AS [company],t3.[posts] AS [posts],t3.[drafts] AS [drafts],t3.[best] AS [best],t3.[titles] AS [titles] FROM (SELECT t2.[name] AS [company],COUNT(*) AS [posts],COUNT((CASE WHEN t.[status] = ? THEN ? ELSE NULL END)) AS [drafts],MAX(t.[views]) AS [best],STRING_AGG(t.[title], ?) AS [titles] FROM [t_post] t LEFT JOIN [t_user] t1 ON t1.[id] = t.[author_id] LEFT JOIN [t_company] t2 ON t2.[id] = t1.[company_id] WHERE t1.[company_id] IS NOT NULL GROUP BY t2.[name]) t3 ORDER BY t3.[company] ASC;
-- params: ['draft', 1, '、']
```

```sql
-- Oracle
SELECT t3."company" AS "company",t3."posts" AS "posts",t3."drafts" AS "drafts",t3."best" AS "best",t3."titles" AS "titles" FROM (SELECT t2."name" AS "company",COUNT(*) AS "posts",COUNT((CASE WHEN t."status" = ? THEN ? ELSE NULL END)) AS "drafts",MAX(t."views") AS "best",LISTAGG(TO_CHAR(t."title"), ?) WITHIN GROUP(ORDER BY 1) AS "titles" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" LEFT JOIN "t_company" t2 ON t2."id" = t1."company_id" WHERE t1."company_id" IS NOT NULL GROUP BY t2."name") t3 ORDER BY t3."company" ASC;
-- params: ['draft', 1, '、']
```
<!-- /card -->

<!-- card: group/CountDistinct sql=mysql -->
```csharp
db.Query<User>().Where(u => u.CompanyId != null).GroupBy(u => u.Company!.City)
    .Select(g => new CityCompanies(g.Key, g.Distinct().Count(u => u.CompanyId), g.Count()))
    .OrderBy(r => r.City).ToListAsync();
```

```sql
-- MySQL
SELECT t2.`city` AS `city`,t2.`companies` AS `companies`,t2.`users` AS `users` FROM (SELECT t1.`city` AS `city`,COUNT(DISTINCT t.`company_id`) AS `companies`,COUNT(*) AS `users` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t.`company_id` IS NOT NULL GROUP BY t1.`city`) t2 ORDER BY t2.`city` ASC;
```
<!-- /card -->

只要分组键时用 `Keys()`：

<!-- card: group/Keys sql=mysql -->
```csharp
db.Query<Comment>().GroupBy(c => c.PostId).Having(g => g.Sum(c => c.Likes) >= 20).OrderBy(g => g.Key).Keys().ToListAsync();
```

```sql
-- MySQL
SELECT `post_id` FROM `t_comment` GROUP BY `post_id` HAVING SUM(`likes`) >= ? ORDER BY `post_id` ASC;
-- params: [20]
```
<!-- /card -->

分组之后再按聚合结果过滤，也可以先投影、再 `Where`（包成派生表）：

<!-- card: query/DerivedTable sql=mysql -->
```csharp
db.Query<Post>().GroupBy(p => p.AuthorId).Select(g => new AuthorHot(g.Key, g.Count(p => p.Views >= 1000)))
    .Where(r => r.Hot >= 2).OrderBy(r => r.AuthorId).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`author_id` AS `author_id`,t1.`hot` AS `hot` FROM (SELECT t.`author_id` AS `author_id`,COUNT((CASE WHEN t.`views` >= ? THEN ? ELSE NULL END)) AS `hot` FROM `t_post` t GROUP BY t.`author_id`) t1 WHERE t1.`hot` >= ? ORDER BY t1.`author_id` ASC;
-- params: [1000, 1, 2]
```
<!-- /card -->

## 小计：ROLLUP、CUBE

`GroupBy(..).Rollup()` / `.Cube()`，`g.Grouping(键)` 区分小计行：

<!-- snippet: group-rollup -->
```csharp
db.Query<OrderItem>().GroupBy(i => new { i.Order!.UserId, i.ProductId }).Rollup()
    .Select(g => new RollupRow(g.Key.UserId, g.Key.ProductId, g.Grouping(g.Key.ProductId) == 1, g.Sum(i => i.Price * i.Quantity)));
```
<!-- /snippet -->

MySQL 不支持 CUBE，SQLite 两者都不支持，渲染时报 `NotSupportedException`。

## 整表聚合

不分组时用终结方法，空表时 `SumAsync` 返回 0 或 null（按结果类型），`AverageAsync` 返回 `double?`（金额用 `AverageDecimalAsync`）：

<!-- card: group/Aggregates sql=mysql -->
```csharp
var published = db.Query<Post>().Where(p => p.Status == PostStatus.Published);
return new Totals(
    await published.SumAsync(p => p.Views),
    await published.MaxAsync(p => p.Views),
    await published.AverageAsync(p => p.Views),
    await published.CountAsync());
```

```sql
-- MySQL
SELECT SUM(t.`views`) FROM `t_post` t WHERE t.`status` = ?;
-- params: ['published']
SELECT MAX(t.`views`) FROM `t_post` t WHERE t.`status` = ?;
-- params: ['published']
SELECT AVG(t.`views`) FROM `t_post` t WHERE t.`status` = ?;
-- params: ['published']
SELECT COUNT(*) FROM `t_post` WHERE `status` = ?;
-- params: ['published']
```
<!-- /card -->

集合导航上的聚合（每个用户的帖子数）不需要分组，见[联表与导航](joins-and-navigations.md#集合导航隐式子查询)。

## 限制

- `g.Column(..)` 选出的非聚合列只在数据库允许时可用（MySQL 关闭 `ONLY_FULL_GROUP_BY`，或该列函数依赖于分组键）。
- MySQL 没有 CUBE；SQLite 没有 ROLLUP / CUBE；HANA 的字符串聚合不能去重；TDengine 没有字符串聚合。
- `Take` 之后再聚合（`q.Take(10).SumAsync(..)`）会先包成派生表，聚合的是这 10 行。

## 对应 Java

| Java | C# |
|---|---|
| `groupBy(o -> GroupKeys.of(o.a(), o.b()))` | `GroupBy(o => new { o.A, o.B })` |
| `group.key1()` / `key2()` | `g.Key` / `g.Key.B` |
| `group.count()` / `sum(..)` / `max(..)` / `avg(..)` | `g.Count()` / `Sum(..)` / `Max(..)` / `Average(..)` |
| `group.where(..).sum(..)`、`count(x -> 条件)` | `g.Where(..).Sum(..)`、`g.Count(x => 条件)` |
| `group.distinct().count(..)` | `g.Distinct().Count(..)` |
| `group.joining(..)` / `groupTable()` | `g.StringJoin(..)` / `g.Column(..)` |
| `having(..)` | `Having(..)` |
| `select(group -> Select.DRAFT.of(..))` | `Select(g => new …(..))` |
| fork 的 rollup / cube / grouping | `Rollup()` / `Cube()` / `g.Grouping(..)` |
| `sumOrNull` / `maxOrNull` / `avgOrNull` / `count()` | `SumAsync` / `MaxAsync` / `AverageAsync` / `CountAsync` |
