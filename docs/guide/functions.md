# 函数

查询表达式里优先写 BCL 的方法（`string`、`Math`、`DateTime` 的成员），翻译成各方言的 SQL 函数；没有 BCL 对应的用 `Sql` 下的静态方法或扩展方法。方言专属的函数在 `Sql.MySql`、`Sql.Pg`、`Sql.Hana` 下。这些方法只能写在查询表达式里，在普通代码里调用会被分析器 EQ0007 报出来。

## 字符串

| C# | SQL（MySQL） | 说明 |
|---|---|---|
| `s.Length` | `CHAR_LENGTH(s)` | 字符数；SQL Server `LEN`，SQLite / Oracle `LENGTH` |
| `s.ToUpper()` / `ToLower()` | `UPPER` / `LOWER` | |
| `s.Trim()` / `TrimStart()` / `TrimEnd()` | `TRIM` / `LTRIM` / `RTRIM` | 只去空白 |
| `s.Substring(i, n)` | `SUBSTR(s, i+1, n)` | 下标从 0 开始，常量直接加 1 |
| `s.Replace(a, b)` | `REPLACE` | |
| `s.IndexOf(x)` | `LOCATE(x, s) - 1` | 找不到是 -1，与 C# 一致 |
| `s.PadLeft(n, c)` / `PadRight` | `LPAD` / `RPAD` | 比 n 长时截断成前 n 个字符（与 LPAD 一致，.NET 原样返回），各方言相同；SQLite、SQL Server 没有 LPAD，用拼接模拟 |
| `a + b`、`string.Concat`、插值字符串 `$"{a}-{b}"` | `CONCAT(..)` | Oracle / Db2 用 `||`；与 C# 一样 null 当空串，拼接遇到 NULL 得 NULL 的库（MySQL、HANA、Db2、ClickHouse、TDengine）给可能为 NULL 的项包一层 `COALESCE(x, '')` |
| `string.IsNullOrEmpty(s)` / `IsNullOrWhiteSpace` | `s IS NULL OR s = ''` 等 | |
| `string.Compare(a, b)` / `a.CompareTo(b)` | `CASE WHEN a = b THEN 0 …` | |
| `s.Contains(x)` / `StartsWith` / `EndsWith` | `LIKE ?` | 值里的通配符按字面匹配 |
| 带 `StringComparison` 的 `Contains` / `StartsWith` / `EndsWith` / `Equals`、`string.Equals(a, b, c)` | `LOWER(s) LIKE LOWER(?)`、`LOWER(a) = LOWER(b)` | 忽略大小写的比较方式（`OrdinalIgnoreCase` 等）两边转小写；区分大小写的照常比较，结果仍取决于库的排序规则 |
| 枚举的 `e.ToString()` | `CASE e WHEN .. THEN '成员名' .. END` | 与 C# 一样是成员名，与枚举怎么存无关；可空枚举没有值时是空串 |
| `s.LikeContains(x)` / `LikeStartsWith` / `LikeEndsWith` | `LIKE CONCAT('%',?,'%')` | Java 的 `contains` / `startsWith` / `endsWith` |
| `s.Like(pattern)` / `NotLike` | `LIKE ?` | 通配符自己写 |

<!-- card: func/Strings sql=mysql,mssql -->
```csharp
db.Query<Post>().Where(p => p.Id <= 2).OrderBy(p => p.Id)
    .Select(p => new TextFunctions(p.Title.ToUpper(), p.Title.Length, p.Title.Substring(0, 2), p.Title.Replace("查询", "query"),
        (" " + p.Title + " ").Trim(), p.Title.IndexOf("join")))
    .ToListAsync();
```

```sql
-- MySQL
SELECT UPPER(t.`title`) AS `upper`,CHAR_LENGTH(t.`title`) AS `length`,SUBSTR(t.`title`,1,2) AS `first`,REPLACE(t.`title`,?,?) AS `replaced`,TRIM(CONCAT(?,t.`title`,?)) AS `trimmed`,(LOCATE(?,t.`title`) - 1) AS `position` FROM `t_post` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: ['查询', 'query', ' ', ' ', 'join', 2]
```

```sql
-- SQL Server
SELECT UPPER(t.[title]) AS [upper],LEN(t.[title]) AS [length],SUBSTRING(t.[title],1,2) AS [first],REPLACE(t.[title],?,?) AS [replaced],LTRIM(RTRIM(CONCAT(?,t.[title],?))) AS [trimmed],(CHARINDEX(?,t.[title]) - 1) AS [position] FROM [t_post] t WHERE t.[id] <= ? ORDER BY t.[id] ASC;
-- params: ['查询', 'query', ' ', ' ', 'join', 2]
```
<!-- /card -->

<!-- card: func/Pad sql=mysql,sqlite -->
```csharp
db.Query<User>().Where(u => u.Id <= 3).OrderBy(u => u.Id).Select(u => u.Name.PadLeft(6, '*')).ToListAsync();
```

```sql
-- MySQL
SELECT LPAD(t.`name`, 6, ?) FROM `t_user` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: ['*', 3]
```

```sql
-- SQLite
SELECT substr(substr(replace(hex(zeroblob(6)), '00', ?), 1, 6 - length(t."name")) || t."name", 1, 6) FROM "t_user" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: ['*', 3]
```
<!-- /card -->

<!-- card: func/Concat sql=mysql,oracle -->
```csharp
db.Query<User>().Where(u => u.Age != null).OrderBy(u => u.Id).Take(3).Select(u => $"{u.Name}（{u.Age} 岁）").ToListAsync();
```

```sql
-- MySQL
SELECT CONCAT(t.`name`,?,IFNULL(CAST(t.`age` AS CHAR),''),?) FROM `t_user` t WHERE t.`age` IS NOT NULL ORDER BY t.`id` ASC LIMIT 3;
-- params: ['（', ' 岁）']
```

```sql
-- Oracle
SELECT rt.* FROM (SELECT t."name" || ? || t."age" || ? FROM "t_user" t WHERE t."age" IS NOT NULL ORDER BY t."id" ASC) rt WHERE ROWNUM < 4;
-- params: ['（', ' 岁）']
```
<!-- /card -->

<!-- card: func/LikeVariants sql=mysql,mssql -->
```csharp
db.Query<Post>().Where(p => p.Title.LikeContains("SQL") || p.Title.Like("%缓存%")).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t WHERE (t.`title` LIKE CONCAT('%',?,'%') OR t.`title` LIKE ?) ORDER BY t.`id` ASC;
-- params: ['SQL', '%缓存%']
```

```sql
-- SQL Server
SELECT t.[title] FROM [t_post] t WHERE (t.[title] LIKE ('%'+CAST(? AS NVARCHAR(MAX))+'%') OR t.[title] LIKE ?) ORDER BY t.[id] ASC;
-- params: ['SQL', '%缓存%']
```
<!-- /card -->

<!-- card: func/Blank sql=mysql -->
```csharp
db.Query<Profile>().Where(p => string.IsNullOrEmpty(p.Website)).OrderBy(p => p.UserId).Select(p => p.UserId).ToListAsync();
```

```sql
-- MySQL
SELECT t.`user_id` FROM `t_profile` t WHERE (t.`website` IS NULL OR t.`website` = '') ORDER BY t.`user_id` ASC;
```
<!-- /card -->

## 日期

| C# | 说明 |
|---|---|
| `d.Year` / `Month` / `Day` / `Hour` / `Minute` / `Second` / `DayOfYear` / `DayOfWeek` | 取部分；`DayOfWeek` 与 C# 一样周日为 0，`d.IsoDayOfWeek()` 周一为 1 |
| `d.AddYears(n)` … `AddMilliseconds(n)` | 加减，`n` 可以是列 |
| `d.ToString("yyyy-MM-dd HH:mm:ss")` | 按格式转字符串，格式符按方言翻译（MySQL `DATE_FORMAT`、PG `TO_CHAR`、SQL Server `CONVERT`、SQLite `strftime`） |
| `(end - start).Days` / `TotalDays` / `TotalHours` / `TotalMinutes` / `TotalSeconds` / `TotalMilliseconds` | 相差的整数单位（截断） |
| `d.DateTrunc(DateTruncUnit.Month)` | 截断到年 / 季 / 月 / 周（周一起）/ 日 / 时 / 分 / 秒的起点 |
| `d.Date`、`DateOnly.FromDateTime(d)` | 当天零点 / 日期部分 |
| `Sql.Now()` / `Sql.UtcNow()` | 数据库的当前时间 |

支持 `DateTime`、`DateTimeOffset`、`DateOnly`（`DateTruncUnit` 只对 `DateTime`）。

<!-- card: func/Dates sql=mysql,pgsql,mssql,oracle -->
```csharp
var newYear = new DateTime(2025, 1, 1);
return db.Query<Order>().Where(o => o.Id <= 2).OrderBy(o => o.Id)
    .Select(o => new DateParts(o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.ToString("yyyy-MM-dd"), o.CreatedAt.AddDays(7), (o.CreatedAt - newYear).Days))
    .ToListAsync();
```

```sql
-- MySQL
SELECT YEAR(t.`created_at`) AS `year`,MONTH(t.`created_at`) AS `month`,DATE_FORMAT(t.`created_at`,'%Y-%m-%d') AS `day`,date_add(t.`created_at`, interval (?) day) AS `next_week`,timestampdiff(DAY, ?, t.`created_at`) AS `days_since_new_year` FROM `t_order` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [7, '2025-01-01 00:00:00', 2]
```

```sql
-- PostgreSQL
SELECT extract(year from (t."created_at")::timestamp) AS "year",extract(month from (t."created_at")::timestamp) AS "month",TO_CHAR((t."created_at")::TIMESTAMP,'YYYY-MM-DD') AS "day",((t."created_at")::timestamp+((?)||' day')::interval) AS "next_week",trunc(extract(epoch from (t."created_at")::timestamp-(?)::timestamp)/86400)::int AS "days_since_new_year" FROM "t_order" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: [7, '2025-01-01 00:00:00', 2]
```

```sql
-- SQL Server
SELECT datepart(year, t.[created_at]) AS [year],datepart(month, t.[created_at]) AS [month],CONVERT(CHAR(10), t.[created_at], 23) AS [day],dateadd(day, ?, t.[created_at]) AS [next_week],datediff(day, ?, t.[created_at]) AS [days_since_new_year] FROM [t_order] t WHERE t.[id] <= ? ORDER BY t.[id] ASC;
-- params: [7, '2025-01-01 00:00:00', 2]
```

```sql
-- Oracle
SELECT cast(to_char(t."created_at",'YYYY') as number) AS "year",cast(to_char(t."created_at",'MM') as number) AS "month",TO_CHAR(t."created_at",'YYYY-MM-DD') AS "day",(t."created_at"+?) AS "next_week",EXTRACT(DAY FROM NUMTODSINTERVAL((t."created_at"+0)-(?+0),'DAY')) AS "days_since_new_year" FROM "t_order" t WHERE t."id" <= ? ORDER BY t."id" ASC;
-- params: [7, '2025-01-01 00:00:00', 2]
```
<!-- /card -->

<!-- card: func/DateTrunc sql=mysql,pgsql -->
```csharp
db.Query<Order>().GroupBy(o => o.CreatedAt.DateTrunc(DateTruncUnit.Month))
    .Select(g => new MonthOrders(g.Key, g.Count())).OrderBy(r => r.Month).ToListAsync();
```

```sql
-- MySQL
SELECT t1.`month` AS `month`,t1.`orders` AS `orders` FROM (SELECT CAST(DATE_FORMAT(t.`created_at`,'%Y-%m-01 00:00:00') AS DATETIME) AS `month`,COUNT(*) AS `orders` FROM `t_order` t GROUP BY CAST(DATE_FORMAT(t.`created_at`,'%Y-%m-01 00:00:00') AS DATETIME)) t1 ORDER BY t1.`month` ASC;
```

```sql
-- PostgreSQL
SELECT t1."month" AS "month",t1."orders" AS "orders" FROM (SELECT DATE_TRUNC('month', t."created_at") AS "month",COUNT(*) AS "orders" FROM "t_order" t GROUP BY DATE_TRUNC('month', t."created_at")) t1 ORDER BY t1."month" ASC;
```
<!-- /card -->

## 数值

`Math.Abs` / `Round` / `Floor` / `Ceiling` / `Truncate` / `Sign` / `Pow` / `Sqrt` / `Exp` / `Log` / `Log10` / 三角函数直接写；`Math.Max` / `Min` 写成 `Sql.MaxColumns` / `MinColumns`，`Math.Clamp(x, lo, hi)` 写成两者的嵌套；`Sql.Random()` 取随机数（多用于随机排序）。

两个整数相除与 C# 一样向零截断（`7 / 2` 是 3，`-7 / 2` 是 -3）：PG、SQL Server、SQLite 这类库的 `/` 本来就是整除，原样输出；MySQL 写 `DIV`，Oracle、达梦、DuckDB、openGauss、Kingbase 写 `TRUNC(a / b)`，HANA、ClickHouse、TDengine 用取模减掉余数再除。有一边是小数时照常是小数除法，要小数结果就先转换一边（`(decimal)a / b`）。

`Math.Round(x)` 交给库的 `ROUND`：小数（`decimal`）在各库上都是远离零舍入（2.5 → 3），与 .NET 默认的银行家舍入（2.5 → 2）不同，浮点数按各库自己的规则；带 `MidpointRounding` 的重载报不支持。

<!-- card: func/Numbers sql=mysql,mssql -->
```csharp
db.Query<OrderItem>().Where(i => i.OrderId == 7).OrderBy(i => i.ProductId)
    .Select(i => new Amounts(i.Price * i.Quantity, Math.Round(i.Price * 0.85m, 1), Math.Floor(i.Price), Math.Ceiling(i.Price), Math.Abs(i.Price - 100)))
    .ToListAsync();
```

```sql
-- MySQL
SELECT (t.`price` * t.`quantity`) AS `total`,ROUND((t.`price` * ?),?) AS `rounded`,FLOOR(t.`price`) AS `floor`,CEILING(t.`price`) AS `ceiling`,ABS((t.`price` - ?)) AS `abs` FROM `t_order_item` t WHERE t.`order_id` = ? ORDER BY t.`product_id` ASC;
-- params: [0.85, 1, 100, 7]
```

```sql
-- SQL Server
SELECT (t.[price] * t.[quantity]) AS [total],ROUND((t.[price] * ?),?) AS [rounded],FLOOR(t.[price]) AS [floor],CEILING(t.[price]) AS [ceiling],ABS((t.[price] - ?)) AS [abs] FROM [t_order_item] t WHERE t.[order_id] = ? ORDER BY t.[product_id] ASC;
-- params: [0.85, 1, 100, 7]
```
<!-- /card -->

## NULL 与通用函数

| C# | SQL | 说明 |
|---|---|---|
| `a ?? b` | `IFNULL` / `COALESCE` / `ISNULL` / `NVL` | |
| `a.GetValueOrDefault()` / `GetValueOrDefault(v)` | 同上 | 没有值时是类型的默认值 / `v` |
| `a.NullIf(b)` | `NULLIF(a, b)` | |
| `a.EqualsNullSafe(b)` | MySQL `<=>`、PG `IS NOT DISTINCT FROM`、SQLite `IS`，其余展开成 OR | 两边都是 NULL 也算相等 |
| `Sql.MaxColumns(a, b, ..)` / `MinColumns` | MySQL / PG `GREATEST` / `LEAST`，SQLite 多参数 `MAX` / `MIN`，SQL Server `VALUES` 派生表，Oracle / 达梦 `UNION ALL` 派生表 | 同一行里几个值取最大 / 最小 |
| 三元表达式 `c ? a : b` | `CASE WHEN c THEN a ELSE b END` | 多分支写成嵌套三元 |
| 条件当值（`p.Views > 1000` 放在投影里） | 原样；SQL Server、Oracle、达梦、HANA、Kingbase、GaussDB 写成 `CASE WHEN … THEN ? ELSE ? END` | |
| `x.ToString()`、`int.Parse(s)`、`Convert.ToXxx(x)`（任一个参数的重载）、`Sql.Cast<T>(x)` | `CAST(x AS …)` | 目标类型按方言；可空值没有值时 `ToString()` 与 C# 一样是空串；`Convert.ToInt32(2.5)` 在 .NET 里四舍六入，数据库按各自的 CAST 规则 |
| `Sql.Constant(v)` | `?` | 客户端的值作为参数放进 SQL，接着的运算（如 `.ToLower()`）在数据库里做 |

<!-- card: func/NullHandling sql=mysql,mssql,oracle -->
```csharp
db.Query<User>().Where(u => u.Id >= 5 && u.Id <= 7).OrderBy(u => u.Id)
    .Select(u => new General(u.Name, u.Age ?? 0, u.Age.NullIf(17), Sql.MaxColumns(u.Age ?? 0, 30)))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,IFNULL(t.`age`,?) AS `age`,NULLIF(t.`age`, ?) AS `not_zero`,GREATEST(IFNULL(t.`age`,?),?) AS `larger` FROM `t_user` t WHERE t.`id` >= ? AND t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [0, 17, 0, 30, 5, 7]
```

```sql
-- SQL Server
SELECT t.[name],ISNULL(t.[age],?) AS [age],NULLIF(t.[age], ?) AS [not_zero],(SELECT MAX(__col__) FROM (VALUES (ISNULL(t.[age],?)), (?)) AS t(__col__)) AS [larger] FROM [t_user] t WHERE t.[id] >= ? AND t.[id] <= ? ORDER BY t.[id] ASC;
-- params: [0, 17, 0, 30, 5, 7]
```

```sql
-- Oracle
SELECT t."name",NVL(t."age",?) AS "age",NULLIF(t."age", ?) AS "not_zero",(SELECT MAX("__mmc"."__col__") FROM (SELECT NVL(t."age",?) AS "__col__" FROM DUAL UNION ALL SELECT ? AS "__col__" FROM DUAL) "__mmc") AS "larger" FROM "t_user" t WHERE t."id" >= ? AND t."id" <= ? ORDER BY t."id" ASC;
-- params: [0, 17, 0, 30, 5, 7]
```
<!-- /card -->

<!-- card: func/NullSafeEquals sql=mysql,pgsql,sqlite -->
```csharp
int? age = null;
return db.Query<User>().Where(u => u.Age.EqualsNullSafe(age)).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE (t.`age` <=> ?);
-- params: [NULL]
```

```sql
-- PostgreSQL
SELECT t."name" FROM "t_user" t WHERE (t."age" IS NOT DISTINCT FROM ?);
-- params: [NULL]
```

```sql
-- SQLite
SELECT t."name" FROM "t_user" t WHERE (t."age" IS ?);
-- params: [NULL]
```
<!-- /card -->

<!-- card: func/CaseWhen sql=mysql -->
```csharp
db.Query<Post>().OrderBy(p => p.Id).Take(5)
    .Select(p => new PostHeat(p.Title, p.Views >= 2000 ? "热门" : p.Views >= 500 ? "普通" : "冷门")).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,(CASE WHEN t.`views` >= ? THEN ? WHEN t.`views` >= ? THEN ? ELSE ? END) AS `heat` FROM `t_post` t ORDER BY t.`id` ASC LIMIT 5;
-- params: [2000, '热门', 500, '普通', '冷门']
```
<!-- /card -->

<!-- card: func/ConvertTo sql=mysql,mssql -->
```csharp
db.Query<Post>().Where(p => Convert.ToDecimal(p.Views) / 1000 >= 2).OrderBy(p => p.Id).Select(p => Convert.ToString(p.Views)).ToListAsync();
```

```sql
-- MySQL
SELECT CAST(t.`views` AS CHAR) FROM `t_post` t WHERE (CAST(t.`views` AS DECIMAL(36,18)) / ?) >= ? ORDER BY t.`id` ASC;
-- params: [1000, 2]
```

```sql
-- SQL Server
SELECT cast(t.[views] as nvarchar(max)) FROM [t_post] t WHERE (cast(t.[views] as decimal(36,18)) / ?) >= ? ORDER BY t.[id] ASC;
-- params: [1000, 2]
```
<!-- /card -->

<!-- card: func/Casts sql=mysql,pgsql,oracle -->
```csharp
db.Query<Post>().Where(p => p.Views.ToString().StartsWith("8")).OrderBy(p => p.Id).Select(p => p.Title + "：" + p.Views.ToString()).ToListAsync();
```

```sql
-- MySQL
SELECT CONCAT(t.`title`,?,CAST(t.`views` AS CHAR)) FROM `t_post` t WHERE CAST(t.`views` AS CHAR) LIKE ? ORDER BY t.`id` ASC;
-- params: ['：', '8%']
```

```sql
-- PostgreSQL
SELECT CONCAT(t."title",?,(t."views")::text) FROM "t_post" t WHERE (t."views")::text LIKE ? ORDER BY t."id" ASC;
-- params: ['：', '8%']
```

```sql
-- Oracle
SELECT t."title" || ? || TO_CHAR(t."views") FROM "t_post" t WHERE TO_CHAR(t."views") LIKE ? ORDER BY t."id" ASC;
-- params: ['：', '8%']
```
<!-- /card -->

## JSON

`Sql.Json(列)` 把 JSON 列（字符串或带 `JsonValueConverter` 的属性）当作文档：`.Get(键)` / `.Get(下标)` 取子文档，`.Text(..)` 取文本值，`.Value<T>(..)` 取值并转换类型，`.Length()` 数组长度，`.ContainsKey(键)` 是否有这个键。

<!-- card: func/Json sql=mysql,pgsql,sqlite -->
```csharp
db.Query<Profile>().Where(p => Sql.Json(p.Skills).Length() >= 3).OrderBy(p => p.UserId)
    .Select(p => new SkillSummary(p.UserId, Sql.Json(p.Skills).Length(), Sql.Json(p.Skills).Text(0))).ToListAsync();
```

```sql
-- MySQL
SELECT t.`user_id`,JSON_LENGTH(t.`skills`) AS `count`,JSON_UNQUOTE(JSON_EXTRACT(t.`skills`,'$[0]')) AS `first` FROM `t_profile` t WHERE JSON_LENGTH(t.`skills`) >= ? ORDER BY t.`user_id` ASC;
-- params: [3]
```

```sql
-- PostgreSQL
SELECT t."user_id",JSONB_ARRAY_LENGTH(t."skills") AS "count",(t."skills" ->> 0) AS "first" FROM "t_profile" t WHERE JSONB_ARRAY_LENGTH(t."skills") >= ? ORDER BY t."user_id" ASC;
-- params: [3]
```

```sql
-- SQLite
SELECT t."user_id",json_array_length(t."skills") AS "count",json_extract(t."skills",'$[0]') AS "first" FROM "t_profile" t WHERE json_array_length(t."skills") >= ? ORDER BY t."user_id" ASC;
-- params: [3]
```
<!-- /card -->

方言专属的 JSON、全文检索、数组函数：

<!-- snippet: func-dialect -->
```csharp
// MySQL：JSON_CONTAINS、全文索引 MATCH … AGAINST
var mysql = db.Query<Profile>()
    .Where(p => Sql.MySql.Json(p.Skills).Contains("\"SQL\""))
    .Where(p => Sql.MySql.Match(p.Bio).Against("迁移"));

// PostgreSQL：jsonb 的 @>、全文检索
var pg = db.Query<Profile>()
    .Where(p => Sql.Pg.Jsonb(p.Skills).Contains("[\"SQL\"]"))
    .Where(p => Sql.Pg.FullText(p.Bio).Matches("simple", "迁移"));
```
<!-- /snippet -->

| 入口 | 方法 |
|---|---|
| `Sql.MySql.Json(列)` | `Contains`、`HasMember`、`Overlaps`、`ContainsPath`、`ContainsAllPaths`、`Edit(e => e.Set / SetJson / ArrayAppend / Remove)` |
| `Sql.MySql.Match(列…)` | `Against(词, 模式)`、`Score(..)`（全文索引） |
| `Sql.Pg.Jsonb(列)` | `Contains`、`ContainedBy`、`HasKey`、`HasAnyKey`、`HasAllKeys`、`Edit(e => e.Set / Merge / Remove)` |
| `Sql.Pg.Array(列)` | `Has`、`HasAny`、`HasAll`、`Length` |
| `Sql.Pg.FullText(列…)` | `Matches(配置, 查询)`、`Rank(..)` |
| `Sql.Hana.Text(列…)` | `Contains`、`Fuzzy(查询, 阈值)`、`Score()` |

在别的方言上用这些入口，渲染时报错，消息里带函数名和当前方言。

## 窗口函数

`Sql.Over` 下：`RowNumber` / `Rank` / `DenseRank` / `Ntile` / `PercentRank` / `CumeDist`，`Lag` / `Lead` / `FirstValue` / `LastValue` / `NthValue`，聚合 `Sum` / `Count` / `Avg` / `Max` / `Min`。窗口用 `w => w.PartitionBy(..).OrderBy(..).Rows(起, 止)` 描述，边界是 `WindowBound.UnboundedPreceding` / `Preceding(n)` / `CurrentRow` / `Following(n)` / `UnboundedFollowing`：

<!-- card: func/Windows sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.AuthorId <= 3).OrderBy(p => p.AuthorId).ThenBy(p => p.Id)
    .Select(p => new PostRank(p.Title, p.AuthorId,
        Sql.Over.RowNumber(w => w.PartitionBy(p.AuthorId).OrderByDescending(p.Views)),
        Sql.Over.Sum(p.Views, w => w.PartitionBy(p.AuthorId))))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`author_id`,(ROW_NUMBER() OVER (PARTITION BY t.`author_id` ORDER BY t.`views` DESC)) AS `rank`,(SUM(t.`views`) OVER (PARTITION BY t.`author_id`)) AS `author_views` FROM `t_post` t WHERE t.`author_id` <= ? ORDER BY t.`author_id` ASC,t.`id` ASC;
-- params: [3]
```
<!-- /card -->

<!-- card: func/WindowFrame sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.PublishedAt != null).OrderBy(p => p.PublishedAt).Take(4)
    .Select(p => new RunningViews(p.Title, p.Views,
        Sql.Over.Sum(p.Views, w => w.OrderBy(p.PublishedAt).Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow))))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`views`,(SUM(t.`views`) OVER (ORDER BY t.`published_at` ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)) AS `running` FROM `t_post` t WHERE t.`published_at` IS NOT NULL ORDER BY t.`published_at` ASC LIMIT 4;
```
<!-- /card -->

取值函数不写窗口（或窗口没写排序）时沿用查询的 ORDER BY；查询也没有排序时报错：

<!-- card: query/basics/PreviousRow sql=mysql -->
```csharp
db.Query<Post>().OrderBy(p => p.PublishedAt).ThenBy(p => p.Id)
    .Select(p => new ViewsChange(p.Title, p.Views, Sql.Over.Lag(p.Views, 1, 0))).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t.`views`,LAG(t.`views`, 1, ?) OVER (ORDER BY t.`published_at` ASC,t.`id` ASC) AS `previous_views` FROM `t_post` t ORDER BY t.`published_at` ASC,t.`id` ASC;
-- params: [0]
```
<!-- /card -->

## 聚合

分组里的聚合写在 `IGroup` 上（见[分组与聚合](grouping.md)）；不分组时整表聚合用 `SumAsync` 等终结方法。表达式里也可以直接写 `Sql.Count()` / `Sql.Sum(x)` / `Sql.Avg(x)` / `Sql.Max(x)` / `Sql.Min(x)` / `Sql.CountDistinct(x)` / `Sql.StringJoin(x, ",")`，带过滤条件的重载 `Sql.Sum(x, 条件)` 写成 `SUM(CASE WHEN 条件 THEN x ELSE 0 END)`。

## 原生片段与自定义函数

`Sql.Raw<T>("模板", 参数…)`：模板里 `{0}`、`{1}` 是参数（列或值）。常用的函数写成方法标 `[SqlFunction("模板")]`，用法和内置函数一样：

<!-- card: func/RawFragment sql=mysql -->
```csharp
db.Query<User>().Where(u => Sql.Raw<int>("LENGTH({0})", u.Name) == 2).OrderBy(u => u.Id).Take(3).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE LENGTH(t.`name`) = ? ORDER BY t.`id` ASC LIMIT 3;
-- params: [2]
```
<!-- /card -->

<!-- snippet: func-sql-function -->
```csharp
[SqlFunction("REPLACE({0}, ' ', '')")]
public static string RemoveSpaces(this string value) => throw new InvalidOperationException("只能在查询里用");
```
<!-- /snippet -->

<!-- card: func/CustomFunction sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Title.RemoveSpaces().Contains("N+1")).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t WHERE REPLACE(t.`title`, ' ', '') LIKE ?;
-- params: ['%N+1%']
```
<!-- /card -->

## 方言支持

| 能力 | 不支持或有差异的方言 |
|---|---|
| `PadLeft` / `PadRight` | SQLite 用 `substr(replace(hex(zeroblob(n)), '00', c), …)` 拼出填充字符再连接、截取；SQL Server 写成 `RIGHT(REPLICATE(c, n) + LEFT(s, n), n)` / `LEFT(s + REPLICATE(c, n), n)`（Java 写 `LPAD` / `RPAD`，SQL Server 没有）；MySQL 不给填充字符时补 `' '`（Java 写两个参数，MySQL 执行不了） |
| `Sql.Json(..)` | 支持：MySQL / MariaDB、PostgreSQL 家族、SQLite、DuckDB、ClickHouse、Db2（没有 `Length()`）；SQL Server、Oracle、达梦、HANA、TDengine 渲染时报不支持 |
| `DateTrunc` | TDengine 只能截到天及以下 |
| 日期加减 | TDengine 只能写字面量时长，按参数加减报不支持 |
| 字符串聚合 | TDengine 不支持；HANA 的 `STRING_AGG` 不能去重；ClickHouse 不能带组内排序 |
| `Math.Round(x)` / `Atan2` / `Truncate` | SQL Server 按 Java 的写法改写（`ROUND(x,0)`、ATAN 加象限 CASE、FLOOR / CEILING 的 CASE） |
| 窗口函数 | MySQL 8.0+、SQLite 3.25+；排名与取值函数（`RowNumber`、`Rank`、`Lag`…）没写排序时，SQL Server 补 `ORDER BY (SELECT NULL)`、Oracle / 达梦补 `ORDER BY NULL`（这几个库要求写 ORDER BY），编号的先后不确定 |

## 限制

- 日期 `ToString(格式)` 只接受自定义格式（`"d"`、`"T"` 这类单字母的标准格式依赖区域设置，报不支持）。各方言能转换的格式符不同，用到方言没有的格式符时渲染报 `NotSupportedException`，不会当文字输出；格式串里的文字（引号里的、`\` 转义的、`T` 这类不是格式符的字母）原样输出：

  | 方言 | 支持的格式符（`yyyy` `MM` `dd` `HH` `mm` `ss` 各方言都有） |
  |---|---|
  | MySQL / MariaDB | `yy` `M` `d` `H` `hh` `h` `ffffff` `tt` |
  | PostgreSQL、Kingbase、openGauss | `yy` `M` `d` `H` `hh` `h` `m` `s` `fff` `ffffff` `tt` |
  | DuckDB | `yy` `M` `d` `H` `hh` `h` `m` `s` `fff` `ffffff` `tt` |
  | SQLite | `hh` `tt`（要 SQLite 3.44+） |
  | SQL Server、Oracle、达梦 | `yy` `M` `d` `H` `hh` `h` `m` `s` |
  | HANA、Db2 | `yy` `hh` `fff` |
  | ClickHouse | `yy` `hh` `tt` |
  | TDengine | `yy` `hh` `fff` |
- 日期相差在 DuckDB 上数的是跨过的边界（03:59 到 04:01 算 1 小时，与 Java 一致），与 .NET 的整段时长不同。
- `Convert.ToXxx` 只映射一个参数的重载；带进制、`IFormatProvider` 的重载报不支持。
- 正则（`Regex`）、`string.Format` 的格式说明符（`{0:N2}`）不翻译。
- 自定义函数的模板原样拼进 SQL，不同方言写法不同时要自己按方言选。

## 对应 Java

| Java | C# |
|---|---|
| `o.name().length()` / `toUpper()` / `subString(..)` / `trim()` | `o.Name.Length` / `ToUpper()` / `Substring(..)` / `Trim()` |
| `o.name().concat(..)` / `stringFormat` | `+`、`string.Concat`、插值字符串 |
| `like` / `contains` / `startsWith` / `endsWith` | `Contains` 等 / `LikeContains` / `LikeStartsWith` / `LikeEndsWith` |
| `o.createTime().format("yyyy-MM")` | `o.CreateTime.ToString("yyyy-MM")` |
| `plusDays(n)` / `plusMonths(n)` | `AddDays(n)` / `AddMonths(n)` |
| `duration(a, b).toDays()` | `(b - a).TotalDays` |
| `dayOfWeek()` / `dayOfWeekSunDayIsLastDay()`；fork 的 dateTrunc | `DayOfWeek` / `IsoDayOfWeek()`；`DateTrunc(DateTruncUnit.X)` |
| `nullOrDefault(..)`；fork 的 NULLIF 与 null 安全等值 | `??`；`NullIf` / `EqualsNullSafe` |
| `caseWhen(..).then(..).elseEnd(..)` | 三元表达式 |
| `toStr()` / `toNumber(Long.class)` / `cast` | `ToString()` / `long.Parse(..)` / `Sql.Cast<T>(..)` |
| `SQLFunc.greatest` / `least` | `Sql.MaxColumns` / `Sql.MinColumns` |
| `rowNumberOver().partitionBy(..).orderBy(..)` | `Sql.Over.RowNumber(w => w.PartitionBy(..).OrderBy(..))` |
| `x.offset(o -> o.partitionBy(..).orderBy(..)).prev(n)` / `next(n)` | `Sql.Over.Lag(x, n, w => ..)` / `Sql.Over.Lead(x, n, w => ..)` |
| `sqlNativeSegment("…", c -> c.expression(..))` / `expression().sql(..)` | `Sql.Raw<T>("…", ..)` |
| 自定义 `SQLFunction` | `[SqlFunction("模板")]` |
| fork 的方言静态入口（MySQL JSON 与全文、PG jsonb / 数组 / 全文、HANA 全文） | `Sql.MySql.Json(..)`、`Sql.Pg.Jsonb(..)` 等 |
