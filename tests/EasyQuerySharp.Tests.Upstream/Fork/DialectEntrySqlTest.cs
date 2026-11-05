namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：dialect/DialectEntrySqlTest.java、MySQLJsonSqlTest.java、PgSQLM4SqlTest.java。
/// Java 的 MySQL.json / MySQL.match / PgSQL.jsonb / array / fulltext 在 C# 是 <c>Sql.MySql.*</c> / <c>Sql.Pg.*</c>；
/// 用户自己的方言函数用 <c>[SqlFunction]</c> 声明，与内置入口同样能用在条件、排序与隐式子查询里。
/// </summary>
public sealed class DialectEntrySqlTest
{
    private const string UserColumns = "`id`,`name`,`age`,`company_id`,`attrs`,`created_at`";
    private const string AliasedUserColumns = "t.`id`,t.`name`,t.`age`,t.`company_id`,t.`attrs`,t.`created_at`";

    private static EasyDb MySql => SqlOnly.Db("mysql");

    private static EasyDb Pg => SqlOnly.Db("pgsql");

    [SqlFunction("SOUNDEX({0}) = SOUNDEX({1})")]
    private static bool SoundsLike(string? column, string word) => throw new InvalidOperationException("只能在查询里使用");

    [SqlFunction("CHAR_LENGTH({0})")]
    private static int CharLength(string? column) => throw new InvalidOperationException("只能在查询里使用");

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#mysqlJsonPredicatesInWhere")]
    public void MysqlJsonPredicatesInWhere() =>
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user` WHERE JSON_CONTAINS(`attrs`,?,?) AND ? MEMBER OF(JSON_EXTRACT(`attrs`,?)) AND JSON_OVERLAPS(`attrs`,?)",
            MySql.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).Contains("\"vip\"", "$.tags")
                                               && Sql.MySql.Json(u.Attrs).HasMember("java", "$.skills")
                                               && Sql.MySql.Json(u.Attrs).Overlaps("[1,2]")).ToSql());

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#mysqlPredicateRespectsOrGroup")]
    public void MysqlPredicateRespectsOrGroup() =>
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user` WHERE `name` = ? AND (JSON_CONTAINS(`attrs`,?) OR `age` > ?)",
            MySql.Query<ForkUser>().Where(u => u.Name == "a" && (Sql.MySql.Json(u.Attrs).Contains("1") || u.Age > 18)).ToSql());

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#mysqlPredicateInsideImplicitSubquery")]
    public void MysqlPredicateInsideImplicitSubquery() =>
        Assert.Equal($"SELECT {AliasedUserColumns} FROM `t_fork_user` t WHERE EXISTS(SELECT 1 FROM `t_fork_post` t1 WHERE t1.`user_id` = t.`id` AND ? MEMBER OF(t1.`title`) LIMIT 1)",
            MySql.Query<ForkUser>().Where(u => u.Posts!.Any(p => Sql.MySql.Json(p.Title).HasMember("x"))).ToSql());

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#mysqlFullTextAgainstAndScore")]
    public void MysqlFullTextAgainstAndScore() =>
        Assert.Equal("SELECT t.`id` AS `value1`,MATCH(t.`name`) AGAINST(? IN NATURAL LANGUAGE MODE) AS `value2` FROM `t_fork_user` t"
                     + " WHERE MATCH(t.`name`,t.`attrs`) AGAINST(? IN BOOLEAN MODE)"
                     + " ORDER BY MATCH(t.`name`,t.`attrs`) AGAINST(? IN NATURAL LANGUAGE MODE) DESC",
            MySql.Query<ForkUser>().Where(u => Sql.MySql.Match(u.Name, u.Attrs).Against("+easy -slow", MySqlMatchMode.Boolean))
                .OrderByDescending(u => Sql.MySql.Match(u.Name, u.Attrs).Score("easy"))
                .Select(u => new Draft<string?, double>(u.Id, Sql.MySql.Match(u.Name).Score("easy"))).ToSql());

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#pgJsonbPredicates")]
    public void PgJsonbPredicates() =>
        Assert.Equal("SELECT \"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\" FROM \"t_fork_user\""
                     + " WHERE (\"attrs\")::jsonb @> (?)::jsonb AND jsonb_exists((\"attrs\")::jsonb,?) AND jsonb_exists_any((\"attrs\")::jsonb,ARRAY[?,?]::text[])",
            Pg.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).Contains("{\"vip\":true}") && Sql.Pg.Jsonb(u.Attrs).HasKey("tags")
                                            && Sql.Pg.Jsonb(u.Attrs).HasAnyKey("a", "b")).ToSql());

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#wrongDialectFailsWhenTheExpressionIsBuilt")]
    public void WrongDialectFailsWhenTheExpressionIsBuilt()
    {
        Assert.Contains("Sql.MySql.Json", Assert.Throws<NotSupportedException>(() => Pg.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).Contains("1")).ToSql()).Message);
        Assert.Contains("Sql.Pg.Jsonb", Assert.Throws<NotSupportedException>(() => MySql.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).HasKey("a")).ToSql()).Message);
    }

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#userDefinedValueFunctionChainsLikeBuiltIns")]
    public void UserDefinedValueFunctionChainsLikeBuiltIns() =>
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user` WHERE CHAR_LENGTH(`name`) > ? ORDER BY CHAR_LENGTH(`name`) DESC",
            MySql.Query<ForkUser>().Where(u => CharLength(u.Name) > 3).OrderByDescending(u => CharLength(u.Name)).ToSql());

    [Fact, Upstream("fork/dialect/DialectEntrySqlTest.java#userDefinedFunctionHasTheSameShape")]
    public void UserDefinedFunctionHasTheSameShape() =>
        Assert.Equal($"SELECT {AliasedUserColumns} FROM `t_fork_user` t WHERE EXISTS(SELECT 1 FROM `t_fork_post` t1 WHERE t1.`user_id` = t.`id` AND SOUNDEX(t1.`title`) = SOUNDEX(?) LIMIT 1)",
            MySql.Query<ForkUser>().Where(u => u.Posts!.Any(p => SoundsLike(p.Title, "smith"))).ToSql());

    [Fact, Upstream("fork/dialect/MySQLJsonSqlTest.java#containsPath")]
    public void ContainsPath()
    {
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user` WHERE JSON_CONTAINS_PATH(`attrs`,'one',?,?)",
            MySql.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).ContainsPath("$.a", "$.b")).ToSql());
        Assert.Equal($"SELECT {UserColumns} FROM `t_fork_user` WHERE JSON_CONTAINS_PATH(`attrs`,'all',?)",
            MySql.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).ContainsAllPaths("$.a")).ToSql());
    }

    [Fact, Upstream("fork/dialect/MySQLJsonSqlTest.java#modificationsChainIntoUpdate")]
    public void ModificationsChainIntoUpdate() =>
        Assert.Equal("UPDATE `t_fork_user` SET `attrs` = JSON_REMOVE(JSON_SET(`attrs`,?,?),?,?) WHERE `id` = ?",
            MySql.Update<ForkUser>().Set(u => u.Attrs, u => Sql.MySql.Json(u.Attrs).Edit(e => e.Set("$.level", 3).Remove("$.old", "$.tmp")))
                .Where(u => u.Id == "u1").ToSql());

    [Fact, Upstream("fork/dialect/PgSQLM4SqlTest.java#jsonbEdit")]
    public void JsonbEdit() =>
        Assert.Equal("UPDATE \"t_fork_user\" SET \"attrs\" = (jsonb_set((\"attrs\")::jsonb,(?)::text[],(?)::jsonb,true) #- (?)::text[]) WHERE \"id\" = ?",
            Pg.Update<ForkUser>().Set(u => u.Attrs, u => Sql.Pg.Jsonb(u.Attrs).Edit(e => e.Set("a.b", "1").Remove("old")))
                .Where(u => u.Id == "u1").ToSql());

    [Fact, Upstream("fork/dialect/PgSQLM4SqlTest.java#arrayAndFullText")]
    public void ArrayAndFullText()
    {
        Assert.EndsWith("WHERE (? = ANY(\"user_id\") AND ? = ANY(\"user_id\"))", Pg.Query<ForkPost>().Where(p => Sql.Pg.Array(p.UserId).HasAll("a", "b")).ToSql());
        Assert.EndsWith("WHERE to_tsvector('english',\"title\") @@ websearch_to_tsquery('english',?)",
            Pg.Query<ForkPost>().Where(p => Sql.Pg.FullText(p.Title).Matches("english", "orm")).ToSql());
        // 配置名直接写进 SQL，只接受标识符
        Assert.ThrowsAny<ArgumentException>(() => Pg.Query<ForkPost>().Where(p => Sql.Pg.FullText(p.Title).Matches("x'; drop", "orm")).ToSql());
    }
}
