# 入门

EasyQuerySharp 是 [easy-query](https://github.com/dromara/easy-query)（Java）的 C# 实现：实体是普通的 C# 类，查询写成表达式树，导航属性直接写在条件和投影里，生成隐式 JOIN、隐式子查询和对象树。本页从安装到第一条查询、第一次写入走一遍，其余能力见[指南索引](../README.md#指南)。

本页和其余指南里的代码都来自 `docs/examples` 下的示例卡片：卡片在 SQLite 示例库上实际执行，各方言的 SQL 由 `tools/DocsExamples` 生成，不手写（见[示例卡片与 SQL 快照](../design/examples.md)）。示例模型是一个技术社区（公司、用户、帖子、评论、标签、订单……），定义在 `samples/EasyQuerySharp.Samples/SampleModel.cs`。

## 安装

- 需要 .NET 10。核心包只依赖 BCL，**不引用任何数据库驱动**：驱动由你的项目引用，通过 ADO.NET 的 `DbDataSource` 交给 EasyQuerySharp。
- 从 nuget.org 安装；`[Projection]` 生成器与分析器（EQ0001–EQ0007）随核心包分发：

```bash
dotnet add package EasyQuerySharp
# 可选：services.AddEasyQuery(..)，见依赖注入指南
dotnet add package EasyQuerySharp.DependencyInjection
# 可选：二级缓存，见二级缓存指南
dotnet add package EasyQuerySharp.Caching
```

## 选方言和驱动

每个方言一个 `UseXxx(数据源)`，数据源用驱动的 `DbProviderFactory.CreateDataSource(连接串)` 创建（驱动自己的 `NpgsqlDataSource`、`MySqlDataSource` 也可以）。完整的支持矩阵见[方言](dialects.md)。

| 数据库 | 配置 | 驱动（NuGet） | 档位 |
|---|---|---|---|
| SQLite | `UseSqlite` | Microsoft.Data.Sqlite：`SqliteFactory.Instance` | 第一档 |
| MySQL 8.0.18+ / 8.4、PolarDB、MariaDB | `UseMySql` | MySqlConnector：`MySqlConnectorFactory.Instance` | 第一档 |
| PostgreSQL 17 | `UsePostgreSql` | Npgsql：`NpgsqlFactory.Instance` | 第一档 |
| SQL Server | `UseSqlServer` | Microsoft.Data.SqlClient：`SqlClientFactory.Instance` | 第一档 |
| Oracle | `UseOracle` | Oracle.ManagedDataAccess.Core：`OracleClientFactory.Instance` | 第一档 |
| DuckDB | `UseDuckDb` | DuckDB.NET.Data.Full：`DuckDBClientFactory.Instance` | 第二档 |
| 达梦、HANA、Kingbase、GaussDB / openGauss、ClickHouse、Db2、TDengine | `UseDameng` / `UseHana` / `UseKingbase` / `UseGaussDb` / `UseClickHouse` / `UseDb2` / `UseTDengine` | 各库的 ADO.NET 驱动 | 第二档 |

## 定义实体

实体是 POCO：`[Table]` 指定表名，`[Column]` 指定主键、生成列等；不写时表名、列名按命名约定由类名、属性名推出（默认驼峰转下划线，`CompanyId` → `company_id`）。导航属性标 `[Navigate]`，写明关系类型和两端的键：

<!-- snippet: start-entities -->
```csharp
[Table("t_company")]
public class Company
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public string City { get; set; } = "";

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Employee.CompanyId)])]
    public List<Employee>? Employees { get; set; }
}

[Table("t_user")]
public class Employee
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public int? Age { get; set; }

    public long? CompanyId { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }
}
```
<!-- /snippet -->

这两个类只映射了示例库 `t_company`、`t_user` 的几列：实体不必覆盖表的全部列。特性的完整说明见[实体与映射](entities.md)。

## 创建 EasyDb

`EasyDb` 是入口，线程安全，整个应用一个（每个库一个）。配置在构造时传入，交给 `EasyDb` 之后就不能再改：

<!-- snippet: start-db -->
```csharp
var db = new EasyDb(o => o
    .UseSqlite(SqliteFactory.Instance.CreateDataSource("Data Source=app.db"))
    .AddListener(new ConsoleSqlLog()));
```
<!-- /snippet -->

`AddListener` 挂执行监听器，每条 SQL 执行前后各调用一次，可以用来记日志、接监控。上面用到的监听器：

<!-- snippet: start-listener -->
```csharp
/// <summary>把每条 SQL 和耗时打到控制台。</summary>
public sealed class ConsoleSqlLog : IExecutionListener
{
    public void OnExecuted(SqlExecutionEvent e) =>
        Console.WriteLine($"{e.Elapsed.TotalMilliseconds:0.0} ms, {e.Rows} 行：{e.Statement.Text}");
}
```
<!-- /snippet -->

用 ASP.NET Core 等依赖注入容器时，改用 `services.AddEasyQuery(..)`，见[依赖注入](dependency-injection.md)。

## 第一条查询

`db.Query<T>()` 开始一条查询。查询对象不可变，每次调用返回新对象，直到 `ToListAsync` 这类终结方法才执行。对一导航（`e.Company!.City`）生成 LEFT JOIN：

<!-- card: start/FirstQuery sql=mysql -->
```csharp
db.Query<Employee>()
    .Where(e => e.Age >= 30 && e.Company!.City == "杭州")
    .OrderBy(e => e.Name)
    .Select(e => new EmployeeRow(e.Name, e.Age, e.Company!.Name))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`age`,t1.`name` AS `company` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t.`age` >= ? AND t1.`city` = ? ORDER BY t.`name` ASC;
-- params: [30, '杭州']
```
<!-- /card -->

表达式树里不能写 `?.`，导航写成 `e.Company!.City`；对方不存在时取到的就是 NULL，与 SQL 的 LEFT JOIN 一致。

集合导航上的 `Any`、`Count` 生成子查询，投影里的 `Select(..).ToList()` 按父键批量查一次（不会 N+1）：

<!-- card: start/CollectionNavigation sql=mysql -->
```csharp
db.Query<Company>()
    .Where(c => c.Employees!.Any(e => e.Age < 30))
    .OrderBy(c => c.Id)
    .Select(c => new CompanyCard(c.Name, c.Employees!.Count(), c.Employees!.OrderBy(e => e.Id).Select(e => e.Name).ToList()))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,(SELECT COUNT(*) FROM `t_user` t2 WHERE t2.`company_id` = t.`id`) AS `headcount`,t.`id` AS `__relation__id` FROM `t_company` t WHERE EXISTS(SELECT 1 FROM `t_user` t1 WHERE t1.`company_id` = t.`id` AND t1.`age` < ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: [30]
SELECT t.`name`,t.`company_id` AS `__relation__companyId` FROM `t_user` t WHERE t.`company_id` IN (?,?,?) ORDER BY t.`id` ASC;
-- params: [1, 2, 3]
```
<!-- /card -->

分页返回 `Page<T>`，带总数：

<!-- card: start/FirstPage sql=mysql,mssql -->
```csharp
db.Query<Employee>().OrderBy(e => e.Id).Select(e => e.Name).ToPageAsync(1, 3);
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_user`;
SELECT t.`name` FROM `t_user` t ORDER BY t.`id` ASC LIMIT 3;
```

```sql
-- SQL Server
SELECT COUNT(*) FROM [t_user];
SELECT TOP 3 t.[name] FROM [t_user] t ORDER BY t.[id] ASC;
```
<!-- /card -->

## 第一次写入

写入一律以 `ExecuteAsync` 结束，返回受影响的行数。按条件更新、删除必须写条件（全表要写 `.All()`）：

<!-- card: start/Writes sql=mysql -->
```csharp
var company = new Company { Name = "东海数据", City = "宁波" };
await db.Insert(company).ExecuteAsync();
await db.Update<Company>().Set(c => c.City, "舟山").Where(c => c.Name == "东海数据").ExecuteAsync();
return await db.Delete<Company>().Where(c => c.Name == "东海数据").ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_company` (`name`,`city`) VALUES (?,?);
-- params: ['东海数据', '宁波']
UPDATE `t_company` SET `city` = ? WHERE `name` = ?;
-- params: ['舟山', '东海数据']
DELETE FROM `t_company` WHERE `name` = ?;
-- params: ['东海数据']
```
<!-- /card -->

## 接下来

| 想做的事 | 看哪一页 |
|---|---|
| 表名、列名、类型转换、值对象、计算列 | [实体与映射](entities.md) |
| 逻辑删除、乐观锁、多租户、加密 | [列规则](rules.md) |
| 条件、排序、分页、投影、DTO | [查询](queries.md) |
| JOIN、导航、子查询 | [联表与导航](joins-and-navigations.md) |
| 带出关联数据、对象树、树形数据 | [带出与对象树](include-and-trees.md) |
| 字符串、日期、数值、JSON、窗口函数 | [函数](functions.md) |
| GROUP BY、HAVING、聚合 | [分组与聚合](grouping.md) |
| 插入、更新、删除、upsert、批量 | [写入](writes.md) |
| 改完整个聚合再保存 | [追踪与聚合保存](save-and-tracking.md) |
| 事务、行锁 | [事务与行锁](transactions.md) |
| 手写 SQL | [原生 SQL](raw-sql.md) |
| 按年份分表、换 schema | [换表](tables.md) |
| 后台列表页的筛选和排序 | [动态筛选与列表页](filter.md) |
| 各数据库支持什么 | [方言](dialects.md) |
| 从 Java 版或 TS 版过来 | [迁移指南](migration.md) |

## 限制

- 只有异步 API（`…Async`），没有同步版本。
- 支持裁剪与 Native AOT（读取结果在 AOT 下解释执行，比 JIT 慢），见[裁剪与 Native AOT](native-aot.md)。
- 查询不实现 `IQueryable<T>`：方法是 EasyQuerySharp 自己的（`WhereIf`、`ToPageAsync`、带配置的 `Include` 等），但 `from … where … select` 查询语法照样能用。
- 写不进表达式树的语法（`?.`、`switch` 表达式、元组字面量、集合表达式 `[..]`）有对应写法，见 [0001](../decisions/0001-query-api-route.md#表达式树里不能写的语法)；分析器会在编译期提示。

## 对应 Java

| Java | C# |
|---|---|
| `EasyQueryClient` / `EasyEntityQuery`，`EasyQueryBootstrapper.defaultBuilderConfiguration()` | `new EasyDb(o => o.UseXxx(dataSource))` |
| `easyEntityQuery.queryable(User.class)` | `db.Query<User>()` |
| APT 生成的 `UserProxy`、`@EntityProxy` | 不需要：lambda 直接写在实体上 |
| `JdbcExecutorListener` | `IExecutionListener` |
| `toList()` / `toPageResult(page, size)` | `ToListAsync()` / `ToPageAsync(page, size)` |
| `insertable` / `updatable` / `deletable` + `executeRows()` | `db.Insert` / `db.Update` / `db.Delete` + `ExecuteAsync()` |
