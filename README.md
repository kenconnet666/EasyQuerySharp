# EasyQuerySharp

[easy-query](https://github.com/dromara/easy-query)（Java）的 C# 实现：POCO 实体加表达式树，导航属性直接写在条件和投影里，生成隐式 JOIN、隐式子查询和对象树；带出、DTO、树、聚合保存走同一条管线；13 个数据库方言。以 Java 版的 2404 个用例为对标基准，逐条移植、SQL 逐字比对，进度见[能力对照总表](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/parity.md)与 [ROADMAP](https://github.com/kenconnet666/EasyQuerySharp/blob/master/ROADMAP.md)。

<!-- card: start/Readme sql=mysql -->
```csharp
db.Query<User>()
    .Where(u => u.Company!.City == "杭州")                       // 对一导航：隐式 LEFT JOIN
    .Where(u => u.Posts!.Any(p => p.Views >= 1000))              // 集合导航：EXISTS 子查询
    .OrderBy(u => u.Name)
    .Select(u => new UserCard(
        u.Name,
        u.Company!.Name,
        u.Posts!.Count(),                                         // 标量子查询
        u.Posts!.OrderByDescending(p => p.Views).Select(p => p.Title).ToList()))   // 对象树：按父键一次批量查询
    .ToPageAsync(1, 10);
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t1.`city` = ? AND EXISTS(SELECT 1 FROM `t_post` t2 WHERE t2.`author_id` = t.`id` AND t2.`views` >= ? LIMIT 1);
-- params: ['杭州', 1000]
SELECT t.`name`,t1.`name` AS `company`,(SELECT COUNT(*) FROM `t_post` t3 WHERE t3.`author_id` = t.`id`) AS `posts`,t.`id` AS `__relation__id` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t1.`city` = ? AND EXISTS(SELECT 1 FROM `t_post` t2 WHERE t2.`author_id` = t.`id` AND t2.`views` >= ? LIMIT 1) ORDER BY t.`name` ASC LIMIT 3;
-- params: ['杭州', 1000]
SELECT t.`title`,t.`author_id` AS `__relation__authorId` FROM `t_post` t WHERE t.`author_id` IN (?,?,?) ORDER BY t.`views` DESC;
-- params: [2, 1, 7]
```
<!-- /card -->

上面的代码和 SQL 由 `tools/DocsExamples` 在示例库上实跑生成，不是手写的。

## 安装

```bash
dotnet add package EasyQuerySharp
```

- 需要 .NET 10。核心包只依赖 BCL，不引用数据库驱动：驱动由你的项目引用，通过 ADO.NET 的 `DbDataSource` 传入。
- `[Projection]` 生成器与分析器随核心包分发，不用另装。
- 支持裁剪与 Native AOT（`PublishAot`），见[裁剪与 Native AOT](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/guide/native-aot.md)。
- 可选：`EasyQuerySharp.DependencyInjection`（`services.AddEasyQuery(..)`）、`EasyQuerySharp.Caching`（二级缓存）。详见[入门](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/guide/getting-started.md#安装)。

<!-- snippet: start-db -->
```csharp
var db = new EasyDb(o => o
    .UseSqlite(SqliteFactory.Instance.CreateDataSource("Data Source=app.db"))
    .AddListener(new ConsoleSqlLog()));
```
<!-- /snippet -->

换数据库就是换 `UseXxx` 和驱动的数据源（如 `UsePostgreSql(NpgsqlFactory.Instance.CreateDataSource(..))`），查询写法不变。

| 档位 | 方言 |
|---|---|
| 第一档（CI 跑全部实库用例） | SQLite、MySQL 8.0.18+ / 8.4、PolarDB、MariaDB、PostgreSQL、SQL Server、Oracle |
| 第二档（比对 SQL 文本加实库冒烟） | DuckDB、达梦、SAP HANA、人大金仓、GaussDB / openGauss、ClickHouse、Db2、TDengine |

各方言的能力与验证情况见[方言](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/guide/dialects.md)。

## 文档

- [入门](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/guide/getting-started.md)，以及按能力写的[指南](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/README.md#指南)：实体、查询、导航、对象树、函数、写入、聚合保存、事务、原生 SQL 等。
- [从 Java / TS 迁移](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/guide/migration.md)：API 对照表。
- 设计：[架构](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/design/architecture.md)、[决策记录](https://github.com/kenconnet666/EasyQuerySharp/blob/master/docs/README.md#设计决定)；进度：[ROADMAP](https://github.com/kenconnet666/EasyQuerySharp/blob/master/ROADMAP.md)。

## 开发

- 需要 .NET 10 SDK，以及 WSL 里的 Docker（实库测试用）。
- 本地数据库：`bash infra/dev/db.sh up -d`，只启动 PostgreSQL 17（SQLite、DuckDB 在进程内）；MySQL 等其余 13 种数据库由 CI 测，要在本机复现时按 profile 启动。
- 构建和测试：`dotnet build`、`dotnet test`；文档卡片与 SQL 快照：`dotnet run --project tools/DocsExamples`（`-- --check` 只校验）。
- 工作约定见 [CLAUDE.md](https://github.com/kenconnet666/EasyQuerySharp/blob/master/CLAUDE.md)。

## 许可证

[Apache-2.0](https://github.com/kenconnet666/EasyQuerySharp/blob/master/LICENSE)，与 [easy-query](https://github.com/dromara/easy-query) 相同；测试用例移植自 easy-query，见 [NOTICE](https://github.com/kenconnet666/EasyQuerySharp/blob/master/NOTICE)。
