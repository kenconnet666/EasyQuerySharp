# 架构

本文说明各模块怎么分、一条查询从调用到出结果经过哪些层，以及用到哪些 C# 特性。为什么走表达式树见 [0001](../decisions/0001-query-api-route.md)，对象树见 [0002](../decisions/0002-object-tree-and-dto.md)，测试见 [0003](../decisions/0003-testing-and-parity.md)，文档站见 [docs-site.md](docs-site.md)。

## 目标与约束

- **能力完整对标** easy-query：上游 `sql-test` 2315 个用例加 fork 的 `sql-fork-test` 89 个，逐条有对应。API 可以和 Java 不一样，但要更顺手。
- **目标框架**：`net10.0`（LTS），不做多目标。生成器和分析器按 Roslyn 的要求用 `netstandard2.0`。
- **核心零第三方依赖**：只依赖 BCL 和 `Microsoft.Extensions.*.Abstractions`。驱动由各方言包引入。
- **异步优先**：所有终结方法都是 `…Async(CancellationToken)`。同步版本第一期不做，记进 BACKLOG。
- **AOT 和裁剪友好**：元数据和物化器由生成器产出，运行时不反射实体。

## 目录与模块

```
EasyQuerySharp.slnx
src/
  EasyQuerySharp/                    核心：元数据、查询模型、翻译、SQL AST、方言基类、执行、拦截器
  EasyQuerySharp.Generator/          Source Generator + Analyzer（netstandard2.0，随核心包分发）
  EasyQuerySharp.Sqlite/             Microsoft.Data.Sqlite
  EasyQuerySharp.MySql/              MySqlConnector（MySQL 8.0.18 / 8.4、PolarDB、TiDB 兼容）
  EasyQuerySharp.PostgreSql/         Npgsql（PG 17；openGauss、Kingbase 的 PG 模式共用）
  EasyQuerySharp.SqlServer/          Microsoft.Data.SqlClient（2022；2008 用 ROW_NUMBER 分页）
  EasyQuerySharp.Oracle/             Oracle.ManagedDataAccess.Core
  EasyQuerySharp.Dameng/             DM.DmProvider（方言继承 Oracle 后覆盖差异）
  EasyQuerySharp.Hana/               SAP HANA ADO.NET 驱动
  EasyQuerySharp.DuckDb/ .ClickHouse/ .Db2/ .TDengine/   按需
  EasyQuerySharp.Encryption/         列加密（AES / SM4，可分段搜索，与 Java 版的密文兼容）
  EasyQuerySharp.Caching/            二级缓存接口（本机 + 分布式两级）
  EasyQuerySharp.DependencyInjection/ services.AddEasyQuery(...)
tests/
  EasyQuerySharp.Tests/              L1：SQL 文本断言（不连库）+ SQLite 实跑 + 文档卡片
  EasyQuerySharp.Tests.Upstream/     L2：逐条移植的 Java 用例（MySQL 等实库）
  EasyQuerySharp.Tests.Live/         L3：跨方言共用实库套件（按环境变量启用）
  EasyQuerySharp.Tests.Generator/    L4：生成器和分析器的快照测试
samples/
  EasyQuerySharp.Samples/            示例模型（与 TS 文档站同样的 17 张表和数据）
tools/
  ParityTests/                       扫描 Java 用例，生成 docs/parity-tests.md 和测试骨架
  DocsExamples/                      执行文档卡片，渲染各方言 SQL，输出快照
docs/
  decisions/  design/  examples/  sql-snapshots/
apps/
  docs/                              文档站（静态站点）
```

模块边界的规则：

- 方言包只做两件事：一是 `Dialect` 子类（渲染 SQL 和函数表），二是驱动适配。业务逻辑不能写进方言包。
- 核心包不引用任何驱动。

## 一条查询的管线

```
db.Query<User>().Where(..).Select(..).ToListAsync()
   │
   ├─ 1. 查询模型   不可变的 QueryModel：每次链式调用返回新对象，只记录 lambda，不翻译
   ├─ 2. 归一       Inliner：展开 [Projection] 方法、Project(map)、MapFrom DTO、Include
   ├─ 3. 缓存       按表达式结构算哈希（常量算作参数）→ 命中则跳到第 6 步
   ├─ 4. 翻译       Translator：表达式 → SQL AST
   │                  导航 → 隐式 JOIN；集合导航 → EXISTS / 标量子查询 / 分组 join
   │                  拦截器注入的条件（租户、逻辑删除）在这里合并
   ├─ 5. 规划       Planner：投影里的对多集合拆成子查询节点（见 0002）
   ├─ 6. 渲染       Dialect.Render(SqlNode) → Statement { Sql, Parameters }
   │                  标识符引用、分页、upsert、函数表、版本分支；参数到这一步才绑定
   ├─ 7. 执行       Session.ExecuteReaderAsync → DbCommand；执行事件、日志、Activity
   └─ 8. 物化       生成器产出的 Reader：DbDataReader → 实体 / DTO；值转换器在这里生效
```

### 核心抽象

| 抽象 | 职责 | 对应 Java / TS |
|---|---|---|
| `EntityMetadata` / `ColumnMetadata` / `NavigationMetadata` | 表名、列名、键、关系、转换器、逻辑删除和版本列 | `EntityMetadata` / `schema.ts` |
| `EasyDb` | 入口：`Query<T>()`、`Insert` / `Update` / `Delete` / `Save`、`Sql`、`BeginTransactionAsync` | `EasyEntityQuery` / `Db` |
| `IQuery<T>`、`IQuery<T1, T2>` … | 查询构建，不可变 | `EntityQueryable` / `Queryable` |
| `SqlNode`（`SelectNode` / `InsertNode` / `UpdateNode` / `DeleteNode` / `CteNode` …） | SQL AST，与方言无关 | `SQLExpression` / `ast.ts` |
| `Dialect` | AST 渲染成文本、函数表 `Functions`、版本分支 `AtLeast`、能力 `Supports` | `ExpressionFactory` + `SQLFunc` / `Dialect` |
| `Session` / `Driver` | 执行文本 SQL、事务、取消，由驱动适配实现 | `JdbcExecutor` / `session.ts` |
| `Interceptors` | `Filter` / `BeforeInsert` / `BeforeUpdate` / `AfterWrite` | `EntityInterceptor` 等 / `interceptor.ts` |
| `ValueConverter<TModel, TProvider>` | 属性值和数据库值互转 | `ValueConverter` |
| `ColumnSqlConverter` | 在 SQL 侧包一层，例如 `AES_DECRYPT(col)` | `ColumnValueSQLConverter` |

### 方言

- **继承**：抽象基类 `Dialect` 提供各方言通用的写法，子类只覆盖差异，例如 `DamengDialect : OracleDialect`、`OpenGaussDialect : PostgreSqlDialect`。
- **版本策略**（照搬 TS）：默认用一套各版本通用的写法，只有以下情况才按版本分支：旧语法被弃用、旧版本没有该能力、语义或性能差别大。可以连库读取版本。
- **方言专属函数**：放在静态入口下，例如 `MySql.JsonContains(u.Attrs, "$.a")`、`Pg.Array(u.Tags).HasAny(...)`、`Hana.Text(u.Body).Contains(...)`，对应 Java fork 的 decisions/0001。这些入口本身是扩展方法或静态方法，可以写在表达式树里；用错方言时在渲染期报错，消息带上函数名和当前方言。
- **能力标记**：`Capability` 枚举，包括 `Returning` / `Upsert` / `ForUpdateSkipLocked` / `Merge` / `WindowFrame` 等。方言声明自己支持哪些，渲染时检查，不支持就明确报错。

## 用到的 C# 特性

| 需求 | Java / TS 的做法 | C# 的做法 |
|---|---|---|
| 类型安全的列引用 | APT Proxy / 运行时引用对象 | 表达式树，直接写在实体上 |
| 投影的结果类型 | `Draft1..N` / 形状推导 | 匿名类型、record、主构造函数 |
| 多表 lambda | `EntityQueryable2..10` | 泛型重载 `IQuery<T1..T8>`，由仓库内的生成器产出，不手写 |
| 可空导航决定 JOIN 方式 | `@Navigate(required)` / `required` | 可空引用类型：`Company?` 用 LEFT，非空用 INNER |
| SQL 函数 | 列对象上的方法 | 扩展方法加 `[SqlFunction]`；用户的自定义函数和内置函数写法一样 |
| 元数据和物化器 | 运行时反射 / 运行时 schema | Source Generator 在编译期生成 |
| 事务 | ThreadLocal / 显式传 tx | `AsyncLocal` 环境事务：`await using var tx = await db.BeginTransactionAsync();`，作用域内的查询自动加入 |
| 流式读取 | `streamBy` / `stream()` | `IAsyncEnumerable<T>` + `await foreach` |
| 取消 | 无 / `AbortSignal` | `CancellationToken` 全链路传递 |
| 执行监听 | `JdbcExecutorListener` / `ExecutionEvent` | `ActivitySource`（对接 OpenTelemetry）+ `ILogger` + `DiagnosticListener` |
| 框架接入 | Spring Boot starter | `services.AddEasyQuery(o => o.UseMySql(dataSource))` |
| 驱动抽象 | JDBC `DataSource` | ADO.NET `DbDataSource`（Npgsql、MySqlConnector、SqlClient、Sqlite 都支持） |
| 动态条件 | `where(cond, x -> ..)` / 数组里的 `cond && expr` | `WhereIf(cond, ..)`；表单驱动的动态筛选用 `FilterBy(request, rules)` |
| 行内原生 SQL | `sqlNativeSegment` / `` sql`…` `` 标签 | `Sql.Raw<T>($"DATE_FORMAT({u.CreatedAt}, '%Y')")`：插值字符串处理器把列和值分开，值自动参数化 |

### 分析器

生成器包里同时带上 Roslyn Analyzer，在编译期报出这些问题：

- `EQ0001`：查询 lambda 里出现了不支持翻译的方法或属性，例如 `u.Name.Normalize()`。
- `EQ0002`：用了表达式树不能写的语法，在编译器报错的基础上给出改写建议，比如 `?.` 改成 `!`。
- `EQ0003`：`[MapFrom<T>]` 的 DTO 成员在实体上找不到对应的属性或路径。
- `EQ0004`：`[Navigate]` 的 `ExtraFilter` 引用的成员不存在，或者类型不对。
- `EQ0005`：对一导航在 DTO 里声明为非空，但关系本身可空。
- `EQ0006`：没有 `Where` 的 `Update` / `Delete`，提示加上 `.All()`（运行时同样会拒绝）。

## 扩展点

用户不改源码就能扩展的地方：

1. 自定义 SQL 函数：写一个扩展方法，标上 `[SqlFunction("...")]`，或者在方言函数表里注册渲染方式。
2. 值转换器、类型处理器。
3. 拦截器：全局注册，也可以在单条查询上用 `.DisableInterceptor<TenantInterceptor>()` 关掉某一个。
4. 命名约定：默认驼峰转下划线，可替换。
5. 方言：继承已有方言，覆盖差异。
6. 分片路由：继承表路由或库路由的基类。

## 命名

- 入口方法：`db.Query<T>()`，不叫 `Queryable`，避免和 `System.Linq.Queryable` 混淆。写入用 `db.Insert(...)` / `db.Update<T>()` / `db.Delete<T>()` / `db.Save(...)`。
- 终结方法：`ToListAsync` / `FirstAsync` / `FirstOrDefaultAsync` / `SingleAsync` / `AnyAsync` / `CountAsync` / `ToPageAsync` / `ExecuteAsync`（写入，返回受影响的行数）。
- 写入一律以 `ExecuteAsync` 结束（TS 的决定），读代码时一眼看出哪里真正执行。
