using EasyQuerySharp.Tests.Upstream.Mysql8;
using EasyQuerySharp.Tests.Upstream.Pgsql;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：live 下的 MySQLDialectEntryLiveTest、MySQLJsonLiveTest、PgSQLDialectEntryLiveTest、PgSQLM4LiveTest。
/// Java 在 MySQL 8.4 与 8.0.18 上跑，这里跑本地必测的 MySQL 8.4 与 PG 17；建表用原生 DDL（JSON / jsonb 列、全文索引、数组列）。
/// </summary>
public sealed class DialectEntryLiveTest(Mysql8Fixture mysql, PgFixture pg)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<List<string?>> Ids(Querying.Query<ForkUser> query) =>
        (await query.Select(u => u.Id).ToListAsync(Ct)).Order(StringComparer.Ordinal).ToList();

    private async Task<EasyDb> MySqlSeeded()
    {
        var db = mysql.Db;
        foreach (var sql in new[]
                 {
                     "DROP TABLE IF EXISTS t_fork_post",
                     "DROP TABLE IF EXISTS t_fork_user",
                     "CREATE TABLE t_fork_user (id VARCHAR(32) PRIMARY KEY, name VARCHAR(100), age INT, company_id VARCHAR(32), attrs JSON, created_at DATETIME, FULLTEXT KEY ft_fork_user_name (name)) ENGINE=InnoDB",
                     "CREATE TABLE t_fork_post (id VARCHAR(32) PRIMARY KEY, user_id VARCHAR(32), title VARCHAR(200), amount DECIMAL(12,2))",
                     "INSERT INTO t_fork_user VALUES ('u1','easyquery fork',30,NULL,'{\"tags\":[\"vip\",\"java\"],\"skills\":[\"java\",\"sql\"]}',NOW()),"
                     + "('u2','hibernate classic',40,NULL,'{\"tags\":[\"normal\"],\"skills\":[\"go\"]}',NOW()),"
                     + "('u3','mybatis plus',20,NULL,'{\"tags\":[\"vip\"],\"skills\":[]}',NOW())",
                     "INSERT INTO t_fork_post VALUES ('p1','u1','[\"x\",\"y\"]',1),('p2','u2','[\"z\"]',2)",
                 })
            await db.ExecuteSqlAsync(sql, cancellationToken: Ct);
        return db;
    }

    private async Task<EasyDb> PgSeeded()
    {
        var db = pg.Db;
        foreach (var sql in new[]
                 {
                     "DROP TABLE IF EXISTS t_fork_post",
                     "DROP TABLE IF EXISTS t_fork_user",
                     "CREATE TABLE t_fork_user (id VARCHAR(32) PRIMARY KEY, name VARCHAR(100), age INT, company_id VARCHAR(32), attrs JSONB, created_at TIMESTAMP)",
                     "CREATE TABLE t_fork_post (id VARCHAR(32) PRIMARY KEY, user_id VARCHAR(32), title VARCHAR(200), amount DECIMAL(12,2))",
                     "INSERT INTO t_fork_user VALUES ('u1','easyquery',30,NULL,'{\"tags\":[\"vip\",\"java\"],\"skills\":[\"java\"]}',now()),"
                     + "('u2','hibernate',40,NULL,'{\"tags\":[\"normal\"],\"skills\":[\"go\"]}',now()),"
                     + "('u3','mybatis',20,NULL,'{\"tags\":[\"vip\"]}',now())",
                     "INSERT INTO t_fork_post VALUES ('p1','u1','{\"lang\":\"java\"}',1),('p2','u2','{\"lang\":\"go\"}',2)",
                 })
            await db.ExecuteSqlAsync(sql, cancellationToken: Ct);
        return db;
    }

    [Fact, Upstream("fork/live/MySQLDialectEntryLiveTest.java#jsonContainsWithPath")]
    public async Task MySqlJsonContainsWithPath()
    {
        var db = await MySqlSeeded();
        Assert.Equal(["u1", "u3"], await Ids(db.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).Contains("\"vip\"", "$.tags"))));
    }

    [Fact, Upstream("fork/live/MySQLDialectEntryLiveTest.java#memberOfAndOverlaps")]
    public async Task MySqlMemberOfAndOverlaps()
    {
        var db = await MySqlSeeded();
        Assert.Equal(["u1"], await Ids(db.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).HasMember("java", "$.skills"))));
        Assert.Equal(["u2"], await Ids(db.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).Overlaps("{\"tags\":[\"normal\"]}"))));
    }

    [Fact, Upstream("fork/live/MySQLDialectEntryLiveTest.java#insideImplicitSubquery")]
    public async Task MySqlInsideImplicitSubquery()
    {
        var db = await MySqlSeeded();
        Assert.Equal(["u1"], await Ids(db.Query<ForkUser>().Where(u => u.Posts!.Any(p => Sql.MySql.Json(p.Title).HasMember("x")))));
    }

    [Fact, Upstream("fork/live/MySQLDialectEntryLiveTest.java#fullTextAgainstAndScore")]
    public async Task MySqlFullTextAgainstAndScore()
    {
        var db = await MySqlSeeded();
        Assert.Equal(["u1"], await Ids(db.Query<ForkUser>().Where(u => Sql.MySql.Match(u.Name).Against("easyquery"))));
        var ranked = await db.Query<ForkUser>().OrderByDescending(u => Sql.MySql.Match(u.Name).Score("hibernate"))
            .Select(u => new Draft<string?, double>(u.Id, Sql.MySql.Match(u.Name).Score("hibernate"))).ToListAsync(Ct);
        Assert.Equal("u2", ranked[0].Value1);
    }

    [Fact, Upstream("fork/live/MySQLJsonLiveTest.java#pathPredicatesAndUpdates")]
    public async Task MySqlPathPredicatesAndUpdates()
    {
        var db = mysql.Db;
        await db.ExecuteSqlAsync("DROP TABLE IF EXISTS t_fork_user", cancellationToken: Ct);
        await db.ExecuteSqlAsync("CREATE TABLE t_fork_user (id VARCHAR(32) PRIMARY KEY, name VARCHAR(100), age INT, company_id VARCHAR(32), attrs JSON, created_at DATETIME)", cancellationToken: Ct);
        await db.ExecuteSqlAsync("INSERT INTO t_fork_user (id, attrs) VALUES ('u1', '{\"tags\":[\"a\"],\"level\":1,\"old\":true}'), ('u2', '{\"tags\":[]}')", cancellationToken: Ct);

        Assert.Equal(1, await db.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).ContainsPath("$.level", "$.nope")).CountAsync(Ct));
        Assert.Equal(0, await db.Query<ForkUser>().Where(u => Sql.MySql.Json(u.Attrs).ContainsAllPaths("$.level", "$.nope")).CountAsync(Ct));
        Assert.Equal(1, await db.Update<ForkUser>()
            .Set(u => u.Attrs, u => Sql.MySql.Json(u.Attrs).Edit(e => e.Set("$.level", 3).SetJson("$.vip", "true").ArrayAppend("$.tags", "b").Remove("$.old")))
            .Where(u => u.Id == "u1").ExecuteAsync(Ct));
        Assert.Equal(1, await db.Query<ForkUser>().Where(u => u.Id == "u1" && Sql.MySql.Json(u.Attrs).Contains("3", "$.level")
                                                             && Sql.MySql.Json(u.Attrs).Contains("true", "$.vip") && Sql.MySql.Json(u.Attrs).HasMember("b", "$.tags")).CountAsync(Ct));
        Assert.Equal(0, await db.Query<ForkUser>().Where(u => u.Id == "u1" && Sql.MySql.Json(u.Attrs).ContainsPath("$.old")).CountAsync(Ct));
    }

    [Fact, Upstream("fork/live/PgSQLDialectEntryLiveTest.java#containsAndContainedBy")]
    public async Task PgContainsAndContainedBy()
    {
        var db = await PgSeeded();
        Assert.Equal(["u1", "u3"], await Ids(db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).Contains("{\"tags\":[\"vip\"]}"))));
        Assert.Equal(["u3"], await Ids(db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).ContainedBy("{\"tags\":[\"vip\"],\"x\":1}"))));
    }

    [Fact, Upstream("fork/live/PgSQLDialectEntryLiveTest.java#keyExistence")]
    public async Task PgKeyExistence()
    {
        var db = await PgSeeded();
        Assert.Equal(["u1", "u2"], await Ids(db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).HasKey("skills"))));
        Assert.Equal(["u1", "u2", "u3"], await Ids(db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).HasAnyKey("nope", "tags"))));
        Assert.Empty(await Ids(db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).HasAllKeys("tags", "nope"))));
    }

    [Fact, Upstream("fork/live/PgSQLDialectEntryLiveTest.java#insideImplicitSubquery")]
    public async Task PgInsideImplicitSubquery()
    {
        var db = await PgSeeded();
        Assert.Equal(["u1"], await Ids(db.Query<ForkUser>().Where(u => u.Posts!.Any(p => Sql.Pg.Jsonb(p.Title).Contains("{\"lang\":\"java\"}")))));
    }

    [Fact, Upstream("fork/live/PgSQLM4LiveTest.java#jsonbEditRoundTrip")]
    public async Task PgJsonbEditRoundTrip()
    {
        var db = pg.Db;
        await db.ExecuteSqlAsync("DROP TABLE IF EXISTS t_fork_user", cancellationToken: Ct);
        await db.ExecuteSqlAsync("CREATE TABLE t_fork_user (id VARCHAR(32) PRIMARY KEY, name VARCHAR(100), age INT, company_id VARCHAR(32), attrs JSONB, created_at TIMESTAMP)", cancellationToken: Ct);
        await db.ExecuteSqlAsync("INSERT INTO t_fork_user (id, attrs) VALUES ('u1', '{\"level\":1,\"old\":true,\"tags\":[\"a\"]}')", cancellationToken: Ct);

        Assert.Equal(1, await db.Update<ForkUser>()
            .Set(u => u.Attrs, u => Sql.Pg.Jsonb(u.Attrs).Edit(e => e.Set("level", "3").Set("tags.1", "\"b\"").Remove("old").Merge("{\"vip\":true}")))
            .Where(u => u.Id == "u1").ExecuteAsync(Ct));
        Assert.Equal(1, await db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).Contains("{\"level\":3,\"vip\":true,\"tags\":[\"a\",\"b\"]}")).CountAsync(Ct));
        Assert.Equal(0, await db.Query<ForkUser>().Where(u => Sql.Pg.Jsonb(u.Attrs).HasKey("old")).CountAsync(Ct));
    }

    [Fact, Upstream("fork/live/PgSQLM4LiveTest.java#arrayPredicatesAndFullText")]
    public async Task PgArrayPredicatesAndFullText()
    {
        var db = pg.Db;
        await db.ExecuteSqlAsync("DROP TABLE IF EXISTS t_fork_post", cancellationToken: Ct);
        await db.ExecuteSqlAsync("CREATE TABLE t_fork_post (id VARCHAR(32) PRIMARY KEY, user_id TEXT[], title TEXT, amount DECIMAL(12,2))", cancellationToken: Ct);
        await db.ExecuteSqlAsync("INSERT INTO t_fork_post VALUES ('p1', '{a,b}', 'easy query makes orm simple', 1), ('p2', '{b}', 'plain jdbc is verbose', 2), ('p3', '{}', 'easy query and jdbc together', 3)",
            cancellationToken: Ct);

        Assert.Equal(2, await db.Query<ForkPost>().Where(p => Sql.Pg.Array(p.UserId).Has("b")).CountAsync(Ct));
        Assert.Equal(1, await db.Query<ForkPost>().Where(p => Sql.Pg.Array(p.UserId).HasAll("a", "b")).CountAsync(Ct));
        Assert.Equal(2, await db.Query<ForkPost>().Where(p => Sql.Pg.Array(p.UserId).HasAny("a", "z", "b")).CountAsync(Ct));
        Assert.Equal(1, await db.Query<ForkPost>().Where(p => Sql.Pg.Array(p.UserId).Length() == 0).CountAsync(Ct));
        Assert.Equal(1, await db.Query<ForkPost>().Where(p => Sql.Pg.FullText(p.Title).Matches("easy query -jdbc")).CountAsync(Ct));
        var ranked = await db.Query<ForkPost>().Where(p => Sql.Pg.FullText(p.Title).Matches("easy or jdbc")).OrderBy(p => p.Id)
            .Select(p => new Draft<string?, double>(p.Id, Sql.Pg.FullText(p.Title).Rank("jdbc"))).ToListAsync(Ct);
        Assert.Equal(3, ranked.Count);
        Assert.True(ranked[1].Value2 > ranked[0].Value2);
    }
}
