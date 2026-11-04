using EasyQuerySharp.Tests.Upstream.Doc.Entities;
using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：StringFunctionsTest.java。Java 按方言各写一个用例（xxxMySQL / xxxMSSQL / xxxPGSQL / xxxORACLE），
/// 这里每种函数一个 Theory，每种方言一行数据；只生成 SQL、不连库（对应 Java 的 EmptyDataSource）。
/// </summary>
public sealed class StringFunctionsTest
{
    [Theory, Upstream("StringFunctionsTest.java#compareToMySQL"), Upstream("StringFunctionsTest.java#compareToMSSQL"), Upstream("StringFunctionsTest.java#compareToPGSQL"), Upstream("StringFunctionsTest.java#compareToORACLE")]
    [InlineData("mysql", "SELECT STRCMP(t.`code`,t.`type`) FROM `doc_bank_card` t WHERE STRCMP(t.`code`,?) = ?", "1(String),1(Integer)")]
    [InlineData("mssql", "SELECT (CASE WHEN t.[code] = t.[type] THEN 0 WHEN t.[code] > t.[type] THEN 1 ELSE -1 END) FROM [doc_bank_card] t WHERE (CASE WHEN t.[code] = ? THEN 0 WHEN t.[code] > ? THEN 1 ELSE -1 END) = ?", "1(String),1(String),1(Integer)")]
    [InlineData("pgsql", "SELECT (CASE WHEN t.\"code\" = t.\"type\" THEN 0 WHEN t.\"code\" > t.\"type\" THEN 1 ELSE -1 END) FROM \"doc_bank_card\" t WHERE (CASE WHEN t.\"code\" = ? THEN 0 WHEN t.\"code\" > ? THEN 1 ELSE -1 END) = ?", "1(String),1(String),1(Integer)")]
    [InlineData("oracle", "SELECT (CASE WHEN t.\"code\" = t.\"type\" THEN 0 WHEN t.\"code\" > t.\"type\" THEN 1 ELSE -1 END) FROM \"doc_bank_card\" t WHERE (CASE WHEN t.\"code\" = ? THEN 0 WHEN t.\"code\" > ? THEN 1 ELSE -1 END) = ?", "1(String),1(String),1(Integer)")]
    public void CompareTo(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Code!.CompareTo("1") == 1).Select(b => b.Code!.CompareTo(b.Type)).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#concatMySQL"), Upstream("StringFunctionsTest.java#concatMSSQL"), Upstream("StringFunctionsTest.java#concatPGSQL"), Upstream("StringFunctionsTest.java#concatORACLE")]
    [InlineData("mysql", "SELECT CONCAT(?,t.`code`,?,t.`type`) FROM `doc_bank_card` t WHERE CONCAT(t.`code`,?) = ? AND CONCAT(t.`code`,t.`type`) = ? AND CONCAT(?,t.`code`) = ? AND CONCAT(?,t.`code`,?,t.`type`) = ?", "这张银行卡编号:(String),,类型为:(String),nextCode(String),aaaa(String),123(String),这张银行卡编号:(String),123(String),这张银行卡编号:(String),,类型为:(String),这张银行卡编号:123,类型为:456(String)")]
    [InlineData("mssql", "SELECT CONCAT(?,t.[code],?,t.[type]) FROM [doc_bank_card] t WHERE CONCAT(t.[code],?) = ? AND CONCAT(t.[code],t.[type]) = ? AND CONCAT(?,t.[code]) = ? AND CONCAT(?,t.[code],?,t.[type]) = ?", "这张银行卡编号:(String),,类型为:(String),nextCode(String),aaaa(String),123(String),这张银行卡编号:(String),123(String),这张银行卡编号:(String),,类型为:(String),这张银行卡编号:123,类型为:456(String)")]
    [InlineData("pgsql", "SELECT CONCAT(?,t.\"code\",?,t.\"type\") FROM \"doc_bank_card\" t WHERE CONCAT(t.\"code\",?) = ? AND CONCAT(t.\"code\",t.\"type\") = ? AND CONCAT(?,t.\"code\") = ? AND CONCAT(?,t.\"code\",?,t.\"type\") = ?", "这张银行卡编号:(String),,类型为:(String),nextCode(String),aaaa(String),123(String),这张银行卡编号:(String),123(String),这张银行卡编号:(String),,类型为:(String),这张银行卡编号:123,类型为:456(String)")]
    [InlineData("oracle", "SELECT ? || t.\"code\" || ? || t.\"type\" FROM \"doc_bank_card\" t WHERE t.\"code\" || ? = ? AND t.\"code\" || t.\"type\" = ? AND ? || t.\"code\" = ? AND ? || t.\"code\" || ? || t.\"type\" = ?", "这张银行卡编号:(String),,类型为:(String),nextCode(String),aaaa(String),123(String),这张银行卡编号:(String),123(String),这张银行卡编号:(String),,类型为:(String),这张银行卡编号:123,类型为:456(String)")]
    public void Concat(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Code + "nextCode" == "aaaa" && b.Code + b.Type == "123" && "这张银行卡编号:" + b.Code == "123" && string.Format("这张银行卡编号:{0},类型为:{1}", b.Code, b.Type) == "这张银行卡编号:123,类型为:456").Select(b => $"这张银行卡编号:{b.Code},类型为:{b.Type}").ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#leftRightPadMySQL"), Upstream("StringFunctionsTest.java#leftRightPadMSSQL"), Upstream("StringFunctionsTest.java#leftRightPadPGSQL"), Upstream("StringFunctionsTest.java#leftRightPadORACLE")]
    [InlineData("mysql", "SELECT `id`,`uid`,`code`,`type`,`bank_id` FROM `doc_bank_card` WHERE LPAD(`code`, 4) = ? AND LPAD(`code`, 4, ?) = ? AND RPAD(`code`, 4) = ? AND RPAD(`code`, 4, ?) = ?", "123(String), (String),123(String),123(String), (String),123(String)")]
    [InlineData("mssql", "SELECT [id],[uid],[code],[type],[bank_id] FROM [doc_bank_card] WHERE LPAD([code], 4) = ? AND LPAD([code], 4, ?) = ? AND RPAD([code], 4) = ? AND RPAD([code], 4, ?) = ?", "123(String), (String),123(String),123(String), (String),123(String)")]
    [InlineData("pgsql", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE LPAD(\"code\", 4) = ? AND LPAD(\"code\", 4, ?) = ? AND RPAD(\"code\", 4) = ? AND RPAD(\"code\", 4, ?) = ?", "123(String), (String),123(String),123(String), (String),123(String)")]
    [InlineData("oracle", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE LPAD(\"code\", 4, ' ') = ? AND LPAD(\"code\", 4, ?) = ? AND RPAD(\"code\", 4, ' ') = ? AND RPAD(\"code\", 4, ?) = ?", "123(String), (String),123(String),123(String), (String),123(String)")]
    public void LeftRightPad(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Code!.PadLeft(4) == "123" && b.Code.PadLeft(4, ' ') == "123" && b.Code.PadRight(4) == "123" && b.Code.PadRight(4, ' ') == "123").ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#lengthMySQL"), Upstream("StringFunctionsTest.java#lengthMSSQL"), Upstream("StringFunctionsTest.java#lengthPGSQL"), Upstream("StringFunctionsTest.java#lengthORACLE")]
    [InlineData("mysql", "SELECT CHAR_LENGTH(t.`code`) FROM `doc_bank_card` t WHERE CHAR_LENGTH(t.`code`) = ?", "2(Integer)")]
    [InlineData("mssql", "SELECT LEN(t.[code]) FROM [doc_bank_card] t WHERE LEN(t.[code]) = ?", "2(Integer)")]
    [InlineData("pgsql", "SELECT CHAR_LENGTH(t.\"code\") FROM \"doc_bank_card\" t WHERE CHAR_LENGTH(t.\"code\") = ?", "2(Integer)")]
    [InlineData("oracle", "SELECT LENGTH(t.\"code\") FROM \"doc_bank_card\" t WHERE LENGTH(t.\"code\") = ?", "2(Integer)")]
    public void Length(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Code!.Length == 2).Select(b => b.Code!.Length).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#lowerUpperMySQL"), Upstream("StringFunctionsTest.java#lowerUpperMSSQL"), Upstream("StringFunctionsTest.java#lowerUpperPGSQL"), Upstream("StringFunctionsTest.java#lowerUpperORACLE")]
    [InlineData("mysql", "SELECT LOWER(t.`id`) AS `value1`,UPPER(t.`id`) AS `value2` FROM `doc_bank_card` t", null)]
    [InlineData("mssql", "SELECT LOWER(t.[id]) AS [value1],UPPER(t.[id]) AS [value2] FROM [doc_bank_card] t", null)]
    [InlineData("pgsql", "SELECT LOWER(t.\"id\") AS \"value1\",UPPER(t.\"id\") AS \"value2\" FROM \"doc_bank_card\" t", null)]
    [InlineData("oracle", "SELECT LOWER(t.\"id\") AS \"value1\",UPPER(t.\"id\") AS \"value2\" FROM \"doc_bank_card\" t", null)]
    public void LowerUpper(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Select(b => new Draft<string, string>(b.Id!.ToLower(), b.Id.ToUpper())).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#replaceMySQL"), Upstream("StringFunctionsTest.java#replaceMSSQL"), Upstream("StringFunctionsTest.java#replacePGSQL"), Upstream("StringFunctionsTest.java#replaceORACLE")]
    [InlineData("mysql", "SELECT REPLACE(t.`id`,?,?) AS `value1` FROM `doc_bank_card` t WHERE REPLACE(t.`code`,?,?) = ?", "oldValue(String),newValue(String),旧值(String),新值(String),123(String)")]
    [InlineData("mssql", "SELECT REPLACE(t.[id],?,?) AS [value1] FROM [doc_bank_card] t WHERE REPLACE(t.[code],?,?) = ?", "oldValue(String),newValue(String),旧值(String),新值(String),123(String)")]
    [InlineData("pgsql", "SELECT REPLACE(t.\"id\",?,?) AS \"value1\" FROM \"doc_bank_card\" t WHERE REPLACE(t.\"code\",?,?) = ?", "oldValue(String),newValue(String),旧值(String),新值(String),123(String)")]
    [InlineData("oracle", "SELECT REPLACE(t.\"id\",?,?) AS \"value1\" FROM \"doc_bank_card\" t WHERE REPLACE(t.\"code\",?,?) = ?", "oldValue(String),newValue(String),旧值(String),新值(String),123(String)")]
    public void Replace(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Code!.Replace("旧值", "新值") == "123").Select(b => new Draft<string>(b.Id!.Replace("oldValue", "newValue"))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#subStringMySQL"), Upstream("StringFunctionsTest.java#subStringMSSQL"), Upstream("StringFunctionsTest.java#subStringPGSQL"), Upstream("StringFunctionsTest.java#subStringORACLE")]
    [InlineData("mysql", "SELECT SUBSTR(t.`id`,1,2) AS `value1`,SUBSTR(t.`id`,1,t.`stars`) AS `value2` FROM `t_topic` t", null)]
    [InlineData("mssql", "SELECT SUBSTRING(t.[id],1,2) AS [value1],SUBSTRING(t.[id],1,t.[stars]) AS [value2] FROM [t_topic] t", null)]
    [InlineData("pgsql", "SELECT SUBSTR(t.\"id\",1,2) AS \"value1\",SUBSTR(t.\"id\",1,t.\"stars\") AS \"value2\" FROM \"t_topic\" t", null)]
    [InlineData("oracle", "SELECT SUBSTR(t.\"id\",1,2) AS \"value1\",SUBSTR(t.\"id\",1,t.\"stars\") AS \"value2\" FROM \"t_topic\" t", null)]
    public void SubString(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<Topic>().Select(t => new Draft<string, string>(t.Id!.Substring(0, 2), t.Id.Substring(0, (int)t.Stars!))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("StringFunctionsTest.java#trimMySQL"), Upstream("StringFunctionsTest.java#trimMSSQL"), Upstream("StringFunctionsTest.java#trimPGSQL"), Upstream("StringFunctionsTest.java#trimORACLE")]
    [InlineData("mysql", "SELECT TRIM(t.`id`) AS `value1` FROM `doc_bank_card` t WHERE TRIM(t.`code`) = ? AND LTRIM(t.`code`) = ? AND RTRIM(t.`code`) = ?", "123(String),123(String),123(String)")]
    [InlineData("mssql", "SELECT LTRIM(RTRIM(t.[id])) AS [value1] FROM [doc_bank_card] t WHERE LTRIM(RTRIM(t.[code])) = ? AND LTRIM(t.[code]) = ? AND RTRIM(t.[code]) = ?", "123(String),123(String),123(String)")]
    [InlineData("pgsql", "SELECT TRIM(t.\"id\") AS \"value1\" FROM \"doc_bank_card\" t WHERE TRIM(t.\"code\") = ? AND LTRIM(t.\"code\") = ? AND RTRIM(t.\"code\") = ?", "123(String),123(String),123(String)")]
    [InlineData("oracle", "SELECT TRIM(t.\"id\") AS \"value1\" FROM \"doc_bank_card\" t WHERE TRIM(t.\"code\") = ? AND LTRIM(t.\"code\") = ? AND RTRIM(t.\"code\") = ?", "123(String),123(String),123(String)")]
    public void Trim(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Code!.Trim() == "123" && b.Code.TrimStart() == "123" && b.Code.TrimEnd() == "123").Select(b => new Draft<string>(b.Id!.Trim())).ToStatement(), sql, parameters);
    }

    [Fact(Skip = "待移植：需要 GroupBy 与导航（M3 / M4）")]
    [Upstream("StringFunctionsTest.java#joining2MySQL", Pending = true)]
    public Task Joining2MySQL()
    {
        // 期望 SQL：SELECT t.`code` AS `value1`,GROUP_CONCAT(DISTINCT (CASE WHEN t.`type` = ? THEN t1.`name` ELSE NULL END) SEPARATOR ?) AS `value2`,GROUP_CONCAT(DISTINCT t.`bank_id` SEPARATOR ?) AS `value3`,GROUP_CONCAT(DISTINCT (CASE WHEN t.`type` = ? THEN t.`bank_id` ELSE NULL END) SEPARATOR ?) AS `value4` FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid` GROUP BY t.`code`
        // 期望参数：储蓄卡(String),,(String),,(String),储蓄卡(String),,(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new MySQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // try {
        // 
        //     List<Draft4<String, String, String, String>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .groupBy(bank_card -> GroupKeys.of(bank_card.code()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        // 
        //                     group.where(o -> o.type().eq("储蓄卡")).distinct().joining(x -> x.user().name(), ","),
        // 
        //                     group.groupTable().bankId().joining(",",true),
        //                     group.groupTable().bankId().joining(",",true).filter(() -> group.groupTable().type().eq("储蓄卡"))
        //             )).toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT t.`code` AS `value1`,GROUP_CONCAT(DISTINCT (CASE WHEN t.`type` = ? THEN t1.`name` ELSE NULL END) SEPARATOR ?) AS `value2`,GROUP_CONCAT(DISTINCT t.`bank_id` SEPARATOR ?) AS `value3`,GROUP_CONCAT(DISTINCT (CASE WHEN t.`type` = ? THEN t.`bank_id` ELSE NULL END) SEPARATOR ?) AS `value4` FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid` GROUP BY t.`code`", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("储蓄卡(String),,(String),,(String),储蓄卡(String),,(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植：需要 GroupBy 与导航（M3 / M4）")]
    [Upstream("StringFunctionsTest.java#joiningMSSQL", Pending = true)]
    public Task JoiningMSSQL()
    {
        // 期望 SQL：SELECT t.[code] AS [value1],STRING_AGG(DISTINCT (CASE WHEN t.[type] = ? THEN t1.[name] ELSE NULL END), ?) AS [value2],STRING_AGG(DISTINCT t.[bank_id], ?) AS [value3] FROM [doc_bank_card] t LEFT JOIN [doc_user] t1 ON t1.[id] = t.[uid] GROUP BY t.[code]
        // 期望参数：储蓄卡(String),,(String),,(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new MsSQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // 
        // try {
        // 
        //     List<Draft3<String, String, String>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .groupBy(bank_card -> GroupKeys.of(bank_card.code()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        // 
        //                     group.where(o -> o.type().eq("储蓄卡")).distinct().joining(x -> x.user().name(), ","),
        // 
        //                     group.groupTable().bankId().joining(",",true)
        //             )).toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT t.[code] AS [value1],STRING_AGG(DISTINCT (CASE WHEN t.[type] = ? THEN t1.[name] ELSE NULL END), ?) AS [value2],STRING_AGG(DISTINCT t.[bank_id], ?) AS [value3] FROM [doc_bank_card] t LEFT JOIN [doc_user] t1 ON t1.[id] = t.[uid] GROUP BY t.[code]", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("储蓄卡(String),,(String),,(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植：需要 GroupBy 与导航（M3 / M4）")]
    [Upstream("StringFunctionsTest.java#joiningMySQL", Pending = true)]
    public Task JoiningMySQL()
    {
        // 期望 SQL：SELECT t.`code` AS `value1`,GROUP_CONCAT(DISTINCT (CASE WHEN t.`type` = ? THEN t1.`name` ELSE NULL END) SEPARATOR ?) AS `value2`,GROUP_CONCAT(DISTINCT t.`bank_id` SEPARATOR ?) AS `value3` FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid` GROUP BY t.`code`
        // 期望参数：储蓄卡(String),,(String),,(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new MySQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // try {
        // 
        //     List<Draft3<String, String, String>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .groupBy(bank_card -> GroupKeys.of(bank_card.code()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        // 
        //                     group.where(o -> o.type().eq("储蓄卡")).distinct().joining(x -> x.user().name(), ","),
        // 
        //                     group.groupTable().bankId().joining(",",true)
        //             )).toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT t.`code` AS `value1`,GROUP_CONCAT(DISTINCT (CASE WHEN t.`type` = ? THEN t1.`name` ELSE NULL END) SEPARATOR ?) AS `value2`,GROUP_CONCAT(DISTINCT t.`bank_id` SEPARATOR ?) AS `value3` FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid` GROUP BY t.`code`", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("储蓄卡(String),,(String),,(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植：需要 GroupBy 与导航（M3 / M4）")]
    [Upstream("StringFunctionsTest.java#joiningORACLE", Pending = true)]
    public Task JoiningORACLE()
    {
        // 期望 SQL：SELECT t."code" AS "value1",LISTAGG(DISTINCT TO_CHAR((CASE WHEN t."type" = ? THEN t1."name" ELSE NULL END)), ?) WITHIN GROUP(ORDER BY 1) AS "value2",LISTAGG(DISTINCT TO_CHAR(t."bank_id"), ?) WITHIN GROUP(ORDER BY 1) AS "value3" FROM "doc_bank_card" t LEFT JOIN "doc_user" t1 ON t1."id" = t."uid" GROUP BY t."code"
        // 期望参数：储蓄卡(String),,(String),,(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new OracleDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // 
        // try {
        // 
        //     List<Draft3<String, String, String>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .groupBy(bank_card -> GroupKeys.of(bank_card.code()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        // 
        //                     group.where(o -> o.type().eq("储蓄卡")).distinct().joining(x -> x.user().name(), ","),
        // 
        //                     group.groupTable().bankId().joining(",",true)
        //             )).toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT t.\"code\" AS \"value1\",LISTAGG(DISTINCT TO_CHAR((CASE WHEN t.\"type\" = ? THEN t1.\"name\" ELSE NULL END)), ?) WITHIN GROUP(ORDER BY 1) AS \"value2\",LISTAGG(DISTINCT TO_CHAR(t.\"bank_id\"), ?) WITHIN GROUP(ORDER BY 1) AS \"value3\" FROM \"doc_bank_card\" t LEFT JOIN \"doc_user\" t1 ON t1.\"id\" = t.\"uid\" GROUP BY t.\"code\"", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("储蓄卡(String),,(String),,(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植：需要 GroupBy 与导航（M3 / M4）")]
    [Upstream("StringFunctionsTest.java#joiningPGSQL", Pending = true)]
    public Task JoiningPGSQL()
    {
        // 期望 SQL：SELECT t."code" AS "value1",STRING_AGG(DISTINCT ((CASE WHEN t."type" = ? THEN t1."name" ELSE NULL END))::TEXT, ?) AS "value2",STRING_AGG(DISTINCT (t."bank_id")::TEXT, ?) AS "value3" FROM "doc_bank_card" t LEFT JOIN "doc_user" t1 ON t1."id" = t."uid" GROUP BY t."code"
        // 期望参数：储蓄卡(String),,(String),,(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new PgSQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // 
        // try {
        // 
        //     List<Draft3<String, String, String>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .groupBy(bank_card -> GroupKeys.of(bank_card.code()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        // 
        //                     group.where(o -> o.type().eq("储蓄卡")).distinct().joining(x -> x.user().name(), ","),
        // 
        //                     group.groupTable().bankId().joining(",",true)
        //             )).toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT t.\"code\" AS \"value1\",STRING_AGG(DISTINCT ((CASE WHEN t.\"type\" = ? THEN t1.\"name\" ELSE NULL END))::TEXT, ?) AS \"value2\",STRING_AGG(DISTINCT (t.\"bank_id\")::TEXT, ?) AS \"value3\" FROM \"doc_bank_card\" t LEFT JOIN \"doc_user\" t1 ON t1.\"id\" = t.\"uid\" GROUP BY t.\"code\"", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("储蓄卡(String),,(String),,(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }
}
