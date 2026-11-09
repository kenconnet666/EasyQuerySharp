# 方言

方言在核心包里，只负责把 SQL 语法树渲染成各库的文本；驱动由你引用，通过 `DbDataSource` 传入（`options.UseXxx(数据源)`）。同一条查询换个方言就是换一个 `UseXxx`，写法不变。

## 一览

| 数据库 | 配置 | 方言类 | 驱动 | 档位 | 实库验证 |
|---|---|---|---|---|---|
| SQLite 3.35+ | `UseSqlite` | `SqliteDialect` | Microsoft.Data.Sqlite | 第一档 | 全部实库用例 |
| MySQL 8.4 | `UseMySql` | `MySqlDialect` | MySqlConnector | 第一档 | 全部实库用例 |
| MySQL 8.0.18（PolarDB 基线） | `UseMySql` | `MySqlDialect` | MySqlConnector | 第一档 | 全部实库用例 |
| MariaDB 11.4 | `UseMySql` | `MySqlDialect` | MySqlConnector | 第一档 | 全部实库用例 |
| PostgreSQL 17 | `UsePostgreSql` | `PostgreSqlDialect` | Npgsql | 第一档 | 全部实库用例 |
| SQL Server 2022 | `UseSqlServer` | `SqlServerDialect` | Microsoft.Data.SqlClient | 第一档 | 实库套件与 Java 的 38 个 SQL Server 用例 |
| Oracle 23ai | `UseOracle` | `OracleDialect` | Oracle.ManagedDataAccess.Core | 第一档 | 实库套件（Java 的 Oracle 用例只比对 SQL） |
| DuckDB | `UseDuckDb` | `DuckDbDialect`（继承 PG） | DuckDB.NET.Data.Full | 第二档 | 实库套件（进程内） |
| 达梦 DM8 | `UseDameng` | `DamengDialect`（继承 Oracle） | DM.DmProvider | 第二档 | 实库套件 |
| SAP HANA 2.0 | `UseHana` | `HanaDialect` | Sap.Data.Hana（随 HANA 客户端安装） | 第二档 | 实库套件与 fork 的 HANA 用例（HANA Express 2.00.088） |
| 人大金仓 Kingbase | `UseKingbase` | `KingbaseDialect`（继承 PG） | 金仓的 ADO.NET 驱动 | 第二档 | 只比对 SQL 文本 |
| GaussDB / openGauss | `UseGaussDb` | `GaussDbDialect`（继承 PG） | Npgsql（连接串加 `No Reset On Close=true`） | 第二档 | openGauss 7 跑实库套件；GaussDB 商业版没有实测 |
| ClickHouse 25.8 | `UseClickHouse` | `ClickHouseDialect` | ClickHouse.Driver（官方，HTTP） | 第二档 | 分析型冒烟（`ClickHouseLiveSuite`） |
| IBM Db2 12.1 | `UseDb2` | `Db2Dialect` | Net.IBM.Data.Db2（Linux 用 Net.IBM.Data.Db2-lnx） | 第二档 | 实库套件（不支持的能力跳过） |
| TDengine 3.3 | `UseTDengine` | `TDengineDialect` | TDengine.Connector（WebSocket） | 第二档 | 时序冒烟（`TDengineLiveSuite`） |

- **第一档**：CI 跑全部实库用例（`tests/EasyQuerySharp.Tests.Live` 的跨方言套件，以及对应的 Java 移植用例）。
- **第二档**：SQL 文本与 Java 用例（有的话）逐字比对，再加实库冒烟。标"只比对 SQL 文本"的还没有实库冒烟，参数占位符、函数写法可能还要按实库修正。
- 表里的实库验证都在 CI 上跑，每次提交和每天一次。
- MySQL 方言以 8.0.18（PolarDB 基线）为下限，不用更新版本才有的写法；MariaDB 用同一个方言。
- 指南里的 SQL 和 [`docs/sql-snapshots`](../sql-snapshots) 的快照覆盖 SQLite、MySQL、PostgreSQL、SQL Server、Oracle、达梦六种方言。

## 能力矩阵

| 方言 | 分页 | upsert | 回填自增键 | `RETURNING` | 行锁 | UPDATE / DELETE 走对一导航 | `Sql.Json` | 递归 CTE | ROLLUP / CUBE |
|---|---|---|---|---|---|---|---|---|---|
| SQLite | `LIMIT … OFFSET` | `ON CONFLICT` | `last_insert_rowid()` | 有 | 不支持 | `UPDATE … FROM` / `EXISTS` | 有 | 有 | 都不支持 |
| MySQL / MariaDB | `LIMIT … OFFSET` | `INSERT IGNORE` / `ON DUPLICATE KEY UPDATE` | `LAST_INSERT_ID()` | 不支持 | 有 | `UPDATE … JOIN` / `DELETE t FROM … JOIN` | 有 | 有 | `WITH ROLLUP`，没有 CUBE |
| PostgreSQL | `LIMIT … OFFSET` | `ON CONFLICT` | `RETURNING` | 有 | 有 | `UPDATE … FROM` / `DELETE … USING` | 有 | 有 | 有 |
| SQL Server | `TOP` / `OFFSET … FETCH` | `MERGE` | `OUTPUT INSERTED` | 不支持 | 不支持（用表提示） | `UPDATE t … FROM … JOIN` | 不支持 | 有 | 有 |
| Oracle | `ROWNUM` | `MERGE` | `RETURNING … INTO` | 不支持 | 有 | 按 ROWID 的 `MERGE` / `DELETE` | 不支持 | 有 | 有 |
| DuckDB | `LIMIT … OFFSET` | `ON CONFLICT` | `RETURNING` | 有 | 不支持 | 同 PG | 有 | 有 | 有 |
| 达梦 | `ROWNUM` | `MERGE` | `RETURNING … INTO` | 不支持 | 有 | `UPDATE … JOIN`；删除同 Oracle | 不支持 | 有 | 有 |
| HANA | `LIMIT … OFFSET` | `MERGE … FROM DUMMY` | `CURRENT_IDENTITY_VALUE()` | 不支持 | 有（`IGNORE LOCKED`） | 按主键 `MERGE` / `EXISTS` | 不支持 | 不支持 | 有 |
| Kingbase | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG |
| GaussDB / openGauss | 同 PG | `ON DUPLICATE KEY UPDATE` | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG | 同 PG |
| ClickHouse | `LIMIT … OFFSET` | 不支持 | 没有自增列 | 不支持 | 不支持 | 不支持（更新删除是 `ALTER TABLE` mutation） | 有 | — | — |
| Db2 | `OFFSET … FETCH` | `MERGE … USING (VALUES …)` | `IDENTITY_VAL_LOCAL()` | 不支持 | `WITH RS`，没有 NOWAIT | 按主键 `MERGE` / `EXISTS` | 有（没有数组长度） | 有 | 有 |
| TDengine | `LIMIT … OFFSET` | 不支持 | 没有自增列 | 不支持 | 不支持 | 不支持（没有 UPDATE） | 不支持 | — | — |

不支持的能力在渲染时抛 `NotSupportedException`，消息说明替代写法（如 SQL Server 的行锁改用 `WITH (UPDLOCK, ROWLOCK)`，ClickHouse 的 upsert 改用 ReplacingMergeTree）。各页的"方言差异"一节有更细的差异：[函数](functions.md#方言支持)、[写入](writes.md#方言差异)、[联表与导航](joins-and-navigations.md#方言差异)、[事务与行锁](transactions.md#行锁)。

同一条分页查询在六种方言上的写法：

<!-- card: query/SkipTake sql=sqlite,mysql,pgsql,mssql,oracle,dameng -->
```csharp
db.Query<Post>().OrderBy(p => p.Id).Skip(2).Take(3).Select(p => p.Title).ToListAsync();
```

```sql
-- SQLite
SELECT t."title" FROM "t_post" t ORDER BY t."id" ASC LIMIT 3 OFFSET 2;
```

```sql
-- MySQL
SELECT t.`title` FROM `t_post` t ORDER BY t.`id` ASC LIMIT 3 OFFSET 2;
```

```sql
-- PostgreSQL
SELECT t."title" FROM "t_post" t ORDER BY t."id" ASC LIMIT 3 OFFSET 2;
```

```sql
-- SQL Server
SELECT t.[title] FROM [t_post] t ORDER BY t.[id] ASC OFFSET 2 ROWS FETCH NEXT 3 ROWS ONLY;
```

```sql
-- Oracle
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."title" FROM "t_post" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 6) rt1 WHERE rt1."__rownum__" > 2;
```

```sql
-- 达梦
SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."title" FROM "t_post" t ORDER BY t."id" ASC) rt WHERE ROWNUM < 6) rt1 WHERE rt1."__rownum__" > 2;
```
<!-- /card -->

同一条 upsert：

<!-- card: writes/OnConflictUpdate sql=sqlite,mysql,pgsql,mssql,oracle,dameng -->
```csharp
db.Insert(new OrderItem { OrderId = 1, ProductId = 6, Quantity = 3, Price = 29.90m })
    .OnConflictUpdate(i => new { i.Quantity, i.Price })
    .ExecuteAsync();
```

```sql
-- SQLite
INSERT INTO "t_order_item" ("order_id","product_id","quantity","price") VALUES (?,?,?,?) ON CONFLICT ("order_id","product_id") DO UPDATE SET "quantity" = EXCLUDED."quantity", "price" = EXCLUDED."price";
-- params: [1, 6, 3, 29.90]
```

```sql
-- MySQL
INSERT INTO `t_order_item` (`order_id`,`product_id`,`quantity`,`price`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `quantity` = VALUES(`quantity`), `price` = VALUES(`price`);
-- params: [1, 6, 3, 29.90]
```

```sql
-- PostgreSQL
INSERT INTO "t_order_item" ("order_id","product_id","quantity","price") VALUES (?,?,?,?) ON CONFLICT ("order_id","product_id") DO UPDATE SET "quantity" = EXCLUDED."quantity", "price" = EXCLUDED."price";
-- params: [1, 6, 3, 29.90]
```

```sql
-- SQL Server
MERGE INTO [t_order_item] t1 USING (SELECT ? AS [order_id],? AS [product_id],? AS [quantity],? AS [price] ) t2 ON (t1.[order_id] = t2.[order_id] AND t1.[product_id] = t2.[product_id]) WHEN MATCHED THEN UPDATE SET t1.[quantity] = t2.[quantity],t1.[price] = t2.[price] WHEN NOT MATCHED THEN INSERT ([order_id],[product_id],[quantity],[price]) VALUES (t2.[order_id],t2.[product_id],t2.[quantity],t2.[price]);
-- params: [1, 6, 3, 29.90]
```

```sql
-- Oracle
MERGE INTO "t_order_item" t1 USING (SELECT ? AS "order_id",? AS "product_id",? AS "quantity",? AS "price" FROM DUAL ) t2 ON (t1."order_id" = t2."order_id" AND t1."product_id" = t2."product_id") WHEN MATCHED THEN UPDATE SET t1."quantity" = t2."quantity",t1."price" = t2."price" WHEN NOT MATCHED THEN INSERT ("order_id","product_id","quantity","price") VALUES (t2."order_id",t2."product_id",t2."quantity",t2."price");
-- params: [1, 6, 3, 29.90]
```

```sql
-- 达梦
MERGE INTO "t_order_item" t1 USING (SELECT ? AS "order_id",? AS "product_id",? AS "quantity",? AS "price" FROM DUAL ) t2 ON (t1."order_id" = t2."order_id" AND t1."product_id" = t2."product_id") WHEN MATCHED THEN UPDATE SET t1."quantity" = t2."quantity",t1."price" = t2."price" WHEN NOT MATCHED THEN INSERT ("order_id","product_id","quantity","price") VALUES (t2."order_id",t2."product_id",t2."quantity",t2."price");
-- params: [1, 6, 3, 29.90]
```
<!-- /card -->

## 各方言的要点

- **SQLite**：没有行锁、ROLLUP / CUBE；`PadLeft` / `PadRight` 用 `zeroblob` 拼出填充字符（没有 `LPAD` / `RPAD`）；日期函数写成 `strftime`；`Sql.MaxColumns` 写多参数 `MAX`。
- **MySQL**：`ON DUPLICATE KEY UPDATE` 用 `VALUES(col)`（8.0.18 能用的写法）；忽略冲突可以改成 `ON DUPLICATE KEY UPDATE 主键 = VALUES(主键)`：`options.Use(new MySqlDialect { IgnoreConflictAsKeyUpdate = true }, 数据源)`。
- **PostgreSQL**：`Sql.Pg.Jsonb` / `Array` / `FullText` 专属函数；条件里的 `IndexOf` 用 `STRPOS`。
- **SQL Server**：日期格式化按 Java 写成 `CONVERT` / `SUBSTRING` 段；拼接式 LIKE 先 `CAST(.. AS NVARCHAR(MAX))`；条件当值写成 `CASE WHEN`；`MERGE` 一次一行。
- **Oracle / 达梦**：`ROWNUM` 分页；IN 列表最多 1000 项，设置 `EasyDbOptions.MaxInClauseSize = 1000` 自动拆成 OR；空字符串就是 NULL；`Sql.MaxColumns` 写 `UNION ALL … FROM DUAL`。达梦的参数是 `:p0`（DmProvider）；DATE 只有日期，加年 / 月写 `DATEADD`（`ADD_MONTHS` 返回 DATE，会丢掉时间）。
- **DuckDB**：进程内数据库，最后一个连接关闭时关库，应用里留一个连接不关能快很多；没有 `IDENTITY`，自增列建表时建序列；没有行锁。
- **HANA**：位置参数 `?`；没有递归 CTE；`STRING_AGG` 不能去重；`GREATEST` / `LEAST` 碰到 NULL 返回 NULL；`DECIMAL` 列求平均先转成不定小数位的 `DECIMAL`（不然按列的小数位截断）；还没有 `Sql.Json`；`Sql.Hana.Text(..)` 全文检索（列上要先建 `FULLTEXT INDEX`）。驱动不在 nuget.org，见下面的“HANA 的驱动与实库测试”。
- **Kingbase / GaussDB**：继承 PG，只覆盖 Java 两个模块与 PG 不同的写法（`STRPOS` 式 LIKE、条件当值写 CASE；Kingbase 的 `CONCAT` 两两嵌套、日期加减与相差）。GaussDB 另按 openGauss 7 实测：没有 IDENTITY 列，自增列建成 `SERIAL` / `BIGSERIAL`；没有 `ON CONFLICT`，upsert 写 `ON DUPLICATE KEY UPDATE`（冲突由表上的主键 / 唯一约束决定）；没有 `LOG10`，写单参数 `LOG`。用 Npgsql 连 openGauss 时连接串要加 `No Reset On Close=true`（openGauss 没有连接回池时发的 `DISCARD ALL`），并且只能用 md5 认证（`password_encryption_type` 为 0 或 1、`pg_hba.conf` 写 md5，官方镜像默认如此）：openGauss 的 sha256 认证与 PG 的 SASL 共用消息号，Npgsql 连不上，要用 sha256 时换 openGauss 版的驱动（如 OpenGauss.NET）；openGauss 默认的 A 兼容模式把空字符串当 NULL，建库时用 `DBCOMPATIBILITY 'PG'` 才与 PG 方言一致。
- **ClickHouse**：分析型，没有自增列、upsert、行锁、事务（一批写入逐条生效，`BeginTransaction` 里执行语句报不支持）；更新删除是 `ALTER TABLE … UPDATE / DELETE`，不能带别名、不能走导航；建表 `ENGINE = MergeTree`。驱动用官方的 ClickHouse.Driver，连接串要带三个会话设置：`set_join_use_nulls=1`（LEFT JOIN 没匹配上的列是 NULL）、`set_mutations_sync=2`（更新删除做完再返回）、`set_session_timezone=UTC`（驱动按会话时区解析 DateTime 参数、按 UTC 读回，时区不是 UTC 时差几个小时）。DateTime 参数写成 `{p0:DateTime64(7)}`（驱动推断的类型只到秒）。带组内排序的字符串聚合不支持（`groupArray` 不保证顺序）；不带 ALL 的 `UNION` 的语义取决于服务器的 `union_default_mode`；`EXISTS` 等关联子查询要 25.x 默认开启的新分析器。
- **Db2**：参数写成 `CAST(@p0 AS 类型)`（CASE 的结果、函数参数、带单位的时长里不能有无类型的参数标记）；字符串函数按 `CODEUNITS32`；`LISTAGG` 的分隔符写成字面量；日期格式化的格式串里只能有格式符与分隔符，其余文字单独拼接；树查询的递归部分写成逗号连接；带导航的更新、删除写成按主键的 `MERGE` / `EXISTS`；没有 `NOWAIT`，`Sql.Json` 没有数组长度。中文 Windows 上要设环境变量 `DB2CODEPAGE=1208`（否则连接失败，只报 "no error information available"）；Linux 引用 Net.IBM.Data.Db2-lnx，`LD_LIBRARY_PATH` 指向输出目录的 `clidriver/lib`。
- **TDengine**：时序库，没有 UPDATE、upsert、自增列、行锁、事务、字符串聚合；删除只能按本表的列过滤；日期加减只能写字面量时长；表要有一个 `DateTime` 主键（行的时间戳）；`InCsv`、截到周及以上的 `DateTrunc` 不支持，子查询与窗口函数的能力有限。驱动用 TDengine.Connector 的 WebSocket 连接（`protocol=WebSocket;host=…;port=6041;username=…;password=…;db=库;timezone=UTC`），不用装客户端；值直接写成字面量、不绑定参数（驱动的参数化语句走 stmt 接口，带参数的 DELETE 会让 taosAdapter 崩溃），`DateTime` 按连接的时区解析，连接串带 `timezone=UTC` 时写入与读回都是同样的钟点。

## HANA 的驱动与实库测试

Sap.Data.Hana 随 SAP HANA 客户端发布，不在 nuget.org 上。应用里引用客户端安装目录下 `dotnetcore/v10.0/Sap.Data.Hana.Net.v10.0.dll`（按目标框架选 v6.0 / v8.0 / v10.0），原生库 `libadonetHDB` 在它的上一级目录，驱动按环境变量 `HDBDOTNETCORE` 找它（Windows 的安装程序会设好）：

```csharp
var db = new EasyDb(o => o.UseHana(HanaFactory.Instance.CreateDataSource("Server=host:39041;UserName=..;Password=..;Current Schema=APP")));
```

本仓库的实库测试不引用它，TestKit 在运行时加载：

1. 从 [SAP Development Tools](https://tools.hana.ondemand.com/#hanatools) 下载 HANA Client（Windows x64 是 `hanaclient-<版本>-windows-x64.zip`，下载前要接受 SAP 开发者许可），解压后执行 `hdbinst --batch --path="C:\Program Files\sap\hdbclient"`。装在别处时用环境变量 `EQS_HANA_DRIVER` 指向客户端的 `dotnetcore` 目录（TestKit 依次找 `EQS_HANA_DRIVER`、`HDBDOTNETCORE`、Windows 与 Linux 的默认安装位置；找不到时 HANA 的测试跳过，在 `EQS_REQUIRE_DBS` 里时判失败）。
2. 起库：`bash infra/dev/db.sh --profile hana up -d --build`。镜像启动时接受 SAP 开发者许可（`--agree-to-sap-license`），首次启动要初始化约 8 分钟；`infra/dev/hana/trim.sh` 在启动后关掉 web dispatcher、diserver 与统计服务，容器限 7g（HANA 按它取 90% 作全局内存上限）。
3. 在 `.local/db.env` 里取消 `EQS_HANA_CS` 的注释。测试按 schema 隔离：实库套件用 `EQS_LIVE` / `EQS_LIVE_DS1`，fork 的 HANA 用例每次运行重建 `EQS_FORK`。

CI 的 `hana` 任务做同样的事：用 compose 起镜像，下载固定版本的 Linux 客户端并按版本缓存。

## 自定义方言

方言是普通的类：继承最接近的方言，覆盖差异，用 `options.Use(方言, 数据源)` 配置。覆盖点在 `SqlDialect` 上（`ParameterPlaceholder`、`WritePaging`、`WriteLock`、`RegisterFunctions`、`WriteMerge`、`ColumnType` 等）：

```csharp
public sealed class MyDialect : PostgreSqlDialect
{
    public override string Name => "mydb";
}

var db = new EasyDb(o => o.Use(new MyDialect(), dataSource));
```

## 限制

- 不在列表里的库：H2 没有 .NET 驱动，不支持（Java 的 H2 用例改在 SQLite 上移植）；SQL Server 2008 的 `ROW_NUMBER` 分页不做（已停止支持）；Doris、Neon、PGlite 等 TS 版支持的库没有专门的方言（Neon 用 PostgreSQL 方言即可）。
- 第二档里 Kingbase 没有实库冒烟（没有可用的容器镜像），只比对 SQL 文本。
- 方言专属函数（`Sql.MySql`、`Sql.Pg`、`Sql.Hana`）用错方言时在渲染期报错，不在编译期。

## 对应 Java

| Java 模块 | C# |
|---|---|
| `sql-mysql` | `UseMySql`（PolarDB、MariaDB 同一个方言） |
| `sql-pgsql` | `UsePostgreSql` |
| `sql-mssql`（SQL Server 2008 的 ROW_NUMBER 分页除外） | `UseSqlServer` |
| `sql-oracle` / `sql-dameng` | `UseOracle` / `UseDameng` |
| `sql-sqlite` / `sql-h2` | `UseSqlite` / 不支持 |
| `sql-kingbase-es` / `sql-gauss-db` | `UseKingbase` / `UseGaussDb` |
| `sql-duckdb` / `sql-clickhouse` / `sql-db2` | `UseDuckDb` / `UseClickHouse` / `UseDb2` |
| `sql-tsdb`（时序库） | `UseTDengine` |
| fork 的 HANA 模块 | `UseHana` |
| `SQLKeyword`、`ExpressionFactory` 等方言扩展点 | 继承 `SqlDialect` |
