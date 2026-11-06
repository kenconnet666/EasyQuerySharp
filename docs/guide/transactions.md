# 事务与行锁

## 环境事务

`db.BeginTransaction()` 开启的事务挂在 AsyncLocal 上：同一异步流程里，这个 `EasyDb` 发出的语句（查询、写入、原生 SQL、带出查询）自动加入，不用把事务对象传来传去。`await using` 结束时没有提交就回滚：

<!-- card: transaction/BeginTransaction sql=mysql -->
```csharp
await using (var tx = db.BeginTransaction())
{
    await db.Update<Product>().Set(p => p.Stock, p => p.Stock - 1).Where(p => p.Id == 3).ExecuteAsync();
    await db.Insert(new Order { UserId = 2, Status = OrderStatus.Pending, CreatedAt = new DateTime(2025, 9, 1) }).ExecuteAsync();
    await tx.CommitAsync();
}
return (await db.Query<Product>().FindAsync(3L))?.Stock;
```

```sql
-- MySQL
UPDATE `t_product` SET `stock` = (`stock` - ?) WHERE `id` = ?;
-- params: [1, 3]
INSERT INTO `t_order` (`user_id`,`status`,`created_at`) VALUES (?,?,?);
-- params: [2, 'pending', '2025-09-01 00:00:00']
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [3]
```
<!-- /card -->

<!-- card: transaction/DisposeRollsBack sql=mysql -->
```csharp
await using (var tx = db.BeginTransaction(IsolationLevel.Serializable))
{
    await db.Update<Product>().Set(p => p.Stock, 0).Where(p => p.Id == 3).ExecuteAsync();
}
return (await db.Query<Product>().FindAsync(3L))?.Stock;
```

```sql
-- MySQL
UPDATE `t_product` SET `stock` = ? WHERE `id` = ?;
-- params: [0, 3]
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [3]
```
<!-- /card -->

回调式：`db.TransactionAsync(async () => { … })`，回调正常结束就提交，抛异常就回滚并原样抛出：

<!-- card: write/Transaction sql=mysql -->
```csharp
try
{
    await db.TransactionAsync(async () =>
    {
        await db.Update<Product>().Set(p => new Product { Stock = p.Stock - 1 }).Where(p => p.Id == 5).ExecuteAsync();
        throw new InvalidOperationException("库存不足，取消");
    });
}
catch (InvalidOperationException)
{
}
return (await db.Query<Product>().FindAsync(5L))?.Stock;
```

```sql
-- MySQL
UPDATE `t_product` SET `stock` = (`stock` - ?) WHERE `id` = ?;
-- params: [1, 5]
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [5]
```
<!-- /card -->

| API | 说明 |
|---|---|
| `db.BeginTransaction(IsolationLevel.X)` | 返回 `EasyTransaction`：`CommitAsync()` / `RollbackAsync()` / `IsCompleted`，`Dispose` 时未完成就回滚 |
| `db.TransactionAsync(work, isolation, ct)` | 回调式 |
| 隔离级别 | ADO.NET 的 `System.Data.IsolationLevel`，不写时用驱动的默认值 |

**不能嵌套**：已经在事务里时再开事务直接抛 `InvalidOperationException`（与 Java 一致）。事务边界放在最外层（服务方法、请求处理），内层代码只发语句、不开事务：

<!-- card: transaction/Nested sql=mysql -->
```csharp
await using var tx = db.BeginTransaction();
try
{
    await using var inner = db.BeginTransaction();
    return "开启了嵌套事务";
}
catch (InvalidOperationException ex)
{
    return ex.Message;
}
```

```sql
-- MySQL
```
<!-- /card -->

- 每个 `EasyDb`（每个库）各有一个环境事务，多个库之间不是分布式事务。
- 批量写入（`.Batch()`）、聚合保存不在事务里时：批量每组自己开一个事务，聚合保存直接报错（要求外面开事务）。
- `EasyDb` 是单例，事务跟随异步流程，不同请求互不影响。

## 行锁

在事务里对单表查询加 `ForUpdate()`，锁住查到的行直到事务结束：

<!-- snippet: transaction-lock -->
```csharp
await using var tx = db.BeginTransaction();
var product = await db.Query<Product>().Where(p => p.Id == 5).ForUpdate().SingleAsync();
// 其它事务读这一行的 FOR UPDATE 会等到这里提交；不想等用 ForUpdateNoWait()，跳过被锁的行用 ForUpdateSkipLocked()
var updated = await db.Update<Product>().Set(p => p.Stock, product.Stock - 1).Where(p => p.Id == 5).ExecuteAsync();
await tx.CommitAsync();
```
<!-- /snippet -->

| 方言 | `ForUpdate()` | `ForUpdateNoWait()` | `ForUpdateSkipLocked()` |
|---|---|---|---|
| MySQL 8 / MariaDB、PostgreSQL 家族、Oracle、达梦 | `FOR UPDATE` | `FOR UPDATE NOWAIT` | `FOR UPDATE SKIP LOCKED` |
| Db2 | `FOR UPDATE WITH RS` | 不支持（用 `SET CURRENT LOCK TIMEOUT`） | `FOR UPDATE SKIP LOCKED DATA` |
| SAP HANA | `FOR UPDATE` | `FOR UPDATE NOWAIT` | `FOR UPDATE IGNORE LOCKED` |
| SQL Server | 不支持：改用表提示 `AsTableSegment((t, a) => $"{t} {a} WITH (UPDLOCK, ROWLOCK)")` | 不支持 | 不支持 |
| SQLite、DuckDB、ClickHouse、TDengine | 不支持（没有行锁） | 不支持 | 不支持 |

不支持时渲染阶段抛 `NotSupportedException`，消息说明替代写法。

## 限制

- 不支持嵌套事务和保存点（savepoint）。"有就加入、没有就新开"用 `db.InTransaction` 判断当前异步流程是否已在这个库的事务里。
- 多个库（多个 `EasyDb`）之间没有分布式事务，一致性要由应用处理。
- `FOR SHARE` 不做（Java 也没有）。
- 行锁只能对单表查询调用一次；在事务外调用报错。

## 对应 Java

| Java | C# |
|---|---|
| `try (Transaction tx = easyQuery.beginTransaction()) { …; tx.commit(); }` | `await using var tx = db.BeginTransaction(); …; await tx.CommitAsync();` |
| `beginTransaction(isolationLevel)` | `BeginTransaction(IsolationLevel.X)` |
| Spring `@Transactional` | 回调式 `db.TransactionAsync(..)`；或在服务方法里显式开启 |
| `forUpdate()` / fork 的 NoWait、SkipLocked | `ForUpdate()` / `ForUpdateNoWait()` / `ForUpdateSkipLocked()` |
| `asTableSegment((t, a) -> t + " " + a + " WITH (UPDLOCK)")` | `AsTableSegment((t, a) => …)` |
