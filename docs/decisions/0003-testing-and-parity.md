# 0003 测试对标：逐条移植 Java 用例，SQL 文本与 Java 对齐

状态：已定（2026-11-04；2026-11-06 按实现修订：测试项目的实际布局、对标基准冻结、按方言分档的本地与 CI 分工、工具链）

## 背景

- Java 版测试规模：上游 `sql-test` 有 **2315** 个 `@Test`，fork 的 `sql-fork-test` 有 **89** 个，合计 **2404** 个，全部是 JUnit 4（含 66 个写全名 `@org.junit.Test` 的用例）。
- 大多数用例连 MySQL 实库，执行后通过 `JdbcExecutorListener` 捕获 SQL，再断言**SQL 全文**和**参数文本**（格式如 `123xxx(String)`）。
- 约 180 个用例不连库：用 `EmptyDataSource` 只生成 SQL，主要是字符串、通用、数值三组函数测试和 `ForUpdateDialectToSQLSmokeTest`。
- TS 版用 `tools/parity-tests.mjs` 把每个 Java 用例按方法体里的 API 归到一种能力，状态记在能力上，生成 `docs/parity-tests.md`。它的粒度是**能力**：一个能力标成"已对应"，下面所有用例都算对应。TS 还明确不追求和 Java 逐字相同的 SQL。

用户的要求是 C# 版"所有单元测试和能力都要完整对应"，所以粒度必须到**每个用例**。

## 决定

### 1. 每个 Java 用例都有一个 C# 测试，状态可以机器统计

C# 测试方法上标出它对应的 Java 用例：

```csharp
[Fact, Upstream("QueryTest.java#query0")]
public async Task Query0()
{
    using var listen = Listen();
    var user = await Db.Query<SysUser>().Where(s => s.Id == "123xxx").FirstOrDefaultAsync();
    Assert.Null(user);
    listen.AssertSql("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE `id` = ? LIMIT 1");
    listen.AssertParams("123xxx(String)");
}
```

`[Upstream]` 一共四种状态：

| 写法 | 含义 |
|---|---|
| `[Upstream("A.java#m")]` | 已移植，SQL 与 Java 一致 |
| `[Upstream("A.java#m", Deviation = "投影别名用成员名而不是 value1")]` | 已移植，SQL 有差异，理由写清楚 |
| `[Upstream("A.java#m", NotApplicable = "H2 没有 .NET 驱动")]` | 不适用。可以加在一个空方法上，也可以写在 `tests/upstream-na.txt` 里 |
| `[Upstream("A.java#m", Pending = true)]` + Skip | 骨架已生成，还没写 C# 查询 |

一个 C# 测试可以对应多个 Java 用例，比如 `[Upstream("A#m1"), Upstream("A#m2")]`。

### 2. SQL 文本默认与 Java 逐字一致

C# 的渲染规则对齐 Java：

- 单表查询不加别名；多表时别名依次是 `t`、`t1`、`t2`……
- 逗号后不留空格；关键字大写。
- 列的顺序等于属性声明的顺序。
- JOIN 和子查询的写法照 Java。

这样做的好处：

- **期望值可以机器搬运**：工具从 Java 方法体里抽出 SQL 和参数字符串，直接写进 C# 测试骨架。人只需要写 C# 那一侧的查询。
- **有现成的正确答案**：Java 的 SQL 经过多年使用检验，逐字对比是最强的回归保护。
- 和 Java 用户的认知一致，排查问题时能直接对照两边的 SQL。

细节处理：

- **占位符**：MySqlConnector 支持 `?`，但 SQL Server 只能用命名参数，Npgsql 用 `$1`。所以 `Dialect` 有一个 `ParameterStyle` 选项，测试里统一用 `Question` 渲染再比对；生产环境按驱动选择。
- **参数文本**：`AssertParams` 把 C# 类型映射成 Java 的类型名再拼文本，Java 的期望字符串原样可用。映射是 `string`→String、`int`→Integer、`long`→Long、`decimal`→BigDecimal、`DateTime`→LocalDateTime、`DateOnly`→LocalDate、`bool`→Boolean。
- **允许偏离**：差异只要是 C# API 形状带来的就可以偏离，比如匿名类型的成员名替代了 Java Draft 的 `value1`，或者 Java 自身有 bug。偏离必须写 `Deviation` 说明理由，并在对照表里单独统计。

### 3. 测试分四层

| 层 | 项目 | 数据库 | 规模（目标） | 何时跑 |
|---|---|---|---|---|
| L1 SQL 文本 | 放在 `tests/EasyQuerySharp.Tests.Upstream` 里，与 L2 同一个项目 | 不连库，`SqlOnly` 会话（对应 `EmptyDataSource`，在 TestKit 里） | Java 不连库的约 180 个用例、fork 的方言与函数用例、C# 特有的翻译用例 | 每次构建，CI |
| L2 上游移植 | `tests/EasyQuerySharp.Tests.Upstream` | MySQL 8.4 实库，库名 `easy-query-test`，表结构和种子数据对齐 Java `BaseTest`；PG、SQL Server、达梦等按 Java 子目录分别连 | 约 2150 个，按 Java 文件一一对应 | 本地和 CI `core` 任务跑 MySQL 和 PG 部分；其他方言在各自的 CI 任务里跑 |
| L3 跨方言共用套件 | `tests/EasyQuerySharp.Tests.Live` | 每个方言一个薄子类，环境变量启用 | 照 TS 的 `liveSuite`（23 组、138 个用例），乘以方言数 | 本地跑 SQLite、MySQL、PG；CI 每种数据库一个任务 |
| L4 生成器与分析器 | `tests/EasyQuerySharp.Tests.Generator` | 无 | 生成代码快照、每条诊断的正例和反例 | 每次构建 |

另外，`docs/examples` 的每张示例卡片在 CI 里实跑一次并比对各方言的 SQL 快照，见 [docs-site.md](../design/docs-site.md)。

L2 的组织方式：

- 类名照搬 Java，目录只有一层、按主题分：`Queries/`、`Write/`、`Functions/`、`Features/`、`Dialects/`、`Fork/`、`Mysql8/`、`Pgsql/`、`Mssql/`、`H2/`（H2 没有 .NET 驱动，改在 SQLite 上跑），C# 独有 API 的用例放 `Extras/`（不计入对标）。例如 `sql-test/.../QueryTest.java` 对应 `Queries/QueryTest.cs`，`mysql8/M8BankTest.java` 对应 `Mysql8/M8BankTest.cs`。实体在 `Entities/`、DTO 在 `Dto/`，夹具与测试用的策略、拦截器在 `Support/`。
- 1.0 范围之外的 Java 用例在 `OutOfScope.cs` 里用程序集级的 `[Upstream(NotApplicable = …)]` 列出。
- 实体模型照搬 Java：约 277 个 `@Table` 类，按 Java 的包放进对应命名空间。同名的 `SysUser` 有 6 份，分属不同命名空间，不合并。
- Java 测试基类对应 C# 的测试基类（xUnit fixture）。`BaseTest` 对应 `MySqlFixture`：负责建表（code-first 完成之前先执行 Java 的 SQL 脚本）和种子数据（`initData` 的 100 条 `BlogEntity` 等）。`PgSQLBaseTest`、`MsSQLBaseTest` 等依次对应。
- 捕获 SQL：`Listen()` 返回一个作用域对象，订阅执行事件，对应 Java 的 `ListenerContext`；`ListenAll()` 用于一次调用发出多条 SQL 的情况。

### 4. 对照工具 `tools/ParityTests`

这是一个 C# 控制台，参考 TS 的 `parity-tests.mjs`，粒度改成每个用例：

1. 扫描 Java 仓库的 `sql-test/src` 和 `sql-fork-test/src`：
   - 抽出每个 `@Test` 的方法体，先去掉注释；
   - 按 TS `capabilities.mjs` 的规则归到一种能力（先看文件名，再看方法体里的 API）；
   - 抽出方法体里所有断言的 SQL 和参数字符串。
2. 扫描 C# 测试项目里的 `[Upstream]` 标注（用 Roslyn 读语法树，不反射）。
3. 输出：
   - `docs/parity-tests.md`（生成文件，不要手改）：总数，按能力的已移植 / 偏离 / 不适用 / 待做统计，逐条明细（Java 用例 → C# 测试 → 状态 → SQL 摘要）。
   - `--scaffold <Java 文件>`：为还没有对应 C# 文件的 Java 文件生成测试骨架。骨架里有方法名、`[Upstream(Pending = true)]`、Skip、期望 SQL 和参数断言、Java 原方法体的注释。已经存在的文件不覆盖。
   - `--check`：在 CI 里检查三件事：对照表是不是最新；`[Upstream]` 引用的 Java 用例是否存在（防止写错）；有没有一个 Java 用例被两个 C# 测试重复声明。
4. 对标基准冻结在快照 `tools/ParityTests/java-tests.json`（取自哪个 Java 版本，加每个用例的 Id、能力、SQL 摘要）。对照表和 `--check` 只看快照，本机和 CI 都不会因为 Java 仓库换了版本而改变统计。
   - `--scaffold` / `--show` 要用方法体，从本机 Java 仓库取，只取基准里有的用例。仓库位置用 `--java <路径>` 指定，默认 `..\..\IdeaProjects\easy-query`。
   - `--rescan` 按 Java 仓库重新生成快照，并列出新增和移除的用例。只在决定移动基准时使用，要求仓库里同时有 `sql-test` 和 `sql-fork-test`。

**完成标准**：对照表里"待做"为 0；"不适用"每一条都有理由；"偏离"每一条都有理由，并且已经评审过。

### 5. 跨方言共用套件照搬 TS 的结构

- 抽象基类 `LiveSuite`，每个方言一个子类，只负责提供连接和方言实例。
- `LiveSuiteOptions` 是一个 record，用布尔开关裁剪能力：`Returning`、`RecursiveCte`、`Rollup`、`Cube`、`RowLocks`、`NoWait`、`JsonEdits`、`Savepoints` 等，和 TS 的选项同名。
- 连接串取自环境变量 `EQS_<KEY>_CS`（ADO.NET 连接串）。没有设置时，回退到仓库根的 `.local/db.env`。两处都没有时，这组测试跳过（用 xUnit v3 的 `Assert.Skip`）。
- `EQS_REQUIRE_DBS`（逗号分隔的 Key）列出必须连上的库：列出的库没配置或连不上时，判失败而不是跳过。CI 用它防止服务容器没起来时测试被静默跳过。
- 数据库在 `tests/EasyQuerySharp.TestKit/Databases/TestDatabases.cs` 里登记：Key、驱动工厂、参数前缀。

### 本地与 CI 的分工

| 数据库 | 本地 | CI |
|---|---|---|
| SQLite（进程内） | 必测 | `core` 任务 |
| MySQL 8.4 | 按需：`--profile mysql`（2026-11-07 起本地只起 PG） | `core` 任务 |
| PostgreSQL 17 | 必测，`infra/dev` 默认启动 | `core` 任务 |
| MySQL 8.0.18（PolarDB 基线）、MariaDB 11.4、SQL Server 2022、Oracle 23ai（第一档其余的库） | 按需：`--profile polardb / mariadb / mssql / oracle` | 各自一个任务；阶段 2 起跑全部实库用例 |
| 达梦、openGauss、Kingbase、DuckDB、ClickHouse、Db2、TDengine（第二档） | 按需：对应 profile（DuckDB 进程内，必测） | 比对 SQL 文本随 `core` 任务；除 Kingbase（没有镜像）外各有一个实库任务 |
| HANA（第二档） | 按需：`--profile hana`（7 GB），还要装 SAP 的 HANA 客户端 | `hana` 任务（下载 Linux 客户端并缓存） |

- 本地容器：仓库自带 `infra/dev/compose.yaml`，compose 项目名是 `easy-query-sharp-dev`，端口统一用 4xxxx，和 `easy-query-ts-dev`（3xxxx）、`vue-tsx-pro-dev`（2xxxx）错开，几个项目可以同时运行。
  - 镜像版本和 digest 跟 TS 仓库一致，两边的实库结果可以直接对比。
  - `infra/dev/db.sh` 首次运行时生成 `.local/db.env`（密码与连接串，不提交）。按 profile 启动的库，连接串默认是注释掉的，启动容器后再取消注释。
- CI：`.github/workflows/ci.yml` 里每种数据库一个任务，各自带服务容器；公共步骤放在 `.github/actions/dotnet-test`。
  - 触发：push 到 master、PR、手动，以及每天一次定时运行，用来发现驱动和镜像的漂移。
  - 失败时上传 xUnit 的 TRX 报告。
- 2026-11-04 实测：七个库的连通性自检全部通过（SQLite 3.53、MySQL 8.4.11 / 8.0.18、MariaDB 11.4.13、PG 17.11、SQL Server 2022 CU27、Oracle 23.26），内容是建表、带参数插入、查询、删表。

### 6. 工具链

- 测试框架用 **xUnit v3**：跑在 Microsoft Testing Platform 上，有动态跳过（`Assert.Skip`）。Upstream 项目的用例共用同一个库，集合之间不并行（`xunit.runner.json`）；改了种子数据的用例结束前要还原。
- 逐条移植的用例用内联字符串断言，期望值就是 Java 的原文，放在代码里更好对照。文档 SQL 的跨方言快照由 `tools/DocsExamples` 生成并在 CI 里校验；生成器与分析器的测试也用内联断言。不引入额外的快照库。

## 后果

- SQL 渲染器的输出格式要尽早定死，以 Java 为准。前期为对齐别名、空格、列顺序花的时间，换来的是 2000 多条期望值可以机器搬运。
- L2 依赖 MySQL 实库。CI 要起 MySQL 8.4 服务容器（TS 版的 CI 已经这样做过）。
- 测试项目体量会很大（约 2400 个测试加约 280 个实体类），要按 Java 的包拆命名空间，编译时间要盯着；必要时把 `Upstream` 拆成按方言的几个项目。
- TS 版独有的能力（多态、值对象 `embed`、计算属性、`table(request)` 等）不在 Java 用例里，单独在 [parity.md](../parity.md) 里列出，按需做。
