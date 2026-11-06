# 0005 关联键的匹配方式

状态：已定（2026-11），对应 ROADMAP 1.1。

## 背景

Java 的 `@Navigate(relationPropertyStrategy = "名字")` 让导航两端的键用别的方式匹配：按名字从 `QueryConfiguration` 取一个 `EntityRelationPropertyProvider`，它负责隐式子查询的关联条件、带出时取子数据的条件和内存里的父子配对。Java 本身只带一个默认实现（键相等）；用例 `M8Test1.testRelationColumns` / `testRelationColumns1` 用的 `find_in_set` 是测试工程自己写的扩展：用户表的 `book_ids` 存逗号分隔的书 id。

TS 版没有这个概念，换成任意关联条件 `r.manyOn('Book', (reader, book) => book.id.inCsv(reader.bookIds))`，查询时把本表自连接成派生表再按主键对上。

## 决定

### API

```csharp
[Navigate(RelationType.ManyToMany, SelfProperties = [nameof(BookIds)], TargetProperties = [nameof(Book.Id)],
          KeyMatch = KeyMatch.SelfCsv)]
public List<Book>? Books { get; set; }
```

- `KeyMatch` 是封闭的枚举：`Equal`（默认）、`SelfCsv`（本表一列存逗号分隔的目标键）。不提供用户自定义策略的接口：Java 的两个用例只需要这一种，单实现的接口按项目约定不加。
- `SelfCsv` 只能用在没有中间表的多对多导航上，本表一个 `string` 列、目标一列；其他组合在解析元数据时报错。写成多对多而不是一对多，聚合保存自然把它归为"不处理"，不会把目标主键回填进列表列。
- 匹配语义与 MySQL 的 `FIND_IN_SET` 一致：不去空格，NULL 或空串不匹配任何行。
- 同时公开 `Sql.InCsv(value, csv)`，条件里可以直接用；导航上的 `SelfCsv` 就翻译成它。

### 各方言的写法

| 方言 | `Sql.InCsv(值, 列表)` |
|---|---|
| MySQL | `FIND_IN_SET(值,列表)`（与 Java 逐字一致） |
| PostgreSQL（基类默认） | `((值)::text = ANY(string_to_array(列表,',')))` |
| SQLite | `(instr(',' \|\| 列表 \|\| ',', ',' \|\| 值 \|\| ',') > 0)` |
| SQL Server | `(CHARINDEX(',' + CAST(值 AS NVARCHAR(MAX)) + ',', ',' + 列表 + ',') > 0)` |
| Oracle | `(INSTR(',' \|\| 列表 \|\| ',', ',' \|\| 值 \|\| ',') > 0)` |

SQLite 没按 ROADMAP 原先写的"拼逗号后用 LIKE"：SQLite 的 LIKE 对 ASCII 不区分大小写，键里的 `%`、`_` 还会被当成通配符，`instr` 是精确匹配。SQL Server 用 `+` 拼接，列表为 NULL 时整体为 NULL，不会误匹配。

### 三种场景

- **条件、隐式子查询**（any / count / sum / string.Join …）：集合子查询的关联条件写成 `InCsv(目标键, 本表列)`。
- **带出、DTO 自动带出**：父键就是列表字符串；带出时在内存里按逗号拆成（父, 目标键）对，子数据用一条 `InCsv(目标键, ?)` 取，参数是全部目标键去重后用逗号连起来（与 Java 一致：`FIND_IN_SET(t.book_id,?)`、`1,2,3`）。拆出来的项转不成目标键类型时不匹配。
- **SelectMany**：连接条件写成 `InCsv(目标键, 本表列)`。

### 限制

- **分组合并**（`GroupJoin` / `GroupJoinAll`）：一行目标属于多个父行，没法按目标的键分组，照常写成子查询。Java 的扩展没实现分组接口，行为相同。
- **取元素**（`First()` / `ElementAt(n)`）、**带出时按父对象取段**（`Take` / `Skip` / 导航的 `Limit`）：都要按父行分区，逗号列表的键做不到，报不支持。
- **作为带出路径的中间一段**、**树查询的父子导航**：不支持。
- 反方向（目标表的一列存本表键的列表）不做，有需要时给枚举加值。

## 不做

- 任意关联条件（TS 的 `manyOn`）：需要本表自连接或在内存里求逆，是另一套实现，记在 BACKLOG。
- 带出时按方言换成更快的写法（MySQL 的 `FIND_IN_SET` 用不上索引，目标表大时是全表扫描）：先保持与 Java 一致，记在 BACKLOG。
