namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>
/// DuckDB 方言自身的写法（Java 用例没覆盖到的部分，除行锁一条外是 C# 独有的）：参数名、通配符 LIKE、自增序列、upsert、IndexOf / PadLeft、Sql.Json。
/// 渲染的断言不连库，行为的断言连固件库。
/// </summary>
public sealed class DuckDbDialectTest(DuckDbFixture fixture) : DuckDbTest(fixture)
{
    private const string BlogColumns = "SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ?";

    private static EasyDb Duck => SqlOnly.Db("duckdb");

    [Fact, Upstream("ForUpdateDialectToSQLSmokeTest.java#duckDBForUpdateThrowsUnsupported")]
    public void ForUpdateThrowsUnsupported()
    {
        using var tx = Duck.BeginTransaction();
        var ex = Assert.Throws<NotSupportedException>(() => Duck.Query<DocBankCard>().Where(c => c.Id != null).ForUpdate().ToSql());
        Assert.Contains("DuckDB 不支持 FOR UPDATE", ex.Message);
    }

    // DuckDB.NET 认 $name，不认 @name
    [Fact]
    public void ParametersAreDollarNamed() =>
        Assert.EndsWith("WHERE \"deleted\" = $p0 AND \"id\" = $p1", Duck.Query<BlogEntity>().WhereById("1").ToStatement().CommandText);

    // 值里有 % 或 _ 时用 STRPOS 按字面比较（Java 的 DuckDBSQLLikeSQLFunction），其余仍是 LIKE
    [Fact]
    public void LikeWithWildcardValueUsesStrpos()
    {
        SqlOnly.AssertStatement(Duck.Query<BlogEntity>().Where(b => b.Title!.Contains("30%")).ToStatement(), BlogColumns + " AND STRPOS(\"title\",?) > 0", "false(Boolean),30%(String)");
        SqlOnly.AssertStatement(Duck.Query<BlogEntity>().Where(b => b.Title!.StartsWith("30%")).ToStatement(), BlogColumns + " AND STRPOS(\"title\",?) = 1", "false(Boolean),30%(String)");
        SqlOnly.AssertStatement(Duck.Query<BlogEntity>().Where(b => b.Title!.EndsWith("30%")).ToStatement(),
            BlogColumns + " AND STRPOS(\"title\",?) = (CHAR_LENGTH(\"title\") - CHAR_LENGTH(?) + 1)", "false(Boolean),30%(String),30%(String)");
        SqlOnly.AssertStatement(Duck.Query<BlogEntity>().Where(b => !b.Title!.Contains("30%")).ToStatement(), BlogColumns + " AND (NOT (STRPOS(\"title\",?) > 0))", "false(Boolean),30%(String)");
        SqlOnly.AssertStatement(Duck.Query<BlogEntity>().Where(b => b.Title!.Contains("abc")).ToStatement(), BlogColumns + " AND \"title\" LIKE ?", "false(Boolean),%abc%(String)");
    }

    [Fact]
    public async Task LikeWithWildcardValueMatchesLiterally()
    {
        await Db.Insert(new BlogEntity { Id = "like1", Title = "like_a%b", Deleted = false }, new BlogEntity { Id = "like2", Title = "likeXaYb", Deleted = false }).ExecuteAsync(Ct);
        try
        {
            async Task<List<string?>> Titles(System.Linq.Expressions.Expression<Func<BlogEntity, bool>> where) =>
                await Db.Query<BlogEntity>().Where(where).Where(b => b.Id!.StartsWith("like")).OrderBy(b => b.Id).Select(b => b.Title).ToListAsync(Ct);
            Assert.Equal(["like_a%b"], await Titles(b => b.Title!.Contains("_a%b")));
            Assert.Equal(["like_a%b"], await Titles(b => b.Title!.StartsWith("like_")));
            Assert.Equal(["like_a%b"], await Titles(b => b.Title!.EndsWith("a%b")));
            Assert.Equal(["likeXaYb"], await Titles(b => !b.Title!.Contains("_")));
        }
        finally
        {
            await Db.Delete<BlogEntity>().Where(b => b.Id == "like1" || b.Id == "like2").DisableLogicDelete().ExecuteAsync(Ct);
        }
    }

    // DuckDB 没有 IDENTITY：自增列用序列 + nextval 默认值，删表时一并删序列
    [Fact]
    public void AutoIncrementUsesSequence()
    {
        Assert.Equal(
            "CREATE SEQUENCE IF NOT EXISTS t_topic_auto_id_seq; CREATE TABLE IF NOT EXISTS \"t_topic_auto\" ( \n\"id\" INTEGER NOT NULL  DEFAULT nextval('t_topic_auto_id_seq'),\n\"stars\" INTEGER NULL ,\n\"title\" VARCHAR NULL ,\n\"create_time\" TIMESTAMP NULL , \n PRIMARY KEY (\"id\")\n);",
            Duck.CodeFirst.CreateTableSql(typeof(TopicAuto)));
        Assert.Equal("DROP TABLE IF EXISTS \"t_topic_auto\"; DROP SEQUENCE IF EXISTS t_topic_auto_id_seq", Duck.Options.Dialect.DropTableIfExistsSql(Duck.Metadata.Get<TopicAuto>()));
    }

    // 主键冲突时：忽略，或改为更新指定的列
    [Fact]
    public async Task OnConflictUpdateAndIgnore()
    {
        ALLTYPE Row(string value) => new() { Id = "upsert1", Value = value };
        await Db.Delete<ALLTYPE>().Where(a => a.Id == "upsert1").ExecuteAsync(Ct);
        await Db.Insert(Row("a")).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();
        await Db.Insert(Row("b")).OnConflictUpdate(a => a.Value).ExecuteAsync(Ct);
        Assert.EndsWith(" ON CONFLICT (\"id\") DO UPDATE SET \"value\" = EXCLUDED.\"value\"", listen.Last.Statement.Text);
        Assert.Equal("b", (await Db.Query<ALLTYPE>().WhereById("upsert1").SingleAsync(Ct)).Value);
        await Db.Insert(Row("c")).OnConflictIgnore().ExecuteAsync(Ct);
        Assert.Equal("b", (await Db.Query<ALLTYPE>().WhereById("upsert1").SingleAsync(Ct)).Value);
    }

    // IndexOf 是 0 起点，翻译成 STRPOS 再减 1；PadLeft 没给填充字符时补空格（DuckDB 的 LPAD 必须有三个参数）
    [Fact]
    public async Task StringFunctions()
    {
        SqlOnly.AssertStatement(Duck.Query<BlogEntity>().Where(b => b.Title!.IndexOf("x") > -1).ToStatement(), BlogColumns + " AND (STRPOS(\"title\",?) - 1) > ?", "false(Boolean),x(String),-1(Integer)");
        SqlOnly.AssertStatement(SqlOnly.Db("pgsql").Query<BlogEntity>().Where(b => b.Title!.IndexOf("x") > -1).ToStatement(),
            BlogColumns + " AND (STRPOS(\"title\",?) - 1) > ?", "false(Boolean),x(String),-1(Integer)");
        var row = await Db.Query<BlogEntity>().WhereById("5")
            .Select(b => new Draft<int, string, string>(b.Title!.IndexOf("tle"), b.Id!.PadLeft(3), b.Id!.PadRight(3, '*'))).SingleAsync(Ct);
        Assert.Equal(2, row.Value1);
        Assert.Equal("  5", row.Value2);
        Assert.Equal("5**", row.Value3);
    }

    // Sql.Json：-> / ->> 取值、json_array_length、json_exists
    [Fact]
    public async Task JsonFunctions()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync(typeof(DuckJsonDoc));
        await Db.CodeFirst.CreateTablesAsync(typeof(DuckJsonDoc));
        await Db.Insert(new DuckJsonDoc { Id = "1", Doc = "{\"name\":\"alice\",\"age\":30,\"tags\":[\"a\",\"b\"]}" }, new DuckJsonDoc { Id = "2", Doc = "{\"name\":\"bob\",\"tags\":[]}" }).ExecuteAsync(Ct);
        async Task<List<string?>> Ids(System.Linq.Expressions.Expression<Func<DuckJsonDoc, bool>> where) =>
            await Db.Query<DuckJsonDoc>().Where(where).OrderBy(d => d.Id).Select(d => d.Id).ToListAsync(Ct);
        Assert.Equal(["1"], await Ids(d => Sql.Json(d.Doc).Text("name") == "alice"));
        Assert.Equal(["1"], await Ids(d => Sql.Json(d.Doc).Get("tags").Text(0) == "a"));
        Assert.Equal(["1"], await Ids(d => Sql.Json(d.Doc).Get("tags").Length() == 2));
        Assert.Equal(["1"], await Ids(d => Sql.Json(d.Doc).ContainsKey("age")));
        Assert.Equal(["2"], await Ids(d => !Sql.Json(d.Doc).ContainsKey("age")));
    }
}
