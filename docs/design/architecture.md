# 架构

本文说明各模块怎么分、一条查询从调用到出结果经过哪些层，以及用到哪些 C# 特性。为什么走表达式树见 [0001](../decisions/0001-query-api-route.md)，对象树见 [0002](../decisions/0002-object-tree-and-dto.md)，测试见 [0003](../decisions/0003-testing-and-parity.md)，聚合保存见 [0004](../decisions/0004-aggregate-save.md)。

## 目标与约束

- **能力完整对标** easy-query：上游 `sql-test` 2315 个用例加 fork 的 `sql-fork-test` 89 个，逐条有对应（基准冻结在 `tools/ParityTests/java-tests.json`）。API 可以和 Java 不一样，但要更顺手。
- **目标框架**：`net10.0`（LTS），不做多目标。生成器和分析器按 Roslyn 的要求用 `netstandard2.0`。
- **核心零第三方依赖**：只依赖 BCL。驱动由使用者通过 `DbDataSource` 传入，核心不引用任何驱动。
- **异步优先**：所有终结方法都是 `…Async(CancellationToken)`。同步版本不做，记在 BACKLOG。
- **不支持 AOT 和裁剪**（1.0）：元数据靠反射读取，物化器在运行时编译成委托并按形状缓存。查询本身是表达式树，在 NativeAOT 下只能解释执行。理由与以后的做法见 BACKLOG。

## 目录与模块

```
EasyQuerySharp.slnx
src/
  EasyQuerySharp/                    核心：元数据、查询模型、翻译、SQL AST、各方言、执行、追踪、拦截器、列加密、code-first
  EasyQuerySharp.Generator/          [Projection] 生成器与分析器（netstandard2.0，随核心包分发）
tests/
  EasyQuerySharp.TestKit/            测试共用：SqlOnly 会话、SqlCapture 捕获 SQL、Upstream 标注、TestDatabases 连接登记
  EasyQuerySharp.Tests.Upstream/     逐条移植的 Java 用例（L1 不连库的 SQL 文本用例也在这里）与 C# 独有 API 的用例（Extras/）
  EasyQuerySharp.Tests.Live/         L3 跨方言共用实库套件（每个库一个薄子类）
  EasyQuerySharp.Tests.Generator/    生成器和分析器的测试
samples/
  EasyQuerySharp.Samples/            示例模型（TS 文档站的表与种子数据），文档卡片用它
benchmarks/
  EasyQuerySharp.Benchmarks/         BenchmarkDotNet
tools/
  ParityTests/                       Java 用例对照：生成 docs/parity-tests.md、测试骨架
  DocsExamples/                      执行文档卡片，渲染各方言 SQL，输出快照
  CodeGen/                           生成多表查询 Query<T1..T10>（Querying/Query.Joins.g.cs）的脚本
docs/
  decisions/  design/  examples/  sql-snapshots/
```

以后按需新增的包：`EasyQuerySharp.DependencyInjection`、`EasyQuerySharp.Caching`（阶段 4），以及用到驱动专属 API（批量复制、PG 数组类型等）的方言包。

核心包内部只有一层目录，命名空间与目录一致，同一目录里用文件名区分职责：

| 目录 | 内容 |
|---|---|
| 根目录 | 用户直接用的入口：`EasyDb`、`EasyDbOptions`、`Sql`（含 `Sql.Over`、`Sql.MySql`、`Sql.Pg`、`Sql.Json`）、`Window`、`Draft` / `Part`、`DynamicRow`、异常 |
| `Metadata/` | 实体元数据、`[Table]` / `[Column]` / `[Navigate]` / `[LogicDelete]` / `[Version]` 等特性、值转换、逻辑删除策略 |
| `Querying/` | 公开的 `Query` / `QueryBase` / `GroupQuery`，以及内部的 `QueryState`、`QueryCompiler`、`ProjectionCompiler`、`AutoProjection`、`Include*` |
| `Translation/` | 表达式树 → SQL AST：`ExpressionTranslator.*`（按主题分 partial）、`TranslationContext`、`MethodTranslators`、`KeyPredicates` |
| `SqlAst/` `Rendering/` | SQL 语法树与渲染（别名分配、方言回调） |
| `Dialects/` | `SqlDialect`（通用渲染、upsert、函数表、DDL 四个分部文件）与各库的 `XxxDialect`（MySQL、PostgreSQL、SQLite、SQL Server、Oracle、达梦、DuckDB、HANA、Kingbase、GaussDB、ClickHouse、Db2、TDengine） |
| `Commands/` | 插入、更新、删除、聚合保存：`EntityCommandBase` → `WriteCommandBase`，`WriteScope` 是一条写入语句的翻译作用域，`SavePlan` 是一次聚合保存的计划 |
| `Execution/` `Tracking/` `Interceptors/` `Filtering/` `Encryption/` `Materialization/` `Schema/` | 执行与环境事务、追踪、拦截器、按对象筛选、列加密（AES、SM4）、读取结果、code-first（`CodeFirst` 是入口，`SchemaPlanner` 规划建表与同步的语句，`SchemaReader` 执行方言的系统表查询读库结构；语句的写法在各方言的 DDL 分部） |

模块边界的规则：

- 方言类只负责渲染 SQL 和函数表，不写业务逻辑。
- 核心包不引用任何驱动。驱动之间的差异（取生成键的方式、参数类型）用 ADO.NET 的通用接口处理。

## 一条查询的管线

```
db.Query<User>().Where(..).Select(..).ToListAsync()
   │
   ├─ 1. 查询模型   不可变的 QueryState：每次链式调用返回新对象，只记录 lambda，不翻译
   ├─ 2. 归一       ProjectionInliner：展开 [Projection] 方法、Project(map)；Select<TDto>() 由 AutoProjection 构造成同样的投影表达式；Include 也是投影
   ├─ 3. 翻译       ExpressionTranslator：表达式 → SQL AST
   │                  导航 → 隐式 JOIN；集合导航 → EXISTS / 标量子查询 / 分组 join
   │                  拦截器注入的条件（租户、逻辑删除）在这里合并
   ├─ 4. 规划       ProjectionCompiler：投影里的对多集合、带出拆成按键批量的子查询（见 0002）
   ├─ 5. 渲染       SqlRenderer + SqlDialect → SqlStatement { Text, Parameters }
   │                  标识符引用、分页、upsert、函数表；参数到这一步才绑定
   ├─ 6. 执行       SqlExecutor → DbCommand / DbBatch（批量写入）；环境事务、执行监听（IExecutionListener）、ActivitySource
   └─ 7. 物化       读取委托：按读取树的结构缓存（ShaperCache），同形状只编译一次；值转换器在这里生效
```

翻译结果目前不缓存：实测翻译加渲染只要十几 µs，开销的大头是编译读取委托（约 1 ms），已经由 ShaperCache 缓存。是否缓存翻译结果，等阶段 5 的基准对比后再定。

### 核心抽象

| 抽象 | 职责 | 对应 Java / TS |
|---|---|---|
| `EntityMetadata` / `ColumnMetadata` / `NavigationMetadata` | 表名、列名、键、关系、转换器、逻辑删除和版本列 | `EntityMetadata` / `schema.ts` |
| `EasyDb` | 入口：`Query<T>()`、`Insert` / `Update` / `Delete` / `Save`、原生 SQL、事务、追踪 | `EasyEntityQuery` / `Db` |
| `Query<T>`、`Query<T1, T2>` … `Query<T1..T10>` | 查询构建，不可变 | `EntityQueryable` / `Queryable` |
| `SqlExpr` / `SelectQuery` / `InsertStatement` / `UpdateStatement` / `DeleteStatement` | SQL AST，与方言无关 | `SQLExpression` / `ast.ts` |
| `SqlDialect` | AST 渲染成文本、函数表、标识符引用、分页、upsert、建表类型 | `ExpressionFactory` + `SQLFunc` / `Dialect` |
| `SqlExecutor` / `EasyTransaction` | 执行 SQL、批量写入（按 SQL 分组，有 `DbBatch` 时交给它）、环境事务（每个库一个，不能嵌套）、取消 | `JdbcExecutor` / `session.ts` |
| `QueryFilter<T>` / `EntityInterceptor<T>` | 查询条件过滤、写入前改实体、按条件更新追加 SET | `EntityInterceptor` 等 / `interceptor.ts` |
| `ValueConverter<TModel, TProvider>` | 属性值和数据库值互转 | `ValueConverter` |
| `ColumnSqlConversion` | 在 SQL 侧包一层，例如库端加解密 | `ColumnValueSQLConverter` |

### 方言

- **继承**：抽象基类 `SqlDialect` 提供各方言通用的写法，子类只覆盖差异。新方言按同样方式加，例如达梦继承 Oracle、openGauss / Kingbase 继承 PostgreSQL。
- **版本策略**（照搬 TS）：默认用一套各版本通用的写法，只有以下情况才按版本分支：旧语法被弃用、旧版本没有该能力、语义或性能差别大。MySQL 方言以 8.0.18（PolarDB 基线）为下限。
- **方言专属函数**：放在 `Sql` 下的静态入口，例如 `Sql.MySql.Json(u.Attrs).Contains(..)`、`Sql.Pg.Array(u.Tags)`。这些入口可以写在表达式树里；用错方言时在渲染期报错，消息带上函数名和当前方言。
- **不支持的能力**：方言渲染时抛 `NotSupportedException`，消息说明怎么改写（如 SQL Server 的行锁、SQLite 的 CUBE）。
- **分档**：第一档（SQLite、MySQL / MariaDB、PG、SQL Server、Oracle）在 CI 里跑全部实库用例；第二档（达梦、HANA、DuckDB、Kingbase / openGauss、ClickHouse、TDengine、Db2）比对 SQL 文本，再加实库冒烟。

## 用到的 C# 特性

| 需求 | Java / TS 的做法 | C# 的做法 |
|---|---|---|
| 类型安全的列引用 | APT Proxy / 运行时引用对象 | 表达式树，直接写在实体上 |
| 投影的结果类型 | `Draft1..N` / 形状推导 | 匿名类型、record、主构造函数；`Draft<…>` 对应 Java 的 Draft |
| 多表 lambda | `EntityQueryable2..10` | 泛型重载 `Query<T1..T10>`，由 `tools/CodeGen` 的脚本生成，不手写 |
| 可空导航决定 JOIN 方式 | `@Navigate(required)` / `required` | `[Navigate(Required = true)]` 用 INNER JOIN，否则 LEFT JOIN |
| SQL 函数 | 列对象上的方法 | 扩展方法；用户的自定义函数标 `[SqlFunction("模板")]`，写法和内置函数一样 |
| 事务 | ThreadLocal / 显式传 tx | `AsyncLocal` 环境事务：`await using var tx = db.BeginTransaction();`，同一异步流程里该库的语句自动加入 |
| 流式读取 | `streamBy` / `stream()` | `await foreach (var x in q.AsAsyncEnumerable())` |
| 取消 | 无 / `AbortSignal` | `CancellationToken` 全链路传递 |
| 执行监听 | `JdbcExecutorListener` / `ExecutionEvent` | `IExecutionListener` 加 `ActivitySource("EasyQuerySharp")`（对接 OpenTelemetry） |
| 驱动抽象 | JDBC `DataSource` | ADO.NET `DbDataSource`（Npgsql、MySqlConnector、SqlClient、Sqlite 都支持） |
| 动态条件 | `where(cond, x -> ..)` / 数组里的 `cond && expr` | `WhereIf(cond, ..)`；表单驱动的动态筛选用 `FilterBy(request)` |
| 行内原生 SQL | `sqlNativeSegment` / `` sql`…` `` 标签 | `Sql.Raw<T>("DATE_FORMAT({0}, '%Y')", u.CreatedAt)`；整条原生 SQL 用 `db.SqlQueryAsync<T>($"…{value}")`，插值处自动参数化 |

### 分析器

生成器包里同时带上 Roslyn 分析器，在编译期报出运行时才会失败的写法，完整说明见 [analyzers.md](analyzers.md)：

| 编号 | 内容 |
|---|---|
| EQ0001 | 查询表达式里调用了用到查询参数、又不能翻译成 SQL 的方法 |
| EQ0002 | 查询表达式里用了 `?.`，给出改成 `!.` 的写法 |
| EQ0003 | `[MapFrom<T>]` 的 DTO 成员在实体上对不上 |
| EQ0004 | `[Navigate]` 的 `ExtraFilter` / `ExtraMappingFilter` 引用的成员不存在或类型不对 |
| EQ0005 | `[MapFrom<T>]` 的 DTO 把可空的对一导航映射成了非空成员 |
| EQ0006 | `[Projection]` 方法的写法生成器处理不了 |
| EQ0007 | 只能在查询表达式里调用的方法（`Sql.*`、`[SqlFunction]`）在普通代码里被调用 |

## 扩展点

用户不改源码就能扩展的地方：

1. 自定义 SQL 函数：写一个静态方法或扩展方法，标上 `[SqlFunction("...")]`。
2. 值转换器：继承 `ValueConverter<TModel, TProvider>`，在 `[Column(Converter = typeof(…))]` 上引用；库端转换继承 `ColumnSqlConversion`。
3. 拦截器：`options.AddInterceptor(..)` 全局注册，单条语句上用 `.DisableInterceptor("名字")` 关掉某一个。
4. 命名约定：默认驼峰转下划线，`options.NameConversion` 可替换。
5. 逻辑删除策略：实现 `ILogicDeleteStrategy`，`options.AddLogicDeleteStrategy("名字", 策略)` 注册。
6. 方言：继承已有方言，覆盖差异，用 `options.Use(方言, 数据源)`。

## 命名

- 入口方法：`db.Query<T>()`，不叫 `Queryable`，避免和 `System.Linq.Queryable` 混淆。写入用 `db.Insert(...)` / `db.Update<T>()` / `db.Delete<T>()` / `db.Save(...)`；原生 SQL 用 `db.FromSql<T>(..)`（可以继续组合的查询）、`db.SqlQueryAsync<T>(..)`、`db.ExecuteSqlAsync(..)`。
- 终结方法：`ToListAsync` / `FirstAsync` / `FirstOrDefaultAsync` / `SingleAsync` / `FindAsync` / `FindOrThrowAsync` / `AnyAsync` / `EnsureAnyAsync` / `CountAsync` / `ToPageAsync` / `AsAsyncEnumerable` / `ExecuteAsync`（写入，返回受影响的行数）。
- 写入一律以 `ExecuteAsync` 结束（TS 的决定），读代码时一眼看出哪里真正执行。
