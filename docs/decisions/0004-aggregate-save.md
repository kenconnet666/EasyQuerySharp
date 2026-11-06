# 0004 聚合根保存

状态：已定（2026-11），对应 ROADMAP 5.11。

## 背景

Java 的 `savable(root).executeCommand()` 按对象图与追踪快照的差异，一次写出整个聚合：新对象插入、改过的对象更新、从图里拿掉的子对象删除或断开，多对多维护中间表。这是 M5 里用例最多的一块（`M8SaveTest`、`M8Save2Test` … 约 40 个）。行为以 Java 的实现（`AutoTrackSaveProvider` + `BasicSaveCommand`）为准；Java 里另外两条路径（`executeCommand2`、`QueryCompareSaveProvider`）没写完，不移植。

## 决定

### API

```csharp
using var tracking = db.BeginTracking();          // 必须：差异来自追踪快照
await using var tx = db.BeginTransaction();        // 必须：一次保存是多条语句
var root = await db.Query<Order>().Include(o => o.Lines).WhereById(id).SingleAsync();
root.Lines!.RemoveAt(0);
root.Lines.Add(new OrderLine { Sku = "x" });
await db.Save(root).ExecuteAsync();                // 也可 db.Save(roots)
await tx.CommitAsync();
```

- `db.Save(entity)` / `db.Save(entities)` 返回 `SaveCommand<T>`；空集合什么也不做。
- 行为开关 `SaveBehavior`（`[Flags]`，对应 Java 的 `SaveBehaviorEnum`）：`IgnoreRoot`（根不增不改，只处理子对象）、`IgnoreRootUpdate`（根新增照常，已追踪的根不更新）、`AllowOwnershipChange`（允许把子对象挂到别的父对象）、`IgnoreNull`（已追踪父对象上为 null 的对一导航跳过，不当孤儿）、`IgnoreEmpty`（空集合跳过）、`PhysicalDelete`（孤儿物理删除，默认按逻辑删除）。用 `.With(SaveBehavior.X)` 叠加。
- `.SavePath(r => r.Lines, r => r.Lines.Select(l => l.Details))`：只级联列出的导航（同深度同名匹配，与 Java 一致）；列出非"值对象"导航时报错。
- `.RemoveRoot()`：删除根，连同它已追踪的子对象按级联规则处理。
- 不在事务里、不在追踪作用域里调用时抛异常。

### 级联声明

`[Navigate(…, Cascade = Cascade.Delete)]`，取值对应 Java 的 `CascadeTypeEnum`，默认 `Auto`：

| 值 | 对拥有的子对象（一对一、一对多、多对多的中间表） |
|---|---|
| `Delete` | 子对象是聚合的一部分：新增插入、改动更新、孤儿删除，并递归处理子对象自己的导航；多对多维护中间表行 |
| `Auto` / `SetNull` | 子对象是引用：只改外键挂上 / 断开（孤儿外键置 NULL），不插入、不递归；新挂上的子对象必须已追踪 |
| `NoAction` | 不处理 |

导航按"值对象 / 聚合根 / 其它"分类（Java 的 `getTargetValueType`）：自身列是主键的一对一、一对多与有中间表的多对多是值对象；目标列是主键的（多对一）是别的聚合根，**从不级联**，只把目标的主键回填到本对象的外键；其余忽略。目标类型等于根类型的导航在深度大于 0 时不再跟进（防环）。

### 差异计算

- 根：没追踪的插入；已追踪的按快照差异更新（无变化不发语句）。
- 只走"已加载"的导航：追踪查询里 `Include` 过的导航，追踪器记下当时的子对象主键集合（Java 的 `DatabaseEntityValues`）。已追踪的父对象上没 Include 过、却有值的导航报错"导航没有被追踪"（`SavePath` 时跳过这个检查）。未追踪的父对象（新对象）的导航全部当新增。
- 子对象按主键匹配原集合；主键为空时按 `[SaveKey]` 标的业务键匹配。匹配上的按快照差异更新；匹配不上的插入；原集合里有、现在没有的是孤儿。
- 归属变更：子对象外键已有值且不等于新父对象的键时，默认报错，`AllowOwnershipChange` 时改写外键。同一实例在一次保存里既从 A 拿掉又挂到 B，合并成一次外键 UPDATE。

### 语句顺序

1. 删除：从最深一层往上，每层先删子对象（逻辑删除优先），再处理中间表行（`DELETE` 或置 NULL）。
2. 根：`RemoveRoot` 时删根；否则插入（回填生成键）、更新。
3. 插入与更新：从浅到深，每层先插入（插入前才把父对象的键回填到外键，于是孙辈拿得到子对象的生成键），再外键置 NULL，再普通更新。

### 实现落点

- 追踪器记录"已加载的导航"：`IncludeRunner` 在追踪查询里把子对象附加到追踪器，并为父对象的追踪项记下导航名与原子对象主键。
- 保存逻辑在 `Commands/SaveCommand*.cs`：构建保存树（每层每个导航的插入 / 更新 / 删除 / 外键变更），再按上面的顺序复用现有的 `InsertCommand` / `UpdateCommand` / `DeleteCommand` 执行，拦截器、主键生成、乐观锁照常生效。

## 不做

- 不支持直达导航（`DirectMapping`）参与保存（与 Java 一致，报错）。
- 不做"查库对比"模式（Java 的 `QueryCompareSaveProvider` 未完成）。
- 异常信息用中文，说明哪条导航、哪个值、怎么改；断言 Java 异常原文的用例记为偏离。
