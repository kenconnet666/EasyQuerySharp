# Java 用例逐条对照

由 `tools/ParityTests` 生成，不要手改。状态来自 C# 测试上的 `[Upstream]` 标注，见 [0003](decisions/0003-testing-and-parity.md)。
对照的是 easy-query 的 `sql-test`（上游）与 `sql-fork-test`（fork，Id 以 `fork/` 开头），共 2404 个用例。
基准冻结在 `tools/ParityTests/java-tests.json`，取自：easy-query fork at an unrecorded commit (sql-test 2315 + sql-fork-test 89), scanned 2026-11-05。

合计：已移植 2011、偏离 193、不适用 141、骨架 7、待做 52。

## 按能力

| 能力 | key | 里程碑 | 用例 | 已移植 | 偏离 | 不适用 | 骨架 | 待做 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 测试基础设施与 Java 专属（代理、注入、Bean 工具） | `misc.infra` | M0 | 1 | 1 | 0 | 0 | 0 | 0 |
| 基本查询（条件、排序、选列、分页） | `query.basic` | M1 | 435 | 361 | 37 | 35 | 0 | 2 |
| 分页结果 | `query.page` | M1 | 22 | 18 | 3 | 0 | 0 | 1 |
| 原生 SQL | `sql.native` | M1 | 50 | 41 | 8 | 1 | 0 | 0 |
| 删除 | `write.delete` | M1 | 25 | 13 | 2 | 10 | 0 | 0 |
| 插入 | `write.insert` | M1 | 50 | 49 | 1 | 0 | 0 | 0 |
| 更新 | `write.update` | M1 | 59 | 46 | 5 | 8 | 0 | 0 |
| CASE WHEN | `expr.case` | M2 | 27 | 20 | 7 | 0 | 0 | 0 |
| 日期函数 | `func.date` | M2 | 101 | 79 | 15 | 7 | 0 | 0 |
| 通用函数（nullOrDefault、greatest、nullif、null-safe） | `func.general` | M2 | 59 | 56 | 3 | 0 | 0 | 0 |
| 字符串聚合（joining） | `func.joining` | M2 | 29 | 27 | 2 | 0 | 0 | 0 |
| JSON 函数 | `func.json` | M2 | 12 | 12 | 0 | 0 | 0 | 0 |
| 数学函数 | `func.math` | M2 | 4 | 1 | 2 | 1 | 0 | 0 |
| 字符串函数 | `func.string` | M2 | 131 | 123 | 4 | 4 | 0 | 0 |
| 行锁 | `lock` | M2 | 28 | 25 | 1 | 2 | 0 | 0 |
| 窗口函数 | `window` | M2 | 44 | 43 | 1 | 0 | 0 | 0 |
| CTE | `query.cte` | M3 | 11 | 8 | 3 | 0 | 0 | 0 |
| 分组与 having（含 rollup / cube） | `query.group` | M3 | 83 | 75 | 6 | 2 | 0 | 0 |
| 显式 join | `query.join` | M3 | 88 | 83 | 4 | 1 | 0 | 0 |
| 无实体的 Map 查询、toMap | `query.map` | M3 | 11 | 10 | 1 | 0 | 0 | 0 |
| 复用查询（cloneQueryable） | `query.reuse` | M3 | 2 | 2 | 0 | 0 | 0 | 0 |
| 流式读取 | `query.stream` | M3 | 9 | 6 | 2 | 1 | 0 | 0 |
| union / union all | `query.union` | M3 | 18 | 14 | 4 | 0 | 0 | 0 |
| 按 DTO 自动带出（selectAutoInclude） | `dto.autoInclude` | M4 | 36 | 34 | 2 | 0 | 0 | 0 |
| 按键填充（fillMany / fillOne） | `fill` | M4 | 4 | 3 | 1 | 0 | 0 | 0 |
| 一对多展开（flatElement / elements） | `nav.flat` | M4 | 56 | 51 | 1 | 3 | 0 | 1 |
| 隐式子查询改分组 join | `nav.groupJoin` | M4 | 67 | 64 | 1 | 2 | 0 | 0 |
| include 带出导航 | `nav.include` | M4 | 37 | 36 | 1 | 0 | 0 | 0 |
| 隐式子查询（any / none / count / sum …） | `nav.subquery` | M4 | 142 | 138 | 3 | 1 | 0 | 0 |
| 树（toTreeList / asTreeCTE） | `tree` | M4 | 25 | 24 | 1 | 0 | 0 | 0 |
| 值转换、枚举、JSON 列 | `column.converter` | M5 | 9 | 5 | 3 | 1 | 0 | 0 |
| 列加密 | `column.encryption` | M5 | 8 | 8 | 0 | 0 | 0 | 0 |
| 拦截器 | `interceptor` | M5 | 3 | 3 | 0 | 0 | 0 | 0 |
| 逻辑删除 | `logicDelete` | M5 | 10 | 10 | 0 | 0 | 0 | 0 |
| 聚合根保存（savable） | `save.aggregate` | M5 | 39 | 30 | 9 | 0 | 0 | 0 |
| 追踪与差异更新 | `tracking` | M5 | 62 | 58 | 4 | 0 | 0 | 0 |
| 乐观锁版本 | `version` | M5 | 13 | 13 | 0 | 0 | 0 | 0 |
| UPDATE / DELETE 里用导航 | `write.join` | M5 | 10 | 10 | 0 | 0 | 0 | 0 |
| 写哪些列、批量执行 | `write.strategy` | M5 | 26 | 25 | 1 | 0 | 0 | 0 |
| upsert | `write.upsert` | M5 | 13 | 12 | 1 | 0 | 0 | 0 |
| ClickHouse 方言 | `dialect.clickhouse` | M6 | 4 | 4 | 0 | 0 | 0 | 0 |
| 达梦方言 | `dialect.dameng` | M6 | 51 | 30 | 20 | 0 | 1 | 0 |
| DuckDB 方言 | `dialect.duckdb` | M6 | 16 | 10 | 6 | 0 | 0 | 0 |
| 方言静态入口与专属函数 | `dialect.functions` | M6 | 25 | 24 | 1 | 0 | 0 | 0 |
| H2 方言 | `dialect.h2` | M6 | 74 | 64 | 3 | 4 | 3 | 0 |
| SAP HANA 方言 | `dialect.hana` | M6 | 18 | 6 | 10 | 0 | 2 | 0 |
| 人大金仓方言 | `dialect.kingbase` | M6 | 2 | 0 | 2 | 0 | 0 | 0 |
| SQL Server 方言 | `dialect.mssql` | M6 | 38 | 37 | 1 | 0 | 0 | 0 |
| SQL Server 2008（ROW_NUMBER 分页） | `dialect.mssql2008` | M6 | 13 | 0 | 0 | 13 | 0 | 0 |
| UPDATE / DELETE … RETURNING | `write.returning` | M6 | 3 | 3 | 0 | 0 | 0 | 0 |
| 建表与迁移（code-first） | `codeFirst` | M7 | 17 | 13 | 3 | 0 | 1 | 0 |
| 按对象筛选排序（whereObject / orderByObject） | `dto.object` | M7 | 101 | 98 | 3 | 0 | 0 | 0 |
| search 扩展 | `search` | M7 | 45 | 0 | 0 | 45 | 0 | 0 |
| 分库分表 | `sharding` | M7 | 48 | 0 | 0 | 0 | 0 | 48 |
| 跨库链接（asTableLink） | `table.link` | M7 | 13 | 11 | 2 | 0 | 0 | 0 |
| 换表名（asTable / 表名映射） | `table.rename` | M7 | 52 | 51 | 1 | 0 | 0 | 0 |
| 换 schema（asSchema） | `table.schema` | M7 | 25 | 23 | 2 | 0 | 0 | 0 |

## 逐条

### 测试基础设施与 Java 专属（代理、注入、Bean 工具）（`misc.infra`，M0）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/ProxySmokeTest.java#implicitJoinAndSubqueryRenderOnTestEntities | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/ProxySmokeTest.cs:7 | `SELECT t.'id',t.'name',t.'age',t.'company_id',t.'attrs',t.'created_at' FROM 't_fork_user' t LEFT JOI…` |

### 基本查询（条件、排序、选列、分页）（`query.basic`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DateTimeFunctionsTest.java#booleanMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:90 | `SELECT t.'deleted' AS 'value1',(NOT (t.'deleted')) AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ?` |
| DateTimeFunctionsTest.java#dateTimeProp2MySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:52 | `SELECT t.'create_time' AS 'value1',YEAR(t.'create_time') AS 'value2',MONTH(t.'create_time') AS 'valu…` |
| DateTimeFunctionsTest.java#dateTimePropMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:52 | `SELECT t.'create_time' AS 'value1',YEAR(t.'create_time') AS 'value2',MONTH(t.'create_time') AS 'valu…` |
| DirectRelationTest.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DirectRelationTest.cs:13 | `SELECT t.'c1',t.'c2',t.'c3',t.'c4',t.'c5' FROM 'direct1' t LEFT JOIN 'direct2' t1 ON t1.'c7' = t.'c1…` |
| DirectRelationTest.java#testJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DirectRelationTest.cs:19 | `SELECT t3.'c18' AS 'value1' FROM 'direct1' t LEFT JOIN 'direct2' t1 ON t1.'c7' = t.'c1' LEFT JOIN 'd…` |
| EntityQueryAggregateTest1.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:59 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:78 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:97 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:116 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:135 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:154 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:173 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:192 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:211 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id'` |
| EntityQueryAggregateTest3.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:59 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:230 | `SELECT SUM(t.'stars') FROM 't_topic' t` |
| EntityQueryAggregateTest3.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:78 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:97 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:116 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:135 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:154 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:173 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:192 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:211 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id'` |
| EntityQueryAggregateTest4.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:11 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:22 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:33 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:44 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:55 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:66 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:77 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:88 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:99 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest7.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:110 | `SELECT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' t LEFT JO…` |
| EntityQueryAggregateTest7.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:205 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:119 | `SELECT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' t LEFT JO…` |
| EntityQueryAggregateTest7.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:128 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:139 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:150 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:161 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:172 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:183 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:194 | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| GeneralFunctionsTest.java#likeOracle | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:68 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?,1,1) > 0` |
| GeneralFunctionsTest.java#likeOracle2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:76 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?,1,1) = 1` |
| GeneralFunctionsTest.java#likeOracle3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:84 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?,1,1) = (LENGTH("…` |
| GeneralFunctionsTest.java#likePgSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:92 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE "type" LIKE CONCAT('%',(?)::TEX…` |
| GeneralFunctionsTest.java#likePgSQL2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:100 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE "type" LIKE CONCAT((?)::TEXT,'%…` |
| GeneralFunctionsTest.java#likePgSQL3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:108 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE "type" LIKE CONCAT('%',(?)::TEX…` |
| GeneralFunctionsTest.java#likeSQLite | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:116 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?) > 0` |
| GeneralFunctionsTest.java#likeSQLite2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:124 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?) = 1` |
| GeneralFunctionsTest.java#likeSQLite3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:132 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?) = (LENGTH("type…` |
| GenericTest2.java#EnumValueTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:35：测的是 Java 测试工程自己的枚举反序列化工具；C# 的枚举按名字 / 数值的转换由 EnumNameConverter 等覆盖 |  |
| GenericTest2.java#EnumValueTest2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:36：同 EnumValueTest1 |  |
| GenericTest2.java#SqlRangeTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:16：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#StringCharSegmentSm4Test | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:31：SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy |  |
| GenericTest2.java#StringCharSegmentSm4Test1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:32：SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy |  |
| GenericTest2.java#StringCharSegmentTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:24：测的是 Java 的 EasyStringUtil 分段工具；C# 的分段是加密策略的内部实现，由往返用例覆盖 |  |
| GenericTest2.java#StringCharSegmentTest10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:110 |  |
| GenericTest2.java#StringCharSegmentTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:85 |  |
| GenericTest2.java#StringCharSegmentTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:85 |  |
| GenericTest2.java#StringCharSegmentTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:102 |  |
| GenericTest2.java#StringCharSegmentTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:102 |  |
| GenericTest2.java#StringCharSegmentTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:110 |  |
| GenericTest2.java#StringCharSegmentTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:93 |  |
| GenericTest2.java#StringCharSegmentTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:110 |  |
| GenericTest2.java#aesTest2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:18：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#base64Test | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:17：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#behavior | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:19：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#check | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:25：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#createTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:23：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#easyEncryptionTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:55 |  |
| GenericTest2.java#easyEncryptionTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:64 |  |
| GenericTest2.java#executeMethodBehaviorTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:20：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#linkedCaseInsensitiveMapTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:21：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericTest2.java#nameConversionTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:40 |  |
| GenericTest2.java#sm4StandardVectorTest | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:29：SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy |  |
| GenericTest2.java#test1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:26：测的是 Java 的 Introspector 反射 |  |
| GenericTest2.java#testAgroalGetUrl | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:28：测的是 Agroal 数据源 |  |
| GenericTest2.java#testLinkedCaseInsensitiveMap1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:21：测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API |  |
| GenericWithDatabaseTest.java#queryLargeColumnTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:18 | `SELECT 'id','name','content' FROM 'query_large_column_test'` |
| GenericWithDatabaseTest.java#repeatApply1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:157：只有拦截器与自定义逻辑删除策略按名注册；加密与主键生成策略在特性上按类型引用，不存在重复注册 |  |
| GenericWithDatabaseTest.java#testPrintSQL1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:166：测的是 Java 表达式上下文（ExpressionContext）的克隆细节；C# 的查询状态是不可变 record，派生查询天然保留全部设置 |  |
| GenericWithDatabaseTest.java#testPrintSQL2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:168：测的是 Java 表达式上下文（ExpressionContext）的克隆细节；C# 的查询状态是不可变 record，派生查询天然保留全部设置 |  |
| GenericWithDatabaseTest.java#testQueryLockClonePropagation | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:170：测的是 Java 表达式上下文（ExpressionContext）的克隆细节；C# 的查询状态是不可变 record，派生查询天然保留全部设置 |  |
| GenericWithDatabaseTest.java#testQueryLockExtendFromPropagation | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:172：测的是 Java 表达式上下文（ExpressionContext）的克隆细节；C# 的查询状态是不可变 record，派生查询天然保留全部设置 |  |
| InjectTest.java#injectTest1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/InjectTest.cs:11：测的是 Java 自带的依赖注入容器；C# 用 Microsoft.Extensions.DependencyInjection，没有对应的内部容器 |  |
| InjectTest.java#injectTest2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/InjectTest.cs:13：测的是 Java 自带的依赖注入容器；C# 用 Microsoft.Extensions.DependencyInjection，没有对应的内部容器 |  |
| InjectTest.java#injectTest3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/InjectTest.cs:15：测的是 Java 自带的依赖注入容器；C# 用 Microsoft.Extensions.DependencyInjection，没有对应的内部容器 |  |
| InsertTest1.java#testColumnConfigure | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest1.cs:42：Java 用例体是空的 |  |
| MyTest1.java#testDraft3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:59 | `SELECT date_add(t.'create_time', interval (?) day) AS 'value1' FROM 't_blog' t WHERE t.'deleted' = ?…` |
| MyTest1.java#testSelect1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:152 | `SELECT ? AS 'title',t.'title' AS 'id' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest1.java#testSelect10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:254 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(*) FROM 't_blog' t1 WHERE t1.'delete…` |
| MyTest1.java#testSelect2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:161 | `SELECT t.'title' AS 'id',1+1+t.'stars' AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest1.java#testSelect8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:236 | `SELECT t.'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL AND t.'create_time' <= ?` |
| MyTest2.java#testSelect1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:152 | `SELECT ? AS 'title',t.'title' AS 'id' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest2.java#testSelect10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:254 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(*) FROM 't_blog' t1 WHERE t1.'delete…` |
| MyTest2.java#testSelect2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:161 | `SELECT t.'title' AS 'id',1+1+t.'stars' AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest2.java#testSelect8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:236 | `SELECT t.'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL AND t.'create_time' <= ?` |
| MyTest2.java#testTuple3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:59 | `SELECT date_add(t.'create_time', interval (?) day) AS 'value1' FROM 't_blog' t WHERE t.'deleted' = ?…` |
| NumberFunctionsTest.java#addMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:9：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] + ?) AS [value1],(t.[score] + ?) AS [value2],(t.[score] + ?) AS [value3],(t.[score…` |
| NumberFunctionsTest.java#addMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:9：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' + ?) AS 'value1',(t.'score' + ?) AS 'value2',(t.'score' + ?) AS 'value3',(t.'score…` |
| NumberFunctionsTest.java#addORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:9：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" + ?) AS "value1",(t."score" + ?) AS "value2",(t."score" + ?) AS "value3",(t."score…` |
| NumberFunctionsTest.java#addPGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:9：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" + ?) AS "value1",(t."score" + ?) AS "value2",(t."score" + ?) AS "value3",(t."score…` |
| NumberFunctionsTest.java#divideMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:42：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] / ?) AS [value1],(t.[score] / t.[score]) AS [value2] FROM [t_blog] t WHERE t.[dele…` |
| NumberFunctionsTest.java#divideMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:42：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' / ?) AS 'value1',(t.'score' / t.'score') AS 'value2' FROM 't_blog' t WHERE t.'dele…` |
| NumberFunctionsTest.java#divideORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:42：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" / ?) AS "value1",(t."score" / t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#dividePGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:42：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" / ?) AS "value1",(t."score" / t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#multiplyMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:31：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] * ?) AS [value1],(t.[score] * t.[score]) AS [value2] FROM [t_blog] t WHERE t.[dele…` |
| NumberFunctionsTest.java#multiplyMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:31：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' * ?) AS 'value1',(t.'score' * t.'score') AS 'value2' FROM 't_blog' t WHERE t.'dele…` |
| NumberFunctionsTest.java#multiplyORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:31：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" * ?) AS "value1",(t."score" * t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#multiplyPGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:31：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" * ?) AS "value1",(t."score" * t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#subtractMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:20：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] - ?) AS [value1],(t.[score] - ?) AS [value2],(t.[score] - ?) AS [value3],(t.[score…` |
| NumberFunctionsTest.java#subtractMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:20：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' - ?) AS 'value1',(t.'score' - ?) AS 'value2',(t.'score' - ?) AS 'value3',(t.'score…` |
| NumberFunctionsTest.java#subtractORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:20：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" - ?) AS "value1",(t."score" - ?) AS "value2",(t."score" - ?) AS "value3",(t."score…` |
| NumberFunctionsTest.java#subtractPGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:20：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" - ?) AS "value1",(t."score" - ?) AS "value2",(t."score" - ?) AS "value3",(t."score…` |
| QueryTest.java#query0 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:11 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query0_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:21 | `SELECT x.'id',x.'create_time',x.'username',x.'phone',x.'id_card',x.'address' FROM 'easy-query-test'.…` |
| QueryTest.java#query0_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:31 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:41 | `SELECT x.'id',x.'create_time',x.'update_time',x.'create_by',x.'update_by',x.'deleted',x.'title',x.'c…` |
| QueryTest.java#query100 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:223 |  |
| QueryTest.java#query101 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:230 |  |
| QueryTest.java#query101_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:237 |  |
| QueryTest.java#query102 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:244 |  |
| QueryTest.java#query102_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:258 |  |
| QueryTest.java#query108 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:797 |  |
| QueryTest.java#query2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:51 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query27 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:279 | `SELECT t.'title',t.'content',t.'url',t.'star',t.'publish_time',t.'score',t.'status',t.'order',t.'is_…` |
| QueryTest.java#query3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:62 | `SELECT 1 FROM 't_blog' WHERE 'deleted' = ? AND 'id' = ? LIMIT 1` |
| QueryTest.java#query30 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:286 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query31 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:294 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query37 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:306 | `update t_blog set content='{…}' where id='1'` |
| QueryTest.java#query38 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:322 | `update t_blog set content=? where id=?` |
| QueryTest.java#query39 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:340：Java 的第 4、5 块把 url 选了两次（allFields 再加 url），C# 成员初始化不能给同一成员赋两次值，这两块只选一次 url | `SELECT t.'url' AS 'my_url' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest.java#query4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:73 | `SELECT 1 as id FROM 't_blog' WHERE 'deleted' = ? AND 'id' = ? LIMIT 1` |
| QueryTest.java#query42 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:364 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query45 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:372 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE EXISTS(SELECT 1 FROM 't_blo…` |
| QueryTest.java#query46 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:385 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE NOT EXISTS(SELECT 1 FROM 't…` |
| QueryTest.java#query47 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:396 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query48 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:416 |  |
| QueryTest.java#query49 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:424 |  |
| QueryTest.java#query5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:84 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query51 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:432 | `SELECT DISTINCT t.'id' FROM 't_topic' t WHERE t.'id' IS NOT NULL` |
| QueryTest.java#query58 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:441 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' IN (SELECT t1.'id' F…` |
| QueryTest.java#query59 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:473 |  |
| QueryTest.java#query5_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:449 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query5_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:462 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:99 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query60 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:480 |  |
| QueryTest.java#query72 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:489 | `SELECT t.'score',t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'delet…` |
| QueryTest.java#query73 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:498 | `SELECT t.'score',t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'delet…` |
| QueryTest.java#query74 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:505 | `SELECT t.'score',t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'delet…` |
| QueryTest.java#query75 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:513 | `SELECT t.'order' AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? LIMIT 1` |
| QueryTest.java#query76 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:521 | `SELECT t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'deleted' = ? AN…` |
| QueryTest.java#query8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:138 | `SELECT 'id','stars','title','create_time' FROM 't_topic' LIMIT 1` |
| QueryTest.java#query82 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:529 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query83 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:539 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query83_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:539 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query84 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:547 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query85 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:547 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query86 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:557 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query87 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:557 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query88 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:547 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query88_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:567 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:146 |  |
| QueryTest.java#query90_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:574 |  |
| QueryTest.java#query92 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:578 |  |
| QueryTest.java#query93 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:582 |  |
| QueryTest.java#query94 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:586 |  |
| QueryTest.java#query95 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:590 |  |
| QueryTest.java#query96 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:594 |  |
| QueryTest.java#query97 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:598 |  |
| QueryTest.java#query98 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:602 |  |
| QueryTest10.java#test223 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:12 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'title' <> ? AND ('title' = ? OR 'tit…` |
| QueryTest11.java#testOrder2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:431 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'topic_type' LIKE ?` |
| QueryTest11.java#testOrder3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:441：Java 的 proxy set 同名列也写 AS（t.'id' AS 'id'），C# 成员初始化同名列不写 | `SELECT t.'id' AS 'id',t.'title' AS 'name' FROM 't_topic_type' t WHERE t.'topic_type' LIKE ?` |
| QueryTest11.java#testx10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:167 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' NOT IN (SELECT t1.'i…` |
| QueryTest11.java#testx11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:178 | `SELECT t.'id',t.'stars',t.'title',t.'topic_type',t.'create_time' FROM 't_topic_type' t WHERE t.'topi…` |
| QueryTest11.java#testx12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:189 | `SELECT CAST(t.'topic_type' AS SIGNED) FROM 't_topic_type' t` |
| QueryTest11.java#testx12_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:197 | `SELECT CAST(t.'topic_type' AS SIGNED) FROM 't_topic_type' t LIMIT 1` |
| QueryTest11.java#testx13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:208 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'topic_type' IN (?,…` |
| QueryTest11.java#testx2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:93：Java 先 selectAll 再 set(url) 会把 my_url 写两遍；C# 的成员初始化每个成员只出现一次 | `SELECT t.'order' AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? LIMIT 1` |
| QueryTest11.java#testx22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:298 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest11.java#testx23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:307 | `SELECT t.'id' AS 'value1',t.'url' AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ? AND ? = (SELECT …` |
| QueryTest11.java#testx24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:318 | `SELECT t.'id' AS 'value1',t.'url' AS 'value2',(SELECT COUNT(*) FROM 't_topic' t1 WHERE t1.'id' = t.'…` |
| QueryTest11.java#testx3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:102 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest11.java#testx4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:111 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(*) FROM 't_blog' t1 WHERE t1.'delete…` |
| QueryTest11.java#testx6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:137 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE EXISTS(SELECT 1 FROM 't_blo…` |
| QueryTest11.java#testx7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:147 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE NOT EXISTS(SELECT 1 FROM 't…` |
| QueryTest11.java#testx8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:157 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' IN (SELECT t1.'id' F…` |
| QueryTest12.java#testDoc11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:73 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest12.java#testDoc12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:325 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:333 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:342 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest12.java#testDoc18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:352 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest12.java#testDoc20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:134 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest12.java#testDraft23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:68 |  |
| QueryTest12.java#testFetchSub2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:362 | `SELECT t.'id',t.'class_id',t.'name',t1.'id',t1.'name',t2.'address' FROM 'my_school_student' t LEFT J…` |
| QueryTest12.java#testFetchSub3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:362 | `SELECT t.'id',t.'class_id',t.'name',t1.'id',t1.'name',t2.'address' FROM 'my_school_student' t LEFT J…` |
| QueryTest12.java#testNum4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:103 | `SELECT ((t.'star' * t.'score') + t.'score') FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testNum5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:103 | `SELECT ((t.'star' * t.'score') + ?) FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testOr26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:75 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest13.java#orderTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest13.cs:28 | `SELECT t.'id' AS 'blogId',t.'create_time' AS 'dateTime' FROM 't_blog' t WHERE t.'deleted' = ? AND t.…` |
| QueryTest14.java#test111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:13 |  |
| QueryTest15.java#test1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:45：Java 的学生数子查询里 COUNT 列带 AS 'id'、且多占一个别名编号（selectColumn 建的匿名表）；C# 的计算列写成 Select(s => s.Id).Count()，不写别名、不跳号 | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest15.java#test12234 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:315：Java 自建 Hikari 数据源与客户端配置，没有断言；C# 的连接与配置由 EasyDbOptions 覆盖 |  |
| QueryTest15.java#test1_1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:74：Java 的学生数子查询里 COUNT 列带 AS 'id'、且多占一个别名编号（selectColumn 建的匿名表）；C# 的计算列写成 Select(s => s.Id).Count()，不写别名、不跳号 | `SELECT t.'id',t.'name',(SELECT COUNT(t1.'id') AS 'id' FROM 'school_student' t1 WHERE t1.'class_id' =…` |
| QueryTest15.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:186 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' = ?` |
| QueryTest15.java#testCaseSubAggregate | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:86：Java 的学生数子查询里 COUNT 列带 AS 'id'、且多占一个别名编号（selectColumn 建的匿名表）；C# 的计算列写成 Select(s => s.Id).Count()，不写别名、不跳号 | `SELECT 'id','name' FROM 'school_class'` |
| QueryTest15.java#testColumn1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:196 | `SELECT t.'create_time' FROM 't_topic' t` |
| QueryTest15.java#testColumn2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:204 | `SELECT t.'stars' FROM 't_topic' t LIMIT 1` |
| QueryTest15.java#testSimple | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:64 | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest15.java#testSubqueryAnonymous | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:99：Java 的学生数子查询里 COUNT 列带 AS 'id'、且多占一个别名编号（selectColumn 建的匿名表）；C# 的计算列写成 Select(s => s.Id).Count()，不写别名、不跳号 | `SELECT t3.'a' AS 'a',t3.'b' AS 'b',t3.'sc' AS 'sc' FROM (SELECT t.'id' AS 'a',t.'name' AS 'b',(SELEC…` |
| QueryTest16.java#test14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:20 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN 't_company' …` |
| QueryTest16.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:31 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest17.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:15 | `SELECT t.'id',t.'title' FROM 't_topic' t WHERE t.'title' = ?` |
| QueryTest17.java#testNopQuery | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:160：Java 用例体全部注释掉了 |  |
| QueryTest17.java#testSubFrom | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:174 |  |
| QueryTest17.java#testSubFrom1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:174 |  |
| QueryTest17.java#testxxx9 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:167：测的是 Java 代理管理器创建代理对象 |  |
| QueryTest17.java#xxx | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:168：Java 的几种实体继承写法（MyTopic / MyTopicx / MyTopic4 / MyTopic5）只跑不断言；C# 的实体就是普通类 |  |
| QueryTest18.java#andTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:76 |  |
| QueryTest18.java#autoFalse | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:60 |  |
| QueryTest18.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:18 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' IS NULL` |
| QueryTest18.java#test125 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:77 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:18 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' IS NULL` |
| QueryTest18.java#test8 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:28：Java 代理对象的 __getMappingPath（导航路径字符串），C# 直接写属性路径字符串，没有对应 API |  |
| QueryTest18.java#testDraft | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:185 |  |
| QueryTest18.java#testEnum | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:64 |  |
| QueryTest18.java#testFETCHER | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:35 | `SELECT t.'id',t.'stars' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest18.java#testFinal | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:110 | `SELECT a.'id',a.'create_time',a.'update_time',a.'create_by',a.'update_by',a.'deleted',a.'title',a.'c…` |
| QueryTest18.java#testMaxEnum | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:78 | `SELECT MAX(t.'topic_type') FROM 't_topic_type' t` |
| QueryTest18.java#testPartition1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Windows.cs:32：Java 用例体全部注释掉了 |  |
| QueryTest18.java#testResultSizeLimit1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:178 |  |
| QueryTest18.java#testStarStr | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:145 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','star',CAST('star'…` |
| QueryTest19.java#aaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:229 |  |
| QueryTest19.java#aaa1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:233 | `SELECT 'id','name','age' FROM 'a'.'dbo'.'my_user'` |
| QueryTest19.java#testConcat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:224 |  |
| QueryTest19.java#testTime | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:143 | `SELECT t.'create_time' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest19.java#testTime1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:155 | `SELECT ? FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest2.java#groupTest10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:192 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest10_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:192 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:192 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:193 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:193 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:186 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#query1232 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:14：同 query1231 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest2.java#query13x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:272 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest2.java#query143x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:272 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest2.java#query14x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:272 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest2.java#queryMultiFrom1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:285 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t , 't_topic' t1 WHERE t.'id' = ? A…` |
| QueryTest20.java#testFullSingleAllColumns | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:24 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest20.java#testFullSingleAllColumns1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:34 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testFullSingleAllColumns2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:34 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testFullSingleAllColumns3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:34 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testFullSingleColumns1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:71 | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' = ?` |
| QueryTest20.java#testFullSingleColumns2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:71 | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' = ?` |
| QueryTest20.java#testFullSingleSomeColumns1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:43 | `SELECT t.'id',t.'star',t.'status' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ?` |
| QueryTest20.java#testFullSingleSomeColumns2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:52 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| QueryTest20.java#testFullSingleSomeColumns3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:60：Java 的 Select.of(b.id().as(..)) 同名列也写 AS；C# 成员初始化同名列不写 | `SELECT t.'id' AS 'id',t.'title' AS 'title',t.'star' AS 'star' FROM 't_blog' t WHERE t.'deleted' = ? …` |
| QueryTest20.java#testWhere1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:190 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest20.java#testWhere2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:199 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'title' LIKE ? AND 'stars' > ?` |
| QueryTest21.java#joinOrderTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:131 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#testOrderBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:181 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#testOrderBy3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:131 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest22.java#testAdd | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:258 | `SELECT MAX((t.'star' + ?)) FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest22.java#testBigDecimal | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:249 | `SELECT t.'score' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest23.java#filterOn1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:221 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 …` |
| QueryTest23.java#filterOn2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:231 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t` |
| QueryTest23.java#filterOn5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:256 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 …` |
| QueryTest23.java#testAddMulty | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:38 | `SELECT ((t.'order' + t.'star') / t.'score') AS 'value1' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest23.java#testJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:80 |  |
| QueryTest23.java#testJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:80 |  |
| QueryTest23.java#testJoin4 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:94：Java 替换实体映射规则的服务（PropertyFirstEntityMappingRule），C# 的列名映射由 NameConversion 决定，没有对应的服务替换 |  |
| QueryTest23.java#testLt | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:267 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testOrderSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:127 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testPageDistinct2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:50 | `SELECT DISTINCT t.'code' AS 'value1',t1.'name' AS 'value2' FROM 'doc_bank_card' t LEFT JOIN 'doc_use…` |
| QueryTest23.java#testaaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:76 |  |
| QueryTest24.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:60 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:60 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testEnum | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:203：Java 的 ClassProxy 总写 AS，C# 的 DTO 投影同名列不写 AS | `SELECT t.'topic_type' AS 'topic_type' FROM 't_topic_type' t` |
| QueryTest24.java#testx13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:165 | `SELECT t.'topic_type' FROM 't_topic_type' t LIMIT 1` |
| QueryTest24.java#testx16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:181 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'id' = ?` |
| QueryTest24.java#testx17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:191 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest25.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:17 |  |
| QueryTest25.java#test11a1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' IN (?)` |
| QueryTest25.java#testList | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:132 |  |
| QueryTest25.java#testMap | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:108 | `SELECT t1.'aa' AS 'aa',t1.'bb' AS 'bb' FROM (SELECT t.'id' AS 'aa',t.'star' AS 'bb' FROM 't_blog' t …` |
| QueryTest25.java#testMap1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:108 | `SELECT t1.'aa' AS 'aa',t1.'bb' AS 'bb' FROM (SELECT t.'id' AS 'aa',t.'star' AS 'bb' FROM 't_blog' t …` |
| QueryTest25.java#testOnlyIgnore | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:142 | `SELECT t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest25.java#testOnlyIgnore2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:142 | `SELECT t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest25.java#testOnlyIgnore3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:151 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest25.java#topicIntegerTest | 待做 |  |  |
| QueryTest25.java#tupleTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:78 | `SELECT t.'id' AS 'value1',t.'create_time' AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest26.java#switchCase | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:137：参数类型：Java 的 Map 键是整数 1、2，C# 与字符串列比较时写成字符串 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest26.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:284 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| QueryTest26.java#test22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:292 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| QueryTest26.java#testFindInSet | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:102 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest26.java#testMaxInCaluse | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:212：Java 的 contains 在 Oracle 上写成 LIKE ('%'\|\|TO_CHAR(?)\|\|'%')，C# 的 string.Contains 写成 LIKE ?（值里带好 %） | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE ("id" IN (?,?,?,?,?,?,?,?,?,?) OR "id…` |
| QueryTest26.java#testQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:309 | `SELECT 'id','name',AES_DECRYPT(from_base64('phone'),?) AS 'phone' FROM 'sys_user2' WHERE AES_DECRYPT…` |
| QueryTest26.java#testSetConstant | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:122 |  |
| QueryTest26.java#testaa1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:113 | `SELECT t.'id' AS 'title',t.'title' AS 'id' FROM 't_topic' t WHERE t.'id' IS NOT NULL` |
| QueryTest27.java#selectAllMap | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:204：C# 的 Sql.Columns 以列名作键，不写 AS；没有按属性名作键的模式 | `SELECT t.'id' AS 'id',t.'create_time' AS 'createTime',t.'update_time' AS 'updateTime',t.'create_by' …` |
| QueryTest27.java#staticTestCustomFunction | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:56 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest27.java#staticTestCustomFunction2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:65 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest27.java#staticTestCustomFunction4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:84 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest27.java#testAAA | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:160 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:19 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testDerivedTable1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:244 | `SELECT t1.'score' AS 'score',t1.'status1' AS 'status1',t1.'order' AS 'order',t1.'is_top' AS 'is_top'…` |
| QueryTest27.java#testMap2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:104 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest27.java#testRandom | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:11 | `SELECT 'id','stars','title','create_time' FROM 't_topic' ORDER BY RAND() ASC` |
| QueryTest28.java#test | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:10：C# 投影的属性要在编译期写出，按名字取列用 Sql.Column |  |
| QueryTest28.java#testInterceptor1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:22 | `SELECT 'id','name' FROM 'm8_interceptor' WHERE 1=1` |
| QueryTest28.java#testNoValueConverter1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:52：JSON 参数按 System.Text.Json 的属性声明顺序序列化（name 在前），Java 按字母序（age 在前） | `SELECT 'id','stars','title','title2','topic_type','create_time' FROM 't_topic_type_array' WHERE 'tit…` |
| QueryTest3.java#extendsUserTest | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:173：检查的是 Java 生成的 DataReader 链（反射字段），C# 用表达式编译的物化器，没有对应结构 |  |
| QueryTest3.java#orTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:151 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('title' LIKE ? OR 'id' LIKE ?)` |
| QueryTest3.java#queryBasic1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:8 | `SELECT t.'id' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest3.java#queryBasic2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:16 | `SELECT t.'stars' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest3.java#queryToList1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:106 |  |
| QueryTest3.java#testGenericKey | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:207 | `SELECT 'id','stars','title','create_time' FROM 't_topic' LIMIT 1` |
| QueryTest3.java#testGenericKey1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:211 |  |
| QueryTest3.java#testLambda3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:152 |  |
| QueryTest3.java#testProperty3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:152 |  |
| QueryTest3.java#testSample | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:178 |  |
| QueryTest3.java#testSelfPredicate1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:166 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest4.java#testSQLFunc10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:125 | `SELECT CONCAT(t.'stars',t.'id') AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY t.'create_time…` |
| QueryTest4.java#testSQLFunc3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:91 | `SELECT DATE_FORMAT(t.'create_time','%Y/%m/%d') FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:91 | `SELECT DATE_FORMAT(t.'create_time','%Y-%m-%d') FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:99：Java 断言固定日期 2023-05-25（种子数据写入日）；C# 断言与客户端格式化结果一致 |  |
| QueryTest4.java#testSQLFunc6 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:100：同 testSQLFunc5 |  |
| QueryTest5.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:124 | `select 1` |
| QueryTest7.java#forEachTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:87 |  |
| QueryTest7.java#testBank1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:98 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NOT NULL AND 'id' <> '' AND …` |
| QueryTest7.java#testBank2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:102 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '' OR LTRIM('…` |
| QueryTest7.java#testEmpty1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:106 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NOT NULL AND 'id' <> '')` |
| QueryTest7.java#testEmpty2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:110 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '')` |
| QueryTest7.java#testQuery9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:115 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '' OR LTRIM('…` |
| QueryTest9.java#selectCount1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:17 | `SELECT COUNT(*) FROM 't_topic' WHERE 'id' = ?` |
| QueryTest9.java#testDsl4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:167 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '')` |
| QueryTest9.java#testQuery7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:313 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| QueryTestRelationTest.java#relation11 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:190：Java 的 sqlConversion 在计数子查询里写了 COUNT(t1.'id') AS 'id'；C# 的计算列是表达式，子查询的列不写别名（见 QueryTest15） | `SELECT (SELECT COUNT(t1.'id') AS 'id' FROM 'school_student' t1 WHERE t1.'class_id' = t.'id') AS 'val…` |
| QueryTestRelationTest.java#relationTest0_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:59 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:78 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:59 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:168 | `SELECT t.'id' AS 'value1',t1.'gw' AS 'value2' FROM 'my_sign_up' t LEFT JOIN 'my_com_user' t1 ON (t1.…` |
| QueryTestRelationTest.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:50 | `SELECT 'id','com_id','user_id','gw' FROM 'my_com_user'` |
| QueryTestRelationTest.java#testDirectWhere1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:148 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#testDirectWhere2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:148 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#testDirectWhere3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:159 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| Relation2Test.java#test | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:17 | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'school_student_address' t1…` |
| Relation2Test.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:21 | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'school_student_address' t1…` |
| Relation2Test.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:25 | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'school_student_address' t1…` |
| Relation2Test.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:30 | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'my_school_class' t1 ON t1.…` |
| Relation2Test.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:42 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT SUM(CAST(t1.'name' AS SIGNED)) …` |
| Relation2Test.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:52 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT SUM(CAST(t1.'name' AS DECIMAL(3…` |
| Relation2Test.java#test9_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:52 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT SUM(CAST(t1.'name' AS DECIMAL(3…` |
| RelationTest.java#schoolTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:118 | `SELECT t.'id',t.'class_id',t.'name' FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' =…` |
| RelationTest.java#testSub | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:190 | `SELECT t1.'id',t1.'name' FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_i…` |
| RelationTest.java#testSub1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:199 | `SELECT t1.'name' FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_id' WHERE…` |
| RelationTest.java#testSub2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:207 | `SELECT t2.'id',t2.'class_id',t2.'name' FROM 'school_student' t LEFT JOIN 'school_student_address' t1…` |
| SkipTest.java#testEasyQueryConcurrentException | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/SkipTest.cs:6：测的是 Java 各异常类的构造函数；C# 用 BCL 异常加少数几个库异常，各自的构造在用到它们的用例里覆盖 |  |
| SubJoinTest.java#testSubJoinSumBigDecimal | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:34 | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSumBigDecimal2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:34 | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSumBigDecimalDistinct | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:37 | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinSumBigDecimalDistinct2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:37 | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| UpdateTest2.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest2.cs:21 | `SELECT t."id",t."abc" AS "title" FROM "t_topic" t WHERE t."abc" = ?` |
| doc/DocTest.java#testDoc3x | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:19：Java 用例体全部被注释掉，没有可移植的内容 |  |
| doc/DocTest1.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:14 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| doc/DocTest1.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:25 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| doc/DocTest1.java#testBlog4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:71 | `SELECT t1.'name' AS 'value1',t3.'name' AS 'value2' FROM 'test_self' t LEFT JOIN 'my_user' t1 ON t1.'…` |
| mysql8/CTETest.java#cteTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:56 | `WITH 'm8_user_temp' AS (SELECT t.'id',t.'name' FROM 'm8_user' t WHERE t.'age' IS NULL) SELECT t2.'id…` |
| mysql8/IncludeConditionTest.java#testInclude1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/IncludeConditionTest.cs:26 | `SELECT 'id','name','version' FROM 'my_task'` |
| mysql8/IncludeConditionTest.java#testInclude2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/IncludeConditionTest.cs:53 | `SELECT 'id','name','version' FROM 'my_task' WHERE 'id' = ?` |
| mysql8/M8BankTest.java#dynamicPartition | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:88 |  |
| mysql8/M8BankTest.java#fromSubQuery1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:123 | `SELECT t1.'value1' AS 'value1',t1.'value2' AS 'value2',t1.'value3' AS 'value3' FROM (SELECT t.'name'…` |
| mysql8/M8BankTest.java#includeSubQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:28 | `SELECT t.'name' AS 'vo1',t.'id' AS 'vo2',t.'phone' AS 'vo3',t.'id' AS '__relation__id' FROM 't_sys_u…` |
| mysql8/M8BankTest.java#includeSubQuery3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:45 | `SELECT t.'name' AS 'vo1',t.'id' AS 'vo2',t.'phone' AS 'vo3',t.'id' AS '__relation__id' FROM 't_sys_u…` |
| mysql8/M8BankTest.java#includeSubQuery5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:78 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t` |
| mysql8/M8BankTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:100 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN 't_…` |
| mysql8/M8BankTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:8 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN 't_…` |
| mysql8/M8BankTest.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:17 | `SELECT t1.'name' AS 'value1',t2.'name' AS 'value2',t.'code' AS 'value3' FROM 't_bank_card' t LEFT JO…` |
| mysql8/M8BankTest.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:53 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/M8BankTest.java#testFirst | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:11 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time',t3.'code' AS '__part__column1' FROM 't_sys_…` |
| mysql8/M8BankTest.java#testFirst2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:21 | `SELECT t.'name' AS 'value1',t3.'code' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'id',t1.'…` |
| mysql8/M8BankTest.java#testFirstElementInnerJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:32 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t INNER JOIN (SELECT t1.'…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:115 | `SELECT t.'name' AS 'value1',t3.'code' AS 'value2',t4.'name' AS 'value3' FROM 't_sys_user' t LEFT JOI…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:42 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/M8BankTest.java#whereSubQuery6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:196 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8BankTest.java#whereSubQuery6_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:196 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8BankTest.java#whereSubQuery7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:206 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE ? = (SELECT COUNT…` |
| mysql8/M8Save2Test.java#testInsert | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:55 |  |
| mysql8/M8Save3Test.java#testForeach | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save3Test.cs:180 | `SELECT 'id','name' FROM 'm8_save_a' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testInsert | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:92 |  |
| mysql8/M8Test1.java#testQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:9 | `SELECT t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time',t1.'name' AS '__part__column1' FROM 't_ba…` |
| mysql8/M8Test1.java#testQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:19 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time',t1.'name' AS '__part__column1' FRO…` |
| mysql8/M8Test1.java#userTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:29 | `SELECT 'id','sex','age','user_name' FROM 'm8_user_test' WHERE 'id' >= ? AND ('sex' = ? OR 'sex' = ?)…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:158 | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t1…` |
| mysql8/ManyJoinTest.java#partitionByFirst | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:258 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#partitionByFirst2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:267 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testElement4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:18 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'co…` |
| mysql8/ManyJoinTest.java#testLastCard | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:471 |  |
| mysql8/MySQL8Test2.java#testJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:79 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| mysql8/MySQL8Test2.java#testOrderCaseWhen1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:100 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test2.java#testOrderCaseWhen2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:110 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test2.java#testParentNull | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:120 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| mysql8/MySQL8Test3.java#testQueryFirstLimit3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:232 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test3.java#testQueryFirstLimit4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:242 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test4.java#includeOneTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:268 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testAll4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:217 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (EXISTS(SELECT 1 …` |
| mysql8/MySQL8Test4.java#testAll5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:229 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test4.java#testPartition | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:12 | `SELECT t.'id' AS 'value1',t.'name' AS 'value2',t3.'code' AS 'value3',t3.'type' AS 'value4',t3.'open_…` |
| mysql8/MySQL8Test4.java#testSelect | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:243 | `SELECT t1.'id',t1.'phone' AS 'phone',t1.'age' FROM (SELECT t.'id',t.'name' AS 'phone',t.'age' FROM '…` |
| mysql8/MySQL8Test4.java#testSelect1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:243 | `SELECT t1.'id',t1.'phone' AS 'phone',t1.'age' FROM (SELECT t.'id',t.'name' AS 'phone',t.'age' FROM '…` |
| mysql8/MySQL8Test4.java#testTimeAfterNull1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:107 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user' WHERE 'create_time' > ?` |
| mysql8/MySQL8Test4.java#testTimeBeforeNull1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:126 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user' WHERE 'create_time' < ?` |
| mysql8/MySQL8Test5.java#configurationLogicDelete | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:288 | `SELECT 'id','name','deleted' FROM 'my_config_logic' WHERE 'deleted' = ?` |
| mysql8/MySQL8Test5.java#interceptorTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:191 | `SELECT 'id','name','age','create_time' FROM 'm8_user' WHERE 1=1` |
| mysql8/MySQL8Test5.java#interceptorTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:199 | `SELECT 'id','name','age','create_time' FROM 'm8_user' WHERE 1=1` |
| mysql8/MySQL8Test5.java#interceptorTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:207 | `SELECT 'id','name','age','create_time' FROM 'm8_user' WHERE 1=1` |
| mysql8/MySQL8Test5.java#interceptorTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:215 | `SELECT 'id','name','age','create_time' FROM 'm8_user'` |
| mysql8/MySQL8Test5.java#interceptorTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:223 | `SELECT 'id','name','age','create_time' FROM 'm8_user'` |
| mysql8/MySQL8Test5.java#interceptorTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:231 | `SELECT 'id','name','age','create_time' FROM 'm8_user'` |
| pgsql/NamedEnumTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/NamedEnumTest.cs:28 | `SELECT "id","name","type" FROM "t_named_enum" WHERE "type" = ?` |
| pgsql/QueryTest.java#query1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:9 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:17 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:26 | `SELECT 1 FROM "t_blog" WHERE "deleted" = ? AND "id" = ? LIMIT 1` |
| pgsql/QueryTest.java#query4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:36 | `SELECT 1 as id FROM "t_blog" WHERE "deleted" = ? AND "id" = ? LIMIT 1` |
| pgsql/QueryTest.java#query5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:44 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:58 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:96 | `SELECT CONCAT(t."id",t."id") FROM "t_blog" t WHERE t."deleted" = ? AND t."id" = ?` |
| pgsql/QueryTest.java#query9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:104 | `SELECT CONCAT(t."id",t."title") FROM "t_blog" t WHERE t."deleted" = ? AND t."id" = ?` |
| pgsql/QueryTest19.java#testMapChain | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:34：Java 的 Map 投影写成带 [Column] 的 DTO，SQL 相同；Map 的值是 Object，Java 的比较参数保持 Integer，C# 按 DTO 属性的 decimal 传参 | `SELECT t1."aaa" AS "aaa",t1."bbb" AS "bbb" FROM (SELECT t."score" AS "aaa",t."star" AS "bbb" FROM "t…` |
| pgsql/QueryTest19.java#testMapChain2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:43：Java 的 Map 投影写成带 [Column] 的 DTO，SQL 相同；Map 的值是 Object，Java 的比较参数保持 Integer，C# 按 DTO 属性的 decimal 传参 | `SELECT t1."aaa" AS "aaa",t1."bbb" AS "bbb" FROM (SELECT (t."score" * ?) AS "aaa",t."star" AS "bbb" F…` |
| sqlite/SQLiteTest.java#testQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SQLiteTest.cs:33 |  |
| sqlite/SQLiteTest.java#testQuery1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SQLiteTest.cs:75 |  |

### 分页结果（`query.page`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:765 |  |
| QueryTest.java#query13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:772 |  |
| QueryTest.java#query50 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:784 |  |
| QueryTest11.java#testx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:16 |  |
| QueryTest15.java#aaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:213 |  |
| QueryTest15.java#aaa1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:213 |  |
| QueryTest18.java#test16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:51 | `SELECT COUNT(DISTINCT t.'stars') FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'i…` |
| QueryTest18.java#test17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:61 | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LE…` |
| QueryTest18.java#textDistinct2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:90：同 textDistinct1 | `SELECT DISTINCT COUNT(*) FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.…` |
| QueryTest2.java#query12x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:267 |  |
| QueryTest21.java#searchCountPageTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:48 | `SELECT COUNT(*) FROM 't_topic' WHERE 'title' LIKE ?` |
| QueryTest23.java#filterOn4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:247 | `SELECT COUNT(*) FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 ON t1.'id' = t.'bank_id' AND (NOT (t…` |
| QueryTest23.java#testPageDistinct | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:50 | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'code' AS 'value1',t1.'name' AS 'value2' FROM 'doc_bank_card…` |
| QueryTest23.java#testPageDistinct3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:50 | `SELECT COUNT(DISTINCT t1.'name') FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 ON t1.'id' = t.'uid'…` |
| QueryTest25.java#xxx | 待做 |  |  |
| QueryTest3.java#nativePage1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:228 | `select * from t_topic` |
| QueryTest3.java#nativePage3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:244 | `select * from t_topic where id = ?` |
| QueryTest3.java#nativePage4 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:253：过滤值按 C# 种子数据的 content 调整（Java 依赖被其它用例改过的数据） | `select * from t_blog where id = ?` |
| QueryTest3.java#testGenericKey2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:219 |  |
| doc/DocTest.java#testTopic | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:22：最后一段 Java 用 stars().as(o.stars()) 给同名列强加别名（t.'stars' AS 'stars'）；C# 列与成员同名时不写 AS | `SELECT COUNT(*) FROM 't_topic' WHERE 'title' LIKE ? AND 'create_time' <= ?` |
| mysql8/ManyJoinTest.java#test16GroupJoinCombinePage | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:459 | `SELECT COUNT(*) FROM 't_sys_user' t` |
| mysql8/MySQL8Test3.java#thisConfigureTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:27 | `SELECT COUNT(*) FROM 't_sys_user' t` |

### 原生 SQL（`sql.native`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| MyTest1.java#testGroup20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:340 | `SELECT SUBSTR(t.'title',2,2),IFNULL(t1.'score',?) FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| MyTest1.java#testGroup21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:350 | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN 't_…` |
| MyTest1.java#testGroup22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:361 | `SELECT IFNULL(t.'create_time',?) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN…` |
| MyTest2.java#testGroup20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:340 | `SELECT SUBSTR(t.'title',2,2),IFNULL(t1.'score',?) FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| MyTest2.java#testGroup21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:350 | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN 't_…` |
| MyTest2.java#testGroup22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:361 | `SELECT IFNULL(t.'create_time',?) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN…` |
| QueryNativeSQLTest.java#testNativeSQL1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:825 |  |
| QueryTest.java#query11_2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:813：Java 把列别名写在原生片段里（rank() over(..) as rank1）；C# 的原生值赋给 DTO 成员，别名由成员名生成（AS 'rank1'） | `SELECT t.'id',rank() over(order by t2.'stars' desc) as rank1 FROM 't_blog' t LEFT JOIN (SELECT t1.'i…` |
| QueryTest.java#query11_3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:813：Java 把列别名写在原生片段里（rank() over(..) as rank1）；C# 的原生值赋给 DTO 成员，别名由成员名生成（AS 'rank1'） | `SELECT t.'id',rank() over(order by t2.'stars' desc) as rank1 FROM 't_blog' t LEFT JOIN (SELECT t1.'i…` |
| QueryTest.java#query32 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:6 | `SELECT * FROM 't_blog' t where t.id=?` |
| QueryTest.java#query33 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:10 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query34 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:14 | `SELECT * FROM 't_blog' t where t.id=?` |
| QueryTest.java#query35 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:18 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query36 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:22 | `SELECT * FROM 't_blog' t where t.id=?` |
| QueryTest.java#query7_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:26 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query7_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:26 | `SELECT 'id','create_time' AS createTime,'update_time' AS updateTime,'create_by' AS createBy,'update_…` |
| QueryTest10.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:26 | `SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest11.java#testx1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:52 | `SELECT 1 AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND DATE_FORMAT(t.'create_time','%Y-%m-%d'…` |
| QueryTest12.java#testNum1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:84 | `SELECT SUM(t.'score')-SUM(t.'score') AS 'score' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testNum7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:123 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testSelectPropTypeColumn1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:171 | `SELECT SUM(case t.'score' when ? then 1 else 0 end) AS 'star' FROM 't_blog' t WHERE t.'deleted' = ? …` |
| QueryTest12.java#testSelectPropTypeColumn2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:180 | `SELECT MIN(case t.'score' when ? then 1 else 0 end) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ?…` |
| QueryTest14.java#test1119 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:18 |  |
| QueryTest15.java#testxxx1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:355 |  |
| QueryTest16.java#testxxc | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:41 |  |
| QueryTest17.java#aaaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:182 |  |
| QueryTest17.java#testNativeSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:34 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? ORDER BY RAND()` |
| QueryTest17.java#testNativeSQL1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:46 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? ORDER BY IFNULL('stars',?) D…` |
| QueryTest17.java#testNativeSQL2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:55 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? AND 'stars'!='create_time' O…` |
| QueryTest17.java#testNativeSQL3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:66 | `SELECT RAND() AS 'value1',IFNULL(t.'stars',?) AS 'value2' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest18.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:7 |  |
| QueryTest18.java#testClient | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:157 |  |
| QueryTest2.java#query1231 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:13：标量子查询里 Java 写 MAX(..) AS 'create_time'，C# 的标量投影不写别名 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest2.java#query1231_1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:14：同 query1231 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest2.java#query24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:221 | `SELECT RAND() AS 'id',COUNT(t.'id') AS 'id_count' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' =…` |
| QueryTest21.java#test | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:12：直接读写 Java 的表达式构建器内部结构（SQLEntityAliasSegment、FilterContext），C# 没有对应的公开结构 |  |
| QueryTest22.java#testAggregateFx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:268 | `SELECT sum(if(timestampdiff(DAY, t.'create_time', ?) < ?, 1, 0)) AS 'count' FROM 't_blog' t WHERE t.…` |
| QueryTest25.java#tupleTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:88 | `SELECT t.'id' AS 'value1',t.'create_time' AS 'value2',1 AS 'value3' FROM 't_blog' t WHERE t.'deleted…` |
| QueryTest25.java#tupleTest9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:97 | `SELECT t.'id' AS 'value1',t.'create_time' AS 'value2',IFNULL(t.'title','1') AS 'value3' FROM 't_blog…` |
| QueryTest27.java#updateTable | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:124 |  |
| QueryTest3.java#nativePage5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:262：同 nativePage4 | `select * from t_blog where id = ?` |
| QueryTest3.java#nativePage6 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:262：同 nativePage4 | `select * from t_blog where id = ?` |
| QueryTest3.java#testSelectAs1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:176 | `SELECT 100 - t.'id' AS 'stars' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ?` |
| QueryTest4.java#testSQLFuncExtension2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:74 | `SELECT t.'id' FROM 't_topic' t WHERE t.'id' = ? AND FIND_IN_SET(?,t.'id')` |
| QueryTest5.java#test2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:132：Topic.Alias 在 C# 里不是列（ColumnIgnore），带 alias 的两段改为不含 alias；动态列清单用成员初始化表达 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest8.java#test4x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:10 |  |
| QueryTestRelationTest.java#relation10 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:180：Java 的 sqlConversion 在计数子查询里写了 COUNT(t1.'id') AS 'id'；C# 的计算列是表达式，子查询的列不写别名（见 QueryTest15） | `SELECT (SELECT COUNT(t1.'id') AS 'id' FROM 'school_student' t1 WHERE t1.'class_id' = t.'id') AS 'val…` |
| mysql8/MySQL8Test2.java#testGroupConcat2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:277 | `SELECT t.'id' AS 'value1',GROUP_CONCAT(DISTINCT t.'content' SEPARATOR ?) AS 'value2' FROM 't_blog' t…` |
| mysql8/MySQL8Test4.java#testToResultSet | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:305 |  |
| pgsql/QueryTest19.java#concat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:53 | `select CONCAT('a', null , 'b') as "aa"` |

### 删除（`write.delete`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#delete | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:118：检查的是 Java 删除命令的内部构建器，C# 没有对应的公开状态 |  |
| DeleteTest.java#deleteTest13 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:52：检查的是 Java 表达式上下文里的行为标志位，C# 没有对应的内部状态 |  |
| DeleteTest.java#deleteTest14 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:55：Java 的 deletable(null) 用空集合表达；ToSql 同样返回 null |  |
| DeleteTest.java#deleteTest19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:65 | `UPDATE 't_blog' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| DeleteTest.java#deleteTest20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:79 | `UPDATE 't_blog123abc' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ? AND 'create_time' <= ?` |
| DeleteTest.java#deleteTest21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:90 | `DELETE FROM 't_topic123abc' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:99 | `UPDATE 't_blog123abc' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| DeleteTest.java#deleteTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:65 | `UPDATE 't_blog' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| DeleteTest.java#deletex | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:121 | `DELETE FROM 't_blog' WHERE 'id' = ?` |
| DeleteTest.java#deletex1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:121 | `UPDATE 't_blog' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| GenericWithDatabaseTest.java#deleteTest13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:69 | `DELETE FROM 'query_large_column_test' WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#deleteTest14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:73 | `DELETE FROM 'aaa' WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:87 |  |
| GenericWithDatabaseTest.java#queryLargeColumnTest15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:91 | `DELETE FROM 'query_large_column_test' WHERE 'id' = ?` |
| QueryTest12.java#testDoc23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:144 | `DELETE FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE EXISTS(SELECT 1…` |
| QueryTest26.java#testAAA | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:233：Java 的达梦删除不支持连接（抛 UnsupportedOperationException，用例吞掉异常）；C# 沿用 Oracle 按 ROWID 删除的写法，实库验证在 3.1 冒烟里做 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:20：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:21：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:22：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey4 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:23：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey5 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:24：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey6 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:25：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey7 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:26：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey8 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:27：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| mysql8/MySQL8Test2.java#testDelete | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:89 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |

### 插入（`write.insert`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#deleteTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:18 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:26 | `DELETE FROM 't_topic' WHERE 'title' = ?` |
| DeleteTest.java#deleteTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:34 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:34 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:34 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:45 |  |
| GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:154 | `INSERT INTO 't_topic_auto' ('stars','title') VALUES (?,?)` |
| GenericWithDatabaseTest.java#queryLargeColumnTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:23 |  |
| GenericWithDatabaseTest.java#queryLargeColumnTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:28 | `INSERT INTO 'query_large_column_test' ('id') VALUES (?)` |
| InsertTest.java#incrementTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:132 | `INSERT INTO 'custom_increment' ('id','name','address') VALUES (mysqlNextId(),?,?)` |
| InsertTest.java#incrementTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:132 | `INSERT INTO 'custom_increment' ('id','name','address') VALUES (mysqlNextId(),?,?)` |
| InsertTest.java#insertCustom | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:122 | `INSERT INTO 'xxxxx' ('stars') VALUES (?)` |
| InsertTest.java#insertCustom1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:127 | `INSERT INTO 'xxxxx' ('id','stars') VALUES (sde.next_rowid('sde',?),?)` |
| InsertTest.java#insertSQLConvert1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:17 | `INSERT INTO 'xxxxx' ('id','username','phone','id_card','address','create_time') VALUES (?,?,to_base6…` |
| InsertTest.java#insertSQLConvert2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:23 |  |
| InsertTest.java#insertSQLConvert3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:32 |  |
| InsertTest.java#insertTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:21 |  |
| InsertTest1.java#inertTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest1.cs:45 | `INSERT INTO 't_topic_abc' ('id','stars','title','create_time') VALUES (?,?,?,?)` |
| MyTest1.java#testDraft11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:123 | `SELECT timestampdiff(DAY, t.'create_time', t.'update_time') AS 'value1',timestampdiff(HOUR, t.'creat…` |
| MyTest1.java#testDraft6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:78 | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1',date_ad…` |
| MyTest1.java#testDraft7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:91 | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest1.java#testDraft8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:91 | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest1.java#testDraft9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:107 | `SELECT timestampdiff(DAY, t.'update_time', t.'create_time') AS 'value1',timestampdiff(HOUR, t.'updat…` |
| MyTest2.java#testTuple11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:123 | `SELECT timestampdiff(DAY, t.'create_time', t.'update_time') AS 'value1',timestampdiff(HOUR, t.'creat…` |
| MyTest2.java#testTuple6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:78 | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1',date_ad…` |
| MyTest2.java#testTuple7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:91 | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest2.java#testTuple8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:91 | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest2.java#testTuple9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:107 | `SELECT timestampdiff(DAY, t.'update_time', t.'create_time') AS 'value1',timestampdiff(HOUR, t.'updat…` |
| QueryTest.java#query69 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:89 |  |
| QueryTest.java#query70 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:103 |  |
| QueryTest.java#query70_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:103 |  |
| QueryTest.java#query71 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:115 |  |
| QueryTest15.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:111 | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest15.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:132 | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest18.java#test111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:115 |  |
| QueryTest27.java#testUUId | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:274 |  |
| QueryTest4.java#testJson1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:42 |  |
| QueryTest4.java#testJson2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:52 |  |
| QueryTest4.java#testJson2Jackson | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:52 |  |
| UpperPropertyTest.java#test1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpperPropertyTest.cs:9：Java 的库是宽松模式，MyTopicTestDTO 不写 create_time 也能插入；MySQL 8.4 严格模式下改为比对插入 SQL、读取已有的行 |  |
| UpperPropertyTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpperPropertyTest.cs:46 |  |
| fork/insert/InsertSelectSqlTest.java#defaultTargetIsTheEntityTable | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/InsertSelectSqlTest.cs:15 | `INSERT INTO "t_fork_post" ("id","user_id","title","amount") SELECT eq_src."id",eq_src."user_id",eq_s…` |
| fork/insert/InsertSelectSqlTest.java#hanaUsesQuotedNames | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/InsertSelectSqlTest.cs:21 | `INSERT INTO "t_fork_post_archive" ("id","user_id","title","amount") SELECT eq_src."id",eq_src."user_…` |
| fork/insert/InsertSelectSqlTest.java#mysqlCopiesEntityColumnsPlusExtraColumn | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/InsertSelectSqlTest.cs:6 | `INSERT INTO 't_fork_post_archive' ('id','user_id','title','amount','archived_at') SELECT eq_src.'id'…` |
| fork/live/InsertSelectLiveTest.java#copyMatchingRowsWithArchiveTime | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsLiveTest.cs:41 | `CREATE COLUMN TABLE` |
| mysql8/MySQL8Test5.java#testLikeStartsWith | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:129 | `SELECT 'column1','column2','column3' FROM 'no_key_table' WHERE 'column2' LIKE CONCAT(?,'%') ESCAPE '…` |
| pgsql/QueryTest.java#testDraft9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:119 |  |
| pgsql/QueryTest.java#testDraft9_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:119 |  |
| pgsql/QueryTest18.java#testLikeStartsWith | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest18.cs:13 | `SELECT "column1","column2","column3" FROM "no_key_table" WHERE "column2" LIKE CONCAT((?)::TEXT,'%') …` |
| pgsql/QueryTest19.java#testUUId | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:177 |  |

### 更新（`write.update`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GenericWithDatabaseTest.java#queryLargeColumnTest23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:115 | `UPDATE 'query_large_column_test' SET 'id' = ?,'name' = ?,'content' = ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:42 | `UPDATE 'query_large_column_test' SET 'name' = ?,'content' = ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:121 | `UPDATE 'x_t_blog' SET 'star' = 'star' + ? WHERE 'deleted' = ? AND 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest25 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:121 | `UPDATE 'x_t_blog' SET 'star' = 'star' + ? WHERE 'deleted' = ? AND 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:131 | `UPDATE 'x_t_blog' SET 'score' = 'score' + ? WHERE 'deleted' = ? AND 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest27 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:136 | `UPDATE 'x_t_blog' SET 'star' = 'star' - ? WHERE 'deleted' = ? AND 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest28 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:141 | `UPDATE 'x_t_blog' SET 'star' = 'star' + ? WHERE 'deleted' = ? AND 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest29 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:146 | `UPDATE 't_unknown' SET 'money' = 'money' + ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#updateTest30 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:152 |  |
| QueryTest18.java#test123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:132 |  |
| QueryTest21.java#TestReverse | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:78 | `SELECT COUNT(*) FROM 't_topic' WHERE 'id' IS NOT NULL` |
| QueryTest24.java#test123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:249 |  |
| QueryTest26.java#testQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:319 | `UPDATE 'sys_user_encrypt' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| QueryTest26.java#testQuery3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:329 | `UPDATE 'sys_user2' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| QueryTest26.java#updateConcat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:264 |  |
| QueryTest28.java#testIncrementNullZero | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:72 | `UPDATE 't_topic' SET 'stars' = (IFNULL('stars',?) + ?) WHERE 'id' = ?` |
| TestUpdateFlatAndPredicate.java#testExpressionKey | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:12：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:13：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:14：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey4 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:15：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey5 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:16：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey6 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:17：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey7 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:18：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey8 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/FlatAndPredicateTest.cs:19：测的是 Java 表达式树的内部结构（getFlatAndPredicates 提取主键值），C# 不暴露 SQL 表达式树 |  |
| UpdateTest.java#testUpdateIdNull1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:428：C# 的主键为 null 时按 C# 语义写成 IS NULL，Java 写 'id' = ? 并传 null | `UPDATE 't_topic' SET 'title' = CONCAT(SUBSTR('title',2,10),LOWER('id')) WHERE 'id' = ?` |
| UpdateTest.java#updateEntityWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:472 | `UPDATE 't_blog' SET 'create_time' = ?,'update_time' = ?,'create_by' = ?,'update_by' = ?,'title' = ?,…` |
| UpdateTest.java#updateSet1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:337 | `UPDATE 't_topic' SET 'stars' = ?,'stars' = CAST('title' AS SIGNED),'title' = ?,'title' = DATE_FORMAT…` |
| UpdateTest.java#updateSetFuncTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:406 | `UPDATE 'a123123' SET 'title' = CONCAT(SUBSTR('title',2,10),'id') WHERE 'id' = ?` |
| UpdateTest.java#updateSetFuncTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:406 | `UPDATE 'a123123' SET 'title' = CONCAT('id',SUBSTR('title',2,10)) WHERE 'id' = ?` |
| UpdateTest.java#updateSetFuncTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:407 | `UPDATE 'a123123' SET 'title' = CONCAT(SUBSTR('title',2,10),?) WHERE 'id' = ?` |
| UpdateTest.java#updateSetFuncTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:407 | `UPDATE 'a123123' SET 'title' = CONCAT(SUBSTR('title',2,10),LOWER('id')) WHERE 'id' = ?` |
| UpdateTest.java#updateTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:14 | `UPDATE 't_topic' SET 'stars' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:100 |  |
| UpdateTest.java#updateTest11 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:110：参数文本：C# 枚举按底层整数参数化为 9(Integer)，Java 输出 CLASSER(TopicTypeEnum) | `UPDATE 't_topic_type' SET 'stars' = ? WHERE 'topic_type' = ?` |
| UpdateTest.java#updateTest11_1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:111：同 updateTest11 | `UPDATE 't_topic_type' SET 'stars' = ? WHERE 'topic_type' = ?` |
| UpdateTest.java#updateTest12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:123 |  |
| UpdateTest.java#updateTest12_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:130 | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ? AND 'stars' = ?` |
| UpdateTest.java#updateTest12_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:130 | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ? AND 'stars' = ?` |
| UpdateTest.java#updateTest18_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:171 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+? WHERE 'id' = ?` |
| UpdateTest.java#updateTest19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:176 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest19_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:176 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:34 |  |
| UpdateTest.java#updateTest25 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:54 | `UPDATE 'xxxxx' SET 'username' = ?,'phone' = to_base64(AES_ENCRYPT(?,?)),'id_card' = ?,'address' = ?,…` |
| UpdateTest.java#updateTest25_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:54 | `UPDATE 'xxxxx' SET 'username' = ?,'phone' = to_base64(AES_ENCRYPT(?,?)),'id_card' = ?,'address' = ?,…` |
| UpdateTest.java#updateTest27 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:65 | `UPDATE 'x123' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| UpdateTest.java#updateTest27_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:65 | `UPDATE 'x123' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| UpdateTest.java#updateTest29 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:239 | `UPDATE 't_topic' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest29_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:239 | `UPDATE 't_topic' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:38 |  |
| UpdateTest.java#updateTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:48 |  |
| UpdateTest1.java#updateNoSet | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:67：Java 没有 SET 时只打日志、不执行、返回 0；C# 直接抛出 InvalidOperationException，避免把漏写 Set 的更新静默吞掉 |  |
| UpdateTest1.java#updateTestUpdate | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:87 |  |
| UpdateTest1.java#updateTestUpdate1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:98 | `UPDATE 't_topic' SET 'stars' = ?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest1.java#updateTestUpdate3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:124 | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest2.java#testUpdateNull | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest2.cs:27 | `UPDATE "t_topic" SET "title" = ? WHERE "id" = ?` |
| UpdateTest2.java#testUpdateValueTest | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest2.cs:38：C# 的 == null 按 C# 语义翻译成 IS NULL（Java 的 eq(null) 写成 = ? 并传 null，永远不成立）；值过滤只作用于查询这一点相同 | `UPDATE "t_topic" SET "title" = ? WHERE "stars" = ?` |
| UpdateTest2.java#update1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest2.cs:53 |  |
| mysql8/M8BankTest.java#testaa1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:125 |  |
| mysql8/ManyJoinTest.java#test2GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:288 | `UPDATE 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',(COUNT(?) > 0) AS '__any2__' FR…` |

### CASE WHEN（`expr.case`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest12.java#testSelectPropTypeColumn10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:242 | `SELECT SUM((CASE WHEN t.'id' = ? THEN ? ELSE ? END)) AS 'star',MIN(case t.'score' when ? then 1 else…` |
| QueryTest12.java#testSelectPropTypeColumn11 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:255：参数类型：C# 条件表达式两个分支同类型，ELSE 的 2 是 decimal（Java 是 String） | `SELECT SUM((CASE WHEN t.'id' = ? THEN t.'score' ELSE ? END)) AS 'star' FROM 't_blog' t WHERE t.'dele…` |
| QueryTest12.java#testSelectPropTypeColumn12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:265 | `SELECT SUM((CASE WHEN t.'id' = ? THEN t.'score' ELSE (t.'status' * ?) END)) AS 'star' FROM 't_blog' …` |
| QueryTest12.java#testSelectPropTypeColumn3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:190 | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:191 | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:192 | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:193 | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn7 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:217：参数类型：C# 条件表达式两个分支同类型（都是字符串），Java 是 Integer 与 String | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn8 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:218：同 testSelectPropTypeColumn7 | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn9 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:219：同 testSelectPropTypeColumn7 | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest15.java#testCaseWhenLazy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:219 | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'value1' FROM 't_topic' t` |
| QueryTest15.java#testCaseWhenLazy2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:228 | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title' FROM 't_topic' t` |
| QueryTest15.java#testOrderCase | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:237 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? ORDER BY (CASE WHEN 'stars' …` |
| QueryTest18.java#testUpdate | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:122 | `UPDATE 't_topic' SET 'stars' = (CASE WHEN ('stars' - ?) < ? THEN ? ELSE ('stars' - ?) END) WHERE 'id…` |
| QueryTest21.java#caseWhenTest1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:27：Java 的 proxy set 同名列也写 AS（t.'id' AS 'id'），C# 成员初始化同名列不写 | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title',t.'id' AS 'id' FROM 't_topic' t WHERE …` |
| QueryTest21.java#caseWhenTest2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:37：同 caseWhenTest1 | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title',t.'id' AS 'id' FROM 't_topic' t WHERE …` |
| QueryTest22.java#testaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:281 |  |
| QueryTest23.java#fetcherOrder | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:34 |  |
| QueryTest23.java#testOrderSQL1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:127 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest25.java#testOrderCaseWhen | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:10 |  |
| QueryTest3.java#propertyCaseWhen | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:194 | `SELECT (CASE WHEN t.'title' = ? THEN ? WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title',t.'id' FROM …` |
| QueryTest3.java#testCaseWhen | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:182 | `SELECT (CASE WHEN t.'title' = ? AND t.'stars' >= ? THEN ? WHEN t.'title' = ? THEN ? WHEN t.'title' =…` |
| SubJoinTest.java#testSubJoinCaseWhen1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:54 | `SELECT IFNULL((SELECT SUM(t3.'age') FROM 'sub_join_book' t2 LEFT JOIN 'sub_join_author' t3 ON t3.'id…` |
| SubJoinTest.java#testSubJoinCaseWhen2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:54 | `SELECT IFNULL((SELECT SUM(t3.'age') FROM 'sub_join_book' t2 LEFT JOIN 'sub_join_author' t3 ON t3.'id…` |
| mysql8/MySQL8Test2.java#abc | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:129 |  |
| pgsql/MathQueryTest.java#testPropertyMath | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/MathQueryTest.cs:91：C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal）；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写 | `SELECT t."test_value" AS "test_value",ABS(t."test_value") AS "test_abs",SIGN(t."test_value") AS "tes…` |
| pgsql/QueryTest19.java#testItem | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:197 |  |

### 日期函数（`func.date`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DateTimeFunctionsTest.java#dayOfWeekSunDayIsEndDayMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:41 | `SELECT t.'create_time' AS 'value1',(WEEKDAY(t.'create_time')+1) AS 'value2' FROM 't_blog' t WHERE t.…` |
| DateTimeFunctionsTest.java#dayOfYearMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:30 | `SELECT t.'create_time' AS 'value1',DAYOFYEAR(t.'create_time') AS 'value2' FROM 't_blog' t WHERE t.'d…` |
| DateTimeFunctionsTest.java#durationMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:70 | `SELECT t.'create_time' AS 'value1',timestampdiff(DAY, t.'create_time', ?) AS 'value2',timestampdiff(…` |
| DateTimeFunctionsTest.java#formatMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:10 | `SELECT t.'create_time' AS 'value1',DATE_FORMAT(t.'create_time','%Y年%m-01 %H:%i分%s秒') AS 'value2',DAT…` |
| DateTimeFunctionsTest.java#testConstant | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/DateTimeFunctionsTest.cs:101 | `SELECT NOW() AS 'value1',UTC_TIMESTAMP() AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ?` |
| GenericTest2.java#sqlFormat | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:27：测的是 druid 的 SQL 格式化工具 | `SELECT IFNULL(t2.'__sum2__',0) AS 'value1',IFNULL(t2.'__sum3__',0) AS 'value2' FROM 'm8_province' t …` |
| MyTest1.java#testDraft4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:59 | `SELECT date_add(t.'create_time', interval (?) month) AS 'value1' FROM 't_blog' t WHERE t.'deleted' =…` |
| MyTest1.java#testDraft5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:59 | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1' FROM 't…` |
| MyTest1.java#testExists | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:138 | `SELECT t.'id' AS 'value1',t.'title' AS 'value2',t.'create_time' AS 'value3',(SELECT t2.'title' FROM …` |
| MyTest1.java#testSelect3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:169：Java 对同一个属性 id 赋值两次，生成两个 AS 'id'；C# 的对象初始化器不允许重复成员 | `SELECT t.'title' AS 'id',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'id',SUBSTR(t.'title',2,2) AS 't…` |
| MyTest1.java#testSelect4 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:170：Java 的 selectExpression 产出两个 'title' 列；C# 的投影成员不能重名 | `SELECT t.'id',t.'title',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'title' FROM 't_topic' t WHERE t.…` |
| MyTest2.java#testExists | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:138 | `SELECT t.'id' AS 'value1',t.'title' AS 'value2',t.'create_time' AS 'value3',(SELECT t2.'title' FROM …` |
| MyTest2.java#testSelect3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:169：Java 对同一个属性 id 赋值两次，生成两个 AS 'id'；C# 的对象初始化器不允许重复成员 | `SELECT t.'title' AS 'id',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'id',SUBSTR(t.'title',2,2) AS 't…` |
| MyTest2.java#testSelect4 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:170：Java 的 selectExpression 产出两个 'title' 列；C# 的投影成员不能重名 | `SELECT t.'id',t.'title',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'title' FROM 't_topic' t WHERE t.…` |
| MyTest2.java#testTuple4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:59 | `SELECT date_add(t.'create_time', interval (?) month) AS 'value1' FROM 't_blog' t WHERE t.'deleted' =…` |
| MyTest2.java#testTuple5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:59 | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1' FROM 't…` |
| QueryTest.java#query68 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:98 | `SELECT t.'id',t.'username',t.'phone',t.'id_card',t.'address',IFNULL(t.'create_time',?) AS 'create_ti…` |
| QueryTest.java#query7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:112 |  |
| QueryTest10.java#test222 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:51 |  |
| QueryTest10.java#testDraft12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:58 | `SELECT t.'id' AS 'value1',REPLACE(LOWER(t.'title'),?,?) AS 'value2',UPPER(t.'title') AS 'value3',SUB…` |
| QueryTest10.java#testDraft13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:71 | `SELECT t.'id' AS 'value1',TRIM(LOWER(t.'title')) AS 'value2',UPPER(TRIM(t.'title')) AS 'value3',SUBS…` |
| QueryTest10.java#testDraft14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:84 | `SELECT t.'id' AS 'value1',LTRIM(LOWER(t.'title')) AS 'value2',UPPER(LTRIM(t.'title')) AS 'value3',SU…` |
| QueryTest10.java#testDraft15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:97 | `SELECT t.'id' AS 'value1',RTRIM(LOWER(t.'title')) AS 'value2',UPPER(RTRIM(t.'title')) AS 'value3',SU…` |
| QueryTest10.java#testDraft16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:110 | `SELECT t.'id' AS 'value1',LOWER(t.'title') AS 'value2' FROM 't_topic' t WHERE LTRIM(RTRIM(t.'title')…` |
| QueryTest10.java#testLike1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:182：前两段 Java 把条件之后再 leftJoin 的查询包成派生表（SELECT t2.… FROM (SELECT … WHERE …) t2 LEFT JOIN …）；C# 的条件与连接写在同一层，结果相同 | `SELECT t2.'id',t2.'content',t2.'create_time',t3.'stars' FROM (SELECT t.'id',t.'create_time',t.'updat…` |
| QueryTest10.java#testQuery8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:208 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| QueryTest10.java#testVO1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:239：Java 的 selectAll + selectIgnores + setSubQuery 会同时选出同名的普通列（t.'star'）和子查询列（AS 'star'）；C# 的成员初始化每个属性只有一列 | `SELECT t.'id',t1.'title' AS 'field1',t2.'id' AS 'field2' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t…` |
| QueryTest11.java#testOrder | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:390：Java 的 selectExpression 里格式化列不写别名；C# 的投影列必须对应成员，写成 AS 'create_time' | `SELECT t.'title',t.'id',DATE_FORMAT(t.'create_time','%Y-%m-%d %H:%i:%s') FROM 't_topic' t WHERE t.'t…` |
| QueryTest11.java#testOrder1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:428：同 testOrder；JSON 参数按 System.Text.Json 的属性声明顺序序列化（name 在前），Java 的 Jackson 按字母序（age 在前） | `SELECT t.'title',t.'id',DATE_FORMAT(t.'create_time','%Y-%m-%d %H:%i:%s') FROM 't_topic_type_array' t…` |
| QueryTest11.java#testx15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:231 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'create_time' >= da…` |
| QueryTest11.java#testx16 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:241：Java 的 constant(end).plusMonths(-3) 写成 date_add(?, interval (?) month)；C# 在客户端算出结果，只传一个参数 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE ? <= 'create_time' …` |
| QueryTest11.java#testx17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:252 |  |
| QueryTest11.java#testx18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:261 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE CONCAT('id','title'…` |
| QueryTest11.java#testx21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:285 | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',SUM(t.'star') AS 'value3…` |
| QueryTest12.java#orderTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:14 | `SELECT 'id','stars','title','create_time' FROM 't_topic' ORDER BY 'id' ASC` |
| QueryTest13.java#testMultiSelect | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest13.cs:39：Java 的 as(\"createTime\") 强制写了 AS 'create_time'；C# 列名与别名相同时不写 AS | `SELECT t.'id',t.'content',t.'create_time' AS 'create_time',t1.'address',t1.'id_card' FROM 't_blog' t…` |
| QueryTest14.java#testOracle | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:134 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| QueryTest14.java#testPlus | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:29 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' < date_add(CAST(? AS DA…` |
| QueryTest14.java#testPlus_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:29 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' < date_add(CAST(? AS DA…` |
| QueryTest14.java#testPropertyFunc | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:40 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' < CAST(? AS DATETIME)` |
| QueryTest15.java#tes123txxx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:247 |  |
| QueryTest15.java#test122345 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:256 | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE TO_CHAR(("create_time")::TIMESTAMP,'Y…` |
| QueryTest15.java#test1223456 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:262 | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE strftime('%Y年%m月%d日', "create_time") …` |
| QueryTest15.java#test123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:268 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE DATE_FORMAT('create_time','%Y年%m月%d日'…` |
| QueryTest15.java#testColumn | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:278 | `SELECT t.'create_time' FROM 't_topic' t WHERE t.'id' = ? AND DATE_FORMAT(t.'create_time','%Y-%m') >=…` |
| QueryTest15.java#testFETCHER | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:290 | `SELECT t.'id',t.'create_time',t.'title' FROM 't_topic' t WHERE t.'id' = ? AND DATE_FORMAT(t.'create_…` |
| QueryTest15.java#testa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:302 | `SELECT DATE_FORMAT(t.'create_time','%Y-%m') AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE …` |
| QueryTest17.java#test12234567 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:166：Java 自建 Hikari 数据源与客户端配置，没有断言 | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE TO_CHAR(("create_time")::TIMESTAMP,'Y…` |
| QueryTest18.java#Sum | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:85 |  |
| QueryTest18.java#Sum1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:92 |  |
| QueryTest18.java#easyQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:102 |  |
| QueryTest18.java#easyQueryPrint1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:102 |  |
| QueryTest18.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:7 |  |
| QueryTest18.java#testxx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:140 | `select * from t_topic where id != ?` |
| QueryTest19.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:135 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN 't_user_addr…` |
| QueryTest19.java#testNow | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:177 |  |
| QueryTest2.java#queryTest16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:212 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest20.java#test1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:233：测的是 Java 工具方法 EasyUtil.getQuarterStart 的格式化，与查询无关 |  |
| QueryTest23.java#datetimeformat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:116 | `SELECT t.'create_time' AS 'value1',DATE_FORMAT(t.'create_time','%Y年%m-01 %H:%i分%s秒') AS 'value2' FRO…` |
| QueryTest23.java#formatMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:116 | `SELECT t.'create_time' AS 'value1',DATE_FORMAT(t.'create_time','%Y年%m-01 %H:%i分%s秒') AS 'value2',DAT…` |
| QueryTest26.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:11 |  |
| QueryTest26.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:43 |  |
| QueryTest26.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:61 |  |
| QueryTest26.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:61 |  |
| QueryTest26.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:74 |  |
| QueryTest26.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:74 |  |
| QueryTest26.java#test7 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:88：NULL 值拼接：Java 得到 \"null1\"，C# 得到 \"1\" |  |
| QueryTest3.java#dynamicWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:25 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:157 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' = ?` |
| QueryTest3.java#orTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:157 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' = ?` |
| QueryTest3.java#orTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:162 | `SELECT 'id','stars','title','create_time' FROM 't_topic'` |
| QueryTest3.java#orTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:162 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:169 |  |
| QueryTest3.java#query9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:84 |  |
| QueryTest3.java#query9_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:84 |  |
| QueryTest3.java#query9_x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:84 |  |
| QueryTest3.java#testAndOr1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:204：查询结果依赖库里已有数据，C# 只执行不断言行数 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? AND ('title' LIKE ? OR 'titl…` |
| QueryTest3.java#testOrder1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:181 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest4.java#testSQLFunc12_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:135 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('id',?) = DATE_FORMAT('create_…` |
| QueryTest4.java#testSQLFunc7 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:108：同 testSQLFunc5 | `SELECT DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY…` |
| QueryTest9.java#dslTest6 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:214：Java 的 selectExpression(key1()) 不写别名；C# 分组键投影到属性时写 AS（同 QueryTest2） | `SELECT t.'title' FROM 't_topic' t WHERE t.'create_time' <= IFNULL(t.'create_time',?) AND IFNULL(t.'i…` |
| QueryTest9.java#dslTest7 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:241：Java 空的 MapProxy 渲染 SELECT *，C# 不投影时按实体的列读成字典；map.put(\"id\", id) 在 C# 匿名类型里成员名与列名相同，不写 AS | `SELECT t.'stars',t.'create_time',t.'id' AS 'abc',t.'id' FROM 't_topic' t WHERE DATE_FORMAT(t.'create…` |
| QueryTest9.java#entityQuery1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:105 |  |
| QueryTest9.java#testDsl1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:119：Java 的 selectExpression 里不带别名的格式化列映射不到属性；C# 投影必须写出属性，这里映射到 Alias | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'stars' = ?` |
| QueryTest9.java#testDsl2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:142 | `SELECT COUNT(t.'id') AS 'stars',DATE_FORMAT(t.'create_time','%Y/%m/%d') AS 'title' FROM 't_topic' t …` |
| QueryTest9.java#testDsl3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:156 | `SELECT CAST(IFNULL(t.'stars',?) AS CHAR) AS 'title',IFNULL(t.'id','') AS 'alias' FROM 't_topic' t WH…` |
| QueryTest9.java#testDsl5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:183 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' <= 'create_time'` |
| QueryTest9.java#testQuery5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:276 | `SELECT t.'id' AS 'id',COUNT(*) AS 'star',MAX(t.'id') AS 'title' FROM 't_blog' t WHERE t.'deleted' = …` |
| doc/DocTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:109 | `SELECT t.'id',t.'name',t.'phone',t.'depart_name' FROM 'a222' t WHERE t.'id' = ? AND t.'id' = DATE_FO…` |
| doc/DocTest.java#testLikeOr | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:135 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| doc/DocTest.java#testSubQuerySelect | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:150：Java 先选 allFieldsExclude 的列（含 create_time），再把子查询 AS 'create_time' 追加在后，读取时后者覆盖前者；C# 的成员初始化每个成员只写一次，create_time 直接取子查询 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| doc/DocTest.java#testSubQuerySelect2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:165：Java 先选 allFieldsExclude 的列（含 create_time），再把子查询 AS 'create_time' 追加在后，读取时后者覆盖前者；C# 的成员初始化每个成员只写一次，create_time 直接取子查询 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| fork/functions/DateTruncSqlTest.java#mysqlRepeatsTheColumnForWeeks | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:13 | `SELECT DATE_SUB(CAST(DATE_FORMAT(t.'created_at','%Y-%m-%d 00:00:00') AS DATETIME), INTERVAL WEEKDAY(…` |
| fork/functions/DateTruncSqlTest.java#postgresUsesDateTrunc | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:9 | `SELECT DATE_TRUNC('quarter', t."created_at") AS "value1" FROM "t_fork_user" t` |
| fork/functions/DateTruncSqlTest.java#unsupportedDialectSaysSo | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:19：Java 用 SQL Server 演示；C# 的 SQL Server 用 2022 的 DATETRUNC，内置方言都支持，这里用一个没登记函数的方言演示 |  |
| fork/live/DateTruncLiveTest.java#truncatesToEachUnit | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsLiveTest.cs:58 | `DROP TABLE {…}t_fork_user` |
| mysql8/M8BankTest.java#includeSubQuery4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:61 | `SELECT t.'name' AS 'vo1',t.'id' AS 'vo2',t.'phone' AS 'vo3',t.'id' AS '__relation__id' FROM 't_sys_u…` |
| pgsql/QueryTest.java#query7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:70 |  |
| pgsql/QueryTest.java#test123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:166 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#testDuration | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:356 |  |
| pgsql/QueryTest19.java#datetimeformat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:61 | `SELECT t."create_time" AS "value1",TO_CHAR((t."create_time")::TIMESTAMP,'YYYY年MM-01 HH24:MI分SS秒') AS…` |

### 通用函数（nullOrDefault、greatest、nullif、null-safe）（`func.general`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#equalsWithMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:27 | `SELECT (CASE t.[id] WHEN ? THEN 1 ELSE 0 END) AS [value1] FROM [doc_bank_card] t WHERE (CASE t.[code…` |
| GeneralFunctionsTest.java#equalsWithMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:27 | `SELECT (CASE t.'id' WHEN ? THEN 1 ELSE 0 END) AS 'value1' FROM 'doc_bank_card' t WHERE (CASE t.'code…` |
| GeneralFunctionsTest.java#equalsWithORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:27 | `SELECT (CASE t."id" WHEN ? THEN 1 ELSE 0 END) AS "value1" FROM "doc_bank_card" t WHERE (CASE t."code…` |
| GeneralFunctionsTest.java#equalsWithPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:27 | `SELECT (CASE t."id" WHEN ? THEN 1 ELSE 0 END) AS "value1" FROM "doc_bank_card" t WHERE (CASE t."code…` |
| GeneralFunctionsTest.java#nullOrDefaultMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:57 | `SELECT [id],[uid],[code],[type],[bank_id] FROM [doc_bank_card] WHERE ISNULL([code],?) = ?` |
| GeneralFunctionsTest.java#nullOrDefaultMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:57 | `SELECT 'id','uid','code','type','bank_id' FROM 'doc_bank_card' WHERE IFNULL('code',?) = ?` |
| GeneralFunctionsTest.java#nullOrDefaultORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:57 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE NVL("code",?) = ?` |
| GeneralFunctionsTest.java#nullOrDefaultPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:57 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE COALESCE("code",?) = ?` |
| QueryTest.java#query16_1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:13 | `SELECT COUNT(1) FROM (SELECT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t I…` |
| QueryTest.java#query16_1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:13 | `SELECT COUNT(1) FROM (SELECT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t I…` |
| QueryTest10.java#testDraft9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:284 | `SELECT IFNULL(t.'title',?) AS 'title',t.'id' AS 'id',t.'stars' AS 'stars' FROM 't_topic' t ORDER BY …` |
| QueryTest11.java#testx27 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:360 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE IFNULL('topic_type'…` |
| QueryTest11.java#testx28 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:369 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE IFNULL('topic_type'…` |
| QueryTest17.java#testOptional1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:192 |  |
| QueryTest18.java#rangeTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:24 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:56 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#test15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:13 |  |
| QueryTest18.java#test1_0 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:8 | `SELECT IFNULL(t.'id',?) AS 'id' FROM 't_topic' t WHERE t.'id' IS NULL` |
| QueryTest18.java#test1xa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:28 |  |
| QueryTest2.java#groupTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:178 | `SELECT t.'id' AS 'id',IFNULL(SUM(DISTINCT t.'score'),?) AS 'score',IFNULL(COUNT(DISTINCT t.'order'),…` |
| QueryTest2.java#groupTest5_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:183 |  |
| QueryTest20.java#testFullSingleColumns3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:80 | `SELECT IFNULL(t.'id',?) FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' =…` |
| QueryTest20.java#testFullSingleColumns4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:80 | `SELECT IFNULL(t.'id',?) FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' =…` |
| QueryTest20.java#testMap1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:222 | `SELECT t.'id' AS 'v1',(t1.'star' + ?) AS 'v2',IFNULL(t.'create_time',?) AS 'v3' FROM 't_topic' t LEF…` |
| QueryTest20.java#testMap2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:222 | `SELECT t.'id' AS 'str',(t1.'star' + ?) AS 'integer',IFNULL(t.'create_time',?) AS 'time' FROM 't_topi…` |
| QueryTest21.java#anyColumnOrderByTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:206 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#anyColumnTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:206 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#anyColumnTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:219 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#joinOrderTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:139 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#joinOrderTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:148 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#typeTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:17 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testAdd | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:210 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest24.java#test13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:69 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testContains4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:42 | `SELECT t.'title' AS 'title',IFNULL(t.'content',?) AS 'content' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest27.java#staticTestCustomFunction3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:74 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest27.java#testMap | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:94 | `SELECT t.'create_time' AS 'a1',IFNULL(t.'create_time',?) AS 'a2' FROM 't_topic' t` |
| QueryTest28.java#testQueryNullOrDefault | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:81 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('stars',0) = ?` |
| QueryTest28.java#testQueryNullOrDefault2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:90 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('stars',1) = ?` |
| QueryTest3.java#testProperty3_1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:156：Java 把三个参数写进 IFNULL，C# 嵌套成 IFNULL(a,IFNULL(b,c))；Java 断言查到数据依赖库里已有的旧数据，C# 只执行 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE (IFNULL('id',?) = IFNULL('stars',?) O…` |
| QueryTest4.java#testSQLFunc1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:82 | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:130 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('id',?) = IFNULL('create_time'…` |
| QueryTest4.java#testSQLFunc2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:82 | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc2_1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:86：Java 把三个参数直接写进 IFNULL（MySQL 并不接受）；C# 的 a ?? b ?? c 嵌套成 IFNULL(a,IFNULL(b,c)) | `SELECT IFNULL(t.'id',t.'title',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc2_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:82 | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFuncExtension1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:68 | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ? AND FIND_IN_SET(?,t.'id')` |
| QueryTest5.java#groupTest5_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:147 |  |
| QueryTest5.java#joinTestx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:155 | `SELECT COUNT(t2.'id') FROM (SELECT DISTINCT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FRO…` |
| QueryTest9.java#testQuery6 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:300：Java 两处引用同一个子查询对象，别名都是 t1；C# 每处子查询各自编号（标量子查询多保留一个别名，IN 子查询是 t3）。分组键投影到属性时写 AS | `SELECT t.'id',COUNT(t.'id') AS 'star',MAX(t.'id') AS 'title' FROM 't_blog' t WHERE t.'deleted' = ? A…` |
| fork/functions/NullSafeSqlTest.java#nullIf | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:53 | `SELECT NULLIF(t.'name', ?) AS 'value1' FROM 't_fork_user' t` |
| fork/functions/NullSafeSqlTest.java#nullSafeEqualsBetweenColumns | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:35 | `SELECT t.'id' FROM 't_fork_post' t INNER JOIN 't_fork_user' t1 ON (t1.'id' <=> t.'user_id')` |
| fork/functions/NullSafeSqlTest.java#nullSafeEqualsPerDialect | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:41 | `SELECT t.'id' FROM 't_fork_user' t WHERE (t.'name' <=> ?)` |
| fork/functions/SmallFixesSqlTest.java#jsonArraySize | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:66 | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user' WHERE JSON_LENGTH('att…` |
| fork/functions/SmallFixesSqlTest.java#offsetWindowHasSpaceBeforeOrderBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:75 |  |
| fork/live/NullSafeLiveTest.java#nullMatchesNullAndEmptyBecomesNull | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsLiveTest.cs:96 | `DROP TABLE {…}t_fork_user` |
| mysql8/ManyJoinTest.java#testElement5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:31 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'co…` |
| mysql8/ManyJoinTest.java#testElement6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:41 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'co…` |
| mysql8/ManyJoinTest.java#testElement7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:53 | `SELECT t3.'type' AS 'value1',t3.'code' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'u…` |
| pgsql/QueryTest.java#query10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:112 | `SELECT COALESCE(t."id",?) FROM "t_blog" t WHERE t."deleted" = ? AND t."id" = ?` |
| pgsql/QueryTest.java#testNullDefault | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:332 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |

### 字符串聚合（joining）（`func.joining`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| MyTest1.java#testGroup15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:295 | `SELECT SUBSTR(t.'title',2,2) AS 'id',MAX(t.'title') AS 'title',MIN(CAST(t.'stars' AS DECIMAL(36,18))…` |
| MyTest2.java#testGroup15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:295 | `SELECT SUBSTR(t.'title',2,2) AS 'id',MAX(t.'title') AS 'title',MIN(CAST(t.'stars' AS DECIMAL(36,18))…` |
| QueryTest10.java#testDraft20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:152 | `SELECT SUBSTR(t.'content',1,8) AS 'value1',GROUP_CONCAT(t.'id' SEPARATOR ?) AS 'value2' FROM 't_blog…` |
| QueryTest12.java#testDraft21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:57 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testNum8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:133 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testOrSub1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:143 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest14.java#testMapQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:53 | `SELECT * FROM 't_topic' WHERE 'id' = ?` |
| QueryTest17.java#xxx123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:108 | `SELECT t.'id' AS 'my_code',GROUP_CONCAT(t1.'title' SEPARATOR ?) AS 'my_name' FROM 't_topic' t LEFT J…` |
| QueryTest19.java#testaaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:167 |  |
| QueryTest22.java#testSelectAnyElement2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:209：Java 第二个子查询的别名跳过了 t2（写成 t3），C# 按顺序编号为 t2 | `SELECT (SELECT GROUP_CONCAT(t1.'code' SEPARATOR ?) FROM 'doc_bank_card' t1 WHERE t1.'uid' = t.'id' A…` |
| QueryTest26.java#joiningTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:131 |  |
| StringFunctionsTest.java#joining2MySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:109 | `SELECT t.'code' AS 'value1',GROUP_CONCAT(DISTINCT (CASE WHEN t.'type' = ? THEN t1.'name' ELSE NULL E…` |
| StringFunctionsTest.java#joiningMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:98 | `SELECT t.[code] AS [value1],STRING_AGG(DISTINCT (CASE WHEN t.[type] = ? THEN t1.[name] ELSE NULL END…` |
| StringFunctionsTest.java#joiningMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:98 | `SELECT t.'code' AS 'value1',GROUP_CONCAT(DISTINCT (CASE WHEN t.'type' = ? THEN t1.'name' ELSE NULL E…` |
| StringFunctionsTest.java#joiningORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:98 | `SELECT t."code" AS "value1",LISTAGG(DISTINCT TO_CHAR((CASE WHEN t."type" = ? THEN t1."name" ELSE NUL…` |
| StringFunctionsTest.java#joiningPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:98 | `SELECT t."code" AS "value1",STRING_AGG(DISTINCT ((CASE WHEN t."type" = ? THEN t1."name" ELSE NULL EN…` |
| mysql8/MySQL8Test2.java#testGroupConcat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:266 | `SELECT t.'id' AS 'value1',GROUP_CONCAT(DISTINCT t.'title' SEPARATOR ?) AS 'value2',GROUP_CONCAT(DIST…` |
| mysql8/MySQL8Test5.java#testJoining1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:68 | `SELECT t.'id',t.'name',t.'create_time',t2.'__joining2__' AS '__part__column1' FROM 't_bank' t LEFT J…` |
| mysql8/MySQL8Test5.java#testJoining2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:79 | `SELECT t.'id',t.'name',t.'create_time',t2.'__joining2__' AS '__part__column1' FROM 't_bank' t LEFT J…` |
| mysql8/MySQL8Test5.java#testManyLimit1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:252 | `SELECT t.'id' AS 'value1',t4.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_…` |
| mysql8/MySQL8Test5.java#testManyLimit2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:11 | `SELECT t.'id' AS 'value1',t4.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_…` |
| mysql8/MySQL8Test5.java#testManyLimit4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:21 | `SELECT t.'id' AS 'value1',t2.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'ui…` |
| mysql8/MySQL8Test5.java#testManyLimit7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:57 | `SELECT t.'id' AS 'value1',t2.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'ui…` |
| mysql8/MySQL8Test5.java#testToSelectPageResult2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:105：Java 的 eq(null) 写 = ?（参数 null）；C# 与值为 null 的变量比较按 C# 语义写 IS NULL | `SELECT t1.'id',t1.'name',t1.'create_time',t3.'__joining2__' AS '__part__column1' FROM (SELECT t.'id'…` |
| mysql8/MySQL8Test5.java#testToSelectPageResult3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:117 | `SELECT t1.'id',t1.'name',t1.'create_time',t3.'__joining2__' AS '__part__column1' FROM (SELECT t.'id'…` |
| pgsql/QueryTest19.java#testJoining | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:72 | `SELECT t."title" AS "value1",STRING_AGG((t."content")::TEXT, ?) AS "value2" FROM "t_blog" t WHERE t.…` |
| pgsql/QueryTest19.java#testJoining2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:72 | `SELECT t."title" AS "value1",STRING_AGG((t."content")::TEXT, ?) AS "value2" FROM "t_blog" t WHERE t.…` |
| pgsql/QueryTest19.java#testJoining3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:83 | `SELECT t."id",t."create_time",t."update_time",t."create_by",t."update_by",t."deleted",t."title",t."c…` |
| pgsql/QueryTest19.java#testJoining4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:95 | `SELECT t."id",t."create_time",t."update_time",t."create_by",t."update_by",t."deleted",t."title",t."c…` |

### JSON 函数（`func.json`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mysql8/JsonTest.java#testJsonField | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:28 |  |
| mysql8/JsonTest.java#testJsonField2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:38 | `SELECT 'id','name','extra_json','extra_json_array' FROM 't_test_json' WHERE JSON_UNQUOTE(JSON_EXTRAC…` |
| mysql8/JsonTest.java#testJsonField4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:49 |  |
| mysql8/JsonTest.java#testJsonField5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:58 |  |
| mysql8/JsonTest.java#testJsonField6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:65 |  |
| pgsql/Json2Test.java#testJsonField4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:130 |  |
| pgsql/Json2Test.java#testJsonField5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:139 |  |
| pgsql/Json2Test.java#testJsonField6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:146 |  |
| pgsql/Json2Test.java#testJsonField7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:155 |  |
| pgsql/JsonTest.java#testJsonField4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:107 |  |
| pgsql/JsonTest.java#testJsonField5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:116 |  |
| pgsql/JsonTest.java#testJsonField6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/JsonColumnTest.cs:123 |  |

### 数学函数（`func.math`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest12.java#testNum6 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:113：参数类型：C# 的 decimal 运算里常量 100 是 decimal，Java 是 Integer | `SELECT ((t.'star' * t.'score') + ?) FROM 't_blog' t WHERE t.'deleted' = ? AND FLOOR(t.'score') = ?` |
| QueryTest23.java#testMath | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:162 | `SELECT t.'score' AS 'value1',ABS(t.'score') AS 'value2',SIGN(t.'score') AS 'value3',FLOOR(t.'score')…` |
| QueryTest4.java#testSQLFunc8 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:117：对日期列取 ABS 在 C# 里写不出来（Math.Abs 不接受 DateTime） | `SELECT ABS(t.'create_time') AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY t.'create_time' DE…` |
| QueryTest4.java#testSQLFunc9 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:120：Java 客户端 API 的 fx().round 把位数写成字面量；C# 与 Java 实体 API 的 round(n) 一致，位数参数化 | `SELECT ROUND(t.'stars',1) AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY t.'create_time' DESC` |

### 字符串函数（`func.string`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#testDoubleConcat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:177 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| GenericTest2.java#StringCharSegmentSm4Test2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:33：SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy |  |
| GenericTest2.java#easySm4EncryptionIdCardTest | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:34：SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy |  |
| GenericTest2.java#easySm4EncryptionTest | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/GenericTest2.cs:30：SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy |  |
| InsertTest.java#insertTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:13 |  |
| MathQueryTest.java#testPropertyMath | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/MathQueryTest.cs:55：C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal）；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写 | `SELECT t.'test_value' AS 'test_value',ABS(t.'test_value') AS 'test_abs',SIGN(t.'test_value') AS 'tes…` |
| MathQueryTest.java#testPropertyMath2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/MathQueryTest.cs:55：C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal）；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写 | `SELECT t.'test_value' AS 'test_value',ABS(t.'test_value') AS 'test_abs',SIGN(t.'test_value') AS 'tes…` |
| MyTest1.java#testDraft1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:40 | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest1.java#testDraft1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:40 | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest1.java#testDraft2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:50 | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest1.java#testGroup11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:266 | `SELECT SUBSTR(t.'title',2,2) AS 'id',COUNT(SUBSTR(t.'title',2,2)) AS 'star' FROM 't_topic' t WHERE t…` |
| MyTest1.java#testGroup12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:275 | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest1.java#testGroup13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:275 | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest1.java#testGroup14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:286 | `SELECT t2.'id' AS 'id',t2.'star' AS 'star' FROM (SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 's…` |
| MyTest1.java#testGroup16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:307 | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2)` |
| MyTest1.java#testGroup17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:307 | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2) HAV…` |
| MyTest1.java#testGroup18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:318 | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE t.'title' IS NO…` |
| MyTest1.java#testGroup19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:329 | `SELECT SUBSTR(t.'title',2,2) AS 'value1',IFNULL(t1.'score',?) AS 'value2',COUNT(*) AS 'value3' FROM …` |
| MyTest1.java#testSelect5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:173 | `SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NU…` |
| MyTest1.java#testSelect6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:187 | `SELECT t2.'id' AS 'id',t2.'id_count' AS 'id_count' FROM (SELECT t1.'id' AS 'id',COUNT(*) AS 'id_coun…` |
| MyTest1.java#testSelect6_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:196 | `SELECT t1.'id' AS 'id',COUNT(*) AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) A…` |
| MyTest1.java#testSelect6_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:204 | `SELECT t1.'id' AS 'id',MIN(t1.'id') AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,…` |
| MyTest1.java#testSelect7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:228 | `SELECT t2.'value1' AS 'value1',t2.'value2' AS 'value2' FROM (SELECT t1.'value1' AS 'value1',COUNT(*)…` |
| MyTest1.java#testSelect9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:246 | `SELECT t1.'value1' AS 'value1',COUNT(*) AS 'value2' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.'tit…` |
| MyTest2.java#testGroup11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:266 | `SELECT SUBSTR(t.'title',2,2) AS 'id',COUNT(SUBSTR(t.'title',2,2)) AS 'star' FROM 't_topic' t WHERE t…` |
| MyTest2.java#testGroup12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:275 | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest2.java#testGroup13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:275 | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest2.java#testGroup14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:286 | `SELECT t2.'id' AS 'id',t2.'star' AS 'star' FROM (SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 's…` |
| MyTest2.java#testGroup16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:307 | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2)` |
| MyTest2.java#testGroup17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:307 | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2) HAV…` |
| MyTest2.java#testGroup18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:318 | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE t.'title' IS NO…` |
| MyTest2.java#testGroup19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:329 | `SELECT SUBSTR(t.'title',2,2) AS 'value1',IFNULL(t1.'score',?) AS 'value2',COUNT(*) AS 'value3' FROM …` |
| MyTest2.java#testSelect5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:173 | `SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NU…` |
| MyTest2.java#testSelect6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:187 | `SELECT t2.'id' AS 'id',t2.'id_count' AS 'id_count' FROM (SELECT t1.'id' AS 'id',COUNT(*) AS 'id_coun…` |
| MyTest2.java#testSelect6_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:196 | `SELECT t1.'id' AS 'id',COUNT(*) AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) A…` |
| MyTest2.java#testSelect6_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:204 | `SELECT t1.'id' AS 'id',MIN(t1.'id') AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,…` |
| MyTest2.java#testSelect7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:228 | `SELECT t2.'value1' AS 'value1',t2.'value2' AS 'value2' FROM (SELECT t1.'value1' AS 'value1',COUNT(*)…` |
| MyTest2.java#testSelect9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:246 | `SELECT t1.'value1' AS 'value1',COUNT(*) AS 'value2' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.'tit…` |
| MyTest2.java#testTuple1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:40 | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest2.java#testTuple1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:40 | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest2.java#testTuple2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:50 | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| QueryTest.java#query102_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:251 |  |
| QueryTest.java#query103 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:265 |  |
| QueryTest.java#query104 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:272 |  |
| QueryTest.java#query112 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:695 |  |
| QueryTest.java#query114 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:695 |  |
| QueryTest.java#query115 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:702 |  |
| QueryTest.java#query116 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:709 |  |
| QueryTest.java#query117 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:718 |  |
| QueryTest.java#query118 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:709 |  |
| QueryTest.java#query119 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:718 |  |
| QueryTest.java#query120 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:726 |  |
| QueryTest.java#query89 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:740 |  |
| QueryTest.java#query89_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:744 |  |
| QueryTest.java#query90 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:740 |  |
| QueryTest.java#query91 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:740 |  |
| QueryTest.java#query99 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:748 |  |
| QueryTest10.java#testDraft11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:161 | `SELECT t.'id' AS 'value1',LOWER(t.'title') AS 'value2',UPPER(t.'title') AS 'value3' FROM 't_topic' t…` |
| QueryTest10.java#testDraft17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:120 | `SELECT t.'id' AS 'value1',REPLACE(LOWER(t.'title'),?,?) AS 'value2' FROM 't_topic' t WHERE REPLACE(L…` |
| QueryTest10.java#testDraft18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:131 | `SELECT t.'id' AS 'value1',STRCMP(t.'title',t.'id') AS 'value2' FROM 't_topic' t WHERE STRCMP(t.'titl…` |
| QueryTest10.java#testDraft19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:141 | `SELECT t.'id' AS 'value1',LPAD(t.'title', 9, ?) AS 'value2' FROM 't_topic' t WHERE LPAD(t.'title', 5…` |
| QueryTest11.java#testx14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:219 | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE SUBSTR('id',2,2) = …` |
| QueryTest11.java#testx26 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:344：Java 的 proxy set 同名列也写 AS（t.'id' AS 'id'），C# 成员初始化同名列不写 | `SELECT t.'id' AS 'id',(CASE t.'id' WHEN ? THEN 1 ELSE 0 END) AS 'is_top',(CASE SUBSTR(t.'id',2,2) WH…` |
| QueryTest12.java#testDoc19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:124 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest14.java#testLikeColumn | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:81 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest17.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:78 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest17.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:78 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest17.java#textLikeFunc | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:24 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#testSubString | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:45 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#query112 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:62 |  |
| QueryTest2.java#query114 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:62 |  |
| QueryTest2.java#query115 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:62 |  |
| QueryTest2.java#query116 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:72 |  |
| QueryTest2.java#query117 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:72 |  |
| QueryTest2.java#query118 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:72 |  |
| QueryTest2.java#query119 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:72 |  |
| QueryTest2.java#query120 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:83：Java 的 avgOrDefault(.., Float.class) 返回 float；C# 的平均值只有 double 与 decimal 两种 |  |
| QueryTest23.java#testConcat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:98 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testConcat2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:111 |  |
| QueryTest23.java#testConcat3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:98 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testIndexOf | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:197 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testMathx1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:162 | `SELECT t.'score' AS 'value1',ABS(t.'score') AS 'value2',SIN(t.'score') AS 'value3',FLOOR(t.'score') …` |
| QueryTest23.java#testMathx2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:175 | `SELECT t.'score' AS 'value1',ROUND(t.'score') AS 'value2',SIGN(t.'score') AS 'value3',ROUND(t.'score…` |
| QueryTest23.java#testQueryConcat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:158 |  |
| QueryTest24.java#testContains | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:17 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testJoinParameterCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:224 | `SELECT SUBSTR(t2.'title',2,2) AS 'value1' FROM (SELECT t.'id',t.'stars',t.'title',t.'create_time' FR…` |
| QueryTest24.java#testdraft1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:237 | `SELECT SUBSTR(t2.'title',2,2) AS 'value1' FROM (SELECT t.'id',t.'stars',t.'title',t.'create_time' FR…` |
| QueryTest3.java#query112 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:74 |  |
| QueryTest3.java#query114 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:74 |  |
| QueryTest3.java#query115 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:74 |  |
| QueryTest3.java#query116 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:84 |  |
| QueryTest3.java#query117 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:84 |  |
| QueryTest3.java#query118 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:84 |  |
| QueryTest3.java#query119 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:84 |  |
| QueryTest3.java#query120 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:95 |  |
| RelationTest.java#testSub3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:216 | `SELECT LOWER(t1.'name') FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_id…` |
| RelationTest.java#testSub4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:216 | `SELECT LOWER(t1.'name') FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_id…` |
| SkipTest.java#test11 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Features/SkipTest.cs:9：JVM 上数组与 HashMap 取值的循环计时，没有断言，与本库无关 |  |
| StringFunctionsTest.java#compareToMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:9 | `SELECT (CASE WHEN t.[code] = t.[type] THEN 0 WHEN t.[code] > t.[type] THEN 1 ELSE -1 END) FROM [doc_…` |
| StringFunctionsTest.java#compareToMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:9 | `SELECT STRCMP(t.'code',t.'type') FROM 'doc_bank_card' t WHERE STRCMP(t.'code',?) = ?` |
| StringFunctionsTest.java#compareToORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:9 | `SELECT (CASE WHEN t."code" = t."type" THEN 0 WHEN t."code" > t."type" THEN 1 ELSE -1 END) FROM "doc_…` |
| StringFunctionsTest.java#compareToPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:9 | `SELECT (CASE WHEN t."code" = t."type" THEN 0 WHEN t."code" > t."type" THEN 1 ELSE -1 END) FROM "doc_…` |
| StringFunctionsTest.java#concatMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:20 | `SELECT CONCAT(?,t.[code],?,t.[type]) FROM [doc_bank_card] t WHERE CONCAT(t.[code],?) = ? AND CONCAT(…` |
| StringFunctionsTest.java#concatMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:20 | `SELECT CONCAT(?,t.'code',?,t.'type') FROM 'doc_bank_card' t WHERE CONCAT(t.'code',?) = ? AND CONCAT(…` |
| StringFunctionsTest.java#concatORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:20 | `SELECT ? \|\| t."code" \|\| ? \|\| t."type" FROM "doc_bank_card" t WHERE t."code" \|\| ? = ? AND t."code" \|\|…` |
| StringFunctionsTest.java#concatPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:20 | `SELECT CONCAT(?,t."code",?,t."type") FROM "doc_bank_card" t WHERE CONCAT(t."code",?) = ? AND CONCAT(…` |
| StringFunctionsTest.java#leftRightPadMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:31 | `SELECT [id],[uid],[code],[type],[bank_id] FROM [doc_bank_card] WHERE LPAD([code], 4) = ? AND LPAD([c…` |
| StringFunctionsTest.java#leftRightPadMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:31 | `SELECT 'id','uid','code','type','bank_id' FROM 'doc_bank_card' WHERE LPAD('code', 4) = ? AND LPAD('c…` |
| StringFunctionsTest.java#leftRightPadORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:31 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE LPAD("code", 4, ' ') = ? AND LP…` |
| StringFunctionsTest.java#leftRightPadPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:31 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE LPAD("code", 4) = ? AND LPAD("c…` |
| StringFunctionsTest.java#lengthMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:42 | `SELECT LEN(t.[code]) FROM [doc_bank_card] t WHERE LEN(t.[code]) = ?` |
| StringFunctionsTest.java#lengthMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:42 | `SELECT CHAR_LENGTH(t.'code') FROM 'doc_bank_card' t WHERE CHAR_LENGTH(t.'code') = ?` |
| StringFunctionsTest.java#lengthORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:42 | `SELECT LENGTH(t."code") FROM "doc_bank_card" t WHERE LENGTH(t."code") = ?` |
| StringFunctionsTest.java#lengthPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:42 | `SELECT CHAR_LENGTH(t."code") FROM "doc_bank_card" t WHERE CHAR_LENGTH(t."code") = ?` |
| StringFunctionsTest.java#lowerUpperMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:53 | `SELECT LOWER(t.[id]) AS [value1],UPPER(t.[id]) AS [value2] FROM [doc_bank_card] t` |
| StringFunctionsTest.java#lowerUpperMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:53 | `SELECT LOWER(t.'id') AS 'value1',UPPER(t.'id') AS 'value2' FROM 'doc_bank_card' t` |
| StringFunctionsTest.java#lowerUpperORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:53 | `SELECT LOWER(t."id") AS "value1",UPPER(t."id") AS "value2" FROM "doc_bank_card" t` |
| StringFunctionsTest.java#lowerUpperPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:53 | `SELECT LOWER(t."id") AS "value1",UPPER(t."id") AS "value2" FROM "doc_bank_card" t` |
| StringFunctionsTest.java#replaceMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:64 | `SELECT REPLACE(t.[id],?,?) AS [value1] FROM [doc_bank_card] t WHERE REPLACE(t.[code],?,?) = ?` |
| StringFunctionsTest.java#replaceMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:64 | `SELECT REPLACE(t.'id',?,?) AS 'value1' FROM 'doc_bank_card' t WHERE REPLACE(t.'code',?,?) = ?` |
| StringFunctionsTest.java#replaceORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:64 | `SELECT REPLACE(t."id",?,?) AS "value1" FROM "doc_bank_card" t WHERE REPLACE(t."code",?,?) = ?` |
| StringFunctionsTest.java#replacePGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:64 | `SELECT REPLACE(t."id",?,?) AS "value1" FROM "doc_bank_card" t WHERE REPLACE(t."code",?,?) = ?` |
| StringFunctionsTest.java#subStringMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:75 | `SELECT SUBSTRING(t.[id],1,2) AS [value1],SUBSTRING(t.[id],1,t.[stars]) AS [value2] FROM [t_topic] t` |
| StringFunctionsTest.java#subStringMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:75 | `SELECT SUBSTR(t.'id',1,2) AS 'value1',SUBSTR(t.'id',1,t.'stars') AS 'value2' FROM 't_topic' t` |
| StringFunctionsTest.java#subStringORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:75 | `SELECT SUBSTR(t."id",1,2) AS "value1",SUBSTR(t."id",1,t."stars") AS "value2" FROM "t_topic" t` |
| StringFunctionsTest.java#subStringPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:75 | `SELECT SUBSTR(t."id",1,2) AS "value1",SUBSTR(t."id",1,t."stars") AS "value2" FROM "t_topic" t` |
| StringFunctionsTest.java#trimMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:86 | `SELECT LTRIM(RTRIM(t.[id])) AS [value1] FROM [doc_bank_card] t WHERE LTRIM(RTRIM(t.[code])) = ? AND …` |
| StringFunctionsTest.java#trimMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:86 | `SELECT TRIM(t.'id') AS 'value1' FROM 'doc_bank_card' t WHERE TRIM(t.'code') = ? AND LTRIM(t.'code') …` |
| StringFunctionsTest.java#trimORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:86 | `SELECT TRIM(t."id") AS "value1" FROM "doc_bank_card" t WHERE TRIM(t."code") = ? AND LTRIM(t."code") …` |
| StringFunctionsTest.java#trimPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/StringFunctionsTest.cs:86 | `SELECT TRIM(t."id") AS "value1" FROM "doc_bank_card" t WHERE TRIM(t."code") = ? AND LTRIM(t."code") …` |
| mysql8/ExampleTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ExampleTest.cs:25 |  |

### 行锁（`lock`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| ForUpdateDialectToSQLSmokeTest.java#clickHouseForUpdateThrowsUnsupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:42 |  |
| ForUpdateDialectToSQLSmokeTest.java#damengForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:27 |  |
| ForUpdateDialectToSQLSmokeTest.java#db2ForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:50 |  |
| ForUpdateDialectToSQLSmokeTest.java#duckDBForUpdateThrowsUnsupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDbDialectTest.cs:13 |  |
| ForUpdateDialectToSQLSmokeTest.java#gaussDBForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:21 |  |
| ForUpdateDialectToSQLSmokeTest.java#h2ForUpdateToSQLSmoke | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:58：C# 没有 H2 方言（H2 没有 .NET 驱动，H2 的用例在 SQLite 上跑）；SQLite 没有行锁语法，见 sqliteForUpdateToSQLSmoke |  |
| ForUpdateDialectToSQLSmokeTest.java#kingbaseESForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:20 |  |
| ForUpdateDialectToSQLSmokeTest.java#msSQLForUpdateThrowsUnsupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:35 |  |
| ForUpdateDialectToSQLSmokeTest.java#msSQLRowNumberForUpdateThrowsUnsupported | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:49：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| ForUpdateDialectToSQLSmokeTest.java#mysqlForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:18 |  |
| ForUpdateDialectToSQLSmokeTest.java#oracleForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:26 |  |
| ForUpdateDialectToSQLSmokeTest.java#pgsqlForUpdateToSQLSmoke | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:19 |  |
| ForUpdateDialectToSQLSmokeTest.java#sqliteForUpdateToSQLSmoke | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ForUpdateDialectToSqlSmokeTest.cs:64：SQLite 没有 FOR UPDATE 语法（写事务锁整个库），Java 照样拼出、执行时才报语法错误；C# 渲染时就抛出 NotSupportedException |  |
| fork/live/LockWaitLiveTest.java#noWaitFailsAndSkipLockedSkips | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/LockWaitLiveTest.cs:14 | `DROP TABLE {…}t_fork_post` |
| mysql8/ForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:46 |  |
| mysql8/ForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:43 |  |
| mysql8/ForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:59 |  |
| mysql8/ForUpdateTest.java#testForUpdateDuplicateCallThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:35 |  |
| mysql8/ForUpdateTest.java#testForUpdateJoinNotSupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:27 |  |
| mysql8/ForUpdateTest.java#testForUpdateSqlAppendInTransaction | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:8 |  |
| mysql8/ForUpdateTest.java#testForUpdateWithoutTransactionThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ForUpdateTest.cs:20 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:46 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:43 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:60 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateDuplicateCallThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:35 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateJoinNotSupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:27 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateSqlAppendInTransaction | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:8 |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateWithoutTransactionThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgSqlForUpdateTest.cs:20 |  |

### 窗口函数（`window`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest18.java#testPartitionBy1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Windows.cs:8 | `SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time',t1.'__part__column1' AS '__part__column1' FROM…` |
| QueryTest18.java#testPartitionBy1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Windows.cs:20 | `SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time',t1.'__part__column1' AS '__part__column1' FROM…` |
| QueryTest26.java#testChunk3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:149 |  |
| QueryTest26.java#testChunk4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:149 |  |
| QueryTest26.java#testChunk5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:163 |  |
| QueryTest26.java#testChunk6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:175 |  |
| QueryTest26.java#testChunk7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:175 |  |
| doc/DocTest1.java#testBlog1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:34 | `SELECT t.'id',t.'title',t.'topic',t.'url',t.'star',t.'publish_time',t.'score',t.'status',(ROW_NUMBER…` |
| doc/DocTest1.java#testBlog2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:45 | `SELECT t1.'id',t1.'title',t1.'topic',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status' FRO…` |
| doc/DocTest1.java#testBlog3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:54 | `SELECT t1.'id',t1.'title',t1.'topic',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status',t1.…` |
| fork/functions/WindowSqlTest.java#distributionFunctions | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:127 |  |
| fork/functions/WindowSqlTest.java#framesFollowOrderBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:116 |  |
| fork/functions/WindowSqlTest.java#globalWindowWithoutPartition | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:108 |  |
| fork/functions/WindowSqlTest.java#invalidArgumentsAreRejected | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:136 |  |
| fork/functions/WindowSqlTest.java#partitionedWindowIsUnchanged | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:99 |  |
| fork/live/WindowLiveTest.java#runningTotalRowNumberAndNtile | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsLiveTest.cs:112 | `DROP TABLE {…}t_fork_post` |
| mysql8/ManyJoinTest.java#test111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:484 |  |
| mysql8/MySQL8Test3.java#nextTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:171 | `SELECT t.'type' AS 'value1',LAG(t.'type', 1) OVER (ORDER BY t.'type' ASC,t.'code' DESC) AS 'value2',…` |
| mysql8/MySQL8Test3.java#nextTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:190 | `SELECT t.'type' AS 'value1',LAG(t.'type', 1) OVER (ORDER BY t.'type' ASC,t.'code' DESC) AS 'value2',…` |
| mysql8/MySQL8Test3.java#nextTest2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:206：异常类型与文本不同：Java 是 EasyQueryInvalidOperationException 的英文提示 |  |
| mysql8/MySQL8Test3.java#nextTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:214 | `SELECT t.'type' AS 'value1',LAG(t.'type', 1) OVER (ORDER BY t.'open_time' ASC) AS 'value2' FROM 't_b…` |
| mysql8/MySQL8Test5.java#testOffsetChunk | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:171 |  |
| pgsql/PartitionByTest.java#testPartitionBy16_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PartitionByTest.cs:18 | `SELECT t1."__part__column1" AS "value1",t1."__part__column2" AS "value2",t1."id" AS "value3" FROM (S…` |
| pgsql/PartitionByTest.java#testPartitionBy16_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PartitionByTest.cs:31 | `SELECT t1."__part__column1" AS "value1",t1."__part__column2" AS "value2",t1."id" AS "value3" FROM (S…` |
| pgsql/PartitionByTest.java#testPartitionBy16_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PartitionByTest.cs:43 | `SELECT t1."__part__column1" AS "value1",t1."__part__column2" AS "value2",t1."id" AS "value3" FROM (S…` |
| pgsql/QueryTest.java#testPartitionBy1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:237 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:300 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:304 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:308 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:312 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:316 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:320 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:324 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy16_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:328 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:241 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:245 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:249 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy3_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:264 | `SELECT t1."title",t1."url",t1."__part__column1" AS "rank" FROM (SELECT t."id",t."create_time",t."upd…` |
| pgsql/QueryTest.java#testPartitionBy4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:276 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:280 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:284 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:288 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:292 | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:296 | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |

### CTE（`query.cte`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest19.java#testToWithA43 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:212：同 testToWithAs2 | `WITH 'with_Topic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WHERE …` |
| QueryTest19.java#testToWithAs | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:189 | `WITH 'withTableTopic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WH…` |
| QueryTest19.java#testToWithAs2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:200：Java 两次引用同一个 CTE 时别名都是 t2（SQL 有歧义）；C# 第二次引用编号为 t3 | `WITH 'with_Topic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WHERE …` |
| QueryTest19.java#testToWithAs3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:201：同 testToWithAs2 | `WITH 'with_Topic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WHERE …` |
| QueryTest20.java#testGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:121 | `WITH 'with_TopicGroupTestDTO' AS (SELECT t1.'id' AS 'id',COUNT(t1.'id') AS 'id_count' FROM 't_topic'…` |
| mysql8/CTETest.java#cteJoinTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:20 | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'age' = ?) ,'…` |
| mysql8/CTETest.java#cteJoinTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:32 | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'name' = ? AN…` |
| mysql8/CTETest.java#cteJoinTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:44 | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'age' = ?) ,'…` |
| mysql8/CTETest.java#cteTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:11 | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'age' = ?) SE…` |
| mysql8/M8BankTest.java#testExtraFilter4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:114 | `WITH 'with_Draft2' AS (SELECT t1.'bank_id' AS 'value1',COUNT(*) AS 'value2' FROM 't_bank_card' t1 GR…` |
| pgsql/QueryTest.java#testToWithAs2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest.cs:342 | `WITH "aa" AS (SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."del…` |

### 分组与 having（含 rollup / cube）（`query.group`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EntityQueryAggregateTest5.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:216 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:234 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:252 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:270 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:288 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:306 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:324 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:342 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:360 | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' GROUP BY t.'id',t1…` |
| GeneralFunctionsTest.java#limitOrderTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:160 | `SELECT t1."value1" AS "value1",t1."value2" AS "value2" FROM (SELECT rt1.* FROM (SELECT rt.*, ROWNUM …` |
| GeneralFunctionsTest.java#limitOrderTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:168 | `SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t1."value1" AS "value1",t1."valu…` |
| QueryTest.java#query16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:606 | `SELECT COUNT(1) FROM (SELECT t1.'id' AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN …` |
| QueryTest.java#query16_1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:13 | `SELECT COUNT(1) FROM (SELECT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t I…` |
| QueryTest.java#query19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:625 |  |
| QueryTest.java#query19_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:635 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query19_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:26 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query19_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:26 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:645 | `SELECT t.'id' AS 'id',COUNT(t.'id') AS 'id_count' FROM 't_topic' t WHERE t.'id' = ? GROUP BY t.'id'` |
| QueryTest.java#query21 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:656：Java 的 Select.of(key1(), …) 不写别名而 .as(…) 写别名；C# 成员初始化无法区分这两种写法，分组投影统一写别名 | `SELECT t.'id',COUNT(t.'id') AS 'id_count' FROM 't_topic' t WHERE t.'id' = ? GROUP BY t.'id'` |
| QueryTest.java#query43 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:665 |  |
| QueryTest.java#query44 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:679 | `SELECT t1.'id' AS 'id',t1.'id_count' AS 'id_count' FROM (SELECT t.'id' AS 'id',COUNT(t.'id') AS 'id_…` |
| QueryTest10.java#test2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:303：Java 的 selectExpression(g.key1()) 把分组键按普通列选出，不写 AS；C# 的成员初始化 Id = g.Key 与 Java 的 .as(BlogEntity::getId) 一致，写 AS | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? GROUP BY t.'id' HAVING MAX(t.'id'…` |
| QueryTest10.java#test3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:315：Java 的 selectExpression(g.key1()) 把分组键按普通列选出，不写 AS；C# 的成员初始化 Id = g.Key 与 Java 的 .as(BlogEntity::getId) 一致，写 AS | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? GROUP BY t.'id' HAVING MAX(t.'id'…` |
| QueryTest10.java#test4 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:327：Java 的 selectExpression(g.key1()) 把分组键按普通列选出，不写 AS；C# 的成员初始化 Id = g.Key 与 Java 的 .as(BlogEntity::getId) 一致，写 AS | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? GROUP BY t.'id' HAVING MAX(t.'id'…` |
| QueryTest10.java#test5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:339：Java 的 selectExpression(g.key1()) 把分组键按普通列选出，不写 AS；C# 的成员初始化 Id = g.Key 与 Java 的 .as(BlogEntity::getId) 一致，写 AS | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest10.java#testDraft10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest10.cs:172 | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE t.'title' LIKE ? AND t.'create…` |
| QueryTest11.java#testdraft | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:379 | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest13.java#orderTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest13.cs:13 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM (SELECT t.'id' AS 'blogId',COUNT(*) AS 'b…` |
| QueryTest13.java#orderTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest13.cs:13 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM (SELECT t.'id' AS 'blogId',COUNT(*) AS 'b…` |
| QueryTest15.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:155 | `SELECT CEILING((timestampdiff(DAY, t.'birthday', NOW()) / ?)) AS 'value1',CONCAT(t.'first_name',t.'l…` |
| QueryTest18.java#test1111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:54 |  |
| QueryTest19.java#testUnion1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:237 | `SELECT '未开始'AS task_status UNION ALL SELECT '进行中' UNION ALL SELECT '已完成' UNION ALL SELECT '审核中' UNIO…` |
| QueryTest2.java#groupTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:129 | `SELECT t.'id' AS 'id',COUNT(t.'id') AS 'id_count',SUM(t.'score') AS 'score_sum',MAX(t.'publish_time'…` |
| QueryTest2.java#groupTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:138 | `SELECT t.'id' AS 'id',SUM(t.'score') AS 'score',MAX(t.'publish_time') AS 'publish_time',MIN(t.'order…` |
| QueryTest2.java#groupTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:146 | `SELECT t.'id' AS 'id',SUM(DISTINCT t.'score') AS 'score_sum',COUNT(DISTINCT t.'id') AS 'id_count',AV…` |
| QueryTest2.java#groupTest3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:154：Java 的 Select.of 可以放不映射到属性的列（没有别名）；C# 投影的每一列都有成员名，这里用 Draft 的 value1.. | `SELECT t.'id' AS 'id',SUM(DISTINCT t.'score'),COUNT(DISTINCT t.'order'),AVG(DISTINCT t.'status') FRO…` |
| QueryTest2.java#groupTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:161 | `SELECT t.'id' AS 'id',SUM(DISTINCT t.'score') AS 'score',COUNT(DISTINCT t.'order') AS 'order',AVG(DI…` |
| QueryTest2.java#query122 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:108 | `SELECT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' LEFT J…` |
| QueryTest2.java#query122x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:232 | `SELECT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHERE …` |
| QueryTest2.java#query123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:118 | `SELECT DISTINCT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'i…` |
| QueryTest2.java#query123x | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:252 | `SELECT DISTINCT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'i…` |
| QueryTest20.java#testFullSingleTable | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:12 | `SELECT t1.'value1' AS 'value1',t1.'value2' AS 'value2',t1.'value3' AS 'value3' FROM (SELECT t.'title…` |
| QueryTest20.java#testGroup | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:157 | `SELECT t.'title' AS 'value1',COUNT(*) AS 'value2',MAX(t.'create_time') AS 'value3',SUM(t.'score') AS…` |
| QueryTest20.java#testGroup1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:180 | `SELECT t.'title' AS 'key',COUNT(*) AS 'id_count',MAX(t.'create_time') AS 'create_time_max',SUM(t.'sc…` |
| QueryTest20.java#testGroupJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:112 | `SELECT t.'id' AS 'value1',t3.'id_count' AS 'value2' FROM 't_blog' t LEFT JOIN (SELECT t1.'id' AS 'id…` |
| QueryTest21.java#testWhereGroup | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:102 | `SELECT t.'title' AS 'value1',COUNT((CASE WHEN t.'star' >= ? THEN t.'id' ELSE NULL END)) AS 'value2' …` |
| QueryTest21.java#testWhereGroup1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:106 | `SELECT t.'title' AS 'value1',COUNT((CASE WHEN t.'star' >= ? THEN ? ELSE NULL END)) AS 'value2' FROM …` |
| QueryTest21.java#testWhereGroup2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:110 | `SELECT t.'title' AS 'value1',COUNT(DISTINCT (CASE WHEN t.'star' >= ? THEN ? ELSE NULL END)) AS 'valu…` |
| QueryTest21.java#testWhereGroup3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:115 | `SELECT t.'title' AS 'value1',COUNT(DISTINCT ?) AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest21.java#testWhereGroup4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:110 | `SELECT t.'title' AS 'value1',COUNT(DISTINCT (CASE WHEN t.'star' >= ? THEN ? ELSE NULL END)) AS 'valu…` |
| QueryTest21.java#testWhereGroup5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:120 | `SELECT t.'title' AS 'value1',SUM((CASE WHEN t.'star' >= ? THEN t.'score' ELSE ? END)) AS 'value2' FR…` |
| QueryTest21.java#testWhereGroup6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:124 | `SELECT t.'title' AS 'value1',SUM(DISTINCT (CASE WHEN t.'star' >= ? THEN t.'score' ELSE ? END)) AS 'v…` |
| QueryTest22.java#testManyGroup39 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:78 |  |
| QueryTest23.java#testaaa1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:187 |  |
| QueryTest25.java#tupleTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:37 | `SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest25.java#tupleTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:45 | `SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id',t.'title'` |
| QueryTest25.java#tupleTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:53 | `SELECT t1.'value1' AS 'value1' FROM (SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id') t1 …` |
| QueryTest25.java#tupleTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:61 | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest25.java#tupleTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:69 | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2',SUM(t.'stars') AS 'value3' FROM 't_topic' t GROUP BY …` |
| QueryTest26.java#test | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:239：测的是 Java 的 Linq / Stream 在内存里分组，与查询无关 |  |
| QueryTest26.java#test23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:301 | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |
| QueryTest28.java#testQueryAggregate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:100 | `SELECT COUNT(*) AS 'value1',AVG(t.'stars') AS 'value2' FROM 't_topic' t WHERE t.'title' LIKE CONCAT(…` |
| QueryTest3.java#query122 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:121 | `SELECT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' LEFT J…` |
| QueryTest3.java#query123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:131 | `SELECT DISTINCT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'i…` |
| QueryTest7.java#testQuery10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:132 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:132 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:137 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:141 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:145 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:149 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:153 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:157 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:161 | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest9.java#havingQueryTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:264 | `SELECT t.'id' AS 'id',COUNT(t.'id') AS 'star',MAX(t.'id') AS 'title' FROM 't_blog' t WHERE t.'delete…` |
| fork/functions/GroupingSqlTest.java#mysqlWithRollupAndNoCube | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:160 |  |
| fork/functions/GroupingSqlTest.java#plainGroupByIsUnchanged | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:170 |  |
| fork/functions/GroupingSqlTest.java#postgresRollupAndCube | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:148 | `SELECT t."user_id" AS "value1",t."title" AS "value2",SUM(t."amount") AS "value3",GROUPING(t."title")…` |
| fork/functions/GroupingSqlTest.java#rollupNeedsGroupBy | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsSqlTest.cs:175：Rollup 只定义在 GroupBy 之后的分组查询上，没有分组时写不出来（编译期保证） |  |
| fork/live/GroupingLiveTest.java#rollupAddsGrandTotal | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/FunctionsLiveTest.cs:82 | `DROP TABLE {…}t_fork_post` |
| mysql8/M8BankTest.java#testExtraFilter3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:103 | `SELECT t.'name' AS 'value1',t3.'value2' AS 'value2' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' A…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:54 | `SELECT t.'name' AS 'value1',COUNT((CASE WHEN IFNULL(t2.'__any2__',?) = ? THEN ? ELSE NULL END)) AS '…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:65 | `SELECT t.'name' AS 'value1',COUNT((CASE WHEN t2.'__any2__' = ? THEN ? ELSE NULL END)) AS 'value2' FR…` |
| mysql8/M8Test1.java#test23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:56 | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |

### 显式 join（`query.join`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:153 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest.java#query10_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:183 | `SELECT y.'id',y.'stars',y.'title',y.'create_time' FROM 't_topic' y LEFT JOIN 't_blog' z ON z.'delete…` |
| QueryTest.java#query11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:190 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:216 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest.java#query113 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:702 |  |
| QueryTest.java#query11_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:208 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query121 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.cs:752 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest.java#query17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:45 | `SELECT COUNT(1) FROM (SELECT t1.'create_time',t1.'update_time',t1.'create_by',t1.'update_by',t1.'del…` |
| QueryTest.java#query26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:56 | `SELECT t1.'id',t1.'create_time',t1.'update_time',t1.'create_by',t1.'update_by',t1.'deleted',t1.'titl…` |
| QueryTest.java#query40 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:67 | `SELECT COUNT(*) FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHE…` |
| QueryTest.java#query78 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:81 | `SELECT t1.'title',t1.'content',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status',t1.'order…` |
| QueryTest.java#query79 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Joins.cs:95：Java 允许投影里两列都映射到 id、后者覆盖前者；C# 成员初始化同一成员不能赋值两次 | `SELECT t1.'id',t1.'title',t1.'content',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status',t…` |
| QueryTest.java#query80 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:69：C# 投影的成员名不会重复；重复列名读成字典时报错，用原生 SQL 验证 |  |
| QueryTest11.java#testx9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:35 |  |
| QueryTest12.java#testDoc15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:83 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:156 |  |
| QueryTest12.java#testDraftInteger | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:31 |  |
| QueryTest12.java#testDraftInteger1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:31 |  |
| QueryTest12.java#testJoinSelect | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:39 | `SELECT t1.'id',t1.'create_time',t1.'update_time',t1.'create_by',t1.'update_by',t1.'deleted',t1.'titl…` |
| QueryTest12.java#testLogicDeleteRecentlyTable | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:155 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testLogicDeleteRecentlyTable1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:163 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testMap1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:9 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testMap2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:20 | `SELECT t.'id',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'content',t.'url',…` |
| QueryTest14.java#testLikeColumn2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:99 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest15.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:167 | `SELECT t.'id',t.'first_name',t.'last_name',t.'birthday',CONCAT(t.'first_name',t.'last_name') AS 'ful…` |
| QueryTest15.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:176 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest18.java#test | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:17 |  |
| QueryTest18.java#test124 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:131 | `SELECT t.'id',t.'title',t1.'id' AS 'content',t1.'top' AS 'score' FROM 't_topic' t LEFT JOIN 't_blog'…` |
| QueryTest18.java#test18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:70 |  |
| QueryTest19.java#mapJoinFixed | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:275 | `SELECT t.'id1' AS 'id1',t.'name1' AS 'name1',t1.'id2' AS 'id2',t1.'name2' AS 'name2' FROM 'table1' t…` |
| QueryTest2.java#query113 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:62 |  |
| QueryTest2.java#query121 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:95 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest2.java#query124 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:30 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest2.java#query124_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:30 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest2.java#query125 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:42 | `SELECT t.'id',t1.'title',t2.'star' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.…` |
| QueryTest2.java#query126 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:47 |  |
| QueryTest20.java#testGroupJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:130 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testJoinMultiOn | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:89 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest20.java#testJoinVo | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:148 | `SELECT t.'title' AS 'column1',t1.'title' AS 'column2' FROM 't_blog' t LEFT JOIN 't_topic' t1 ON t.'i…` |
| QueryTest20.java#testMapIgnore | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:20 | `SELECT t.'id',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'content',t.'url',…` |
| QueryTest20.java#testWhere3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest20.cs:212 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest24.java#aaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:50 |  |
| QueryTest25.java#testDraft | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:122 | `SELECT t1.'id',t1.'star',t.'title' AS 'content' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'delete…` |
| QueryTest25.java#testDraft2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:122 | `SELECT t1.'id',t1.'star',t.'title' AS 'content' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'delete…` |
| QueryTest26.java#testRelationOn | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:273 | `SELECT t.'aid',t.'aname' FROM 'on_relation_a' t LEFT JOIN 'on_relation_b' t2 ON t2.'aid' = t.'aid' L…` |
| QueryTest26.java#testViewWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:243 |  |
| QueryTest26.java#testViewWhere2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:253 |  |
| QueryTest26.java#testaa | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:20 | `SELECT t.'id',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'content',t.'url',…` |
| QueryTest27.java#testDerivedTable2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:254 | `SELECT t2.'score' AS 'score',t2.'status1' AS 'status1',t2.'order' AS 'order',t2.'is_top' AS 'is_top'…` |
| QueryTest27.java#testDerivedTable3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:264 | `SELECT t2.'score' AS 'score',t2.'status1' AS 'status1',t2.'order' AS 'order',t2.'is_top' AS 'is_top'…` |
| QueryTest27.java#testDerivedTable4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:264 | `SELECT t2.'score' AS 'score',t2.'status1' AS 'status1',t2.'order' AS 'order',t2.'is_top' AS 'is_top'…` |
| QueryTest27.java#testQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:133 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ?` |
| QueryTest27.java#testSave1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:114 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 'easy-query-test'.'t_sy…` |
| QueryTest27.java#testaaaa54 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:29 |  |
| QueryTest27.java#testaaaa541 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:29 |  |
| QueryTest3.java#query113 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:74 |  |
| QueryTest3.java#query121 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:104 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query124_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:53 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query124_4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:40 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query125 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:59 | `SELECT t.'id',t1.'title',t2.'star' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.…` |
| QueryTest3.java#query126 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:64 |  |
| QueryTest4.java#queryTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:151 |  |
| QueryTest8.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:14 |  |
| QueryTest8.java#testJoin10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:77 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:22 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:31 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:41 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:52 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest8.cs:64 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest9.java#selectCount2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:25 | `SELECT COUNT(*) FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHE…` |
| QueryTest9.java#selectCount3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:25 | `SELECT COUNT(*) FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHE…` |
| QueryTest9.java#selectThrow3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:36 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'del…` |
| QueryTest9.java#selectThrow4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:45 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'del…` |
| QueryTest9.java#selectThrow5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:54 |  |
| QueryTest9.java#selectThrow6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:58 |  |
| QueryTest9.java#selectThrow7 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:62：Java 抛 EasyQuerySingleMoreElementException；C# 多于一行时抛 InvalidOperationException（与 LINQ 的 Single 一致） |  |
| QueryTest9.java#selectThrow9 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest9.cs:66：Java 按方法分 First / Single / Find / Required 四种异常，C# 都是 NotFoundException；多于一行时 C# 抛 InvalidOperationException，消息不同 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'del…` |
| UpdateTest.java#aaaa | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:459：Java 把 BlogEntity 全部列映射到 SysMenuDto（类型并不匹配）；C# 只投影用到的两列 |  |
| mysql8/CTETest.java#cteTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:77 | `WITH 'm8_user_temp' AS (SELECT t.'id',t.'name' FROM 'm8_user' t WHERE t.'age' IS NULL) SELECT t2.'na…` |
| mysql8/CrossJoinTest.java#crossJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:380 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t CROSS JOIN 't…` |
| mysql8/MySQL8Test2.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:13 | `SELECT t2.'id',t2.'name',t2.'create_time',t2.'__part__column1' AS '__part__column1' FROM (SELECT t.'…` |
| mysql8/MySQL8Test2.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:26 | `SELECT t3.'id',t3.'name',t3.'create_time',t3.'__part__column1' AS '__part__column1' FROM (SELECT t.'…` |
| mysql8/MySQL8Test2.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:46 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN (SE…` |
| mysql8/MySQL8Test2.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:57 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN (SE…` |
| mysql8/MySQL8Test2.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:67 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN (SE…` |
| mysql8/MySQL8Test3.java#testDTOWhereToTableWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:360 | `SELECT t2.'name' AS 'name',t2.'phone' AS 'phone',t2.'code2' AS 'code2' FROM (SELECT t.'name' AS 'nam…` |
| mysql8/MySQL8Test3.java#testDTOWhereToTableWhere1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:370 | `SELECT t2.'name' AS 'name',t2.'phone' AS 'phone',t2.'code2' AS 'code2' FROM (SELECT t.'name' AS 'nam…` |
| mysql8/MySQL8Test5.java#testWhereJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:157 |  |

### 无实体的 Map 查询、toMap（`query.map`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:56 |  |
| QueryTest.java#query23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.NativeSql.cs:60 |  |
| QueryTest14.java#testDynamicTable2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:74 | `SELECT t1.'id' AS 'id' FROM (SELECT t.'id' AS 'id' FROM 'aaa' t) t1 LEFT JOIN (SELECT * FROM 'bbb' t…` |
| QueryTest15.java#test1223 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:322 | `SELECT * FROM '123' WHERE 'name' = ?` |
| QueryTest15.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:337 |  |
| QueryTest15.java#testxxx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest15.cs:327 | `SELECT * FROM 'table1' t INNER JOIN 'table2' t1 ON t.'id1' = t1.'id2'` |
| QueryTest16.java#test16 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:173：Java 的 Map 查询 select 之后每个 UNION 分支多占一个别名编号（t、t2，外层 t4）；C# 按实际出现的表连续编号 | `SELECT t4.'a' AS 'a',t4.'b' AS 'b' FROM ( (SELECT t.'a' AS 'a',t.'b' AS 'b' FROM 't_user' t) UNION (…` |
| QueryTest19.java#mapJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:288 | `SELECT t.'table1Column' AS 'table1Column',t1.'table2Column' AS 'table2Column',t2.'table3Column' AS '…` |
| QueryTest19.java#mapQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:306 | `SELECT 'id' AS 'id','age' AS 'age','id_card' AS 'id_card' FROM 'aaa' WHERE 'id' = ? AND 'name' = ?` |
| QueryTest19.java#testMapJson | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:318 | `SELECT * FROM 'table1' t LEFT JOIN 'table2' t1 ON t1.'id1' = t.'id2' LEFT JOIN 'table3' t2 ON t2.'id…` |
| QueryTest28.java#testQueryMap | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:125 | `SELECT * FROM 't_topic' WHERE 'id' = ?` |

### 复用查询（cloneQueryable）（`query.reuse`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest16.java#testSubSumOracle2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:160 | `SELECT COUNT(*) FROM "t_company" WHERE "name" LIKE ?` |
| QueryTest18.java#testSubCountPage | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Encryption.cs:9 | `SELECT COUNT(*) FROM 'easy-query-test'.'t_sys_user' WHERE 'phone' LIKE ?` |

### 流式读取（`query.stream`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest17.java#testOptional | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:192：Java 另外测了 EasyFieldCheckUtil 的字段名校验与 Stream 收集；C# 用 FirstOrDefaultAsync / ToListAsync / ToDictionary |  |
| QueryTest18.java#testChunk1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:151 |  |
| QueryTest18.java#testChunk2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:151 |  |
| QueryTest18.java#testChunk3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:151 |  |
| QueryTest18.java#testChunk4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:151 |  |
| QueryTest18.java#testStreamChunk1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:166 |  |
| QueryTest23.java#subquery | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:206：Java 取 JDBC 流式结果的 ResultSetMetaData；C# 的流式读取（AsAsyncEnumerable）不暴露驱动的元数据 | `select * from t_blog where id <> ?` |
| QueryTest25.java#test12 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:21：Java 按表名反查实体元数据（getEntityMetadataList）；C# 的元数据按类型取，这里核对表名 |  |
| pgsql/QueryTest19.java#testChunk | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:107 |  |

### union / union all（`query.union`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#unionSQLite | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:191 | `SELECT t3."id",t3."stars",t3."title",t3."create_time" FROM ( SELECT t."id",t."stars",t."title",t."cr…` |
| QueryTest.java#query52 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:18 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:18 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:23 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:23 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:32 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:32 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:36 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query53 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:44 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query54 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:47 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query55 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:50 | `SELECT t3.'create_time',t3.'stars' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't…` |
| QueryTest.java#query56 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:55 | `SELECT t5.'id',t5.'stars',t5.'title',t5.'create_time' FROM ( (SELECT t3.'id',t3.'stars',t3.'title',t…` |
| QueryTest.java#query57 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.Union.cs:65 |  |
| QueryTest.java#query65 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:66：别名编号：Java 复用了执行过的查询对象，外层别名是 t7；C# 连续编号为 t4 | `SELECT t7.'id',t7.'stars',t7.'title',t7.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query66 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:78：别名编号：Java 的分支与外层是 t、t2、t4/t5、t8；C# 连续编号为 t、t1、t2/t3、t4 | `SELECT t8.'id',t8.'stars',t8.'abc' AS 'abc' FROM ( (SELECT t.'id',t.'stars',t.'title' AS 'abc' FROM …` |
| QueryTest.java#query67 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:92：别名编号：Java 的外层别名是 t5（中间跳号），C# 连续编号为 t2；Java 的 proxy set 同名列也写 AS，C# 成员初始化同名列不写 | `SELECT t5.'id',t5.'stars',t5.'abc' AS 'abc' FROM ( (SELECT t.'id',t.'stars',t.'title' AS 'abc' FROM …` |
| QueryTest11.java#testx20 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:274：别名编号：Java 的 union 分支与外层是 t、t2、t4、t6；C# 连续编号 | `SELECT t6.'value1' AS 'value1',t6.'value2' AS 'value2' FROM ( (SELECT ? AS 'value1',COUNT(*) AS 'val…` |
| mysql8/MySQL8Test3.java#testOneIsNull | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:164 |  |

### 按 DTO 自动带出（selectAutoInclude）（`dto.autoInclude`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DirectRelationTest.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DirectRelationTest.cs:81 | `SELECT t.'c1',t.'c2',t.'c3',t.'c4' FROM 'direct1' t` |
| DirectRelationTest.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DirectRelationTest.cs:102 | `SELECT t.'c1',t.'c2',t.'c3',t.'c4' FROM 'direct1' t` |
| GeneralFunctionsTest.java#testKingbaseCte | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:186 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0+0 AS "deep",t1."id",t1."parent_id",t1."content",t1."user…` |
| QueryTest14.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:265 | `SELECT t.'id',t.'name',t.'password' FROM 't_test_user' t` |
| QueryTest16.java#test111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:132 | `SELECT t.'id' AS '__relation__id' FROM 'sys_user' t WHERE t.'deleted' = ?` |
| QueryTest19.java#testDoc100 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:80 | `SELECT t.'id',t.'name' FROM 't_user' t` |
| QueryTest19.java#testDoc99 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:63 | `SELECT t.'id',t.'name' FROM 't_user' t` |
| QueryTestRelationTest.java#testRelation | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.Relation.cs:91：24 段拆成本文件的多个测试；第 9 段（NavigateJoin 的 DTO 分页投影）Java 给 JOIN 的列写同名 AS 并多选一遍 t1 的关联列，C# 不写；第 18、19 段在 MySQL 8.4 严格模式下拿 '123123' 比较 DATETIME 会报错，只核对 SQL | `SELECT 'id','com_id','user_id','gw' FROM 'my_com_user'` |
| doc/DocTest1.java#testBlog5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest1.cs:82 | `SELECT t1.'name' AS 'uname1',t3.'name' AS 'uname2',t.'id',t.'name' FROM 'test_self' t LEFT JOIN 'my_…` |
| mysql8/IncludeConditionTest.java#testInclude3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/IncludeConditionTest.cs:111 | `SELECT t.'id',t.'name',t.'version' AS '__relation__version' FROM 'my_task' t WHERE t.'id' = ?` |
| mysql8/IncludeConditionTest.java#testInclude4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/IncludeConditionTest.cs:127 | `SELECT t.'id',t.'name',t.'version' FROM 'my_task' t WHERE t.'id' = ?` |
| mysql8/M8BankTest.java#partitionBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Flat.cs:32 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t` |
| mysql8/M8BankTest.java#testExtraDTO | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Flat.cs:113 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/M8BankTest.java#testExtraDTO123 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Flat.cs:168 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/M8BankTest.java#testExtraFilter | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.AutoInclude.cs:56 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t` |
| mysql8/M8BankTest.java#testExtraFilter2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.AutoInclude.cs:129 | `SELECT IFNULL(t2.'__count2__',0) AS 'card_count',t.'id',t.'name',t.'phone',t.'age',t.'create_time' F…` |
| mysql8/M8PCABTest.java#testFlat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8PCABTest.cs:137 | `SELECT t.'id',t.'name' FROM 'm8_province' t` |
| mysql8/M8PCABTest.java#testFlat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8PCABTest.cs:149 | `SELECT t.'id',t.'name' FROM 'm8_province' t` |
| mysql8/M8PCABTest.java#testFlat2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8PCABTest.cs:161 | `SELECT t.'id',t.'name' FROM 'm8_province' t` |
| mysql8/M8Test1.java#test24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:98 | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |
| mysql8/M8Test1.java#test25 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:111 | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |
| mysql8/ManyJoinTest.java#testFlatExtra | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.Flat.cs:32 | `SELECT t.'id',t.'name',t.'create_time' FROM 'm8_role' t` |
| mysql8/ManyJoinTest.java#testFlatMenuOwner | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.Flat.cs:71 |  |
| mysql8/ManyJoinTest.java#testFlatMenuOwner1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.Flat.cs:92 |  |
| mysql8/ManyJoinTest.java#testFlatMenuOwnerFlatValueConverter | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.Flat.cs:129 |  |
| mysql8/MySQL8Test2.java#includePartitionBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:473 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#includePartitionBy1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:486 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test3.java#testQueryFirstLimit2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:425 | `SELECT t2.'code',t2.'type',t2.'open_time',t2.'uid' AS '__relation__uid' FROM (SELECT t1.'id' AS 'id'…` |
| mysql8/MySQL8Test3.java#testTree2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:285 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'deep',t1.'id',t1.'parent_id',t1.'content',t1.'user_i…` |
| mysql8/MySQL8Test3.java#testTree3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:317 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'deep',t1.'id',t1.'parent_id',t1.'content',t1.'user_i…` |
| mysql8/MySQL8Test3.java#testTree4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:327 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'deep',t1.'id',t1.'parent_id',t1.'content',t1.'user_i…` |
| mysql8/MySQL8Test3.java#testTree5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:337：异常信息用中文，说明缺的是哪个属性 |  |
| mysql8/MySQL8Test5.java#testSysDeptCTE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:312 |  |
| pgsql/QueryCteTest.java#tree7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:141 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep1",t1."id",t1."parent_id",t1."name" FROM "public…` |
| pgsql/QueryTest19.java#tree7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:141 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep1",t1."id",t1."parent_id",t1."name" FROM "catego…` |
| pgsql/QueryTest19.java#tree8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:152 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |

### 按键填充（fillMany / fillOne）（`fill`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| RelationTest.java#provinceTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:62 |  |
| RelationTest.java#provinceTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:62 |  |
| RelationTest.java#provinceTest4 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:79：Java 第一段漏了 province_code 条件（只有 t.'code' = ?），C# 每段都按键过滤 | `SELECT 'code','name' FROM 't_province'` |
| RelationTest.java#provinceVoTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:94 |  |

### 一对多展开（flatElement / elements）（`nav.flat`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest12.java#testDoc24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:150 | `DELETE FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE (EXISTS(SELECT …` |
| QueryTest16.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:49 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE EXISTS(SELECT 1 …` |
| QueryTest16.java#test15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:26 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE (t.'name' LIKE ?…` |
| QueryTest16.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:55 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN (SELECT t2.'…` |
| QueryTest16.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:62 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN (SELECT t2.'…` |
| QueryTest16.java#testDocSub | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:68 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_company' t WHERE EXISTS(SELECT 1 FROM 't_user' t1 WHE…` |
| QueryTest17.java#testFlatElement1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:126 | `SELECT t.'id',t.'f1',t.'f2',t.'parent_id' FROM 'MyObject' t LEFT JOIN 'MyObjectParent' t1 ON t1.'id'…` |
| QueryTest17.java#testFlatElement2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:136 | `SELECT t.'id',t.'f1',t.'f2',t.'parent_id' FROM 'MyObject' t LEFT JOIN 'MyObjectParent' t1 ON t1.'id'…` |
| QueryTest17.java#testNop2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:149 | `SELECT t4.'value1' AS 'value1',SUM(t4.'value2') AS 'value2',COUNT(t5.'type') AS 'value3' FROM (SELEC…` |
| QueryTest17.java#testaaa | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:165：测的是 Java 代理对象的导航路径工具（EasyProxyUtil.getFullNavValue） |  |
| QueryTest18.java#test8_1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:29：同 test8 |  |
| QueryTest22.java#testManyGroup17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:60 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:60 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest25.java#test11a | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest25.java#testToListFlat1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:29：Java 的 toList(flatElement…) 先只查关联键、再逐层带出并在内存里去重；C# 的 SelectMany 按 LINQ 语义写成一条 INNER JOIN（保留重复，去重加 Distinct） |  |
| QueryTest25.java#xxx22 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest25.cs:159：用 Hikari 连本机 3306、替换多个内部服务另建客户端，属于 Java 的启动配置 |  |
| QueryTest26.java#includeFlat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:339 |  |
| QueryTestRelationTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:13 | `SELECT t.'id',t.'com_id',t.'user_id',t.'gw' FROM 'my_com_user' t LEFT JOIN 'my_company_info' t1 ON t…` |
| SubJoinTest.java#testSubJoinCaseWhen1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:54 | `SELECT IFNULL((SELECT SUM(t3.'age') FROM 'sub_join_book' t2 LEFT JOIN 'sub_join_author' t3 ON t3.'id…` |
| mysql8/CTETest.java#cteTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:67 | `WITH 'm8_user_temp' AS (SELECT t.'id',t.'name' FROM 'm8_user' t WHERE t.'age' IS NULL) SELECT t2.'id…` |
| mysql8/CTETest.java#cteTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/CTETest.cs:90 | `WITH 'm8_user_temp2' AS (SELECT t.'id',t.'name',t.'age',(ROW_NUMBER() OVER (PARTITION BY t.'age' ORD…` |
| mysql8/M8BankTest.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:9 | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:21 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'u…` |
| mysql8/M8BankTest.java#test13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:32 | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:46 | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:58 | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:71 | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.GroupJoinWindows.cs:84 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'u…` |
| mysql8/M8BankTest.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:63 | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t1.'type' SEPARATOR ?) FROM 't_bank_card' t1 WHERE …` |
| mysql8/M8BankTest.java#test7_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:75 | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t2.'type' SEPARATOR ?) FROM (SELECT t1.'id',t1.'uid…` |
| mysql8/M8BankTest.java#test7_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:87 | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t1.'type' SEPARATOR ?) FROM 't_bank_card' t1 WHERE …` |
| mysql8/M8BankTest.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:99 | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t4.'type' SEPARATOR ?) FROM 't_bank_card' t4 WHERE …` |
| mysql8/M8BankTest.java#testFlatTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Elements.cs:77 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:93 | `SELECT t.'id',t.'name',t.'create_time',(NOT (EXISTS(SELECT 1 FROM (SELECT t1.'id',t1.'uid',t1.'code'…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:104 | `SELECT t.'id',t.'name',t.'create_time',IFNULL(t4.'__none2__',?) AS '__part__column1' FROM 't_bank' t…` |
| mysql8/M8OrderTest.java#testSubQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8OrderTest.cs:13 | `SELECT t.'id',t.'no',t.'price',t.'create_time' FROM 't_order' t INNER JOIN (SELECT t1.'order_id' AS …` |
| mysql8/M8OrderTest.java#testSubQuery3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8OrderTest.cs:25 | `SELECT t.'id',t.'no',t.'price',t.'create_time' FROM 't_order' t INNER JOIN (SELECT t1.'order_id' AS …` |
| mysql8/ManyJoinTest.java#testElement8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:83 | `SELECT t4.'__max2__' AS 'value1',t7.'type' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t3.'uid' …` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:206 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:216 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:236 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:246 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/MySQL8Test3.java#testFlatElement | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:42 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testFlatElement1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:42 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testFlatElement2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:53 | `SELECT t.'id',t.'name' FROM 'm8_province' t WHERE EXISTS(SELECT 1 FROM 'm8_city' t1 WHERE t1.'pid' =…` |
| mysql8/MySQL8Test3.java#testFlatElement3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:64 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:76 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:86 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:98 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:111 | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testMany2Many | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:123 | `SELECT t.'id',t.'name',t.'order' FROM 'm8_parent' t LEFT JOIN (SELECT t2.'parent_id' AS '__group_key…` |
| mysql8/MySQL8Test5.java#testManyLimit1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:240 | `SELECT t.'id' AS 'value1',t6.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t4.'__gro…` |
| mysql8/MySQL8Test5.java#testManyLimit1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:263 | `SELECT t.'id' AS 'value1',t6.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t4.'__gro…` |
| mysql8/MySQL8Test5.java#testManyLimit1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:275 | `SELECT t.'id' AS 'value1',t6.'__joining2__' AS 'value2',t6.'__joining3__' AS 'value3' FROM 'm8_user'…` |
| mysql8/MySQL8Test5.java#testManyLimit5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:32 | `SELECT t.'id' AS 'value1',t5.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t4.'ui…` |
| mysql8/MySQL8Test5.java#testManyLimit6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:45 | `SELECT t.'id' AS 'value1',t5.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t4.'ui…` |

### 隐式子查询改分组 join（`nav.groupJoin`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| NumberFunctionsTest.java#subQueryMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:76 | `SELECT IFNULL(t2.'__any7__',?) AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__grou…` |
| NumberFunctionsTest.java#subQueryORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:92 | `SELECT NVL(t2."__any7__",?) AS "value1" FROM "t_sys_user" t LEFT JOIN (SELECT t1."uid" AS "__group_k…` |
| QueryTest19.java#test1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:123 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN (SELECT t2.'…` |
| QueryTest22.java#testDoubleField | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:190 | `SELECT IFNULL(t2.'__count3__',0) AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 'my_com_user…` |
| QueryTest22.java#testManyGroup | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:19 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:130 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:9 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:9 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:19 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t2.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:28 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:39 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest22.java#testManyGroup16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:50 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest22.java#testManyGroup2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:87 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:93 | `SELECT IFNULL(t2.'__count3__',0) AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2',IFNULL(t2.'__coun…` |
| QueryTest22.java#testManyGroup4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:103 | `SELECT IFNULL(t2.'__any2__',?) AS 'value1',IFNULL(t2.'__none3__',?) AS 'value2' FROM 'doc_user' t LE…` |
| QueryTest22.java#testManyGroup5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:112 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup6 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:118：Java 在 where 里只写聚合不写比较时什么也不生成；C# 的条件必须是布尔表达式 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t` |
| QueryTest22.java#testManyGroup7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:122 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t` |
| QueryTest22.java#testManyGroup8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:130 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:140 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT COUNT(*) FROM 'doc_bank_car…` |
| QueryTest22.java#testManyGroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:37 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:46 | `SELECT t.'id' AS 'value1',IFNULL(t3.'__sum2__',0) AS 'value2',t3.'__min5__' AS 'value3' FROM 'doc_us…` |
| QueryTest22.java#testManyGroupJoin2_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:47 | `SELECT t.'id' AS 'value1',IFNULL(t3.'__sum2__',0) AS 'value2',t3.'__min5__' AS 'value3' FROM 'doc_us…` |
| QueryTest22.java#testManyGroupJoin2_2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:64：Java 在条件中途写 filter，之后引用的聚合另起一个分组 JOIN；C# 的子查询条件统一写在 GroupJoin 的配置里 | `SELECT t.'id' AS 'value1',IFNULL(t5.'__sum2__',0) AS 'value2',t5.'__min4__' AS 'value3' FROM 'doc_us…` |
| QueryTest22.java#testManyGroupJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:67 | `SELECT t.'id' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2',IFNULL(t2.'__count4__',0) AS 'value3…` |
| QueryTest22.java#testManyGroupx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.cs:29 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyJoinCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:233 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyJoinNone | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:225 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:158 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:170 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:170 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:183 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:104 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:117 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:129 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:142 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:148 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:158 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testSelectAnyElement | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:201 | `SELECT t2.'__joining2__' AS 'value1',t2.'__joining3__' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELE…` |
| QueryTest22.java#testSelectAnyElement3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:217 | `SELECT t2.'__max2__' AS 'value1',t2.'__min3__' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'u…` |
| QueryTest23.java#manyJoinMany2Many | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:25 | `SELECT t.'id',t.'name' FROM 'school_class' t LEFT JOIN (SELECT t2.'class_id' AS '__group_key1__',COU…` |
| QueryTest23.java#testElement3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:12 | `SELECT t2.'__max2__' AS 'value1',t5.'type' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' …` |
| QueryTest23.java#testJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:80 |  |
| QueryTest23.java#testMany2Many2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:148 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_user_account' t LEFT JOIN …` |
| QueryTest23.java#testMany2Many2_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:148 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_user_account' t LEFT JOIN …` |
| mysql8/M8BankTest.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:41 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#testExtraFilter5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:152 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count2__',0) AS 'value2' FROM 't_bank' t LEFT JOIN (SELECT …` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountToGroupTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:47 | `SELECT t.'id',t.'name',t.'create_time',IFNULL(t2.'__count2__',0) AS '__part__column1' FROM 't_bank' …` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountToGroupTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:81 | `SELECT t.'id',t.'name',t.'create_time',IFNULL(t2.'__count2__',0) AS '__part__column1',IFNULL(t2.'__c…` |
| mysql8/M8BankTest.java#whereSubQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:135 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#whereSubQuery2_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:145 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#whereSubQuery2_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:156 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#whereSubQuery3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:168 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE EXISTS(SELECT 1 F…` |
| mysql8/M8BankTest.java#whereSubQuery4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:178 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8OrderTest.java#testSubQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8OrderTest.cs:19 | `SELECT t.'id',t.'no',t.'price',t.'create_time' FROM 't_order' t INNER JOIN (SELECT t1.'id',t1.'order…` |
| mysql8/M8OrderTest.java#testSubQuery4 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8OrderTest.cs:31：异常信息用中文，说明缺主键、该怎样声明排序 |  |
| mysql8/ManyJoinTest.java#manyJoinMany2Many10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:168 | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t1…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:97 | `SELECT t.'id',t.'name' FROM 'school_class' t LEFT JOIN (SELECT t2.'class_id' AS '__class_id__',t1.'i…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:108 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:117 | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t2…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many3_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:127 | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t2…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:137 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many4_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:147 | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t2…` |
| mysql8/ManyJoinTest.java#testElement3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:69 | `SELECT t2.'__max2__' AS 'value1',t5.'type' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' …` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:193 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testManyJoinGroup | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:178 |  |
| mysql8/MySQL8Test2.java#testPageSelectSubQueryCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:137 | `SELECT COUNT(*) FROM 't_bank' t` |

### include 带出导航（`nav.include`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DirectRelationTest.java#test | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DirectRelationTest.cs:120 | `SELECT 'c1','c2','c3','c4','c5' FROM 'direct1'` |
| DirectRelationTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DirectRelationTest.cs:137 | `SELECT 'c1','c2','c3','c4','c5' FROM 'direct1'` |
| QueryTest18.java#esss | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Includes.cs:23 |  |
| QueryTest18.java#test14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Includes.cs:6 | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'id',t.'stars',t1.'create_time' FROM 't_topic' t LEFT JOIN '…` |
| QueryTest2.java#query26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:394 |  |
| QueryTest28.java#testInterceptor3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:39 | `SELECT 'id','name' FROM 'm8_interceptor'` |
| RelationNullValueTest.java#one2oneTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationNullValueTest.cs:7 |  |
| RelationTest.java#provinceTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:23 |  |
| RelationTest.java#provinceTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:42 |  |
| RelationTest.java#provinceTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:57 |  |
| RelationTest.java#provinceTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:31 |  |
| RelationTest.java#provinceTest61 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:226 |  |
| RelationTest.java#provinceTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:31 |  |
| mysql8/M8CommentTest.java#testPartitionBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8CommentTest.cs:6 | `SELECT 'id','parent_id','content','username','time' FROM 'comment'` |
| mysql8/M8IncludeLimitTest.java#testIncludeLimit | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8IncludeLimitTest.cs:24 |  |
| mysql8/M8IncludeLimitTest.java#testIncludeLimitPagingPartition | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8IncludeLimitTest.cs:35 |  |
| mysql8/M8IncludeLimitTest.java#testIncludeLimitPagingUnionAll | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8IncludeLimitTest.cs:42 |  |
| mysql8/M8IncludeOrderTest.java#testOrde2r | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8IncludeOrderTest.cs:16 |  |
| mysql8/M8IncludeOrderTest.java#testOrder | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8IncludeOrderTest.cs:7 |  |
| mysql8/M8Test1.java#testRelationColumns1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8RelationColumnsTest.cs:38 | `SELECT 'user_id','user_name','user_age','create_time','book_ids' FROM 'm8_user_book_ids'` |
| mysql8/M8Test1.java#testSelfTargetTypeDiff | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:67：Java 的异常来自 MySQL 驱动（Cannot determine value type from string 'u1'）；C# 在把父键转成目标列的类型时就抛出 FormatException |  |
| mysql8/MySQL8Test3.java#testQueryFirstLimit1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:391 | `SELECT t2.'id' AS 'id',t2.'uid' AS 'uid',t2.'code' AS 'code',t2.'type' AS 'type',t2.'bank_id' AS 'ba…` |
| mysql8/MySQL8Test4.java#includeOneTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:254 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testAll | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:154 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE NOT (EXISTS(SELEC…` |
| mysql8/MySQL8Test4.java#testAll1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:167 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE NOT (EXISTS(SELEC…` |
| mysql8/MySQL8Test4.java#testAll2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:179 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE NOT (EXISTS(SELEC…` |
| mysql8/MySQL8Test4.java#testAllGroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:191 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test4.java#testAllGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:203 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test4.java#testIncludeIdOnly1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:282 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testIncludeIdOnly2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:293 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test5.java#selectJoinInclude | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:164 |  |
| pgsql/QueryTest19.java#test | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:216 |  |
| pgsql/QueryTest19.java#testAll | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:114 | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t WHERE NOT (EXISTS(SELECT 1 FROM "doc_bank…` |
| pgsql/QueryTest19.java#testAll1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:127 | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t WHERE NOT (EXISTS(SELECT 1 FROM "doc_bank…` |
| pgsql/QueryTest19.java#testAll2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:140 | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t WHERE NOT (EXISTS(SELECT 1 FROM "doc_bank…` |
| pgsql/QueryTest19.java#testAllGroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:152 | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t LEFT JOIN (SELECT t1."uid" AS "__group_ke…` |
| pgsql/QueryTest19.java#testAllGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19Queries.cs:164 | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t LEFT JOIN (SELECT t1."uid" AS "__group_ke…` |

### 隐式子查询（any / none / count / sum …）（`nav.subquery`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#countFilterMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:8 | `SELECT COUNT(t.'id') AS 'value1',COUNT(DISTINCT t.'title') AS 'value2',COUNT((CASE WHEN t.'title' <>…` |
| GeneralFunctionsTest.java#countMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:16 | `SELECT COUNT(t.[id]) AS [value1],COUNT(DISTINCT t.[id]) AS [value2] FROM [doc_bank_card] t` |
| GeneralFunctionsTest.java#countMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:16 | `SELECT COUNT(t.'id') AS 'value1',COUNT(DISTINCT t.'id') AS 'value2' FROM 'doc_bank_card' t` |
| GeneralFunctionsTest.java#countORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:16 | `SELECT COUNT(t."id") AS "value1",COUNT(DISTINCT t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#countPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:16 | `SELECT COUNT(t."id") AS "value1",COUNT(DISTINCT t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#maxMinMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:38 | `SELECT MAX(t.[id]) AS [value1],MIN(t.[id]) AS [value2] FROM [doc_bank_card] t` |
| GeneralFunctionsTest.java#maxMinMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:38 | `SELECT MAX(t.'id') AS 'value1',MIN(t.'id') AS 'value2' FROM 'doc_bank_card' t` |
| GeneralFunctionsTest.java#maxMinORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:38 | `SELECT MAX(t."id") AS "value1",MIN(t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#maxMinPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:38 | `SELECT MAX(t."id") AS "value1",MIN(t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#minMaxFilterMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:49 | `SELECT MAX(t.'score') AS 'value1',MAX((CASE WHEN t.'title' <> ? THEN t.'score' ELSE NULL END)) AS 'v…` |
| NumberFunctionsTest.java#avgMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:53 | `SELECT AVG(t.[score]) AS [value1],AVG(DISTINCT t.[score]) AS [value2],AVG(DISTINCT (CASE WHEN t.[tit…` |
| NumberFunctionsTest.java#avgMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:53 | `SELECT AVG(t.'score') AS 'value1',AVG(DISTINCT t.'score') AS 'value2',AVG(DISTINCT (CASE WHEN t.'tit…` |
| NumberFunctionsTest.java#avgORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:53 | `SELECT AVG(t."score") AS "value1",AVG(DISTINCT t."score") AS "value2",AVG(DISTINCT (CASE WHEN t."tit…` |
| NumberFunctionsTest.java#avgPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:53 | `SELECT AVG(t."score") AS "value1",AVG(DISTINCT t."score") AS "value2",AVG(DISTINCT (CASE WHEN t."tit…` |
| NumberFunctionsTest.java#sumMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:64 | `SELECT SUM(t.[score]) AS [value1],SUM(DISTINCT t.[score]) AS [value2],SUM(DISTINCT t.[score]) AS [va…` |
| NumberFunctionsTest.java#sumMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:64 | `SELECT SUM(t.'score') AS 'value1',SUM(DISTINCT t.'score') AS 'value2',SUM(DISTINCT t.'score') AS 'va…` |
| NumberFunctionsTest.java#sumORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:64 | `SELECT SUM(t."score") AS "value1",SUM(DISTINCT t."score") AS "value2",SUM(DISTINCT t."score") AS "va…` |
| NumberFunctionsTest.java#sumPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/NumberFunctionsTest.cs:64 | `SELECT SUM(t."score") AS "value1",SUM(DISTINCT t."score") AS "value2",SUM(DISTINCT t."score") AS "va…` |
| QueryTest.java#query61 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:6 |  |
| QueryTest.java#query63 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:18 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(t1.'id') FROM 't_blog' t1 WHERE t1.'…` |
| QueryTest.java#query63_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:33 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT SUM(t1.'star') FROM 't_blog' t1 WHERE t1.'…` |
| QueryTest.java#query64 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:52 | `SELECT t.'id',t.'stars',t.'title',(SELECT COUNT(t1.'id') FROM 't_blog' t1 WHERE t1.'deleted' = ? AND…` |
| QueryTest11.java#testx5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest11.cs:124 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT SUM(t1.'star') FROM 't_blog' t1 WHERE t1.'…` |
| QueryTest12.java#testAny1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:30 | `SELECT 1 FROM 't_topic' WHERE 'title' = ? ORDER BY CASE WHEN 'id' IS NULL THEN 1 ELSE 0 END ASC,'id'…` |
| QueryTest12.java#testDoc1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:280 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 WHERE t1…` |
| QueryTest12.java#testDoc10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:64 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest12.java#testDoc16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:96 | `SELECT t.'id',t.'name' FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_book' t1 WHERE (t…` |
| QueryTest12.java#testDoc17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:104 | `SELECT t.'id',t.'name' FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE…` |
| QueryTest12.java#testDoc2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:289 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE NOT (EXISTS(SELECT 1 FROM 'school_student' t1 WHE…` |
| QueryTest12.java#testDoc22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:115 | `SELECT t.'id',t.'name' FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE…` |
| QueryTest12.java#testDoc3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:297 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| QueryTest12.java#testDoc4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:306 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE (SELECT COUNT(*) FROM 'school_student' t1 WHERE t…` |
| QueryTest12.java#testDoc5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:315 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_teacher' t1 WHERE EX…` |
| QueryTest12.java#testDoc6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:315 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_teacher' t1 WHERE EX…` |
| QueryTest12.java#testDoc7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:43 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:43 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.Part2.cs:54 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest12.java#testNum2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:92 | `SELECT (SUM(t.'score') - SUM(t.'score')) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testNum3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest12.cs:92 | `SELECT (SUM(t.'score') - SUM(t.'score')) FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest16.java#docSub | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:77 | `SELECT t.'id' AS 'value1',(NOT (EXISTS(SELECT 1 FROM 't_user' t1 WHERE t1.'company_id' = t.'id' LIMI…` |
| QueryTest16.java#test19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:91 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE EXISTS(SELECT 1 …` |
| QueryTest16.java#test20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:107 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN 't_company' …` |
| QueryTest16.java#testSubSum | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:101 | `SELECT t1.'id' AS 'value1',IFNULL((SELECT SUM(t2.'age') FROM 't_user' t2 WHERE t2.'company_id' = t1.…` |
| QueryTest16.java#testSubSumOracle | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest16.cs:142 | `SELECT t1."id" AS "value1",NVL((SELECT SUM(t2."age") FROM "t_user" t2 WHERE t2."company_id" = t1."id…` |
| QueryTest17.java#testFlatElement5 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest17.cs:160：Java 用例体全部注释掉了 | `SELECT t.'id',t.'f1',t.'f2',t.'parent_id' FROM 'MyObject' t LEFT JOIN 'MyObjectParent' t1 ON t1.'id'…` |
| QueryTest18.java#testCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.More.cs:68 |  |
| QueryTest18.java#testSubCountLimitSelect1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Encryption.cs:41 | `SELECT t1.'id' AS 'value1',t1.'phone' AS 'value2',(SELECT COUNT(*) FROM 't_blog' t2 WHERE t2.'delete…` |
| QueryTest18.java#testSubCountPage1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Encryption.cs:28 | `SELECT COUNT(*) FROM 'easy-query-test'.'t_sys_user' WHERE 'phone' <> ?` |
| QueryTest18.java#textDistinct1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:89：Java 写 SELECT DISTINCT COUNT(*)，等于不去重计数；C# 包一层子查询保证去重后再计数 | `SELECT DISTINCT COUNT(*) FROM 't_topic'` |
| QueryTest19.java#test122 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:248 |  |
| QueryTest19.java#test1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:112 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE t.'name' = ? AND…` |
| QueryTest19.java#test1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:117 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE t.'name' = ? AND…` |
| QueryTest21.java#subNumberRangeClosed | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:162 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE ((SELECT COUNT(*) FROM 'doc_bank_ca…` |
| QueryTest21.java#subNumberRangeClosed1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:171 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT COUNT(*) FROM 'doc_bank_car…` |
| QueryTest21.java#testDistinctCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:229 | `SELECT COUNT(DISTINCT t.'id') FROM 't_blog' t WHERE t.'deleted' = ? AND t.'title' LIKE ?` |
| QueryTest21.java#testDistinctCount1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:239 | `SELECT COUNT(DISTINCT t.'id') FROM 't_blog' t WHERE t.'deleted' = ? AND t.'title' LIKE ?` |
| QueryTest22.java#testManyGroup29 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:71 |  |
| QueryTest22.java#testOrderCountDistinct1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:98 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t ORDER BY IFNULL((SELECT SUM((SELECT COUNT…` |
| QueryTest22.java#testOrderCountDistinct11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:165 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT MAX(t1.'type') FROM 'doc_ba…` |
| QueryTest22.java#testOrderCountDistinct14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:177 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT MIN(t1.'type') FROM 'doc_ba…` |
| QueryTest22.java#testOrderCountDistinct3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:111 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t ORDER BY (SELECT COUNT(DISTINCT t2.'id') …` |
| QueryTest22.java#testOrderCountDistinct5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:123 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t ORDER BY IFNULL((SELECT SUM(DISTINCT (SEL…` |
| QueryTest22.java#testOrderCountDistinct6_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:136 | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest23.java#anyTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:64 | `SELECT (SELECT MAX(t2.'type') FROM 'doc_bank_card' t2 WHERE t2.'uid' = t.'id' AND t.'name' = t2.'cod…` |
| QueryTest23.java#filterOn3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:239 | `SELECT COUNT(*) FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 ON t1.'id' = t.'bank_id' AND t1.'nam…` |
| QueryTest23.java#testJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:80 |  |
| QueryTest23.java#testMany2Many | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest23.cs:138 | `SELECT t.'name' AS 'value1',(SELECT COUNT(*) FROM 't_user_book' t2 WHERE t2.'uid' = t.'uid') AS 'val…` |
| QueryTest24.java#testSubCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:213 | `SELECT t.'id',t.'create_time',t.'title',IFNULL(t2.'__count2__',0) AS 'stars' FROM 't_blog' t LEFT JO…` |
| QueryTest24.java#testx14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:165 | `SELECT MAX(t.'topic_type') FROM 't_topic_type' t LIMIT 1` |
| QueryTest24.java#testx15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:165 | `SELECT MIN(t.'topic_type') FROM 't_topic_type' t LIMIT 1` |
| QueryTest27.java#autoMergeMany2Many1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:193 | `SELECT t.'station_id',t.'station_name' FROM 't_station' t LEFT JOIN (SELECT t2.'station_id' AS '__gr…` |
| QueryTest27.java#autoMergeMany2Many2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:198 | `SELECT t.'station_id',t.'station_name' FROM 't_station' t LEFT JOIN (SELECT t2.'station_id' AS '__gr…` |
| QueryTest27.java#testCountSubQueryCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:39 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest28.java#testInterceptor2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:31 | `SELECT t.'id',t.'name' FROM 'm8_interceptor' t WHERE EXISTS(SELECT 1 FROM 'm8_interceptor2' t1 WHERE…` |
| QueryTest3.java#nativePage2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:235 | `select * from t_topicxx` |
| QueryTest4.java#query1233 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:9 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest5.java#joinTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:27 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:58 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:70 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:82 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:93 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:104 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:114 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| Relation2Test.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:57 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE (SELECT MAX(t1.'name') FROM 'my_school_student…` |
| Relation2Test.java#test10_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:57 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE (SELECT MAX(t1.'name') FROM 'my_school_student…` |
| Relation2Test.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:34 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE (SELECT COUNT(*) FROM 'my_school_student' t1 W…` |
| Relation2Test.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:38 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE EXISTS(SELECT 1 FROM 'my_school_student' t1 WH…` |
| Relation2Test.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/Relation2Test.cs:47 | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT AVG(CAST(t1.'name' AS DECIMAL(3…` |
| RelationTest.java#schoolTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:106 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 WHERE t1…` |
| RelationTest.java#schoolTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:127 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| RelationTest.java#schoolTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:137 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| RelationTest.java#schoolTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:137 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| RelationTest.java#schoolTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:149 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_teacher' t1 WHERE EX…` |
| RelationTest.java#schoolTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:158 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE (SELECT COUNT(*) FROM 'school_student' t1 WHERE t…` |
| RelationTest.java#schoolTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:168 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE NOT (EXISTS(SELECT 1 FROM 'school_student' t1 WHE…` |
| RelationTest.java#schoolTest8_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.cs:180 | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 WHERE t1…` |
| SubJoinTest.java#testSubJoinAvg | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:40 | `SELECT IFNULL((SELECT AVG(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinAvg2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:40 | `SELECT IFNULL((SELECT AVG(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinAvgDistinct | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:43 | `SELECT IFNULL((SELECT AVG(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinAvgDistinct2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:43 | `SELECT IFNULL((SELECT AVG(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinMax | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:46 | `SELECT (SELECT MAX(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinMax2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:46 | `SELECT (SELECT MAX(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinMin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:49 | `SELECT (SELECT MIN(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinMin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:49 | `SELECT (SELECT MIN(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinSum | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:28 | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSum2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:28 | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSumDistinct | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:31 | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinSumDistinct2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SubJoinTest.cs:31 | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| mysql8/M8BankTest.java#fromSubQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:137 | `SELECT t2.'vo1' AS 'vo1',t2.'vo2' AS 'vo2',t2.'vo3' AS 'vo3',t2.'bank_card_count' AS 'bank_card_coun…` |
| mysql8/M8BankTest.java#testQueryBankAndCardExplicitCountTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:36 | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(t1.'id') FROM 't_bank_card' t1 WHERE t1.'bank_i…` |
| mysql8/M8BankTest.java#testQueryBankAndCardExplicitCountTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:68 | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(t1.'id') FROM 't_bank_card' t1 WHERE t1.'bank_i…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:27 | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(*) FROM 't_bank_card' t1 WHERE t1.'bank_id' = t…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:57 | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(*) FROM 't_bank_card' t1 WHERE t1.'bank_id' = t…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:161 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time',IFNULL(t2.'__count2__',0) AS '__part__colum…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:171 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time',t2.'__count2__' AS '__part__column1' FROM '…` |
| mysql8/M8BankTest.java#whereSubQuery1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:126 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE EXISTS(SELECT 1 F…` |
| mysql8/M8BankTest.java#whereSubQuery5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.SubQueries.cs:187 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8QueryTest.java#whereObject5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8QueryTest.cs:102 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8Test1.java#testMany | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:42 |  |
| mysql8/M8Test1.java#testRelationColumns | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8RelationColumnsTest.cs:27 | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time',t.'book_ids' FROM 'm8_user_book_ids' t…` |
| mysql8/ManyJoinTest.java#manyGroupTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:11 |  |
| mysql8/ManyJoinTest.java#test10GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:373 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test11GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:385 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test12GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:400 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test13GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:412 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test14GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:425 | `SELECT t.'name' AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',COUNT…` |
| mysql8/ManyJoinTest.java#test15GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:437 | `SELECT t.'name' AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',MAX(t…` |
| mysql8/ManyJoinTest.java#test16GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:448 | `SELECT t.'name' AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',MAX((…` |
| mysql8/ManyJoinTest.java#test3GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:298 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test4GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:311 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test5GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:323 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test6GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:336 | `SELECT t.'name' AS 'value1',IFNULL(t3.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test7GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:348 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test8GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:361 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test9GroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:361 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#testGroupJoinCombine | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:277 | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:226 | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/MySQL8Test2.java#testAA | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:288 |  |
| mysql8/MySQL8Test3.java#testDTO1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:155 |  |
| mysql8/MySQL8Test4.java#testPartition2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:45：Java 的 DTO Proxy 逐列 set 时同名列也写 AS（t.'id' AS 'id'）；C# 的成员初始化与 Java 的 select(DTO.class) 一致，同名不写 | `SELECT t.'id' AS 'id',t.'name' AS 'name',t5.'type' AS 'third_card_type',t5.'code' AS 'third_card_cod…` |
| mysql8/MySQL8Test4.java#testPartition3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:64：Java 的 DTO Proxy 逐列 set 时同名列也写 AS（t.'id' AS 'id'）；C# 的成员初始化与 Java 的 select(DTO.class) 一致，同名不写 | `SELECT t.'id' AS 'id',t.'name' AS 'name',t5.'type' AS 'third_card_type',t5.'code' AS 'third_card_cod…` |
| mysql8/MySQL8Test4.java#testSubQueryCountCompareNull1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:84 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |

### 树（toTreeList / asTreeCTE）（`tree`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest11.java#testCTE1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:169 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:169 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:173 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:177 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:182 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:186 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:186 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:186 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:189 | `WITH RECURSIVE 'abc' AS ( (SELECT 0 AS 'xyz',t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_…` |
| mysql8/MySQL8Test2.java#cteViewTree1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:383 |  |
| mysql8/MySQL8Test2.java#cteViewTree2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:394 |  |
| mysql8/MySQL8Test3.java#testTree | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:256 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'parent_id',t1.'content',t1.'us…` |
| mysql8/MySQL8Test3.java#testTree_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:276 | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'parent_id',t1.'content',t1.'us…` |
| pgsql/QueryTest19.java#cteViewTree1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:383：PostgreSQL 版的同一用例（只执行不断言）；视图实体的表只建在 MySQL 夹具里，在这里一并验证 |  |
| pgsql/QueryTest19.java#testToTreeList | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:21 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#testToTreeList2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:33 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#testToTreeList3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:43 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#testToTreeListDTO | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:98 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:68 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree10_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:77 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:77 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:87 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t2."id",t2."parent_id",t2."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree7_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:113 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep",t1."id",t1."parent_id",t1."name" FROM "categor…` |
| pgsql/QueryTest19.java#tree7_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:126 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep1",t1."id",t1."parent_id",t1."name" FROM "catego…` |
| pgsql/QueryTest19.java#tree9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgQueryTest19.cs:57 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |

### 值转换、枚举、JSON 列（`column.converter`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/json/JsonValueConverterTest.java#columnIsBoundToTheConverter | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/JsonValueConverterTest.cs:34 |  |
| fork/json/JsonValueConverterTest.java#decodesWithTheDeclaredGenericType | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/JsonValueConverterTest.cs:13：Java 另外验证 JDBC 的 Clob；.NET 的驱动把文本列读成 string、二进制列读成 byte[]，这两种都验证 |  |
| fork/json/JsonValueConverterTest.java#nullAndBlankStayNull | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/JsonValueConverterTest.cs:26 |  |
| fork/live/JsonValueConverterLiveTest.java#hanaNclobColumn | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:207：Java 在 HANA 实库上往返 NCLOB 列；C# 只核对建表与写入的 SQL（JSON 序列化为字符串参数），实库验证留到 HANA 冒烟（ROADMAP 3.2） | `DROP TABLE "t_fork_profile"` |
| fork/live/JsonValueConverterLiveTest.java#mysql8018JsonColumn | 偏离 | tests/EasyQuerySharp.Tests.Live/LiveSuite.Json.cs:10：C# 的实库套件在每个库上跑同一份往返（MySQL 系的列改成 JSON 类型）；Dictionary<string, object?> 的值按 System.Text.Json 读成 JsonElement | `DROP TABLE IF EXISTS t_fork_profile` |
| fork/types/ZonedTimeColumnSqlTest.java#compareAndDateArithmetic | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/ZonedTimeColumnSqlTest.cs:15 | `SELECT 'id','kind','created_at','completed_at','due_at','seen_at' FROM 't_fork_event' WHERE 'created…` |
| fork/types/ZonedTimeColumnSqlTest.java#durationAggregateInGroupSelect | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/ZonedTimeColumnSqlTest.cs:33 | `SELECT t.'kind' AS 'value1',AVG(` |
| fork/types/ZonedTimeColumnSqlTest.java#proxyColumnsAreDateTimeColumns | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Fork/ZonedTimeColumnSqlTest.cs:8：测的是 Java 代理列的类型（SQLInstantTypeColumn 等）；C# 的 DateTimeOffset 属性直接用日期的成员和扩展方法，见本类其余用例 |  |
| fork/types/ZonedTimeColumnSqlTest.java#selectSingleZonedColumn | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/ZonedTimeColumnSqlTest.cs:24 | `SELECT t.'created_at' FROM 't_fork_event' t ORDER BY t.'created_at' ASC` |

### 列加密（`column.encryption`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EncryptionTest.java#phoneTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:13 |  |
| EncryptionTest.java#phoneTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:16 |  |
| EncryptionTest.java#phoneTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:25 |  |
| EncryptionTest.java#phoneTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:25 |  |
| EncryptionTest.java#phoneTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:25 |  |
| EncryptionTest.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:43 |  |
| EncryptionTest.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/EncryptionTest.cs:57 |  |
| Sm4EncryptionTest.java#sm4Test | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/Sm4EncryptionTest.cs:37 |  |

### 拦截器（`interceptor`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| InterceptorTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/InterceptorTest.cs:32 | `SELECT 'id','stars','title','create_time','create_by','update_time','update_by','tenant_id' FROM 't_…` |
| InterceptorTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/InterceptorTest.cs:44 | `SELECT 'id','stars','title','create_time','create_by','update_time','update_by','tenant_id' FROM 't_…` |
| InterceptorTest.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/InterceptorTest.cs:59 | `SELECT 'id','stars','title','create_time','create_by','update_time','update_by','tenant_id' FROM 't_…` |

### 逻辑删除（`logicDelete`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| LogicDeleteTest.java#Test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:12 | `SELECT 'id','stars','title','deleted','create_time' FROM 't_logic_del_topic' WHERE 'deleted' = ?` |
| LogicDeleteTest.java#Test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:91 | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |
| LogicDeleteTest.java#Test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:19 | `UPDATE 't_logic_del_topic' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| LogicDeleteTest.java#Test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:26 | `UPDATE 't_logic_del_topic' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| LogicDeleteTest.java#Test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:34 | `SELECT 'id','stars','title','deleted','create_time' FROM 't_logic_del_topic'` |
| LogicDeleteTest.java#Test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:41 | `DELETE FROM 't_logic_del_topic' WHERE 'id' = ?` |
| LogicDeleteTest.java#Test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:48 | `UPDATE 't_logic_del_topic' SET 'title' = ? WHERE 'id' = ?` |
| LogicDeleteTest.java#Test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:58 | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |
| LogicDeleteTest.java#Test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:66 | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |
| LogicDeleteTest.java#Test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/LogicDeleteTest.cs:78 | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |

### 聚合根保存（savable）（`save.aggregate`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mysql8/M8Save2Test.java#testOne2Many1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:63：异常信息用中文，说明哪个子对象的外键、当前值与应有的值 |  |
| mysql8/M8Save2Test.java#testOne2Many2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:83 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' IN (?,?)` |
| mysql8/M8Save2Test.java#testOne2Many3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:107：异常信息用中文，说明同一个子对象只能属于一个父对象 |  |
| mysql8/M8Save2Test.java#testOne2Many4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:124 | `DELETE FROM 'm8_save_root_many' WHERE 'id' = ?` |
| mysql8/M8Save2Test.java#testOne2Many5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:141 |  |
| mysql8/M8Save2Test.java#testOne2Many6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:156 |  |
| mysql8/M8Save2Test.java#testOne2OneChange | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:171 | `SELECT 'id','name','code' FROM 'm8_save_root'` |
| mysql8/M8Save2Test.java#testOne2OneChange2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:197 | `SELECT 'id','name','code' FROM 'm8_save_root'` |
| mysql8/M8Save2Test.java#testOne2OneChange3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save2Test.cs:221：异常信息用中文，说明哪个子对象的外键已指向别的父对象 |  |
| mysql8/M8Save3Test.java#many2manySetNullInsert | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save3Test.cs:154 | `INSERT INTO 'm8_role3' ('role_id','role_name') VALUES (?,?)` |
| mysql8/M8Save3Test.java#testOwnershipChange1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save3Test.cs:91 | `UPDATE 'm8_save_b' SET 'pid' = ? WHERE 'id' = ?` |
| mysql8/M8Save3Test.java#testOwnershipChange1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save3Test.cs:95 | `UPDATE 'm8_save_b' SET 'pid' = ? WHERE 'id' = ?` |
| mysql8/M8Save3Test.java#testOwnershipChange2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save3Test.cs:100 | `DELETE FROM 'm8_save_d' WHERE 'id' = ?` |
| mysql8/M8Save3Test.java#testOwnershipChange3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save3Test.cs:122 | `DELETE FROM 'm8_save_d' WHERE 'id' = ?` |
| mysql8/M8Save4Test.java#testToManyToMany1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save4Test.cs:37 |  |
| mysql8/M8Save4Test.java#testToManyToMany2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Save4Test.cs:57 | `SELECT 'a','b','c' FROM 'm8_many1' WHERE 'a' = ?` |
| mysql8/M8SaveTest.java#test2One1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:96 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#test2One2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:126：异常信息用中文，说明导航没有被追踪、该怎样改 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#test2One3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:151 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#test2One4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:193 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testDeleteAll | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:217 | `DELETE FROM 'm8_save_root_many_one' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:368 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:410 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:436：异常信息用中文，说明导航没有被追踪、该怎样改 |  |
| mysql8/M8SaveTest.java#testMany2Many4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:455 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:439：异常信息用中文，说明导航没有被追踪、该怎样改 |  |
| mysql8/M8SaveTest.java#testMany2Many6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:471 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:490 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2ManyMiddle1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:508 | `DELETE FROM 'm8_save_root_middle_many' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2ManyMiddle2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:526 |  |
| mysql8/M8SaveTest.java#testMany2ManyMiddle3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:540：异常信息用中文，说明是哪条导航上业务键相同 |  |
| mysql8/M8SaveTest.java#testOne2Many1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:240 |  |
| mysql8/M8SaveTest.java#testOne2Many2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:253 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many2_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:276 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:297 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:322 | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:341：异常信息用中文，说明导航没有被追踪、该怎样改 |  |
| mysql8/M8SaveTest.java#testOne2Many6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:355 |  |
| mysql8/M8SaveTest.java#testPathAggregateRoot | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8SaveTest.cs:557：异常信息用中文，说明列出的导航是别的聚合根 |  |

### 追踪与差异更新（`tracking`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EntityQueryAggregateTest1.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:230 | `SELECT SUM(t.'stars') FROM 't_topic' t` |
| EntityQueryAggregateTest6.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:59 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:78 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:97 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:116 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:135 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:154 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:173 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:192 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:211 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id'` |
| MyTest1.java#testSelect6_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:212 | `SELECT t.'id' AS 'id',COUNT(t1.'id') AS 'id_count' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'del…` |
| MyTest2.java#testSelect6_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/MyTest1.cs:212 | `SELECT t.'id' AS 'id',COUNT(t1.'id') AS 'id_count' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'del…` |
| QueryTest.java#query14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:122 |  |
| QueryTest27.java#testDiff | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:214 |  |
| QueryTest27.java#testDiff2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:225 |  |
| QueryTest28.java#testQueryAggregate | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:113：Java 的 where(..).sumBigDecimal 里 ELSE 的 0 是 Integer，分组上的 sum(..).filter 是 Long；C# 只有 Sql.Sum(x, 条件) 一个入口，统一用 Long | `SELECT COUNT(*) AS 'value1',SUM((CASE WHEN t.'title' LIKE CONCAT('%',?,'%') THEN t.'stars' ELSE ? EN…` |
| QueryTest28.java#testQueryAggregate1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:113：Java 的 where(..).sumBigDecimal 里 ELSE 的 0 是 Integer，分组上的 sum(..).filter 是 Long；C# 只有 Sql.Sum(x, 条件) 一个入口，统一用 Long | `SELECT COUNT(*) AS 'value1',SUM((CASE WHEN t.'title' LIKE CONCAT('%',?,'%') THEN t.'stars' ELSE ? EN…` |
| QueryTest6.java#joinTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:11 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:22 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:33 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:44 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:55 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:66 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest6.cs:77 | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest7.java#joinTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:11 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:22 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:33 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:44 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:55 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:66 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest7.cs:77 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| RelationTest.java#testOneToOne | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/RelationTest.OneToOne.cs:531：约 80 段拆成本文件的多个测试，Upstream 只标在这里。写法不同：include2 写成嵌套的 Include；flatElement 链的 toList 写成 SelectMany（INNER JOIN、保留重复，不比较 SQL，加 Distinct 与 Java 的去重对齐）。 | `SELECT 'id','name' FROM 'school_class'` |
| TrackTest.java#track1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/TrackTest.cs:21 |  |
| TrackTest.java#track2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/TrackTest.cs:36 |  |
| TrackTest.java#track3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/TrackTest.cs:51 |  |
| TrackTest.java#track4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/TrackTest.cs:60 |  |
| TrackTest.java#track5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/TrackTest.cs:73 |  |
| UpdateTest.java#updateTest17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:144 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'title' IS NULL` |
| UpdateTest.java#updateTest17_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:144 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'title' IS NULL` |
| UpdateTest.java#updateTest17_2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:157：C# 的 Topic.Alias 标了 ColumnIgnore（Java 映射了 alias 列），条件里少一个 'alias' IS NULL | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'stars' IS NULL AND 'create_time' IS NULL AND 'a…` |
| UpdateTest.java#updateTest20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:181 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:195 | `UPDATE 'x1234' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest21_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:195 | `UPDATE 'x1234' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:195 | `UPDATE 'x123' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest22_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:196 | `UPDATE 'x123' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:196 | `UPDATE 'x123' SET 'title' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest23_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:196 | `UPDATE 'x123' SET 'title' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:219 | `UPDATE 'x123' SET 'stars' = ?,'create_time' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest24_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:219 | `UPDATE 'x123' SET 'stars' = ?,'create_time' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest28 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:229 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest28_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:229 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest30 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:247 | `UPDATE 'x1234' SET 'content' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| UpdateTest.java#updateTest31 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:247 | `UPDATE 'x1234' SET 'content' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| UpdateTest.java#updateTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:64 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:64 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:64 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTrackParameterTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:355 | `UPDATE 'aaaxxx' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTrackParameterTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:373 | `UPDATE 'aaaxxx' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTrackParameterTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:373 | `UPDATE 'aaaxxx' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest1.java#updateTest51 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:75 |  |
| UpdateTest1.java#updateTestUpdate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:108 | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| mysql8/ManyJoinTest.java#testManyJoinGroup1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ManyJoinTest.cs:184 |  |

### 乐观锁版本（`version`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| VersionTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:32 |  |
| VersionTest.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:143 | `UPDATE 'AXX' SET 'username' = ?,'version' = ? WHERE 'deleted' = ? AND 'version' = ? AND 'id' = ?` |
| VersionTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:46 |  |
| VersionTest.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:62 | `UPDATE 't_sys_user_version' SET 'phone' = ? WHERE 'id' = ?` |
| VersionTest.java#test3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:74 | `UPDATE 't_sys_user_version' SET 'phone' = ?,'version' = ? WHERE 'version' = ? AND 'id' = ?` |
| VersionTest.java#test3_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:79 | `UPDATE 't_sys_user_version' SET 'phone' = ? WHERE 'id' = ?` |
| VersionTest.java#test3_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:84 |  |
| VersionTest.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:92 |  |
| VersionTest.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:102 |  |
| VersionTest.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:111 | `UPDATE 't_sys_user_version_del' SET 'deleted' = ?,'version' = ? WHERE 'deleted' = ? AND 'version' = …` |
| VersionTest.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:117 | `UPDATE 'aaa' SET 'username' = ?,'phone' = ?,'id_card' = ?,'address' = ?,'create_time' = ? WHERE 'del…` |
| VersionTest.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:126 |  |
| VersionTest.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/VersionTest.cs:133 | `UPDATE 'AXX' SET 'username' = ?,'version' = ? WHERE 'deleted' = ? AND 'version' = ? AND 'id' = ?` |

### UPDATE / DELETE 里用导航（`write.join`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| UpdateDeleteJoinTest.java#testDeleteJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateDeleteJoinTest.cs:27 | `DELETE t FROM 'doc_bank_card' t INNER JOIN 'doc_user' t1 ON t1.'id' = t.'uid' WHERE t1.'name' LIKE ?` |
| UpdateDeleteJoinTest.java#testDeleteJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateDeleteJoinTest.cs:27 | `DELETE t FROM 'doc_bank_card' t INNER JOIN 'doc_user' t1 ON t1.'id' = t.'uid' WHERE t1.'name' LIKE ?` |
| UpdateDeleteJoinTest.java#testUpdateJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateDeleteJoinTest.cs:9 | `UPDATE 'doc_bank_card' t INNER JOIN 'doc_user' t1 ON t1.'id' = t.'uid' SET t.'type' = t1.'name' WHER…` |
| UpdateDeleteJoinTest.java#testUpdateJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateDeleteJoinTest.cs:18 | `UPDATE 'doc_bank_card' t INNER JOIN 'doc_bank' t1 ON t1.'id' = t.'bank_id' INNER JOIN 'doc_user' t2 …` |
| pgsql/DeleteUpdateJoinTest.java#testDeleteJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgDeleteUpdateJoinTest.cs:60 | `DELETE FROM "doc_bank_card" t USING "doc_user" t1 WHERE t1."id" = t."uid" AND t1."name" LIKE ?` |
| pgsql/DeleteUpdateJoinTest.java#testDeleteJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgDeleteUpdateJoinTest.cs:60 | `DELETE FROM "doc_bank_card" t USING "doc_user" t1 WHERE t1."id" = t."uid" AND t1."name" LIKE ?` |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgDeleteUpdateJoinTest.cs:9 | `UPDATE "doc_bank_card" t SET "type" = t1."name" FROM "doc_user" t1 WHERE t1."id" = t."uid" AND t1."n…` |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgDeleteUpdateJoinTest.cs:18 | `UPDATE "doc_bank_card" t SET "type" = t1."name" FROM "doc_bank" t1 , "doc_user" t2 WHERE t1."id" = t…` |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgDeleteUpdateJoinTest.cs:29 |  |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgDeleteUpdateJoinTest.cs:45 |  |

### 写哪些列、批量执行（`write.strategy`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#testBatchDelete | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:108 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:55 | `UPDATE 'query_large_column_test' SET 'name' = ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:60 | `UPDATE 'query_large_column_test' SET 'content' = ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:65 |  |
| GenericWithDatabaseTest.java#queryLargeColumnTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:28 | `INSERT INTO 'query_large_column_test' ('id') VALUES (?)` |
| GenericWithDatabaseTest.java#queryLargeColumnTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:32 | `INSERT INTO 'query_large_column_test' ('id','name','content') VALUES (?,?,?)` |
| GenericWithDatabaseTest.java#queryLargeColumnTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:37 | `INSERT INTO 'query_large_column_test' ('name','content') VALUES (?,?)` |
| GenericWithDatabaseTest.java#queryLargeColumnTest9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:47 |  |
| InsertTest.java#insertBatch | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:109：Java 的 batch() 在 rewriteBatchedStatements 下返回 JDBC 的 SUCCESS_NO_INFO（-3）；C# 的批量返回真实行数 |  |
| InsertTest.java#mapInsertTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:142 | `INSERT INTO 'aaaaa' ('id','name') VALUES (?,?)` |
| InsertTest.java#mapInsertTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:150 | `INSERT INTO 'aaaaa' ('id','name','name1') VALUES (?,?,?)` |
| InsertTest.java#mapInsertTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:155 | `INSERT INTO 'aaaaa' ('id','name','name1','name2') VALUES (?,?,?,?)` |
| InsertTest.java#testIncrement | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:161 | `truncate table t_test_inc;` |
| InsertTest1.java#tesInsertMapKey2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest1.cs:73 | `INSERT INTO "xxx" ("ID","STARS","USERNAME") VALUES (?,ifnull("STARS",0)+?,?)` |
| UpdateTest.java#mapUpdateTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:267 | `UPDATE 'aaa' SET 'name' = ? WHERE 'id' = ?` |
| UpdateTest.java#mapUpdateTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:272 | `UPDATE 'aaa' SET 'name' = ?,'name1' = ? WHERE 'id' = ?` |
| UpdateTest.java#mapUpdateTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:277 |  |
| UpdateTest.java#mapUpdateTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:284 | `UPDATE 't_topic' SET 'stars' = ? WHERE 'id' = ?` |
| UpdateTest.java#mapUpdateTest511 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:296 |  |
| UpdateTest.java#testColumnConfigure1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:436 | `INSERT INTO 'xxxxx' ('id','stars','title','create_time') VALUES (?,ifnull('stars',0)+?,?,?)` |
| UpdateTest.java#updateTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:82 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:91 | `UPDATE 't_topic' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTestBatch | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:310 |  |
| UpdateTest.java#updateTestBatch1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:318 |  |
| UpdateTest.java#updateTestBatch2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:326 |  |
| mysql8/MySQL8Test3.java#batchTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:133 |  |

### upsert（`write.upsert`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:143 | `INSERT INTO 'doc_bank_card' ('id','code','type') VALUES (?,?,?) ON DUPLICATE KEY UPDATE 'id' = VALUE…` |
| GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Functions/GeneralFunctionsTest.cs:149：异常文本不同：Java 是英文的 [MySQL INSERT IGNORE INTO TO DUPLICATE KEY UPDATE] No non-auto-increment primary key found. |  |
| InsertOrUpdateTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertOrUpdateTest.cs:27 | `MERGE INTO [t_user] t1 USING (SELECT ? AS [column1],? AS [column2],? AS [column3],? AS [column4],? A…` |
| InsertTest.java#insertDuplicateKeyUpdate1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:62 | `INSERT INTO 't_topic_auto' ('stars','title','create_time') VALUES (?,?,?) ON DUPLICATE KEY UPDATE 's…` |
| InsertTest.java#insertDuplicateKeyUpdate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:67 | `INSERT INTO 't_topic_auto' ('stars','title','create_time') VALUES (?,?,?) ON DUPLICATE KEY UPDATE 's…` |
| InsertTest.java#insertTestx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:93 |  |
| InsertTest1.java#inertTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest1.cs:54 | `INSERT INTO 't_topic_abc' ('id','stars','title','create_time') VALUES (?,?,?,?) ON DUPLICATE KEY UPD…` |
| QueryTest14.java#testxxxxx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertOrUpdateTest.cs:35 | `MERGE INTO "t_topic" t1 USING (SELECT ? AS "id",? AS "stars",? AS "title",? AS "create_time" FROM DU…` |
| pgsql/InsertTest.java#insertDuplicateKeyIgnore2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgInsertTest.cs:18 | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT DO NOTHING` |
| pgsql/InsertTest.java#insertDuplicateKeyUpdate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgInsertTest.cs:23 | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT ("title") DO U…` |
| pgsql/InsertTest.java#insertDuplicateKeyUpdate2_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgInsertTest.cs:28 | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT ("title") DO N…` |
| pgsql/InsertTest.java#insertDuplicateKeyUpdate3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgInsertTest.cs:32 | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT ("title") DO U…` |
| pgsql/InsertTest.java#insertTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgInsertTest.cs:44 |  |

### ClickHouse 方言（`dialect.clickhouse`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| click/ClickHouseTest.java#testDelete1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ClickHouseTest.cs:27 | `ALTER TABLE 't_topic' DELETE WHERE 'ID' = ?` |
| click/ClickHouseTest.java#testDelete2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ClickHouseTest.cs:32 | `ALTER TABLE 't_topic' DELETE WHERE 'ID' = ?` |
| click/ClickHouseTest.java#testUpdate1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ClickHouseTest.cs:17 | `ALTER TABLE 't_topic' UPDATE 'TITLE' = ? WHERE 'ID' = ?` |
| click/ClickHouseTest.java#testUpdate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dialects/ClickHouseTest.cs:22 | `ALTER TABLE 't_topic' UPDATE 'STARS' = ?,'TITLE' = ?,'CREATE_TIME' = ? WHERE 'ID' = ?` |

### 达梦方言（`dialect.dameng`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| dameng/DamengForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:53 |  |
| dameng/DamengForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 骨架 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:48 |  |
| dameng/DamengForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:66 |  |
| dameng/DamengForUpdateTest.java#testForUpdateDuplicateCallThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:39 |  |
| dameng/DamengForUpdateTest.java#testForUpdateJoinNotSupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:31 |  |
| dameng/DamengForUpdateTest.java#testForUpdateSqlAppendInTransaction | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:15 |  |
| dameng/DamengForUpdateTest.java#testForUpdateWithoutTransactionThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengForUpdateTest.cs:24 |  |
| dameng/DamengQueryTest.java#concat1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:21：Java 用原生 SQL 在实库上验证 \|\| 遇到 NULL 当空串；C# 核对字符串拼接写成 \|\|，实库验证在 3.1 冒烟里做 | `select 'a' \|\| null \|\| 'b' as "aa" from DUAL` |
| dameng/DamengQueryTest.java#cteViewTree1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:27：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#query0 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:35 | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE "ID" = ?` |
| dameng/DamengQueryTest.java#query0_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:38 | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE "ID" = ? AND ROWNUM < 2` |
| dameng/DamengQueryTest.java#query1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:41 | `SELECT rt.* FROM(SELECT "ID","STARS","TITLE","CREATE_TIME", ROWNUM AS "__rownum__" FROM "MY_TOPIC" W…` |
| dameng/DamengQueryTest.java#query16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:94 | `SELECT COUNT(*) FROM (SELECT COUNT(*) AS "STARS" FROM "MY_TOPIC" t LEFT JOIN "MY_TOPIC" t1 ON t."ID"…` |
| dameng/DamengQueryTest.java#query17 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:106：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#query2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:46 | `SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT "ID","STARS","TITLE","CREATE_TIM…` |
| dameng/DamengQueryTest.java#query3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:51 | `SELECT rt.* FROM (SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE "ID" = ? ORDER BY …` |
| dameng/DamengQueryTest.java#query4 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:57：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#query5 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:66：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#query6 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:77：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#query7 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:86：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#queryFormat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:113 | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY-MM-"01"') AS "VALUE2" FROM "MY_TOPI…` |
| dameng/DamengQueryTest.java#queryFormat2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:114 | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY"年"MM-"01"') AS "VALUE2" FROM "MY_TO…` |
| dameng/DamengQueryTest.java#queryFormat3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:115 | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY"年"MM-"01" HH24"时"MI"分"SS"秒"') AS "V…` |
| dameng/DamengQueryTest.java#queryFormat4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:116 | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY"年"MM-"01" HH24:MI"分"SS"秒"') AS "VAL…` |
| dameng/DamengQueryTest.java#subQueryTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:122 | `SELECT t."ID",t."STARS",t."TITLE",t."CREATE_TIME" FROM "MY_TOPIC" t LEFT JOIN (SELECT t1."TITLE" AS …` |
| dameng/DamengQueryTest.java#test1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:129：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#test111 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:139：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#testAll | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:145：Java 还断言带出银行卡的第二条语句与结果：要先在实库上读出用户才会发出，C# 只核对第一条，带出与结果在 3.1 冒烟里验证 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t WHERE NOT (EXISTS(SELEC…` |
| dameng/DamengQueryTest.java#testAll1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:152：Java 还断言带出银行卡的第二条语句与结果：要先在实库上读出用户才会发出，C# 只核对第一条，带出与结果在 3.1 冒烟里验证 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t WHERE NOT (EXISTS(SELEC…` |
| dameng/DamengQueryTest.java#testAll2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:159：Java 还断言带出银行卡的第二条语句与结果：要先在实库上读出用户才会发出，C# 只核对第一条，带出与结果在 3.1 冒烟里验证 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t WHERE NOT (EXISTS(SELEC…` |
| dameng/DamengQueryTest.java#testAllGroupJoin | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:166：Java 还断言带出银行卡的第二条语句与结果：要先在实库上读出用户才会发出，C# 只核对第一条，带出与结果在 3.1 冒烟里验证 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testAllGroupJoin2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:173：Java 还断言带出银行卡的第二条语句与结果：要先在实库上读出用户才会发出，C# 只核对第一条，带出与结果在 3.1 冒烟里验证 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testBooleanValue | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:183 | `SELECT (CASE WHEN (EXISTS(SELECT 1 FROM "t_bank_card" t2 WHERE t2."UID" = t."ID" AND ROWNUM < 2)) TH…` |
| dameng/DamengQueryTest.java#testBooleanValue2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:183 | `SELECT (CASE WHEN (EXISTS(SELECT 1 FROM "t_bank_card" t2 WHERE t2."UID" = t."ID" AND ROWNUM < 2)) TH…` |
| dameng/DamengQueryTest.java#testDraft9 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:189：Java 在达梦实库上断言结果；C# 只核对列与列之间的加减、相差渲染出的 SQL（两个常量之间的 C# 在本地算好，不写进 SQL），实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#testEasyQuery | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:203：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#testJoin1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:209：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/DamengQueryTest.java#testJoining | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:215 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testJoining1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:222 | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testMaxColumns1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:230 | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE (SELECT MAX(__mmc.__col__) FROM (SEL…` |
| dameng/DamengQueryTest.java#testMaxColumns2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:236 | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE (SELECT MAX(__mmc.__col__) FROM (SEL…` |
| dameng/DamengQueryTest.java#testMaxColumns3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:243 | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE (SELECT MIN(__mmc.__col__) FROM (SEL…` |
| dameng/DamengQueryTest.java#testQuery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:250 | `SELECT t."ID",t."NAME",t."ISDELETE" AS "IS_DELETE",t."ROWNUMBER" AS "ROW_NUMBER" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testQueryAlias | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:255 | `SELECT t."ID",t."NAME",t."ROWNUMBER" AS "ROW_NUMBER",t."ISDELETE" AS "IS_DELETE" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testQueryAlias2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:260 | `SELECT t."ID",t."NAME",t."ROWNUMBER",t."ISDELETE" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testQueryAlias3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:264 | `SELECT t."ID",t."NAME",t."ROWNUMBER",t."ISDELETE" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testaa | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengQueryTest.cs:270：Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做 |  |
| dameng/MathQueryTest.java#testPropertyMath1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengMathQueryTest.cs:6：Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写；Java 还在实库 10000 行随机数上比对结果，实库验证在 3.1 冒烟里做 | `SELECT t."TEST_VALUE" AS "TEST_VALUE",ABS(t."TEST_VALUE") AS "TEST_ABS",SIGN(t."TEST_VALUE") AS "TES…` |
| dameng/UpdateJoinTest.java#testTopicInsertOrUpdate | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengUpdateJoinTest.cs:10：Java 在达梦实库上执行（不断言）；C# 只核对渲染出的 MERGE，实库验证在 3.1 冒烟里做 |  |
| dameng/UpdateJoinTest.java#testUpdateJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengUpdateJoinTest.cs:16 | `UPDATE "doc_bank_card" t INNER JOIN "doc_user" t1 ON t1."ID" = t."UID" SET t."TYPE" = t1."NAME" WHER…` |
| dameng/UpdateJoinTest.java#testUpdateJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Dameng/DamengUpdateJoinTest.cs:21 | `UPDATE "doc_bank_card" t INNER JOIN "doc_bank" t1 ON t1."ID" = t."BANK_ID" INNER JOIN "doc_user" t2 …` |

### DuckDB 方言（`dialect.duckdb`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| duckdb/AllTypeTest.java#testAllType | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/AllTypeTest.cs:41 |  |
| duckdb/AllTypeTest.java#testAllType1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/AllTypeTest.cs:75 |  |
| duckdb/DuckDBMathQueryTest.java#testPropertyMath | 偏离 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBMathQueryTest.cs:13：C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal）；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写 | `SELECT t."test_value" AS "test_value",ABS(t."test_value") AS "test_abs",SIGN(t."test_value") AS "tes…` |
| duckdb/DuckDBQueryTest.java#datetimeformat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:60 | `SELECT t."create_time" AS "value1",strftime(t."create_time", '%Y年%m-01 %H:%M分%S秒') AS "value2" FROM …` |
| duckdb/DuckDBQueryTest.java#datetimeformat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:60 | `SELECT t."create_time" AS "value1",strftime(t."create_time", '%Y年%m-01 %H:%M分%S秒') AS "value2" FROM …` |
| duckdb/DuckDBQueryTest.java#formatMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:72 | `SELECT t."create_time" AS "value1",strftime(t."create_time", '%Y年%m-01 %H:%M分%S秒') AS "value2",strft…` |
| duckdb/DuckDBQueryTest.java#readExcel1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:124：只核对 SQL、不执行：read_xlsx 在 excel 扩展里（要联网安装），还要有 ducktest.xlsx；Java 的表函数写在 @Table 里，C# 用 AsTableSegment 替换表引用 | `SELECT "id","name","age" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') WHERE "name" = ?` |
| duckdb/DuckDBQueryTest.java#readExcel2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:131：只核对 SQL、不执行：read_xlsx 在 excel 扩展里（要联网安装），还要有 ducktest.xlsx；Java 的表函数写在 @Table 里，C# 用 AsTableSegment 替换表引用 | `SELECT t."id",t."name",t."age" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t WHERE EXISTS(SELEC…` |
| duckdb/DuckDBQueryTest.java#readExcel3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:141：只核对 SQL、不执行：read_xlsx 在 excel 扩展里（要联网安装），还要有 ducktest.xlsx；Java 的表函数写在 @Table 里，C# 用 AsTableSegment 替换表引用 | `SELECT t."id",t."name",t."age" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t LEFT JOIN (SELECT …` |
| duckdb/DuckDBQueryTest.java#readExcel4 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:152：只核对 SQL、不执行：read_xlsx 在 excel 扩展里（要联网安装），还要有 ducktest.xlsx；Java 的表函数写在 @Table 里，C# 用 AsTableSegment 替换表引用 | `SELECT t."name" AS "value1",SUBSTR(t."name",5,1) AS "value2" FROM read_xlsx('./ducktest.xlsx',sheet=…` |
| duckdb/DuckDBQueryTest.java#testConcat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:94 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| duckdb/DuckDBQueryTest.java#testConcat2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:107：Java 断言 EasyQueryInvalidOperationException 和它的文本（Mismatch: provided 3 arguments…），C# 只核对翻译时抛出异常 |  |
| duckdb/DuckDBQueryTest.java#testConcat3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:94 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| duckdb/DuckDBQueryTest.java#testDraft9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:10 |  |
| duckdb/DuckDBQueryTest.java#testDraft9_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBQueryTest.cs:10 |  |
| duckdb/M8Save4Test.java#testToManyToMany1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/DuckDb/DuckDBM8Save4Test.cs:29 |  |

### 方言静态入口与专属函数（`dialect.functions`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/dialect/DialectEntrySqlTest.java#mysqlFullTextAgainstAndScore | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:40 | `SELECT t.'id' AS 'value1',MATCH(t.'name') AGAINST(? IN NATURAL LANGUAGE MODE) AS 'value2' FROM 't_fo…` |
| fork/dialect/DialectEntrySqlTest.java#mysqlJsonPredicatesInWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:23 | `SELECT {…} FROM 't_fork_user' WHERE JSON_CONTAINS('attrs',?,?) AND ? MEMBER OF(JSON_EXTRACT('attrs',…` |
| fork/dialect/DialectEntrySqlTest.java#mysqlPredicateInsideImplicitSubquery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:35 | `SELECT t.{…} FROM 't_fork_user' t WHERE EXISTS(SELECT 1 FROM 't_fork_post' t1 WHERE t1.'user_id' = t…` |
| fork/dialect/DialectEntrySqlTest.java#mysqlPredicateRespectsOrGroup | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:30 | `SELECT {…} FROM 't_fork_user' WHERE 'name' = ? AND (JSON_CONTAINS('attrs',?) OR 'age' > ?)` |
| fork/dialect/DialectEntrySqlTest.java#pgJsonbPredicates | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:49 | `SELECT "id","name","age","company_id","attrs","created_at" FROM "t_fork_user" WHERE ("attrs")::jsonb…` |
| fork/dialect/DialectEntrySqlTest.java#userDefinedFunctionHasTheSameShape | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:68 | `SELECT t.{…} FROM 't_fork_user' t WHERE EXISTS(SELECT 1 FROM 't_fork_post' t1 WHERE t1.'user_id' = t…` |
| fork/dialect/DialectEntrySqlTest.java#userDefinedValueFunctionChainsLikeBuiltIns | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:63 | `SELECT {…} FROM 't_fork_user' WHERE CHAR_LENGTH('name') > ? ORDER BY CHAR_LENGTH('name') DESC` |
| fork/dialect/DialectEntrySqlTest.java#wrongDialectFailsWhenTheExpressionIsBuilt | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:56 |  |
| fork/dialect/HanaTextSqlTest.java#containsAndFuzzy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:90 |  |
| fork/dialect/HanaTextSqlTest.java#wrongDialectAndBadThreshold | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:105 |  |
| fork/dialect/MySQLJsonSqlTest.java#containsPath | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:73 | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user' WHERE JSON_CONTAINS_PA…` |
| fork/dialect/MySQLJsonSqlTest.java#modificationsChainIntoUpdate | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:82 | `UPDATE 't_fork_user' SET 'attrs' = JSON_REMOVE(JSON_SET('attrs',?,?),?,?) WHERE 'id' = ?` |
| fork/dialect/PgSQLM4SqlTest.java#arrayAndFullText | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:94 |  |
| fork/dialect/PgSQLM4SqlTest.java#jsonbEdit | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntrySqlTest.cs:88 | `UPDATE "t_fork_user" SET "attrs" = (jsonb_set(("attrs")::jsonb,(?)::text[],(?)::jsonb,true) #- (?)::…` |
| fork/live/HanaTextLiveTest.java#containsFuzzyAndScore | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:179：Java 在 HANA 实库上建全文索引后检索；C# 只核对检索与删表的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） | `DROP TABLE "t_fork_post"` |
| fork/live/MySQLDialectEntryLiveTest.java#fullTextAgainstAndScore | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:75 |  |
| fork/live/MySQLDialectEntryLiveTest.java#insideImplicitSubquery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:68 |  |
| fork/live/MySQLDialectEntryLiveTest.java#jsonContainsWithPath | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:53 |  |
| fork/live/MySQLDialectEntryLiveTest.java#memberOfAndOverlaps | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:60 |  |
| fork/live/MySQLJsonLiveTest.java#pathPredicatesAndUpdates | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:85 | `DROP TABLE IF EXISTS t_fork_user` |
| fork/live/PgSQLDialectEntryLiveTest.java#containsAndContainedBy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:103 |  |
| fork/live/PgSQLDialectEntryLiveTest.java#insideImplicitSubquery | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:120 |  |
| fork/live/PgSQLDialectEntryLiveTest.java#keyExistence | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:111 |  |
| fork/live/PgSQLM4LiveTest.java#arrayPredicatesAndFullText | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:142 | `DROP TABLE "t_fork_post"` |
| fork/live/PgSQLM4LiveTest.java#jsonbEditRoundTrip | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/DialectEntryLiveTest.cs:127 | `DROP TABLE "t_fork_user"` |

### H2 方言（`dialect.h2`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| h2/BasicTypeTest.java#booleanTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:34 | `SELECT ? FROM t_all_type t LIMIT 1` |
| h2/BasicTypeTest.java#booleanTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:43 | `SELECT t.enable FROM t_all_type t WHERE t.enable = ? LIMIT 1` |
| h2/BasicTypeTest.java#booleanTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:52 | `SELECT t.enable_basic FROM t_all_type t WHERE t.enable_basic = ? LIMIT 1` |
| h2/BasicTypeTest.java#byteTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:130 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#byteTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:133 | `SELECT t.number_byte FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#byteTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:136 | `SELECT t.number_byte_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#doubleTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:61 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#doubleTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:64 | `SELECT t.number_double FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#doubleTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:67 | `SELECT t.number_double_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#floatTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:70 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#floatTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:73 | `SELECT t.number_float FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#floatTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:76 | `SELECT t.number_float_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#intTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:79 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#intTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:82 | `SELECT t.number_integer FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#intTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:85 | `SELECT t.number_integer_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:112 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:115 | `SELECT t.time_local_date FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTimeTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:106 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTimeTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:109 | `SELECT t.time_local_date_time FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localTimeTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:118 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localTimeTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:121 | `SELECT t.time_local_time FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#longTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:88 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#longTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:91 | `SELECT t.number_long FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#longTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:94 | `SELECT t.number_long_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#shortTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:97 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#shortTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:100 | `SELECT t.number_short FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#shortTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:103 | `SELECT t.number_short_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#timeTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:124 | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#timeTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/BasicTypeTest.cs:127 | `SELECT t.only_time FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/H2DocTest.java#selectClassTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:13 | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectClassTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:39 | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectClassTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:52 | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectClassTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:65 | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectClassTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:82 | `SELECT t.id,t.name,t.phone,t.age,IFNULL(t.age,1) AS card_count FROM doc_user t WHERE t.name LIKE ?` |
| h2/H2DocTest.java#selectProxyTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:13 | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectProxyTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:27 | `SELECT t.id,t.code FROM doc_bank_card t WHERE t.type = ?` |
| h2/H2DocTest.java#selectProxyTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:39 | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectProxyTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:52 | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectProxyTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:65 | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectProxyTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:82 | `SELECT t.id,t.name,t.phone,t.age,IFNULL(t.age,1) AS card_count FROM doc_user t WHERE t.name LIKE ?` |
| h2/H2DocTest.java#testToOne | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:95 | `SELECT id,name,phone,age FROM doc_user WHERE name LIKE ? AND id IS NULL AND id IS NOT NULL` |
| h2/H2DocTest.java#testToOne1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2DocTest.cs:105 | `SELECT t.id,t.uid,t.code,t.type,t.bank_id FROM doc_bank_card t LEFT JOIN doc_user t1 ON t1.id = t.ui…` |
| h2/H2ForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 不适用 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:45：测 H2 渲染的 FOR UPDATE 与行锁；SQLite 没有行锁语法，同样的用例在 mysql8/ForUpdateTest、pgsql/PgSQLForUpdateTest 已移植 |  |
| h2/H2ForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 不适用 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:40：测 H2 渲染的 FOR UPDATE 与行锁；SQLite 没有行锁语法，同样的用例在 mysql8/ForUpdateTest、pgsql/PgSQLForUpdateTest 已移植 |  |
| h2/H2ForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 不适用 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:50：测 H2 渲染的 FOR UPDATE 与行锁；SQLite 没有行锁语法，同样的用例在 mysql8/ForUpdateTest、pgsql/PgSQLForUpdateTest 已移植 |  |
| h2/H2ForUpdateTest.java#testForUpdateDuplicateCallThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:32 |  |
| h2/H2ForUpdateTest.java#testForUpdateJoinNotSupported | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:24 |  |
| h2/H2ForUpdateTest.java#testForUpdateSqlAppendInTransaction | 不适用 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:12：测 H2 渲染的 FOR UPDATE 与行锁；SQLite 没有行锁语法，同样的用例在 mysql8/ForUpdateTest、pgsql/PgSQLForUpdateTest 已移植 |  |
| h2/H2ForUpdateTest.java#testForUpdateWithoutTransactionThrows | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2ForUpdateTest.cs:17 |  |
| h2/H2OrderTest.java#test1 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/H2/H2OrderTest.cs:10 |  |
| h2/H2OrderTest.java#test2 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/H2/H2OrderTest.cs:14 |  |
| h2/H2QueryRelationTest.java#tbTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryRelationTest.cs:11 | `SELECT id,uid,uname,price FROM t_tb_order` |
| h2/H2QueryTest.java#allTypeTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:110 |  |
| h2/H2QueryTest.java#allTypeTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:113 |  |
| h2/H2QueryTest.java#allTypeTest1_1_proxy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:122 |  |
| h2/H2QueryTest.java#allTypeTest1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:116 |  |
| h2/H2QueryTest.java#allTypeTest1_2_proxy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:125 |  |
| h2/H2QueryTest.java#allTypeTest1_proxy | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:119 |  |
| h2/H2QueryTest.java#allTypeTestSharding1 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:129 |  |
| h2/H2QueryTest.java#conditionTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:134 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:18 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:26 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:34 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin2_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:43 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:52 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:61 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:70 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin4_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:80 | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#testDDL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:158：建表语句由 SQLite 方言生成，格式与 Java 的 H2 一致，类型是 SQLite 的（integer、TEXT）；Java 是 H2 的类型（INT、TIMESTAMP(3)） | `CREATE TABLE IF NOT EXISTS t_topic ( id VARCHAR(255) NOT NULL , stars INT NULL , title VARCHAR(255) …` |
| h2/H2QueryTest.java#testDDL2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:167 | `CREATE TABLE IF NOT EXISTS t_multi_primary ( id1 VARCHAR(255) NOT NULL , id2 VARCHAR(255) NOT NULL ,…` |
| h2/H2QueryTest.java#testTableCommentDDL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/H2/H2QueryTest.cs:178：SQLite 不支持表注释与列注释，建表语句里没有 COMMENT；Java 断言 H2 的 COMMENT ON TABLE 语句 | `CREATE TABLE IF NOT EXISTS t_h2_table_comment ( id VARCHAR(255) NOT NULL COMMENT '主键ID', name VARCHA…` |
| h2/TreeCTETest.java#tree8 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/H2/TreeCTETest.cs:9：SQLite 的复合查询分支不能加括号，递归 CTE 也不写列名清单；Java 的 H2 写成 as_tree_cte(cte_deep,id,parent_id,name) AS ( (…)  UNION ALL  (…) ) | `WITH RECURSIVE as_tree_cte(cte_deep,id,parent_id,name) AS ( (SELECT 0 AS cte_deep,t1.id,t1.parent_id…` |
| h2/v2/H2V2Test.java#testAllType | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2V2Test.cs:29 |  |
| h2/v2/H2V2Test.java#testAllType1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/H2/H2V2Test.cs:40 |  |

### SAP HANA 方言（`dialect.hana`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/dialect/HanaSqlTest.java#dateFormatTranslation | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:49 |  |
| fork/dialect/HanaSqlTest.java#existsSubqueryHasNoLimit | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:28 | `SELECT {…} FROM "t_fork_user" t LEFT JOIN "t_fork_company" t1 ON t1."id" = t."company_id" WHERE t1."…` |
| fork/dialect/HanaSqlTest.java#functions | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:36 | `SELECT ((t."name" \|\| ?) \|\| t."id") AS "value1",SUBSTRING(t."name",1,4) AS "value2",(LOCATE(t."name",…` |
| fork/dialect/HanaSqlTest.java#joinedUpdateIsMergeAndJoinedDeleteIsExists | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:61 | `MERGE INTO "t_fork_user" t USING (SELECT t."id" AS "eq_key_0",? AS "eq_value_0" FROM "t_fork_user" t…` |
| fork/dialect/HanaSqlTest.java#pagingUsesLimitOffset | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:18 | `SELECT "id","name","age","company_id","attrs","created_at" FROM "t_fork_user" WHERE "name" LIKE ? OR…` |
| fork/dialect/HanaSqlTest.java#upsertIsMerge | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaSqlTest.cs:73 | `MERGE INTO "t_fork_user" t1 USING (SELECT ? AS "id",? AS "name" FROM DUMMY) t2 ON (t1."id" = t2."id"…` |
| fork/live/HanaDialectLiveTest.java#aggregatesAndBooleanProjection | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:112：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#dateFunctions | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:96：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#joinedUpdateAndDelete | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:49：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#likeEscapesWildcards | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:63：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#mergeUpsert | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:37：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#pagingImplicitJoinAndSubquery | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:76：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#plainCrudAndForUpdate | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:19：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaDialectLiveTest.java#stringAndNullFunctions | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:84：Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaKeysAndTypesLiveTest.java#generatedKeyIsFilledBack | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:143：Java 在 HANA 实库上插入并读回生成的键；C# 只核对插入语句、取键的查询与建表语句，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaKeysAndTypesLiveTest.java#generatedKeysForBatch | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:154：Java 在 HANA 实库上插入并读回生成的键；C# 只核对插入语句、取键的查询与建表语句，实库验证留到 HANA 冒烟（ROADMAP 3.2） |  |
| fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeRoundTrip | 骨架 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:162 |  |
| fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeSurvivesDstGapOfJvmZone | 骨架 | tests/EasyQuerySharp.Tests.Upstream/Fork/HanaLiveTest.cs:168 |  |

### 人大金仓方言（`dialect.kingbase`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| kingbase/MyKingbaseESTest.java#testaa | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Kingbase/MyKingbaseESTest.cs:38：Java 连开发者本机 MySQL 上的现成数据（3 个专题、1074 个视频），这里改在 SQLite 内存库里造同样规模的数据（视频的 order_by 与键的顺序相反）：SQL 只差引号，第三条的参数是造的键 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','delete_by','delete_time',…` |
| kingbase/MyKingbaseESTest.java#testaa1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Kingbase/MyKingbaseESTest.cs:49：Java 连开发者本机 MySQL 上的现成数据（3 个专题、1074 个视频），这里改在 SQLite 内存库里造同样规模的数据（视频的 order_by 与键的顺序相反）：SQL 只差引号，第三条的参数是造的键 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','delete_by','delete_time',…` |

### SQL Server 方言（`dialect.mssql`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mssql/MSSQLMathQueryTest.java#testPropertyMath | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MSSQLMathQueryTest.cs:15：C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal），SQL Server 的 atan2 模板里同一个参数写六次；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写；Java 把 floor 写成与 truncate 相同的 CASE（对负数是 Java 的 bug），C# 写 FLOOR | `SELECT t.[TestValue] AS [TestValue],ABS(t.[TestValue]) AS [TestAbs],SIGN(t.[TestValue]) AS [TestSign…` |
| mssql/MSSQLTest1.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MSSQLTest1.cs:9 | `SELECT t2.[code] AS [code],t2.[name] AS [name] FROM (SELECT t.[Code] AS [code],t1.[Code] AS [name] F…` |
| mssql/MsSQLQueryTest.java#concat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:255 | `select CONCAT('a', null , 'b') as [aa]` |
| mssql/MsSQLQueryTest.java#query0 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:14 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:22 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:90 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (SUBSTRING(CONVERT(CHAR(8), [CreateTim…` |
| mssql/MsSQLQueryTest.java#query11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:100 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (SUBSTRING(CONVERT(CHAR(8), [CreateTim…` |
| mssql/MsSQLQueryTest.java#query2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:14 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:22 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:36 |  |
| mssql/MsSQLQueryTest.java#query5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:43 |  |
| mssql/MsSQLQueryTest.java#query6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:50 |  |
| mssql/MsSQLQueryTest.java#query7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:62 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (CASE WHEN [Title] = ? THEN 0 WHEN [Ti…` |
| mssql/MsSQLQueryTest.java#query8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:71 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (CASE WHEN [Title] = [Id] THEN 0 WHEN …` |
| mssql/MsSQLQueryTest.java#query9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:80 | `SELECT t.[Title] AS [Value1],STRING_AGG(t.[Id], ?) AS [Value2] FROM [MyTopic] t GROUP BY t.[Title]` |
| mssql/MsSQLQueryTest.java#testAll | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:48 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WHERE NOT (EXISTS(SELECT…` |
| mssql/MsSQLQueryTest.java#testAll1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:62 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WHERE NOT (EXISTS(SELECT…` |
| mssql/MsSQLQueryTest.java#testAll2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:76 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WHERE NOT (EXISTS(SELECT…` |
| mssql/MsSQLQueryTest.java#testAllGroupJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:90 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t LEFT JOIN (SELECT t1.[Ui…` |
| mssql/MsSQLQueryTest.java#testAllGroupJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:104 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t LEFT JOIN (SELECT t1.[Ui…` |
| mssql/MsSQLQueryTest.java#testBatchInsert | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:200 |  |
| mssql/MsSQLQueryTest.java#testBigDecima2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:157 |  |
| mssql/MsSQLQueryTest.java#testBigDecima4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:178 |  |
| mssql/MsSQLQueryTest.java#testBigDecimal | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:137 |  |
| mssql/MsSQLQueryTest.java#testFormat | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:111 | `SELECT t.[CreateTime] AS [Value1],(SUBSTRING(CONVERT(CHAR(8), t.[CreateTime], 112), 1, 4) + ? + SUBS…` |
| mssql/MsSQLQueryTest.java#testLikeConcat1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:10 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Title] LIKE ('%'+CAST(CONCAT(?,[Id],?…` |
| mssql/MsSQLQueryTest.java#testLikeConcat2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:19 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Title] LIKE (CAST(CONCAT(?,[Id],?) AS…` |
| mssql/MsSQLQueryTest.java#testLikeConcat3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.Like.cs:28 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Title] LIKE ('%'+CAST(CONCAT(?,[Id],?…` |
| mssql/MsSQLQueryTest.java#testMaxColumns | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:220 | `SELECT [Id],[Column1],[Column2],[Column3] FROM [t_calc] WHERE (SELECT MAX(__col__) FROM (VALUES ([Co…` |
| mssql/MsSQLQueryTest.java#testMaxColumns2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:232 | `SELECT t.[Column1] AS [Value1],t.[Column2] AS [Value2],t.[Column3] AS [Value3],(SELECT MAX(__col__) …` |
| mssql/MsSQLQueryTest.java#testWithIndex | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:269 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WITH(INDEX(myIndex)) LEF…` |
| mssql/MsSQLQueryTest.java#testWithNolock1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:279 | `SELECT t.[Id],t.[Uid],t.[Code],t.[Type],t.[BankId],t.[OpenTime] FROM [t_bank_card] t WITH(NOLOCK) LE…` |
| mssql/MsSQLQueryTest.java#testWithNolock2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:290 | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WITH(NOLOCK) LEFT JOIN […` |
| mssql/MsSQLQueryTest.java#update1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLQueryTest.cs:123 | `UPDATE t SET t.[Title] = ? FROM [MyTopic] t WHERE t.[Id] IS NULL AND EXISTS(SELECT 1 FROM [MyTopic] …` |
| mssql/MsSQLUpdateJoinTest.java#testDeleteJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLUpdateJoinTest.cs:27 | `DELETE t FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Uid] WHERE t1.[Name] LIKE ?` |
| mssql/MsSQLUpdateJoinTest.java#testDeleteJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLUpdateJoinTest.cs:27 | `DELETE t FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Uid] WHERE t1.[Name] LIKE ?` |
| mssql/MsSQLUpdateJoinTest.java#testUpdateJoin1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLUpdateJoinTest.cs:9 | `UPDATE t SET t.[Type] = t1.[Name] FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Ui…` |
| mssql/MsSQLUpdateJoinTest.java#testUpdateJoin2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mssql/MsSQLUpdateJoinTest.cs:18 | `UPDATE t SET t.[Type] = t1.[Name] FROM [doc_bank_card] t INNER JOIN [doc_bank] t1 ON t1.[Id] = t.[Ba…` |

### SQL Server 2008（ROW_NUMBER 分页）（`dialect.mssql2008`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query0 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:50：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:51：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:52：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:53：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query4 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:54：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query4_1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:55：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query4_2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:56：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:57：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5_1 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:58：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5_2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:59：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 | `WITH rt AS ( SELECT [Id],[Stars],[Title],[CreateTime], ROW_NUMBER() OVER( ORDER BY [Id] ASC ) AS __r…` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5_3 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:60：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 | `WITH rt AS ( SELECT [Id],[Stars],[Title],[CreateTime], ROW_NUMBER() OVER( ORDER BY [Id] ) AS __rownu…` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query6 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:61：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#testDraft9 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:62：SQL Server 2008 的 ROW_NUMBER 分页不做：2008 已于 2019 年停止支持，SQL Server 方言按 2012+ 的 OFFSET / FETCH 渲染 |  |

### UPDATE / DELETE … RETURNING（`write.returning`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/dialect/ReturningSqlTest.java#deleteAndUpdateReturning | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/ReturningSqlTest.cs:6 | `DELETE FROM "t_fork_post" WHERE "user_id" = ? RETURNING *` |
| fork/dialect/ReturningSqlTest.java#otherDialectsAreRejected | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/ReturningSqlTest.cs:19 |  |
| fork/live/ReturningLiveTest.java#deleteAndUpdateReturnRows | 已移植 | tests/EasyQuerySharp.Tests.Live/LiveSuite.Writes.cs:29 | `DROP TABLE "t_fork_post"` |

### 建表与迁移（code-first）（`codeFirst`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| InsertTest1.java#testInsert | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest1.cs:15 | `INSERT INTO 't_test_insert' ('id','column1','column2') VALUES (?,?,?)` |
| MigrationTest.java#createTableSQLTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/MigrationTest.cs:11 | `CREATE TABLE IF NOT EXISTS 'my_migration_blog' ( 'id' VARCHAR(255) NOT NULL , 'create_time' DATETIME…` |
| MigrationTest.java#createTableSQLTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/MigrationTest.cs:37 | `ALTER TABLE 't_topic' RENAME TO 't_topic2';` |
| MigrationTest.java#createTableSQLTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/MigrationTest.cs:45 | `ALTER TABLE 't_topic' CHANGE 'title' 'abc' varchar(200) NULL;` |
| MigrationTest.java#createTableSQLTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Features/MigrationTest.cs:53 |  |
| QueryTest22.java#testManyOrder | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest22.Part2.cs:91 |  |
| QueryTest24.java#testx18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:191 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest26.java#testGenerateKeys | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest26.cs:195：建表用原生 DDL：C# 的 code-first 还不能声明列的库端默认值（Java 的 dbDefault，归 4.1）；插入、回填与读取同 Java |  |
| QueryTest3.java#userBookTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:127 |  |
| migration/MigrationDamengTest.java#test | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Dameng/MigrationDamengTest.cs:9：达梦建表沿用 TS 版在 DM8 上实测的写法：一条语句一次执行，类型是 INT / BIT / VARCHAR2(255) / TIMESTAMP，列注释是单独的 COMMENT ON 语句； |  |
| migration/MigrationPgTest.java#test | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgMigrationTest.cs:23 |  |
| mysql8/M8Test1.java#testNoKeyCodeFirst | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8Test1.cs:46 |  |
| mysql8/MySQL8Test2.java#test | 骨架 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.CodeFirst.cs:11 |  |
| pgsql/MigrationTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgMigrationTest.cs:7 |  |
| sqlite/SQLiteTest.java#testDDL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SQLiteTest.cs:47 | `CREATE TABLE IF NOT EXISTS "t_topic" ( "Id" VARCHAR(255) NOT NULL , "Stars" integer NULL , "Title" V…` |
| sqlite/SQLiteTest.java#testDDL2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/SQLiteTest.cs:62 | `CREATE TABLE IF NOT EXISTS "t_multi_primary" ( "Id1" VARCHAR(255) NOT NULL , "Id2" VARCHAR(255) NOT …` |
| testmysql8/MyTest.java#testaa | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/TestMysql8MyTest.cs:79：C# 没有 createDatabaseIfNotExists（库由连接串决定，建库属于部署）：测试先用服务器连接建库 easy-query-test123，再同步三张表 |  |

### 按对象筛选排序（whereObject / orderByObject）（`dto.object`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.ObjectFilter.cs:6 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' > ?` |
| QueryTest.java#query18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.ObjectFilter.cs:12 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' > ? ORDER BY 'create_ti…` |
| QueryTest14.java#testClose | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:112 | `select * from t_topic` |
| QueryTest18.java#rangeTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:38 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:38 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:38 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:46 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:46 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:46 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.Ranges.cs:62 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#test11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:33 |  |
| QueryTest18.java#test13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest18.cs:41 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest19.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:101 | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE t.'name' = ?` |
| QueryTest19.java#testMyFilter | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:13：Java 可以自定义 ValueFilter（按表和属性放行）；C# 的值过滤只有固定几种，需要保留的条件写在 UseValueFilter 之前 | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'title' LIKE ?` |
| QueryTest2.java#dynamicSort | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:365 | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t WHERE t.'deleted' = ? ORDER BY t.'title' ASC` |
| QueryTest2.java#dynamicSort2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:375 | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t WHERE t.'deleted' = ? ORDER BY t.'title' ASC,t.'…` |
| QueryTest2.java#dynamicSort3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:383 | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' WHER…` |
| QueryTest2.java#dynamicSort4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:383 | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' WHER…` |
| QueryTest2.java#queryTest17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:300 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:317 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest18_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:326 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest18_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:335 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:345 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest2.cs:352 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest21.java#testOrderBy2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest21.cs:198 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest24.java#test14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:78 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:78 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:78 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:92 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test18 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:92 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:92 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:106 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:106 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:106 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:120 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:120 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test25 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:120 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:135 | `SELECT (t.'create_time' < ? AND t.'title' = ?) FROM 't_blog' t WHERE t.'deleted' = ? AND (t.'create_…` |
| QueryTest24.java#test27 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:148 | `SELECT DATE_FORMAT(t.'create_time','%Y-%m') AS 'value1',(DATE_FORMAT(t.'create_time','%Y-%m') LIKE C…` |
| QueryTest24.java#test28 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:157 | `SELECT DATE_FORMAT(t.'create_time','%Y-%m') AS 'value1',((DATE_FORMAT(t.'create_time','%Y-%m') LIKE …` |
| QueryTest24.java#test29 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:135 | `SELECT (t.'create_time' < ? AND t.'title' = ?) FROM 't_blog' t WHERE t.'deleted' = ? AND date_add(t.…` |
| QueryTest24.java#testContains2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:29：Java 的 contains/startsWith/endsWith 函数写成 LIKE CONCAT('%',?,'%')，C# 的 string.Contains 等写成 LIKE ?（值里带好 %），语义相同 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testContains3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest24.cs:30：同 testContains2；Java 的 asNumber/asStr 等只改类型，C# 用 (T)(object)x 同样不写 CAST | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#dynamicWhere1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:70 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orderTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:195 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orderTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.Part2.cs:200 |  |
| QueryTest3.java#query124 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:30 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query124_5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:35 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest4.java#joinTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:176 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:196 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:214 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:231 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:247 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:263 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:278 | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTestRelationTest.java#relationTest0 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:200 | `SELECT t.'id',t.'com_id',t.'user_id',t.'gw' FROM 'my_com_user' t WHERE EXISTS(SELECT 1 FROM 'my_sign…` |
| QueryTestRelationTest.java#relationTest0_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:70 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t WHERE t.'com_id' = ?` |
| QueryTestRelationTest.java#relationTest0_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:88 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:98 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:107 | `SELECT (SELECT COUNT(*) FROM 'my_sign_up' t2 WHERE t2.'com_id' = t1.'com_id') AS 'id' FROM 'my_sign_…` |
| QueryTestRelationTest.java#relationTest0_6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:117 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:117 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:127 | `SELECT t1.'com_id' AS 'value1' FROM 'my_sign_up' t LEFT JOIN 'my_com_user' t1 ON (t1.'com_id' = t.'c…` |
| QueryTestRelationTest.java#test1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:26 | `SELECT (CASE WHEN t1.'name' LIKE ? THEN ? ELSE ? END) AS 'value1' FROM 'my_com_user' t LEFT JOIN 'my…` |
| QueryTestRelationTest.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:38 | `SELECT t.'id',t.'com_id',t.'user_id',t.'gw' FROM 'my_com_user' t` |
| QueryTestRelationTest.java#testDirectWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTestRelationTest.cs:138 | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t` |
| doc/DocTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/DocTest.cs:83 | `SELECT COUNT(*) FROM 't_sys_user' WHERE 'name' LIKE ? AND 'phone' LIKE ? AND 'create_time' >= ? AND …` |
| mysql8/ExampleTest.java#sampleTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/ExampleTest.cs:7 |  |
| mysql8/M8BankTest.java#test10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:110 | `SELECT t.'code' AS 'value1',t2.'name' AS 'value2',t1.'name' AS 'value3' FROM 't_bank_card' t LEFT JO…` |
| mysql8/M8BankTest.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:29 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8BankTest.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.Navigations.cs:112 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t` |
| mysql8/M8QueryTest.java#whereObject1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8QueryTest.cs:51 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| mysql8/M8QueryTest.java#whereObject2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8QueryTest.cs:61 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8QueryTest.java#whereObject3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8QueryTest.cs:92 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE EXISTS(SELECT 1 F…` |
| mysql8/M8QueryTest.java#whereObject4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8QueryTest.cs:71 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8QueryTest.java#whereObject6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8QueryTest.cs:81 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test2.java#testFilterConfigure1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:147 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:246 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:255 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:157 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:166 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/MySQL8Test2.java#testFilterConfigure3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:176 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/MySQL8Test2.java#testFilterConfigure4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:186 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/MySQL8Test2.java#testFilterConfigure5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:197 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#testFilterConfigure6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:206 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#testFilterConfigure7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:215 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#testFilterConfigure8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:225 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'code',…` |
| mysql8/MySQL8Test2.java#testFilterConfigure9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:235 | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'code',…` |
| mysql8/MySQL8Test2.java#testMerge | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:310 | `SELECT IFNULL(t2.'__count2__',0) AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 'm8_province…` |
| mysql8/MySQL8Test2.java#testMerge2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:327 | `SELECT COUNT(*) FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',COUNT(?) AS '__c…` |
| mysql8/MySQL8Test2.java#testMerge3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:339 | `SELECT COUNT(*) FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',COUNT(?) AS '__c…` |
| mysql8/MySQL8Test2.java#testMerge4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:367 | `SELECT COUNT(*) FROM 'm8_province' t WHERE t.'name' LIKE CONCAT('%',?,'%')` |
| mysql8/MySQL8Test2.java#testMerge5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:352 | `SELECT COUNT(*) FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',COUNT((CASE WHEN…` |
| mysql8/MySQL8Test2.java#testSumCount | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test2.cs:296 | `SELECT IFNULL(t2.'__sum2__',0) AS 'value1',IFNULL(t2.'__sum3__',0) AS 'value2' FROM 'm8_province' t …` |
| mysql8/MySQL8Test3.java#testDynamicJoin | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test3.cs:13 |  |
| mysql8/MySQL8Test4.java#testFilter | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:335 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN 't_sys_user' …` |
| mysql8/MySQL8Test4.java#testFilterAndDependency | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:320 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t2.'i…` |
| mysql8/MySQL8Test4.java#testSubQueryCountCompareNull2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:95 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testTimeAfterNull2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:117 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testTimeBeforeNull2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test4.cs:136 | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test5.java#testToSelectPageResult | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/MySQL8Test5.cs:93 | `SELECT t1.'id',t1.'name',t1.'create_time',t3.'__joining2__' AS '__part__column1' FROM (SELECT t.'id'…` |

### search 扩展（`search`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| search/EasySearchBaseCommonTests.java#testCommon | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:3：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testCommon2 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:4：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testComplexJoinConditions | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:5：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testDefaultSort | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:6：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testEmpty | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:7：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testMultiFieldSortOverride | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:8：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testMultiSourceCondition | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:9：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseCommonTests.java#testNullValueHandling | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:10：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testCrossTableCondition | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:11：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testEmptyResult | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:12：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testInvalidOperator | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:13：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testLargeResultSet | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:14：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testMultiCondition | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:15：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testNullFieldName | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:16：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testNullValueHandling | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:17：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_gt_maxValue | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:18：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_in_empty | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:19：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_lk_emptyPattern | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:20：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_lt_boundary | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:21：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_roc_zero_range | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:22：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testThreeConditionCombination | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:23：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpExtraTests.java#testTypeMismatch | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:24：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_eq | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:25：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_ge | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:26：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_gt | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:27：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_in | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:28：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_inn | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:29：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_le | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:30：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_lk | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:31：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_lml | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:32：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_lmr | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:33：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_lt | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:34：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_ne | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:35：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_ni | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:36：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nl | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:37：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nlml | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:38：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nlmr | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:39：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nrc | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:40：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nrco | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:41：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nro | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:42：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_nroc | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:43：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_rc | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:44：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_rco | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:45：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_ro | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:46：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |
| search/EasySearchBaseOpTests.java#testOperator_roc | 不适用 | tests/EasyQuerySharp.Tests.Upstream/OutOfScope.cs:47：search 扩展（sql-search）不做：它是由前端选运算符的 URL 参数约定，同类需求用 FilterBy / SortBy |  |

### 分库分表（`sharding`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| ShardingTest.java#sharding1 | 待做 |  |  |
| ShardingTest.java#sharding10 | 待做 |  |  |
| ShardingTest.java#sharding11 | 待做 |  |  |
| ShardingTest.java#sharding12 | 待做 |  |  |
| ShardingTest.java#sharding18 | 待做 |  |  |
| ShardingTest.java#sharding19 | 待做 |  |  |
| ShardingTest.java#sharding2 | 待做 |  |  |
| ShardingTest.java#sharding20 | 待做 |  |  |
| ShardingTest.java#sharding21 | 待做 |  |  |
| ShardingTest.java#sharding26 | 待做 |  |  |
| ShardingTest.java#sharding27 | 待做 |  |  |
| ShardingTest.java#sharding28 | 待做 |  |  |
| ShardingTest.java#sharding29 | 待做 |  |  |
| ShardingTest.java#sharding3 | 待做 |  |  |
| ShardingTest.java#sharding30 | 待做 |  |  |
| ShardingTest.java#sharding31 | 待做 |  |  |
| ShardingTest.java#sharding32 | 待做 |  |  |
| ShardingTest.java#sharding33 | 待做 |  |  |
| ShardingTest.java#sharding34 | 待做 |  |  |
| ShardingTest.java#sharding35 | 待做 |  |  |
| ShardingTest.java#sharding36 | 待做 |  |  |
| ShardingTest.java#sharding37 | 待做 |  |  |
| ShardingTest.java#sharding38 | 待做 |  |  |
| ShardingTest.java#sharding39 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' = ? AND EXISTS(SELEC…` |
| ShardingTest.java#sharding4 | 待做 |  |  |
| ShardingTest.java#sharding40 | 待做 |  |  |
| ShardingTest.java#sharding41 | 待做 |  |  |
| ShardingTest.java#sharding42 | 待做 |  |  |
| ShardingTest.java#sharding43 | 待做 |  |  |
| ShardingTest.java#sharding44 | 待做 |  |  |
| ShardingTest.java#sharding45 | 待做 |  |  |
| ShardingTest.java#sharding46 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',(SELECT COUNT(t1.'id') FROM 't_topic_sharding_time' t1 WHERE (t1.'…` |
| ShardingTest.java#sharding47 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',(SELECT COUNT(t1.'id') FROM 't_topic_sharding_time' t1 WHERE (t1.'…` |
| ShardingTest.java#sharding5 | 待做 |  |  |
| ShardingTest.java#sharding6 | 待做 |  |  |
| ShardingTest.java#sharding7 | 待做 |  |  |
| ShardingTest.java#sharding8 | 待做 |  |  |
| ShardingTest.java#sharding9 | 待做 |  |  |
| ShardingTest.java#shardingTest48 | 待做 |  |  |
| ShardingTest.java#shardingTest49 | 待做 |  |  |
| ShardingTest.java#shardingTest50 | 待做 |  |  |
| ShardingTest.java#shardingTest51 | 待做 |  |  |
| ShardingTest.java#shardingTest51_1 | 待做 |  |  |
| ShardingTest.java#shardingTestMap | 待做 |  | `SELECT t.'create_time' AS 'createTime',COUNT(t.'id') AS 'idCount' FROM (SELECT * FROM 't_topic_shard…` |
| ShardingTest.java#shardingTestMap1 | 待做 |  | `SELECT t.'create_time' AS 'createTime',COUNT(t.'id') AS 'idCount' FROM (SELECT * FROM 't_topic_shard…` |
| ShardingTest.java#shardingTestMap2 | 待做 |  | `SELECT t.'create_time' AS 'createTime',COUNT(t.'id') AS 'idCount' FROM (SELECT * FROM 't_topic_shard…` |
| ShardingTest.java#shardingTestMap3 | 待做 |  | `SELECT t.'create_time' AS 'createTime',COUNT(t.'id') AS 'idCount' FROM (SELECT * FROM 't_topic_shard…` |
| ShardingTest.java#test111 | 待做 |  |  |

### 跨库链接（asTableLink）（`table.link`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EntityQueryAggregateTest2.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:11 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:22 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:33 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:44 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:55 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:66 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:77 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:88 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryJoinChainTest.cs:99 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| InsertTest.java#insertTableLinkTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:73 | `INSERT INTO 1't_topic_auto' ('stars','title','create_time') VALUES (?,?,?) ON DUPLICATE KEY UPDATE '…` |
| QueryTest14.java#test2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:160：MERGE 的约束列 C# 按列名排序（Java 按 HashSet 的遍历顺序，stars 在 id 前）；没有可作约束的列（自增主键）时 Java 抛 EasyQueryInvalidOperationException，C# 抛 InvalidOperationException，消息不同 | `SELECT rt.* FROM (SELECT t."title" AS "title",COUNT(t."id") AS "star" FROM "t_blog" t WHERE t."delet…` |
| QueryTest14.java#test3 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:195：插入冲突：Java 的 SQLite 方言写 REPLACE INTO（更新）与 INSERT … SELECT … WHERE NOT EXISTS（忽略）， | `UPDATE "t_blog" SET "deleted" = ? WHERE "t_blog"."deleted" = ? AND EXISTS(SELECT 1 FROM "t_blog" t1 …` |
| QueryTest3.java#query124_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest3.cs:47 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN lateral 't_blog' t1 ON …` |

### 换表名（asTable / 表名映射）（`table.rename`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| InsertTest1.java#testColumnConfigure2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest1.cs:64 | `INSERT INTO 'xxx' ('id','stars') VALUES (?,ifnull('stars',0)+?)` |
| QueryAggregateTest1.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:250 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:269 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:288 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:307 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:326 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:345 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:364 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:383 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 'abcv'…` |
| QueryAggregateTest2.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:250 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:269 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:288 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:307 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:326 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:345 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:364 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/EntityQueryAggregateTest.cs:383 | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 'abcv'…` |
| QueryTest.java#query24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:103 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query25 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:108 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query81 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest.SubQueries.cs:113 | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update…` |
| QueryTest14.java#testDynamicTable | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest14.cs:120 | `SELECT * FROM 'multi_hot' WHERE CAST('col8' AS DATETIME) > ? AND CAST('col9' AS DATETIME) < ?` |
| QueryTest19.java#mapInsert | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:254 | `INSERT INTO 'xxxxx' ('id','stars') VALUES (?,?)` |
| QueryTest19.java#mapUpdate | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest19.cs:263 | `UPDATE 'xxxxx' SET 'stars' = ? WHERE 'id' = ?` |
| QueryTest27.java#testAAA2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:169 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:173 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA42 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:179 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA43 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:179 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA45 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest27.cs:186 | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest28.java#testQueryMap2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest28.cs:142 | `SELECT 'id' FROM 't_topic' WHERE 'id' = ?` |
| QueryTest4.java#queryFirstSingle | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest4.cs:140 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'sys_123' WH…` |
| QueryTest5.java#joinTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:32 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| QueryTest5.java#joinTest1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:34 | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| QueryTest5.java#joinTest1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:34 | `SELECT AVG(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| QueryTest5.java#joinTest1_4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:36 | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:36 | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:38 | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:38 | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:40 | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:40 | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTestx1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Queries/QueryTest5.cs:169 | `SELECT t.'title',t.'content',t.'url',t.'star',t.'publish_time',t.'score',t.'status',t.'order',t.'is_…` |
| UpdateTest.java#mapUpdateTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:262 | `UPDATE 'aaa' SET 'name' = ? WHERE 'id' = ?` |
| UpdateTest.java#testColumnConfigure2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:441 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+? WHERE 'id' = ?` |
| UpdateTest.java#testColumnConfigure3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:450 | `UPDATE 'xxxxx' SET 'id' = ?,'stars' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:60 | `SELECT 'id','username',AES_DECRYPT(from_base64('phone'),?) AS 'phone','id_card','address','create_ti…` |
| UpdateTest.java#updateTest26_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/SqlConversionTest.cs:60 | `SELECT 'id','username',AES_DECRYPT(from_base64('phone'),?) AS 'phone','id_card','address','create_ti…` |
| fork/live/TableNameMappingLiveTest.java#includeAndImplicitSubqueryReadArchiveTables | 已移植 | tests/EasyQuerySharp.Tests.Live/LiveSuite.TableMapping.cs:34 |  |
| fork/mapping/TableNameMappingSqlTest.java#emptyMappingChangesNothing | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/TableNameMappingSqlTest.cs:16 | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user'` |
| fork/mapping/TableNameMappingSqlTest.java#explicitAsTableWins | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/TableNameMappingSqlTest.cs:24 | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user_2025'` |
| fork/mapping/TableNameMappingSqlTest.java#mainTableJoinAndSubqueriesFollowTheMapping | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/TableNameMappingSqlTest.cs:33 | `SELECT t.'id' AS 'value1',(SELECT COUNT(*) FROM 't_fork_post_archive' t3 WHERE t3.'user_id' = t.'id'…` |
| fork/mapping/TableNameMappingSqlTest.java#mappedTableNameIsReadable | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Fork/TableNameMappingSqlTest.cs:42：Java 在映射作用域里读出当前生效的表名；C# 的映射是查询对象上的不可变状态，没有作用域可读，映射后的表名从 ToSql() 体现，实体元数据里的表名不变 |  |
| fork/mapping/TableNameMappingSqlTest.java#onlyMappedEntitiesChange | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/TableNameMappingSqlTest.cs:51 | `SELECT t.'id',t.'name',t.'age',t.'company_id',t.'attrs',t.'created_at' FROM 't_fork_user' t WHERE EX…` |
| fork/mapping/TableNameMappingSqlTest.java#scopesNestAndRestore | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Fork/TableNameMappingSqlTest.cs:59 | `SELECT t.'id',t.'name',t.'age',t.'company_id',t.'attrs',t.'created_at' FROM 't_fork_user_other' t WH…` |

### 换 schema（asSchema）（`table.schema`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#deletex2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:134 | `UPDATE 'asv'.'t_blog' xx SET xx.'deleted' = ? WHERE xx.'deleted' = ? AND xx.'id' = ?` |
| GenericWithDatabaseTest.java#deleteTest15 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:77 | `DELETE FROM 'xxx'.'query_large_column_testaaa' WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#deleteTest16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:77 | `DELETE FROM 'xxx'.'query_large_column_testaaa' WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:95 | `UPDATE 'xxx'.'abc' SET 'content' = ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:100 | `UPDATE 'xcv'.'query_large_column_testabc' SET 'content' = ? WHERE 'id' = ?` |
| GenericWithDatabaseTest.java#queryLargeColumnTest21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:105 |  |
| GenericWithDatabaseTest.java#queryLargeColumnTest22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/GenericWithDatabaseTest.cs:110 |  |
| InsertTest.java#insertEmpty | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:174 |  |
| InsertTest.java#insertTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:29 | `INSERT INTO 'xxx'.'aaa' ('stars','title','create_time') VALUES (?,?,?)` |
| InsertTest.java#insertTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:34 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','title','create_time') VALUES (?,?,?)` |
| InsertTest.java#insertTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:40 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','create_time') VALUES (?,?)` |
| InsertTest.java#insertTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:40 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','title','create_time') VALUES (?,?,?)` |
| InsertTest.java#insertTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:40 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','create_time') VALUES (?,?)` |
| InsertTest.java#insertTest8_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:52 | `INSERT IGNORE INTO 'xxx'.'t_topic_autoaaa' ('stars','create_time') VALUES (?,?)` |
| InsertTest.java#insertTest9 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:57：Java 的 insertable(null) 串了一长串配置；C# 用空集合表达没有实体 |  |
| UpdateTest.java#updateTest16 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:138：Java 的 updatable(null) 串了一长串配置方法；C# 用空集合表达没有实体，配置方法只保留有对应的几个 |  |
| UpdateTest1.java#testUpdate1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:19 | `UPDATE '_abc'.'t_topic_abc' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:24 | `UPDATE '_abc'.'t_topic_abc' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:30 | `UPDATE '_abc'.'t_topic_abc' SET 'title' = CAST('stars' AS CHAR) WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:36 | `UPDATE '_abc'.'t_topic_abc' SET 'title' = 'stars' WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:43 | `UPDATE '_abc'.'t_topic_abc' SET 'stars' = ?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:50 | `UPDATE '_abc'.'t_topic_abc' SET 'title' = ?,'stars' = ? WHERE 'id' = ? AND 'stars' = ?` |
| UpdateTest1.java#testUpdate4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:56 | `UPDATE '_abc'.'t_blog_abc' SET 'is_top' = (NOT ('is_top')) WHERE 'deleted' = ? AND 'id' = ?` |
| UpdateTest1.java#testUpdate5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest1.cs:61 | `UPDATE '_abc'.'t_blog_abc' SET 'title' = (CASE WHEN 'title' = ? THEN ? ELSE ? END) WHERE 'deleted' =…` |
| pgsql/InsertTest.java#insertTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Pgsql/PgInsertTest.cs:13 | `INSERT INTO "xxx"."t_topic_autoaaa" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT DO NO…` |
