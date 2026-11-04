# Java 用例逐条对照

由 `tools/ParityTests` 生成，不要手改。状态来自 C# 测试上的 `[Upstream]` 标注，见 [0003](decisions/0003-testing-and-parity.md)。
对照的是 easy-query 的 `sql-test`（上游）与 `sql-fork-test`（fork，Id 以 `fork/` 开头），共 2338 个用例。

合计：已移植 290、偏离 37、不适用 3、骨架 15、待做 1993。

## 按能力

| 能力 | key | 里程碑 | 用例 | 已移植 | 偏离 | 不适用 | 骨架 | 待做 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 测试基础设施与 Java 专属（代理、注入、Bean 工具） | `misc.infra` | M0 | 1 | 0 | 0 | 0 | 0 | 1 |
| 基本查询（条件、排序、选列、分页） | `query.basic` | M1 | 402 | 70 | 17 | 0 | 0 | 315 |
| 分页结果 | `query.page` | M1 | 22 | 3 | 0 | 0 | 0 | 19 |
| 原生 SQL | `sql.native` | M1 | 50 | 7 | 0 | 0 | 0 | 43 |
| 删除 | `write.delete` | M1 | 21 | 7 | 1 | 2 | 0 | 11 |
| 插入 | `write.insert` | M1 | 48 | 15 | 0 | 0 | 1 | 32 |
| 更新 | `write.update` | M1 | 50 | 19 | 3 | 0 | 0 | 28 |
| CASE WHEN | `expr.case` | M2 | 27 | 0 | 0 | 0 | 0 | 27 |
| 日期函数 | `func.date` | M2 | 100 | 3 | 0 | 0 | 0 | 97 |
| 通用函数（nullOrDefault、greatest、nullif、null-safe） | `func.general` | M2 | 59 | 10 | 0 | 0 | 0 | 49 |
| 字符串聚合（joining） | `func.joining` | M2 | 29 | 0 | 0 | 0 | 5 | 24 |
| JSON 函数 | `func.json` | M2 | 12 | 0 | 0 | 0 | 0 | 12 |
| 数学函数 | `func.math` | M2 | 4 | 0 | 0 | 0 | 0 | 4 |
| 字符串函数 | `func.string` | M2 | 128 | 49 | 0 | 0 | 1 | 78 |
| 行锁 | `lock` | M2 | 28 | 0 | 0 | 0 | 0 | 28 |
| 窗口函数 | `window` | M2 | 44 | 0 | 0 | 0 | 0 | 44 |
| CTE | `query.cte` | M3 | 11 | 0 | 0 | 0 | 0 | 11 |
| 分组与 having（含 rollup / cube） | `query.group` | M3 | 83 | 9 | 1 | 0 | 2 | 71 |
| 显式 join | `query.join` | M3 | 88 | 11 | 2 | 1 | 0 | 74 |
| 无实体的 Map 查询、toMap | `query.map` | M3 | 11 | 2 | 0 | 0 | 0 | 9 |
| 复用查询（cloneQueryable） | `query.reuse` | M3 | 2 | 0 | 0 | 0 | 0 | 2 |
| 流式读取 | `query.stream` | M3 | 9 | 0 | 0 | 0 | 0 | 9 |
| union / union all | `query.union` | M3 | 18 | 12 | 3 | 0 | 1 | 2 |
| 按 DTO 自动带出（selectAutoInclude） | `dto.autoInclude` | M4 | 35 | 0 | 0 | 0 | 1 | 34 |
| 按键填充（fillMany / fillOne） | `fill` | M4 | 4 | 0 | 0 | 0 | 0 | 4 |
| 一对多展开（flatElement / elements） | `nav.flat` | M4 | 56 | 0 | 0 | 0 | 0 | 56 |
| 隐式子查询改分组 join | `nav.groupJoin` | M4 | 67 | 0 | 0 | 0 | 2 | 65 |
| include 带出导航 | `nav.include` | M4 | 37 | 0 | 0 | 0 | 0 | 37 |
| 隐式子查询（any / none / count / sum …） | `nav.subquery` | M4 | 142 | 23 | 0 | 0 | 0 | 119 |
| 树（toTreeList / asTreeCTE） | `tree` | M4 | 25 | 0 | 0 | 0 | 0 | 25 |
| 值转换、枚举、JSON 列 | `column.converter` | M5 | 9 | 0 | 0 | 0 | 0 | 9 |
| 列加密 | `column.encryption` | M5 | 8 | 0 | 0 | 0 | 0 | 8 |
| 拦截器 | `interceptor` | M5 | 3 | 0 | 0 | 0 | 0 | 3 |
| 逻辑删除 | `logicDelete` | M5 | 10 | 0 | 0 | 0 | 0 | 10 |
| 聚合根保存（savable） | `save.aggregate` | M5 | 39 | 0 | 0 | 0 | 0 | 39 |
| 追踪与差异更新 | `tracking` | M5 | 62 | 22 | 1 | 0 | 0 | 39 |
| 乐观锁版本 | `version` | M5 | 13 | 0 | 0 | 0 | 0 | 13 |
| UPDATE / DELETE 里用导航 | `write.join` | M5 | 10 | 0 | 0 | 0 | 0 | 10 |
| 写哪些列、批量执行 | `write.strategy` | M5 | 19 | 11 | 6 | 0 | 0 | 2 |
| upsert | `write.upsert` | M5 | 13 | 3 | 0 | 0 | 2 | 8 |
| ClickHouse 方言 | `dialect.clickhouse` | M6 | 4 | 0 | 0 | 0 | 0 | 4 |
| 达梦方言 | `dialect.dameng` | M6 | 51 | 0 | 0 | 0 | 0 | 51 |
| DuckDB 方言 | `dialect.duckdb` | M6 | 16 | 0 | 0 | 0 | 0 | 16 |
| 方言静态入口与专属函数 | `dialect.functions` | M6 | 25 | 0 | 0 | 0 | 0 | 25 |
| H2 方言 | `dialect.h2` | M6 | 74 | 0 | 0 | 0 | 0 | 74 |
| SAP HANA 方言 | `dialect.hana` | M6 | 18 | 0 | 0 | 0 | 0 | 18 |
| 人大金仓方言 | `dialect.kingbase` | M6 | 2 | 0 | 0 | 0 | 0 | 2 |
| SQL Server 方言 | `dialect.mssql` | M6 | 38 | 0 | 0 | 0 | 0 | 38 |
| SQL Server 2008（ROW_NUMBER 分页） | `dialect.mssql2008` | M6 | 13 | 0 | 0 | 0 | 0 | 13 |
| UPDATE / DELETE … RETURNING | `write.returning` | M6 | 3 | 0 | 0 | 0 | 0 | 3 |
| 建表与迁移（code-first） | `codeFirst` | M7 | 17 | 0 | 0 | 0 | 0 | 17 |
| 按对象筛选排序（whereObject / orderByObject） | `dto.object` | M7 | 101 | 0 | 1 | 0 | 0 | 100 |
| search 扩展 | `search` | M7 | 45 | 0 | 0 | 0 | 0 | 45 |
| 分库分表 | `sharding` | M7 | 48 | 0 | 0 | 0 | 0 | 48 |
| 跨库链接（asTableLink） | `table.link` | M7 | 13 | 0 | 0 | 0 | 0 | 13 |
| 换表名（asTable / 表名映射） | `table.rename` | M7 | 52 | 6 | 0 | 0 | 0 | 46 |
| 换 schema（asSchema） | `table.schema` | M7 | 19 | 8 | 2 | 0 | 0 | 9 |

## 逐条

### 测试基础设施与 Java 专属（代理、注入、Bean 工具）（`misc.infra`，M0）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/ProxySmokeTest.java#implicitJoinAndSubqueryRenderOnTestEntities | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'company_id',t.'attrs',t.'created_at' FROM 't_fork_user' t LEFT JOI…` |

### 基本查询（条件、排序、选列、分页）（`query.basic`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DateTimeFunctionsTest.java#booleanMySQL | 待做 |  | `SELECT t.'deleted' AS 'value1',(NOT (t.'deleted')) AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ?` |
| DateTimeFunctionsTest.java#dateTimeProp2MySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',YEAR(t.'create_time') AS 'value2',MONTH(t.'create_time') AS 'valu…` |
| DateTimeFunctionsTest.java#dateTimePropMySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',YEAR(t.'create_time') AS 'value2',MONTH(t.'create_time') AS 'valu…` |
| DirectRelationTest.java#test3 | 待做 |  | `SELECT t.'c1',t.'c2',t.'c3',t.'c4',t.'c5' FROM 'direct1' t LEFT JOIN 'direct2' t1 ON t1.'c7' = t.'c1…` |
| DirectRelationTest.java#testJoin1 | 待做 |  | `SELECT t3.'c18' AS 'value1' FROM 'direct1' t LEFT JOIN 'direct2' t1 ON t1.'c7' = t.'c1' LEFT JOIN 'd…` |
| EntityQueryAggregateTest1.java#test1 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test2 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test3 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test4 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test5 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test6 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test7 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test8 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest1.java#test9 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id'` |
| EntityQueryAggregateTest3.java#test1 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test10 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t` |
| EntityQueryAggregateTest3.java#test2 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test3 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test4 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test5 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test6 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test7 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test8 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest3.java#test9 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id'` |
| EntityQueryAggregateTest4.java#test1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test2 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test3 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test4 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test5 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test6 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test7 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test8 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest4.java#test9 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest7.java#test1 | 待做 |  | `SELECT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' t LEFT JO…` |
| EntityQueryAggregateTest7.java#test10 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test2 | 待做 |  | `SELECT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' t LEFT JO…` |
| EntityQueryAggregateTest7.java#test3 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test4 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test5 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test6 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test7 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test8 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| EntityQueryAggregateTest7.java#test9 | 待做 |  | `SELECT DISTINCT t.'id' AS 'value1',t.'stars' AS 'value2',t.'create_time' AS 'value3' FROM 't_topic' …` |
| GeneralFunctionsTest.java#likeOracle | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:71 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?,1,1) > 0` |
| GeneralFunctionsTest.java#likeOracle2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:79 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?,1,1) = 1` |
| GeneralFunctionsTest.java#likeOracle3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:87 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?,1,1) = (LENGTH("…` |
| GeneralFunctionsTest.java#likePgSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:95 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE "type" LIKE CONCAT('%',(?)::TEX…` |
| GeneralFunctionsTest.java#likePgSQL2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:103 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE "type" LIKE CONCAT((?)::TEXT,'%…` |
| GeneralFunctionsTest.java#likePgSQL3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:111 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE "type" LIKE CONCAT('%',(?)::TEX…` |
| GeneralFunctionsTest.java#likeSQLite | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:119 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?) > 0` |
| GeneralFunctionsTest.java#likeSQLite2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:127 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?) = 1` |
| GeneralFunctionsTest.java#likeSQLite3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:135 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE INSTR("type",?) = (LENGTH("type…` |
| GenericTest2.java#testAgroalGetUrl | 待做 |  |  |
| InjectTest.java#injectTest1 | 待做 |  |  |
| InjectTest.java#injectTest2 | 待做 |  |  |
| InjectTest.java#injectTest3 | 待做 |  |  |
| InsertTest1.java#testColumnConfigure | 待做 |  |  |
| MyTest1.java#testDraft3 | 待做 |  | `SELECT date_add(t.'create_time', interval (?) day) AS 'value1' FROM 't_blog' t WHERE t.'deleted' = ?…` |
| MyTest1.java#testSelect1 | 待做 |  | `SELECT ? AS 'title',t.'title' AS 'id' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest1.java#testSelect10 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(*) FROM 't_blog' t1 WHERE t1.'delete…` |
| MyTest1.java#testSelect2 | 待做 |  | `SELECT t.'title' AS 'id',1+1+t.'stars' AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest1.java#testSelect8 | 待做 |  | `SELECT t.'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL AND t.'create_time' <= ?` |
| MyTest2.java#testSelect1 | 待做 |  | `SELECT ? AS 'title',t.'title' AS 'id' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest2.java#testSelect10 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(*) FROM 't_blog' t1 WHERE t1.'delete…` |
| MyTest2.java#testSelect2 | 待做 |  | `SELECT t.'title' AS 'id',1+1+t.'stars' AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL` |
| MyTest2.java#testSelect8 | 待做 |  | `SELECT t.'title' FROM 't_topic' t WHERE t.'title' IS NOT NULL AND t.'create_time' <= ?` |
| MyTest2.java#testTuple3 | 待做 |  | `SELECT date_add(t.'create_time', interval (?) day) AS 'value1' FROM 't_blog' t WHERE t.'deleted' = ?…` |
| NumberFunctionsTest.java#addMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:11：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] + ?) AS [value1],(t.[score] + ?) AS [value2],(t.[score] + ?) AS [value3],(t.[score…` |
| NumberFunctionsTest.java#addMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:11：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' + ?) AS 'value1',(t.'score' + ?) AS 'value2',(t.'score' + ?) AS 'value3',(t.'score…` |
| NumberFunctionsTest.java#addORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:11：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" + ?) AS "value1",(t."score" + ?) AS "value2",(t."score" + ?) AS "value3",(t."score…` |
| NumberFunctionsTest.java#addPGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:11：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" + ?) AS "value1",(t."score" + ?) AS "value2",(t."score" + ?) AS "value3",(t."score…` |
| NumberFunctionsTest.java#divideMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:44：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] / ?) AS [value1],(t.[score] / t.[score]) AS [value2] FROM [t_blog] t WHERE t.[dele…` |
| NumberFunctionsTest.java#divideMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:44：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' / ?) AS 'value1',(t.'score' / t.'score') AS 'value2' FROM 't_blog' t WHERE t.'dele…` |
| NumberFunctionsTest.java#divideORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:44：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" / ?) AS "value1",(t."score" / t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#dividePGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:44：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" / ?) AS "value1",(t."score" / t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#multiplyMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:33：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] * ?) AS [value1],(t.[score] * t.[score]) AS [value2] FROM [t_blog] t WHERE t.[dele…` |
| NumberFunctionsTest.java#multiplyMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:33：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' * ?) AS 'value1',(t.'score' * t.'score') AS 'value2' FROM 't_blog' t WHERE t.'dele…` |
| NumberFunctionsTest.java#multiplyORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:33：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" * ?) AS "value1",(t."score" * t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#multiplyPGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:33：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" * ?) AS "value1",(t."score" * t."score") AS "value2" FROM "t_blog" t WHERE t."dele…` |
| NumberFunctionsTest.java#subtractMSSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:22：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.[score] - ?) AS [value1],(t.[score] - ?) AS [value2],(t.[score] - ?) AS [value3],(t.[score…` |
| NumberFunctionsTest.java#subtractMySQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:22：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t.'score' - ?) AS 'value1',(t.'score' - ?) AS 'value2',(t.'score' - ?) AS 'value3',(t.'score…` |
| NumberFunctionsTest.java#subtractORACLE | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:22：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" - ?) AS "value1",(t."score" - ?) AS "value2",(t."score" - ?) AS "value3",(t."score…` |
| NumberFunctionsTest.java#subtractPGSQL | 偏离 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:22：C# 编译期把常量提升为 decimal，参数类型是 BigDecimal 而不是 Integer（值与 SQL 相同） | `SELECT (t."score" - ?) AS "value1",(t."score" - ?) AS "value2",(t."score" - ?) AS "value3",(t."score…` |
| QueryTest.java#query0 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:16 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query0_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:26 | `SELECT x.'id',x.'create_time',x.'username',x.'phone',x.'id_card',x.'address' FROM 'easy-query-test'.…` |
| QueryTest.java#query0_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:36 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:46 | `SELECT x.'id',x.'create_time',x.'update_time',x.'create_by',x.'update_by',x.'deleted',x.'title',x.'c…` |
| QueryTest.java#query100 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:228 |  |
| QueryTest.java#query101 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:235 |  |
| QueryTest.java#query101_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:242 |  |
| QueryTest.java#query102 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:249 |  |
| QueryTest.java#query102_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:263 |  |
| QueryTest.java#query108 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:785 |  |
| QueryTest.java#query2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:56 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query27 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:284 | `SELECT t.'title',t.'content',t.'url',t.'star',t.'publish_time',t.'score',t.'status',t.'order',t.'is_…` |
| QueryTest.java#query3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:67 | `SELECT 1 FROM 't_blog' WHERE 'deleted' = ? AND 'id' = ? LIMIT 1` |
| QueryTest.java#query30 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:291 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query31 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:299 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query37 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:310 | `update t_blog set content='{…}' where id='1'` |
| QueryTest.java#query38 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:319 | `update t_blog set content=? where id=?` |
| QueryTest.java#query39 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:328：Java 的第 4、5 块把 url 选了两次（allFields 再加 url），C# 成员初始化不能给同一成员赋两次值，这两块只选一次 url | `SELECT t.'url' AS 'my_url' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest.java#query4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:78 | `SELECT 1 as id FROM 't_blog' WHERE 'deleted' = ? AND 'id' = ? LIMIT 1` |
| QueryTest.java#query42 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:352 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query45 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:360 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE EXISTS(SELECT 1 FROM 't_blo…` |
| QueryTest.java#query46 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:373 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE NOT EXISTS(SELECT 1 FROM 't…` |
| QueryTest.java#query47 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:384 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query48 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:404 |  |
| QueryTest.java#query49 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:412 |  |
| QueryTest.java#query5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:89 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query51 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:420 | `SELECT DISTINCT t.'id' FROM 't_topic' t WHERE t.'id' IS NOT NULL` |
| QueryTest.java#query58 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:429 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' IN (SELECT t1.'id' F…` |
| QueryTest.java#query59 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:461 |  |
| QueryTest.java#query5_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:437 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query5_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:450 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:104 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query60 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:468 |  |
| QueryTest.java#query72 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:477 | `SELECT t.'score',t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'delet…` |
| QueryTest.java#query73 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:486 | `SELECT t.'score',t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'delet…` |
| QueryTest.java#query74 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:493 | `SELECT t.'score',t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'delet…` |
| QueryTest.java#query75 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:501 | `SELECT t.'order' AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? LIMIT 1` |
| QueryTest.java#query76 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:509 | `SELECT t.'status' AS 'status1',t.'order',t.'is_top',t.'top' FROM 't_blog' t WHERE t.'deleted' = ? AN…` |
| QueryTest.java#query8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:143 | `SELECT 'id','stars','title','create_time' FROM 't_topic' LIMIT 1` |
| QueryTest.java#query82 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:517 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query83 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:527 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query83_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:527 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query84 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:535 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query85 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:535 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query86 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:545 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query87 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:545 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query88 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:535 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query88_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:555 | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest.java#query9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:151 |  |
| QueryTest.java#query90_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:562 |  |
| QueryTest.java#query92 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:566 |  |
| QueryTest.java#query93 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:570 |  |
| QueryTest.java#query94 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:574 |  |
| QueryTest.java#query95 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:578 |  |
| QueryTest.java#query96 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:582 |  |
| QueryTest.java#query97 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:586 |  |
| QueryTest.java#query98 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:590 |  |
| QueryTest10.java#test223 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'title' <> ? AND ('title' = ? OR 'tit…` |
| QueryTest11.java#testOrder2 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'topic_type' LIKE ?` |
| QueryTest11.java#testOrder3 | 待做 |  | `SELECT t.'id' AS 'id',t.'title' AS 'name' FROM 't_topic_type' t WHERE t.'topic_type' LIKE ?` |
| QueryTest11.java#testx10 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' NOT IN (SELECT t1.'i…` |
| QueryTest11.java#testx11 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'topic_type',t.'create_time' FROM 't_topic_type' t WHERE t.'topi…` |
| QueryTest11.java#testx12 | 待做 |  | `SELECT CAST(t.'topic_type' AS SIGNED) FROM 't_topic_type' t` |
| QueryTest11.java#testx12_1 | 待做 |  | `SELECT CAST(t.'topic_type' AS SIGNED) FROM 't_topic_type' t LIMIT 1` |
| QueryTest11.java#testx13 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'topic_type' IN (?,…` |
| QueryTest11.java#testx2 | 待做 |  | `SELECT t.'order' AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? LIMIT 1` |
| QueryTest11.java#testx22 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest11.java#testx23 | 待做 |  | `SELECT t.'id' AS 'value1',t.'url' AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ? AND ? = (SELECT …` |
| QueryTest11.java#testx24 | 待做 |  | `SELECT t.'id' AS 'value1',t.'url' AS 'value2',(SELECT COUNT(*) FROM 't_topic' t1 WHERE t1.'id' = t.'…` |
| QueryTest11.java#testx3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest11.java#testx4 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(*) FROM 't_blog' t1 WHERE t1.'delete…` |
| QueryTest11.java#testx6 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE EXISTS(SELECT 1 FROM 't_blo…` |
| QueryTest11.java#testx7 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE NOT EXISTS(SELECT 1 FROM 't…` |
| QueryTest11.java#testx8 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'id' IN (SELECT t1.'id' F…` |
| QueryTest12.java#testDoc11 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest12.java#testDoc12 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc13 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc14 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest12.java#testDoc18 | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest12.java#testDoc20 | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest12.java#testDraft23 | 待做 |  |  |
| QueryTest12.java#testFetchSub2 | 待做 |  | `SELECT t.'id',t.'class_id',t.'name',t1.'id',t1.'name',t2.'address' FROM 'my_school_student' t LEFT J…` |
| QueryTest12.java#testFetchSub3 | 待做 |  | `SELECT t.'id',t.'class_id',t.'name',t1.'id',t1.'name',t2.'address' FROM 'my_school_student' t LEFT J…` |
| QueryTest12.java#testNum4 | 待做 |  | `SELECT ((t.'star' * t.'score') + t.'score') FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testNum5 | 待做 |  | `SELECT ((t.'star' * t.'score') + ?) FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testOr26 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest13.java#orderTest2 | 待做 |  | `SELECT t.'id' AS 'blogId',t.'create_time' AS 'dateTime' FROM 't_blog' t WHERE t.'deleted' = ? AND t.…` |
| QueryTest14.java#test111 | 待做 |  |  |
| QueryTest15.java#test1 | 待做 |  | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest15.java#test12234 | 待做 |  |  |
| QueryTest15.java#test1_1 | 待做 |  | `SELECT t.'id',t.'name',(SELECT COUNT(t1.'id') AS 'id' FROM 'school_student' t1 WHERE t1.'class_id' =…` |
| QueryTest15.java#test7 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' = ?` |
| QueryTest15.java#testCaseSubAggregate | 待做 |  | `SELECT 'id','name' FROM 'school_class'` |
| QueryTest15.java#testColumn1 | 待做 |  | `SELECT t.'create_time' FROM 't_topic' t` |
| QueryTest15.java#testColumn2 | 待做 |  | `SELECT t.'stars' FROM 't_topic' t LIMIT 1` |
| QueryTest15.java#testSimple | 待做 |  | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest15.java#testSubqueryAnonymous | 待做 |  | `SELECT t3.'a' AS 'a',t3.'b' AS 'b',t3.'sc' AS 'sc' FROM (SELECT t.'id' AS 'a',t.'name' AS 'b',(SELEC…` |
| QueryTest16.java#test14 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN 't_company' …` |
| QueryTest16.java#test8 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest17.java#test11 | 待做 |  | `SELECT t.'id',t.'title' FROM 't_topic' t WHERE t.'title' = ?` |
| QueryTest17.java#testNopQuery | 待做 |  |  |
| QueryTest17.java#testSubFrom | 待做 |  |  |
| QueryTest17.java#testSubFrom1 | 待做 |  |  |
| QueryTest17.java#testxxx9 | 待做 |  |  |
| QueryTest17.java#xxx | 待做 |  |  |
| QueryTest18.java#andTest | 待做 |  |  |
| QueryTest18.java#autoFalse | 待做 |  |  |
| QueryTest18.java#test1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' IS NULL` |
| QueryTest18.java#test125 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#test2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' IS NULL` |
| QueryTest18.java#test8 | 待做 |  |  |
| QueryTest18.java#testDraft | 待做 |  |  |
| QueryTest18.java#testEnum | 待做 |  |  |
| QueryTest18.java#testFETCHER | 待做 |  | `SELECT t.'id',t.'stars' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest18.java#testFinal | 待做 |  | `SELECT a.'id',a.'create_time',a.'update_time',a.'create_by',a.'update_by',a.'deleted',a.'title',a.'c…` |
| QueryTest18.java#testMaxEnum | 待做 |  | `SELECT MAX(t.'topic_type') FROM 't_topic_type' t` |
| QueryTest18.java#testPartition1 | 待做 |  |  |
| QueryTest18.java#testResultSizeLimit1 | 待做 |  |  |
| QueryTest18.java#testStarStr | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','star',CAST('star'…` |
| QueryTest19.java#aaa | 待做 |  |  |
| QueryTest19.java#aaa1 | 待做 |  | `SELECT 'id','name','age' FROM 'a'.'dbo'.'my_user'` |
| QueryTest19.java#testConcat | 待做 |  |  |
| QueryTest19.java#testTime | 待做 |  | `SELECT t.'create_time' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest19.java#testTime1 | 待做 |  | `SELECT ? FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest2.java#groupTest10 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest10_1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest11 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest12 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest13 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#groupTest9 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#query1232 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest2.java#query13x | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest2.java#query143x | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest2.java#query14x | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest2.java#queryMultiFrom1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t , 't_topic' t1 WHERE t.'id' = ? A…` |
| QueryTest20.java#testFullSingleAllColumns | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest20.java#testFullSingleAllColumns1 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testFullSingleAllColumns2 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testFullSingleAllColumns3 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testFullSingleColumns1 | 待做 |  | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' = ?` |
| QueryTest20.java#testFullSingleColumns2 | 待做 |  | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' = ?` |
| QueryTest20.java#testFullSingleSomeColumns1 | 待做 |  | `SELECT t.'id',t.'star',t.'status' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ?` |
| QueryTest20.java#testFullSingleSomeColumns2 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| QueryTest20.java#testFullSingleSomeColumns3 | 待做 |  | `SELECT t.'id' AS 'id',t.'title' AS 'title',t.'star' AS 'star' FROM 't_blog' t WHERE t.'deleted' = ? …` |
| QueryTest20.java#testWhere1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest20.java#testWhere2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'title' LIKE ? AND 'stars' > ?` |
| QueryTest21.java#joinOrderTest | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#testOrderBy | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#testOrderBy3 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest22.java#testAdd | 待做 |  | `SELECT MAX((t.'star' + ?)) FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest22.java#testBigDecimal | 待做 |  | `SELECT t.'score' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest23.java#filterOn1 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 …` |
| QueryTest23.java#filterOn2 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t` |
| QueryTest23.java#filterOn5 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 …` |
| QueryTest23.java#testAddMulty | 待做 |  | `SELECT ((t.'order' + t.'star') / t.'score') AS 'value1' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest23.java#testJoin | 待做 |  |  |
| QueryTest23.java#testJoin1 | 待做 |  |  |
| QueryTest23.java#testJoin4 | 待做 |  |  |
| QueryTest23.java#testLt | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testOrderSQL | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testPageDistinct2 | 待做 |  | `SELECT DISTINCT t.'code' AS 'value1',t1.'name' AS 'value2' FROM 'doc_bank_card' t LEFT JOIN 'doc_use…` |
| QueryTest23.java#testaaa | 待做 |  |  |
| QueryTest24.java#test11 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test12 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testEnum | 待做 |  | `SELECT t.'topic_type' AS 'topic_type' FROM 't_topic_type' t` |
| QueryTest24.java#testx13 | 待做 |  | `SELECT t.'topic_type' FROM 't_topic_type' t LIMIT 1` |
| QueryTest24.java#testx16 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'id' = ?` |
| QueryTest24.java#testx17 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest25.java#test11 | 待做 |  |  |
| QueryTest25.java#test11a1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' IN (?)` |
| QueryTest25.java#testList | 待做 |  |  |
| QueryTest25.java#testMap | 待做 |  | `SELECT t1.'aa' AS 'aa',t1.'bb' AS 'bb' FROM (SELECT t.'id' AS 'aa',t.'star' AS 'bb' FROM 't_blog' t …` |
| QueryTest25.java#testMap1 | 待做 |  | `SELECT t1.'aa' AS 'aa',t1.'bb' AS 'bb' FROM (SELECT t.'id' AS 'aa',t.'star' AS 'bb' FROM 't_blog' t …` |
| QueryTest25.java#testOnlyIgnore | 待做 |  | `SELECT t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest25.java#testOnlyIgnore2 | 待做 |  | `SELECT t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest25.java#testOnlyIgnore3 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest25.java#topicIntegerTest | 待做 |  |  |
| QueryTest25.java#tupleTest7 | 待做 |  | `SELECT t.'id' AS 'value1',t.'create_time' AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest26.java#switchCase | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest26.java#test11 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| QueryTest26.java#test22 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| QueryTest26.java#testFindInSet | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest26.java#testMaxInCaluse | 待做 |  | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE ("id" IN (?,?,?,?,?,?,?,?,?,?) OR "id…` |
| QueryTest26.java#testQuery | 待做 |  | `SELECT 'id','name',AES_DECRYPT(from_base64('phone'),?) AS 'phone' FROM 'sys_user2' WHERE AES_DECRYPT…` |
| QueryTest26.java#testSetConstant | 待做 |  |  |
| QueryTest26.java#testaa1 | 待做 |  | `SELECT t.'id' AS 'title',t.'title' AS 'id' FROM 't_topic' t WHERE t.'id' IS NOT NULL` |
| QueryTest27.java#selectAllMap | 待做 |  | `SELECT t.'id' AS 'id',t.'create_time' AS 'createTime',t.'update_time' AS 'updateTime',t.'create_by' …` |
| QueryTest27.java#staticTestCustomFunction | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest27.java#staticTestCustomFunction2 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest27.java#staticTestCustomFunction4 | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest27.java#testAAA | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA1 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testDerivedTable1 | 待做 |  | `SELECT t1.'score' AS 'score',t1.'status1' AS 'status1',t1.'order' AS 'order',t1.'is_top' AS 'is_top'…` |
| QueryTest27.java#testMap2 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest27.java#testRandom | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' ORDER BY RAND() ASC` |
| QueryTest28.java#test | 待做 |  |  |
| QueryTest28.java#testInterceptor1 | 待做 |  | `SELECT 'id','name' FROM 'm8_interceptor' WHERE 1=1` |
| QueryTest28.java#testNoValueConverter1 | 待做 |  | `SELECT 'id','stars','title','title2','topic_type','create_time' FROM 't_topic_type_array' WHERE 'tit…` |
| QueryTest3.java#extendsUserTest | 待做 |  |  |
| QueryTest3.java#orTest | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('title' LIKE ? OR 'id' LIKE ?)` |
| QueryTest3.java#queryBasic1 | 待做 |  | `SELECT t.'id' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest3.java#queryBasic2 | 待做 |  | `SELECT t.'stars' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest3.java#queryToList1 | 待做 |  |  |
| QueryTest3.java#testGenericKey | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' LIMIT 1` |
| QueryTest3.java#testGenericKey1 | 待做 |  |  |
| QueryTest3.java#testLambda3 | 待做 |  |  |
| QueryTest3.java#testProperty3 | 待做 |  |  |
| QueryTest3.java#testSample | 待做 |  |  |
| QueryTest3.java#testSelfPredicate1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest4.java#testSQLFunc10 | 待做 |  | `SELECT CONCAT(t.'stars',t.'id') AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY t.'create_time…` |
| QueryTest4.java#testSQLFunc3 | 待做 |  | `SELECT DATE_FORMAT(t.'create_time','%Y/%m/%d') FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc4 | 待做 |  | `SELECT DATE_FORMAT(t.'create_time','%Y-%m-%d') FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc5 | 待做 |  |  |
| QueryTest4.java#testSQLFunc6 | 待做 |  |  |
| QueryTest5.java#test1 | 待做 |  | `select 1` |
| QueryTest7.java#forEachTest1 | 待做 |  |  |
| QueryTest7.java#testBank1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NOT NULL AND 'id' <> '' AND …` |
| QueryTest7.java#testBank2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '' OR LTRIM('…` |
| QueryTest7.java#testEmpty1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NOT NULL AND 'id' <> '')` |
| QueryTest7.java#testEmpty2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '')` |
| QueryTest7.java#testQuery9 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '' OR LTRIM('…` |
| QueryTest9.java#selectCount1 | 待做 |  | `SELECT COUNT(*) FROM 't_topic' WHERE 'id' = ?` |
| QueryTest9.java#testDsl4 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE ('id' IS NULL OR 'id' = '')` |
| QueryTest9.java#testQuery7 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| QueryTestRelationTest.java#relation11 | 待做 |  | `SELECT (SELECT COUNT(t1.'id') AS 'id' FROM 'school_student' t1 WHERE t1.'class_id' = t.'id') AS 'val…` |
| QueryTestRelationTest.java#relationTest0_1 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_x | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest1 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest2 | 待做 |  | `SELECT t.'id' AS 'value1',t1.'gw' AS 'value2' FROM 'my_sign_up' t LEFT JOIN 'my_com_user' t1 ON (t1.…` |
| QueryTestRelationTest.java#test3 | 待做 |  | `SELECT 'id','com_id','user_id','gw' FROM 'my_com_user'` |
| QueryTestRelationTest.java#testDirectWhere1 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#testDirectWhere2 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#testDirectWhere3 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| Relation2Test.java#test | 待做 |  | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'school_student_address' t1…` |
| Relation2Test.java#test1 | 待做 |  | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'school_student_address' t1…` |
| Relation2Test.java#test2 | 待做 |  | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'school_student_address' t1…` |
| Relation2Test.java#test3 | 待做 |  | `SELECT t.'id',t.'class_id',t.'name' FROM 'my_school_student' t LEFT JOIN 'my_school_class' t1 ON t1.…` |
| Relation2Test.java#test7 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT SUM(CAST(t1.'name' AS SIGNED)) …` |
| Relation2Test.java#test9 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT SUM(CAST(t1.'name' AS DECIMAL(3…` |
| Relation2Test.java#test9_1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT SUM(CAST(t1.'name' AS DECIMAL(3…` |
| RelationTest.java#schoolTest2 | 待做 |  | `SELECT t.'id',t.'class_id',t.'name' FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' =…` |
| RelationTest.java#testSub | 待做 |  | `SELECT t1.'id',t1.'name' FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_i…` |
| RelationTest.java#testSub1 | 待做 |  | `SELECT t1.'name' FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_id' WHERE…` |
| RelationTest.java#testSub2 | 待做 |  | `SELECT t2.'id',t2.'class_id',t2.'name' FROM 'school_student' t LEFT JOIN 'school_student_address' t1…` |
| SkipTest.java#testEasyQueryConcurrentException | 待做 |  |  |
| SubJoinTest.java#testSubJoinSumBigDecimal | 待做 |  | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSumBigDecimal2 | 待做 |  | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSumBigDecimalDistinct | 待做 |  | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinSumBigDecimalDistinct2 | 待做 |  | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| UpdateTest2.java#test11 | 待做 |  | `SELECT t."id",t."abc" AS "title" FROM "t_topic" t WHERE t."abc" = ?` |
| doc/DocTest.java#testDoc3x | 待做 |  |  |
| doc/DocTest1.java#test1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| doc/DocTest1.java#test2 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| doc/DocTest1.java#testBlog4 | 待做 |  | `SELECT t1.'name' AS 'value1',t3.'name' AS 'value2' FROM 'test_self' t LEFT JOIN 'my_user' t1 ON t1.'…` |
| mysql8/CTETest.java#cteTest3 | 待做 |  | `WITH 'm8_user_temp' AS (SELECT t.'id',t.'name' FROM 'm8_user' t WHERE t.'age' IS NULL) SELECT t2.'id…` |
| mysql8/IncludeConditionTest.java#testInclude1 | 待做 |  | `SELECT 'id','name','version' FROM 'my_task'` |
| mysql8/IncludeConditionTest.java#testInclude2 | 待做 |  | `SELECT 'id','name','version' FROM 'my_task' WHERE 'id' = ?` |
| mysql8/M8BankTest.java#dynamicPartition | 待做 |  |  |
| mysql8/M8BankTest.java#fromSubQuery1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:124 | `SELECT t1.'value1' AS 'value1',t1.'value2' AS 'value2',t1.'value3' AS 'value3' FROM (SELECT t.'name'…` |
| mysql8/M8BankTest.java#includeSubQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:30 | `SELECT t.'name' AS 'vo1',t.'id' AS 'vo2',t.'phone' AS 'vo3',t.'id' AS '__relation__id' FROM 't_sys_u…` |
| mysql8/M8BankTest.java#includeSubQuery3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:47 | `SELECT t.'name' AS 'vo1',t.'id' AS 'vo2',t.'phone' AS 'vo3',t.'id' AS '__relation__id' FROM 't_sys_u…` |
| mysql8/M8BankTest.java#includeSubQuery5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:80 | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t` |
| mysql8/M8BankTest.java#test1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:102 | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN 't_…` |
| mysql8/M8BankTest.java#test2 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN 't_…` |
| mysql8/M8BankTest.java#test3 | 待做 |  | `SELECT t1.'name' AS 'value1',t2.'name' AS 'value2',t.'code' AS 'value3' FROM 't_bank_card' t LEFT JO…` |
| mysql8/M8BankTest.java#test6 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/M8BankTest.java#testFirst | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time',t3.'code' AS '__part__column1' FROM 't_sys_…` |
| mysql8/M8BankTest.java#testFirst2 | 待做 |  | `SELECT t.'name' AS 'value1',t3.'code' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'id',t1.'…` |
| mysql8/M8BankTest.java#testFirstElementInnerJoin | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t INNER JOIN (SELECT t1.'…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest4 | 待做 |  | `SELECT t.'name' AS 'value1',t3.'code' AS 'value2',t4.'name' AS 'value3' FROM 't_sys_user' t LEFT JOI…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/M8BankTest.java#whereSubQuery6 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8BankTest.java#whereSubQuery6_1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8BankTest.java#whereSubQuery7 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE ? = (SELECT COUNT…` |
| mysql8/M8Save2Test.java#testInsert | 待做 |  |  |
| mysql8/M8Save3Test.java#testForeach | 待做 |  | `SELECT 'id','name' FROM 'm8_save_a' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testInsert | 待做 |  |  |
| mysql8/M8Test1.java#testQuery | 待做 |  | `SELECT t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time',t1.'name' AS '__part__column1' FROM 't_ba…` |
| mysql8/M8Test1.java#testQuery2 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time',t1.'name' AS '__part__column1' FRO…` |
| mysql8/M8Test1.java#userTest | 待做 |  | `SELECT 'id','sex','age','user_name' FROM 'm8_user_test' WHERE 'id' >= ? AND ('sex' = ? OR 'sex' = ?)…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many9 | 待做 |  | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t1…` |
| mysql8/ManyJoinTest.java#partitionByFirst | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#partitionByFirst2 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testElement4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'co…` |
| mysql8/ManyJoinTest.java#testLastCard | 待做 |  |  |
| mysql8/MySQL8Test2.java#testJoin | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| mysql8/MySQL8Test2.java#testOrderCaseWhen1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test2.java#testOrderCaseWhen2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test2.java#testParentNull | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| mysql8/MySQL8Test3.java#testQueryFirstLimit3 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test3.java#testQueryFirstLimit4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'i…` |
| mysql8/MySQL8Test4.java#includeOneTest2 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testAll4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (EXISTS(SELECT 1 …` |
| mysql8/MySQL8Test4.java#testAll5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test4.java#testPartition | 待做 |  | `SELECT t.'id' AS 'value1',t.'name' AS 'value2',t3.'code' AS 'value3',t3.'type' AS 'value4',t3.'open_…` |
| mysql8/MySQL8Test4.java#testSelect | 待做 |  | `SELECT t1.'id',t1.'phone' AS 'phone',t1.'age' FROM (SELECT t.'id',t.'name' AS 'phone',t.'age' FROM '…` |
| mysql8/MySQL8Test4.java#testSelect1 | 待做 |  | `SELECT t1.'id',t1.'phone' AS 'phone',t1.'age' FROM (SELECT t.'id',t.'name' AS 'phone',t.'age' FROM '…` |
| mysql8/MySQL8Test4.java#testTimeAfterNull1 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user' WHERE 'create_time' > ?` |
| mysql8/MySQL8Test4.java#testTimeBeforeNull1 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user' WHERE 'create_time' < ?` |
| mysql8/MySQL8Test5.java#configurationLogicDelete | 待做 |  | `SELECT 'id','name','deleted' FROM 'my_config_logic' WHERE 'deleted' = ?` |
| mysql8/MySQL8Test5.java#interceptorTest1 | 待做 |  | `SELECT 'id','name','age','create_time' FROM 'm8_user' WHERE 1=1` |
| mysql8/MySQL8Test5.java#interceptorTest2 | 待做 |  | `SELECT 'id','name','age','create_time' FROM 'm8_user' WHERE 1=1` |
| mysql8/MySQL8Test5.java#interceptorTest3 | 待做 |  | `SELECT 'id','name','age','create_time' FROM 'm8_user' WHERE 1=1` |
| mysql8/MySQL8Test5.java#interceptorTest4 | 待做 |  | `SELECT 'id','name','age','create_time' FROM 'm8_user'` |
| mysql8/MySQL8Test5.java#interceptorTest5 | 待做 |  | `SELECT 'id','name','age','create_time' FROM 'm8_user'` |
| mysql8/MySQL8Test5.java#interceptorTest6 | 待做 |  | `SELECT 'id','name','age','create_time' FROM 'm8_user'` |
| pgsql/NamedEnumTest.java#test1 | 待做 |  | `SELECT "id","name","type" FROM "t_named_enum" WHERE "type" = ?` |
| pgsql/QueryTest.java#query1 | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query2 | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query3 | 待做 |  | `SELECT 1 FROM "t_blog" WHERE "deleted" = ? AND "id" = ? LIMIT 1` |
| pgsql/QueryTest.java#query4 | 待做 |  | `SELECT 1 as id FROM "t_blog" WHERE "deleted" = ? AND "id" = ? LIMIT 1` |
| pgsql/QueryTest.java#query5 | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query6 | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#query8 | 待做 |  | `SELECT CONCAT(t."id",t."id") FROM "t_blog" t WHERE t."deleted" = ? AND t."id" = ?` |
| pgsql/QueryTest.java#query9 | 待做 |  | `SELECT CONCAT(t."id",t."title") FROM "t_blog" t WHERE t."deleted" = ? AND t."id" = ?` |
| pgsql/QueryTest19.java#testMapChain | 待做 |  | `SELECT t1."aaa" AS "aaa",t1."bbb" AS "bbb" FROM (SELECT t."score" AS "aaa",t."star" AS "bbb" FROM "t…` |
| pgsql/QueryTest19.java#testMapChain2 | 待做 |  | `SELECT t1."aaa" AS "aaa",t1."bbb" AS "bbb" FROM (SELECT (t."score" * ?) AS "aaa",t."star" AS "bbb" F…` |
| sqlite/SQLiteTest.java#testQuery | 待做 |  |  |
| sqlite/SQLiteTest.java#testQuery1 | 待做 |  |  |

### 分页结果（`query.page`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:753 |  |
| QueryTest.java#query13 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:760 |  |
| QueryTest.java#query50 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:772 |  |
| QueryTest11.java#testx | 待做 |  |  |
| QueryTest15.java#aaa | 待做 |  |  |
| QueryTest15.java#aaa1 | 待做 |  |  |
| QueryTest18.java#test16 | 待做 |  | `SELECT COUNT(DISTINCT t.'stars') FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'i…` |
| QueryTest18.java#test17 | 待做 |  | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LE…` |
| QueryTest18.java#textDistinct2 | 待做 |  | `SELECT DISTINCT COUNT(*) FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.…` |
| QueryTest2.java#query12x | 待做 |  |  |
| QueryTest21.java#searchCountPageTest | 待做 |  | `SELECT COUNT(*) FROM 't_topic' WHERE 'title' LIKE ?` |
| QueryTest23.java#filterOn4 | 待做 |  | `SELECT COUNT(*) FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 ON t1.'id' = t.'bank_id' AND (NOT (t…` |
| QueryTest23.java#testPageDistinct | 待做 |  | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'code' AS 'value1',t1.'name' AS 'value2' FROM 'doc_bank_card…` |
| QueryTest23.java#testPageDistinct3 | 待做 |  | `SELECT COUNT(DISTINCT t1.'name') FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 ON t1.'id' = t.'uid'…` |
| QueryTest25.java#xxx | 待做 |  |  |
| QueryTest3.java#nativePage1 | 待做 |  | `select * from t_topic` |
| QueryTest3.java#nativePage3 | 待做 |  | `select * from t_topic where id = ?` |
| QueryTest3.java#nativePage4 | 待做 |  | `select * from t_blog where id = ?` |
| QueryTest3.java#testGenericKey2 | 待做 |  |  |
| doc/DocTest.java#testTopic | 待做 |  | `SELECT COUNT(*) FROM 't_topic' WHERE 'title' LIKE ? AND 'create_time' <= ?` |
| mysql8/ManyJoinTest.java#test16GroupJoinCombinePage | 待做 |  | `SELECT COUNT(*) FROM 't_sys_user' t` |
| mysql8/MySQL8Test3.java#thisConfigureTest | 待做 |  | `SELECT COUNT(*) FROM 't_sys_user' t` |

### 原生 SQL（`sql.native`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| MyTest1.java#testGroup20 | 待做 |  | `SELECT SUBSTR(t.'title',2,2),IFNULL(t1.'score',?) FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| MyTest1.java#testGroup21 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN 't_…` |
| MyTest1.java#testGroup22 | 待做 |  | `SELECT IFNULL(t.'create_time',?) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN…` |
| MyTest2.java#testGroup20 | 待做 |  | `SELECT SUBSTR(t.'title',2,2),IFNULL(t1.'score',?) FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| MyTest2.java#testGroup21 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN 't_…` |
| MyTest2.java#testGroup22 | 待做 |  | `SELECT IFNULL(t.'create_time',?) AS 'value1',COUNT(t.'title') AS 'value2' FROM 't_topic' t LEFT JOIN…` |
| QueryNativeSQLTest.java#testNativeSQL1 | 待做 |  |  |
| QueryTest.java#query11_2 | 待做 |  | `SELECT t.'id',rank() over(order by t2.'stars' desc) as rank1 FROM 't_blog' t LEFT JOIN (SELECT t1.'i…` |
| QueryTest.java#query11_3 | 待做 |  | `SELECT t.'id',rank() over(order by t2.'stars' desc) as rank1 FROM 't_blog' t LEFT JOIN (SELECT t1.'i…` |
| QueryTest.java#query32 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:8 | `SELECT * FROM 't_blog' t where t.id=?` |
| QueryTest.java#query33 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:12 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query34 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:16 | `SELECT * FROM 't_blog' t where t.id=?` |
| QueryTest.java#query35 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:20 | `SELECT * FROM 't_blog' t` |
| QueryTest.java#query36 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:24 | `SELECT * FROM 't_blog' t where t.id=?` |
| QueryTest.java#query7_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:28 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query7_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:28 | `SELECT 'id','create_time' AS createTime,'update_time' AS updateTime,'create_by' AS createBy,'update_…` |
| QueryTest10.java#test6 | 待做 |  | `SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest11.java#testx1 | 待做 |  | `SELECT 1 AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND DATE_FORMAT(t.'create_time','%Y-%m-%d'…` |
| QueryTest12.java#testNum1 | 待做 |  | `SELECT SUM(t.'score')-SUM(t.'score') AS 'score' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testNum7 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testSelectPropTypeColumn1 | 待做 |  | `SELECT SUM(case t.'score' when ? then 1 else 0 end) AS 'star' FROM 't_blog' t WHERE t.'deleted' = ? …` |
| QueryTest12.java#testSelectPropTypeColumn2 | 待做 |  | `SELECT MIN(case t.'score' when ? then 1 else 0 end) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ?…` |
| QueryTest14.java#test1119 | 待做 |  |  |
| QueryTest15.java#testxxx1 | 待做 |  |  |
| QueryTest16.java#testxxc | 待做 |  |  |
| QueryTest17.java#aaaa | 待做 |  |  |
| QueryTest17.java#testNativeSQL | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? ORDER BY RAND()` |
| QueryTest17.java#testNativeSQL1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? ORDER BY IFNULL('stars',?) D…` |
| QueryTest17.java#testNativeSQL2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? AND 'stars'!='create_time' O…` |
| QueryTest17.java#testNativeSQL3 | 待做 |  | `SELECT RAND() AS 'value1',IFNULL(t.'stars',?) AS 'value2' FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest18.java#test9 | 待做 |  |  |
| QueryTest18.java#testClient | 待做 |  |  |
| QueryTest2.java#query1231 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest2.java#query1231_1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest2.java#query24 | 待做 |  | `SELECT RAND() AS 'id',COUNT(t.'id') AS 'id_count' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' =…` |
| QueryTest21.java#test | 待做 |  |  |
| QueryTest22.java#testAggregateFx | 待做 |  | `SELECT sum(if(timestampdiff(DAY, t.'create_time', ?) < ?, 1, 0)) AS 'count' FROM 't_blog' t WHERE t.…` |
| QueryTest25.java#tupleTest8 | 待做 |  | `SELECT t.'id' AS 'value1',t.'create_time' AS 'value2',1 AS 'value3' FROM 't_blog' t WHERE t.'deleted…` |
| QueryTest25.java#tupleTest9 | 待做 |  | `SELECT t.'id' AS 'value1',t.'create_time' AS 'value2',IFNULL(t.'title','1') AS 'value3' FROM 't_blog…` |
| QueryTest27.java#updateTable | 待做 |  |  |
| QueryTest3.java#nativePage5 | 待做 |  | `select * from t_blog where id = ?` |
| QueryTest3.java#nativePage6 | 待做 |  | `select * from t_blog where id = ?` |
| QueryTest3.java#testSelectAs1 | 待做 |  | `SELECT 100 - t.'id' AS 'stars' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ?` |
| QueryTest4.java#testSQLFuncExtension2 | 待做 |  | `SELECT t.'id' FROM 't_topic' t WHERE t.'id' = ? AND FIND_IN_SET(?,t.'id')` |
| QueryTest5.java#test2 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t` |
| QueryTest8.java#test4x | 待做 |  |  |
| QueryTestRelationTest.java#relation10 | 待做 |  | `SELECT (SELECT COUNT(t1.'id') AS 'id' FROM 'school_student' t1 WHERE t1.'class_id' = t.'id') AS 'val…` |
| mysql8/MySQL8Test2.java#testGroupConcat2 | 待做 |  | `SELECT t.'id' AS 'value1',GROUP_CONCAT(DISTINCT t.'content' SEPARATOR ?) AS 'value2' FROM 't_blog' t…` |
| mysql8/MySQL8Test4.java#testToResultSet | 待做 |  |  |
| pgsql/QueryTest19.java#concat1 | 待做 |  | `select CONCAT('a', null , 'b') as "aa"` |

### 删除（`write.delete`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#delete | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:122：检查的是 Java 删除命令的内部构建器，C# 没有对应的公开状态 |  |
| DeleteTest.java#deleteTest13 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:55：检查的是 Java 表达式上下文里的行为标志位，C# 没有对应的内部状态 |  |
| DeleteTest.java#deleteTest14 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:58：Java 的 deletable(null) 用空集合表达；ToSql 同样返回 null |  |
| DeleteTest.java#deleteTest19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:68 | `UPDATE 't_blog' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| DeleteTest.java#deleteTest20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:82 | `UPDATE 't_blog123abc' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ? AND 'create_time' <= ?` |
| DeleteTest.java#deleteTest21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:93 | `DELETE FROM 't_topic123abc' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:102 | `UPDATE 't_blog123abc' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| DeleteTest.java#deleteTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:68 | `UPDATE 't_blog' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| DeleteTest.java#deletex | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:125 | `DELETE FROM 't_blog' WHERE 'id' = ?` |
| DeleteTest.java#deletex1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:125 | `UPDATE 't_blog' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| QueryTest12.java#testDoc23 | 待做 |  | `DELETE FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE EXISTS(SELECT 1…` |
| QueryTest26.java#testAAA | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey2 | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey3 | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey4 | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey5 | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey6 | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey7 | 待做 |  |  |
| TestDeleteFlatAndPredicate.java#testExpressionKey8 | 待做 |  |  |
| mysql8/MySQL8Test2.java#testDelete | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |

### 插入（`write.insert`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#deleteTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:21 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:29 | `DELETE FROM 't_topic' WHERE 'title' = ?` |
| DeleteTest.java#deleteTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:37 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:37 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:37 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| DeleteTest.java#deleteTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:48 |  |
| GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey3 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:200 | `INSERT INTO 't_topic_auto' ('stars','title') VALUES (?,?)` |
| InsertTest.java#incrementTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:125 | `INSERT INTO 'custom_increment' ('id','name','address') VALUES (mysqlNextId(),?,?)` |
| InsertTest.java#incrementTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:125 | `INSERT INTO 'custom_increment' ('id','name','address') VALUES (mysqlNextId(),?,?)` |
| InsertTest.java#insertCustom | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:115 | `INSERT INTO 'xxxxx' ('stars') VALUES (?)` |
| InsertTest.java#insertCustom1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:120 | `INSERT INTO 'xxxxx' ('id','stars') VALUES (sde.next_rowid('sde',?),?)` |
| InsertTest.java#insertSQLConvert1 | 待做 |  | `INSERT INTO 'xxxxx' ('id','username','phone','id_card','address','create_time') VALUES (?,?,to_base6…` |
| InsertTest.java#insertSQLConvert2 | 待做 |  |  |
| InsertTest.java#insertSQLConvert3 | 待做 |  |  |
| InsertTest.java#insertTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:23 |  |
| InsertTest1.java#inertTest1 | 待做 |  | `INSERT INTO 't_topic_abc' ('id','stars','title','create_time') VALUES (?,?,?,?)` |
| MyTest1.java#testDraft11 | 待做 |  | `SELECT timestampdiff(DAY, t.'create_time', t.'update_time') AS 'value1',timestampdiff(HOUR, t.'creat…` |
| MyTest1.java#testDraft6 | 待做 |  | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1',date_ad…` |
| MyTest1.java#testDraft7 | 待做 |  | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest1.java#testDraft8 | 待做 |  | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest1.java#testDraft9 | 待做 |  | `SELECT timestampdiff(DAY, t.'update_time', t.'create_time') AS 'value1',timestampdiff(HOUR, t.'updat…` |
| MyTest2.java#testTuple11 | 待做 |  | `SELECT timestampdiff(DAY, t.'create_time', t.'update_time') AS 'value1',timestampdiff(HOUR, t.'creat…` |
| MyTest2.java#testTuple6 | 待做 |  | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1',date_ad…` |
| MyTest2.java#testTuple7 | 待做 |  | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest2.java#testTuple8 | 待做 |  | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',DAYOFMONTH(t.'create_tim…` |
| MyTest2.java#testTuple9 | 待做 |  | `SELECT timestampdiff(DAY, t.'update_time', t.'create_time') AS 'value1',timestampdiff(HOUR, t.'updat…` |
| QueryTest.java#query69 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:91 |  |
| QueryTest.java#query70 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:105 |  |
| QueryTest.java#query70_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:105 |  |
| QueryTest.java#query71 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:117 |  |
| QueryTest15.java#test2 | 待做 |  | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest15.java#test3 | 待做 |  | `SELECT 'id','first_name','last_name','birthday',CONCAT('first_name','last_name') AS 'full_name',CEIL…` |
| QueryTest18.java#test111 | 待做 |  |  |
| QueryTest27.java#testUUId | 待做 |  |  |
| QueryTest4.java#testJson1 | 待做 |  |  |
| QueryTest4.java#testJson2 | 待做 |  |  |
| QueryTest4.java#testJson2Jackson | 待做 |  |  |
| UpperPropertyTest.java#test1 | 待做 |  |  |
| UpperPropertyTest.java#test2 | 待做 |  |  |
| fork/insert/InsertSelectSqlTest.java#defaultTargetIsTheEntityTable | 待做 |  | `INSERT INTO "t_fork_post" ("id","user_id","title","amount") SELECT eq_src."id",eq_src."user_id",eq_s…` |
| fork/insert/InsertSelectSqlTest.java#hanaUsesQuotedNames | 待做 |  | `INSERT INTO "t_fork_post_archive" ("id","user_id","title","amount") SELECT eq_src."id",eq_src."user_…` |
| fork/insert/InsertSelectSqlTest.java#mysqlCopiesEntityColumnsPlusExtraColumn | 待做 |  | `INSERT INTO 't_fork_post_archive' ('id','user_id','title','amount','archived_at') SELECT eq_src.'id'…` |
| fork/live/InsertSelectLiveTest.java#copyMatchingRowsWithArchiveTime | 待做 |  | `CREATE COLUMN TABLE` |
| mysql8/MySQL8Test5.java#testLikeStartsWith | 待做 |  | `SELECT 'column1','column2','column3' FROM 'no_key_table' WHERE 'column2' LIKE CONCAT(?,'%') ESCAPE '…` |
| pgsql/QueryTest.java#testDraft9 | 待做 |  |  |
| pgsql/QueryTest.java#testDraft9_1 | 待做 |  |  |
| pgsql/QueryTest18.java#testLikeStartsWith | 待做 |  | `SELECT "column1","column2","column3" FROM "no_key_table" WHERE "column2" LIKE CONCAT((?)::TEXT,'%') …` |
| pgsql/QueryTest19.java#testUUId | 待做 |  |  |

### 更新（`write.update`，M1）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest18.java#test123 | 待做 |  |  |
| QueryTest21.java#TestReverse | 待做 |  | `SELECT COUNT(*) FROM 't_topic' WHERE 'id' IS NOT NULL` |
| QueryTest24.java#test123 | 待做 |  |  |
| QueryTest26.java#testQuery2 | 待做 |  | `UPDATE 'sys_user_encrypt' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| QueryTest26.java#testQuery3 | 待做 |  | `UPDATE 'sys_user2' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| QueryTest26.java#updateConcat | 待做 |  |  |
| QueryTest28.java#testIncrementNullZero | 待做 |  | `UPDATE 't_topic' SET 'stars' = (IFNULL('stars',?) + ?) WHERE 'id' = ?` |
| TestUpdateFlatAndPredicate.java#testExpressionKey | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey2 | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey3 | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey4 | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey5 | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey6 | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey7 | 待做 |  |  |
| TestUpdateFlatAndPredicate.java#testExpressionKey8 | 待做 |  |  |
| UpdateTest.java#testUpdateIdNull1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:428：C# 的主键为 null 时按 C# 语义写成 IS NULL，Java 写 'id' = ? 并传 null | `UPDATE 't_topic' SET 'title' = CONCAT(SUBSTR('title',2,10),LOWER('id')) WHERE 'id' = ?` |
| UpdateTest.java#updateEntityWhere | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:472 | `UPDATE 't_blog' SET 'create_time' = ?,'update_time' = ?,'create_by' = ?,'update_by' = ?,'title' = ?,…` |
| UpdateTest.java#updateSet1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:337 | `UPDATE 't_topic' SET 'stars' = ?,'stars' = CAST('title' AS SIGNED),'title' = ?,'title' = DATE_FORMAT…` |
| UpdateTest.java#updateSetFuncTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:406 | `UPDATE 'a123123' SET 'title' = CONCAT(SUBSTR('title',2,10),'id') WHERE 'id' = ?` |
| UpdateTest.java#updateSetFuncTest1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:406 | `UPDATE 'a123123' SET 'title' = CONCAT('id',SUBSTR('title',2,10)) WHERE 'id' = ?` |
| UpdateTest.java#updateSetFuncTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:407 | `UPDATE 'a123123' SET 'title' = CONCAT(SUBSTR('title',2,10),?) WHERE 'id' = ?` |
| UpdateTest.java#updateSetFuncTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:407 | `UPDATE 'a123123' SET 'title' = CONCAT(SUBSTR('title',2,10),LOWER('id')) WHERE 'id' = ?` |
| UpdateTest.java#updateTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:16 | `UPDATE 't_topic' SET 'stars' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:102 |  |
| UpdateTest.java#updateTest11 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:112：参数文本：C# 枚举按底层整数参数化为 9(Integer)，Java 输出 CLASSER(TopicTypeEnum) | `UPDATE 't_topic_type' SET 'stars' = ? WHERE 'topic_type' = ?` |
| UpdateTest.java#updateTest11_1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:113：同 updateTest11 | `UPDATE 't_topic_type' SET 'stars' = ? WHERE 'topic_type' = ?` |
| UpdateTest.java#updateTest12 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:125 |  |
| UpdateTest.java#updateTest12_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:132 | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ? AND 'stars' = ?` |
| UpdateTest.java#updateTest12_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:132 | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ? AND 'stars' = ?` |
| UpdateTest.java#updateTest18_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:173 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+? WHERE 'id' = ?` |
| UpdateTest.java#updateTest19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:178 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest19_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:178 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:36 |  |
| UpdateTest.java#updateTest25 | 待做 |  | `UPDATE 'xxxxx' SET 'username' = ?,'phone' = to_base64(AES_ENCRYPT(?,?)),'id_card' = ?,'address' = ?,…` |
| UpdateTest.java#updateTest25_1 | 待做 |  | `UPDATE 'xxxxx' SET 'username' = ?,'phone' = to_base64(AES_ENCRYPT(?,?)),'id_card' = ?,'address' = ?,…` |
| UpdateTest.java#updateTest27 | 待做 |  | `UPDATE 'x123' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| UpdateTest.java#updateTest27_1 | 待做 |  | `UPDATE 'x123' SET 'phone' = to_base64(AES_ENCRYPT(?,?)) WHERE 'id' = ?` |
| UpdateTest.java#updateTest29 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:241 | `UPDATE 't_topic' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest29_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:241 | `UPDATE 't_topic' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:40 |  |
| UpdateTest.java#updateTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:50 |  |
| UpdateTest1.java#updateNoSet | 待做 |  |  |
| UpdateTest1.java#updateTestUpdate | 待做 |  |  |
| UpdateTest1.java#updateTestUpdate1 | 待做 |  | `UPDATE 't_topic' SET 'stars' = ?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest1.java#updateTestUpdate3 | 待做 |  | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest2.java#testUpdateNull | 待做 |  | `UPDATE "t_topic" SET "title" = ? WHERE "id" = ?` |
| UpdateTest2.java#testUpdateValueTest | 待做 |  | `UPDATE "t_topic" SET "title" = ? WHERE "stars" = ?` |
| UpdateTest2.java#update1 | 待做 |  |  |
| mysql8/M8BankTest.java#testaa1 | 待做 |  |  |
| mysql8/ManyJoinTest.java#test2GroupJoinCombine | 待做 |  | `UPDATE 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',(COUNT(?) > 0) AS '__any2__' FR…` |

### CASE WHEN（`expr.case`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest12.java#testSelectPropTypeColumn10 | 待做 |  | `SELECT SUM((CASE WHEN t.'id' = ? THEN ? ELSE ? END)) AS 'star',MIN(case t.'score' when ? then 1 else…` |
| QueryTest12.java#testSelectPropTypeColumn11 | 待做 |  | `SELECT SUM((CASE WHEN t.'id' = ? THEN t.'score' ELSE ? END)) AS 'star' FROM 't_blog' t WHERE t.'dele…` |
| QueryTest12.java#testSelectPropTypeColumn12 | 待做 |  | `SELECT SUM((CASE WHEN t.'id' = ? THEN t.'score' ELSE (t.'status' * ?) END)) AS 'star' FROM 't_blog' …` |
| QueryTest12.java#testSelectPropTypeColumn3 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn4 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn5 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn6 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn7 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn8 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest12.java#testSelectPropTypeColumn9 | 待做 |  | `SELECT (CASE WHEN t.'id' = ? THEN ? ELSE ? END) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest15.java#testCaseWhenLazy | 待做 |  | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'value1' FROM 't_topic' t` |
| QueryTest15.java#testCaseWhenLazy2 | 待做 |  | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title' FROM 't_topic' t` |
| QueryTest15.java#testOrderCase | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? ORDER BY (CASE WHEN 'stars' …` |
| QueryTest18.java#testUpdate | 待做 |  | `UPDATE 't_topic' SET 'stars' = (CASE WHEN ('stars' - ?) < ? THEN ? ELSE ('stars' - ?) END) WHERE 'id…` |
| QueryTest21.java#caseWhenTest1 | 待做 |  | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title',t.'id' AS 'id' FROM 't_topic' t WHERE …` |
| QueryTest21.java#caseWhenTest2 | 待做 |  | `SELECT (CASE WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title',t.'id' AS 'id' FROM 't_topic' t WHERE …` |
| QueryTest22.java#testaa | 待做 |  |  |
| QueryTest23.java#fetcherOrder | 待做 |  |  |
| QueryTest23.java#testOrderSQL1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest25.java#testOrderCaseWhen | 待做 |  |  |
| QueryTest3.java#propertyCaseWhen | 待做 |  | `SELECT (CASE WHEN t.'title' = ? THEN ? WHEN t.'title' = ? THEN ? ELSE ? END) AS 'title',t.'id' FROM …` |
| QueryTest3.java#testCaseWhen | 待做 |  | `SELECT (CASE WHEN t.'title' = ? AND t.'stars' >= ? THEN ? WHEN t.'title' = ? THEN ? WHEN t.'title' =…` |
| SubJoinTest.java#testSubJoinCaseWhen1_1 | 待做 |  | `SELECT IFNULL((SELECT SUM(t3.'age') FROM 'sub_join_book' t2 LEFT JOIN 'sub_join_author' t3 ON t3.'id…` |
| SubJoinTest.java#testSubJoinCaseWhen2 | 待做 |  | `SELECT IFNULL((SELECT SUM(t3.'age') FROM 'sub_join_book' t2 LEFT JOIN 'sub_join_author' t3 ON t3.'id…` |
| mysql8/MySQL8Test2.java#abc | 待做 |  |  |
| pgsql/MathQueryTest.java#testPropertyMath | 待做 |  | `SELECT t."test_value" AS "test_value",ABS(t."test_value") AS "test_abs",SIGN(t."test_value") AS "tes…` |
| pgsql/QueryTest19.java#testItem | 待做 |  |  |

### 日期函数（`func.date`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DateTimeFunctionsTest.java#dayOfWeekSunDayIsEndDayMySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',(WEEKDAY(t.'create_time')+1) AS 'value2' FROM 't_blog' t WHERE t.…` |
| DateTimeFunctionsTest.java#dayOfYearMySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',DAYOFYEAR(t.'create_time') AS 'value2' FROM 't_blog' t WHERE t.'d…` |
| DateTimeFunctionsTest.java#durationMySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',timestampdiff(DAY, t.'create_time', ?) AS 'value2',timestampdiff(…` |
| DateTimeFunctionsTest.java#formatMySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',DATE_FORMAT(t.'create_time','%Y年%m-01 %H:%i分%s秒') AS 'value2',DAT…` |
| DateTimeFunctionsTest.java#testConstant | 待做 |  | `SELECT NOW() AS 'value1',UTC_TIMESTAMP() AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ?` |
| MyTest1.java#testDraft4 | 待做 |  | `SELECT date_add(t.'create_time', interval (?) month) AS 'value1' FROM 't_blog' t WHERE t.'deleted' =…` |
| MyTest1.java#testDraft5 | 待做 |  | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1' FROM 't…` |
| MyTest1.java#testExists | 待做 |  | `SELECT t.'id' AS 'value1',t.'title' AS 'value2',t.'create_time' AS 'value3',(SELECT t2.'title' FROM …` |
| MyTest1.java#testSelect3 | 待做 |  | `SELECT t.'title' AS 'id',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'id',SUBSTR(t.'title',2,2) AS 't…` |
| MyTest1.java#testSelect4 | 待做 |  | `SELECT t.'id',t.'title',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'title' FROM 't_topic' t WHERE t.…` |
| MyTest2.java#testExists | 待做 |  | `SELECT t.'id' AS 'value1',t.'title' AS 'value2',t.'create_time' AS 'value3',(SELECT t2.'title' FROM …` |
| MyTest2.java#testSelect3 | 待做 |  | `SELECT t.'title' AS 'id',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'id',SUBSTR(t.'title',2,2) AS 't…` |
| MyTest2.java#testSelect4 | 待做 |  | `SELECT t.'id',t.'title',DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'title' FROM 't_topic' t WHERE t.…` |
| MyTest2.java#testTuple4 | 待做 |  | `SELECT date_add(t.'create_time', interval (?) month) AS 'value1' FROM 't_blog' t WHERE t.'deleted' =…` |
| MyTest2.java#testTuple5 | 待做 |  | `SELECT date_add(date_add(t.'create_time', interval (?) month), interval (?) day) AS 'value1' FROM 't…` |
| QueryTest.java#query68 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:101 | `SELECT t.'id',t.'username',t.'phone',t.'id_card',t.'address',IFNULL(t.'create_time',?) AS 'create_ti…` |
| QueryTest.java#query7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:117 |  |
| QueryTest10.java#test222 | 待做 |  |  |
| QueryTest10.java#testDraft12 | 待做 |  | `SELECT t.'id' AS 'value1',REPLACE(LOWER(t.'title'),?,?) AS 'value2',UPPER(t.'title') AS 'value3',SUB…` |
| QueryTest10.java#testDraft13 | 待做 |  | `SELECT t.'id' AS 'value1',TRIM(LOWER(t.'title')) AS 'value2',UPPER(TRIM(t.'title')) AS 'value3',SUBS…` |
| QueryTest10.java#testDraft14 | 待做 |  | `SELECT t.'id' AS 'value1',LTRIM(LOWER(t.'title')) AS 'value2',UPPER(LTRIM(t.'title')) AS 'value3',SU…` |
| QueryTest10.java#testDraft15 | 待做 |  | `SELECT t.'id' AS 'value1',RTRIM(LOWER(t.'title')) AS 'value2',UPPER(RTRIM(t.'title')) AS 'value3',SU…` |
| QueryTest10.java#testDraft16 | 待做 |  | `SELECT t.'id' AS 'value1',LOWER(t.'title') AS 'value2' FROM 't_topic' t WHERE LTRIM(RTRIM(t.'title')…` |
| QueryTest10.java#testLike1 | 待做 |  | `SELECT t2.'id',t2.'content',t2.'create_time',t3.'stars' FROM (SELECT t.'id',t.'create_time',t.'updat…` |
| QueryTest10.java#testQuery8 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| QueryTest10.java#testVO1 | 待做 |  | `SELECT t.'id',t1.'title' AS 'field1',t2.'id' AS 'field2' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t…` |
| QueryTest11.java#testOrder | 待做 |  | `SELECT t.'title',t.'id',DATE_FORMAT(t.'create_time','%Y-%m-%d %H:%i:%s') FROM 't_topic' t WHERE t.'t…` |
| QueryTest11.java#testOrder1 | 待做 |  | `SELECT t.'title',t.'id',DATE_FORMAT(t.'create_time','%Y-%m-%d %H:%i:%s') FROM 't_topic_type_array' t…` |
| QueryTest11.java#testx15 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE 'create_time' >= da…` |
| QueryTest11.java#testx16 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE ? <= 'create_time' …` |
| QueryTest11.java#testx17 | 待做 |  |  |
| QueryTest11.java#testx18 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE CONCAT('id','title'…` |
| QueryTest11.java#testx21 | 待做 |  | `SELECT YEAR(t.'create_time') AS 'value1',MONTH(t.'create_time') AS 'value2',SUM(t.'star') AS 'value3…` |
| QueryTest12.java#orderTest1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' ORDER BY 'id' ASC` |
| QueryTest13.java#testMultiSelect | 待做 |  | `SELECT t.'id',t.'content',t.'create_time' AS 'create_time',t1.'address',t1.'id_card' FROM 't_blog' t…` |
| QueryTest14.java#testOracle | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| QueryTest14.java#testPlus | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' < date_add(CAST(? AS DA…` |
| QueryTest14.java#testPlus_1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' < date_add(CAST(? AS DA…` |
| QueryTest14.java#testPropertyFunc | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' < CAST(? AS DATETIME)` |
| QueryTest15.java#tes123txxx | 待做 |  |  |
| QueryTest15.java#test122345 | 待做 |  | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE TO_CHAR(("create_time")::TIMESTAMP,'Y…` |
| QueryTest15.java#test1223456 | 待做 |  | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE strftime('%Y年%m月%d日', "create_time") …` |
| QueryTest15.java#test123 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE DATE_FORMAT('create_time','%Y年%m月%d日'…` |
| QueryTest15.java#testColumn | 待做 |  | `SELECT t.'create_time' FROM 't_topic' t WHERE t.'id' = ? AND DATE_FORMAT(t.'create_time','%Y-%m') >=…` |
| QueryTest15.java#testFETCHER | 待做 |  | `SELECT t.'id',t.'create_time',t.'title' FROM 't_topic' t WHERE t.'id' = ? AND DATE_FORMAT(t.'create_…` |
| QueryTest15.java#testa | 待做 |  | `SELECT DATE_FORMAT(t.'create_time','%Y-%m') AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE …` |
| QueryTest17.java#test12234567 | 待做 |  | `SELECT "id","stars","title","create_time" FROM "t_topic" WHERE TO_CHAR(("create_time")::TIMESTAMP,'Y…` |
| QueryTest18.java#Sum | 待做 |  |  |
| QueryTest18.java#Sum1 | 待做 |  |  |
| QueryTest18.java#easyQuery | 待做 |  |  |
| QueryTest18.java#easyQueryPrint1 | 待做 |  |  |
| QueryTest18.java#test10 | 待做 |  |  |
| QueryTest18.java#testxx | 待做 |  | `select * from t_topic where id != ?` |
| QueryTest19.java#test2 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN 't_user_addr…` |
| QueryTest19.java#testNow | 待做 |  |  |
| QueryTest2.java#queryTest16 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest20.java#test1 | 待做 |  |  |
| QueryTest23.java#datetimeformat | 待做 |  | `SELECT t.'create_time' AS 'value1',DATE_FORMAT(t.'create_time','%Y年%m-01 %H:%i分%s秒') AS 'value2' FRO…` |
| QueryTest23.java#formatMySQL | 待做 |  | `SELECT t.'create_time' AS 'value1',DATE_FORMAT(t.'create_time','%Y年%m-01 %H:%i分%s秒') AS 'value2',DAT…` |
| QueryTest26.java#test1 | 待做 |  |  |
| QueryTest26.java#test2 | 待做 |  |  |
| QueryTest26.java#test3 | 待做 |  |  |
| QueryTest26.java#test4 | 待做 |  |  |
| QueryTest26.java#test5 | 待做 |  |  |
| QueryTest26.java#test6 | 待做 |  |  |
| QueryTest26.java#test7 | 待做 |  |  |
| QueryTest3.java#dynamicWhere | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orTest1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' = ?` |
| QueryTest3.java#orTest2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' = ?` |
| QueryTest3.java#orTest3 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic'` |
| QueryTest3.java#orTest4 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orTest5 | 待做 |  |  |
| QueryTest3.java#query9 | 待做 |  |  |
| QueryTest3.java#query9_1 | 待做 |  |  |
| QueryTest3.java#query9_x | 待做 |  |  |
| QueryTest3.java#testAndOr1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ? AND ('title' LIKE ? OR 'titl…` |
| QueryTest3.java#testOrder1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest4.java#testSQLFunc12_1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('id',?) = DATE_FORMAT('create_…` |
| QueryTest4.java#testSQLFunc7 | 待做 |  | `SELECT DATE_FORMAT(t.'create_time','%Y-%m-%d') AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY…` |
| QueryTest9.java#dslTest6 | 待做 |  | `SELECT t.'title' FROM 't_topic' t WHERE t.'create_time' <= IFNULL(t.'create_time',?) AND IFNULL(t.'i…` |
| QueryTest9.java#dslTest7 | 待做 |  | `SELECT t.'stars',t.'create_time',t.'id' AS 'abc',t.'id' FROM 't_topic' t WHERE DATE_FORMAT(t.'create…` |
| QueryTest9.java#entityQuery1 | 待做 |  |  |
| QueryTest9.java#testDsl1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'stars' = ?` |
| QueryTest9.java#testDsl2 | 待做 |  | `SELECT COUNT(t.'id') AS 'stars',DATE_FORMAT(t.'create_time','%Y/%m/%d') AS 'title' FROM 't_topic' t …` |
| QueryTest9.java#testDsl3 | 待做 |  | `SELECT CAST(IFNULL(t.'stars',?) AS CHAR) AS 'title',IFNULL(t.'id','') AS 'alias' FROM 't_topic' t WH…` |
| QueryTest9.java#testDsl5 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' <= 'create_time'` |
| QueryTest9.java#testQuery5 | 待做 |  | `SELECT t.'id' AS 'id',COUNT(*) AS 'star',MAX(t.'id') AS 'title' FROM 't_blog' t WHERE t.'deleted' = …` |
| doc/DocTest.java#test2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'depart_name' FROM 'a222' t WHERE t.'id' = ? AND t.'id' = DATE_FO…` |
| doc/DocTest.java#testLikeOr | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| doc/DocTest.java#testSubQuerySelect | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| doc/DocTest.java#testSubQuerySelect2 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'content',t.…` |
| fork/functions/DateTruncSqlTest.java#mysqlRepeatsTheColumnForWeeks | 待做 |  | `SELECT DATE_SUB(CAST(DATE_FORMAT(t.'created_at','%Y-%m-%d 00:00:00') AS DATETIME), INTERVAL WEEKDAY(…` |
| fork/functions/DateTruncSqlTest.java#postgresUsesDateTrunc | 待做 |  | `SELECT DATE_TRUNC('quarter', t."created_at") AS "value1" FROM "t_fork_user" t` |
| fork/functions/DateTruncSqlTest.java#unsupportedDialectSaysSo | 待做 |  |  |
| fork/live/DateTruncLiveTest.java#truncatesToEachUnit | 待做 |  | `DROP TABLE {…}t_fork_user` |
| mysql8/M8BankTest.java#includeSubQuery4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:63 | `SELECT t.'name' AS 'vo1',t.'id' AS 'vo2',t.'phone' AS 'vo3',t.'id' AS '__relation__id' FROM 't_sys_u…` |
| pgsql/QueryTest.java#query7 | 待做 |  |  |
| pgsql/QueryTest.java#test123 | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| pgsql/QueryTest.java#testDuration | 待做 |  |  |
| pgsql/QueryTest19.java#datetimeformat | 待做 |  | `SELECT t."create_time" AS "value1",TO_CHAR((t."create_time")::TIMESTAMP,'YYYY年MM-01 HH24:MI分SS秒') AS…` |

### 通用函数（nullOrDefault、greatest、nullif、null-safe）（`func.general`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#equalsWithMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:30 | `SELECT (CASE t.[id] WHEN ? THEN 1 ELSE 0 END) AS [value1] FROM [doc_bank_card] t WHERE (CASE t.[code…` |
| GeneralFunctionsTest.java#equalsWithMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:30 | `SELECT (CASE t.'id' WHEN ? THEN 1 ELSE 0 END) AS 'value1' FROM 'doc_bank_card' t WHERE (CASE t.'code…` |
| GeneralFunctionsTest.java#equalsWithORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:30 | `SELECT (CASE t."id" WHEN ? THEN 1 ELSE 0 END) AS "value1" FROM "doc_bank_card" t WHERE (CASE t."code…` |
| GeneralFunctionsTest.java#equalsWithPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:30 | `SELECT (CASE t."id" WHEN ? THEN 1 ELSE 0 END) AS "value1" FROM "doc_bank_card" t WHERE (CASE t."code…` |
| GeneralFunctionsTest.java#nullOrDefaultMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:60 | `SELECT [id],[uid],[code],[type],[bank_id] FROM [doc_bank_card] WHERE ISNULL([code],?) = ?` |
| GeneralFunctionsTest.java#nullOrDefaultMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:60 | `SELECT 'id','uid','code','type','bank_id' FROM 'doc_bank_card' WHERE IFNULL('code',?) = ?` |
| GeneralFunctionsTest.java#nullOrDefaultORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:60 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE NVL("code",?) = ?` |
| GeneralFunctionsTest.java#nullOrDefaultPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:60 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE COALESCE("code",?) = ?` |
| QueryTest.java#query16_1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:16 | `SELECT COUNT(1) FROM (SELECT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t I…` |
| QueryTest.java#query16_1_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:16 | `SELECT COUNT(1) FROM (SELECT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t I…` |
| QueryTest10.java#testDraft9 | 待做 |  | `SELECT IFNULL(t.'title',?) AS 'title',t.'id' AS 'id',t.'stars' AS 'stars' FROM 't_topic' t ORDER BY …` |
| QueryTest11.java#testx27 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE IFNULL('topic_type'…` |
| QueryTest11.java#testx28 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE IFNULL('topic_type'…` |
| QueryTest17.java#testOptional1 | 待做 |  |  |
| QueryTest18.java#rangeTest1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest2 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#test15 | 待做 |  |  |
| QueryTest18.java#test1_0 | 待做 |  | `SELECT IFNULL(t.'id',?) AS 'id' FROM 't_topic' t WHERE t.'id' IS NULL` |
| QueryTest18.java#test1xa | 待做 |  |  |
| QueryTest2.java#groupTest5 | 待做 |  | `SELECT t.'id' AS 'id',IFNULL(SUM(DISTINCT t.'score'),?) AS 'score',IFNULL(COUNT(DISTINCT t.'order'),…` |
| QueryTest2.java#groupTest5_1 | 待做 |  |  |
| QueryTest20.java#testFullSingleColumns3 | 待做 |  | `SELECT IFNULL(t.'id',?) FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' =…` |
| QueryTest20.java#testFullSingleColumns4 | 待做 |  | `SELECT IFNULL(t.'id',?) FROM 't_blog' t WHERE t.'deleted' = ? AND t.'content' LIKE ? AND t.'title' =…` |
| QueryTest20.java#testMap1 | 待做 |  | `SELECT t.'id' AS 'v1',(t1.'star' + ?) AS 'v2',IFNULL(t.'create_time',?) AS 'v3' FROM 't_topic' t LEF…` |
| QueryTest20.java#testMap2 | 待做 |  | `SELECT t.'id' AS 'str',(t1.'star' + ?) AS 'integer',IFNULL(t.'create_time',?) AS 'time' FROM 't_topi…` |
| QueryTest21.java#anyColumnOrderByTest1 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#anyColumnTest1 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#anyColumnTest2 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#joinOrderTest2 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#joinOrderTest3 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest21.java#typeTest | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testAdd | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest24.java#test13 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testContains4 | 待做 |  | `SELECT t.'title' AS 'title',IFNULL(t.'content',?) AS 'content' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest27.java#staticTestCustomFunction3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest27.java#testMap | 待做 |  | `SELECT t.'create_time' AS 'a1',IFNULL(t.'create_time',?) AS 'a2' FROM 't_topic' t` |
| QueryTest28.java#testQueryNullOrDefault | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('stars',0) = ?` |
| QueryTest28.java#testQueryNullOrDefault2 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('stars',1) = ?` |
| QueryTest3.java#testProperty3_1 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE (IFNULL('id',?) = IFNULL('stars',?) O…` |
| QueryTest4.java#testSQLFunc1 | 待做 |  | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc12 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE IFNULL('id',?) = IFNULL('create_time'…` |
| QueryTest4.java#testSQLFunc2 | 待做 |  | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc2_1 | 待做 |  | `SELECT IFNULL(t.'id',t.'title',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFunc2_2 | 待做 |  | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ?` |
| QueryTest4.java#testSQLFuncExtension1 | 待做 |  | `SELECT IFNULL(t.'id',?) FROM 't_topic' t WHERE t.'id' = ? AND FIND_IN_SET(?,t.'id')` |
| QueryTest5.java#groupTest5_2 | 待做 |  |  |
| QueryTest5.java#joinTestx | 待做 |  | `SELECT COUNT(t2.'id') FROM (SELECT DISTINCT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FRO…` |
| QueryTest9.java#testQuery6 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'star',MAX(t.'id') AS 'title' FROM 't_blog' t WHERE t.'deleted' = ? A…` |
| fork/functions/NullSafeSqlTest.java#nullIf | 待做 |  | `SELECT NULLIF(t.'name', ?) AS 'value1' FROM 't_fork_user' t` |
| fork/functions/NullSafeSqlTest.java#nullSafeEqualsBetweenColumns | 待做 |  | `SELECT t.'id' FROM 't_fork_post' t INNER JOIN 't_fork_user' t1 ON (t1.'id' <=> t.'user_id')` |
| fork/functions/NullSafeSqlTest.java#nullSafeEqualsPerDialect | 待做 |  | `SELECT t.'id' FROM 't_fork_user' t WHERE (t.'name' <=> ?)` |
| fork/functions/SmallFixesSqlTest.java#jsonArraySize | 待做 |  | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user' WHERE JSON_LENGTH('att…` |
| fork/functions/SmallFixesSqlTest.java#offsetWindowHasSpaceBeforeOrderBy | 待做 |  |  |
| fork/live/NullSafeLiveTest.java#nullMatchesNullAndEmptyBecomesNull | 待做 |  | `DROP TABLE {…}t_fork_user` |
| mysql8/ManyJoinTest.java#testElement5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'co…` |
| mysql8/ManyJoinTest.java#testElement6 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'co…` |
| mysql8/ManyJoinTest.java#testElement7 | 待做 |  | `SELECT t3.'type' AS 'value1',t3.'code' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'id',t1.'u…` |
| pgsql/QueryTest.java#query10 | 待做 |  | `SELECT COALESCE(t."id",?) FROM "t_blog" t WHERE t."deleted" = ? AND t."id" = ?` |
| pgsql/QueryTest.java#testNullDefault | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |

### 字符串聚合（joining）（`func.joining`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| MyTest1.java#testGroup15 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'id',MAX(t.'title') AS 'title',MIN(CAST(t.'stars' AS DECIMAL(36,18))…` |
| MyTest2.java#testGroup15 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'id',MAX(t.'title') AS 'title',MIN(CAST(t.'stars' AS DECIMAL(36,18))…` |
| QueryTest10.java#testDraft20 | 待做 |  | `SELECT SUBSTR(t.'content',1,8) AS 'value1',GROUP_CONCAT(t.'id' SEPARATOR ?) AS 'value2' FROM 't_blog…` |
| QueryTest12.java#testDraft21 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testNum8 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testOrSub1 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest14.java#testMapQuery | 待做 |  | `SELECT * FROM 't_topic' WHERE 'id' = ?` |
| QueryTest17.java#xxx123 | 待做 |  | `SELECT t.'id' AS 'my_code',GROUP_CONCAT(t1.'title' SEPARATOR ?) AS 'my_name' FROM 't_topic' t LEFT J…` |
| QueryTest19.java#testaaa | 待做 |  |  |
| QueryTest22.java#testSelectAnyElement2 | 待做 |  | `SELECT (SELECT GROUP_CONCAT(t1.'code' SEPARATOR ?) FROM 'doc_bank_card' t1 WHERE t1.'uid' = t.'id' A…` |
| QueryTest26.java#joiningTest | 待做 |  |  |
| StringFunctionsTest.java#joining2MySQL | 骨架 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:101 | `SELECT t.'code' AS 'value1',GROUP_CONCAT(DISTINCT (CASE WHEN t.'type' = ? THEN t1.'name' ELSE NULL E…` |
| StringFunctionsTest.java#joiningMSSQL | 骨架 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:137 | `SELECT t.[code] AS [value1],STRING_AGG(DISTINCT (CASE WHEN t.[type] = ? THEN t1.[name] ELSE NULL END…` |
| StringFunctionsTest.java#joiningMySQL | 骨架 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:173 | `SELECT t.'code' AS 'value1',GROUP_CONCAT(DISTINCT (CASE WHEN t.'type' = ? THEN t1.'name' ELSE NULL E…` |
| StringFunctionsTest.java#joiningORACLE | 骨架 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:208 | `SELECT t."code" AS "value1",LISTAGG(DISTINCT TO_CHAR((CASE WHEN t."type" = ? THEN t1."name" ELSE NUL…` |
| StringFunctionsTest.java#joiningPGSQL | 骨架 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:244 | `SELECT t."code" AS "value1",STRING_AGG(DISTINCT ((CASE WHEN t."type" = ? THEN t1."name" ELSE NULL EN…` |
| mysql8/MySQL8Test2.java#testGroupConcat1 | 待做 |  | `SELECT t.'id' AS 'value1',GROUP_CONCAT(DISTINCT t.'title' SEPARATOR ?) AS 'value2',GROUP_CONCAT(DIST…` |
| mysql8/MySQL8Test5.java#testJoining1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',t2.'__joining2__' AS '__part__column1' FROM 't_bank' t LEFT J…` |
| mysql8/MySQL8Test5.java#testJoining2 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',t2.'__joining2__' AS '__part__column1' FROM 't_bank' t LEFT J…` |
| mysql8/MySQL8Test5.java#testManyLimit1_1 | 待做 |  | `SELECT t.'id' AS 'value1',t4.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_…` |
| mysql8/MySQL8Test5.java#testManyLimit2 | 待做 |  | `SELECT t.'id' AS 'value1',t4.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_…` |
| mysql8/MySQL8Test5.java#testManyLimit4 | 待做 |  | `SELECT t.'id' AS 'value1',t2.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'ui…` |
| mysql8/MySQL8Test5.java#testManyLimit7 | 待做 |  | `SELECT t.'id' AS 'value1',t2.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'ui…` |
| mysql8/MySQL8Test5.java#testToSelectPageResult2 | 待做 |  | `SELECT t1.'id',t1.'name',t1.'create_time',t3.'__joining2__' AS '__part__column1' FROM (SELECT t.'id'…` |
| mysql8/MySQL8Test5.java#testToSelectPageResult3 | 待做 |  | `SELECT t1.'id',t1.'name',t1.'create_time',t3.'__joining2__' AS '__part__column1' FROM (SELECT t.'id'…` |
| pgsql/QueryTest19.java#testJoining | 待做 |  | `SELECT t."title" AS "value1",STRING_AGG((t."content")::TEXT, ?) AS "value2" FROM "t_blog" t WHERE t.…` |
| pgsql/QueryTest19.java#testJoining2 | 待做 |  | `SELECT t."title" AS "value1",STRING_AGG((t."content")::TEXT, ?) AS "value2" FROM "t_blog" t WHERE t.…` |
| pgsql/QueryTest19.java#testJoining3 | 待做 |  | `SELECT t."id",t."create_time",t."update_time",t."create_by",t."update_by",t."deleted",t."title",t."c…` |
| pgsql/QueryTest19.java#testJoining4 | 待做 |  | `SELECT t."id",t."create_time",t."update_time",t."create_by",t."update_by",t."deleted",t."title",t."c…` |

### JSON 函数（`func.json`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mysql8/JsonTest.java#testJsonField | 待做 |  |  |
| mysql8/JsonTest.java#testJsonField2 | 待做 |  | `SELECT 'id','name','extra_json','extra_json_array' FROM 't_test_json' WHERE JSON_UNQUOTE(JSON_EXTRAC…` |
| mysql8/JsonTest.java#testJsonField4 | 待做 |  |  |
| mysql8/JsonTest.java#testJsonField5 | 待做 |  |  |
| mysql8/JsonTest.java#testJsonField6 | 待做 |  |  |
| pgsql/Json2Test.java#testJsonField4 | 待做 |  |  |
| pgsql/Json2Test.java#testJsonField5 | 待做 |  |  |
| pgsql/Json2Test.java#testJsonField6 | 待做 |  |  |
| pgsql/Json2Test.java#testJsonField7 | 待做 |  |  |
| pgsql/JsonTest.java#testJsonField4 | 待做 |  |  |
| pgsql/JsonTest.java#testJsonField5 | 待做 |  |  |
| pgsql/JsonTest.java#testJsonField6 | 待做 |  |  |

### 数学函数（`func.math`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest12.java#testNum6 | 待做 |  | `SELECT ((t.'star' * t.'score') + ?) FROM 't_blog' t WHERE t.'deleted' = ? AND FLOOR(t.'score') = ?` |
| QueryTest23.java#testMath | 待做 |  | `SELECT t.'score' AS 'value1',ABS(t.'score') AS 'value2',SIGN(t.'score') AS 'value3',FLOOR(t.'score')…` |
| QueryTest4.java#testSQLFunc8 | 待做 |  | `SELECT ABS(t.'create_time') AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY t.'create_time' DE…` |
| QueryTest4.java#testSQLFunc9 | 待做 |  | `SELECT ROUND(t.'stars',1) AS 'title' FROM 't_topic' t WHERE t.'id' = ? ORDER BY t.'create_time' DESC` |

### 字符串函数（`func.string`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#testDoubleConcat | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:307 | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| InsertTest.java#insertTest | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:15 |  |
| MathQueryTest.java#testPropertyMath | 待做 |  | `SELECT t.'test_value' AS 'test_value',ABS(t.'test_value') AS 'test_abs',SIGN(t.'test_value') AS 'tes…` |
| MathQueryTest.java#testPropertyMath2 | 待做 |  | `SELECT t.'test_value' AS 'test_value',ABS(t.'test_value') AS 'test_abs',SIGN(t.'test_value') AS 'tes…` |
| MyTest1.java#testDraft1 | 待做 |  | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest1.java#testDraft1_1 | 待做 |  | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest1.java#testDraft2 | 待做 |  | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest1.java#testGroup11 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'id',COUNT(SUBSTR(t.'title',2,2)) AS 'star' FROM 't_topic' t WHERE t…` |
| MyTest1.java#testGroup12 | 待做 |  | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest1.java#testGroup13 | 待做 |  | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest1.java#testGroup14 | 待做 |  | `SELECT t2.'id' AS 'id',t2.'star' AS 'star' FROM (SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 's…` |
| MyTest1.java#testGroup16 | 待做 |  | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2)` |
| MyTest1.java#testGroup17 | 待做 |  | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2) HAV…` |
| MyTest1.java#testGroup18 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE t.'title' IS NO…` |
| MyTest1.java#testGroup19 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'value1',IFNULL(t1.'score',?) AS 'value2',COUNT(*) AS 'value3' FROM …` |
| MyTest1.java#testSelect5 | 待做 |  | `SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NU…` |
| MyTest1.java#testSelect6 | 待做 |  | `SELECT t2.'id' AS 'id',t2.'id_count' AS 'id_count' FROM (SELECT t1.'id' AS 'id',COUNT(*) AS 'id_coun…` |
| MyTest1.java#testSelect6_1 | 待做 |  | `SELECT t1.'id' AS 'id',COUNT(*) AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) A…` |
| MyTest1.java#testSelect6_2 | 待做 |  | `SELECT t1.'id' AS 'id',MIN(t1.'id') AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,…` |
| MyTest1.java#testSelect7 | 待做 |  | `SELECT t2.'value1' AS 'value1',t2.'value2' AS 'value2' FROM (SELECT t1.'value1' AS 'value1',COUNT(*)…` |
| MyTest1.java#testSelect9 | 待做 |  | `SELECT t1.'value1' AS 'value1',COUNT(*) AS 'value2' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.'tit…` |
| MyTest2.java#testGroup11 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'id',COUNT(SUBSTR(t.'title',2,2)) AS 'star' FROM 't_topic' t WHERE t…` |
| MyTest2.java#testGroup12 | 待做 |  | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest2.java#testGroup13 | 待做 |  | `SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 'star' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.…` |
| MyTest2.java#testGroup14 | 待做 |  | `SELECT t2.'id' AS 'id',t2.'star' AS 'star' FROM (SELECT t1.'value1' AS 'id',COUNT(t1.'value2') AS 's…` |
| MyTest2.java#testGroup16 | 待做 |  | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2)` |
| MyTest2.java#testGroup17 | 待做 |  | `SELECT SUBSTR('title',2,2) FROM 't_topic' WHERE 'title' IS NOT NULL GROUP BY SUBSTR('title',2,2) HAV…` |
| MyTest2.java#testGroup18 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE t.'title' IS NO…` |
| MyTest2.java#testGroup19 | 待做 |  | `SELECT SUBSTR(t.'title',2,2) AS 'value1',IFNULL(t1.'score',?) AS 'value2',COUNT(*) AS 'value3' FROM …` |
| MyTest2.java#testSelect5 | 待做 |  | `SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) AS 'title' FROM 't_topic' t WHERE t.'title' IS NOT NU…` |
| MyTest2.java#testSelect6 | 待做 |  | `SELECT t2.'id' AS 'id',t2.'id_count' AS 'id_count' FROM (SELECT t1.'id' AS 'id',COUNT(*) AS 'id_coun…` |
| MyTest2.java#testSelect6_1 | 待做 |  | `SELECT t1.'id' AS 'id',COUNT(*) AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,2) A…` |
| MyTest2.java#testSelect6_2 | 待做 |  | `SELECT t1.'id' AS 'id',MIN(t1.'id') AS 'id_count' FROM (SELECT t.'title' AS 'id',SUBSTR(t.'title',2,…` |
| MyTest2.java#testSelect7 | 待做 |  | `SELECT t2.'value1' AS 'value1',t2.'value2' AS 'value2' FROM (SELECT t1.'value1' AS 'value1',COUNT(*)…` |
| MyTest2.java#testSelect9 | 待做 |  | `SELECT t1.'value1' AS 'value1',COUNT(*) AS 'value2' FROM (SELECT t.'title' AS 'value1',SUBSTR(t.'tit…` |
| MyTest2.java#testTuple1 | 待做 |  | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest2.java#testTuple1_1 | 待做 |  | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| MyTest2.java#testTuple2 | 待做 |  | `SELECT t.'content' AS 'value1',CHAR_LENGTH(t.'content') AS 'value2' FROM 't_blog' t WHERE t.'deleted…` |
| QueryTest.java#query102_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:256 |  |
| QueryTest.java#query103 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:270 |  |
| QueryTest.java#query104 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:277 |  |
| QueryTest.java#query112 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:683 |  |
| QueryTest.java#query114 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:683 |  |
| QueryTest.java#query115 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:690 |  |
| QueryTest.java#query116 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:697 |  |
| QueryTest.java#query117 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:706 |  |
| QueryTest.java#query118 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:697 |  |
| QueryTest.java#query119 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:706 |  |
| QueryTest.java#query120 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:714 |  |
| QueryTest.java#query89 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:728 |  |
| QueryTest.java#query89_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:732 |  |
| QueryTest.java#query90 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:728 |  |
| QueryTest.java#query91 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:728 |  |
| QueryTest.java#query99 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:736 |  |
| QueryTest10.java#testDraft11 | 待做 |  | `SELECT t.'id' AS 'value1',LOWER(t.'title') AS 'value2',UPPER(t.'title') AS 'value3' FROM 't_topic' t…` |
| QueryTest10.java#testDraft17 | 待做 |  | `SELECT t.'id' AS 'value1',REPLACE(LOWER(t.'title'),?,?) AS 'value2' FROM 't_topic' t WHERE REPLACE(L…` |
| QueryTest10.java#testDraft18 | 待做 |  | `SELECT t.'id' AS 'value1',STRCMP(t.'title',t.'id') AS 'value2' FROM 't_topic' t WHERE STRCMP(t.'titl…` |
| QueryTest10.java#testDraft19 | 待做 |  | `SELECT t.'id' AS 'value1',LPAD(t.'title', 9, ?) AS 'value2' FROM 't_topic' t WHERE LPAD(t.'title', 5…` |
| QueryTest11.java#testx14 | 待做 |  | `SELECT 'id','stars','title','topic_type','create_time' FROM 't_topic_type' WHERE SUBSTR('id',2,2) = …` |
| QueryTest11.java#testx26 | 待做 |  | `SELECT t.'id' AS 'id',(CASE t.'id' WHEN ? THEN 1 ELSE 0 END) AS 'is_top',(CASE SUBSTR(t.'id',2,2) WH…` |
| QueryTest12.java#testDoc19 | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'t_sys_user'…` |
| QueryTest14.java#testLikeColumn | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest17.java#test1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest17.java#test3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest17.java#textLikeFunc | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#testSubString | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#query112 | 待做 |  |  |
| QueryTest2.java#query114 | 待做 |  |  |
| QueryTest2.java#query115 | 待做 |  |  |
| QueryTest2.java#query116 | 待做 |  |  |
| QueryTest2.java#query117 | 待做 |  |  |
| QueryTest2.java#query118 | 待做 |  |  |
| QueryTest2.java#query119 | 待做 |  |  |
| QueryTest2.java#query120 | 待做 |  |  |
| QueryTest23.java#testConcat | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testConcat2 | 待做 |  |  |
| QueryTest23.java#testConcat3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testIndexOf | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest23.java#testMathx1 | 待做 |  | `SELECT t.'score' AS 'value1',ABS(t.'score') AS 'value2',SIN(t.'score') AS 'value3',FLOOR(t.'score') …` |
| QueryTest23.java#testMathx2 | 待做 |  | `SELECT t.'score' AS 'value1',ROUND(t.'score') AS 'value2',SIGN(t.'score') AS 'value3',ROUND(t.'score…` |
| QueryTest23.java#testQueryConcat | 待做 |  |  |
| QueryTest24.java#testContains | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testJoinParameterCount | 待做 |  | `SELECT SUBSTR(t2.'title',2,2) AS 'value1' FROM (SELECT t.'id',t.'stars',t.'title',t.'create_time' FR…` |
| QueryTest24.java#testdraft1 | 待做 |  | `SELECT SUBSTR(t2.'title',2,2) AS 'value1' FROM (SELECT t.'id',t.'stars',t.'title',t.'create_time' FR…` |
| QueryTest3.java#query112 | 待做 |  |  |
| QueryTest3.java#query114 | 待做 |  |  |
| QueryTest3.java#query115 | 待做 |  |  |
| QueryTest3.java#query116 | 待做 |  |  |
| QueryTest3.java#query117 | 待做 |  |  |
| QueryTest3.java#query118 | 待做 |  |  |
| QueryTest3.java#query119 | 待做 |  |  |
| QueryTest3.java#query120 | 待做 |  |  |
| RelationTest.java#testSub3 | 待做 |  | `SELECT LOWER(t1.'name') FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_id…` |
| RelationTest.java#testSub4 | 待做 |  | `SELECT LOWER(t1.'name') FROM 'school_student' t LEFT JOIN 'school_class' t1 ON t1.'id' = t.'class_id…` |
| SkipTest.java#test11 | 待做 |  |  |
| StringFunctionsTest.java#compareToMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:12 | `SELECT (CASE WHEN t.[code] = t.[type] THEN 0 WHEN t.[code] > t.[type] THEN 1 ELSE -1 END) FROM [doc_…` |
| StringFunctionsTest.java#compareToMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:12 | `SELECT STRCMP(t.'code',t.'type') FROM 'doc_bank_card' t WHERE STRCMP(t.'code',?) = ?` |
| StringFunctionsTest.java#compareToORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:12 | `SELECT (CASE WHEN t."code" = t."type" THEN 0 WHEN t."code" > t."type" THEN 1 ELSE -1 END) FROM "doc_…` |
| StringFunctionsTest.java#compareToPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:12 | `SELECT (CASE WHEN t."code" = t."type" THEN 0 WHEN t."code" > t."type" THEN 1 ELSE -1 END) FROM "doc_…` |
| StringFunctionsTest.java#concatMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:23 | `SELECT CONCAT(?,t.[code],?,t.[type]) FROM [doc_bank_card] t WHERE CONCAT(t.[code],?) = ? AND CONCAT(…` |
| StringFunctionsTest.java#concatMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:23 | `SELECT CONCAT(?,t.'code',?,t.'type') FROM 'doc_bank_card' t WHERE CONCAT(t.'code',?) = ? AND CONCAT(…` |
| StringFunctionsTest.java#concatORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:23 | `SELECT ? \|\| t."code" \|\| ? \|\| t."type" FROM "doc_bank_card" t WHERE t."code" \|\| ? = ? AND t."code" \|\|…` |
| StringFunctionsTest.java#concatPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:23 | `SELECT CONCAT(?,t."code",?,t."type") FROM "doc_bank_card" t WHERE CONCAT(t."code",?) = ? AND CONCAT(…` |
| StringFunctionsTest.java#leftRightPadMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:34 | `SELECT [id],[uid],[code],[type],[bank_id] FROM [doc_bank_card] WHERE LPAD([code], 4) = ? AND LPAD([c…` |
| StringFunctionsTest.java#leftRightPadMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:34 | `SELECT 'id','uid','code','type','bank_id' FROM 'doc_bank_card' WHERE LPAD('code', 4) = ? AND LPAD('c…` |
| StringFunctionsTest.java#leftRightPadORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:34 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE LPAD("code", 4, ' ') = ? AND LP…` |
| StringFunctionsTest.java#leftRightPadPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:34 | `SELECT "id","uid","code","type","bank_id" FROM "doc_bank_card" WHERE LPAD("code", 4) = ? AND LPAD("c…` |
| StringFunctionsTest.java#lengthMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:45 | `SELECT LEN(t.[code]) FROM [doc_bank_card] t WHERE LEN(t.[code]) = ?` |
| StringFunctionsTest.java#lengthMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:45 | `SELECT CHAR_LENGTH(t.'code') FROM 'doc_bank_card' t WHERE CHAR_LENGTH(t.'code') = ?` |
| StringFunctionsTest.java#lengthORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:45 | `SELECT LENGTH(t."code") FROM "doc_bank_card" t WHERE LENGTH(t."code") = ?` |
| StringFunctionsTest.java#lengthPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:45 | `SELECT CHAR_LENGTH(t."code") FROM "doc_bank_card" t WHERE CHAR_LENGTH(t."code") = ?` |
| StringFunctionsTest.java#lowerUpperMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:56 | `SELECT LOWER(t.[id]) AS [value1],UPPER(t.[id]) AS [value2] FROM [doc_bank_card] t` |
| StringFunctionsTest.java#lowerUpperMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:56 | `SELECT LOWER(t.'id') AS 'value1',UPPER(t.'id') AS 'value2' FROM 'doc_bank_card' t` |
| StringFunctionsTest.java#lowerUpperORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:56 | `SELECT LOWER(t."id") AS "value1",UPPER(t."id") AS "value2" FROM "doc_bank_card" t` |
| StringFunctionsTest.java#lowerUpperPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:56 | `SELECT LOWER(t."id") AS "value1",UPPER(t."id") AS "value2" FROM "doc_bank_card" t` |
| StringFunctionsTest.java#replaceMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:67 | `SELECT REPLACE(t.[id],?,?) AS [value1] FROM [doc_bank_card] t WHERE REPLACE(t.[code],?,?) = ?` |
| StringFunctionsTest.java#replaceMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:67 | `SELECT REPLACE(t.'id',?,?) AS 'value1' FROM 'doc_bank_card' t WHERE REPLACE(t.'code',?,?) = ?` |
| StringFunctionsTest.java#replaceORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:67 | `SELECT REPLACE(t."id",?,?) AS "value1" FROM "doc_bank_card" t WHERE REPLACE(t."code",?,?) = ?` |
| StringFunctionsTest.java#replacePGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:67 | `SELECT REPLACE(t."id",?,?) AS "value1" FROM "doc_bank_card" t WHERE REPLACE(t."code",?,?) = ?` |
| StringFunctionsTest.java#subStringMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:78 | `SELECT SUBSTRING(t.[id],1,2) AS [value1],SUBSTRING(t.[id],1,t.[stars]) AS [value2] FROM [t_topic] t` |
| StringFunctionsTest.java#subStringMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:78 | `SELECT SUBSTR(t.'id',1,2) AS 'value1',SUBSTR(t.'id',1,t.'stars') AS 'value2' FROM 't_topic' t` |
| StringFunctionsTest.java#subStringORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:78 | `SELECT SUBSTR(t."id",1,2) AS "value1",SUBSTR(t."id",1,t."stars") AS "value2" FROM "t_topic" t` |
| StringFunctionsTest.java#subStringPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:78 | `SELECT SUBSTR(t."id",1,2) AS "value1",SUBSTR(t."id",1,t."stars") AS "value2" FROM "t_topic" t` |
| StringFunctionsTest.java#trimMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:89 | `SELECT LTRIM(RTRIM(t.[id])) AS [value1] FROM [doc_bank_card] t WHERE LTRIM(RTRIM(t.[code])) = ? AND …` |
| StringFunctionsTest.java#trimMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:89 | `SELECT TRIM(t.'id') AS 'value1' FROM 'doc_bank_card' t WHERE TRIM(t.'code') = ? AND LTRIM(t.'code') …` |
| StringFunctionsTest.java#trimORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:89 | `SELECT TRIM(t."id") AS "value1" FROM "doc_bank_card" t WHERE TRIM(t."code") = ? AND LTRIM(t."code") …` |
| StringFunctionsTest.java#trimPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/StringFunctionsTest.cs:89 | `SELECT TRIM(t."id") AS "value1" FROM "doc_bank_card" t WHERE TRIM(t."code") = ? AND LTRIM(t."code") …` |
| mysql8/ExampleTest.java#test2 | 待做 |  |  |

### 行锁（`lock`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| ForUpdateDialectToSQLSmokeTest.java#clickHouseForUpdateThrowsUnsupported | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#damengForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#db2ForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#duckDBForUpdateThrowsUnsupported | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#gaussDBForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#h2ForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#kingbaseESForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#msSQLForUpdateThrowsUnsupported | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#msSQLRowNumberForUpdateThrowsUnsupported | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#mysqlForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#oracleForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#pgsqlForUpdateToSQLSmoke | 待做 |  |  |
| ForUpdateDialectToSQLSmokeTest.java#sqliteForUpdateToSQLSmoke | 待做 |  |  |
| fork/live/LockWaitLiveTest.java#noWaitFailsAndSkipLockedSkips | 待做 |  | `DROP TABLE {…}t_fork_post` |
| mysql8/ForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 待做 |  |  |
| mysql8/ForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 待做 |  |  |
| mysql8/ForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 待做 |  |  |
| mysql8/ForUpdateTest.java#testForUpdateDuplicateCallThrows | 待做 |  |  |
| mysql8/ForUpdateTest.java#testForUpdateJoinNotSupported | 待做 |  |  |
| mysql8/ForUpdateTest.java#testForUpdateSqlAppendInTransaction | 待做 |  |  |
| mysql8/ForUpdateTest.java#testForUpdateWithoutTransactionThrows | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateDuplicateCallThrows | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateJoinNotSupported | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateSqlAppendInTransaction | 待做 |  |  |
| pgsql/PgSQLForUpdateTest.java#testForUpdateWithoutTransactionThrows | 待做 |  |  |

### 窗口函数（`window`，M2）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest18.java#testPartitionBy1 | 待做 |  | `SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time',t1.'__part__column1' AS '__part__column1' FROM…` |
| QueryTest18.java#testPartitionBy1_1 | 待做 |  | `SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time',t1.'__part__column1' AS '__part__column1' FROM…` |
| QueryTest26.java#testChunk3 | 待做 |  |  |
| QueryTest26.java#testChunk4 | 待做 |  |  |
| QueryTest26.java#testChunk5 | 待做 |  |  |
| QueryTest26.java#testChunk6 | 待做 |  |  |
| QueryTest26.java#testChunk7 | 待做 |  |  |
| doc/DocTest1.java#testBlog1 | 待做 |  | `SELECT t.'id',t.'title',t.'topic',t.'url',t.'star',t.'publish_time',t.'score',t.'status',(ROW_NUMBER…` |
| doc/DocTest1.java#testBlog2 | 待做 |  | `SELECT t1.'id',t1.'title',t1.'topic',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status' FRO…` |
| doc/DocTest1.java#testBlog3 | 待做 |  | `SELECT t1.'id',t1.'title',t1.'topic',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status',t1.…` |
| fork/functions/WindowSqlTest.java#distributionFunctions | 待做 |  |  |
| fork/functions/WindowSqlTest.java#framesFollowOrderBy | 待做 |  |  |
| fork/functions/WindowSqlTest.java#globalWindowWithoutPartition | 待做 |  |  |
| fork/functions/WindowSqlTest.java#invalidArgumentsAreRejected | 待做 |  |  |
| fork/functions/WindowSqlTest.java#partitionedWindowIsUnchanged | 待做 |  |  |
| fork/live/WindowLiveTest.java#runningTotalRowNumberAndNtile | 待做 |  | `DROP TABLE {…}t_fork_post` |
| mysql8/ManyJoinTest.java#test111 | 待做 |  |  |
| mysql8/MySQL8Test3.java#nextTest | 待做 |  | `SELECT t.'type' AS 'value1',LAG(t.'type', 1) OVER (ORDER BY t.'type' ASC,t.'code' DESC) AS 'value2',…` |
| mysql8/MySQL8Test3.java#nextTest1 | 待做 |  | `SELECT t.'type' AS 'value1',LAG(t.'type', 1) OVER (ORDER BY t.'type' ASC,t.'code' DESC) AS 'value2',…` |
| mysql8/MySQL8Test3.java#nextTest2 | 待做 |  |  |
| mysql8/MySQL8Test3.java#nextTest3 | 待做 |  | `SELECT t.'type' AS 'value1',LAG(t.'type', 1) OVER (ORDER BY t.'open_time' ASC) AS 'value2' FROM 't_b…` |
| mysql8/MySQL8Test5.java#testOffsetChunk | 待做 |  |  |
| pgsql/PartitionByTest.java#testPartitionBy16_1 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."__part__column2" AS "value2",t1."id" AS "value3" FROM (S…` |
| pgsql/PartitionByTest.java#testPartitionBy16_2 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."__part__column2" AS "value2",t1."id" AS "value3" FROM (S…` |
| pgsql/PartitionByTest.java#testPartitionBy16_3 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."__part__column2" AS "value2",t1."id" AS "value3" FROM (S…` |
| pgsql/QueryTest.java#testPartitionBy1 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy10 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy11 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy12 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy13 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy14 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy15 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy16 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy16_1 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy2 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy3 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy3_1 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy3_2 | 待做 |  | `SELECT t1."title",t1."url",t1."__part__column1" AS "rank" FROM (SELECT t."id",t."create_time",t."upd…` |
| pgsql/QueryTest.java#testPartitionBy4 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy5 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy6 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy7 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |
| pgsql/QueryTest.java#testPartitionBy8 | 待做 |  | `SELECT t1."__part__column1" AS "value1",t1."id" AS "value2" FROM (SELECT t."id",t."create_time",t."u…` |
| pgsql/QueryTest.java#testPartitionBy9 | 待做 |  | `SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."deleted",t1."titl…` |

### CTE（`query.cte`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest19.java#testToWithA43 | 待做 |  | `WITH 'with_Topic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WHERE …` |
| QueryTest19.java#testToWithAs | 待做 |  | `WITH 'withTableTopic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WH…` |
| QueryTest19.java#testToWithAs2 | 待做 |  | `WITH 'with_Topic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WHERE …` |
| QueryTest19.java#testToWithAs3 | 待做 |  | `WITH 'with_Topic' AS (SELECT t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_topic' t1 WHERE …` |
| QueryTest20.java#testGroupJoin2 | 待做 |  | `WITH 'with_TopicGroupTestDTO' AS (SELECT t1.'id' AS 'id',COUNT(t1.'id') AS 'id_count' FROM 't_topic'…` |
| mysql8/CTETest.java#cteJoinTest2 | 待做 |  | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'age' = ?) ,'…` |
| mysql8/CTETest.java#cteJoinTest7 | 待做 |  | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'name' = ? AN…` |
| mysql8/CTETest.java#cteJoinTest8 | 待做 |  | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'age' = ?) ,'…` |
| mysql8/CTETest.java#cteTest1 | 待做 |  | `WITH 'ttt' AS (SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t WHERE t.'age' = ?) SE…` |
| mysql8/M8BankTest.java#testExtraFilter4 | 待做 |  | `WITH 'with_Draft2' AS (SELECT t1.'bank_id' AS 'value1',COUNT(*) AS 'value2' FROM 't_bank_card' t1 GR…` |
| pgsql/QueryTest.java#testToWithAs2 | 待做 |  | `WITH "aa" AS (SELECT t1."id",t1."create_time",t1."update_time",t1."create_by",t1."update_by",t1."del…` |

### 分组与 having（含 rollup / cube）（`query.group`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EntityQueryAggregateTest5.java#test1 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test2 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test3 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test4 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test5 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test6 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test7 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test8 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest5.java#test9 | 待做 |  | `SELECT t.'id',t1.'id' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' GROUP BY t.'id',t1…` |
| GeneralFunctionsTest.java#limitOrderTest1 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:231 | `SELECT t1."value1" AS "value1",t1."value2" AS "value2" FROM (SELECT rt1.* FROM (SELECT rt.*, ROWNUM …` |
| GeneralFunctionsTest.java#limitOrderTest2 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:269 | `SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t1."value1" AS "value1",t1."valu…` |
| QueryTest.java#query16 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:594 | `SELECT COUNT(1) FROM (SELECT t1.'id' AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN …` |
| QueryTest.java#query16_1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:16 | `SELECT COUNT(1) FROM (SELECT IFNULL(t1.'id',?) AS 'id',SUM(t1.'score') AS 'score' FROM 't_topic' t I…` |
| QueryTest.java#query19 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:613 |  |
| QueryTest.java#query19_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:623 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query19_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:29 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query19_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:29 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:633 | `SELECT t.'id' AS 'id',COUNT(t.'id') AS 'id_count' FROM 't_topic' t WHERE t.'id' = ? GROUP BY t.'id'` |
| QueryTest.java#query21 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:644：Java 的 Select.of(key1(), …) 不写别名而 .as(…) 写别名；C# 成员初始化无法区分这两种写法，分组投影统一写别名 | `SELECT t.'id',COUNT(t.'id') AS 'id_count' FROM 't_topic' t WHERE t.'id' = ? GROUP BY t.'id'` |
| QueryTest.java#query43 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:653 |  |
| QueryTest.java#query44 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:667 | `SELECT t1.'id' AS 'id',t1.'id_count' AS 'id_count' FROM (SELECT t.'id' AS 'id',COUNT(t.'id') AS 'id_…` |
| QueryTest10.java#test2 | 待做 |  | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? GROUP BY t.'id' HAVING MAX(t.'id'…` |
| QueryTest10.java#test3 | 待做 |  | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? GROUP BY t.'id' HAVING MAX(t.'id'…` |
| QueryTest10.java#test4 | 待做 |  | `SELECT t.'id' FROM 't_blog' t WHERE t.'deleted' = ? AND t.'id' = ? GROUP BY t.'id' HAVING MAX(t.'id'…` |
| QueryTest10.java#test5 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest10.java#testDraft10 | 待做 |  | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t WHERE t.'title' LIKE ? AND t.'create…` |
| QueryTest11.java#testdraft | 待做 |  | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest13.java#orderTest1 | 待做 |  | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM (SELECT t.'id' AS 'blogId',COUNT(*) AS 'b…` |
| QueryTest13.java#orderTest1_1 | 待做 |  | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM (SELECT t.'id' AS 'blogId',COUNT(*) AS 'b…` |
| QueryTest15.java#test4 | 待做 |  | `SELECT CEILING((timestampdiff(DAY, t.'birthday', NOW()) / ?)) AS 'value1',CONCAT(t.'first_name',t.'l…` |
| QueryTest18.java#test1111 | 待做 |  |  |
| QueryTest19.java#testUnion1 | 待做 |  | `SELECT '未开始'AS task_status UNION ALL SELECT '进行中' UNION ALL SELECT '已完成' UNION ALL SELECT '审核中' UNIO…` |
| QueryTest2.java#groupTest | 待做 |  | `SELECT t.'id' AS 'id',COUNT(t.'id') AS 'id_count',SUM(t.'score') AS 'score_sum',MAX(t.'publish_time'…` |
| QueryTest2.java#groupTest1 | 待做 |  | `SELECT t.'id' AS 'id',SUM(t.'score') AS 'score',MAX(t.'publish_time') AS 'publish_time',MIN(t.'order…` |
| QueryTest2.java#groupTest2 | 待做 |  | `SELECT t.'id' AS 'id',SUM(DISTINCT t.'score') AS 'score_sum',COUNT(DISTINCT t.'id') AS 'id_count',AV…` |
| QueryTest2.java#groupTest3 | 待做 |  | `SELECT t.'id' AS 'id',SUM(DISTINCT t.'score'),COUNT(DISTINCT t.'order'),AVG(DISTINCT t.'status') FRO…` |
| QueryTest2.java#groupTest4 | 待做 |  | `SELECT t.'id' AS 'id',SUM(DISTINCT t.'score') AS 'score',COUNT(DISTINCT t.'order') AS 'order',AVG(DI…` |
| QueryTest2.java#query122 | 待做 |  | `SELECT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' LEFT J…` |
| QueryTest2.java#query122x | 待做 |  | `SELECT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHERE …` |
| QueryTest2.java#query123 | 待做 |  | `SELECT DISTINCT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'i…` |
| QueryTest2.java#query123x | 待做 |  | `SELECT DISTINCT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'i…` |
| QueryTest20.java#testFullSingleTable | 待做 |  | `SELECT t1.'value1' AS 'value1',t1.'value2' AS 'value2',t1.'value3' AS 'value3' FROM (SELECT t.'title…` |
| QueryTest20.java#testGroup | 待做 |  | `SELECT t.'title' AS 'value1',COUNT(*) AS 'value2',MAX(t.'create_time') AS 'value3',SUM(t.'score') AS…` |
| QueryTest20.java#testGroup1 | 待做 |  | `SELECT t.'title' AS 'key',COUNT(*) AS 'id_count',MAX(t.'create_time') AS 'create_time_max',SUM(t.'sc…` |
| QueryTest20.java#testGroupJoin1 | 待做 |  | `SELECT t.'id' AS 'value1',t3.'id_count' AS 'value2' FROM 't_blog' t LEFT JOIN (SELECT t1.'id' AS 'id…` |
| QueryTest21.java#testWhereGroup | 待做 |  | `SELECT t.'title' AS 'value1',COUNT((CASE WHEN t.'star' >= ? THEN t.'id' ELSE NULL END)) AS 'value2' …` |
| QueryTest21.java#testWhereGroup1 | 待做 |  | `SELECT t.'title' AS 'value1',COUNT((CASE WHEN t.'star' >= ? THEN ? ELSE NULL END)) AS 'value2' FROM …` |
| QueryTest21.java#testWhereGroup2 | 待做 |  | `SELECT t.'title' AS 'value1',COUNT(DISTINCT (CASE WHEN t.'star' >= ? THEN ? ELSE NULL END)) AS 'valu…` |
| QueryTest21.java#testWhereGroup3 | 待做 |  | `SELECT t.'title' AS 'value1',COUNT(DISTINCT ?) AS 'value2' FROM 't_blog' t WHERE t.'deleted' = ? AND…` |
| QueryTest21.java#testWhereGroup4 | 待做 |  | `SELECT t.'title' AS 'value1',COUNT(DISTINCT (CASE WHEN t.'star' >= ? THEN ? ELSE NULL END)) AS 'valu…` |
| QueryTest21.java#testWhereGroup5 | 待做 |  | `SELECT t.'title' AS 'value1',SUM((CASE WHEN t.'star' >= ? THEN t.'score' ELSE ? END)) AS 'value2' FR…` |
| QueryTest21.java#testWhereGroup6 | 待做 |  | `SELECT t.'title' AS 'value1',SUM(DISTINCT (CASE WHEN t.'star' >= ? THEN t.'score' ELSE ? END)) AS 'v…` |
| QueryTest22.java#testManyGroup39 | 待做 |  |  |
| QueryTest23.java#testaaa1 | 待做 |  |  |
| QueryTest25.java#tupleTest1 | 待做 |  | `SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest25.java#tupleTest2 | 待做 |  | `SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id',t.'title'` |
| QueryTest25.java#tupleTest3 | 待做 |  | `SELECT t1.'value1' AS 'value1' FROM (SELECT t.'id' AS 'value1' FROM 't_topic' t GROUP BY t.'id') t1 …` |
| QueryTest25.java#tupleTest4 | 待做 |  | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2' FROM 't_topic' t GROUP BY t.'id'` |
| QueryTest25.java#tupleTest5 | 待做 |  | `SELECT t.'id' AS 'value1',COUNT(*) AS 'value2',SUM(t.'stars') AS 'value3' FROM 't_topic' t GROUP BY …` |
| QueryTest26.java#test | 待做 |  |  |
| QueryTest26.java#test23 | 待做 |  | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |
| QueryTest28.java#testQueryAggregate2 | 待做 |  | `SELECT COUNT(*) AS 'value1',AVG(t.'stars') AS 'value2' FROM 't_topic' t WHERE t.'title' LIKE CONCAT(…` |
| QueryTest3.java#query122 | 待做 |  | `SELECT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' LEFT J…` |
| QueryTest3.java#query123 | 待做 |  | `SELECT DISTINCT t.'id' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'i…` |
| QueryTest7.java#testQuery10 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery11 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery12 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery13 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery14 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery15 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery16 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery17 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest7.java#testQuery18 | 待做 |  | `SELECT t1.'id',SUM(t1.'score') AS 'score' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = …` |
| QueryTest9.java#havingQueryTest1 | 待做 |  | `SELECT t.'id' AS 'id',COUNT(t.'id') AS 'star',MAX(t.'id') AS 'title' FROM 't_blog' t WHERE t.'delete…` |
| fork/functions/GroupingSqlTest.java#mysqlWithRollupAndNoCube | 待做 |  |  |
| fork/functions/GroupingSqlTest.java#plainGroupByIsUnchanged | 待做 |  |  |
| fork/functions/GroupingSqlTest.java#postgresRollupAndCube | 待做 |  | `SELECT t."user_id" AS "value1",t."title" AS "value2",SUM(t."amount") AS "value3",GROUPING(t."title")…` |
| fork/functions/GroupingSqlTest.java#rollupNeedsGroupBy | 待做 |  |  |
| fork/live/GroupingLiveTest.java#rollupAddsGrandTotal | 待做 |  | `DROP TABLE {…}t_fork_post` |
| mysql8/M8BankTest.java#testExtraFilter3 | 待做 |  | `SELECT t.'name' AS 'value1',t3.'value2' AS 'value2' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' A…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin3 | 待做 |  | `SELECT t.'name' AS 'value1',COUNT((CASE WHEN IFNULL(t2.'__any2__',?) = ? THEN ? ELSE NULL END)) AS '…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin3_1 | 待做 |  | `SELECT t.'name' AS 'value1',COUNT((CASE WHEN t2.'__any2__' = ? THEN ? ELSE NULL END)) AS 'value2' FR…` |
| mysql8/M8Test1.java#test23 | 待做 |  | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |

### 显式 join（`query.join`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query10 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:158 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest.java#query10_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:188 | `SELECT y.'id',y.'stars',y.'title',y.'create_time' FROM 't_topic' y LEFT JOIN 't_blog' z ON z.'delete…` |
| QueryTest.java#query11 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:195 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query111 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:221 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest.java#query113 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:690 |  |
| QueryTest.java#query11_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:213 | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest.java#query121 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.cs:740 | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest.java#query17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:48 | `SELECT COUNT(1) FROM (SELECT t1.'create_time',t1.'update_time',t1.'create_by',t1.'update_by',t1.'del…` |
| QueryTest.java#query26 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:59 | `SELECT t1.'id',t1.'create_time',t1.'update_time',t1.'create_by',t1.'update_by',t1.'deleted',t1.'titl…` |
| QueryTest.java#query40 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:70 | `SELECT COUNT(*) FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHE…` |
| QueryTest.java#query78 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:84 | `SELECT t1.'title',t1.'content',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status',t1.'order…` |
| QueryTest.java#query79 | 不适用 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Joins.cs:98：Java 允许投影里两列都映射到 id、后者覆盖前者；C# 成员初始化同一成员不能赋值两次 | `SELECT t1.'id',t1.'title',t1.'content',t1.'url',t1.'star',t1.'publish_time',t1.'score',t1.'status',t…` |
| QueryTest.java#query80 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:71：C# 投影的成员名不会重复；重复列名读成字典时报错，用原生 SQL 验证 |  |
| QueryTest11.java#testx9 | 待做 |  |  |
| QueryTest12.java#testDoc15 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc21 | 待做 |  |  |
| QueryTest12.java#testDraftInteger | 待做 |  |  |
| QueryTest12.java#testDraftInteger1 | 待做 |  |  |
| QueryTest12.java#testJoinSelect | 待做 |  | `SELECT t1.'id',t1.'create_time',t1.'update_time',t1.'create_by',t1.'update_by',t1.'deleted',t1.'titl…` |
| QueryTest12.java#testLogicDeleteRecentlyTable | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testLogicDeleteRecentlyTable1 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testMap1 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testMap2 | 待做 |  | `SELECT t.'id',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'content',t.'url',…` |
| QueryTest14.java#testLikeColumn2 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest15.java#test5 | 待做 |  | `SELECT t.'id',t.'first_name',t.'last_name',t.'birthday',CONCAT(t.'first_name',t.'last_name') AS 'ful…` |
| QueryTest15.java#test6 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest18.java#test | 待做 |  |  |
| QueryTest18.java#test124 | 待做 |  | `SELECT t.'id',t.'title',t1.'id' AS 'content',t1.'top' AS 'score' FROM 't_topic' t LEFT JOIN 't_blog'…` |
| QueryTest18.java#test18 | 待做 |  |  |
| QueryTest19.java#mapJoinFixed | 待做 |  | `SELECT t.'id1' AS 'id1',t.'name1' AS 'name1',t1.'id2' AS 'id2',t1.'name2' AS 'name2' FROM 'table1' t…` |
| QueryTest2.java#query113 | 待做 |  |  |
| QueryTest2.java#query121 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest2.java#query124 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest2.java#query124_1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest2.java#query125 | 待做 |  | `SELECT t.'id',t1.'title',t2.'star' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.…` |
| QueryTest2.java#query126 | 待做 |  |  |
| QueryTest20.java#testGroupJoin3 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest20.java#testJoinMultiOn | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest20.java#testJoinVo | 待做 |  | `SELECT t.'title' AS 'column1',t1.'title' AS 'column2' FROM 't_blog' t LEFT JOIN 't_topic' t1 ON t.'i…` |
| QueryTest20.java#testMapIgnore | 待做 |  | `SELECT t.'id',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'content',t.'url',…` |
| QueryTest20.java#testWhere3 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest24.java#aaa | 待做 |  |  |
| QueryTest25.java#testDraft | 待做 |  | `SELECT t1.'id',t1.'star',t.'title' AS 'content' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'delete…` |
| QueryTest25.java#testDraft2 | 待做 |  | `SELECT t1.'id',t1.'star',t.'title' AS 'content' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'delete…` |
| QueryTest26.java#testRelationOn | 待做 |  | `SELECT t.'aid',t.'aname' FROM 'on_relation_a' t LEFT JOIN 'on_relation_b' t2 ON t2.'aid' = t.'aid' L…` |
| QueryTest26.java#testViewWhere | 待做 |  |  |
| QueryTest26.java#testViewWhere2 | 待做 |  |  |
| QueryTest26.java#testaa | 待做 |  | `SELECT t.'id',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'content',t.'url',…` |
| QueryTest27.java#testDerivedTable2 | 待做 |  | `SELECT t2.'score' AS 'score',t2.'status1' AS 'status1',t2.'order' AS 'order',t2.'is_top' AS 'is_top'…` |
| QueryTest27.java#testDerivedTable3 | 待做 |  | `SELECT t2.'score' AS 'score',t2.'status1' AS 'status1',t2.'order' AS 'order',t2.'is_top' AS 'is_top'…` |
| QueryTest27.java#testDerivedTable4 | 待做 |  | `SELECT t2.'score' AS 'score',t2.'status1' AS 'status1',t2.'order' AS 'order',t2.'is_top' AS 'is_top'…` |
| QueryTest27.java#testQuery | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'id' = ?` |
| QueryTest27.java#testSave1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 'easy-query-test'.'t_sy…` |
| QueryTest27.java#testaaaa54 | 待做 |  |  |
| QueryTest27.java#testaaaa541 | 待做 |  |  |
| QueryTest3.java#query113 | 待做 |  |  |
| QueryTest3.java#query121 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query124_2 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query124_4 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query125 | 待做 |  | `SELECT t.'id',t1.'title',t2.'star' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.…` |
| QueryTest3.java#query126 | 待做 |  |  |
| QueryTest4.java#queryTest7 | 待做 |  |  |
| QueryTest8.java#test1 | 待做 |  |  |
| QueryTest8.java#testJoin10 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin5 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin6 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin7 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin8 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest8.java#testJoin9 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| QueryTest9.java#selectCount2 | 待做 |  | `SELECT COUNT(*) FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHE…` |
| QueryTest9.java#selectCount3 | 待做 |  | `SELECT COUNT(*) FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'deleted' = ? AND t.'id' = t1.'id' WHE…` |
| QueryTest9.java#selectThrow3 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'del…` |
| QueryTest9.java#selectThrow4 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'del…` |
| QueryTest9.java#selectThrow5 | 待做 |  |  |
| QueryTest9.java#selectThrow6 | 待做 |  |  |
| QueryTest9.java#selectThrow7 | 待做 |  |  |
| QueryTest9.java#selectThrow9 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t INNER JOIN 't_blog' t1 ON t1.'del…` |
| UpdateTest.java#aaaa | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:459：Java 把 BlogEntity 全部列映射到 SysMenuDto（类型并不匹配）；C# 只投影用到的两列 |  |
| mysql8/CTETest.java#cteTest5 | 待做 |  | `WITH 'm8_user_temp' AS (SELECT t.'id',t.'name' FROM 'm8_user' t WHERE t.'age' IS NULL) SELECT t2.'na…` |
| mysql8/CrossJoinTest.java#crossJoin1 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t CROSS JOIN 't…` |
| mysql8/MySQL8Test2.java#test1 | 待做 |  | `SELECT t2.'id',t2.'name',t2.'create_time',t2.'__part__column1' AS '__part__column1' FROM (SELECT t.'…` |
| mysql8/MySQL8Test2.java#test2 | 待做 |  | `SELECT t3.'id',t3.'name',t3.'create_time',t3.'__part__column1' AS '__part__column1' FROM (SELECT t.'…` |
| mysql8/MySQL8Test2.java#test3 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN (SE…` |
| mysql8/MySQL8Test2.java#test4 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN (SE…` |
| mysql8/MySQL8Test2.java#test5 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t LEFT JOIN (SE…` |
| mysql8/MySQL8Test3.java#testDTOWhereToTableWhere | 待做 |  | `SELECT t2.'name' AS 'name',t2.'phone' AS 'phone',t2.'code2' AS 'code2' FROM (SELECT t.'name' AS 'nam…` |
| mysql8/MySQL8Test3.java#testDTOWhereToTableWhere1 | 待做 |  | `SELECT t2.'name' AS 'name',t2.'phone' AS 'phone',t2.'code2' AS 'code2' FROM (SELECT t.'name' AS 'nam…` |
| mysql8/MySQL8Test5.java#testWhereJoin | 待做 |  |  |

### 无实体的 Map 查询、toMap（`query.map`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:58 |  |
| QueryTest.java#query23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.NativeSql.cs:62 |  |
| QueryTest14.java#testDynamicTable2 | 待做 |  | `SELECT t1.'id' AS 'id' FROM (SELECT t.'id' AS 'id' FROM 'aaa' t) t1 LEFT JOIN (SELECT * FROM 'bbb' t…` |
| QueryTest15.java#test1223 | 待做 |  | `SELECT * FROM '123' WHERE 'name' = ?` |
| QueryTest15.java#test8 | 待做 |  |  |
| QueryTest15.java#testxxx | 待做 |  | `SELECT * FROM 'table1' t INNER JOIN 'table2' t1 ON t.'id1' = t1.'id2'` |
| QueryTest16.java#test16 | 待做 |  | `SELECT t4.'a' AS 'a',t4.'b' AS 'b' FROM ( (SELECT t.'a' AS 'a',t.'b' AS 'b' FROM 't_user' t) UNION (…` |
| QueryTest19.java#mapJoin | 待做 |  | `SELECT t.'table1Column' AS 'table1Column',t1.'table2Column' AS 'table2Column',t2.'table3Column' AS '…` |
| QueryTest19.java#mapQuery | 待做 |  | `SELECT 'id' AS 'id','age' AS 'age','id_card' AS 'id_card' FROM 'aaa' WHERE 'id' = ? AND 'name' = ?` |
| QueryTest19.java#testMapJson | 待做 |  | `SELECT * FROM 'table1' t LEFT JOIN 'table2' t1 ON t1.'id1' = t.'id2' LEFT JOIN 'table3' t2 ON t2.'id…` |
| QueryTest28.java#testQueryMap | 待做 |  | `SELECT * FROM 't_topic' WHERE 'id' = ?` |

### 复用查询（cloneQueryable）（`query.reuse`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest16.java#testSubSumOracle2 | 待做 |  | `SELECT COUNT(*) FROM "t_company" WHERE "name" LIKE ?` |
| QueryTest18.java#testSubCountPage | 待做 |  | `SELECT COUNT(*) FROM 'easy-query-test'.'t_sys_user' WHERE 'phone' LIKE ?` |

### 流式读取（`query.stream`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest17.java#testOptional | 待做 |  |  |
| QueryTest18.java#testChunk1 | 待做 |  |  |
| QueryTest18.java#testChunk2 | 待做 |  |  |
| QueryTest18.java#testChunk3 | 待做 |  |  |
| QueryTest18.java#testChunk4 | 待做 |  |  |
| QueryTest18.java#testStreamChunk1 | 待做 |  |  |
| QueryTest23.java#subquery | 待做 |  | `select * from t_blog where id <> ?` |
| QueryTest25.java#test12 | 待做 |  |  |
| pgsql/QueryTest19.java#testChunk | 待做 |  |  |

### union / union all（`query.union`，M3）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#unionSQLite | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:369 | `SELECT t3."id",t3."stars",t3."title",t3."create_time" FROM ( SELECT t."id",t."stars",t."title",t."cr…` |
| QueryTest.java#query52 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:21 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:21 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_1_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:26 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_1_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:26 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:35 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:35 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query52_3_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:39 | `SELECT t2.'id',t2.'stars',t2.'title',t2.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query53 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:47 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query54 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:50 | `SELECT t3.'id',t3.'stars',t3.'title',t3.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query55 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:53 | `SELECT t3.'create_time',t3.'stars' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't…` |
| QueryTest.java#query56 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:58 | `SELECT t5.'id',t5.'stars',t5.'title',t5.'create_time' FROM ( (SELECT t3.'id',t3.'stars',t3.'title',t…` |
| QueryTest.java#query57 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.Union.cs:68 |  |
| QueryTest.java#query65 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:68：别名编号：Java 复用了执行过的查询对象，外层别名是 t7；C# 连续编号为 t4 | `SELECT t7.'id',t7.'stars',t7.'title',t7.'create_time' FROM ( (SELECT t.'id',t.'stars',t.'title',t.'c…` |
| QueryTest.java#query66 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:80：别名编号：Java 的分支与外层是 t、t2、t4/t5、t8；C# 连续编号为 t、t1、t2/t3、t4 | `SELECT t8.'id',t8.'stars',t8.'abc' AS 'abc' FROM ( (SELECT t.'id',t.'stars',t.'title' AS 'abc' FROM …` |
| QueryTest.java#query67 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:94：别名编号：Java 的外层别名是 t5（中间跳号），C# 连续编号为 t2；Java 的 proxy set 同名列也写 AS，C# 成员初始化同名列不写 | `SELECT t5.'id',t5.'stars',t5.'abc' AS 'abc' FROM ( (SELECT t.'id',t.'stars',t.'title' AS 'abc' FROM …` |
| QueryTest11.java#testx20 | 待做 |  | `SELECT t6.'value1' AS 'value1',t6.'value2' AS 'value2' FROM ( (SELECT ? AS 'value1',COUNT(*) AS 'val…` |
| mysql8/MySQL8Test3.java#testOneIsNull | 待做 |  |  |

### 按 DTO 自动带出（selectAutoInclude）（`dto.autoInclude`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DirectRelationTest.java#test4 | 待做 |  | `SELECT t.'c1',t.'c2',t.'c3',t.'c4' FROM 'direct1' t` |
| DirectRelationTest.java#test5 | 待做 |  | `SELECT t.'c1',t.'c2',t.'c3',t.'c4' FROM 'direct1' t` |
| GeneralFunctionsTest.java#testKingbaseCte | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:338 | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0+0 AS "deep",t1."id",t1."parent_id",t1."content",t1."user…` |
| QueryTest14.java#test1 | 待做 |  | `SELECT t.'id',t.'name',t.'password' FROM 't_test_user' t` |
| QueryTest16.java#test111 | 待做 |  | `SELECT t.'id' AS '__relation__id' FROM 'sys_user' t WHERE t.'deleted' = ?` |
| QueryTest19.java#testDoc100 | 待做 |  | `SELECT t.'id',t.'name' FROM 't_user' t` |
| QueryTestRelationTest.java#testRelation | 待做 |  | `SELECT 'id','com_id','user_id','gw' FROM 'my_com_user'` |
| doc/DocTest1.java#testBlog5 | 待做 |  | `SELECT t1.'name' AS 'uname1',t3.'name' AS 'uname2',t.'id',t.'name' FROM 'test_self' t LEFT JOIN 'my_…` |
| mysql8/IncludeConditionTest.java#testInclude3 | 待做 |  | `SELECT t.'id',t.'name',t.'version' AS '__relation__version' FROM 'my_task' t WHERE t.'id' = ?` |
| mysql8/IncludeConditionTest.java#testInclude4 | 待做 |  | `SELECT t.'id',t.'name',t.'version' FROM 'my_task' t WHERE t.'id' = ?` |
| mysql8/M8BankTest.java#partitionBy | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t` |
| mysql8/M8BankTest.java#testExtraDTO | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/M8BankTest.java#testExtraDTO123 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/M8BankTest.java#testExtraFilter | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t` |
| mysql8/M8BankTest.java#testExtraFilter2 | 待做 |  | `SELECT IFNULL(t2.'__count2__',0) AS 'card_count',t.'id',t.'name',t.'phone',t.'age',t.'create_time' F…` |
| mysql8/M8PCABTest.java#testFlat | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t` |
| mysql8/M8PCABTest.java#testFlat1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t` |
| mysql8/M8PCABTest.java#testFlat2 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t` |
| mysql8/M8Test1.java#test24 | 待做 |  | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |
| mysql8/M8Test1.java#test25 | 待做 |  | `SELECT t1.'uid' AS 'uid',t1.'count' AS 'count' FROM (SELECT t.'uid' AS 'uid',COUNT(*) AS 'count' FRO…` |
| mysql8/ManyJoinTest.java#testFlatExtra | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 'm8_role' t` |
| mysql8/ManyJoinTest.java#testFlatMenuOwner | 待做 |  |  |
| mysql8/ManyJoinTest.java#testFlatMenuOwner1 | 待做 |  |  |
| mysql8/ManyJoinTest.java#testFlatMenuOwnerFlatValueConverter | 待做 |  |  |
| mysql8/MySQL8Test2.java#includePartitionBy | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#includePartitionBy1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test3.java#testQueryFirstLimit2 | 待做 |  | `SELECT t2.'code',t2.'type',t2.'open_time',t2.'uid' AS '__relation__uid' FROM (SELECT t1.'id' AS 'id'…` |
| mysql8/MySQL8Test3.java#testTree2 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'deep',t1.'id',t1.'parent_id',t1.'content',t1.'user_i…` |
| mysql8/MySQL8Test3.java#testTree3 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'deep',t1.'id',t1.'parent_id',t1.'content',t1.'user_i…` |
| mysql8/MySQL8Test3.java#testTree4 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'deep',t1.'id',t1.'parent_id',t1.'content',t1.'user_i…` |
| mysql8/MySQL8Test3.java#testTree5 | 待做 |  |  |
| mysql8/MySQL8Test5.java#testSysDeptCTE | 待做 |  |  |
| pgsql/QueryCteTest.java#tree7 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep1",t1."id",t1."parent_id",t1."name" FROM "public…` |
| pgsql/QueryTest19.java#tree7 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep1",t1."id",t1."parent_id",t1."name" FROM "catego…` |
| pgsql/QueryTest19.java#tree8 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |

### 按键填充（fillMany / fillOne）（`fill`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| RelationTest.java#provinceTest2 | 待做 |  |  |
| RelationTest.java#provinceTest3 | 待做 |  |  |
| RelationTest.java#provinceTest4 | 待做 |  | `SELECT 'code','name' FROM 't_province'` |
| RelationTest.java#provinceVoTest | 待做 |  |  |

### 一对多展开（flatElement / elements）（`nav.flat`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest12.java#testDoc24 | 待做 |  | `DELETE FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE (EXISTS(SELECT …` |
| QueryTest16.java#test1 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE EXISTS(SELECT 1 …` |
| QueryTest16.java#test15 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE (t.'name' LIKE ?…` |
| QueryTest16.java#test2 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN (SELECT t2.'…` |
| QueryTest16.java#test3 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN (SELECT t2.'…` |
| QueryTest16.java#testDocSub | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_company' t WHERE EXISTS(SELECT 1 FROM 't_user' t1 WHE…` |
| QueryTest17.java#testFlatElement1 | 待做 |  | `SELECT t.'id',t.'f1',t.'f2',t.'parent_id' FROM 'MyObject' t LEFT JOIN 'MyObjectParent' t1 ON t1.'id'…` |
| QueryTest17.java#testFlatElement2 | 待做 |  | `SELECT t.'id',t.'f1',t.'f2',t.'parent_id' FROM 'MyObject' t LEFT JOIN 'MyObjectParent' t1 ON t1.'id'…` |
| QueryTest17.java#testNop2 | 待做 |  | `SELECT t4.'value1' AS 'value1',SUM(t4.'value2') AS 'value2',COUNT(t5.'type') AS 'value3' FROM (SELEC…` |
| QueryTest17.java#testaaa | 待做 |  |  |
| QueryTest18.java#test8_1 | 待做 |  |  |
| QueryTest22.java#testManyGroup17 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup18 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest25.java#test11a | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest25.java#testToListFlat1 | 待做 |  |  |
| QueryTest25.java#xxx22 | 待做 |  |  |
| QueryTest26.java#includeFlat | 待做 |  |  |
| QueryTestRelationTest.java#test1 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'gw' FROM 'my_com_user' t LEFT JOIN 'my_company_info' t1 ON t…` |
| SubJoinTest.java#testSubJoinCaseWhen1 | 待做 |  | `SELECT IFNULL((SELECT SUM(t3.'age') FROM 'sub_join_book' t2 LEFT JOIN 'sub_join_author' t3 ON t3.'id…` |
| mysql8/CTETest.java#cteTest4 | 待做 |  | `WITH 'm8_user_temp' AS (SELECT t.'id',t.'name' FROM 'm8_user' t WHERE t.'age' IS NULL) SELECT t2.'id…` |
| mysql8/CTETest.java#cteTest6 | 待做 |  | `WITH 'm8_user_temp2' AS (SELECT t.'id',t.'name',t.'age',(ROW_NUMBER() OVER (PARTITION BY t.'age' ORD…` |
| mysql8/M8BankTest.java#test11 | 待做 |  | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test12 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'u…` |
| mysql8/M8BankTest.java#test13 | 待做 |  | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test14 | 待做 |  | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test15 | 待做 |  | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test16 | 待做 |  | `SELECT t.'name' AS 'value1',t4.'__joining3__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'…` |
| mysql8/M8BankTest.java#test17 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t3.'u…` |
| mysql8/M8BankTest.java#test7 | 待做 |  | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t1.'type' SEPARATOR ?) FROM 't_bank_card' t1 WHERE …` |
| mysql8/M8BankTest.java#test7_1 | 待做 |  | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t2.'type' SEPARATOR ?) FROM (SELECT t1.'id',t1.'uid…` |
| mysql8/M8BankTest.java#test7_2 | 待做 |  | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t1.'type' SEPARATOR ?) FROM 't_bank_card' t1 WHERE …` |
| mysql8/M8BankTest.java#test8 | 待做 |  | `SELECT t.'name' AS 'value1',(SELECT GROUP_CONCAT(t4.'type' SEPARATOR ?) FROM 't_bank_card' t4 WHERE …` |
| mysql8/M8BankTest.java#testFlatTest | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest2 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',(NOT (EXISTS(SELECT 1 FROM (SELECT t1.'id',t1.'uid',t1.'code'…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest3 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',IFNULL(t4.'__none2__',?) AS '__part__column1' FROM 't_bank' t…` |
| mysql8/M8OrderTest.java#testSubQuery | 待做 |  | `SELECT t.'id',t.'no',t.'price',t.'create_time' FROM 't_order' t INNER JOIN (SELECT t1.'order_id' AS …` |
| mysql8/M8OrderTest.java#testSubQuery3 | 待做 |  | `SELECT t.'id',t.'no',t.'price',t.'create_time' FROM 't_order' t INNER JOIN (SELECT t1.'order_id' AS …` |
| mysql8/ManyJoinTest.java#testElement8 | 待做 |  | `SELECT t4.'__max2__' AS 'value1',t7.'type' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t3.'uid' …` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin2 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin24 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin4 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin5 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/MySQL8Test3.java#testFlatElement | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testFlatElement1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testFlatElement2 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t WHERE EXISTS(SELECT 1 FROM 'm8_city' t1 WHERE t1.'pid' =…` |
| mysql8/MySQL8Test3.java#testFlatElement3 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin2 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin3 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testGroupJoin4 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',(COUNT(?)…` |
| mysql8/MySQL8Test3.java#testMany2Many | 待做 |  | `SELECT t.'id',t.'name',t.'order' FROM 'm8_parent' t LEFT JOIN (SELECT t2.'parent_id' AS '__group_key…` |
| mysql8/MySQL8Test5.java#testManyLimit1 | 待做 |  | `SELECT t.'id' AS 'value1',t6.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t4.'__gro…` |
| mysql8/MySQL8Test5.java#testManyLimit1_2 | 待做 |  | `SELECT t.'id' AS 'value1',t6.'__joining2__' AS 'value2' FROM 'm8_user' t LEFT JOIN (SELECT t4.'__gro…` |
| mysql8/MySQL8Test5.java#testManyLimit1_3 | 待做 |  | `SELECT t.'id' AS 'value1',t6.'__joining2__' AS 'value2',t6.'__joining3__' AS 'value3' FROM 'm8_user'…` |
| mysql8/MySQL8Test5.java#testManyLimit5 | 待做 |  | `SELECT t.'id' AS 'value1',t5.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t4.'ui…` |
| mysql8/MySQL8Test5.java#testManyLimit6 | 待做 |  | `SELECT t.'id' AS 'value1',t5.'__joining2__' AS 'value2' FROM 't_sys_user' t LEFT JOIN (SELECT t4.'ui…` |

### 隐式子查询改分组 join（`nav.groupJoin`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| NumberFunctionsTest.java#subQueryMySQL | 骨架 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:78 | `SELECT IFNULL(t2.'__any7__',?) AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__grou…` |
| NumberFunctionsTest.java#subQueryORACLE | 骨架 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:118 | `SELECT NVL(t2."__any7__",?) AS "value1" FROM "t_sys_user" t LEFT JOIN (SELECT t1."uid" AS "__group_k…` |
| QueryTest19.java#test1_3 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN (SELECT t2.'…` |
| QueryTest22.java#testDoubleField | 待做 |  | `SELECT IFNULL(t2.'__count3__',0) AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 'my_com_user…` |
| QueryTest22.java#testManyGroup | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup10 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup11 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup12 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup13 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t2.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup14 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup15 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest22.java#testManyGroup16 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest22.java#testManyGroup2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup3 | 待做 |  | `SELECT IFNULL(t2.'__count3__',0) AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2',IFNULL(t2.'__coun…` |
| QueryTest22.java#testManyGroup4 | 待做 |  | `SELECT IFNULL(t2.'__any2__',?) AS 'value1',IFNULL(t2.'__none3__',?) AS 'value2' FROM 'doc_user' t LE…` |
| QueryTest22.java#testManyGroup5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup6 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t` |
| QueryTest22.java#testManyGroup7 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t` |
| QueryTest22.java#testManyGroup8 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroup9 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT COUNT(*) FROM 'doc_bank_car…` |
| QueryTest22.java#testManyGroupJoin | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyGroupJoin2 | 待做 |  | `SELECT t.'id' AS 'value1',IFNULL(t3.'__sum2__',0) AS 'value2',t3.'__min5__' AS 'value3' FROM 'doc_us…` |
| QueryTest22.java#testManyGroupJoin2_1 | 待做 |  | `SELECT t.'id' AS 'value1',IFNULL(t3.'__sum2__',0) AS 'value2',t3.'__min5__' AS 'value3' FROM 'doc_us…` |
| QueryTest22.java#testManyGroupJoin2_2 | 待做 |  | `SELECT t.'id' AS 'value1',IFNULL(t5.'__sum2__',0) AS 'value2',t5.'__min4__' AS 'value3' FROM 'doc_us…` |
| QueryTest22.java#testManyGroupJoin3 | 待做 |  | `SELECT t.'id' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2',IFNULL(t2.'__count4__',0) AS 'value3…` |
| QueryTest22.java#testManyGroupx | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyJoinCount | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testManyJoinNone | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct10 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct12 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct13 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct15 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct6 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct7 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct8 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testOrderCountDistinct9 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest22.java#testSelectAnyElement | 待做 |  | `SELECT t2.'__joining2__' AS 'value1',t2.'__joining3__' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELE…` |
| QueryTest22.java#testSelectAnyElement3 | 待做 |  | `SELECT t2.'__max2__' AS 'value1',t2.'__min3__' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'u…` |
| QueryTest23.java#manyJoinMany2Many | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t LEFT JOIN (SELECT t2.'class_id' AS '__group_key1__',COU…` |
| QueryTest23.java#testElement3 | 待做 |  | `SELECT t2.'__max2__' AS 'value1',t5.'type' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' …` |
| QueryTest23.java#testJoin3 | 待做 |  |  |
| QueryTest23.java#testMany2Many2 | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_user_account' t LEFT JOIN …` |
| QueryTest23.java#testMany2Many2_1 | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_user_account' t LEFT JOIN …` |
| mysql8/M8BankTest.java#test5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#testExtraFilter5 | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count2__',0) AS 'value2' FROM 't_bank' t LEFT JOIN (SELECT …` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountToGroupTest | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',IFNULL(t2.'__count2__',0) AS '__part__column1' FROM 't_bank' …` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountToGroupTest1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',IFNULL(t2.'__count2__',0) AS '__part__column1',IFNULL(t2.'__c…` |
| mysql8/M8BankTest.java#whereSubQuery2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#whereSubQuery2_1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#whereSubQuery2_2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8BankTest.java#whereSubQuery3 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE EXISTS(SELECT 1 F…` |
| mysql8/M8BankTest.java#whereSubQuery4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8OrderTest.java#testSubQuery2 | 待做 |  | `SELECT t.'id',t.'no',t.'price',t.'create_time' FROM 't_order' t INNER JOIN (SELECT t1.'id',t1.'order…` |
| mysql8/M8OrderTest.java#testSubQuery4 | 待做 |  |  |
| mysql8/ManyJoinTest.java#manyJoinMany2Many10 | 待做 |  | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t1…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many2 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t LEFT JOIN (SELECT t2.'class_id' AS '__class_id__',t1.'i…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many3 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many3_1 | 待做 |  | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t2…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many3_2 | 待做 |  | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t2…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many4 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#manyJoinMany2Many4_1 | 待做 |  | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time' FROM 'm8_user2' t LEFT JOIN (SELECT t2…` |
| mysql8/ManyJoinTest.java#testElement3 | 待做 |  | `SELECT t2.'__max2__' AS 'value1',t5.'type' AS 'value2' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' …` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/ManyJoinTest.java#testManyJoinGroup | 待做 |  |  |
| mysql8/MySQL8Test2.java#testPageSelectSubQueryCount | 待做 |  | `SELECT COUNT(*) FROM 't_bank' t` |

### include 带出导航（`nav.include`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DirectRelationTest.java#test | 待做 |  | `SELECT 'c1','c2','c3','c4','c5' FROM 'direct1'` |
| DirectRelationTest.java#test2 | 待做 |  | `SELECT 'c1','c2','c3','c4','c5' FROM 'direct1'` |
| QueryTest18.java#esss | 待做 |  |  |
| QueryTest18.java#test14 | 待做 |  | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'id',t.'stars',t1.'create_time' FROM 't_topic' t LEFT JOIN '…` |
| QueryTest2.java#query26 | 待做 |  |  |
| QueryTest28.java#testInterceptor3 | 待做 |  | `SELECT 'id','name' FROM 'm8_interceptor'` |
| RelationNullValueTest.java#one2oneTest | 待做 |  |  |
| RelationTest.java#provinceTest | 待做 |  |  |
| RelationTest.java#provinceTest1 | 待做 |  |  |
| RelationTest.java#provinceTest5 | 待做 |  |  |
| RelationTest.java#provinceTest6 | 待做 |  |  |
| RelationTest.java#provinceTest61 | 待做 |  |  |
| RelationTest.java#provinceTest7 | 待做 |  |  |
| mysql8/M8CommentTest.java#testPartitionBy | 待做 |  | `SELECT 'id','parent_id','content','username','time' FROM 'comment'` |
| mysql8/M8IncludeLimitTest.java#testIncludeLimit | 待做 |  |  |
| mysql8/M8IncludeLimitTest.java#testIncludeLimitPagingPartition | 待做 |  |  |
| mysql8/M8IncludeLimitTest.java#testIncludeLimitPagingUnionAll | 待做 |  |  |
| mysql8/M8IncludeOrderTest.java#testOrde2r | 待做 |  |  |
| mysql8/M8IncludeOrderTest.java#testOrder | 待做 |  |  |
| mysql8/M8Test1.java#testRelationColumns1 | 待做 |  | `SELECT 'user_id','user_name','user_age','create_time','book_ids' FROM 'm8_user_book_ids'` |
| mysql8/M8Test1.java#testSelfTargetTypeDiff | 待做 |  |  |
| mysql8/MySQL8Test3.java#testQueryFirstLimit1 | 待做 |  | `SELECT t2.'id' AS 'id',t2.'uid' AS 'uid',t2.'code' AS 'code',t2.'type' AS 'type',t2.'bank_id' AS 'ba…` |
| mysql8/MySQL8Test4.java#includeOneTest1 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testAll | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE NOT (EXISTS(SELEC…` |
| mysql8/MySQL8Test4.java#testAll1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE NOT (EXISTS(SELEC…` |
| mysql8/MySQL8Test4.java#testAll2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE NOT (EXISTS(SELEC…` |
| mysql8/MySQL8Test4.java#testAllGroupJoin | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test4.java#testAllGroupJoin2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test4.java#testIncludeIdOnly1 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testIncludeIdOnly2 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test5.java#selectJoinInclude | 待做 |  |  |
| pgsql/QueryTest19.java#test | 待做 |  |  |
| pgsql/QueryTest19.java#testAll | 待做 |  | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t WHERE NOT (EXISTS(SELECT 1 FROM "doc_bank…` |
| pgsql/QueryTest19.java#testAll1 | 待做 |  | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t WHERE NOT (EXISTS(SELECT 1 FROM "doc_bank…` |
| pgsql/QueryTest19.java#testAll2 | 待做 |  | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t WHERE NOT (EXISTS(SELECT 1 FROM "doc_bank…` |
| pgsql/QueryTest19.java#testAllGroupJoin | 待做 |  | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t LEFT JOIN (SELECT t1."uid" AS "__group_ke…` |
| pgsql/QueryTest19.java#testAllGroupJoin2 | 待做 |  | `SELECT t."id",t."name",t."phone",t."age" FROM "doc_user" t LEFT JOIN (SELECT t1."uid" AS "__group_ke…` |

### 隐式子查询（any / none / count / sum …）（`nav.subquery`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#countFilterMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:11 | `SELECT COUNT(t.'id') AS 'value1',COUNT(DISTINCT t.'title') AS 'value2',COUNT((CASE WHEN t.'title' <>…` |
| GeneralFunctionsTest.java#countMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:19 | `SELECT COUNT(t.[id]) AS [value1],COUNT(DISTINCT t.[id]) AS [value2] FROM [doc_bank_card] t` |
| GeneralFunctionsTest.java#countMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:19 | `SELECT COUNT(t.'id') AS 'value1',COUNT(DISTINCT t.'id') AS 'value2' FROM 'doc_bank_card' t` |
| GeneralFunctionsTest.java#countORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:19 | `SELECT COUNT(t."id") AS "value1",COUNT(DISTINCT t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#countPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:19 | `SELECT COUNT(t."id") AS "value1",COUNT(DISTINCT t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#maxMinMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:41 | `SELECT MAX(t.[id]) AS [value1],MIN(t.[id]) AS [value2] FROM [doc_bank_card] t` |
| GeneralFunctionsTest.java#maxMinMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:41 | `SELECT MAX(t.'id') AS 'value1',MIN(t.'id') AS 'value2' FROM 'doc_bank_card' t` |
| GeneralFunctionsTest.java#maxMinORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:41 | `SELECT MAX(t."id") AS "value1",MIN(t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#maxMinPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:41 | `SELECT MAX(t."id") AS "value1",MIN(t."id") AS "value2" FROM "doc_bank_card" t` |
| GeneralFunctionsTest.java#minMaxFilterMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:52 | `SELECT MAX(t.'score') AS 'value1',MAX((CASE WHEN t.'title' <> ? THEN t.'score' ELSE NULL END)) AS 'v…` |
| NumberFunctionsTest.java#avgMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:55 | `SELECT AVG(t.[score]) AS [value1],AVG(DISTINCT t.[score]) AS [value2],AVG(DISTINCT (CASE WHEN t.[tit…` |
| NumberFunctionsTest.java#avgMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:55 | `SELECT AVG(t.'score') AS 'value1',AVG(DISTINCT t.'score') AS 'value2',AVG(DISTINCT (CASE WHEN t.'tit…` |
| NumberFunctionsTest.java#avgORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:55 | `SELECT AVG(t."score") AS "value1",AVG(DISTINCT t."score") AS "value2",AVG(DISTINCT (CASE WHEN t."tit…` |
| NumberFunctionsTest.java#avgPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:55 | `SELECT AVG(t."score") AS "value1",AVG(DISTINCT t."score") AS "value2",AVG(DISTINCT (CASE WHEN t."tit…` |
| NumberFunctionsTest.java#sumMSSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:66 | `SELECT SUM(t.[score]) AS [value1],SUM(DISTINCT t.[score]) AS [value2],SUM(DISTINCT t.[score]) AS [va…` |
| NumberFunctionsTest.java#sumMySQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:66 | `SELECT SUM(t.'score') AS 'value1',SUM(DISTINCT t.'score') AS 'value2',SUM(DISTINCT t.'score') AS 'va…` |
| NumberFunctionsTest.java#sumORACLE | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:66 | `SELECT SUM(t."score") AS "value1",SUM(DISTINCT t."score") AS "value2",SUM(DISTINCT t."score") AS "va…` |
| NumberFunctionsTest.java#sumPGSQL | 已移植 | tests/EasyQuerySharp.Tests.Upstream/NumberFunctionsTest.cs:66 | `SELECT SUM(t."score") AS "value1",SUM(DISTINCT t."score") AS "value2",SUM(DISTINCT t."score") AS "va…` |
| QueryTest.java#query61 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:8 |  |
| QueryTest.java#query63 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:20 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT COUNT(t1.'id') FROM 't_blog' t1 WHERE t1.'…` |
| QueryTest.java#query63_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:35 | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT SUM(t1.'star') FROM 't_blog' t1 WHERE t1.'…` |
| QueryTest.java#query64 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:54 | `SELECT t.'id',t.'stars',t.'title',(SELECT COUNT(t1.'id') FROM 't_blog' t1 WHERE t1.'deleted' = ? AND…` |
| QueryTest11.java#testx5 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time',(SELECT SUM(t1.'star') FROM 't_blog' t1 WHERE t1.'…` |
| QueryTest12.java#testAny1 | 待做 |  | `SELECT 1 FROM 't_topic' WHERE 'title' = ? ORDER BY CASE WHEN 'id' IS NULL THEN 1 ELSE 0 END ASC,'id'…` |
| QueryTest12.java#testDoc1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 WHERE t1…` |
| QueryTest12.java#testDoc10 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest12.java#testDoc16 | 待做 |  | `SELECT t.'id',t.'name' FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_book' t1 WHERE (t…` |
| QueryTest12.java#testDoc17 | 待做 |  | `SELECT t.'id',t.'name' FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE…` |
| QueryTest12.java#testDoc2 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE NOT (EXISTS(SELECT 1 FROM 'school_student' t1 WHE…` |
| QueryTest12.java#testDoc22 | 待做 |  | `SELECT t.'id',t.'name' FROM 'relation_user' t WHERE EXISTS(SELECT 1 FROM 'relation_teacher' t1 WHERE…` |
| QueryTest12.java#testDoc3 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| QueryTest12.java#testDoc4 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE (SELECT COUNT(*) FROM 'school_student' t1 WHERE t…` |
| QueryTest12.java#testDoc5 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_teacher' t1 WHERE EX…` |
| QueryTest12.java#testDoc6 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_teacher' t1 WHERE EX…` |
| QueryTest12.java#testDoc7 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc8 | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest12.java#testDoc9 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest12.java#testNum2 | 待做 |  | `SELECT (SUM(t.'score') - SUM(t.'score')) AS 'score' FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest12.java#testNum3 | 待做 |  | `SELECT (SUM(t.'score') - SUM(t.'score')) FROM 't_blog' t WHERE t.'deleted' = ?` |
| QueryTest16.java#docSub | 待做 |  | `SELECT t.'id' AS 'value1',(NOT (EXISTS(SELECT 1 FROM 't_user' t1 WHERE t1.'company_id' = t.'id' LIMI…` |
| QueryTest16.java#test19 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE EXISTS(SELECT 1 …` |
| QueryTest16.java#test20 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t LEFT JOIN 't_company' …` |
| QueryTest16.java#testSubSum | 待做 |  | `SELECT t1.'id' AS 'value1',IFNULL((SELECT SUM(t2.'age') FROM 't_user' t2 WHERE t2.'company_id' = t1.…` |
| QueryTest16.java#testSubSumOracle | 待做 |  | `SELECT t1."id" AS "value1",NVL((SELECT SUM(t2."age") FROM "t_user" t2 WHERE t2."company_id" = t1."id…` |
| QueryTest17.java#testFlatElement5 | 待做 |  | `SELECT t.'id',t.'f1',t.'f2',t.'parent_id' FROM 'MyObject' t LEFT JOIN 'MyObjectParent' t1 ON t1.'id'…` |
| QueryTest18.java#testCount | 待做 |  |  |
| QueryTest18.java#testSubCountLimitSelect1 | 待做 |  | `SELECT t1.'id' AS 'value1',t1.'phone' AS 'value2',(SELECT COUNT(*) FROM 't_blog' t2 WHERE t2.'delete…` |
| QueryTest18.java#testSubCountPage1 | 待做 |  | `SELECT COUNT(*) FROM 'easy-query-test'.'t_sys_user' WHERE 'phone' <> ?` |
| QueryTest18.java#textDistinct1 | 待做 |  | `SELECT DISTINCT COUNT(*) FROM 't_topic'` |
| QueryTest19.java#test122 | 待做 |  |  |
| QueryTest19.java#test1_1 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE t.'name' = ? AND…` |
| QueryTest19.java#test1_2 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE t.'name' = ? AND…` |
| QueryTest21.java#subNumberRangeClosed | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE ((SELECT COUNT(*) FROM 'doc_bank_ca…` |
| QueryTest21.java#subNumberRangeClosed1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT COUNT(*) FROM 'doc_bank_car…` |
| QueryTest21.java#testDistinctCount | 待做 |  | `SELECT COUNT(DISTINCT t.'id') FROM 't_blog' t WHERE t.'deleted' = ? AND t.'title' LIKE ?` |
| QueryTest21.java#testDistinctCount1 | 待做 |  | `SELECT COUNT(DISTINCT t.'id') FROM 't_blog' t WHERE t.'deleted' = ? AND t.'title' LIKE ?` |
| QueryTest22.java#testManyGroup29 | 待做 |  |  |
| QueryTest22.java#testOrderCountDistinct1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t ORDER BY IFNULL((SELECT SUM((SELECT COUNT…` |
| QueryTest22.java#testOrderCountDistinct11 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT MAX(t1.'type') FROM 'doc_ba…` |
| QueryTest22.java#testOrderCountDistinct14 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t WHERE (SELECT MIN(t1.'type') FROM 'doc_ba…` |
| QueryTest22.java#testOrderCountDistinct3 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t ORDER BY (SELECT COUNT(DISTINCT t2.'id') …` |
| QueryTest22.java#testOrderCountDistinct5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t ORDER BY IFNULL((SELECT SUM(DISTINCT (SEL…` |
| QueryTest22.java#testOrderCountDistinct6_1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age' FROM 'doc_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_ke…` |
| QueryTest23.java#anyTest | 待做 |  | `SELECT (SELECT MAX(t2.'type') FROM 'doc_bank_card' t2 WHERE t2.'uid' = t.'id' AND t.'name' = t2.'cod…` |
| QueryTest23.java#filterOn3 | 待做 |  | `SELECT COUNT(*) FROM 'doc_bank_card' t INNER JOIN 'doc_bank' t1 ON t1.'id' = t.'bank_id' AND t1.'nam…` |
| QueryTest23.java#testJoin2 | 待做 |  |  |
| QueryTest23.java#testMany2Many | 待做 |  | `SELECT t.'name' AS 'value1',(SELECT COUNT(*) FROM 't_user_book' t2 WHERE t2.'uid' = t.'uid') AS 'val…` |
| QueryTest24.java#testSubCount | 待做 |  | `SELECT t.'id',t.'create_time',t.'title',IFNULL(t2.'__count2__',0) AS 'stars' FROM 't_blog' t LEFT JO…` |
| QueryTest24.java#testx14 | 待做 |  | `SELECT MAX(t.'topic_type') FROM 't_topic_type' t LIMIT 1` |
| QueryTest24.java#testx15 | 待做 |  | `SELECT MIN(t.'topic_type') FROM 't_topic_type' t LIMIT 1` |
| QueryTest27.java#autoMergeMany2Many1 | 待做 |  | `SELECT t.'station_id',t.'station_name' FROM 't_station' t LEFT JOIN (SELECT t2.'station_id' AS '__gr…` |
| QueryTest27.java#autoMergeMany2Many2 | 待做 |  | `SELECT t.'station_id',t.'station_name' FROM 't_station' t LEFT JOIN (SELECT t2.'station_id' AS '__gr…` |
| QueryTest27.java#testCountSubQueryCount | 待做 |  | `SELECT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update_by',t.'deleted',t.'title',t.'c…` |
| QueryTest28.java#testInterceptor2 | 待做 |  | `SELECT t.'id',t.'name' FROM 'm8_interceptor' t WHERE EXISTS(SELECT 1 FROM 'm8_interceptor2' t1 WHERE…` |
| QueryTest3.java#nativePage2 | 待做 |  | `select * from t_topicxx` |
| QueryTest4.java#query1233 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t WHERE t.'create_time' = (SELECT M…` |
| QueryTest5.java#joinTest1 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest2 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest3 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest4 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest5 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest6 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest5.java#joinTest7 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| Relation2Test.java#test10 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE (SELECT MAX(t1.'name') FROM 'my_school_student…` |
| Relation2Test.java#test10_1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE (SELECT MAX(t1.'name') FROM 'my_school_student…` |
| Relation2Test.java#test5 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE (SELECT COUNT(*) FROM 'my_school_student' t1 W…` |
| Relation2Test.java#test6 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE EXISTS(SELECT 1 FROM 'my_school_student' t1 WH…` |
| Relation2Test.java#test8 | 待做 |  | `SELECT t.'id',t.'name' FROM 'my_school_class' t WHERE IFNULL((SELECT AVG(CAST(t1.'name' AS DECIMAL(3…` |
| RelationTest.java#schoolTest1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 WHERE t1…` |
| RelationTest.java#schoolTest3 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| RelationTest.java#schoolTest4 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| RelationTest.java#schoolTest5 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 LEFT JOI…` |
| RelationTest.java#schoolTest6 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_teacher' t1 WHERE EX…` |
| RelationTest.java#schoolTest7 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE (SELECT COUNT(*) FROM 'school_student' t1 WHERE t…` |
| RelationTest.java#schoolTest8 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE NOT (EXISTS(SELECT 1 FROM 'school_student' t1 WHE…` |
| RelationTest.java#schoolTest8_1 | 待做 |  | `SELECT t.'id',t.'name' FROM 'school_class' t WHERE EXISTS(SELECT 1 FROM 'school_student' t1 WHERE t1…` |
| SubJoinTest.java#testSubJoinAvg | 待做 |  | `SELECT IFNULL((SELECT AVG(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinAvg2 | 待做 |  | `SELECT IFNULL((SELECT AVG(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinAvgDistinct | 待做 |  | `SELECT IFNULL((SELECT AVG(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinAvgDistinct2 | 待做 |  | `SELECT IFNULL((SELECT AVG(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinMax | 待做 |  | `SELECT (SELECT MAX(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinMax2 | 待做 |  | `SELECT (SELECT MAX(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinMin | 待做 |  | `SELECT (SELECT MIN(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinMin2 | 待做 |  | `SELECT (SELECT MIN(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id' = t1.…` |
| SubJoinTest.java#testSubJoinSum | 待做 |  | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSum2 | 待做 |  | `SELECT IFNULL((SELECT SUM(t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 ON t2.'id…` |
| SubJoinTest.java#testSubJoinSumDistinct | 待做 |  | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| SubJoinTest.java#testSubJoinSumDistinct2 | 待做 |  | `SELECT IFNULL((SELECT SUM(DISTINCT t2.'age') FROM 'sub_join_book' t1 LEFT JOIN 'sub_join_author' t2 …` |
| mysql8/M8BankTest.java#fromSubQuery2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:138 | `SELECT t2.'vo1' AS 'vo1',t2.'vo2' AS 'vo2',t2.'vo3' AS 'vo3',t2.'bank_card_count' AS 'bank_card_coun…` |
| mysql8/M8BankTest.java#testQueryBankAndCardExplicitCountTest | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(t1.'id') FROM 't_bank_card' t1 WHERE t1.'bank_i…` |
| mysql8/M8BankTest.java#testQueryBankAndCardExplicitCountTest1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(t1.'id') FROM 't_bank_card' t1 WHERE t1.'bank_i…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(*) FROM 't_bank_card' t1 WHERE t1.'bank_id' = t…` |
| mysql8/M8BankTest.java#testQueryBankAndCardImplicitCountTest1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time',(SELECT COUNT(*) FROM 't_bank_card' t1 WHERE t1.'bank_id' = t…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time',IFNULL(t2.'__count2__',0) AS '__part__colum…` |
| mysql8/M8BankTest.java#testUserAutoSubQueryToGroupJoin1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time',t2.'__count2__' AS '__part__column1' FROM '…` |
| mysql8/M8BankTest.java#whereSubQuery1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE EXISTS(SELECT 1 F…` |
| mysql8/M8BankTest.java#whereSubQuery5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8QueryTest.java#whereObject5 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8Test1.java#testMany | 待做 |  |  |
| mysql8/M8Test1.java#testRelationColumns | 待做 |  | `SELECT t.'user_id',t.'user_name',t.'user_age',t.'create_time',t.'book_ids' FROM 'm8_user_book_ids' t…` |
| mysql8/ManyJoinTest.java#manyGroupTest1 | 待做 |  |  |
| mysql8/ManyJoinTest.java#test10GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test11GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test12GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test13GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count4__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test14GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',COUNT…` |
| mysql8/ManyJoinTest.java#test15GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',MAX(t…` |
| mysql8/ManyJoinTest.java#test16GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'uid' AS '__group_key1__',MAX((…` |
| mysql8/ManyJoinTest.java#test3GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test4GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test5GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test6GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t3.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test7GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test8GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#test9GroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#testGroupJoinCombine | 待做 |  | `SELECT t.'name' AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 't_sys_user' t LEFT JOIN (SEL…` |
| mysql8/ManyJoinTest.java#testMany2Many2GroupJoin3 | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'create_time' FROM 'm8_user' t LEFT JOIN (SELECT t2.'user_id' AS '_…` |
| mysql8/MySQL8Test2.java#testAA | 待做 |  |  |
| mysql8/MySQL8Test3.java#testDTO1 | 待做 |  |  |
| mysql8/MySQL8Test4.java#testPartition2 | 待做 |  | `SELECT t.'id' AS 'id',t.'name' AS 'name',t5.'type' AS 'third_card_type',t5.'code' AS 'third_card_cod…` |
| mysql8/MySQL8Test4.java#testPartition3 | 待做 |  | `SELECT t.'id' AS 'id',t.'name' AS 'name',t5.'type' AS 'third_card_type',t5.'code' AS 'third_card_cod…` |
| mysql8/MySQL8Test4.java#testSubQueryCountCompareNull1 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |

### 树（toTreeList / asTreeCTE）（`tree`，M4）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest11.java#testCTE1 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE1 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE1_1 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE2 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE3 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE4 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE5 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE6 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'stars',t1.'title',t1.'create_t…` |
| QueryTest7.java#testCTE7 | 待做 |  | `WITH RECURSIVE 'abc' AS ( (SELECT 0 AS 'xyz',t1.'id',t1.'stars',t1.'title',t1.'create_time' FROM 't_…` |
| mysql8/MySQL8Test2.java#cteViewTree1 | 待做 |  |  |
| mysql8/MySQL8Test2.java#cteViewTree2 | 待做 |  |  |
| mysql8/MySQL8Test3.java#testTree | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'parent_id',t1.'content',t1.'us…` |
| mysql8/MySQL8Test3.java#testTree_1 | 待做 |  | `WITH RECURSIVE 'as_tree_cte' AS ( (SELECT 0 AS 'cte_deep',t1.'id',t1.'parent_id',t1.'content',t1.'us…` |
| pgsql/QueryTest19.java#cteViewTree1 | 待做 |  |  |
| pgsql/QueryTest19.java#testToTreeList | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#testToTreeList2 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#testToTreeList3 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#testToTreeListDTO | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree10 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree10_1 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree11 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree12 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t2."id",t2."parent_id",t2."name" FROM "cat…` |
| pgsql/QueryTest19.java#tree7_1 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep",t1."id",t1."parent_id",t1."name" FROM "categor…` |
| pgsql/QueryTest19.java#tree7_2 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "deep1",t1."id",t1."parent_id",t1."name" FROM "catego…` |
| pgsql/QueryTest19.java#tree9 | 待做 |  | `WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0 AS "cte_deep",t1."id",t1."parent_id",t1."name" FROM "cat…` |

### 值转换、枚举、JSON 列（`column.converter`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/json/JsonValueConverterTest.java#columnIsBoundToTheConverter | 待做 |  |  |
| fork/json/JsonValueConverterTest.java#decodesWithTheDeclaredGenericType | 待做 |  |  |
| fork/json/JsonValueConverterTest.java#nullAndBlankStayNull | 待做 |  |  |
| fork/live/JsonValueConverterLiveTest.java#hanaNclobColumn | 待做 |  | `DROP TABLE "t_fork_profile"` |
| fork/live/JsonValueConverterLiveTest.java#mysql8018JsonColumn | 待做 |  | `DROP TABLE IF EXISTS t_fork_profile` |
| fork/types/ZonedTimeColumnSqlTest.java#compareAndDateArithmetic | 待做 |  | `SELECT 'id','kind','created_at','completed_at','due_at','seen_at' FROM 't_fork_event' WHERE 'created…` |
| fork/types/ZonedTimeColumnSqlTest.java#durationAggregateInGroupSelect | 待做 |  | `SELECT t.'kind' AS 'value1',AVG(` |
| fork/types/ZonedTimeColumnSqlTest.java#proxyColumnsAreDateTimeColumns | 待做 |  |  |
| fork/types/ZonedTimeColumnSqlTest.java#selectSingleZonedColumn | 待做 |  | `SELECT t.'created_at' FROM 't_fork_event' t ORDER BY t.'created_at' ASC` |

### 列加密（`column.encryption`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EncryptionTest.java#phoneTest | 待做 |  |  |
| EncryptionTest.java#phoneTest1 | 待做 |  |  |
| EncryptionTest.java#phoneTest2 | 待做 |  |  |
| EncryptionTest.java#phoneTest3 | 待做 |  |  |
| EncryptionTest.java#phoneTest4 | 待做 |  |  |
| EncryptionTest.java#test5 | 待做 |  |  |
| EncryptionTest.java#test6 | 待做 |  |  |
| Sm4EncryptionTest.java#sm4Test | 待做 |  |  |

### 拦截器（`interceptor`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| InterceptorTest.java#test1 | 待做 |  | `SELECT 'id','stars','title','create_time','create_by','update_time','update_by','tenant_id' FROM 't_…` |
| InterceptorTest.java#test2 | 待做 |  | `SELECT 'id','stars','title','create_time','create_by','update_time','update_by','tenant_id' FROM 't_…` |
| InterceptorTest.java#test3 | 待做 |  | `SELECT 'id','stars','title','create_time','create_by','update_time','update_by','tenant_id' FROM 't_…` |

### 逻辑删除（`logicDelete`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| LogicDeleteTest.java#Test1 | 待做 |  | `SELECT 'id','stars','title','deleted','create_time' FROM 't_logic_del_topic' WHERE 'deleted' = ?` |
| LogicDeleteTest.java#Test10 | 待做 |  | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |
| LogicDeleteTest.java#Test2 | 待做 |  | `UPDATE 't_logic_del_topic' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| LogicDeleteTest.java#Test3 | 待做 |  | `UPDATE 't_logic_del_topic' SET 'deleted' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| LogicDeleteTest.java#Test4 | 待做 |  | `SELECT 'id','stars','title','deleted','create_time' FROM 't_logic_del_topic'` |
| LogicDeleteTest.java#Test5 | 待做 |  | `DELETE FROM 't_logic_del_topic' WHERE 'id' = ?` |
| LogicDeleteTest.java#Test6 | 待做 |  | `UPDATE 't_logic_del_topic' SET 'title' = ? WHERE 'id' = ?` |
| LogicDeleteTest.java#Test7 | 待做 |  | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |
| LogicDeleteTest.java#Test8 | 待做 |  | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |
| LogicDeleteTest.java#Test9 | 待做 |  | `SELECT 'id','stars','title','deleted_at','deleted_user','create_time' FROM 't_logic_del_topic_custom…` |

### 聚合根保存（savable）（`save.aggregate`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mysql8/M8Save2Test.java#testOne2Many1 | 待做 |  |  |
| mysql8/M8Save2Test.java#testOne2Many2 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' IN (?,?)` |
| mysql8/M8Save2Test.java#testOne2Many3 | 待做 |  |  |
| mysql8/M8Save2Test.java#testOne2Many4 | 待做 |  | `DELETE FROM 'm8_save_root_many' WHERE 'id' = ?` |
| mysql8/M8Save2Test.java#testOne2Many5 | 待做 |  |  |
| mysql8/M8Save2Test.java#testOne2Many6 | 待做 |  |  |
| mysql8/M8Save2Test.java#testOne2OneChange | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root'` |
| mysql8/M8Save2Test.java#testOne2OneChange2 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root'` |
| mysql8/M8Save2Test.java#testOne2OneChange3 | 待做 |  |  |
| mysql8/M8Save3Test.java#many2manySetNullInsert | 待做 |  | `INSERT INTO 'm8_role3' ('role_id','role_name') VALUES (?,?)` |
| mysql8/M8Save3Test.java#testOwnershipChange1 | 待做 |  | `UPDATE 'm8_save_b' SET 'pid' = ? WHERE 'id' = ?` |
| mysql8/M8Save3Test.java#testOwnershipChange1_1 | 待做 |  | `UPDATE 'm8_save_b' SET 'pid' = ? WHERE 'id' = ?` |
| mysql8/M8Save3Test.java#testOwnershipChange2 | 待做 |  | `DELETE FROM 'm8_save_d' WHERE 'id' = ?` |
| mysql8/M8Save3Test.java#testOwnershipChange3 | 待做 |  | `DELETE FROM 'm8_save_d' WHERE 'id' = ?` |
| mysql8/M8Save4Test.java#testToManyToMany1 | 待做 |  |  |
| mysql8/M8Save4Test.java#testToManyToMany2 | 待做 |  | `SELECT 'a','b','c' FROM 'm8_many1' WHERE 'a' = ?` |
| mysql8/M8SaveTest.java#test2One1 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#test2One2 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#test2One3 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#test2One4 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testDeleteAll | 待做 |  | `DELETE FROM 'm8_save_root_many_one' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many1 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many2 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many3 | 待做 |  |  |
| mysql8/M8SaveTest.java#testMany2Many4 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many5 | 待做 |  |  |
| mysql8/M8SaveTest.java#testMany2Many6 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2Many7 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2ManyMiddle1 | 待做 |  | `DELETE FROM 'm8_save_root_middle_many' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testMany2ManyMiddle2 | 待做 |  |  |
| mysql8/M8SaveTest.java#testMany2ManyMiddle3 | 待做 |  |  |
| mysql8/M8SaveTest.java#testOne2Many1 | 待做 |  |  |
| mysql8/M8SaveTest.java#testOne2Many2 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many2_1 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many3 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many4 | 待做 |  | `SELECT 'id','name','code' FROM 'm8_save_root' WHERE 'id' = ?` |
| mysql8/M8SaveTest.java#testOne2Many5 | 待做 |  |  |
| mysql8/M8SaveTest.java#testOne2Many6 | 待做 |  |  |
| mysql8/M8SaveTest.java#testPathAggregateRoot | 待做 |  |  |

### 追踪与差异更新（`tracking`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| EntityQueryAggregateTest1.java#test10 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t` |
| EntityQueryAggregateTest6.java#test1 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test2 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test3 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test4 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test5 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test6 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test7 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test8 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| EntityQueryAggregateTest6.java#test9 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id'` |
| MyTest1.java#testSelect6_3 | 待做 |  | `SELECT t.'id' AS 'id',COUNT(t1.'id') AS 'id_count' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'del…` |
| MyTest2.java#testSelect6_3 | 待做 |  | `SELECT t.'id' AS 'id',COUNT(t1.'id') AS 'id_count' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'del…` |
| QueryTest.java#query14 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:124 |  |
| QueryTest27.java#testDiff | 待做 |  |  |
| QueryTest27.java#testDiff2 | 待做 |  |  |
| QueryTest28.java#testQueryAggregate | 待做 |  | `SELECT COUNT(*) AS 'value1',SUM((CASE WHEN t.'title' LIKE CONCAT('%',?,'%') THEN t.'stars' ELSE ? EN…` |
| QueryTest28.java#testQueryAggregate1 | 待做 |  | `SELECT COUNT(*) AS 'value1',SUM((CASE WHEN t.'title' LIKE CONCAT('%',?,'%') THEN t.'stars' ELSE ? EN…` |
| QueryTest6.java#joinTest1 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest2 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest3 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest4 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest5 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest6 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest6.java#joinTest7 | 待做 |  | `SELECT DISTINCT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' =…` |
| QueryTest7.java#joinTest1 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest2 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest3 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest4 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest5 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest6 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| QueryTest7.java#joinTest7 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' …` |
| RelationTest.java#testOneToOne | 待做 |  | `SELECT 'id','name' FROM 'school_class'` |
| TrackTest.java#track1 | 待做 |  |  |
| TrackTest.java#track2 | 待做 |  |  |
| TrackTest.java#track3 | 待做 |  |  |
| TrackTest.java#track4 | 待做 |  |  |
| TrackTest.java#track5 | 待做 |  |  |
| UpdateTest.java#updateTest17 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:146 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'title' IS NULL` |
| UpdateTest.java#updateTest17_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:146 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'title' IS NULL` |
| UpdateTest.java#updateTest17_2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:159：C# 的 Topic.Alias 标了 ColumnIgnore（Java 映射了 alias 列），条件里少一个 'alias' IS NULL | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'stars' IS NULL AND 'create_time' IS NULL AND 'a…` |
| UpdateTest.java#updateTest20 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:183 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest21 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:197 | `UPDATE 'x1234' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest21_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:197 | `UPDATE 'x1234' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTest22 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:197 | `UPDATE 'x123' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest22_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:198 | `UPDATE 'x123' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest23 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:198 | `UPDATE 'x123' SET 'title' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest23_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:198 | `UPDATE 'x123' SET 'title' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:221 | `UPDATE 'x123' SET 'stars' = ?,'create_time' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest24_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:221 | `UPDATE 'x123' SET 'stars' = ?,'create_time' = ? WHERE 'title' = ?` |
| UpdateTest.java#updateTest28 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:231 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest28_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:231 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest30 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:249 | `UPDATE 'x1234' SET 'content' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| UpdateTest.java#updateTest31 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:249 | `UPDATE 'x1234' SET 'content' = ? WHERE 'deleted' = ? AND 'id' = ?` |
| UpdateTest.java#updateTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:66 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:66 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:66 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTrackParameterTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:355 | `UPDATE 'aaaxxx' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTrackParameterTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:373 | `UPDATE 'aaaxxx' SET 'title' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest.java#updateTrackParameterTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:373 | `UPDATE 'aaaxxx' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ? AND 'title' = ?` |
| UpdateTest1.java#updateTest51 | 待做 |  |  |
| UpdateTest1.java#updateTestUpdate2 | 待做 |  | `UPDATE 't_topic' SET 'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| mysql8/ManyJoinTest.java#testManyJoinGroup1 | 待做 |  |  |

### 乐观锁版本（`version`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| VersionTest.java#test1 | 待做 |  |  |
| VersionTest.java#test10 | 待做 |  | `UPDATE 'AXX' SET 'username' = ?,'version' = ? WHERE 'deleted' = ? AND 'version' = ? AND 'id' = ?` |
| VersionTest.java#test2 | 待做 |  |  |
| VersionTest.java#test3 | 待做 |  | `UPDATE 't_sys_user_version' SET 'phone' = ? WHERE 'id' = ?` |
| VersionTest.java#test3_1 | 待做 |  | `UPDATE 't_sys_user_version' SET 'phone' = ?,'version' = ? WHERE 'version' = ? AND 'id' = ?` |
| VersionTest.java#test3_2 | 待做 |  | `UPDATE 't_sys_user_version' SET 'phone' = ? WHERE 'id' = ?` |
| VersionTest.java#test3_3 | 待做 |  |  |
| VersionTest.java#test4 | 待做 |  |  |
| VersionTest.java#test5 | 待做 |  |  |
| VersionTest.java#test6 | 待做 |  | `UPDATE 't_sys_user_version_del' SET 'deleted' = ?,'version' = ? WHERE 'deleted' = ? AND 'version' = …` |
| VersionTest.java#test7 | 待做 |  | `UPDATE 'aaa' SET 'username' = ?,'phone' = ?,'id_card' = ?,'address' = ?,'create_time' = ? WHERE 'del…` |
| VersionTest.java#test8 | 待做 |  |  |
| VersionTest.java#test9 | 待做 |  | `UPDATE 'AXX' SET 'username' = ?,'version' = ? WHERE 'deleted' = ? AND 'version' = ? AND 'id' = ?` |

### UPDATE / DELETE 里用导航（`write.join`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| UpdateDeleteJoinTest.java#testDeleteJoin1 | 待做 |  | `DELETE t FROM 'doc_bank_card' t INNER JOIN 'doc_user' t1 ON t1.'id' = t.'uid' WHERE t1.'name' LIKE ?` |
| UpdateDeleteJoinTest.java#testDeleteJoin2 | 待做 |  | `DELETE t FROM 'doc_bank_card' t INNER JOIN 'doc_user' t1 ON t1.'id' = t.'uid' WHERE t1.'name' LIKE ?` |
| UpdateDeleteJoinTest.java#testUpdateJoin1 | 待做 |  | `UPDATE 'doc_bank_card' t INNER JOIN 'doc_user' t1 ON t1.'id' = t.'uid' SET t.'type' = t1.'name' WHER…` |
| UpdateDeleteJoinTest.java#testUpdateJoin2 | 待做 |  | `UPDATE 'doc_bank_card' t INNER JOIN 'doc_bank' t1 ON t1.'id' = t.'bank_id' INNER JOIN 'doc_user' t2 …` |
| pgsql/DeleteUpdateJoinTest.java#testDeleteJoin1 | 待做 |  | `DELETE FROM "doc_bank_card" t USING "doc_user" t1 WHERE t1."id" = t."uid" AND t1."name" LIKE ?` |
| pgsql/DeleteUpdateJoinTest.java#testDeleteJoin2 | 待做 |  | `DELETE FROM "doc_bank_card" t USING "doc_user" t1 WHERE t1."id" = t."uid" AND t1."name" LIKE ?` |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin1 | 待做 |  | `UPDATE "doc_bank_card" t SET "type" = t1."name" FROM "doc_user" t1 WHERE t1."id" = t."uid" AND t1."n…` |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin2 | 待做 |  | `UPDATE "doc_bank_card" t SET "type" = t1."name" FROM "doc_bank" t1 , "doc_user" t2 WHERE t1."id" = t…` |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin3 | 待做 |  |  |
| pgsql/DeleteUpdateJoinTest.java#testUpdateJoin4 | 待做 |  |  |

### 写哪些列、批量执行（`write.strategy`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#testBatchDelete | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:111：批量：Java 一条语句两组参数，C# 每个实体一条语句 | `DELETE FROM 't_topic' WHERE 'id' = ?` |
| InsertTest.java#insertBatch | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:102：Java 的 batch() 返回 JDBC 的 SUCCESS_NO_INFO（-3）；C# 每个实体一条语句，返回真实行数 |  |
| InsertTest.java#mapInsertTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:135 | `INSERT INTO 'aaaaa' ('id','name') VALUES (?,?)` |
| InsertTest.java#mapInsertTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:143 | `INSERT INTO 'aaaaa' ('id','name','name1') VALUES (?,?,?)` |
| InsertTest.java#mapInsertTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:148 | `INSERT INTO 'aaaaa' ('id','name','name1','name2') VALUES (?,?,?,?)` |
| InsertTest.java#testIncrement | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:154：批量规模缩小（Java 共 1.8 万行，验证的是自增键逐行回填）；C# 每行一条语句，批量写入见 M5.7 | `truncate table t_test_inc;` |
| InsertTest1.java#tesInsertMapKey2 | 待做 |  | `INSERT INTO "xxx" ("ID","STARS","USERNAME") VALUES (?,ifnull("STARS",0)+?,?)` |
| UpdateTest.java#mapUpdateTest2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:269 | `UPDATE 'aaa' SET 'name' = ? WHERE 'id' = ?` |
| UpdateTest.java#mapUpdateTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:274 | `UPDATE 'aaa' SET 'name' = ?,'name1' = ? WHERE 'id' = ?` |
| UpdateTest.java#mapUpdateTest4 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:279 |  |
| UpdateTest.java#mapUpdateTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:286 | `UPDATE 't_topic' SET 'stars' = ? WHERE 'id' = ?` |
| UpdateTest.java#mapUpdateTest511 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:298 |  |
| UpdateTest.java#testColumnConfigure1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:436 | `INSERT INTO 'xxxxx' ('id','stars','title','create_time') VALUES (?,ifnull('stars',0)+?,?,?)` |
| UpdateTest.java#updateTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:84 | `UPDATE 't_topic' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest9 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:93 | `UPDATE 't_topic' SET 'stars' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTestBatch | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:312：批量：Java 用 JDBC batch 一条语句多组参数，C# 每个实体一条语句；Java 的 ignoreVersion 留到版本号（M5.3） |  |
| UpdateTest.java#updateTestBatch1 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:320：同 updateTestBatch |  |
| UpdateTest.java#updateTestBatch2 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:328：同 updateTestBatch |  |
| mysql8/MySQL8Test3.java#batchTest | 待做 |  |  |

### upsert（`write.upsert`，M5）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:144 | `INSERT INTO 'doc_bank_card' ('id','code','type') VALUES (?,?,?) ON DUPLICATE KEY UPDATE 'id' = VALUE…` |
| GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey2 | 骨架 | tests/EasyQuerySharp.Tests.Upstream/GeneralFunctionsTest.cs:175 |  |
| InsertOrUpdateTest.java#test1 | 待做 |  | `MERGE INTO [t_user] t1 USING (SELECT ? AS [column1],? AS [column2],? AS [column3],? AS [column4],? A…` |
| InsertTest.java#insertDuplicateKeyUpdate1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:64 | `INSERT INTO 't_topic_auto' ('stars','title','create_time') VALUES (?,?,?) ON DUPLICATE KEY UPDATE 's…` |
| InsertTest.java#insertDuplicateKeyUpdate2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:69 | `INSERT INTO 't_topic_auto' ('stars','title','create_time') VALUES (?,?,?) ON DUPLICATE KEY UPDATE 's…` |
| InsertTest.java#insertTestx | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:86 |  |
| InsertTest1.java#inertTest2 | 待做 |  | `INSERT INTO 't_topic_abc' ('id','stars','title','create_time') VALUES (?,?,?,?) ON DUPLICATE KEY UPD…` |
| QueryTest14.java#testxxxxx | 待做 |  | `MERGE INTO "t_topic" t1 USING (SELECT ? AS "id",? AS "stars",? AS "title",? AS "create_time" FROM DU…` |
| pgsql/InsertTest.java#insertDuplicateKeyIgnore2 | 待做 |  | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT DO NOTHING` |
| pgsql/InsertTest.java#insertDuplicateKeyUpdate2 | 待做 |  | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT ("title") DO U…` |
| pgsql/InsertTest.java#insertDuplicateKeyUpdate2_1 | 待做 |  | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT ("title") DO N…` |
| pgsql/InsertTest.java#insertDuplicateKeyUpdate3 | 待做 |  | `INSERT INTO "t_topic_auto" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT ("title") DO U…` |
| pgsql/InsertTest.java#insertTest2 | 待做 |  |  |

### ClickHouse 方言（`dialect.clickhouse`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| click/ClickHouseTest.java#testDelete1 | 待做 |  | `ALTER TABLE 't_topic' DELETE WHERE 'ID' = ?` |
| click/ClickHouseTest.java#testDelete2 | 待做 |  | `ALTER TABLE 't_topic' DELETE WHERE 'ID' = ?` |
| click/ClickHouseTest.java#testUpdate1 | 待做 |  | `ALTER TABLE 't_topic' UPDATE 'TITLE' = ? WHERE 'ID' = ?` |
| click/ClickHouseTest.java#testUpdate2 | 待做 |  | `ALTER TABLE 't_topic' UPDATE 'STARS' = ?,'TITLE' = ?,'CREATE_TIME' = ? WHERE 'ID' = ?` |

### 达梦方言（`dialect.dameng`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| dameng/DamengForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 待做 |  |  |
| dameng/DamengForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 待做 |  |  |
| dameng/DamengForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 待做 |  |  |
| dameng/DamengForUpdateTest.java#testForUpdateDuplicateCallThrows | 待做 |  |  |
| dameng/DamengForUpdateTest.java#testForUpdateJoinNotSupported | 待做 |  |  |
| dameng/DamengForUpdateTest.java#testForUpdateSqlAppendInTransaction | 待做 |  |  |
| dameng/DamengForUpdateTest.java#testForUpdateWithoutTransactionThrows | 待做 |  |  |
| dameng/DamengQueryTest.java#concat1 | 待做 |  | `select 'a' \|\| null \|\| 'b' as "aa" from DUAL` |
| dameng/DamengQueryTest.java#cteViewTree1 | 待做 |  |  |
| dameng/DamengQueryTest.java#query0 | 待做 |  | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE "ID" = ?` |
| dameng/DamengQueryTest.java#query0_1 | 待做 |  | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE "ID" = ? AND ROWNUM < 2` |
| dameng/DamengQueryTest.java#query1 | 待做 |  | `SELECT rt.* FROM(SELECT "ID","STARS","TITLE","CREATE_TIME", ROWNUM AS "__rownum__" FROM "MY_TOPIC" W…` |
| dameng/DamengQueryTest.java#query16 | 待做 |  | `SELECT COUNT(*) FROM (SELECT COUNT(*) AS "STARS" FROM "MY_TOPIC" t LEFT JOIN "MY_TOPIC" t1 ON t."ID"…` |
| dameng/DamengQueryTest.java#query17 | 待做 |  |  |
| dameng/DamengQueryTest.java#query2 | 待做 |  | `SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT "ID","STARS","TITLE","CREATE_TIM…` |
| dameng/DamengQueryTest.java#query3 | 待做 |  | `SELECT rt.* FROM (SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE "ID" = ? ORDER BY …` |
| dameng/DamengQueryTest.java#query4 | 待做 |  |  |
| dameng/DamengQueryTest.java#query5 | 待做 |  |  |
| dameng/DamengQueryTest.java#query6 | 待做 |  |  |
| dameng/DamengQueryTest.java#query7 | 待做 |  |  |
| dameng/DamengQueryTest.java#queryFormat1 | 待做 |  | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY-MM-"01"') AS "VALUE2" FROM "MY_TOPI…` |
| dameng/DamengQueryTest.java#queryFormat2 | 待做 |  | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY"年"MM-"01"') AS "VALUE2" FROM "MY_TO…` |
| dameng/DamengQueryTest.java#queryFormat3 | 待做 |  | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY"年"MM-"01" HH24"时"MI"分"SS"秒"') AS "V…` |
| dameng/DamengQueryTest.java#queryFormat4 | 待做 |  | `SELECT t."CREATE_TIME" AS "VALUE1",TO_CHAR(t."CREATE_TIME",'YYYY"年"MM-"01" HH24:MI"分"SS"秒"') AS "VAL…` |
| dameng/DamengQueryTest.java#subQueryTest1 | 待做 |  | `SELECT t."ID",t."STARS",t."TITLE",t."CREATE_TIME" FROM "MY_TOPIC" t LEFT JOIN (SELECT t1."TITLE" AS …` |
| dameng/DamengQueryTest.java#test1 | 待做 |  |  |
| dameng/DamengQueryTest.java#test111 | 待做 |  |  |
| dameng/DamengQueryTest.java#testAll | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t WHERE NOT (EXISTS(SELEC…` |
| dameng/DamengQueryTest.java#testAll1 | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t WHERE NOT (EXISTS(SELEC…` |
| dameng/DamengQueryTest.java#testAll2 | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t WHERE NOT (EXISTS(SELEC…` |
| dameng/DamengQueryTest.java#testAllGroupJoin | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testAllGroupJoin2 | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testBooleanValue | 待做 |  | `SELECT (CASE WHEN (EXISTS(SELECT 1 FROM "t_bank_card" t2 WHERE t2."UID" = t."ID" AND ROWNUM < 2)) TH…` |
| dameng/DamengQueryTest.java#testBooleanValue2 | 待做 |  | `SELECT (CASE WHEN (EXISTS(SELECT 1 FROM "t_bank_card" t2 WHERE t2."UID" = t."ID" AND ROWNUM < 2)) TH…` |
| dameng/DamengQueryTest.java#testDraft9 | 待做 |  |  |
| dameng/DamengQueryTest.java#testEasyQuery | 待做 |  |  |
| dameng/DamengQueryTest.java#testJoin1 | 待做 |  |  |
| dameng/DamengQueryTest.java#testJoining | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testJoining1 | 待做 |  | `SELECT t."ID",t."NAME",t."PHONE",t."AGE",t."CREATE_TIME" FROM "t_sys_user" t LEFT JOIN (SELECT t1."U…` |
| dameng/DamengQueryTest.java#testMaxColumns1 | 待做 |  | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE (SELECT MAX(__mmc.__col__) FROM (SEL…` |
| dameng/DamengQueryTest.java#testMaxColumns2 | 待做 |  | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE (SELECT MAX(__mmc.__col__) FROM (SEL…` |
| dameng/DamengQueryTest.java#testMaxColumns3 | 待做 |  | `SELECT "ID","STARS","TITLE","CREATE_TIME" FROM "MY_TOPIC" WHERE (SELECT MIN(__mmc.__col__) FROM (SEL…` |
| dameng/DamengQueryTest.java#testQuery | 待做 |  | `SELECT t."ID",t."NAME",t."ISDELETE" AS "IS_DELETE",t."ROWNUMBER" AS "ROW_NUMBER" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testQueryAlias | 待做 |  | `SELECT t."ID",t."NAME",t."ROWNUMBER" AS "ROW_NUMBER",t."ISDELETE" AS "IS_DELETE" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testQueryAlias2 | 待做 |  | `SELECT t."ID",t."NAME",t."ROWNUMBER",t."ISDELETE" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testQueryAlias3 | 待做 |  | `SELECT t."ID",t."NAME",t."ROWNUMBER",t."ISDELETE" FROM "MY_CUSTOM" t` |
| dameng/DamengQueryTest.java#testaa | 待做 |  |  |
| dameng/MathQueryTest.java#testPropertyMath1 | 待做 |  | `SELECT t."TEST_VALUE" AS "TEST_VALUE",ABS(t."TEST_VALUE") AS "TEST_ABS",SIGN(t."TEST_VALUE") AS "TES…` |
| dameng/UpdateJoinTest.java#testTopicInsertOrUpdate | 待做 |  |  |
| dameng/UpdateJoinTest.java#testUpdateJoin1 | 待做 |  | `UPDATE "doc_bank_card" t INNER JOIN "doc_user" t1 ON t1."ID" = t."UID" SET t."TYPE" = t1."NAME" WHER…` |
| dameng/UpdateJoinTest.java#testUpdateJoin2 | 待做 |  | `UPDATE "doc_bank_card" t INNER JOIN "doc_bank" t1 ON t1."ID" = t."BANK_ID" INNER JOIN "doc_user" t2 …` |

### DuckDB 方言（`dialect.duckdb`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| duckdb/AllTypeTest.java#testAllType | 待做 |  |  |
| duckdb/AllTypeTest.java#testAllType1 | 待做 |  |  |
| duckdb/DuckDBMathQueryTest.java#testPropertyMath | 待做 |  | `SELECT t."test_value" AS "test_value",ABS(t."test_value") AS "test_abs",SIGN(t."test_value") AS "tes…` |
| duckdb/DuckDBQueryTest.java#datetimeformat | 待做 |  | `SELECT t."create_time" AS "value1",strftime(t."create_time", '%Y年%m-01 %H:%M分%S秒') AS "value2" FROM …` |
| duckdb/DuckDBQueryTest.java#datetimeformat1 | 待做 |  | `SELECT t."create_time" AS "value1",strftime(t."create_time", '%Y年%m-01 %H:%M分%S秒') AS "value2" FROM …` |
| duckdb/DuckDBQueryTest.java#formatMySQL | 待做 |  | `SELECT t."create_time" AS "value1",strftime(t."create_time", '%Y年%m-01 %H:%M分%S秒') AS "value2",strft…` |
| duckdb/DuckDBQueryTest.java#readExcel1 | 待做 |  | `SELECT "id","name","age" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') WHERE "name" = ?` |
| duckdb/DuckDBQueryTest.java#readExcel2 | 待做 |  | `SELECT t."id",t."name",t."age" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t WHERE EXISTS(SELEC…` |
| duckdb/DuckDBQueryTest.java#readExcel3 | 待做 |  | `SELECT t."id",t."name",t."age" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t LEFT JOIN (SELECT …` |
| duckdb/DuckDBQueryTest.java#readExcel4 | 待做 |  | `SELECT t."name" AS "value1",SUBSTR(t."name",5,1) AS "value2" FROM read_xlsx('./ducktest.xlsx',sheet=…` |
| duckdb/DuckDBQueryTest.java#testConcat | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| duckdb/DuckDBQueryTest.java#testConcat2 | 待做 |  |  |
| duckdb/DuckDBQueryTest.java#testConcat3 | 待做 |  | `SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","s…` |
| duckdb/DuckDBQueryTest.java#testDraft9 | 待做 |  |  |
| duckdb/DuckDBQueryTest.java#testDraft9_1 | 待做 |  |  |
| duckdb/M8Save4Test.java#testToManyToMany1 | 待做 |  |  |

### 方言静态入口与专属函数（`dialect.functions`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/dialect/DialectEntrySqlTest.java#mysqlFullTextAgainstAndScore | 待做 |  | `SELECT t.'id' AS 'value1',MATCH(t.'name') AGAINST(? IN NATURAL LANGUAGE MODE) AS 'value2' FROM 't_fo…` |
| fork/dialect/DialectEntrySqlTest.java#mysqlJsonPredicatesInWhere | 待做 |  | `SELECT {…} FROM 't_fork_user' WHERE JSON_CONTAINS('attrs',?,?) AND ? MEMBER OF(JSON_EXTRACT('attrs',…` |
| fork/dialect/DialectEntrySqlTest.java#mysqlPredicateInsideImplicitSubquery | 待做 |  | `SELECT t.{…} FROM 't_fork_user' t WHERE EXISTS(SELECT 1 FROM 't_fork_post' t1 WHERE t1.'user_id' = t…` |
| fork/dialect/DialectEntrySqlTest.java#mysqlPredicateRespectsOrGroup | 待做 |  | `SELECT {…} FROM 't_fork_user' WHERE 'name' = ? AND (JSON_CONTAINS('attrs',?) OR 'age' > ?)` |
| fork/dialect/DialectEntrySqlTest.java#pgJsonbPredicates | 待做 |  | `SELECT "id","name","age","company_id","attrs","created_at" FROM "t_fork_user" WHERE ("attrs")::jsonb…` |
| fork/dialect/DialectEntrySqlTest.java#userDefinedFunctionHasTheSameShape | 待做 |  | `SELECT t.{…} FROM 't_fork_user' t WHERE EXISTS(SELECT 1 FROM 't_fork_post' t1 WHERE t1.'user_id' = t…` |
| fork/dialect/DialectEntrySqlTest.java#userDefinedValueFunctionChainsLikeBuiltIns | 待做 |  | `SELECT {…} FROM 't_fork_user' WHERE CHAR_LENGTH('name') > ? ORDER BY CHAR_LENGTH('name') DESC` |
| fork/dialect/DialectEntrySqlTest.java#wrongDialectFailsWhenTheExpressionIsBuilt | 待做 |  |  |
| fork/dialect/HanaTextSqlTest.java#containsAndFuzzy | 待做 |  |  |
| fork/dialect/HanaTextSqlTest.java#wrongDialectAndBadThreshold | 待做 |  |  |
| fork/dialect/MySQLJsonSqlTest.java#containsPath | 待做 |  | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user' WHERE JSON_CONTAINS_PA…` |
| fork/dialect/MySQLJsonSqlTest.java#modificationsChainIntoUpdate | 待做 |  | `UPDATE 't_fork_user' SET 'attrs' = JSON_REMOVE(JSON_SET('attrs',?,?),?,?) WHERE 'id' = ?` |
| fork/dialect/PgSQLM4SqlTest.java#arrayAndFullText | 待做 |  |  |
| fork/dialect/PgSQLM4SqlTest.java#jsonbEdit | 待做 |  | `UPDATE "t_fork_user" SET "attrs" = (jsonb_set(("attrs")::jsonb,(?)::text[],(?)::jsonb,true) #- (?)::…` |
| fork/live/HanaTextLiveTest.java#containsFuzzyAndScore | 待做 |  | `DROP TABLE "t_fork_post"` |
| fork/live/MySQLDialectEntryLiveTest.java#fullTextAgainstAndScore | 待做 |  |  |
| fork/live/MySQLDialectEntryLiveTest.java#insideImplicitSubquery | 待做 |  |  |
| fork/live/MySQLDialectEntryLiveTest.java#jsonContainsWithPath | 待做 |  |  |
| fork/live/MySQLDialectEntryLiveTest.java#memberOfAndOverlaps | 待做 |  |  |
| fork/live/MySQLJsonLiveTest.java#pathPredicatesAndUpdates | 待做 |  | `DROP TABLE IF EXISTS t_fork_user` |
| fork/live/PgSQLDialectEntryLiveTest.java#containsAndContainedBy | 待做 |  |  |
| fork/live/PgSQLDialectEntryLiveTest.java#insideImplicitSubquery | 待做 |  |  |
| fork/live/PgSQLDialectEntryLiveTest.java#keyExistence | 待做 |  |  |
| fork/live/PgSQLM4LiveTest.java#arrayPredicatesAndFullText | 待做 |  | `DROP TABLE "t_fork_post"` |
| fork/live/PgSQLM4LiveTest.java#jsonbEditRoundTrip | 待做 |  | `DROP TABLE "t_fork_user"` |

### H2 方言（`dialect.h2`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| h2/BasicTypeTest.java#booleanTest1 | 待做 |  | `SELECT ? FROM t_all_type t LIMIT 1` |
| h2/BasicTypeTest.java#booleanTest2 | 待做 |  | `SELECT t.enable FROM t_all_type t WHERE t.enable = ? LIMIT 1` |
| h2/BasicTypeTest.java#booleanTest3 | 待做 |  | `SELECT t.enable_basic FROM t_all_type t WHERE t.enable_basic = ? LIMIT 1` |
| h2/BasicTypeTest.java#byteTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#byteTest2 | 待做 |  | `SELECT t.number_byte FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#byteTest3 | 待做 |  | `SELECT t.number_byte_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#doubleTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#doubleTest2 | 待做 |  | `SELECT t.number_double FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#doubleTest3 | 待做 |  | `SELECT t.number_double_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#floatTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#floatTest2 | 待做 |  | `SELECT t.number_float FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#floatTest3 | 待做 |  | `SELECT t.number_float_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#intTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#intTest2 | 待做 |  | `SELECT t.number_integer FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#intTest3 | 待做 |  | `SELECT t.number_integer_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTest2 | 待做 |  | `SELECT t.time_local_date FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTimeTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localDateTimeTest2 | 待做 |  | `SELECT t.time_local_date_time FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localTimeTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#localTimeTest2 | 待做 |  | `SELECT t.time_local_time FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#longTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#longTest2 | 待做 |  | `SELECT t.number_long FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#longTest3 | 待做 |  | `SELECT t.number_long_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#shortTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#shortTest2 | 待做 |  | `SELECT t.number_short FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#shortTest3 | 待做 |  | `SELECT t.number_short_basic FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#timeTest1 | 待做 |  | `SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/BasicTypeTest.java#timeTest2 | 待做 |  | `SELECT t.only_time FROM t_all_type t WHERE t.id = ? LIMIT 1` |
| h2/H2DocTest.java#selectClassTest1 | 待做 |  | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectClassTest2 | 待做 |  | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectClassTest3 | 待做 |  | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectClassTest4 | 待做 |  | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectClassTest5 | 待做 |  | `SELECT t.id,t.name,t.phone,t.age,IFNULL(t.age,1) AS card_count FROM doc_user t WHERE t.name LIKE ?` |
| h2/H2DocTest.java#selectProxyTest1 | 待做 |  | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectProxyTest1_1 | 待做 |  | `SELECT t.id,t.code FROM doc_bank_card t WHERE t.type = ?` |
| h2/H2DocTest.java#selectProxyTest2 | 待做 |  | `SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t LEFT …` |
| h2/H2DocTest.java#selectProxyTest3 | 待做 |  | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectProxyTest4 | 待做 |  | `SELECT t.id,t.name,t.phone,t.age,(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card…` |
| h2/H2DocTest.java#selectProxyTest5 | 待做 |  | `SELECT t.id,t.name,t.phone,t.age,IFNULL(t.age,1) AS card_count FROM doc_user t WHERE t.name LIKE ?` |
| h2/H2DocTest.java#testToOne | 待做 |  | `SELECT id,name,phone,age FROM doc_user WHERE name LIKE ? AND id IS NULL AND id IS NOT NULL` |
| h2/H2DocTest.java#testToOne1 | 待做 |  | `SELECT t.id,t.uid,t.code,t.type,t.bank_id FROM doc_bank_card t LEFT JOIN doc_user t1 ON t1.id = t.ui…` |
| h2/H2ForUpdateTest.java#testForUpdateAppliesOnlyOnOuterQueryForExistsAndInSubQuery | 待做 |  |  |
| h2/H2ForUpdateTest.java#testForUpdateBlocksConcurrentTransaction | 待做 |  |  |
| h2/H2ForUpdateTest.java#testForUpdateDerivedCountAndExistsSqlBehaviorConsistent | 待做 |  |  |
| h2/H2ForUpdateTest.java#testForUpdateDuplicateCallThrows | 待做 |  |  |
| h2/H2ForUpdateTest.java#testForUpdateJoinNotSupported | 待做 |  |  |
| h2/H2ForUpdateTest.java#testForUpdateSqlAppendInTransaction | 待做 |  |  |
| h2/H2ForUpdateTest.java#testForUpdateWithoutTransactionThrows | 待做 |  |  |
| h2/H2OrderTest.java#test1 | 待做 |  |  |
| h2/H2OrderTest.java#test2 | 待做 |  |  |
| h2/H2QueryRelationTest.java#tbTest1 | 待做 |  | `SELECT id,uid,uname,price FROM t_tb_order` |
| h2/H2QueryTest.java#allTypeTest1 | 待做 |  |  |
| h2/H2QueryTest.java#allTypeTest1_1 | 待做 |  |  |
| h2/H2QueryTest.java#allTypeTest1_1_proxy | 待做 |  |  |
| h2/H2QueryTest.java#allTypeTest1_2 | 待做 |  |  |
| h2/H2QueryTest.java#allTypeTest1_2_proxy | 待做 |  |  |
| h2/H2QueryTest.java#allTypeTest1_proxy | 待做 |  |  |
| h2/H2QueryTest.java#allTypeTestSharding1 | 待做 |  |  |
| h2/H2QueryTest.java#conditionTest1 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin1 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin1_1 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin2 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin2_1 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin3 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin3_1 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin4 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#leftJoin4_1 | 待做 |  | `SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.…` |
| h2/H2QueryTest.java#testDDL | 待做 |  | `CREATE TABLE IF NOT EXISTS t_topic ( id VARCHAR(255) NOT NULL , stars INT NULL , title VARCHAR(255) …` |
| h2/H2QueryTest.java#testDDL2 | 待做 |  | `CREATE TABLE IF NOT EXISTS t_multi_primary ( id1 VARCHAR(255) NOT NULL , id2 VARCHAR(255) NOT NULL ,…` |
| h2/H2QueryTest.java#testTableCommentDDL | 待做 |  | `CREATE TABLE IF NOT EXISTS t_h2_table_comment ( id VARCHAR(255) NOT NULL COMMENT '主键ID', name VARCHA…` |
| h2/TreeCTETest.java#tree8 | 待做 |  | `WITH RECURSIVE as_tree_cte(cte_deep,id,parent_id,name) AS ( (SELECT 0 AS cte_deep,t1.id,t1.parent_id…` |
| h2/v2/H2V2Test.java#testAllType | 待做 |  |  |
| h2/v2/H2V2Test.java#testAllType1 | 待做 |  |  |

### SAP HANA 方言（`dialect.hana`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/dialect/HanaSqlTest.java#dateFormatTranslation | 待做 |  |  |
| fork/dialect/HanaSqlTest.java#existsSubqueryHasNoLimit | 待做 |  | `SELECT {…} FROM "t_fork_user" t LEFT JOIN "t_fork_company" t1 ON t1."id" = t."company_id" WHERE t1."…` |
| fork/dialect/HanaSqlTest.java#functions | 待做 |  | `SELECT ((t."name" \|\| ?) \|\| t."id") AS "value1",SUBSTRING(t."name",1,4) AS "value2",(LOCATE(t."name",…` |
| fork/dialect/HanaSqlTest.java#joinedUpdateIsMergeAndJoinedDeleteIsExists | 待做 |  | `MERGE INTO "t_fork_user" t USING (SELECT t."id" AS "eq_key_0",? AS "eq_value_0" FROM "t_fork_user" t…` |
| fork/dialect/HanaSqlTest.java#pagingUsesLimitOffset | 待做 |  | `SELECT "id","name","age","company_id","attrs","created_at" FROM "t_fork_user" WHERE "name" LIKE ? OR…` |
| fork/dialect/HanaSqlTest.java#upsertIsMerge | 待做 |  | `MERGE INTO "t_fork_user" t1 USING (SELECT ? AS "id",? AS "name" FROM DUMMY) t2 ON (t1."id" = t2."id"…` |
| fork/live/HanaDialectLiveTest.java#aggregatesAndBooleanProjection | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#dateFunctions | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#joinedUpdateAndDelete | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#likeEscapesWildcards | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#mergeUpsert | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#pagingImplicitJoinAndSubquery | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#plainCrudAndForUpdate | 待做 |  |  |
| fork/live/HanaDialectLiveTest.java#stringAndNullFunctions | 待做 |  |  |
| fork/live/HanaKeysAndTypesLiveTest.java#generatedKeyIsFilledBack | 待做 |  |  |
| fork/live/HanaKeysAndTypesLiveTest.java#generatedKeysForBatch | 待做 |  |  |
| fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeRoundTrip | 待做 |  |  |
| fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeSurvivesDstGapOfJvmZone | 待做 |  |  |

### 人大金仓方言（`dialect.kingbase`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| kingbase/MyKingbaseESTest.java#testaa | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','delete_by','delete_time',…` |
| kingbase/MyKingbaseESTest.java#testaa1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','delete_by','delete_time',…` |

### SQL Server 方言（`dialect.mssql`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mssql/MSSQLMathQueryTest.java#testPropertyMath | 待做 |  | `SELECT t.[TestValue] AS [TestValue],ABS(t.[TestValue]) AS [TestAbs],SIGN(t.[TestValue]) AS [TestSign…` |
| mssql/MSSQLTest1.java#test1 | 待做 |  | `SELECT t2.[code] AS [code],t2.[name] AS [name] FROM (SELECT t.[Code] AS [code],t1.[Code] AS [name] F…` |
| mssql/MsSQLQueryTest.java#concat1 | 待做 |  | `select CONCAT('a', null , 'b') as [aa]` |
| mssql/MsSQLQueryTest.java#query0 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query1 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query10 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (SUBSTRING(CONVERT(CHAR(8), [CreateTim…` |
| mssql/MsSQLQueryTest.java#query11 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (SUBSTRING(CONVERT(CHAR(8), [CreateTim…` |
| mssql/MsSQLQueryTest.java#query2 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query3 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssql/MsSQLQueryTest.java#query4 | 待做 |  |  |
| mssql/MsSQLQueryTest.java#query5 | 待做 |  |  |
| mssql/MsSQLQueryTest.java#query6 | 待做 |  |  |
| mssql/MsSQLQueryTest.java#query7 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (CASE WHEN [Title] = ? THEN 0 WHEN [Ti…` |
| mssql/MsSQLQueryTest.java#query8 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE (CASE WHEN [Title] = [Id] THEN 0 WHEN …` |
| mssql/MsSQLQueryTest.java#query9 | 待做 |  | `SELECT t.[Title] AS [Value1],STRING_AGG(t.[Id], ?) AS [Value2] FROM [MyTopic] t GROUP BY t.[Title]` |
| mssql/MsSQLQueryTest.java#testAll | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WHERE NOT (EXISTS(SELECT…` |
| mssql/MsSQLQueryTest.java#testAll1 | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WHERE NOT (EXISTS(SELECT…` |
| mssql/MsSQLQueryTest.java#testAll2 | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WHERE NOT (EXISTS(SELECT…` |
| mssql/MsSQLQueryTest.java#testAllGroupJoin | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t LEFT JOIN (SELECT t1.[Ui…` |
| mssql/MsSQLQueryTest.java#testAllGroupJoin2 | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t LEFT JOIN (SELECT t1.[Ui…` |
| mssql/MsSQLQueryTest.java#testBatchInsert | 待做 |  |  |
| mssql/MsSQLQueryTest.java#testBigDecima2 | 待做 |  |  |
| mssql/MsSQLQueryTest.java#testBigDecima4 | 待做 |  |  |
| mssql/MsSQLQueryTest.java#testBigDecimal | 待做 |  |  |
| mssql/MsSQLQueryTest.java#testFormat | 待做 |  | `SELECT t.[CreateTime] AS [Value1],(SUBSTRING(CONVERT(CHAR(8), t.[CreateTime], 112), 1, 4) + ? + SUBS…` |
| mssql/MsSQLQueryTest.java#testLikeConcat1 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Title] LIKE ('%'+CAST(CONCAT(?,[Id],?…` |
| mssql/MsSQLQueryTest.java#testLikeConcat2 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Title] LIKE (CAST(CONCAT(?,[Id],?) AS…` |
| mssql/MsSQLQueryTest.java#testLikeConcat3 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Title] LIKE ('%'+CAST(CONCAT(?,[Id],?…` |
| mssql/MsSQLQueryTest.java#testMaxColumns | 待做 |  | `SELECT [Id],[Column1],[Column2],[Column3] FROM [t_calc] WHERE (SELECT MAX(__col__) FROM (VALUES ([Co…` |
| mssql/MsSQLQueryTest.java#testMaxColumns2 | 待做 |  | `SELECT t.[Column1] AS [Value1],t.[Column2] AS [Value2],t.[Column3] AS [Value3],(SELECT MAX(__col__) …` |
| mssql/MsSQLQueryTest.java#testWithIndex | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WITH(INDEX(myIndex)) LEF…` |
| mssql/MsSQLQueryTest.java#testWithNolock1 | 待做 |  | `SELECT t.[Id],t.[Uid],t.[Code],t.[Type],t.[BankId],t.[OpenTime] FROM [t_bank_card] t WITH(NOLOCK) LE…` |
| mssql/MsSQLQueryTest.java#testWithNolock2 | 待做 |  | `SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WITH(NOLOCK) LEFT JOIN […` |
| mssql/MsSQLQueryTest.java#update1 | 待做 |  | `UPDATE t SET t.[Title] = ? FROM [MyTopic] t WHERE t.[Id] IS NULL AND EXISTS(SELECT 1 FROM [MyTopic] …` |
| mssql/MsSQLUpdateJoinTest.java#testDeleteJoin1 | 待做 |  | `DELETE t FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Uid] WHERE t1.[Name] LIKE ?` |
| mssql/MsSQLUpdateJoinTest.java#testDeleteJoin2 | 待做 |  | `DELETE t FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Uid] WHERE t1.[Name] LIKE ?` |
| mssql/MsSQLUpdateJoinTest.java#testUpdateJoin1 | 待做 |  | `UPDATE t SET t.[Type] = t1.[Name] FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Ui…` |
| mssql/MsSQLUpdateJoinTest.java#testUpdateJoin2 | 待做 |  | `UPDATE t SET t.[Type] = t1.[Name] FROM [doc_bank_card] t INNER JOIN [doc_bank] t1 ON t1.[Id] = t.[Ba…` |

### SQL Server 2008（ROW_NUMBER 分页）（`dialect.mssql2008`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query0 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query1 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query2 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query3 | 待做 |  | `SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic] WHERE [Id] = ?` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query4 | 待做 |  |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query4_1 | 待做 |  |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query4_2 | 待做 |  |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5 | 待做 |  |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5_1 | 待做 |  |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5_2 | 待做 |  | `WITH rt AS ( SELECT [Id],[Stars],[Title],[CreateTime], ROW_NUMBER() OVER( ORDER BY [Id] ASC ) AS __r…` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query5_3 | 待做 |  | `WITH rt AS ( SELECT [Id],[Stars],[Title],[CreateTime], ROW_NUMBER() OVER( ORDER BY [Id] ) AS __rownu…` |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#query6 | 待做 |  |  |
| mssqlrownumber/MsSQLRowNumberQueryTest.java#testDraft9 | 待做 |  |  |

### UPDATE / DELETE … RETURNING（`write.returning`，M6）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| fork/dialect/ReturningSqlTest.java#deleteAndUpdateReturning | 待做 |  | `DELETE FROM "t_fork_post" WHERE "user_id" = ? RETURNING *` |
| fork/dialect/ReturningSqlTest.java#otherDialectsAreRejected | 待做 |  |  |
| fork/live/ReturningLiveTest.java#deleteAndUpdateReturnRows | 待做 |  | `DROP TABLE "t_fork_post"` |

### 建表与迁移（code-first）（`codeFirst`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| InsertTest1.java#testInsert | 待做 |  | `INSERT INTO 't_test_insert' ('id','column1','column2') VALUES (?,?,?)` |
| MigrationTest.java#createTableSQLTest1 | 待做 |  | `CREATE TABLE IF NOT EXISTS 'my_migration_blog' ( 'id' VARCHAR(255) NOT NULL , 'create_time' DATETIME…` |
| MigrationTest.java#createTableSQLTest2 | 待做 |  | `ALTER TABLE 't_topic' RENAME TO 't_topic2';` |
| MigrationTest.java#createTableSQLTest3 | 待做 |  | `ALTER TABLE 't_topic' CHANGE 'title' 'abc' varchar(200) NULL;` |
| MigrationTest.java#createTableSQLTest4 | 待做 |  |  |
| QueryTest22.java#testManyOrder | 待做 |  |  |
| QueryTest24.java#testx18 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest26.java#testGenerateKeys | 待做 |  |  |
| QueryTest3.java#userBookTest | 待做 |  |  |
| migration/MigrationDamengTest.java#test | 待做 |  |  |
| migration/MigrationPgTest.java#test | 待做 |  |  |
| mysql8/M8Test1.java#testNoKeyCodeFirst | 待做 |  |  |
| mysql8/MySQL8Test2.java#test | 待做 |  |  |
| pgsql/MigrationTest.java#test1 | 待做 |  |  |
| sqlite/SQLiteTest.java#testDDL | 待做 |  | `CREATE TABLE IF NOT EXISTS "t_topic" ( "Id" VARCHAR(255) NOT NULL , "Stars" integer NULL , "Title" V…` |
| sqlite/SQLiteTest.java#testDDL2 | 待做 |  | `CREATE TABLE IF NOT EXISTS "t_multi_primary" ( "Id1" VARCHAR(255) NOT NULL , "Id2" VARCHAR(255) NOT …` |
| testmysql8/MyTest.java#testaa | 待做 |  |  |

### 按对象筛选排序（whereObject / orderByObject）（`dto.object`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| QueryTest.java#query15 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' > ?` |
| QueryTest.java#query18 | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'create_time' > ? ORDER BY 'create_ti…` |
| QueryTest14.java#testClose | 待做 |  | `select * from t_topic` |
| QueryTest18.java#rangeTest1_1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_2 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_4 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_5 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest1_6 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#rangeTest3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest18.java#test11 | 待做 |  |  |
| QueryTest18.java#test13 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest19.java#test1 | 待做 |  | `SELECT t.'id',t.'company_id',t.'name',t.'age',t.'create_time' FROM 't_user' t WHERE t.'name' = ?` |
| QueryTest19.java#testMyFilter | 待做 |  | `SELECT 'id','stars','title','create_time' FROM 't_topic' WHERE 'title' LIKE ?` |
| QueryTest2.java#dynamicSort | 待做 |  | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t WHERE t.'deleted' = ? ORDER BY t.'title' ASC` |
| QueryTest2.java#dynamicSort2 | 待做 |  | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t WHERE t.'deleted' = ? ORDER BY t.'title' ASC,t.'…` |
| QueryTest2.java#dynamicSort3 | 待做 |  | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' WHER…` |
| QueryTest2.java#dynamicSort4 | 待做 |  | `SELECT t.'id',t.'title',t.'content' FROM 't_blog' t INNER JOIN 't_topic' t1 ON t.'id' = t1.'id' WHER…` |
| QueryTest2.java#queryTest17 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest18 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest18_1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest18_2 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest19 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest2.java#queryTest20 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest21.java#testOrderBy2 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id' FROM 'doc_bank_card' t LEFT JOIN 'doc_user' t1 O…` |
| QueryTest24.java#test14 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test15 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test16 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test17 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test18 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test19 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test20 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test21 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test22 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test23 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test24 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test25 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#test26 | 待做 |  | `SELECT (t.'create_time' < ? AND t.'title' = ?) FROM 't_blog' t WHERE t.'deleted' = ? AND (t.'create_…` |
| QueryTest24.java#test27 | 待做 |  | `SELECT DATE_FORMAT(t.'create_time','%Y-%m') AS 'value1',(DATE_FORMAT(t.'create_time','%Y-%m') LIKE C…` |
| QueryTest24.java#test28 | 待做 |  | `SELECT DATE_FORMAT(t.'create_time','%Y-%m') AS 'value1',((DATE_FORMAT(t.'create_time','%Y-%m') LIKE …` |
| QueryTest24.java#test29 | 待做 |  | `SELECT (t.'create_time' < ? AND t.'title' = ?) FROM 't_blog' t WHERE t.'deleted' = ? AND date_add(t.…` |
| QueryTest24.java#testContains2 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest24.java#testContains3 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#dynamicWhere1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orderTest1 | 待做 |  | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest3.java#orderTest2 | 待做 |  |  |
| QueryTest3.java#query124 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest3.java#query124_5 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_blog' t1 ON t1.'dele…` |
| QueryTest4.java#joinTest1 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest2 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest3 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest4 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest5 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest6 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTest4.java#joinTest7 | 待做 |  | `SELECT t.'id',COUNT(t.'id') AS 'stars' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' L…` |
| QueryTestRelationTest.java#relationTest0 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'gw' FROM 'my_com_user' t WHERE EXISTS(SELECT 1 FROM 'my_sign…` |
| QueryTestRelationTest.java#relationTest0_2 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t WHERE t.'com_id' = ?` |
| QueryTestRelationTest.java#relationTest0_3 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_4 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_5 | 待做 |  | `SELECT (SELECT COUNT(*) FROM 'my_sign_up' t2 WHERE t2.'com_id' = t1.'com_id') AS 'id' FROM 'my_sign_…` |
| QueryTestRelationTest.java#relationTest0_6 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_7 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t LEFT JOIN 'my_com_user…` |
| QueryTestRelationTest.java#relationTest0_8 | 待做 |  | `SELECT t1.'com_id' AS 'value1' FROM 'my_sign_up' t LEFT JOIN 'my_com_user' t1 ON (t1.'com_id' = t.'c…` |
| QueryTestRelationTest.java#test1_1 | 待做 |  | `SELECT (CASE WHEN t1.'name' LIKE ? THEN ? ELSE ? END) AS 'value1' FROM 'my_com_user' t LEFT JOIN 'my…` |
| QueryTestRelationTest.java#test2 | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'gw' FROM 'my_com_user' t` |
| QueryTestRelationTest.java#testDirectWhere | 待做 |  | `SELECT t.'id',t.'com_id',t.'user_id',t.'time',t.'content' FROM 'my_sign_up' t` |
| doc/DocTest.java#test1 | 待做 |  | `SELECT COUNT(*) FROM 't_sys_user' WHERE 'name' LIKE ? AND 'phone' LIKE ? AND 'create_time' >= ? AND …` |
| mysql8/ExampleTest.java#sampleTest1 | 待做 |  |  |
| mysql8/M8BankTest.java#test10 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Mysql8/M8BankTest.cs:111：Java 的 filterConfigure(NotNullOrEmptyValueFilter) 在 C# 里用 WhereIf 表达，这里条件都有值，SQL 相同 | `SELECT t.'code' AS 'value1',t2.'name' AS 'value2',t1.'name' AS 'value3' FROM 't_bank_card' t LEFT JO…` |
| mysql8/M8BankTest.java#test4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE (SELECT COUNT(*) …` |
| mysql8/M8BankTest.java#test9 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t` |
| mysql8/M8QueryTest.java#whereObject1 | 待做 |  | `SELECT t.'id',t.'uid',t.'code',t.'type',t.'bank_id',t.'open_time' FROM 't_bank_card' t INNER JOIN 't…` |
| mysql8/M8QueryTest.java#whereObject2 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8QueryTest.java#whereObject3 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t WHERE EXISTS(SELECT 1 F…` |
| mysql8/M8QueryTest.java#whereObject4 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/M8QueryTest.java#whereObject6 | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t1.'u…` |
| mysql8/MySQL8Test2.java#testFilterConfigure1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure10 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure11 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure2 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t WHERE EXISTS(SELECT 1 FROM 't_bank_card' t1 W…` |
| mysql8/MySQL8Test2.java#testFilterConfigure3 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/MySQL8Test2.java#testFilterConfigure3_1 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/MySQL8Test2.java#testFilterConfigure4 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'bank_id' AS '__group_ke…` |
| mysql8/MySQL8Test2.java#testFilterConfigure5 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#testFilterConfigure6 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#testFilterConfigure7 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t` |
| mysql8/MySQL8Test2.java#testFilterConfigure8 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'code',…` |
| mysql8/MySQL8Test2.java#testFilterConfigure9 | 待做 |  | `SELECT t.'id',t.'name',t.'create_time' FROM 't_bank' t LEFT JOIN (SELECT t1.'id',t1.'uid',t1.'code',…` |
| mysql8/MySQL8Test2.java#testMerge | 待做 |  | `SELECT IFNULL(t2.'__count2__',0) AS 'value1',IFNULL(t2.'__count3__',0) AS 'value2' FROM 'm8_province…` |
| mysql8/MySQL8Test2.java#testMerge2 | 待做 |  | `SELECT COUNT(*) FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',COUNT(?) AS '__c…` |
| mysql8/MySQL8Test2.java#testMerge3 | 待做 |  | `SELECT COUNT(*) FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',COUNT(?) AS '__c…` |
| mysql8/MySQL8Test2.java#testMerge4 | 待做 |  | `SELECT COUNT(*) FROM 'm8_province' t WHERE t.'name' LIKE CONCAT('%',?,'%')` |
| mysql8/MySQL8Test2.java#testMerge5 | 待做 |  | `SELECT COUNT(*) FROM 'm8_province' t LEFT JOIN (SELECT t1.'pid' AS '__group_key1__',COUNT((CASE WHEN…` |
| mysql8/MySQL8Test2.java#testSumCount | 待做 |  | `SELECT IFNULL(t2.'__sum2__',0) AS 'value1',IFNULL(t2.'__sum3__',0) AS 'value2' FROM 'm8_province' t …` |
| mysql8/MySQL8Test3.java#testDynamicJoin | 待做 |  |  |
| mysql8/MySQL8Test4.java#testFilter | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN 't_sys_user' …` |
| mysql8/MySQL8Test4.java#testFilterAndDependency | 待做 |  | `SELECT t.'id',t.'name',t.'phone',t.'age',t.'create_time' FROM 't_sys_user' t LEFT JOIN (SELECT t2.'i…` |
| mysql8/MySQL8Test4.java#testSubQueryCountCompareNull2 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testTimeAfterNull2 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test4.java#testTimeBeforeNull2 | 待做 |  | `SELECT 'id','name','phone','age','create_time' FROM 't_sys_user'` |
| mysql8/MySQL8Test5.java#testToSelectPageResult | 待做 |  | `SELECT t1.'id',t1.'name',t1.'create_time',t3.'__joining2__' AS '__part__column1' FROM (SELECT t.'id'…` |

### search 扩展（`search`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| search/EasySearchBaseCommonTests.java#testCommon | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testCommon2 | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testComplexJoinConditions | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testDefaultSort | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testEmpty | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testMultiFieldSortOverride | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testMultiSourceCondition | 待做 |  |  |
| search/EasySearchBaseCommonTests.java#testNullValueHandling | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testCrossTableCondition | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testEmptyResult | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testInvalidOperator | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testLargeResultSet | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testMultiCondition | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testNullFieldName | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testNullValueHandling | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_gt_maxValue | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_in_empty | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_lk_emptyPattern | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_lt_boundary | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testOperator_roc_zero_range | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testThreeConditionCombination | 待做 |  |  |
| search/EasySearchBaseOpExtraTests.java#testTypeMismatch | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_eq | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_ge | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_gt | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_in | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_inn | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_le | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_lk | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_lml | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_lmr | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_lt | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_ne | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_ni | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nl | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nlml | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nlmr | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nrc | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nrco | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nro | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_nroc | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_rc | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_rco | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_ro | 待做 |  |  |
| search/EasySearchBaseOpTests.java#testOperator_roc | 待做 |  |  |

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
| EntityQueryAggregateTest2.java#test1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test2 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test3 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test4 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test5 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test6 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test7 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test8 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| EntityQueryAggregateTest2.java#test9 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' …` |
| InsertTest.java#insertTableLinkTest1 | 待做 |  | `INSERT INTO 1't_topic_auto' ('stars','title','create_time') VALUES (?,?,?) ON DUPLICATE KEY UPDATE '…` |
| QueryTest14.java#test2 | 待做 |  | `SELECT rt.* FROM (SELECT t."title" AS "title",COUNT(t."id") AS "star" FROM "t_blog" t WHERE t."delet…` |
| QueryTest14.java#test3 | 待做 |  | `UPDATE "t_blog" SET "deleted" = ? WHERE "t_blog"."deleted" = ? AND EXISTS(SELECT 1 FROM "t_blog" t1 …` |
| QueryTest3.java#query124_1 | 待做 |  | `SELECT t.'id',t.'stars',t.'title',t.'create_time' FROM 't_topic' t LEFT JOIN lateral 't_blog' t1 ON …` |

### 换表名（asTable / 表名映射）（`table.rename`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| InsertTest1.java#testColumnConfigure2 | 待做 |  | `INSERT INTO 'xxx' ('id','stars') VALUES (?,ifnull('stars',0)+?)` |
| QueryAggregateTest1.java#test1 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test2 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test3 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test4 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test5 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test6 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test7 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest1.java#test8 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 'abcv'…` |
| QueryAggregateTest2.java#test1 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test2 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test3 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test4 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test5 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test6 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test7 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 't_top…` |
| QueryAggregateTest2.java#test8 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t RIGHT JOIN 't_topic' t1 ON t.'id' = t1.'id' RIGHT JOIN 'abcv'…` |
| QueryTest.java#query24 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:105 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query25 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:110 | `SELECT 'id','create_time','update_time','create_by','update_by','deleted','title','content','url','s…` |
| QueryTest.java#query81 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Query/QueryTest.SubQueries.cs:115 | `SELECT COUNT(*) FROM (SELECT DISTINCT t.'id',t.'create_time',t.'update_time',t.'create_by',t.'update…` |
| QueryTest14.java#testDynamicTable | 待做 |  | `SELECT * FROM 'multi_hot' WHERE CAST('col8' AS DATETIME) > ? AND CAST('col9' AS DATETIME) < ?` |
| QueryTest19.java#mapInsert | 待做 |  | `INSERT INTO 'xxxxx' ('id','stars') VALUES (?,?)` |
| QueryTest19.java#mapUpdate | 待做 |  | `UPDATE 'xxxxx' SET 'stars' = ? WHERE 'id' = ?` |
| QueryTest27.java#testAAA2 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA4 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA42 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA43 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest27.java#testAAA45 | 待做 |  | `SELECT t.'id',t.'create_time',t.'username',t.'phone',t.'id_card',t.'address' FROM 'easy-query-test'.…` |
| QueryTest28.java#testQueryMap2 | 待做 |  | `SELECT 'id' FROM 't_topic' WHERE 'id' = ?` |
| QueryTest4.java#queryFirstSingle | 待做 |  | `SELECT 'id','create_time','username','phone','id_card','address' FROM 'easy-query-test'.'sys_123' WH…` |
| QueryTest5.java#joinTest1_1 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| QueryTest5.java#joinTest1_2 | 待做 |  | `SELECT SUM(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| QueryTest5.java#joinTest1_3 | 待做 |  | `SELECT AVG(t.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topic…` |
| QueryTest5.java#joinTest1_4 | 待做 |  | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_5 | 待做 |  | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_6 | 待做 |  | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_7 | 待做 |  | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_8 | 待做 |  | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTest1_9 | 待做 |  | `SELECT AVG(t2.'stars') FROM 't_topic' t LEFT JOIN 't_topic' t1 ON t.'id' = t1.'id' LEFT JOIN 't_topi…` |
| QueryTest5.java#joinTestx1 | 待做 |  | `SELECT t.'title',t.'content',t.'url',t.'star',t.'publish_time',t.'score',t.'status',t.'order',t.'is_…` |
| UpdateTest.java#mapUpdateTest1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:264 | `UPDATE 'aaa' SET 'name' = ? WHERE 'id' = ?` |
| UpdateTest.java#testColumnConfigure2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:441 | `UPDATE 'xxxxx' SET 'stars' = ifnull('stars',0)+? WHERE 'id' = ?` |
| UpdateTest.java#testColumnConfigure3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:450 | `UPDATE 'xxxxx' SET 'id' = ?,'stars' = ? WHERE 'id' = ?` |
| UpdateTest.java#updateTest26 | 待做 |  | `SELECT 'id','username',AES_DECRYPT(from_base64('phone'),?) AS 'phone','id_card','address','create_ti…` |
| UpdateTest.java#updateTest26_1 | 待做 |  | `SELECT 'id','username',AES_DECRYPT(from_base64('phone'),?) AS 'phone','id_card','address','create_ti…` |
| fork/live/TableNameMappingLiveTest.java#includeAndImplicitSubqueryReadArchiveTables | 待做 |  |  |
| fork/mapping/TableNameMappingSqlTest.java#emptyMappingChangesNothing | 待做 |  | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user'` |
| fork/mapping/TableNameMappingSqlTest.java#explicitAsTableWins | 待做 |  | `SELECT 'id','name','age','company_id','attrs','created_at' FROM 't_fork_user_2025'` |
| fork/mapping/TableNameMappingSqlTest.java#mainTableJoinAndSubqueriesFollowTheMapping | 待做 |  | `SELECT t.'id' AS 'value1',(SELECT COUNT(*) FROM 't_fork_post_archive' t3 WHERE t3.'user_id' = t.'id'…` |
| fork/mapping/TableNameMappingSqlTest.java#mappedTableNameIsReadable | 待做 |  |  |
| fork/mapping/TableNameMappingSqlTest.java#onlyMappedEntitiesChange | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'company_id',t.'attrs',t.'created_at' FROM 't_fork_user' t WHERE EX…` |
| fork/mapping/TableNameMappingSqlTest.java#scopesNestAndRestore | 待做 |  | `SELECT t.'id',t.'name',t.'age',t.'company_id',t.'attrs',t.'created_at' FROM 't_fork_user_other' t WH…` |

### 换 schema（asSchema）（`table.schema`，M7）

| 用例 | 状态 | C# 位置 / 说明 | 断言的 SQL（摘要） |
| --- | --- | --- | --- |
| DeleteTest.java#deletex2 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/DeleteTest.cs:138 | `UPDATE 'asv'.'t_blog' xx SET xx.'deleted' = ? WHERE xx.'deleted' = ? AND xx.'id' = ?` |
| InsertTest.java#insertEmpty | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:167 |  |
| InsertTest.java#insertTest3 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:31 | `INSERT INTO 'xxx'.'aaa' ('stars','title','create_time') VALUES (?,?,?)` |
| InsertTest.java#insertTest5 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:36 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','title','create_time') VALUES (?,?,?)` |
| InsertTest.java#insertTest6 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:42 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','create_time') VALUES (?,?)` |
| InsertTest.java#insertTest7 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:42 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','title','create_time') VALUES (?,?,?)` |
| InsertTest.java#insertTest8 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:42 | `INSERT INTO 'xxx'.'t_topic_autoaaa' ('stars','create_time') VALUES (?,?)` |
| InsertTest.java#insertTest8_1 | 已移植 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:54 | `INSERT IGNORE INTO 'xxx'.'t_topic_autoaaa' ('stars','create_time') VALUES (?,?)` |
| InsertTest.java#insertTest9 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/InsertTest.cs:59：Java 的 insertable(null) 串了一长串配置；C# 用空集合表达没有实体 |  |
| UpdateTest.java#updateTest16 | 偏离 | tests/EasyQuerySharp.Tests.Upstream/Write/UpdateTest.cs:140：Java 的 updatable(null) 串了一长串配置方法；C# 用空集合表达没有实体，配置方法只保留有对应的几个 |  |
| UpdateTest1.java#testUpdate1 | 待做 |  | `UPDATE '_abc'.'t_topic_abc' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate1_1 | 待做 |  | `UPDATE '_abc'.'t_topic_abc' SET 'title' = ? WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate1_2 | 待做 |  | `UPDATE '_abc'.'t_topic_abc' SET 'title' = CAST('stars' AS CHAR) WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate1_3 | 待做 |  | `UPDATE '_abc'.'t_topic_abc' SET 'title' = 'stars' WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate2 | 待做 |  | `UPDATE '_abc'.'t_topic_abc' SET 'stars' = ?,'title' = ?,'create_time' = ? WHERE 'id' = ?` |
| UpdateTest1.java#testUpdate3 | 待做 |  | `UPDATE '_abc'.'t_topic_abc' SET 'title' = ?,'stars' = ? WHERE 'id' = ? AND 'stars' = ?` |
| UpdateTest1.java#testUpdate4 | 待做 |  | `UPDATE '_abc'.'t_blog_abc' SET 'is_top' = (NOT ('is_top')) WHERE 'deleted' = ? AND 'id' = ?` |
| UpdateTest1.java#testUpdate5 | 待做 |  | `UPDATE '_abc'.'t_blog_abc' SET 'title' = (CASE WHEN 'title' = ? THEN ? ELSE ? END) WHERE 'deleted' =…` |
| pgsql/InsertTest.java#insertTest1 | 待做 |  | `INSERT INTO "xxx"."t_topic_autoaaa" ("stars","title","create_time") VALUES (?,?,?) ON CONFLICT DO NO…` |
