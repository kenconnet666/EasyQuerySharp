# 联表与导航

两种连表方式：

- **显式 JOIN**：`LeftJoin<T2>((a, b) => …)`，之后的 lambda 依次收到每张表，适合没有声明关系的表、JOIN 子查询。
- **导航**：实体上声明 `[Navigate]`，查询里直接写 `p.Author!.Name`、`u.Posts!.Any(..)`。对一导航生成隐式 JOIN，集合导航生成隐式子查询（EXISTS、标量子查询），按需合并成分组 JOIN。导航在条件、排序、投影、分组、更新删除的条件里都能用。

关系的声明见[实体与映射](entities.md#关系)；把关联数据带出成对象树见[带出与对象树](include-and-trees.md)。

## 显式 JOIN

`LeftJoin` / `InnerJoin` / `RightJoin` / `CrossJoin`，或 `From<T2>()`（逗号连接）。最多十张表（`Query<T1, …, T10>`）：

<!-- card: navigate/LeftJoin sql=mysql -->
```csharp
db.Query<Post>()
    .LeftJoin<User>((p, u) => p.AuthorId == u.Id)
    .Where((p, u) => u.Age >= 30)
    .OrderBy((p, u) => p.Id)
    .Select((p, u) => new PostAuthor(p.Title, u.Name))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t.`author_id` = t1.`id` WHERE t1.`age` >= ? ORDER BY t.`id` ASC;
-- params: [30]
```
<!-- /card -->

JOIN 的另一边可以是一条查询（分组结果、CTE 等），作为派生表：

<!-- card: navigate/JoinSubQuery sql=mysql -->
```csharp
var counts = db.Query<Comment>().GroupBy(c => c.PostId).Select(g => new PostComments(g.Key, g.Count()));
return db.Query<Post>()
    .InnerJoin(counts, (p, c) => p.Id == c.PostId)
    .OrderBy((p, c) => p.Id)
    .Select((p, c) => new PostWithComments(p.Title, c.Count))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t3.`count` AS `comments` FROM `t_post` t INNER JOIN (SELECT t1.`post_id` AS `post_id`,COUNT(*) AS `count` FROM `t_comment` t1 GROUP BY t1.`post_id`) t3 ON t.`id` = t3.`post_id` ORDER BY t.`id` ASC;
```
<!-- /card -->

也可以用 LINQ 查询语法：

<!-- card: navigate/QuerySyntax sql=mysql -->
```csharp
(from p in db.Query<Post>()
 join u in db.Query<User>() on p.AuthorId equals u.Id
 where p.Views >= 2000
 select new PostAuthor(p.Title, u.Name)).ToListAsync();
```

```sql
-- MySQL
SELECT t2.`title`,t2.`name` AS `author` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at`,t1.`id` AS `id1`,t1.`name`,t1.`age`,t1.`company_id`,t1.`department_id`,t1.`manager_id`,t1.`level`,t1.`joined_at` FROM `t_post` t INNER JOIN `t_user` t1 ON t.`author_id` = t1.`id`) t2 WHERE t2.`views` >= ?;
-- params: [2000]
```
<!-- /card -->

## 对一导航：隐式 JOIN

`ManyToOne` / `OneToOne` 导航写在表达式里生成 LEFT JOIN；导航标了 `Required = true` 时生成 INNER JOIN。同一条导航路径在一条查询里只 JOIN 一次，多级导航每级一个 JOIN：

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

<!-- card: navigate/RequiredToOne sql=mysql -->
```csharp
db.Query<Profile>().OrderBy(p => p.UserId).Take(3).Select(p => new ProfileOwner(p.User!.Name, p.Bio)).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`name`,t.`bio` FROM `t_profile` t INNER JOIN `t_user` t1 ON t1.`id` = t.`user_id` ORDER BY t.`user_id` ASC LIMIT 3;
```
<!-- /card -->

<!-- card: navigate/SelfReference sql=mysql -->
```csharp
db.Query<User>().Where(u => u.ManagerId != null).OrderBy(u => u.Id)
    .Select(u => new EmployeeManager(u.Name, u.Manager!.Name, u.Manager!.Manager!.Name)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t1.`name` AS `manager`,t2.`name` AS `managers_manager` FROM `t_user` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`manager_id` LEFT JOIN `t_user` t2 ON t2.`id` = t1.`manager_id` WHERE t.`manager_id` IS NOT NULL ORDER BY t.`id` ASC;
```
<!-- /card -->

`u.Company == null` / `!= null` 写成对方主键 `IS NULL` / `IS NOT NULL`。

## 集合导航：隐式子查询

集合导航（`OneToMany`、`ManyToMany`）上用 LINQ 方法：

| 写法 | SQL |
|---|---|
| `u.Posts.Any(p => …)` / `!u.Posts.Any(..)` | `EXISTS (…)` / `NOT EXISTS (…)` |
| `u.Posts.All(p => …)` | `NOT EXISTS (… AND NOT 条件)` |
| `u.Posts.Count(..)`、`Sum` / `Max` / `Min` / `Average` | 标量子查询 |
| `u.Posts.Where(..).OrderBy(..).Select(..)` 之后接上面的方法 | 子查询里的条件、排序 |
| `string.Join("、", u.Posts.Select(p => p.Title))` | 子查询里的字符串聚合 |
| `u.Posts.OrderBy(..).First()` / `ElementAt(n)` | 按父行分区编行号的派生表 JOIN，见下文 |

<!-- card: navigate/AnyNone sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Posts!.Any(p => p.Views >= 2000) && !u.Comments!.Any()).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` AND t1.`views` >= ? LIMIT 1) AND NOT (EXISTS(SELECT 1 FROM `t_comment` t2 WHERE t2.`author_id` = t.`id` LIMIT 1));
-- params: [2000]
```
<!-- /card -->

<!-- card: navigate/CollectionAggregates sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
    .Select(u => new AuthorStats(u.Name, u.Posts!.Count(), u.Posts!.Sum(p => p.Views), u.Posts!.Max(p => (int?)p.Views)))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post` t2 WHERE t2.`author_id` = t.`id`) AS `posts`,IFNULL((SELECT SUM(t3.`views`) FROM `t_post` t3 WHERE t3.`author_id` = t.`id`),0) AS `views`,(SELECT MAX(t4.`views`) FROM `t_post` t4 WHERE t4.`author_id` = t.`id`) AS `best` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
```
<!-- /card -->

多对多经中间表：

<!-- card: navigate/ManyToMany sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Tags!.Any(t => t.Name == "架构")).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t WHERE EXISTS(SELECT 1 FROM `t_tag` t1 WHERE EXISTS(SELECT 1 FROM `t_post_tag` t2 WHERE t2.`tag_id` = t1.`id` AND t2.`post_id` = t.`id` LIMIT 1) AND t1.`name` = ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: ['架构']
```
<!-- /card -->

<!-- card: navigate/StringJoin sql=mysql,pgsql,mssql,oracle -->
```csharp
db.Query<Post>().Where(p => p.Id <= 4).OrderBy(p => p.Id)
    .Select(p => p.Title + "：" + string.Join("、", p.Tags!.OrderBy(t => t.Id).Select(t => t.Name))).ToListAsync();
```

```sql
-- MySQL
SELECT CONCAT(t.`title`,?,(SELECT GROUP_CONCAT(t1.`name` ORDER BY t1.`id` ASC SEPARATOR ?) FROM `t_tag` t1 WHERE EXISTS(SELECT 1 FROM `t_post_tag` t2 WHERE t2.`tag_id` = t1.`id` AND t2.`post_id` = t.`id` LIMIT 1))) FROM `t_post` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: ['：', '、', 4]
```

```sql
-- PostgreSQL
SELECT CONCAT(t."title",?,(SELECT STRING_AGG(t1."name", ? ORDER BY t1."id" ASC) FROM "t_tag" t1 WHERE EXISTS(SELECT 1 FROM "t_post_tag" t2 WHERE t2."tag_id" = t1."id" AND t2."post_id" = t."id" LIMIT 1))) FROM "t_post" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: ['：', '、', 4]
```

```sql
-- SQL Server
SELECT CONCAT(t.[title],?,(SELECT STRING_AGG(t1.[name], ?) WITHIN GROUP (ORDER BY t1.[id] ASC) FROM [t_tag] t1 WHERE EXISTS(SELECT TOP 1 1 FROM [t_post_tag] t2 WHERE t2.[tag_id] = t1.[id] AND t2.[post_id] = t.[id]))) FROM [t_post] t WHERE t.[id] <= ? ORDER BY t.[id] ASC;
-- params: ['：', '、', 4]
```

```sql
-- Oracle
SELECT t."title" || ? || (SELECT LISTAGG(TO_CHAR(t1."name"), ?) WITHIN GROUP(ORDER BY t1."id" ASC) FROM "t_tag" t1 WHERE EXISTS(SELECT 1 FROM "t_post_tag" t2 WHERE t2."tag_id" = t1."id" AND t2."post_id" = t."id" AND ROWNUM < 2)) FROM "t_post" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: ['：', '、', 4]
```
<!-- /card -->

沿集合导航展开成行（LINQ 的 `SelectMany`，写成 INNER JOIN，多对多经中间表，保留重复）：

<!-- card: query/basics/FlattenNavigations sql=mysql -->
```csharp
db.Query<Company>().Where(c => c.Id == 1).SelectMany(c => c.Users).SelectMany(u => u.Following)
    .OrderBy(u => u.Name).Select(u => u.Name).Distinct().ToListAsync();
```

```sql
-- MySQL
SELECT DISTINCT t3.`name` FROM `t_company` t INNER JOIN `t_user` t1 ON t1.`company_id` = t.`id` INNER JOIN `t_follow` t2 ON t2.`follower_id` = t1.`id` INNER JOIN `t_user` t3 ON t3.`id` = t2.`followee_id` WHERE t.`id` = ? ORDER BY t3.`name` ASC;
-- params: [1]
```
<!-- /card -->

### 导航上的条件

- 导航自带的固定条件：`[Navigate(.., ExtraFilter = nameof(静态表达式))]`，多对多中间表上的条件用 `ExtraMappingFilter`（分析器 EQ0004 检查成员与类型）。示例模型的 `User.HotPosts` 只算阅读量过千的帖子：

<!-- card: navigate/ExtraFilter sql=mysql -->
```csharp
db.Query<User>().Where(u => u.HotPosts!.Any()).OrderBy(u => u.Id).Select(u => new AuthorHot(u.Name, u.HotPosts!.Count())).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post` t2 WHERE (t2.`author_id` = t.`id` AND t2.`views` >= ?)) AS `hot` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE (t1.`author_id` = t.`id` AND t1.`views` >= ?) LIMIT 1) ORDER BY t.`id` ASC;
-- params: [1000, 1000]
```
<!-- /card -->

- 只在这条查询里给导航加条件：`FilterNavigation(u => u.Posts, p => …)`，导航出现的每个地方（条件、投影、带出）都带上：

<!-- card: navigate/FilterNavigation sql=mysql -->
```csharp
db.Query<User>().FilterNavigation(u => u.Posts, p => p.Status == PostStatus.Published)
    .Where(u => u.Posts!.Any()).OrderBy(u => u.Id).Select(u => new AuthorHot(u.Name, u.Posts!.Count())).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post` t2 WHERE t2.`author_id` = t.`id` AND t2.`status` = ?) AS `hot` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` AND t1.`status` = ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: ['published', 'published']
```
<!-- /card -->

- 目标实体有逻辑删除时导航上默认带"未删除"条件；集合导航上 `.DisableLogicDelete()`、对一导航 `Sql.DisableLogicDelete(p.Author)` 关掉。`AsTable(..)` / `AsAlias(..)` 同样可以写在导航上：

<!-- card: navigate/NavigationLogicDelete sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Id <= 2).OrderBy(u => u.Id).Select(u => new AuthorHot(u.Name, u.Docs!.DisableLogicDelete().Count())).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,(SELECT COUNT(*) FROM `t_doc` t1 WHERE t1.`created_by` = t.`name`) AS `hot` FROM `t_user` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [2]
```
<!-- /card -->

### 合并成分组 JOIN

同一个集合导航上有几个聚合时，每个都是一条关联子查询。`GroupJoin(u => u.Posts)`（或导航上 `[Navigate(GroupJoin = true)]`）把它们合并成一次 `LEFT JOIN (SELECT 外键, COUNT(*), SUM(..) … GROUP BY 外键)`；`GroupJoinAll()` 合并查询里所有的集合导航，`GroupJoinNoMerge()` 关掉导航上声明的合并：

<!-- card: navigate/GroupJoin sql=mysql -->
```csharp
db.Query<User>().GroupJoin(u => u.Posts).Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
    .Select(u => new AuthorStats(u.Name, u.Posts!.Count(), u.Posts!.Sum(p => p.Views), u.Posts!.Max(p => (int?)p.Views)))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,IFNULL(t2.`__count3__`,0) AS `posts`,IFNULL(t2.`__sum4__`,0) AS `views`,t2.`__max5__` AS `best` FROM `t_user` t LEFT JOIN (SELECT t1.`author_id` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,COUNT(*) AS `__count3__`,SUM(t1.`views`) AS `__sum4__`,MAX(t1.`views`) AS `__max5__` FROM `t_post` t1 GROUP BY t1.`author_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? ORDER BY t.`id` ASC;
-- params: [false, true]
```
<!-- /card -->

<!-- card: navigate/GroupJoinAll sql=mysql -->
```csharp
db.Query<User>().GroupJoinAll().Where(u => u.Id <= 3).OrderBy(u => u.Id)
    .Select(u => new AuthorActivity(u.Name, u.Posts!.Count(), u.Comments!.Count())).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,IFNULL(t2.`__count2__`,0) AS `posts`,IFNULL(t4.`__count2__`,0) AS `comments` FROM `t_user` t LEFT JOIN (SELECT t1.`author_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_post` t1 GROUP BY t1.`author_id`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`author_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_comment` t3 GROUP BY t3.`author_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [3]
```
<!-- /card -->

主查询里对关联列的等值 / IN 条件会推进分组子查询，避免对整张子表分组。

### 取第几个元素

`First()` / `FirstOrDefault()` / `ElementAt(n)` 写成按父行分区编行号（`ROW_NUMBER() OVER (PARTITION BY 外键 ORDER BY …)`）的派生表 JOIN，之后可以继续取列、走对一导航，条件、排序、投影里都能用：

<!-- card: navigate/Elements sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Posts!.Any()).OrderBy(u => u.Id)
    .Select(u => new TopPosts(u.Name, u.Posts!.OrderByDescending(p => p.Views).First().Title, u.Posts!.OrderByDescending(p => p.Views).ElementAt(1).Title))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t4.`title` AS `top`,t7.`title` AS `second` FROM `t_user` t LEFT JOIN (SELECT t2.`id`,t2.`author_id`,t2.`category_id`,t2.`title`,t2.`status`,t2.`views`,t2.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t2.`author_id` ORDER BY t2.`views` DESC)) AS `__row__` FROM `t_post` t2) t4 ON (t4.`author_id` = t.`id` AND t4.`__row__` = ?) LEFT JOIN (SELECT t5.`id`,t5.`author_id`,t5.`category_id`,t5.`title`,t5.`status`,t5.`views`,t5.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t5.`author_id` ORDER BY t5.`views` DESC)) AS `__row__` FROM `t_post` t5) t7 ON (t7.`author_id` = t.`id` AND t7.`__row__` = ?) WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
-- params: [1, 2]
```
<!-- /card -->

<!-- card: navigate/FirstInWhere sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Posts!.OrderByDescending(p => p.Views).First().Views >= 2000).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t LEFT JOIN (SELECT t1.`id`,t1.`author_id`,t1.`category_id`,t1.`title`,t1.`status`,t1.`views`,t1.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t1.`author_id` ORDER BY t1.`views` DESC)) AS `__row__` FROM `t_post` t1) t3 ON (t3.`author_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`views` >= ?;
-- params: [1, 2000]
```
<!-- /card -->

链上没写排序时按导航的 `PartitionOrder`：默认（`Default`）按导航声明的 `OrderBy`，没有声明时按目标主键升序；`Key` / `KeyDescending` 按主键升 / 降序，`Navigation` 只按声明的 `OrderBy`，`None` 不排序。Java 的默认 `THROW` 在链上和导航上都没写排序时报错，C# 按上面的规则取，不报错。

## 更多导航声明

<!-- snippet: navigate-declare -->
```csharp
[Table("t_user")]
public class Author
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public long? CompanyId { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }

    // 一对多：目标表的 author_id 对本表主键
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.AuthorId)], GroupJoin = true)]
    public List<Post>? Posts { get; set; }

    // 声明了排序和条数的对一导航：每个作者阅读量最高的那篇
    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Post.AuthorId)], OrderBy = "Views desc", Limit = 1)]
    public Post? TopPost { get; set; }
}

[Table("t_post")]
public class Article
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long AuthorId { get; set; }

    public string Title { get; set; } = "";

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)])]
    public Author? Author { get; set; }

    // 直达导航：等于 Author.Company
    [Navigate(RelationType.ManyToOne, DirectMapping = [nameof(Author), nameof(Author.Company)])]
    public Company? AuthorCompany { get; set; }
}
```
<!-- /snippet -->

声明了排序和条数的对一导航（Java 的 OneToOne + `orderByProps` + `limit`，如"每个作者最热的一篇"）在条件、投影、隐式子查询里按排序取那一行（排序规则同上，声明了 `OrderBy` 就按它，不用再写 `PartitionOrder`）；与 null 比较时看那一行的主键，和普通的对一导航一样：

<!-- card: navigate/RankedToOne sql=mysql -->
```csharp
db.Query<Author>().Where(a => a.TopPost!.Views >= 1000).OrderBy(a => a.Id)
    .Select(a => new AuthorTop(a.Name, a.TopPost!.Title, a.TopPost!.Views)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t3.`title` AS `top`,t3.`views` FROM `t_user` t LEFT JOIN (SELECT t1.`id`,t1.`author_id`,t1.`category_id`,t1.`title`,t1.`status`,t1.`views`,t1.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t1.`author_id` ORDER BY t1.`views` DESC)) AS `__row__` FROM `t_post` t1) t3 ON (t3.`author_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`views` >= ? ORDER BY t.`id` ASC;
-- params: [1, 1000]
```
<!-- /card -->

<!-- card: navigate/RankedToOneNull sql=mysql -->
```csharp
db.Query<Author>().Where(a => a.TopPost == null).OrderBy(a => a.Id).Select(a => a.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t LEFT JOIN (SELECT t1.`id`,t1.`author_id`,t1.`category_id`,t1.`title`,t1.`status`,t1.`views`,t1.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t1.`author_id` ORDER BY t1.`views` DESC)) AS `__row__` FROM `t_post` t1) t3 ON (t3.`author_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`id` IS NULL ORDER BY t.`id` ASC;
-- params: [1]
```
<!-- /card -->

直达导航 `DirectMapping`：沿一串对一导航走到底的捷径，与直接写 `a.Author!.Company` 共用 JOIN：

<!-- card: navigate/DirectMapping sql=mysql -->
```csharp
db.Query<Article>().Where(a => a.AuthorCompany!.City == "上海").OrderBy(a => a.Id)
    .Select(a => new ArticleCompany(a.Title, a.AuthorCompany!.Name)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t2.`name` AS `company` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` LEFT JOIN `t_company` t2 ON t2.`id` = t1.`company_id` WHERE t2.`city` = ? ORDER BY t.`id` ASC;
-- params: ['上海']
```
<!-- /card -->

键是逗号分隔列表的导航（本表一列存 `"1,3,5"` 这样的目标键）：`[Navigate(.., KeyMatch = KeyMatch.SelfCsv)]`，条件里单独用 `Sql.InCsv(值, 列)`，各方言写法见 [0005](../decisions/0005-relation-key-match.md)：

<!-- card: query/basics/InCsv sql=mysql,pgsql -->
```csharp
db.Query<Post>().Where(p => Sql.InCsv(p.Id, "1,3")).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t WHERE FIND_IN_SET(t.`id`,?) ORDER BY t.`id` ASC;
-- params: ['1,3']
```

```sql
-- PostgreSQL
SELECT t."title" FROM "t_post" t WHERE ((t."id")::text = ANY(string_to_array(?,','))) ORDER BY t."id" ASC;
-- params: ['1,3']
```
<!-- /card -->

## 方言差异

| 能力 | 差异 |
|---|---|
| 取第几个元素、声明了条数的对一导航 | 需要窗口函数 `ROW_NUMBER()`：MySQL 8.0+、MariaDB 10.2+、SQLite 3.25+，其余方言都有 |
| `string.Join` 聚合 | MySQL `GROUP_CONCAT`、PG / SQL Server `STRING_AGG`、Oracle / 达梦 `LISTAGG`、SQLite `GROUP_CONCAT`；带排序时写进聚合（`WITHIN GROUP` 等） |
| `KeyMatch.SelfCsv` / `Sql.InCsv` | MySQL `FIND_IN_SET`、PG `= ANY(string_to_array(..))`、SQLite / Oracle `instr`、SQL Server `CHARINDEX` |
| 导航生成的 EXISTS | 子查询带取一行的限制（与 Java 一致）：`LIMIT 1`、SQL Server `TOP 1`、Oracle `ROWNUM < 2`；HANA 不带 |

## 限制

- `KeyMatch.SelfCsv` 的导航不能取第几个元素、不能合并成分组 JOIN（回退成子查询）。
- 集合导航上只支持 `Where` / `Select` / `OrderBy` / `ThenBy` / `Skip` / `Take` / `Distinct` 和终结的聚合方法；`GroupBy`、`Join` 等写在导航上报不支持。
- 显式 JOIN 最多 10 张表；更多时用导航或派生表。
- LINQ 查询语法的 `join` 会先把两张表的列包成一个派生表，再在外层投影；写成方法调用 `InnerJoin<T2>(..)` 时没有这一层。
- 集合导航取段之后再聚合（`string.Join(",", u.Cards.OrderBy(..).Take(2).Select(..))`）的写法与 Java 一致、语义存疑，见 ROADMAP 2.4。

## 对应 Java

| Java | C# |
|---|---|
| `leftJoin(B.class, (a, b) -> a.id().eq(b.aid()))` | `LeftJoin<B>((a, b) => a.Id == b.Aid)` |
| `innerJoin` / `rightJoin` / 子查询 join | `InnerJoin` / `RightJoin` / `LeftJoin(subQuery, on)` |
| `@Navigate(value = RelationTypeEnum.ManyToOne, selfProperty = …, targetProperty = …)` | `[Navigate(RelationType.ManyToOne, SelfProperties = [..], TargetProperties = [..])]` |
| `@Navigate(required = true)` | `[Navigate(.., Required = true)]` |
| `@Navigate(extraFilter = …)` | `[Navigate(.., ExtraFilter = nameof(..), ExtraMappingFilter = nameof(..))]` |
| `@Navigate(directMapping = {..})` | `[Navigate(.., DirectMapping = [..])]` |
| `relationPropertyStrategy` | `[Navigate(.., KeyMatch = KeyMatch.SelfCsv)]`、`Sql.InCsv` |
| `o.user().name()` | `o.User!.Name` |
| `u.bankCards().any(..)` / `none(..)` / `count()` / `sum(..)` | `u.BankCards!.Any(..)` / `!u.BankCards!.Any(..)` / `Count()` / `Sum(..)` |
| `u.bankCards().where(..).joining(..)` | `string.Join(",", u.BankCards!.Where(..).Select(..))` |
| `subQueryToGroupJoin(u -> u.bankCards())` / `@Navigate(subQueryToGroupJoin = true)` | `GroupJoin(u => u.BankCards)` / `[Navigate(GroupJoin = true)]` |
| `first()` / `element(n)` / `elements(i, j)` | `First()` / `ElementAt(n)` / `OrderBy(..).Skip(i).Take(j - i + 1)` |
| `flatElement()` | 条件里写成 `Any(..)`；展开成行用 `SelectMany` |
| `partitionOrder` / `orderByProps` / `limit` | `PartitionOrder` / `OrderBy` / `Limit` |
| `nav().filter(..)`（查询级导航条件） | `FilterNavigation(..)` |
