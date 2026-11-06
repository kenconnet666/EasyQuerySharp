# 写入

写入命令从 `db.Insert(..)`、`db.Update(..)`、`db.Delete(..)` 开始，链式配置，一律以 `ExecuteAsync()` 结束（返回受影响的行数），读代码时一眼看出哪里真正执行。`ToSql()` / `ToStatement()` 只生成 SQL 不执行。拦截器、逻辑删除、乐观锁、加密对写入同样生效（见[列规则](rules.md)）；改完整个聚合一次保存见[追踪与聚合保存](save-and-tracking.md)。

## 插入

<!-- card: write/InsertOne sql=mysql -->
```csharp
db.Insert(new Tag { Name = "性能" }).ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_tag` (`name`) VALUES (?);
-- params: ['性能']
```
<!-- /card -->

多个实体默认每行一条语句；`.Batch()` 把同一 SQL 的多行一次发出（驱动支持 `DbBatch` 时交给它，否则同一连接逐条执行），不在事务里时整批在一个事务里：

<!-- card: write/InsertBatch sql=mysql -->
```csharp
db.Insert(new Tag { Name = "前端" }, new Tag { Name = "后端" }, new Tag { Name = "数据" }).Batch().ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_tag` (`name`) VALUES (?);
-- params: ['前端']
-- params: ['后端']
-- params: ['数据']
```
<!-- /card -->

`FillGeneratedKeys()` 把数据库生成的主键回填到实体，各方言取法不同：

<!-- card: writes/FillGeneratedKeys sql=mysql,pgsql,mssql -->
```csharp
Tag[] tags = [new() { Name = "测试" }, new() { Name = "运维" }];
await db.Insert(tags).FillGeneratedKeys().ExecuteAsync();
return [.. tags.Select(t => t.Id)];
```

```sql
-- MySQL
INSERT INTO `t_tag` (`name`) VALUES (?);
-- params: ['测试']
SELECT LAST_INSERT_ID();
INSERT INTO `t_tag` (`name`) VALUES (?);
-- params: ['运维']
SELECT LAST_INSERT_ID();
```

```sql
-- PostgreSQL
INSERT INTO "t_tag" ("name") VALUES (?) RETURNING "id";
-- params: ['测试']
INSERT INTO "t_tag" ("name") VALUES (?) RETURNING "id";
-- params: ['运维']
```

```sql
-- SQL Server
INSERT INTO [t_tag] ([name]) OUTPUT INSERTED.[id] VALUES (?);
-- params: ['测试']
INSERT INTO [t_tag] ([name]) OUTPUT INSERTED.[id] VALUES (?);
-- params: ['运维']
```
<!-- /card -->

| 选项 | 作用 |
|---|---|
| `.Columns(ColumnStrategy.OnlyNotNull)` / `OnlyNull` / `All` | 只写非 null 的列 / 只写 null 的列 / 全部（默认） |
| `.ColumnSql(p => p.列, "SQL 模板")` | 这一列的值用 SQL 包一层：`{0}` 是列，`{1}` 是实体上的值（对应 Java 的 `columnConfigure`） |
| `.AsTable(..)` / `.AsSchema(..)` / `.WithTableLink(..)` | 写到别的表，见[换表](tables.md) |
| `.DisableInterceptor(..)` 等 | 关掉拦截器 |

<!-- card: writes/InsertOnlyNotNull sql=mysql -->
```csharp
db.Insert(new Profile { UserId = 5, Bio = "做数据的", Website = null }).Columns(ColumnStrategy.OnlyNotNull).ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_profile` (`user_id`,`bio`,`skills`) VALUES (?,?,?);
-- params: [5, '做数据的', '[]']
```
<!-- /card -->

没有实体类时按字典写入，键是列名（`EasyDbOptions.MapKeyConversion` 可以换算键）：

<!-- card: writes/Maps sql=mysql -->
```csharp
await db.InsertMap("t_tag", new Dictionary<string, object?> { ["name"] = "数据库" }).ExecuteAsync();
return await db.UpdateMap("t_product", new Dictionary<string, object?> { ["id"] = 6L, ["stock"] = 150 }).WhereColumns("id").ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_tag` (`name`) VALUES (?);
-- params: ['数据库']
UPDATE `t_product` SET `stock` = ? WHERE `id` = ?;
-- params: [150, 6]
```
<!-- /card -->

查询结果直接写进另一张表（`INSERT … SELECT`）：投影成目标实体，再 `InsertInto()`；`.Column(名, 值)` 补投影里没有的列：

<!-- card: writes/InsertSelect sql=mysql -->
```csharp
db.Query<Category>().Where(c => c.ParentId == 1).Select(c => new Tag { Name = c.Name }).InsertInto().ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_tag` (`name`) SELECT eq_src.`name` FROM (SELECT t1.`name` FROM (SELECT t.`name` FROM `t_category` t WHERE t.`parent_id` = ?) t1) eq_src;
-- params: [1]
```
<!-- /card -->

## upsert：冲突时忽略或更新

`OnConflictIgnore(冲突列?)` / `OnConflictUpdate(更新列?, 冲突列?)`，冲突列不写时取主键，更新列不写时更新除冲突列以外的全部列：

<!-- card: writes/OnConflictIgnore sql=mysql,pgsql,mssql,oracle -->
```csharp
db.Insert(new PostTag { PostId = 1, TagId = 1 }).OnConflictIgnore().ExecuteAsync();
```

```sql
-- MySQL
INSERT IGNORE INTO `t_post_tag` (`post_id`,`tag_id`) VALUES (?,?);
-- params: [1, 1]
```

```sql
-- PostgreSQL
INSERT INTO "t_post_tag" ("post_id","tag_id") VALUES (?,?) ON CONFLICT DO NOTHING;
-- params: [1, 1]
```

```sql
-- SQL Server
MERGE INTO [t_post_tag] t1 USING (SELECT ? AS [post_id],? AS [tag_id] ) t2 ON (t1.[post_id] = t2.[post_id] AND t1.[tag_id] = t2.[tag_id]) WHEN NOT MATCHED THEN INSERT ([post_id],[tag_id]) VALUES (t2.[post_id],t2.[tag_id]);
-- params: [1, 1]
```

```sql
-- Oracle
MERGE INTO "t_post_tag" t1 USING (SELECT ? AS "post_id",? AS "tag_id" FROM DUAL ) t2 ON (t1."post_id" = t2."post_id" AND t1."tag_id" = t2."tag_id") WHEN NOT MATCHED THEN INSERT ("post_id","tag_id") VALUES (t2."post_id",t2."tag_id");
-- params: [1, 1]
```
<!-- /card -->

<!-- card: writes/OnConflictUpdate sql=mysql,pgsql,mssql,oracle -->
```csharp
db.Insert(new OrderItem { OrderId = 1, ProductId = 6, Quantity = 3, Price = 29.90m })
    .OnConflictUpdate(i => new { i.Quantity, i.Price })
    .ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_order_item` (`order_id`,`product_id`,`quantity`,`price`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `quantity` = VALUES(`quantity`), `price` = VALUES(`price`);
-- params: [1, 6, 3, 29.90]
```

```sql
-- PostgreSQL
INSERT INTO "t_order_item" ("order_id","product_id","quantity","price") VALUES (?,?,?,?) ON CONFLICT ("order_id","product_id") DO UPDATE SET "quantity" = EXCLUDED."quantity", "price" = EXCLUDED."price";
-- params: [1, 6, 3, 29.90]
```

```sql
-- SQL Server
MERGE INTO [t_order_item] t1 USING (SELECT ? AS [order_id],? AS [product_id],? AS [quantity],? AS [price] ) t2 ON (t1.[order_id] = t2.[order_id] AND t1.[product_id] = t2.[product_id]) WHEN MATCHED THEN UPDATE SET t1.[quantity] = t2.[quantity],t1.[price] = t2.[price] WHEN NOT MATCHED THEN INSERT ([order_id],[product_id],[quantity],[price]) VALUES (t2.[order_id],t2.[product_id],t2.[quantity],t2.[price]);
-- params: [1, 6, 3, 29.90]
```

```sql
-- Oracle
MERGE INTO "t_order_item" t1 USING (SELECT ? AS "order_id",? AS "product_id",? AS "quantity",? AS "price" FROM DUAL ) t2 ON (t1."order_id" = t2."order_id" AND t1."product_id" = t2."product_id") WHEN MATCHED THEN UPDATE SET t1."quantity" = t2."quantity",t1."price" = t2."price" WHEN NOT MATCHED THEN INSERT ("order_id","product_id","quantity","price") VALUES (t2."order_id",t2."product_id",t2."quantity",t2."price");
-- params: [1, 6, 3, 29.90]
```
<!-- /card -->

| 方言 | 忽略 | 更新 |
|---|---|---|
| MySQL / MariaDB / PolarDB | `INSERT IGNORE`（或 `new MySqlDialect { IgnoreConflictAsKeyUpdate = true }` 写成 `ON DUPLICATE KEY UPDATE 主键 = VALUES(主键)`） | `ON DUPLICATE KEY UPDATE` |
| PostgreSQL、Kingbase、SQLite、DuckDB | `ON CONFLICT DO NOTHING` | `ON CONFLICT (…) DO UPDATE SET … = EXCLUDED.…` |
| GaussDB / openGauss（没有 `ON CONFLICT`） | `ON DUPLICATE KEY UPDATE NOTHING` | `ON DUPLICATE KEY UPDATE … = EXCLUDED.…`（冲突由表上的主键 / 唯一约束决定，不写冲突列） |
| SQL Server、Oracle、达梦、Db2、HANA | `MERGE`（一次一行） | `MERGE` |
| ClickHouse、TDengine | 不支持 | 不支持 |

## 按条件更新

`db.Update<T>()` 加 `Set` 和 `Where`。`Set` 可以写成对象初始化器（值可以引用原列），也可以一列一列写：

<!-- card: write/UpdateWithExpression sql=mysql -->
```csharp
db.Update<Post>().Set(p => new Post { Views = p.Views + 100, Title = p.Title + "（修订）" }).Where(p => p.Id == 1).ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_post` SET `views` = (`views` + ?),`title` = CONCAT(`title`,?) WHERE `id` = ?;
-- params: [100, '（修订）', 1]
```
<!-- /card -->

<!-- card: writes/UpdateSets sql=mysql -->
```csharp
var newName = (string?)null;
return db.Update<Product>()
    .SetIf(newName is not null, p => p.Name, newName!)
    .Increment(p => p.Stock, 10)
    .Set(p => p.Price, p => p.Price * 0.9m)
    .Where(p => p.Stock < 30)
    .ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_product` SET `stock` = `stock` + ?,`price` = (`price` * ?) WHERE `stock` < ?;
-- params: [10, 0.9, 30]
```
<!-- /card -->

| 方法 | 作用 |
|---|---|
| `Set(x => new T { A = .., B = x.B + 1 })` | 一次设几列 |
| `Set(x => x.A, 值)` / `Set(x => x.A, x => 表达式)` | 一列 |
| `SetIf(条件, ..)` | 条件成立时才设 |
| `SetNull(x => x.A)` | 设成 NULL |
| `Increment(x => x.A, n)` / `Decrement` / `IncrementIf` / `DecrementIf` | 加减 |
| `Where(..)` / `WhereIf(..)` / `WhereById(id)` / `WhereByIds(ids)` | 条件；条件里可以用导航 |
| `All()` | 确认整表更新（没有条件又没写 `All()` 时抛异常） |

条件里可以用集合导航（子查询）和对一导航（JOIN），各方言写法不同：

<!-- card: write/UpdateThroughNavigation sql=mysql -->
```csharp
db.Update<User>().Set(u => u.Level, UserLevel.Senior).Where(u => u.Posts!.Any(p => p.Views >= 2000)).ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_user` t SET t.`level` = ? WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` AND t1.`views` >= ? LIMIT 1);
-- params: ['senior', 2000]
```
<!-- /card -->

<!-- card: writes/UpdateJoin sql=mysql,pgsql,mssql,oracle,sqlite -->
```csharp
db.Update<Post>().Set(p => p.Status, PostStatus.Draft).Where(p => p.Author!.Age < 18).ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_post` t INNER JOIN `t_user` t1 ON t1.`id` = t.`author_id` SET t.`status` = ? WHERE t1.`age` < ?;
-- params: ['draft', 18]
```

```sql
-- PostgreSQL
UPDATE "t_post" t SET "status" = ? FROM "t_user" t1 WHERE t1."id" = t."author_id" AND t1."age" < ?;
-- params: ['draft', 18]
```

```sql
-- SQL Server
UPDATE t SET t.[status] = ? FROM [t_post] t INNER JOIN [t_user] t1 ON t1.[id] = t.[author_id] WHERE t1.[age] < ?;
-- params: ['draft', 18]
```

```sql
-- Oracle
MERGE INTO "t_post" eq_w USING (SELECT t.ROWID AS "__rid__",? AS "__v0__" FROM "t_post" t INNER JOIN "t_user" t1 ON t1."id" = t."author_id" WHERE t1."age" < ?) eq_src ON (eq_w.ROWID = eq_src."__rid__") WHEN MATCHED THEN UPDATE SET eq_w."status" = eq_src."__v0__";
-- params: ['draft', 18]
```

```sql
-- SQLite
UPDATE "t_post" AS t SET "status" = ? FROM "t_user" t1 WHERE t1."id" = t."author_id" AND t1."age" < ?;
-- params: ['draft', 18]
```
<!-- /card -->

## 按实体更新

`db.Update(实体)` 按主键写全部列（`[UpdateIgnore]` 的除外）。只写部分列用 `SetColumns(..)`，跳过几列用 `IgnoreColumns(..)`，换定位列用 `WhereColumns(..)`：

<!-- card: write/UpdateEntity sql=mysql -->
```csharp
var product = await db.Query<Product>().FindAsync(6L);
product!.Price = 25.00m;
return await db.Update(product).ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [6]
UPDATE `t_product` SET `name` = ?,`price` = ?,`stock` = ? WHERE `id` = ?;
-- params: ['贴纸礼包', 25.00, 200, 6]
```
<!-- /card -->

<!-- card: write/UpdateColumns sql=mysql -->
```csharp
var product = await db.Query<Product>().FindAsync(6L);
product!.Stock -= 1;
return await db.Update(product).SetColumns(p => new { p.Stock }).ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [6]
UPDATE `t_product` SET `stock` = ? WHERE `id` = ?;
-- params: [199, 6]
```
<!-- /card -->

<!-- card: writes/UpdateWhereColumns sql=mysql -->
```csharp
var product = await db.Query<Product>().FindOrThrowAsync(6L);
product.Price = 19.90m;
product.Stock = 0;
return await db.Update(product).WhereColumns(p => p.Name).IgnoreColumns(p => p.Stock).ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [6]
UPDATE `t_product` SET `price` = ? WHERE `name` = ?;
-- params: [19.90, '贴纸礼包']
```
<!-- /card -->

只写改过的列：在追踪作用域里查出来的实体自动按差异更新（见[追踪与聚合保存](save-and-tracking.md)）；不追踪时用 `Changes(修改前)` 对比两份：

<!-- card: writes/UpdateChanges sql=mysql -->
```csharp
var before = await db.Query<Product>().FindOrThrowAsync(6L);
var after = new Product { Id = before.Id, Name = before.Name, Price = before.Price, Stock = before.Stock - 5 };
return await db.Update(after).Changes(before).ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [6]
UPDATE `t_product` SET `stock` = ? WHERE `id` = ?;
-- params: [195, 6]
```
<!-- /card -->

<!-- card: writes/UpdateBatch sql=mysql -->
```csharp
var products = await db.Query<Product>().Where(p => p.Stock >= 999).ToListAsync();
foreach (var p in products) p.Stock = 500;
return await db.Update(products).Batch().ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `stock` >= ?;
-- params: [999]
UPDATE `t_product` SET `name` = ?,`price` = ?,`stock` = ? WHERE `id` = ?;
-- params: ['年度会员', 299, 500, 1]
-- params: ['季度会员', 99, 500, 2]
```
<!-- /card -->

全局默认写哪些列：`EasyDbOptions.UpdateStrategy`（如 `ColumnStrategy.OnlyNotNull`）。

## 删除

<!-- card: write/Delete sql=mysql -->
```csharp
db.Delete<Comment>().Where(c => c.Likes < 3).ExecuteAsync();
```

```sql
-- MySQL
DELETE FROM `t_comment` WHERE `likes` < ?;
-- params: [3]
```
<!-- /card -->

<!-- card: writes/DeleteJoin sql=mysql,pgsql,mssql,oracle,sqlite -->
```csharp
db.Delete<Comment>().Where(c => c.Post!.Status == PostStatus.Draft).ExecuteAsync();
```

```sql
-- MySQL
DELETE t FROM `t_comment` t INNER JOIN `t_post` t1 ON t1.`id` = t.`post_id` WHERE t1.`status` = ?;
-- params: ['draft']
```

```sql
-- PostgreSQL
DELETE FROM "t_comment" t USING "t_post" t1 WHERE t1."id" = t."post_id" AND t1."status" = ?;
-- params: ['draft']
```

```sql
-- SQL Server
DELETE t FROM [t_comment] t INNER JOIN [t_post] t1 ON t1.[id] = t.[post_id] WHERE t1.[status] = ?;
-- params: ['draft']
```

```sql
-- Oracle
DELETE FROM "t_comment" WHERE ROWID IN (SELECT t.ROWID FROM "t_comment" t INNER JOIN "t_post" t1 ON t1."id" = t."post_id" WHERE t1."status" = ?);
-- params: ['draft']
```

```sql
-- SQLite
DELETE FROM "t_comment" AS t WHERE EXISTS(SELECT 1 FROM "t_post" t1 WHERE t1."id" = t."post_id" AND t1."status" = ?);
-- params: ['draft']
```
<!-- /card -->

<!-- card: writes/DeleteEntitiesAndAll sql=mysql -->
```csharp
var follows = await db.Query<Follow>().Where(f => f.FollowerId == 1).ToListAsync();
await db.Delete(follows).ExecuteAsync();
return await db.Delete<PostTag>().All().ExecuteAsync();
```

```sql
-- MySQL
SELECT `follower_id`,`followee_id` FROM `t_follow` WHERE `follower_id` = ?;
-- params: [1]
DELETE FROM `t_follow` WHERE `follower_id` = ? AND `followee_id` = ?;
-- params: [1, 2]
DELETE FROM `t_follow` WHERE `follower_id` = ? AND `followee_id` = ?;
-- params: [1, 3]
DELETE FROM `t_post_tag`;
```
<!-- /card -->

- 有逻辑删除的实体，删除改写成 UPDATE；要真删用 `.Hard()`（见[列规则](rules.md#逻辑删除)）。
- `EasyDbOptions.AllowDeleteStatement = false` 时，没有逻辑删除的实体也不能物理删除，除非命令上 `AllowDeleteStatement(true)`，防止误删。

## 影响的行数与 RETURNING

`ExecuteAsync(期望行数, 消息)`：影响的行数不等于期望时抛 `ConcurrencyException`：

<!-- card: writes/ExpectedRows sql=mysql -->
```csharp
try
{
    await db.Update<Product>().Set(p => p.Stock, 0).Where(p => p.Id == 999).ExecuteAsync(1, "商品不存在");
    return "updated";
}
catch (ConcurrencyException ex)
{
    return ex.Message;
}
```

```sql
-- MySQL
UPDATE `t_product` SET `stock` = ? WHERE `id` = ?;
-- params: [0, 999]
```
<!-- /card -->

按条件的更新 / 删除加 `.Returning()`（整行）或 `.Returning(x => 选择器)` 读回受影响的行（PostgreSQL 及继承它的 DuckDB、Kingbase、GaussDB，以及 SQLite 支持；其余方言渲染时报不支持）：

<!-- card: write/UpdateReturning sql=pgsql,mysql -->
```csharp
db.Update<Product>().Set(p => new Product { Stock = p.Stock - 1 }).Where(p => p.Id == 5).Returning(p => p.Stock).ExecuteAsync();
```

```sql
-- PostgreSQL
UPDATE "t_product" SET "stock" = ("stock" - ?) WHERE "id" = ? RETURNING "stock";
-- params: [1, 5]
```

```sql
-- MySQL
-- error: mysql 不支持 UPDATE / DELETE … RETURNING
```
<!-- /card -->

<!-- card: writes/DeleteReturning sql=sqlite -->
```csharp
db.Delete<Comment>().Where(c => c.Likes <= 2).Returning().ExecuteAsync();
```

```sql
-- SQLite
DELETE FROM "t_comment" WHERE "likes" <= ? RETURNING *;
-- params: [2]
```
<!-- /card -->

## 方言差异

| 能力 | 差异 |
|---|---|
| 回填自增键 | PG / DuckDB `RETURNING`，SQLite `last_insert_rowid()`，SQL Server `OUTPUT INSERTED`，Oracle / 达梦 `RETURNING … INTO`，MySQL 每条插入后 `SELECT LAST_INSERT_ID()`，HANA `CURRENT_IDENTITY_VALUE()`，Db2 `IDENTITY_VAL_LOCAL()`；ClickHouse、TDengine 没有自增列 |
| UPDATE / DELETE 里的对一导航 | MySQL `UPDATE t INNER JOIN … SET` / `DELETE t FROM … JOIN`；PG `UPDATE … FROM` / `DELETE … USING`；SQL Server `UPDATE t SET … FROM … JOIN`；SQLite `UPDATE … FROM` / `DELETE … WHERE EXISTS`；Oracle 按 ROWID 的 `MERGE` / `DELETE … WHERE ROWID IN`；达梦 `UPDATE t INNER JOIN … SET`、删除同 Oracle；HANA、Db2 按主键 MERGE / EXISTS；ClickHouse、TDengine 不支持 |
| 更新、删除本身 | ClickHouse 写成 `ALTER TABLE … UPDATE / DELETE`（不能带别名、不能用导航）；TDengine 没有 UPDATE，删除只能按本表的列过滤 |
| `RETURNING` | PostgreSQL 及继承它的 DuckDB、Kingbase、GaussDB，SQLite |
| 批量 `.Batch()` | Npgsql 一组一个往返；MySqlConnector 的 `DbBatch` 不省往返，但整批一个事务，比逐条提交快得多；SQLite 同连接逐条执行 |

## 限制

- 批量写入按 SQL 分组，每组最多 1000 条；插入的列不同（如 `OnlyNotNull` 下各行的空列不同）会分成几组。
- `MERGE` 实现的 upsert（SQL Server、Oracle、达梦、Db2、HANA）一次只能插入一行，多行时逐条执行。
- 按条件更新没有任何 SET 时抛异常（Java 是静默返回 0）。
- `RETURNING` 不能用在走导航连接的更新 / 删除和按实体写入上。

## 对应 Java

| Java | C# |
|---|---|
| `insertable(entity).executeRows()` / `executeRows(true)` | `db.Insert(entity).ExecuteAsync()` / `.FillGeneratedKeys().ExecuteAsync()` |
| `insertable(list).batch()` | `db.Insert(list).Batch()` |
| `setSQLStrategy(SQLExecuteStrategyEnum.ONLY_NOT_NULL_COLUMNS)` | `.Columns(ColumnStrategy.OnlyNotNull)` |
| `mapInsertable` / `mapUpdatable`、`MapKeyNameConversion` | `db.InsertMap` / `db.UpdateMap`、`EasyDbOptions.MapKeyConversion` |
| fork 的类型化 INSERT … SELECT | `q.Select(..).InsertInto()` |
| `onConflictThen(o -> o.FETCHER.allFields())` / `onConflictThen(null)` | `OnConflictUpdate()` / `OnConflictIgnore()` |
| `onConflictThen(更新列, 冲突列)` | `OnConflictUpdate(x => new { .. }, x => new { .. })` |
| `mysqlInsertIgnoreToUpdateKey` 配置 | `new MySqlDialect { IgnoreConflictAsKeyUpdate = true }` |
| `updatable(T.class).setColumns(o -> o.a().set(..)).where(..)` | `db.Update<T>().Set(x => new T { A = .. }).Where(..)` |
| `set(..)` / `setIncrement(..)` / `setDecrement(..)` | `Set(..)` / `Increment(..)` / `Decrement(..)` |
| `updatable(entity)` / `setColumns(..)` / `setIgnoreColumns(..)` / `whereColumns(..)` | `db.Update(entity)` / `SetColumns(..)` / `IgnoreColumns(..)` / `WhereColumns(..)` |
| `deletable(T.class).where(..)` / `allowDeleteStatement(true)` | `db.Delete<T>().Where(..)` / `AllowDeleteStatement(true)` |
| `executeRows(1, "msg")` | `ExecuteAsync(1, "msg")` |
| fork 的 `returning` | `.Returning()` / `.Returning(x => ..)` |
| `toSQL()` | `ToSql()` |
