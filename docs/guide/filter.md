# 动态筛选与列表页

后台列表页的条件来自表单或地址栏，字段多、每个都可能不填。三种做法，按需要选：

| 做法 | 适合 |
|---|---|
| `WhereIf(条件, ..)`、`UseValueFilter(ValueFilter.NotNullOrBlank)` | 条件不多，写在代码里（见[查询](queries.md#动态条件)） |
| `FilterBy(条件对象)` / `SortBy(排序请求)` | 条件对象（请求 DTO）上用特性声明每个属性怎么比较 |
| `ToPageAsync(TableRequest.Parse(查询字符串))` | 通用的表格组件：表头筛选、关键字、排序都在地址栏里 |

## FilterBy：条件对象

请求 DTO 的属性标 `[Filter]`：有值的属性才成为条件（null、空白字符串、空集合跳过），属性之间是 AND：

<!-- card: query/filter/FilterBy sql=mysql -->
```csharp
db.Query<Post>().FilterBy(new PostQuery { Author = "林", MinViews = 500 }).OrderBy(p => p.Id)
    .Select(p => new PostRow { Title = p.Title, Status = p.Status, Views = p.Views, Author = p.Author!.Name }).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`status`,t.`views`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t1.`name` LIKE ? AND t.`views` >= ? ORDER BY t.`id` ASC;
-- params: ['%林%', 500]
```
<!-- /card -->

| `[Filter(..)]` 参数 | 说明 |
|---|---|
| `FilterOp`（第一个参数） | `Default`（字符串 LIKE、其余等于）、`Equal` / `NotEqual` / `GreaterThan` …、`Like` / `LikeStart` / `LikeEnd`（`LIKE ?`）、`Contains` / `StartsWith` / `EndsWith`（`LIKE CONCAT(..)`，值里的通配符按字面）、`In` / `NotIn` / `AnyEqual`（值是集合）、`RangeOpen` / `RangeClosed` / `RangeClosedOpen` / `RangeOpenClosed`（值是两个元素的集合，第二个缺省时只比下限） |
| `Property = "Author.Name"` | 对应的实体属性，可以是导航路径：对一导航 JOIN，集合导航 EXISTS（同一集合下的条件合并进一个 EXISTS） |
| `Properties = [..]` | 几个属性之间是 OR |
| `Table` / `Tables` | 联表查询里作用在第几张表（FROM 是 0） |
| `AllowEmptyStrings` | 空白字符串也加条件 |

<!-- card: filter/FilterOperators sql=mysql -->
```csharp
db.Query<Post>()
    .FilterBy(new PostSearch { Keyword = "林", Published = [new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)], Tag = "ORM" })
    .OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE (t.`title` LIKE ? OR t1.`name` LIKE ?) AND (t.`published_at` >= ? AND t.`published_at` <= ?) AND EXISTS(SELECT 1 FROM `t_tag` t2 WHERE EXISTS(SELECT 1 FROM `t_post_tag` t3 WHERE t3.`tag_id` = t2.`id` AND t3.`post_id` = t.`id` LIMIT 1) AND t2.`name` = ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: ['%林%', '%林%', '2025-01-01', '2025-12-31', 'ORM']
```
<!-- /card -->

`FilterByIf(条件, 对象)` 在条件成立时才应用。

## SortBy：动态排序

排序请求实现 `ISortRequest`，在 `ConfigureSort` 里按属性名追加排序；`Allow(..)` 限定允许排序的属性，防止前端传入任意列：

<!-- card: filter/SortBy sql=mysql -->
```csharp
db.Query<Post>().SortBy(new PostSort { By = "Views", Descending = true }).Take(3).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t ORDER BY t.`views` DESC LIMIT 3;
```
<!-- /card -->

`SortBuilder.OrderBy(属性, ascending, table, nulls)`：`table` 是联表时的表序号，`nulls` 指定 NULL 的位置。

按路径取列（列名来自配置或请求）：`Sql.Column<T>(实体, "Author.Age")`：

<!-- card: filter/ColumnByPath sql=mysql -->
```csharp
var path = "Author.Age";
return db.Query<Post>().Where(p => Sql.Column<int?>(p, path) >= 35).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t1.`age` >= ? ORDER BY t.`id` ASC;
-- params: [35]
```
<!-- /card -->

## 列表页请求：TableRequest

`TableRequest.Parse(查询字符串)` 读通用表格组件的约定（与 TS 版的 `parseTableRequest` 一致）：

| 参数 | 含义 |
|---|---|
| `page`、`pageSize` | 页码（从 1 开始）、每页条数（最多 `TableRequest.MaxPageSize` = 2000，超过报错） |
| `sort=-views,title` | 排序，`-` 是降序（也认 `views desc, title asc`）；排在查询自己的排序之前 |
| `filter.列=a&filter.列=b` | 表头筛选：`列 IN (a, b)` |
| `search.列=关键字` | 包含 |
| `total=123` | 前端已知总数时不再计数 |

`ToPageAsync(request)` 作用在投影之后：筛选、排序只认结果里的列（结果类型的属性名，忽略大小写；值对象写成 `address.city`），相当于白名单，前端只能按页面上展示的列筛选和排序；不是结果里的列时抛 `ArgumentException`，消息里列出可用的列。其余参数（查询栏的条件）原样放在 `request.Params`，由调用方自己写成条件：

<!-- card: query/filter/TableRequest sql=mysql -->
```csharp
var request = Filtering.TableRequest.Parse("page=1&pageSize=3&sort=-views&filter.status=published&search.title=的");
return db.Query<Post>().Select(p => new PostRow { Title = p.Title, Status = p.Status, Views = p.Views, Author = p.Author!.Name }).ToPageAsync(request);
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_post` t WHERE t.`status` IN (?) AND t.`title` LIKE ?;
-- params: ['published', '%的%']
SELECT t.`title`,t.`status`,t.`views`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t.`status` IN (?) AND t.`title` LIKE ? ORDER BY t.`views` DESC LIMIT 3;
-- params: ['published', '%的%']
```
<!-- /card -->

## 限制

- `TableRequest` 的筛选只有 IN 和包含两种；条件树（AND / OR 嵌套）、整天的日期区间（TS 的 11b.3a）还没做（记在 BACKLOG）。
- `FilterBy` 的属性路径在运行时解析，写错时第一次执行才报错。
- search 扩展（Java 的 `sql-search`：由前端选运算符）不做，同类需求用 `FilterBy` / `SortBy`。

## 对应 Java

| Java | C# |
|---|---|
| `whereObject(obj)` / `whereObject(条件, obj)` | `FilterBy(obj)` / `FilterByIf(条件, obj)` |
| `@EasyWhereCondition(type = …, propName = …, propNames = …, tableIndex = …)` | `[Filter(FilterOp.X, Property = …, Properties = […], Table = …)]` |
| `EasyWhereCondition.Condition.LIKE / CONTAINS / IN / RANGE_LEFT_CLOSED …` | `FilterOp.Like / Contains / In / GreaterThanOrEqual …` |
| `orderByObject(ObjectSort)`、`ObjectSortBuilder` | `SortBy(ISortRequest)`、`SortBuilder` |
| `anyColumn("a.b")` | `Sql.Column<T>(x, "A.B")` |
| `filterConfigure(NotNullOrEmptyValueFilter.DEFAULT)` | `UseValueFilter(ValueFilter.NotNullOrBlank)` |
| TS 的 `table(request)` / `parseTableRequest` | `ToPageAsync(TableRequest.Parse(..))` |
