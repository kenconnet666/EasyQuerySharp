namespace EasyQuerySharp.Tests.Upstream.Functions;

/// <summary>
/// 对应 Java：GeneralFunctionsTest.java。按方言的用例写成 Theory，每种方言一行数据；只生成 SQL、不连库。
/// </summary>
public sealed class GeneralFunctionsTest
{
    [Theory, Upstream("GeneralFunctionsTest.java#countFilterMySQL")]
    [InlineData("mysql", "SELECT COUNT(t.`id`) AS `value1`,COUNT(DISTINCT t.`title`) AS `value2`,COUNT((CASE WHEN t.`title` <> ? THEN t.`title` ELSE NULL END)) AS `value3` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`content` LIKE ?", "恐怖(String),false(Boolean),%abc%(String)")]
    public void CountFilter(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Where(b => b.Content!.Contains("abc")).Select(b => new Draft<long, long, long>(Sql.Count(b.Id), Sql.CountDistinct(b.Title), Sql.Count(b.Title, b.Title != "恐怖"))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#countMySQL"), Upstream("GeneralFunctionsTest.java#countMSSQL"), Upstream("GeneralFunctionsTest.java#countPGSQL"), Upstream("GeneralFunctionsTest.java#countORACLE")]
    [InlineData("mysql", "SELECT COUNT(t.`id`) AS `value1`,COUNT(DISTINCT t.`id`) AS `value2` FROM `doc_bank_card` t", null)]
    [InlineData("mssql", "SELECT COUNT(t.[id]) AS [value1],COUNT(DISTINCT t.[id]) AS [value2] FROM [doc_bank_card] t", null)]
    [InlineData("pgsql", "SELECT COUNT(t.\"id\") AS \"value1\",COUNT(DISTINCT t.\"id\") AS \"value2\" FROM \"doc_bank_card\" t", null)]
    [InlineData("oracle", "SELECT COUNT(t.\"id\") AS \"value1\",COUNT(DISTINCT t.\"id\") AS \"value2\" FROM \"doc_bank_card\" t", null)]
    public void Count(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Select(b => new Draft<long, long>(Sql.Count(b.Id), Sql.CountDistinct(b.Id))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#equalsWithMySQL"), Upstream("GeneralFunctionsTest.java#equalsWithMSSQL"), Upstream("GeneralFunctionsTest.java#equalsWithPGSQL"), Upstream("GeneralFunctionsTest.java#equalsWithORACLE")]
    [InlineData("mysql", "SELECT (CASE t.`id` WHEN ? THEN 1 ELSE 0 END) AS `value1` FROM `doc_bank_card` t WHERE (CASE t.`code` WHEN ? THEN 1 ELSE 0 END) = ?", "123(String),myCode(String),false(Boolean)")]
    [InlineData("mssql", "SELECT (CASE t.[id] WHEN ? THEN 1 ELSE 0 END) AS [value1] FROM [doc_bank_card] t WHERE (CASE t.[code] WHEN ? THEN 1 ELSE 0 END) = ?", "123(String),myCode(String),false(Boolean)")]
    [InlineData("pgsql", "SELECT (CASE t.\"id\" WHEN ? THEN 1 ELSE 0 END) AS \"value1\" FROM \"doc_bank_card\" t WHERE (CASE t.\"code\" WHEN ? THEN 1 ELSE 0 END) = ?", "123(String),myCode(String),false(Boolean)")]
    [InlineData("oracle", "SELECT (CASE t.\"id\" WHEN ? THEN 1 ELSE 0 END) AS \"value1\" FROM \"doc_bank_card\" t WHERE (CASE t.\"code\" WHEN ? THEN 1 ELSE 0 END) = ?", "123(String),myCode(String),false(Boolean)")]
    public void EqualsWith(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => Sql.EqualsWith(b.Code, "myCode") == false).Select(b => new Draft<bool>(Sql.EqualsWith(b.Id, "123"))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#maxMinMySQL"), Upstream("GeneralFunctionsTest.java#maxMinMSSQL"), Upstream("GeneralFunctionsTest.java#maxMinPGSQL"), Upstream("GeneralFunctionsTest.java#maxMinORACLE")]
    [InlineData("mysql", "SELECT MAX(t.`id`) AS `value1`,MIN(t.`id`) AS `value2` FROM `doc_bank_card` t", null)]
    [InlineData("mssql", "SELECT MAX(t.[id]) AS [value1],MIN(t.[id]) AS [value2] FROM [doc_bank_card] t", null)]
    [InlineData("pgsql", "SELECT MAX(t.\"id\") AS \"value1\",MIN(t.\"id\") AS \"value2\" FROM \"doc_bank_card\" t", null)]
    [InlineData("oracle", "SELECT MAX(t.\"id\") AS \"value1\",MIN(t.\"id\") AS \"value2\" FROM \"doc_bank_card\" t", null)]
    public void MaxMin(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Select(b => new Draft<string, string>(Sql.Max(b.Id)!, Sql.Min(b.Id)!)).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#minMaxFilterMySQL")]
    [InlineData("mysql", "SELECT MAX(t.`score`) AS `value1`,MAX((CASE WHEN t.`title` <> ? THEN t.`score` ELSE NULL END)) AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`content` LIKE ?", "恐怖(String),false(Boolean),%abc%(String)")]
    public void MinMaxFilter(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().Where(b => b.Content!.Contains("abc")).Select(b => new Draft<decimal?, decimal?>(Sql.Max(b.Score), Sql.Max(b.Score, b.Title != "恐怖"))).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#nullOrDefaultMySQL"), Upstream("GeneralFunctionsTest.java#nullOrDefaultMSSQL"), Upstream("GeneralFunctionsTest.java#nullOrDefaultPGSQL"), Upstream("GeneralFunctionsTest.java#nullOrDefaultORACLE")]
    [InlineData("mysql", "SELECT `id`,`uid`,`code`,`type`,`bank_id` FROM `doc_bank_card` WHERE IFNULL(`code`,?) = ?", "noCode(String),123(String)")]
    [InlineData("mssql", "SELECT [id],[uid],[code],[type],[bank_id] FROM [doc_bank_card] WHERE ISNULL([code],?) = ?", "noCode(String),123(String)")]
    [InlineData("pgsql", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE COALESCE(\"code\",?) = ?", "noCode(String),123(String)")]
    [InlineData("oracle", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE NVL(\"code\",?) = ?", "noCode(String),123(String)")]
    public void NullOrDefault(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => (b.Code ?? "noCode") == "123").ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likeOracle")]
    [InlineData("oracle", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE INSTR(\"type\",?,1,1) > 0", "30%(String)")]
    public void LikeOracle(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.Contains("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likeOracle2")]
    [InlineData("oracle", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE INSTR(\"type\",?,1,1) = 1", "30%(String)")]
    public void LikeOracle2(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.StartsWith("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likeOracle3")]
    [InlineData("oracle", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE INSTR(\"type\",?,1,1) = (LENGTH(\"type\") - LENGTH(?) + 1)", "30%(String),30%(String)")]
    public void LikeOracle3(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.EndsWith("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likePgSQL")]
    [InlineData("pgsql", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE \"type\" LIKE CONCAT('%',(?)::TEXT,'%') ESCAPE '\\'", "30\\%(String)")]
    public void LikePgSql(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.Contains("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likePgSQL2")]
    [InlineData("pgsql", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE \"type\" LIKE CONCAT((?)::TEXT,'%') ESCAPE '\\'", "30\\%(String)")]
    public void LikePgSql2(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.StartsWith("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likePgSQL3")]
    [InlineData("pgsql", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE \"type\" LIKE CONCAT('%',(?)::TEXT) ESCAPE '\\'", "30\\%(String)")]
    public void LikePgSql3(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.EndsWith("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likeSQLite")]
    [InlineData("sqlite", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE INSTR(\"type\",?) > 0", "30%(String)")]
    public void LikeSqlite(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.Contains("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likeSQLite2")]
    [InlineData("sqlite", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE INSTR(\"type\",?) = 1", "30%(String)")]
    public void LikeSqlite2(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.StartsWith("30%")).ToStatement(), sql, parameters);
    }

    [Theory, Upstream("GeneralFunctionsTest.java#likeSQLite3")]
    [InlineData("sqlite", "SELECT \"id\",\"uid\",\"code\",\"type\",\"bank_id\" FROM \"doc_bank_card\" WHERE INSTR(\"type\",?) = (LENGTH(\"type\") - LENGTH(?) + 1)", "30%(String),30%(String)")]
    public void LikeSqlite3(string dialect, string sql, string? parameters)
    {
        var Db = SqlOnly.Db(dialect);
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().Where(b => b.Type!.EndsWith("30%")).ToStatement(), sql, parameters);
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey", Pending = true)]
    public Task MySQLInsertIgnore2UpdateKey()
    {
        // 期望 SQL：INSERT INTO `doc_bank_card` (`id`,`code`,`type`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `id` = VALUES(`id`)
        // 期望参数：123(String),code(String),123(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create2(listenerContextManager, new MySQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // try {
        //     DocBankCard docBankCard = new DocBankCard();
        //     docBankCard.setId("123");
        //     docBankCard.setCode("code");
        //     docBankCard.setType("123");
        // 
        //     easyEntityQuery.insertable(docBankCard).onConflictThen(null).executeRows();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("INSERT INTO `doc_bank_card` (`id`,`code`,`type`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `id` = VALUES(`id`)", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("123(String),code(String),123(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey2", Pending = true)]
    public Task MySQLInsertIgnore2UpdateKey2()
    {
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create2(listenerContextManager, new MySQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // String error=null;
        // try {
        // 
        //     TopicAuto entity = new TopicAuto();
        //     entity.setStars(123);
        //     entity.setTitle("123");
        // 
        //     easyEntityQuery.insertable(entity).onConflictThen(null).executeRows();
        // } catch (Exception ex) {
        //     error=ex.getMessage();
        // }
        // 
        // Assert.assertEquals("[MySQL INSERT IGNORE INTO TO DUPLICATE KEY UPDATE] No non-auto-increment primary key found.", error);
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#MySQLInsertIgnore2UpdateKey3", Pending = true)]
    public Task MySQLInsertIgnore2UpdateKey3()
    {
        // 期望 SQL：INSERT INTO `t_topic_auto` (`stars`,`title`) VALUES (?,?)
        // 期望参数：123(Integer),123(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create2(listenerContextManager, new MySQLDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // try {
        // 
        //     TopicAuto entity = new TopicAuto();
        //     entity.setStars(123);
        //     entity.setTitle("123");
        // 
        //     easyEntityQuery.insertable(entity).executeRows();
        // } catch (Exception ex) {
        // }
        // 
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("INSERT INTO `t_topic_auto` (`stars`,`title`) VALUES (?,?)", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("123(Integer),123(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#limitOrderTest1", Pending = true)]
    public Task LimitOrderTest1()
    {
        // 期望 SQL：SELECT t1."value1" AS "value1",t1."value2" AS "value2" FROM (SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t."id" AS "value1",COUNT(*) AS "value2" FROM "doc_bank_card" t WHERE t."type" = ? GROUP BY t."id") rt WHERE ROWNUM < 6) rt1 WHERE rt1."__rownum__" > 2) t1 ORDER BY t1."value2" ASC
        // 期望参数：123(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new OracleDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // 
        // try {
        // 
        //     List<Draft2<String, Long>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .where(bank_card -> {
        //                 bank_card.type().eq("123");
        //             }).groupBy(bank_card -> GroupKeys.of(bank_card.id()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        //                     group.count()
        //             ))
        //             .limit(2, 3)
        //             .orderBy(draft2 -> draft2.value2().asc())
        //             .toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT t1.\"value1\" AS \"value1\",t1.\"value2\" AS \"value2\" FROM (SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT t.\"id\" AS \"value1\",COUNT(*) AS \"value2\" FROM \"doc_bank_card\" t WHERE t.\"type\" = ? GROUP BY t.\"id\") rt WHERE ROWNUM < 6) rt1 WHERE rt1.\"__rownum__\" > 2) t1 ORDER BY t1.\"value2\" ASC", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("123(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#limitOrderTest2", Pending = true)]
    public Task LimitOrderTest2()
    {
        // 期望 SQL：SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS "__rownum__" FROM (SELECT t1."value1" AS "value1",t1."value2" AS "value2" FROM (SELECT t."id" AS "value1",COUNT(*) AS "value2" FROM "doc_bank_card" t WHERE t."type" = ? GROUP BY t."id") t1 ORDER BY t1."value2" ASC) rt WHERE ROWNUM < 6) rt1 WHERE rt1."__rownum__" > 2
        // 期望参数：123(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new OracleDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // 
        // try {
        // 
        //     List<Draft2<String, Long>> list = easyEntityQuery.queryable(DocBankCard.class)
        //             .where(bank_card -> {
        //                 bank_card.type().eq("123");
        //             }).groupBy(bank_card -> GroupKeys.of(bank_card.id()))
        //             .select(group -> Select.DRAFT.of(
        //                     group.key1(),
        //                     group.count()
        //             ))
        //             .orderBy(draft2 -> draft2.value2().asc())
        //             .limit(2, 3)
        //             .toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT t1.\"value1\" AS \"value1\",t1.\"value2\" AS \"value2\" FROM (SELECT t.\"id\" AS \"value1\",COUNT(*) AS \"value2\" FROM \"doc_bank_card\" t WHERE t.\"type\" = ? GROUP BY t.\"id\") t1 ORDER BY t1.\"value2\" ASC) rt WHERE ROWNUM < 6) rt1 WHERE rt1.\"__rownum__\" > 2", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("123(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#testDoubleConcat", Pending = true)]
    public Task TestDoubleConcat()
    {
        // 期望 SQL：SELECT "id","create_time","update_time","create_by","update_by","deleted","title","content","url","star","publish_time","score","status","order","is_top","top" FROM "t_blog" WHERE "deleted" = ? AND CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(?,"title"),?),"star"),?),"content"),?),?),?) = ?
        // 期望参数：false(Boolean),你好:(String),我叫(String),你好吗!我今年(String),岁了,(String),12(Integer),你呢(String),比较一下(String)
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new KingbaseESDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // try {
        // 
        //     List<BlogEntity> list = easyEntityQuery.queryable(BlogEntity.class)
        //             .where(t_blog -> {
        //                 t_blog.expression().stringFormat("你好:{0}我叫{1}你好吗!我今年{2}岁了,{3}你呢", t_blog.title(), t_blog.star(), t_blog.content(), 12)
        //                         .eq("比较一下");
        //             }).toList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(CONCAT(?,\"title\"),?),\"star\"),?),\"content\"),?),?),?) = ?", jdbcExecuteAfterArg.getBeforeArg().getSql());
        // Assert.assertEquals("false(Boolean),你好:(String),我叫(String),你好吗!我今年(String),岁了,(String),12(Integer),你呢(String),比较一下(String)", EasySQLUtil.sqlParameterToString(jdbcExecuteAfterArg.getBeforeArg().getSqlParameters().get(0)));
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#testKingbaseCte", Pending = true)]
    public Task TestKingbaseCte()
    {
        // 期望 SQL：WITH RECURSIVE "as_tree_cte" AS ( (SELECT 0+0 AS "deep",t1."id",t1."parent_id",t1."content",t1."user_id",t1."post_id",t1."create_at" FROM "t_comment" t1) UNION ALL (SELECT t2."deep" + 1 AS "deep",t3."id",t3."parent_id",t3."content",t3."user_id",t3."post_id",t3."create_at" FROM "as_tree_cte" t2 INNER JOIN "t_comment" t3 ON t3."id" = t2."parent_id") ) SELECT t."id",t."parent_id",t."content",t."user_id",t."post_id",t."create_at",t."deep" FROM "as_tree_cte" t
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new KingbaseESDatabaseConfiguration());
        // listenerContextManager.startCreateListen();
        // 
        // try {
        // 
        //     List<MyComment3> treeList = easyEntityQuery.queryable(Comment.class)
        //             .asTreeCTE(o -> {
        //                 o.setUp(true);
        //                 o.setDeepColumnName("deep");
        //             })
        //             .selectAutoInclude(MyComment3.class)
        //             .toTreeList();
        // } catch (Exception ignored) {
        // 
        // }
        // ListenerContext listenerContext = listenerContextManager.getListenContext();
        // listenerContextManager.clear();
        // 
        // Assert.assertNotNull(listenerContext.getJdbcExecuteAfterArg());
        // JdbcExecuteAfterArg jdbcExecuteAfterArg = listenerContext.getJdbcExecuteAfterArg();
        // Assert.assertEquals("WITH RECURSIVE \"as_tree_cte\" AS ( (SELECT 0+0 AS \"deep\",t1.\"id\",t1.\"parent_id\",t1.\"content\",t1.\"user_id\",t1.\"post_id\",t1.\"create_at\" FROM \"t_comment\" t1)  UNION ALL  (SELECT t2.\"deep\" + 1 AS \"deep\",t3.\"id\",t3.\"parent_id\",t3.\"content\",t3.\"user_id\",t3.\"post_id\",t3.\"create_at\" FROM \"as_tree_cte\" t2 INNER JOIN \"t_comment\" t3 ON t3.\"id\" = t2.\"parent_id\") ) SELECT t.\"id\",t.\"parent_id\",t.\"content\",t.\"user_id\",t.\"post_id\",t.\"create_at\",t.\"deep\" FROM \"as_tree_cte\" t", jdbcExecuteAfterArg.getBeforeArg().getSql());
        return Task.CompletedTask;
    }

    [Fact(Skip = "待移植")]
    [Upstream("GeneralFunctionsTest.java#unionSQLite", Pending = true)]
    public Task UnionSQLite()
    {
        // 期望 SQL：SELECT t3."id",t3."stars",t3."title",t3."create_time" FROM ( SELECT t."id",t."stars",t."title",t."create_time" FROM "t_topic" t WHERE t."id" IS NOT NULL UNION SELECT t1."id",t1."stars",t1."title",t1."create_time" FROM "t_topic" t1 WHERE t1."id" IS NULL UNION SELECT t2."id",t2."stars",t2."title",t2."create_time" FROM "t_topic" t2 WHERE t2."id" = ? ) t3
        // Java 原文：
        // ListenerContextManager listenerContextManager = new ListenerContextManager();
        // EasyEntityQuery easyEntityQuery = create(listenerContextManager, new SQLiteDatabaseConfiguration());
        // 
        // 
        // EasyQueryClient easyQueryClient = easyEntityQuery.getEasyQueryClient();
        // 
        // ClientQueryable<Topic> q1 = easyQueryClient
        //         .queryable(Topic.class)
        //         .where(o -> o.isNotNull("id"));
        // ClientQueryable<Topic> q2 = easyQueryClient
        //         .queryable(Topic.class)
        //         .where(o -> o.isNull("id"));
        // ClientQueryable<Topic> q3 = easyQueryClient
        //         .queryable(Topic.class)
        //         .where(o -> o.eq("id", "123"));
        // 
        // String sql = q1.union(q2, q3).toSQL();
        // System.out.println(sql);
        // Assert.assertEquals("SELECT t3.\"id\",t3.\"stars\",t3.\"title\",t3.\"create_time\" FROM ( SELECT t.\"id\",t.\"stars\",t.\"title\",t.\"create_time\" FROM \"t_topic\" t WHERE t.\"id\" IS NOT NULL  UNION  SELECT t1.\"id\",t1.\"stars\",t1.\"title\",t1.\"create_time\" FROM \"t_topic\" t1 WHERE t1.\"id\" IS NULL  UNION  SELECT t2.\"id\",t2.\"stars\",t2.\"title\",t2.\"create_time\" FROM \"t_topic\" t2 WHERE t2.\"id\" = ? ) t3", sql);
        return Task.CompletedTask;
    }
}
