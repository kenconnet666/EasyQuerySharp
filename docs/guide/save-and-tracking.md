# 追踪与聚合保存

两个相关的能力：

- **追踪**：在追踪作用域里查出的实体记下快照，之后 `db.Update(实体)` 只写改过的列。
- **聚合保存**：`db.Save(根)` 按追踪快照的差异写出整个聚合——新对象插入、改过的更新、从集合里拿掉的删除或断开、多对多维护中间表。设计见 [0004](../decisions/0004-aggregate-save.md)。

读数据给前端看用投影（[查询](queries.md#投影)、[带出与对象树](include-and-trees.md)）；查出来要修改再存回去时才用追踪和保存。

## 追踪

`using var tracking = db.BeginTracking();` 开启追踪作用域，挂在 AsyncLocal 上，跟随当前异步流程（每个请求互不影响）。作用域里：

- 查出的实体自动被追踪，`AsNoTracking()` 的查询除外（`AsTracking()` 对应 Java 的 asTracking，作用域里默认就是追踪）。作用域外的查询不追踪。
- `db.Update(实体)` 只写快照之后改过的列，没有改动就不发语句。
- `db.GetTrackedChanges(实体)` 列出改过的属性；`db.AddTracking(实体)` / `db.RemoveTracking(实体)` 手动加入、移出；`db.IsTracking` 判断是否在作用域里。

<!-- card: save/TrackedUpdate sql=mysql -->
```csharp
using var tracking = db.BeginTracking();
var product = await db.Query<Product>().FindOrThrowAsync(5L);
product.Stock -= 1;
return await db.Update(product).ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`name`,`price`,`stock` FROM `t_product` WHERE `id` = ?;
-- params: [5]
UPDATE `t_product` SET `stock` = ? WHERE `id` = ?;
-- params: [7, 5]
```
<!-- /card -->

<!-- card: save/TrackedChanges sql=mysql -->
```csharp
using var tracking = db.BeginTracking();
var user = await db.Query<User>().FindOrThrowAsync(1L);
user.Age = 30;
user.Level = UserLevel.Lead;
return db.GetTrackedChanges(user);
```

```sql
-- MySQL
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `id` = ?;
-- params: [1]
```
<!-- /card -->

<!-- card: save/NoTracking sql=mysql -->
```csharp
using var tracking = db.BeginTracking();
var user = await db.Query<User>().AsNoTracking().FindOrThrowAsync(1L);
return db.RemoveTracking(user);
```

```sql
-- MySQL
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `id` = ?;
-- params: [1]
```
<!-- /card -->

不追踪时也能只写差异：`db.Update(修改后).Changes(修改前)`，见[写入](writes.md#按实体更新)。

## 聚合保存

聚合的边界写在导航的 `Cascade` 上：

<!-- snippet: save-model -->
```csharp
[Table("t_order")]
public class SalesOrder
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public long UserId { get; set; }

    [Column(Converter = typeof(EnumNameConverter<OrderStatus>))]
    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    // 明细是订单的一部分：新增插入、改动更新、从集合里拿掉的删除
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SalesOrderLine.OrderId)], Cascade = Cascade.Delete)]
    public List<SalesOrderLine>? Lines { get; set; }
}

[Table("t_order_item")]
public class SalesOrderLine
{
    [Column(PrimaryKey = true)]
    public long OrderId { get; set; }

    [Column(PrimaryKey = true)]
    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}
```
<!-- /snippet -->

| `Cascade` | 对拥有的子对象（一对一、一对多、多对多的中间表） |
|---|---|
| `Delete` | 子对象是聚合的一部分：新增插入、改动更新、从集合里拿掉的删除，并递归处理子对象自己的导航；多对多维护中间表行 |
| `Auto`（默认）/ `SetNull` | 子对象是引用：只改外键挂上 / 断开（拿掉的外键置 NULL），不插入、不递归；新挂上的子对象必须已被追踪 |
| `NoAction` | 不处理 |

多对一导航（目标是别的聚合根）从不级联，只把目标的主键回填到本对象的外键。

保存必须在追踪作用域与事务里（一次保存是多条语句）。只走查询时 `Include` 过的导航：

<!-- card: save/SaveAggregate sql=mysql -->
```csharp
using var tracking = db.BeginTracking();
await using var tx = db.BeginTransaction();
var order = await db.Query<SalesOrder>().Include(o => o.Lines, q => q.OrderBy(l => l.ProductId)).WhereById(1L).SingleAsync();
order.Lines![0].Quantity = 2;                                                  // 年度会员改成 2 份
order.Lines.RemoveAt(1);                                                        // 不要贴纸礼包了
order.Lines.Add(new SalesOrderLine { OrderId = order.Id, ProductId = 3, Quantity = 1, Price = 199.00m }); // 加一门课
await db.Save(order).ExecuteAsync();
await tx.CommitAsync();
return await db.Query<OrderItem>().Where(i => i.OrderId == 1).OrderBy(i => i.ProductId)
    .Select(i => i.Product!.Name + " × " + i.Quantity).ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`user_id`,`status`,`created_at` FROM `t_order` WHERE `id` = ?;
-- params: [1]
SELECT t.`order_id`,t.`product_id`,t.`quantity`,t.`price` FROM `t_order_item` t WHERE t.`order_id` IN (?) ORDER BY t.`product_id` ASC;
-- params: [1]
DELETE FROM `t_order_item` WHERE `order_id` = ? AND `product_id` = ?;
-- params: [1, 6]
INSERT INTO `t_order_item` (`order_id`,`product_id`,`quantity`,`price`) VALUES (?,?,?,?);
-- params: [1, 3, 1, 199.00]
UPDATE `t_order_item` SET `quantity` = ? WHERE `order_id` = ? AND `product_id` = ?;
-- params: [2, 1, 1]
SELECT CONCAT(t1.`name`,?,t.`quantity`) FROM `t_order_item` t LEFT JOIN `t_product` t1 ON t1.`id` = t.`product_id` WHERE t.`order_id` = ? ORDER BY t.`product_id` ASC;
-- params: [' × ', 1]
```
<!-- /card -->

语句顺序：先删除（从最深一层往上），再根，再从浅到深插入和更新；插入前才把父对象的键回填到子对象的外键，孙辈拿得到子对象的生成键。

删除根：`.RemoveRoot()`，它拥有的子对象按级联规则一起处理：

<!-- card: save/RemoveRoot sql=mysql -->
```csharp
using var tracking = db.BeginTracking();
await using var tx = db.BeginTransaction();
var order = await db.Query<SalesOrder>().Include(o => o.Lines).WhereById(5L).SingleAsync();
await db.Save(order).RemoveRoot().ExecuteAsync();
await tx.CommitAsync();
return await db.Query<OrderItem>().Where(i => i.OrderId == 5).CountAsync();
```

```sql
-- MySQL
SELECT `id`,`user_id`,`status`,`created_at` FROM `t_order` WHERE `id` = ?;
-- params: [5]
SELECT t.`order_id`,t.`product_id`,t.`quantity`,t.`price` FROM `t_order_item` t WHERE t.`order_id` IN (?);
-- params: [5]
DELETE FROM `t_order_item` WHERE `order_id` = ? AND `product_id` = ?;
-- params: [5, 2]
DELETE FROM `t_order` WHERE `id` = ?;
-- params: [5]
SELECT COUNT(*) FROM `t_order_item` WHERE `order_id` = ?;
-- params: [5]
```
<!-- /card -->

### 行为开关

`.With(SaveBehavior.X)` 叠加：

| `SaveBehavior` | 作用 |
|---|---|
| `IgnoreRoot` | 根不增不改，只处理子对象 |
| `IgnoreRootUpdate` | 新根照常插入，已追踪的根不更新 |
| `AllowOwnershipChange` | 允许把子对象挂到别的父对象（改写外键）；默认报错 |
| `IgnoreNull` | 已追踪父对象上为 null 的对一导航跳过，不当孤儿 |
| `IgnoreEmpty` | 空集合跳过，不当成"全部拿掉" |
| `PhysicalDelete` | 孤儿物理删除（默认有逻辑删除的按逻辑删除） |

- `.SavePath(r => r.Lines, r => r.Lines.Select(l => l.Details))`：只级联列出的导航。
- 子对象按主键匹配原集合；主键为空时按 `[SaveKey]` 标的业务键匹配。
- 新建的对象带着某个已追踪实体的主键时，当作那个实体：合并到已追踪的实例上，按差异更新。

## 限制

- 必须在 `BeginTracking()` 作用域和事务里，否则抛异常。
- 只保存追踪查询里 `Include` 过的导航；已追踪的父对象上没 Include 过、却有值的导航报错（`SavePath` 时不检查）。
- 给已有的父对象新增子对象时，子对象的外键要么留空（可空类型）、要么先设成父对象的键：非空外键的默认值 0 会被当成"挂在别的父对象上"而报错（要求 `AllowOwnershipChange`）。
- 直达导航（`DirectMapping`）不参与保存。
- 不做"查库对比"模式（Java 的 `QueryCompareSaveProvider` 没有完成）。
- 一个子对象只能属于一个父对象：同一实例既留在原父对象上又挂到别处时报错。

## 对应 Java

| Java | C# |
|---|---|
| `TrackManager.begin()` / `release()` | `using var tracking = db.BeginTracking();` |
| `asTracking()` / `asNoTracking()` | `AsTracking()` / `AsNoTracking()` |
| `addTracking(e)` / `removeTracking(e)` / `getTrackEntityStateNotNull(e)` | `db.AddTracking(e)` / `db.RemoveTracking(e)` / `db.GetTrackedChanges(e)` |
| 追踪模式下 `updatable(e)` 只写差异 | 同样：作用域里 `db.Update(e)` |
| `savable(root).executeCommand()` | `db.Save(root).ExecuteAsync()` |
| `@Navigate(cascade = CascadeTypeEnum.DELETE / SET_NULL / NO_ACTION / AUTO)` | `[Navigate(.., Cascade = Cascade.Delete / SetNull / NoAction / Auto)]` |
| `SaveBehaviorEnum` | `SaveBehavior`，`.With(..)` |
| `savePath(..)` / `removeRoot()` | `.SavePath(..)` / `.RemoveRoot()` |
| `@SaveKey` | `[SaveKey]` |
