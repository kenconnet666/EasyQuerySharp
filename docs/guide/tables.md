# 换表：表名、schema、表引用

实体的表名、schema 来自 `[Table]`；同一个实体要读写别的表（按年份归档的表、另一个 schema、跨库链接）时，在查询或写入上换掉。本页是手动指定表名的写法；按值自动路由到分表、分库见[分库分表](sharding.md)。

| 写法 | 作用范围 |
|---|---|
| `q.AsTable("表名")` / `AsTable(name => …)` | 最近一张表（`From` 的主表或刚 JOIN 的表） |
| `Sql.AsTable(p.Author, "表名")`、`u.Posts!.AsTable("表名")` | 导航上的那张表 |
| `q.MapTable<T>("表名")` / `MapTable<T>(name => …)` | 整条查询里所有 `T` 的表：主表、JOIN、隐式 JOIN、集合导航的子查询与分组合并、取第几行、多对多中间表、带出查询、嵌在里面的子查询 |
| `q.InSchema("schema")` | 整条查询里所有实体表的 schema，范围同 `MapTable` |
| `q.AsTableSegment((表, 别名) => …)`、`Sql.AsTableSegment(导航, ..)` | 整个表引用自己写：表提示、表函数、子查询 |
| `q.WithTableLink("LEFT OUTER JOIN")` / `WithTableLink(join => join + " LATERAL")` | 改写最近一张表前面的连接符 |
| 写入命令上 `AsTable(..)` / `AsSchema(..)` / `WithTableLink(..)` | 插入、更新、删除的目标表 |

优先级：显式的 `AsTable` / `Sql.AsTable` 高于 `MapTable`；子查询自己的 `MapTable` 高于外层的。

## 一张表

<!-- card: tables/as-table/AsTable sql=mysql -->
```csharp
await db.ExecuteSqlRawAsync("CREATE TABLE t_tag_2025 AS SELECT * FROM t_tag");
try
{
    return await db.Query<Tag>().AsTable("t_tag_2025").Where(t => t.Id <= 3).OrderBy(t => t.Id).Select(t => t.Name).ToListAsync();
}
finally
{
    await db.ExecuteSqlRawAsync("DROP TABLE t_tag_2025");
}
```

```sql
-- MySQL
CREATE TABLE t_tag_2025 AS SELECT * FROM t_tag;
SELECT t.`name` FROM `t_tag_2025` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [3]
DROP TABLE t_tag_2025;
```
<!-- /card -->

<!-- card: tables/as-table/AsTableByName sql=mysql -->
```csharp
await db.ExecuteSqlRawAsync("CREATE TABLE t_user_archive AS SELECT * FROM t_user");
try
{
    var archived = await db.Query<User>().AsTable(name => name + "_archive").Where(u => u.Age >= 35).Select(u => u.Name).ToListAsync();
    var posts = await db.Query<Post>().Where(p => Sql.AsTable(p.Author, "t_user_archive")!.Age >= 35).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
    return [.. archived, .. posts];
}
finally
{
    await db.ExecuteSqlRawAsync("DROP TABLE t_user_archive");
}
```

```sql
-- MySQL
CREATE TABLE t_user_archive AS SELECT * FROM t_user;
SELECT t.`name` FROM `t_user_archive` t WHERE t.`age` >= ?;
-- params: [35]
SELECT t.`title` FROM `t_post` t LEFT JOIN `t_user_archive` t1 ON t1.`id` = t.`author_id` WHERE t1.`age` >= ? ORDER BY t.`id` ASC;
-- params: [35]
DROP TABLE t_user_archive;
```
<!-- /card -->

## 整条查询

`MapTable<T>` 让这条查询里出现的每一张 `T` 表都换掉，包括条件和投影里的导航子查询：

<!-- card: tables/MapTable sql=mysql -->
```csharp
await db.ExecuteSqlRawAsync("CREATE TABLE t_post_2025 AS SELECT * FROM t_post WHERE id <= 2");
try
{
    return await db.Query<User>().MapTable<Post>("t_post_2025")
        .Where(u => u.Posts!.Any(p => p.Views >= 100))
        .OrderBy(u => u.Id)
        .Select(u => new ArchivedPosts(u.Name, u.Posts!.Count()))
        .ToListAsync();
}
finally
{
    await db.ExecuteSqlRawAsync("DROP TABLE t_post_2025");
}
```

```sql
-- MySQL
CREATE TABLE t_post_2025 AS SELECT * FROM t_post WHERE id <= 2;
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post_2025` t2 WHERE t2.`author_id` = t.`id`) AS `posts` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post_2025` t1 WHERE t1.`author_id` = t.`id` AND t1.`views` >= ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: [100]
DROP TABLE t_post_2025;
```
<!-- /card -->

<!-- card: tables/InSchema sql=mysql,mssql -->
```csharp
db.Query<Post>().InSchema("main").Where(p => p.Views >= 1000).OrderBy(p => p.Id).Select(p => new PostByAuthor(p.Title, p.Author!.Name)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t1.`name` AS `author` FROM `main`.`t_post` t LEFT JOIN `main`.`t_user` t1 ON t1.`id` = t.`author_id` WHERE t.`views` >= ? ORDER BY t.`id` ASC;
-- params: [1000]
```

```sql
-- SQL Server
SELECT t.[title],t1.[name] AS [author] FROM [main].[t_post] t LEFT JOIN [main].[t_user] t1 ON t1.[id] = t.[author_id] WHERE t.[views] >= ? ORDER BY t.[id] ASC;
-- params: [1000]
```
<!-- /card -->

## 表引用与连接符

`AsTableSegment` 拿到渲染好的表名（带引号）和别名，返回整个表引用的写法，常用于 SQL Server 的 `WITH (NOLOCK)`、DuckDB 的表函数：

<!-- card: tables/as-table/AsTableSegment sql=mysql -->
```csharp
db.Query<Post>().AsTableSegment((table, alias) => $"(SELECT * FROM {table} WHERE status = 'published') {alias}")
    .Where(p => p.Views >= 2000).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM (SELECT * FROM `t_post` WHERE status = 'published') t WHERE t.`views` >= ? ORDER BY t.`id` ASC;
-- params: [2000]
```
<!-- /card -->

<!-- card: tables/TableLink sql=mysql -->
```csharp
db.Query<Post>().LeftJoin<User>((p, u) => p.AuthorId == u.Id).WithTableLink("LEFT OUTER JOIN")
    .Where((p, _) => p.Views >= 1000).OrderBy((p, _) => p.Id).Select((p, u) => new PostByAuthor(p.Title, u.Name)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title`,t1.`name` AS `author` FROM `t_post` t LEFT OUTER JOIN `t_user` t1 ON t.`author_id` = t1.`id` WHERE t.`views` >= ? ORDER BY t.`id` ASC;
-- params: [1000]
```
<!-- /card -->

## 写入

<!-- card: tables/as-table/WriteToOtherTable sql=mysql,pgsql -->
```csharp
await db.ExecuteSqlRawAsync("CREATE TABLE t_tag_archive AS SELECT * FROM t_tag WHERE 1 = 0");
try
{
    await db.Insert(new Tag { Name = "归档" }).AsTable("t_tag_archive").ExecuteAsync();
    return await db.Update<Tag>().AsSchema("main").Set(t => t.Name, "ORM 框架").Where(t => t.Id == 1).ExecuteAsync();
}
finally
{
    await db.ExecuteSqlRawAsync("DROP TABLE t_tag_archive");
}
```

```sql
-- MySQL
CREATE TABLE t_tag_archive AS SELECT * FROM t_tag WHERE 1 = 0;
INSERT INTO `t_tag_archive` (`name`) VALUES (?);
-- params: ['归档']
UPDATE `main`.`t_tag` SET `name` = ? WHERE `id` = ?;
-- params: ['ORM 框架', 1]
DROP TABLE t_tag_archive;
```

```sql
-- PostgreSQL
CREATE TABLE t_tag_archive AS SELECT * FROM t_tag WHERE 1 = 0;
INSERT INTO "t_tag_archive" ("name") VALUES (?);
-- params: ['归档']
UPDATE "main"."t_tag" SET "name" = ? WHERE "id" = ?;
-- params: ['ORM 框架', 1]
DROP TABLE t_tag_archive;
```
<!-- /card -->

## 限制

- `MapTable` / `InSchema` 是查询上的不可变状态，没有作用域，也读不出当前映射（Java 的 `TableNameMapping` 可以在作用域里读）。
- 走导航的更新 / 删除不能用 `WithTableLink`（报不支持）。
- 换表只改 SQL 里的名字，不检查表是否存在、结构是否一致。
- 按值自动路由到分表（按月、取模）用[分库分表](sharding.md)。

## 对应 Java

| Java | C# |
|---|---|
| `asTable("t_x")` / `asTable(old -> old + "_2025")` | `AsTable("t_x")` / `AsTable(name => name + "_2025")` |
| `asSchema("s")` | 写入上 `AsSchema("s")`；查询上 `InSchema("s")` |
| fork 的 `TableNameMapping`（查询级换表） | `MapTable<T>(..)` |
| `asTableLink(..)` | `WithTableLink(..)` |
| `asTableSegment((table, alias) -> ..)` | `AsTableSegment((table, alias) => ..)` |
| 导航上的 `asTable` / `asAlias` | `Sql.AsTable(nav, ..)`、`nav.AsTable(..)` / `Sql.AsAlias(..)` |
| `asAlias("x")` | `AsAlias("x")` |
