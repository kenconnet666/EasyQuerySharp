namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/M8Test1.java 的 testRelationColumns / testRelationColumns1：导航的键是逗号列表
/// （Java 的 relationPropertyStrategy = "find_in_set"，C# 的 <c>KeyMatch.SelfCsv</c>，见 0005）。
/// 另外覆盖 DTO 带出、SelectMany、分组合并的回退与其他方言的写法。
/// </summary>
public sealed class M8RelationColumnsTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // 与 Java 的 before1 相同的数据（Java 的 create_time 年份是 202，MySQL 的 DATETIME 不支持，这一列不参与断言）
    private async Task SeedAsync()
    {
        await Db.Delete<M8UserBookIds>().All().ExecuteAsync(Ct);
        await Db.Delete<M8UserBook2>().All().ExecuteAsync(Ct);
        var created = new DateTime(2020, 1, 1);
        await Db.Insert(new[]
        {
            new M8UserBookIds { UserId = "1", UserName = "1", UserAge = 1, CreateTime = created, BookIds = "1,2" },
            new M8UserBookIds { UserId = "2", UserName = "2", UserAge = 2, CreateTime = created, BookIds = "3,2" },
            new M8UserBookIds { UserId = "3", UserName = "3", UserAge = 3, CreateTime = created, BookIds = "2" },
            new M8UserBookIds { UserId = "4", UserName = "4", UserAge = 4, CreateTime = created, BookIds = "2" },
            new M8UserBookIds { UserId = "5", UserName = "5", UserAge = 5, CreateTime = created },
        }).ExecuteAsync(Ct);
        await Db.Insert(Enumerable.Range(1, 4).Select(i => new M8UserBook2 { BookId = $"{i}", BookName = $"book{i}", BookPrice = i }).ToList()).ExecuteAsync(Ct);
    }

    [Fact, Upstream("mysql8/M8Test1.java#testRelationColumns")]
    public async Task TestRelationColumns()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<M8UserBookIds>().Where(m => m.Books!.Any(x => x.BookPrice >= 0)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`user_id`,t.`user_name`,t.`user_age`,t.`create_time`,t.`book_ids` FROM `m8_user_book_ids` t WHERE EXISTS(SELECT 1 FROM `m8_user_book2` t1 WHERE FIND_IN_SET(t1.`book_id`,t.`book_ids`) AND t1.`book_price` >= ? LIMIT 1)");
        listen.AssertParams("0(BigDecimal)");
        Assert.Equal(["1", "2", "3", "4"], users.Select(u => u.UserId).Order(StringComparer.Ordinal));
    }

    [Fact, Upstream("mysql8/M8Test1.java#testRelationColumns1")]
    public async Task TestRelationColumns1()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var users = (await Db.Query<M8UserBookIds>().Include(m => m.Books).ToListAsync(Ct)).ToDictionary(u => u.UserId!);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT `user_id`,`user_name`,`user_age`,`create_time`,`book_ids` FROM `m8_user_book_ids`");
        listen.AssertSql(1, "SELECT t.`book_id`,t.`book_name`,t.`book_price` FROM `m8_user_book2` t WHERE FIND_IN_SET(t.`book_id`,?)");
        listen.AssertParams(1, "1,2,3(String)");
        Assert.Equal(["1", "2"], Books(users["1"]));
        Assert.Equal(["2", "3"], Books(users["2"]));
        Assert.Equal(["2"], Books(users["3"]));
        Assert.Equal(["2"], Books(users["4"]));
        Assert.Empty(users["5"].Books!);
    }

    private static string?[] Books(M8UserBookIds user) => [.. user.Books!.Select(b => b.BookId).Order(StringComparer.Ordinal)];

    /// <summary>用户与他的书名。</summary>
    public sealed class UserBooks
    {
        public string? UserId { get; set; }

        public List<M8UserBook2>? Books { get; set; }
    }

    // 投影到 DTO 时自动带出、SelectMany 展开、开了分组合并也照常写成子查询
    [Fact]
    public async Task CsvKeysWorkInDtoProjectionSelectManyAndGroupJoin()
    {
        await SeedAsync();
        var dto = (await Db.Query<M8UserBookIds>().Select<UserBooks>().ToListAsync(Ct)).ToDictionary(u => u.UserId!);
        Assert.Equal(["2", "3"], dto["2"].Books!.Select(b => b.BookId).Order(StringComparer.Ordinal));
        Assert.Empty(dto["5"].Books!);

        using var listen = SqlCapture.Listen();
        var pairs = await Db.Query<M8UserBookIds>().SelectMany(u => u.Books!, (u, b) => new { u.UserId, b.BookId }).ToListAsync(Ct);
        Assert.Contains("INNER JOIN `m8_user_book2` t1 ON FIND_IN_SET(t1.`book_id`,t.`book_ids`)", listen.Last.Statement.Text);
        Assert.Equal(6, pairs.Count);

        var counts = await Db.Query<M8UserBookIds>().GroupJoinAll().OrderBy(u => u.UserId).Select(u => u.Books!.Count()).ToListAsync(Ct);
        Assert.Contains("FIND_IN_SET(t1.`book_id`,t.`book_ids`)", listen.Last.Statement.Text);
        Assert.Equal([2, 2, 1, 1, 0], counts);
    }

    [Theory]
    [InlineData("pgsql", "SELECT t.\"user_id\" FROM \"m8_user_book_ids\" t WHERE EXISTS(SELECT 1 FROM \"m8_user_book2\" t1 WHERE ((t1.\"book_id\")::text = ANY(string_to_array(t.\"book_ids\",','))) LIMIT 1)")]
    [InlineData("sqlite", "SELECT t.\"user_id\" FROM \"m8_user_book_ids\" t WHERE EXISTS(SELECT 1 FROM \"m8_user_book2\" t1 WHERE (instr(',' || t.\"book_ids\" || ',', ',' || t1.\"book_id\" || ',') > 0) LIMIT 1)")]
    [InlineData("mssql", "SELECT t.[user_id] FROM [m8_user_book_ids] t WHERE EXISTS(SELECT TOP 1 1 FROM [m8_user_book2] t1 WHERE (CHARINDEX(',' + CAST(t1.[book_id] AS NVARCHAR(MAX)) + ',', ',' + t.[book_ids] + ',') > 0))")]
    [InlineData("oracle", "SELECT t.\"user_id\" FROM \"m8_user_book_ids\" t WHERE EXISTS(SELECT 1 FROM \"m8_user_book2\" t1 WHERE (INSTR(',' || t.\"book_ids\" || ',', ',' || t1.\"book_id\" || ',') > 0) AND ROWNUM < 2)")]
    public void CsvKeysRenderPerDialect(string dialect, string expected) =>
        Assert.Equal(expected, SqlOnly.Db(dialect).Query<M8UserBookIds>().Where(m => m.Books!.Any()).Select(m => m.UserId).ToSql());

    [Fact]
    public void ElementAccessOnCsvKeysIsRejected() =>
        Assert.Throws<NotSupportedException>(() => SqlOnly.Db("mysql").Query<M8UserBookIds>().Select(m => m.Books!.First().BookName).ToSql());
}
