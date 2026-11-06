# 原生 SQL

三个层次：整条手写的 SQL、作为查询来源继续组合的原生 SQL、查询里的原生片段。

## 整条原生 SQL

| 方法 | 结果 |
|---|---|
| `db.SqlQueryAsync<T>($"…{值}…")` | `List<T>`：`T` 是标量时读第一列，是类时按列名映射到属性（属性名按命名约定换算成列名，忽略大小写；对不上的列忽略） |
| `db.ExecuteSqlAsync($"…{值}…")` | 受影响的行数 |
| `db.SqlQueryRawAsync<T>("… ? …", [值…])` / `ExecuteSqlRawAsync("…", [值…])` | 普通字符串，`?` 位置参数（Java 的写法） |

方法成对出现（与 EF Core 的 `FromSql` / `FromSqlRaw` 一样）：不带 `Raw` 的只收插值字符串（`FormattableString`），`{值}` 全部参数化，集合展开成 `?,?,?`（空集合报错），`Sql.Identifier("名字")` 按方言加引号；带 `Raw` 的收普通字符串，文本原样执行。插值字符串传给不带 `Raw` 的方法时一定参数化，存进 `string` 变量之后就只能传给 `…Raw`，名字上就看得出是在拼文本：

<!-- card: raw/SqlQuery sql=mysql,pgsql -->
```csharp
var minAge = 30;
long[] companies = [1, 2];
return db.SqlQueryAsync<NameAge>($"SELECT name, age FROM t_user WHERE age >= {minAge} AND company_id IN ({companies}) ORDER BY id");
```

```sql
-- MySQL
SELECT name, age FROM t_user WHERE age >= ? AND company_id IN (?,?) ORDER BY id;
-- params: [30, 1, 2]
```

```sql
-- PostgreSQL
SELECT name, age FROM t_user WHERE age >= ? AND company_id IN (?,?) ORDER BY id;
-- params: [30, 1, 2]
```
<!-- /card -->

<!-- card: raw/ScalarAndIdentifier sql=mysql,mssql -->
```csharp
db.SqlQueryAsync<long>($"SELECT COUNT(*) FROM {Sql.Identifier("t_post")} WHERE {Sql.Identifier("views")} >= {1000}");
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_post` WHERE `views` >= ?;
-- params: [1000]
```

```sql
-- SQL Server
SELECT COUNT(*) FROM [t_post] WHERE [views] >= ?;
-- params: [1000]
```
<!-- /card -->

<!-- card: raw/ExecuteSql sql=mysql -->
```csharp
db.ExecuteSqlAsync($"UPDATE t_product SET stock = stock - {1} WHERE id = {5}");
```

```sql
-- MySQL
UPDATE t_product SET stock = stock - ? WHERE id = ?;
-- params: [1, 5]
```
<!-- /card -->

<!-- card: raw/Positional sql=mysql,oracle -->
```csharp
db.SqlQueryRawAsync<string>("SELECT name FROM t_tag WHERE id > ? ORDER BY id", [4L]);
```

```sql
-- MySQL
SELECT name FROM t_tag WHERE id > ? ORDER BY id;
-- params: [4]
```

```sql
-- Oracle
SELECT name FROM t_tag WHERE id > ? ORDER BY id;
-- params: [4]
```
<!-- /card -->

`?` 占位时，引号、反引号里的 `?` 不算占位符；插值写法里的 `?` 原样保留（如 PostgreSQL 的 jsonb 运算符）。

## 原生 SQL 作为查询来源

`db.FromSql<T>($"…{值}…")`（或 `db.FromSqlRaw<T>("… ? …", 参数…)`）把一段 SQL 当作 `T` 的表，之后照常 Where / OrderBy / Select / 分页（外面包一层派生表），参数与外层的参数一起编号：

<!-- card: raw/FromSql sql=mysql -->
```csharp
db.FromSql<User>($"SELECT * FROM t_user WHERE age >= {30}").Where(u => u.CompanyId == 1).OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM (SELECT * FROM t_user WHERE age >= ?) t WHERE t.`company_id` = ? ORDER BY t.`id` ASC;
-- params: [30, 1]
```
<!-- /card -->

## 查询里的原生片段

| 写法 | 作用 |
|---|---|
| `Sql.Raw<T>("模板", 参数…)` | 表达式里的一段 SQL，`{0}`、`{1}` 是参数（列或值） |
| `[SqlFunction("模板")]` 标在静态方法上 | 自定义函数，用法和内置函数一样（见[函数](functions.md#原生片段与自定义函数)） |
| `OrderByRaw("views % 7")` / `OrderByRaw(x => Sql.Raw<object>("{0} DESC", x.Col))` | 原生排序项（自带方向） |
| `SelectRaw("id, title")` | 原生 SELECT 列表，按列名映射回实体 |
| `AsTableSegment((表, 别名) => …)` | 整个表引用自己写（表提示、表函数），见[换表](tables.md) |
| `db.Table("表名")` | 没有实体类的表，见[查询](queries.md#无实体的表) |

<!-- card: raw/RawOrderAndSelect sql=mysql -->
```csharp
db.Query<Post>().Where(p => p.Views > 0).OrderByRaw("views % 7").SelectRaw("id, title, views").Take(3).ToListAsync();
```

```sql
-- MySQL
SELECT id, title, views FROM `t_post` WHERE `views` > ? ORDER BY views % 7 LIMIT 3;
-- params: [0]
```
<!-- /card -->

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

## 自己读结果、只要 SQL

- `ReadAsync(async (reader, ct) => …)`：执行查询，拿到 `DbDataReader` 自己读（列按投影的顺序；带出、追踪都不适用）。
- `ToSql()` / `ToStatement()`：只生成 SQL 和参数，不执行；写入命令同样有。
- `IExecutionListener`：每条 SQL 执行前后的回调（日志、监控、测试里捕获 SQL），`SqlExecutionEvent` 带语句、参数、耗时、行数、异常；`ActivitySource("EasyQuerySharp")` 对接 OpenTelemetry。

<!-- card: raw/ReadAsync sql=mysql -->
```csharp
db.Query<Company>().OrderBy(c => c.Id).Select(c => c.Name).ReadAsync(async (reader, ct) =>
{
    var names = new List<string>();
    while (await reader.ReadAsync(ct)) names.Add(reader.GetString(0));
    return names;
});
```

```sql
-- MySQL
SELECT t.`name` FROM `t_company` t ORDER BY t.`id` ASC;
```
<!-- /card -->

<!-- card: raw/ToSql -->
```csharp
Task.FromResult(db.Query<User>().Where(u => u.Age >= 30).Select(u => u.Name).ToSql());
```
<!-- /card -->

## 限制

- 原生 SQL 不经过方言翻译：分页、引号、函数名要按目标库写；`Sql.Identifier` 只处理标识符的引号。
- 原生 SQL 不应用拦截器和逻辑删除：`FromSql<Doc>($"SELECT * FROM t_doc")` 不会加"未删除"条件，也不会加查询过滤的条件。
- `SqlQueryAsync<T>` 映射到类时只按列名对应，不带出导航。
- `…Raw` 方法的 SQL、`OrderByRaw` / `SelectRaw` / `Sql.Raw` 的模板原样拼进 SQL，不要把用户输入拼进去，值用参数传。

## 对应 Java

| Java | C# |
|---|---|
| `easyQuery.sqlQuery(sql, Class, params)` | `db.SqlQueryRawAsync<T>(sql, params)` 或插值字符串 `db.SqlQueryAsync<T>($"…")` |
| `easyQuery.sqlQueryMap(sql)` | `db.SqlQueryAsync<DynamicRow>(..)` 或 `db.Table(..)` |
| `easyQuery.sqlExecute(sql, params)` | `db.ExecuteSqlRawAsync(sql, params)` / `db.ExecuteSqlAsync($"…")` |
| `queryable("sql", Class)` | `db.FromSqlRaw<T>(sql, params)` / `db.FromSql<T>($"…")` |
| `sqlNativeSegment("…", c -> c.expression(..))` / `expression().sql(..)` | `Sql.Raw<T>("…", ..)` |
| `select(String)` | `SelectRaw("…")` |
| `orderBy(x -> x.expression().sql("{0} ASC", ..))` | `OrderByRaw(x => Sql.Raw<object>("{0} ASC", ..))` |
| `toResultSet(..)` | `ReadAsync(..)` |
| `toSQL()` | `ToSql()` |
| `JdbcExecutorListener` | `IExecutionListener` |
