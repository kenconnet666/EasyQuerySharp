# 查询

`db.Query<T>()` 开始一条查询，链式调用只记录 lambda，终结方法（`ToListAsync` 等）才翻译和执行。查询对象不可变，可以存进变量反复用、在上面接着加条件。本页讲单表查询的条件、排序分页、投影与 DTO、子查询、合并与 CTE、流式读取；JOIN 与导航见[联表与导航](joins-and-navigations.md)，函数见[函数](functions.md)，分组见[分组与聚合](grouping.md)。

## 终结方法

| 方法 | 结果 | 查不到时 |
|---|---|---|
| `ToListAsync()` | `List<T>` | 空列表 |
| `FirstOrDefaultAsync()` / `SingleOrDefaultAsync()` | `T?` | null |
| `FirstAsync()` / `SingleAsync()` | `T` | 抛 `NotFoundException`；可传消息或异常工厂 `FirstAsync("用户不存在")` |
| `FindAsync(id)` / `FindOrThrowAsync(id, 消息?)` | 按主键取一行 | null / 抛 `NotFoundException` |
| `AnyAsync()` / `EnsureAnyAsync(消息?)` | 有没有 | false / 抛 `NotFoundException` |
| `CountAsync()`、`SumAsync` / `MaxAsync` / `MinAsync` / `AverageAsync` | 聚合值 | — |
| `ToPageAsync(page, size)` | `Page<T>`：`Rows`、`Total`、`PageIndex`、`PageSize`、`PageCount` | — |
| `AsAsyncEnumerable()` / `ToChunksAsync(n)` | 流式、分块读取 | — |
| `ToRowsAsync()` / `FirstRowOrDefaultAsync()` | 读成 `DynamicRow`（按列名取值） | — |
| `ToSql()` / `ToStatement()` | 只生成 SQL，不执行 | — |

`SingleAsync` 查到多行时抛 `InvalidOperationException`。没有消息时，`NotFoundException` 的消息取 `[Table(NotFoundMessage = ..)]`。

<!-- card: query/basics/FirstMissing sql=mysql -->
```csharp
var first = await db.Query<User>().Where(u => u.Name == "不存在的人").FirstOrDefaultAsync();
var found = await db.Query<User>().FindAsync(999L);
return new { FirstIsNull = first is null, FoundIsNull = found is null };
```

```sql
-- MySQL
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `name` = ? LIMIT 1;
-- params: ['不存在的人']
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `id` = ?;
-- params: [999]
```
<!-- /card -->

<!-- card: query/basics/FindThrows sql=mysql -->
```csharp
try
{
    await db.Query<User>().FindOrThrowAsync(999L, "用户不存在");
    return "found";
}
catch (NotFoundException ex)
{
    return ex.Message;
}
```

```sql
-- MySQL
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `id` = ?;
-- params: [999]
```
<!-- /card -->

<!-- card: query/basics/CountAndAny sql=mysql -->
```csharp
new
{
Adults = await db.Query<User>().Where(u => u.Age >= 18).CountAsync(),
HasDrafts = await db.Query<Post>().Where(p => p.Status == PostStatus.Draft).AnyAsync(),
};
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_user` WHERE `age` >= ?;
-- params: [18]
SELECT  1  FROM `t_post` WHERE `status` = ? LIMIT 1;
-- params: ['draft']
```
<!-- /card -->

## 条件

`Where` 里写普通的 C# 表达式：比较、`&&` / `||` / `!`、`== null`、三元表达式，闭包变量都参数化。多个 `Where` 之间是 AND：

<!-- card: query/basics/WhereChain sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Age >= 18).Where(u => u.CompanyId == 1).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE t.`age` >= ? AND t.`company_id` = ?;
-- params: [18, 1]
```
<!-- /card -->

<!-- card: query/basics/CompareColumns sql=mysql -->
```csharp
db.Query<Comment>().Where(c => c.AuthorId == c.Post!.AuthorId).Select(c => new CommentBy(c.Body, c.Author!.Name)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`body`,t2.`name` AS `by` FROM `t_comment` t LEFT JOIN `t_post` t1 ON t1.`id` = t.`post_id` LEFT JOIN `t_user` t2 ON t2.`id` = t.`author_id` WHERE t.`author_id` = t1.`author_id`;
```
<!-- /card -->

<!-- card: query/basics/NullChecks sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Age == null || u.CompanyId == null).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE (t.`age` IS NULL OR t.`company_id` IS NULL);
```
<!-- /card -->

### 动态条件

`WhereIf(条件, ..)` 在条件成立时才加；全局或单条查询上的 `UseValueFilter` 让值为 null / 空白的比较整个跳过（对应 Java 的 `filterConfigure`）：

<!-- card: query/basics/WhereIf sql=mysql -->
```csharp
string? keyword = "数据";
int? minViews = null;
return db.Query<Post>()
    .WhereIf(keyword is not null, p => p.Title.Contains(keyword!))
    .WhereIf(minViews is not null, p => p.Views >= minViews!.Value)
    .Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t WHERE t.`title` LIKE ?;
-- params: ['%数据%']
```
<!-- /card -->

<!-- card: query/ValueFilter sql=mysql -->
```csharp
string? name = null;
var city = "杭州";
return db.Query<User>().UseValueFilter(Filtering.ValueFilter.NotNullOrBlank)
    .Where(u => u.Name == name && u.Company!.City == city)
    .OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t1.`city` = ? ORDER BY t.`id` ASC;
-- params: ['杭州']
```
<!-- /card -->

表单驱动的筛选（每个属性一个条件）用 `FilterBy(请求对象)`，见[动态筛选与列表页](filter.md)。

### LIKE、IN、区间

| 写法 | SQL | 说明 |
|---|---|---|
| `s.Contains(x)` / `StartsWith` / `EndsWith` | `LIKE ?`，`%` 在参数里 | 值里的 `%`、`_` 按字面匹配（自动转义） |
| `s.LikeContains(x)` / `LikeStartsWith` / `LikeEndsWith` | `LIKE CONCAT('%',?,'%')` | 对应 Java 的 `contains` / `startsWith` / `endsWith` |
| `s.Like("a%b")` / `NotLike` | `LIKE ?` | 通配符自己写 |
| `ids.Contains(x)`、`x.In(a, b)`、`x.NotIn(..)` | `IN (?,?)` | 集合为空时恒假；超过 `MaxInClauseSize` 拆成 OR |
| `x.In(子查询)` / `Sql.Exists(子查询)` | `IN (SELECT …)` / `EXISTS (…)` | 子查询里可以引用外层的列 |
| `x.RangeClosed(a, b)`、`RangeOpen`、`RangeClosedOpen`、`RangeOpenClosed` | `x >= ? AND x <= ?` 等 | 区间 |
| `WhereById(id)` / `WhereByIds(ids)` | `id = ?` / `id IN (…)` | 按主键 |

<!-- card: query/basics/LikeFamily sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Title.Contains("缓存") || p.Title.StartsWith("把") || p.Title.EndsWith("入门")).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t WHERE (t.`title` LIKE ? OR t.`title` LIKE ? OR t.`title` LIKE ?);
-- params: ['%缓存%', '把%', '%入门']
```
<!-- /card -->

<!-- card: query/basics/InList sql=mysql -->
```csharp
long[] companies = [1, 3];
return db.Query<User>().Where(u => companies.Contains(u.CompanyId!.Value) && !new[] { UserLevel.Lead }.Contains(u.Level)).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE t.`company_id` IN (?,?) AND t.`level` NOT IN (?);
-- params: [1, 3, 'lead']
```
<!-- /card -->

<!-- card: query/RangeAndIn sql=mysql -->
```csharp
db.Query<User>()
    .Where(u => u.Age!.Value.RangeClosed(25, 35) && u.Level.In(UserLevel.Senior, UserLevel.Lead))
    .OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE (t.`age` >= ? AND t.`age` <= ?) AND t.`level` IN (?,?) ORDER BY t.`id` ASC;
-- params: [25, 35, 'senior', 'lead']
```
<!-- /card -->

<!-- card: query/WhereByIds sql=mysql -->
```csharp
db.Query<User>().WhereByIds(new[] { 1L, 3L }).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE t.`id` IN (?,?);
-- params: [1, 3]
```
<!-- /card -->

### 子查询

`Sql.Exists` / `Sql.NotExists`、`x.In(子查询)` / `x.NotIn(子查询)`、标量子查询 `Sql.SubQuery(..)`。子查询就是另一条 `db.Query<T>()`，lambda 里可以引用外层的参数：

<!-- card: query/SubQueryConditions sql=mysql -->
```csharp
db.Query<User>()
    .Where(u => u.Id.In(db.Query<Post>().Where(p => p.Views >= 2000).Select(p => p.AuthorId)))
    .Where(u => Sql.Exists(db.Query<Comment>().Where(c => c.AuthorId == u.Id)))
    .OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE t.`id` IN (SELECT t1.`author_id` FROM `t_post` t1 WHERE t1.`views` >= ?) AND EXISTS(SELECT 1 FROM `t_comment` t2 WHERE t2.`author_id` = t.`id`) ORDER BY t.`id` ASC;
-- params: [2000]
```
<!-- /card -->

<!-- card: query/ScalarSubQuery sql=mysql -->
```csharp
db.Query<Post>()
    .Where(p => p.Views > Sql.SubQuery(db.Query<Post>().Select(x => Sql.Avg((double)x.Views))))
    .OrderBy(p => p.Id)
    .Select(p => new AboveAverage(p.Title, p.Views, Sql.SubQuery(db.Query<Post>().Select(x => Sql.Avg((double)x.Views)))))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`views`,(SELECT AVG(t3.`views`) FROM `t_post` t3) AS `average` FROM `t_post` t WHERE t.`views` > (SELECT AVG(t1.`views`) FROM `t_post` t1) ORDER BY t.`id` ASC;
```
<!-- /card -->

沿导航的子查询（`u.Posts.Any(..)`、`u.Posts.Count()`）不用手写，见[联表与导航](joins-and-navigations.md#集合导航隐式子查询)。

## 排序与分页

`OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending`；`OrderByIf` / `OrderByDescendingIf` 在条件成立时才排；NULL 的位置用 `Sql.NullsFirst(x)` / `Sql.NullsLast(x)`（没有原生 NULLS FIRST / LAST 的库写成 CASE）；原生片段 `OrderByRaw("…")`（见[原生 SQL](raw-sql.md)）。

<!-- card: query/basics/OrderByColumns sql=mysql -->
```csharp
db.Query<User>().OrderByDescending(u => u.Level).ThenBy(u => u.Age).Select(u => new NameAge(u.Name, u.Age)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`age` FROM `t_user` t ORDER BY t.`level` DESC,t.`age` ASC;
```
<!-- /card -->

<!-- card: query/NullsLast sql=mysql,mssql -->
```csharp
db.Query<User>().OrderByDescending(u => Sql.NullsLast(u.Age)).Take(3).Select(u => new NameAge(u.Name, u.Age)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`age` FROM `t_user` t ORDER BY CASE WHEN t.`age` IS NULL THEN 1 ELSE 0 END ASC,t.`age` DESC LIMIT 3;
```

```sql
-- SQL Server
SELECT TOP 3 t.[name],t.[age] FROM [t_user] t ORDER BY CASE WHEN t.[age] IS NULL THEN 1 ELSE 0 END ASC,t.[age] DESC;
```
<!-- /card -->

<!-- card: query/OrderByIf sql=mysql -->
```csharp
var byViews = true;
return db.Query<Post>().OrderByDescendingIf(byViews, p => p.Views).ThenBy(p => p.Id).Take(3).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t ORDER BY t.`views` DESC,t.`id` ASC LIMIT 3;
```
<!-- /card -->

`Skip` / `Take` 按方言写成 `LIMIT … OFFSET`、`OFFSET … FETCH`、`ROWNUM` 等：

<!-- card: query/SkipTake sql=mysql,mssql,oracle -->
```csharp
db.Query<Post>().OrderBy(p => p.Id).Skip(2).Take(3).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t ORDER BY t.`id` ASC LIMIT 3 OFFSET 2;
```

```sql
-- SQL Server
SELECT t.[title] FROM [t_post] t ORDER BY t.[id] ASC OFFSET 2 ROWS FETCH NEXT 3 ROWS ONLY;
```

```sql
-- Oracle
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."title" FROM "t_post" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 6) rt1 WHERE rt1."__rownum__" > 2;
```
<!-- /card -->

`ToPageAsync(page, size)` 先计数再取这一页（页码从 1 开始）；计数为 0 或页码超出范围时不再查行。只作用在根查询上，带出的子数据不影响总数：

<!-- card: query/basics/Paging sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Status == PostStatus.Published).OrderByDescending(p => p.Views).Select(p => p.Title).ToPageAsync(2, 4);
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_post` WHERE `status` = ?;
-- params: ['published']
SELECT t.`title` FROM `t_post` t WHERE t.`status` = ? ORDER BY t.`views` DESC LIMIT 4 OFFSET 4;
-- params: ['published']
```
<!-- /card -->

深分页：`EasyDbOptions.ReverseOffsetThreshold` 大于 0 时，偏移量不小于它、且超过总数一半的页倒过来取（ORDER BY 整体反向，读出后在内存里倒序），数据库少跳过很多行。单条查询用 `DisableReversePaging()` 关闭。排序里有原生片段或 NULL 位置时不反转。

<!-- card: query/ReversePaging sql=mysql -->
```csharp
var reversing = DocsDb.Reconfigure(db, o => o.ReverseOffsetThreshold = 5);
return reversing.Query<Post>().OrderBy(p => p.Id).Select(p => p.Title).ToPageAsync(4, 3);
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_post`;
SELECT t.`title` FROM `t_post` t ORDER BY t.`id` DESC LIMIT 3;
```
<!-- /card -->

## 投影

`Select` 里写 `new`：匿名类型、record 的构造函数、对象初始化器都行，表达式、函数、导航都能用。只选出投影用到的列：

<!-- card: query/basics/SelectExpressions sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Status == PostStatus.Published)
    .Select(p => new PostCard(p.Title.ToUpper(), p.Views + 1, p.Views >= 2000))
    .Take(3).ToListAsync();
```

```sql
-- MySQL
SELECT UPPER(t.`title`) AS `title`,(t.`views` + ?) AS `next_views`,(t.`views` >= ?) AS `is_hot` FROM `t_post` t WHERE t.`status` = ? LIMIT 3;
-- params: [1, 2000, 'published']
```
<!-- /card -->

<!-- card: query/basics/SelectNavigation sql=mysql -->
```csharp
db.Query<Post>().OrderBy(p => p.Id).Take(2)
    .Select(p => new PostWithAuthor(p.Title, p.Views, p.Author!.Name, p.Author!.Company!.Name))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`views`,t1.`name` AS `author`,t2.`name` AS `company` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` LEFT JOIN `t_company` t2 ON t2.`id` = t1.`company_id` ORDER BY t.`id` ASC LIMIT 2;
```
<!-- /card -->

<!-- card: query/basics/DistinctRows sql=mysql -->
```csharp
db.Query<Post>().Select(p => new AuthorStatus(p.AuthorId, p.Status)).Distinct().OrderBy(r => r.AuthorId).Take(4).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`author_id`,t1.`status` FROM (SELECT DISTINCT t.`author_id`,t.`status` FROM `t_post` t) t1 ORDER BY t1.`author_id` ASC LIMIT 4;
```
<!-- /card -->

### 映射到 DTO

`Select<TDto>()` 按属性名（忽略大小写）映射：DTO 的每个可写属性在实体上找同名的列；与导航同名的属性按导航带出（见[带出与对象树](include-and-trees.md#按-dto-结构自动带出)）。`SelectExcept<TDto>(..)` 跳过几列：

<!-- card: query/SelectDto sql=mysql -->
```csharp
db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id).Select<UserRow>().ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`name`,t.`age` FROM `t_user` t WHERE t.`company_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
```
<!-- /card -->

<!-- card: query/SelectExcept sql=mysql -->
```csharp
db.Query<User>().Where(u => u.CompanyId == 1).OrderBy(u => u.Id).SelectExcept<UserRow>(u => u.Age).ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`name` FROM `t_user` t WHERE t.`company_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
```
<!-- /card -->

DTO 属性上的特性：

| 特性 | 作用 | 对应 Java |
|---|---|---|
| `[MapPath("Author.Name")]` | 取导航路径上的值。全是对一导航时默认走带出查询（同一导航上的几个属性共用一条）；`Join = true` 时在主查询里 LEFT JOIN；路径经过集合导航时属性是列表 | `@NavigateFlat` / `@NavigateJoin` |
| `[MapExpression(nameof(静态表达式))]` | 取一个表达式的值（聚合、函数、导航都可以） | `EXTRA_AUTO_INCLUDE_CONFIGURE` 里的 select |
| `[Column("别名")]` | 列别名 | `@Column` |
| `[ColumnIgnore]` | 不映射 | `@ColumnIgnore` |
| 类上 `[MapFrom<User>]` | 分析器在编译期检查成员能不能对上（EQ0003、EQ0005） | `@EntityProxy` 生成的 DTO 校验 |

<!-- card: query/MapPath sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Id <= 3).OrderBy(p => p.Id).Select<PostRow>().ToListAsync();
```

```sql
-- MySQL
SELECT t2.`name` AS `company_name`,(SELECT COUNT(*) FROM `t_comment` t3 WHERE t3.`post_id` = t.`id`) AS `comments`,t.`title`,t.`author_id` AS `__relation__authorId`,t.`id` AS `__relation__id` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` LEFT JOIN `t_company` t2 ON t2.`id` = t1.`company_id` WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [3]
SELECT `name`,`id` AS `__relation__id` FROM `t_user` WHERE `id` IN (?,?);
-- params: [1, 2]
SELECT `post_id`,`tag_id` FROM `t_post_tag` WHERE `post_id` IN (?,?,?);
-- params: [1, 2, 3]
SELECT `name`,`id` AS `__relation__id` FROM `t_tag` WHERE `id` IN (?,?);
-- params: [1, 3]
```
<!-- /card -->

### 可复用投影

多处共用的投影写成方法标 `[Projection]`（需要引用生成器，见[入门](getting-started.md#安装)），或写成表达式字段用 `Project(map)` 套上。查询里调用时内联成方法体，生成的 SQL 和写在原地一样；在内存里调用就是普通方法：

<!-- snippet: query-projection -->
```csharp
[Projection]
public static PostVo ToVo(this Post p) => new(p.Id, p.Title, p.Author!.Name);
```
<!-- /snippet -->

<!-- card: query/ProjectionMethod sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Views >= 2000).OrderBy(p => p.Id).Select(p => p.ToVo()).ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`title`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t.`views` >= ? ORDER BY t.`id` ASC;
-- params: [2000]
```
<!-- /card -->

<!-- card: query/basics/ProjectMap sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Status == PostStatus.Published).OrderBy(p => p.Id).Take(3).Select(p => p.Author!.Project(AuthorMap)).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`name`,t1.`age` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t.`status` = ? ORDER BY t.`id` ASC LIMIT 3;
-- params: ['published']
```
<!-- /card -->

### 其它投影

| 写法 | 作用 |
|---|---|
| `Sql.Without(u, u.Password, ..)` | 整个实体去掉几列（对应 Java 的 `selectIgnores`） |
| `Select<Dictionary<string, object?>>()`、`Sql.Columns(u)` / `Sql.ColumnsExcept(u, ..)` | 读成字典，键是列名（对应 Java 的 `toMap`） |
| `Sql.ClientSide(值, v => 换算)` | 值读出后在客户端换算（对应 Java 的 `valueConvert`） |
| `Draft<T1, …, T10>` | 现成的泛型 record，不想声明类型时用（对应 Java 的 `Draft1..10`） |
| `ForEach(x => ..)` | 每行读出后调用，用来补算字段 |

<!-- card: query/Without sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Id <= 2).Select(u => Sql.Without(u, u.JoinedAt, u.Level)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id` FROM `t_user` t WHERE t.`id` <= ?;
-- params: [2]
```
<!-- /card -->

<!-- card: query/SelectDictionary sql=mysql -->
```csharp
db.Query<Company>().OrderBy(c => c.Id).Take(2).Select<Dictionary<string, object?>>().ToListAsync();
```

```sql
-- MySQL
SELECT t.`id`,t.`name`,t.`city` FROM `t_company` t ORDER BY t.`id` ASC LIMIT 2;
```
<!-- /card -->

<!-- card: query/ClientSide sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Id <= 3).OrderBy(u => u.Id)
    .Select(u => new NameLevel(u.Name, Sql.ClientSide(u.Level, l => l == UserLevel.Lead ? "负责人" : "成员"))).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`level` FROM `t_user` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [3]
```
<!-- /card -->

## 合并、CTE、派生表

- `q1.Union(q2)` / `UnionAll(..)`：结果作为派生表，可以接着 Where / OrderBy / 分页。
- `q.AsCte("名字")`：写成 `WITH 名字 AS (…)`，之后照常查询或作为 JOIN 来源。
- 投影之后再 `Where` / `OrderBy`、分页之后再投影：自动包一层派生表；`AsSubQuery()` 手动包一层。

<!-- card: query/UnionAll sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Age < 25).Select(u => new NamedRow(u.Name, "用户"))
    .UnionAll(db.Query<Company>().Where(c => c.City == "北京").Select(c => new NamedRow(c.Name, "公司")))
    .OrderBy(r => r.Name).Take(3).ToListAsync();
```

```sql
-- MySQL
SELECT t2.`name`,t2.`kind` AS `kind` FROM ( (SELECT t.`name`,? AS `kind` FROM `t_user` t WHERE t.`age` < ?)  UNION ALL  (SELECT t1.`name`,? AS `kind` FROM `t_company` t1 WHERE t1.`city` = ?) ) t2 ORDER BY t2.`name` ASC LIMIT 3;
-- params: ['用户', 25, '公司', '北京']
```
<!-- /card -->

<!-- card: query/Cte sql=mysql -->
```csharp
var hot = db.Query<Post>().Where(p => p.Views >= 1000).AsCte("hot_posts");
return hot.GroupBy(p => p.AuthorId).Select(g => new AuthorHot(g.Key, g.Count())).OrderBy(r => r.AuthorId).ToListAsync();
```

```sql
-- MySQL
WITH `hot_posts` AS (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at` FROM `t_post` t WHERE t.`views` >= ?) SELECT t2.`author_id` AS `author_id`,t2.`hot` AS `hot` FROM (SELECT t1.`author_id` AS `author_id`,COUNT(*) AS `hot` FROM `hot_posts` t1 GROUP BY t1.`author_id`) t2 ORDER BY t2.`author_id` ASC;
-- params: [1000]
```
<!-- /card -->

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

## 无实体的表

`db.Table("表名")` 查询没有实体类的表，列用 `t.Get<T>("列名")` 引用，结果是 `DynamicRow`：

<!-- card: query/basics/DynamicTable sql=mysql -->
```csharp
var rows = await db.Table("t_user").Where(u => u.Get<int>("age") >= 30).OrderBy(u => u.Get<int>("age"))
    .SelectColumns("name", "age").Take(3).ToListAsync();
return rows.Select(r => new { Name = r.Get<string>("name"), Age = r.Get<int>("age") }).ToList();
```

```sql
-- MySQL
SELECT `name` AS `name`,`age` AS `age` FROM `t_user` WHERE `age` >= ? ORDER BY `age` ASC LIMIT 3;
-- params: [30]
```
<!-- /card -->

## 流式读取

`AsAsyncEnumerable()` 一行一行读，不把结果全放进内存；`ToChunksAsync(n)` 按页分块，每块一条查询；`OffsetChunkAsync(n, 处理函数)` 按偏移分批、由处理函数决定前进多少行（边读边改的场景）。

<!-- card: query/Streaming sql=mysql -->
```csharp
var names = new List<string>();
await foreach (var name in db.Query<User>().OrderBy(u => u.Id).Select(u => u.Name).AsAsyncEnumerable())
    names.Add(name);
await foreach (var chunk in db.Query<Tag>().OrderBy(t => t.Id).ToChunksAsync(4))
    names.Add($"{chunk.Count} 个标签");
return names;
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t ORDER BY t.`id` ASC;
SELECT `id`,`name` FROM `t_tag` ORDER BY `id` ASC LIMIT 4;
SELECT `id`,`name` FROM `t_tag` ORDER BY `id` ASC LIMIT 4 OFFSET 4;
```
<!-- /card -->

## 限制

- 投影里有带出（集合导航、`Select<TDto>()` 的嵌套 DTO）时不能 `AsAsyncEnumerable()`，要用 `ToListAsync()`。
- 标量结果的查询（如 `Select(u => u.Name)`）合并之后不能再按元素本身排序：`q1.UnionAll(q2).OrderBy(n => n)` 报不支持，先投影成带属性的类型再排。
- `MaxResultSize(n)`：结果超过 n 行时抛 `ResultSizeLimitException`，防止误查整张大表。
- `SelectMany` 之外的 LINQ 方法（`Zip`、`Aggregate` 等）不支持；不支持的写法在翻译时抛 `NotSupportedException`，消息里带原表达式。
- DTO 自动映射只认同名属性和 `[MapPath]`；不会把 `CompanyName` 自动拆成 `Company.Name`。

## 对应 Java

| Java | C# |
|---|---|
| `queryable(T.class)` | `db.Query<T>()` |
| `where(o -> o.age().ge(18))` / `where(条件, ..)` | `Where(o => o.Age >= 18)` / `WhereIf(条件, ..)` |
| `whereById` / `whereByIds` | `WhereById` / `WhereByIds` |
| `like` / `contains` / `startsWith` / `endsWith` | `Contains` 等 / `LikeContains` / `LikeStartsWith` / `LikeEndsWith` |
| `in` / `notIn` / `rangeClosed` … | `x.In(..)` 或 `list.Contains(x)` / `NotIn` / `RangeClosed` … |
| `exists` / `notExists` / `subQuery` | `Sql.Exists` / `Sql.NotExists` / `Sql.SubQuery` |
| `filterConfigure(NotNullOrEmptyValueFilter.DEFAULT)` | `UseValueFilter(ValueFilter.NotNullOrBlank)`、`EasyDbOptions.QueryValueFilter` |
| `orderBy(o -> o.id().asc())`、`nullsLast` | `OrderBy(o => o.Id)`、`Sql.NullsLast(..)` |
| `limit(offset, rows)` | `Skip(offset).Take(rows)` |
| `toList()` / `firstOrNull()` / `firstNotNull(msg)` | `ToListAsync()` / `FirstOrDefaultAsync()` / `FirstAsync(msg)` |
| `singleOrNull()` / `singleNotNull()` / `findOrNull(id)` / `findNotNull(id)` | `SingleOrDefaultAsync()` / `SingleAsync()` / `FindAsync(id)` / `FindOrThrowAsync(id)` |
| `any()` / `required(msg)` / `count()` | `AnyAsync()` / `EnsureAnyAsync(msg)` / `CountAsync()` |
| `toPageResult(page, size)` / `toPageSelectResult(..)` | `ToPageAsync(page, size)` / `ToPageAsync(page, size, q => q.Select<TDto>())` |
| `reverseOffsetThreshold` 配置 | `EasyDbOptions.ReverseOffsetThreshold`、`DisableReversePaging()` |
| `select(Dto.class)` / `selectAutoInclude(Dto.class)` | `Select<TDto>()` |
| `allFieldsExclude(..)` / `selectIgnores` | `SelectExcept<TDto>(..)` / `Sql.Without(..)` |
| `select(o -> Select.DRAFT.of(..))` | `Select(o => new Draft<..>(..))` 或任意 record |
| `toMap()` / `mapQueryable` | `Select<Dictionary<string, object?>>()` / `db.Table("…")` |
| `valueConvert` | `Sql.ClientSide` |
| `union` / `unionAll` / `toCteAs` | `Union` / `UnionAll` / `AsCte` |
| `toStreamResult` / `toChunk` / `offsetChunk` | `AsAsyncEnumerable` / `ToChunksAsync` / `OffsetChunkAsync` |
| `setResultSizeLimit` | `MaxResultSize(n)` |
