using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>
/// 对应 Java：pgsql/QueryTest19.java 的查询部分（Map 投影再过滤、字符串聚合、All、日期格式化、原生 SQL）。
/// Java 的 MapProxy / MapKey 投影写成带 [Column] 的 DTO；joining 写成 <c>g.StringJoin(x => x.Content, sep)</c>。
/// </summary>
public sealed class PgQueryTest19Queries(PgFixture fixture) : PgTest(fixture)
{
    private const string MapDeviation = "Java 的 Map 投影写成带 [Column] 的 DTO，SQL 相同";
    private const string MapValueDeviation = "Java 的 Map 投影写成带 [Column] 的 DTO，SQL 相同；Map 的值是 Object，Java 的比较参数保持 Integer，C# 按 DTO 属性的 decimal 传参";
    private const string DateFormat = "yyyy年MM-01 HH:mm分ss秒";

    /// <summary>Java 的 Map 投影 aaa / bbb。</summary>
    public sealed class AaaBbb
    {
        [Column("aaa")]
        public decimal? Aaa { get; set; }

        [Column("bbb")]
        public int? Bbb { get; set; }
    }

    /// <summary>Java 的 Map 投影 column1 / column2。</summary>
    public sealed class Column12
    {
        [Column("column1")]
        public string? Column1 { get; set; }

        [Column("column2")]
        public string? Column2 { get; set; }
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testMapChain", Deviation = MapValueDeviation)]
    public async Task TestMapChain()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new AaaBbb { Aaa = b.Score, Bbb = b.Star }).Where(o => o.Aaa == 123).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.\"aaa\" AS \"aaa\",t1.\"bbb\" AS \"bbb\" FROM (SELECT t.\"score\" AS \"aaa\",t.\"star\" AS \"bbb\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?) t1 WHERE t1.\"aaa\" = ?");
        listen.AssertParams("false(Boolean),123(BigDecimal)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testMapChain2", Deviation = MapValueDeviation)]
    public async Task TestMapChain2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new AaaBbb { Aaa = b.Score * 100, Bbb = b.Star }).Where(o => o.Aaa == 123).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.\"aaa\" AS \"aaa\",t1.\"bbb\" AS \"bbb\" FROM (SELECT (t.\"score\" * ?) AS \"aaa\",t.\"star\" AS \"bbb\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?) t1 WHERE t1.\"aaa\" = ?");
        listen.AssertParams("100(BigDecimal),false(Boolean),123(BigDecimal)");
    }

    // CONCAT 跳过 NULL（PostgreSQL 的行为）
    [Fact, Upstream("pgsql/QueryTest19.java#concat1")]
    public async Task Concat1()
    {
        var rows = await Db.QuerySql<DynamicRow>("select CONCAT('a', null , 'b') as \"aa\" ").ToListAsync(Ct);
        Assert.Equal("ab", rows[0]["aa"]);
    }

    // 带中文的格式串转成 TO_CHAR 的模式，结果与 .NET 按同样格式格式化一致
    [Fact, Upstream("pgsql/QueryTest19.java#datetimeformat")]
    public async Task Datetimeformat()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(d => new Draft<DateTime?, string?>(d.CreateTime, d.CreateTime!.Value.ToString(DateFormat))).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, r => Assert.Equal(r.Value1!.Value.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture), r.Value2));
        listen.AssertSql("SELECT t.\"create_time\" AS \"value1\",TO_CHAR((t.\"create_time\")::TIMESTAMP,'YYYY年MM-01 HH24:MI分SS秒') AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testJoining"), Upstream("pgsql/QueryTest19.java#testJoining2")]
    public async Task TestJoining()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.CreateTime < new DateTime(2022, 1, 1)).GroupBy(b => b.Title)
            .Select(g => new Draft<string?, string>(g.Key, g.StringJoin(x => x.Content, ","))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"title\" AS \"value1\",STRING_AGG((t.\"content\")::TEXT, ?) AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" < ? GROUP BY t.\"title\"");
        listen.AssertParams(",(String),false(Boolean),2022-01-01T00:00(LocalDateTime)");
    }

    // 分组拼接的结果作为 JOIN 来源
    [Fact, Upstream("pgsql/QueryTest19.java#testJoining3")]
    public async Task TestJoining3()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<BlogEntity>().Where(b => b.CreateTime < new DateTime(2022, 1, 1)).GroupBy(b => b.Title)
            .Select(g => new Draft<string?, string>(g.Key, g.StringJoin(x => x.Content, ",")));
        await Db.Query<BlogEntity>().LeftJoin(grouped, (a, b) => a.Content == b.Value2).Where((_, b) => b.Value1 == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\" FROM \"t_blog\" t LEFT JOIN (SELECT t1.\"title\" AS \"value1\",STRING_AGG((t1.\"content\")::TEXT, ?) AS \"value2\" FROM \"t_blog\" t1 WHERE t1.\"deleted\" = ? AND t1.\"create_time\" < ? GROUP BY t1.\"title\") t3 ON t.\"content\" = t3.\"value2\" WHERE t.\"deleted\" = ? AND t3.\"value1\" = ?");
        listen.AssertParams(",(String),false(Boolean),2022-01-01T00:00(LocalDateTime),false(Boolean),123(String)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testJoining4", Deviation = MapDeviation)]
    public async Task TestJoining4()
    {
        using var listen = SqlCapture.Listen();
        var grouped = Db.Query<BlogEntity>().Where(b => b.CreateTime < new DateTime(2022, 1, 1)).GroupBy(b => b.Title)
            .Select(g => new Column12 { Column1 = g.Key, Column2 = g.StringJoin(x => x.Content, ",") });
        await Db.Query<BlogEntity>().LeftJoin(grouped, (a, b) => a.Content == b.Column1).Where((_, b) => b.Column2 == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\" FROM \"t_blog\" t LEFT JOIN (SELECT t1.\"title\" AS \"column1\",STRING_AGG((t1.\"content\")::TEXT, ?) AS \"column2\" FROM \"t_blog\" t1 WHERE t1.\"deleted\" = ? AND t1.\"create_time\" < ? GROUP BY t1.\"title\") t3 ON t.\"content\" = t3.\"column1\" WHERE t.\"deleted\" = ? AND t3.\"column2\" = ?");
        listen.AssertParams(",(String),false(Boolean),2022-01-01T00:00(LocalDateTime),false(Boolean),123(String)");
    }

    // 分块读取
    [Fact, Upstream("pgsql/QueryTest19.java#testChunk")]
    public async Task TestChunk()
    {
        await foreach (var chunk in Db.Query<BlogEntity>().OrderBy(b => b.Id).ToChunksAsync(100, Ct)) Assert.NotEmpty(chunk);
    }

    // 储蓄卡都以 33123 开头的用户（没有卡、卡号为空的也算）
    [Fact, Upstream("pgsql/QueryTest19.java#testAll")]
    public async Task TestAll()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<DocUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToListAsync(Ct);
        Assert.Equal(3, users.Count);
        listen.AssertSql(0, "SELECT t.\"id\",t.\"name\",t.\"phone\",t.\"age\" FROM \"doc_user\" t WHERE NOT (EXISTS(SELECT 1 FROM \"doc_bank_card\" t1 WHERE t1.\"uid\" = t.\"id\" AND t1.\"type\" = ? AND (NOT (t1.\"code\" LIKE CONCAT((?)::TEXT,'%'))) LIMIT 1))");
        listen.AssertParams(0, "储蓄卡(String),33123(String)");
        listen.AssertSql(1, "SELECT t.\"id\",t.\"uid\",t.\"code\",t.\"type\",t.\"bank_id\" FROM \"doc_bank_card\" t WHERE t.\"uid\" IN (?,?,?)");
    }

    // 卡号为空当空串：有储蓄卡的用户都不满足
    [Fact, Upstream("pgsql/QueryTest19.java#testAll1")]
    public async Task TestAll1()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<DocUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => (c.Code ?? "").LikeStartsWith("33123"))).ToListAsync(Ct);
        Assert.Single(users);
        listen.AssertSql(0, "SELECT t.\"id\",t.\"name\",t.\"phone\",t.\"age\" FROM \"doc_user\" t WHERE NOT (EXISTS(SELECT 1 FROM \"doc_bank_card\" t1 WHERE t1.\"uid\" = t.\"id\" AND t1.\"type\" = ? AND (NOT (COALESCE(t1.\"code\",?) LIKE CONCAT((?)::TEXT,'%'))) LIMIT 1))");
        listen.AssertParams(0, "储蓄卡(String),(String),33123(String)");
        listen.AssertSql(1, "SELECT t.\"id\",t.\"uid\",t.\"code\",t.\"type\",t.\"bank_id\" FROM \"doc_bank_card\" t WHERE t.\"uid\" IN (?)");
        listen.AssertParams(1, "小黄id(String)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testAll2")]
    public async Task TestAll2()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<DocUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToListAsync(Ct);
        Assert.Equal(3, users.Count);
        listen.AssertSql(0, "SELECT t.\"id\",t.\"name\",t.\"phone\",t.\"age\" FROM \"doc_user\" t WHERE NOT (EXISTS(SELECT 1 FROM \"doc_bank_card\" t1 WHERE t1.\"uid\" = t.\"id\" AND t1.\"type\" = ? AND (NOT (t1.\"code\" LIKE CONCAT((?)::TEXT,'%') AND t1.\"code\" LIKE CONCAT((?)::TEXT,'%'))) LIMIT 1))");
        listen.AssertParams(0, "储蓄卡(String),33123(String),45678(String)");
        listen.AssertSql(1, "SELECT t.\"id\",t.\"uid\",t.\"code\",t.\"type\",t.\"bank_id\" FROM \"doc_bank_card\" t WHERE t.\"uid\" IN (?,?,?)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testAllGroupJoin")]
    public async Task TestAllGroupJoin()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<DocUser>().GroupJoinAll().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToListAsync(Ct);
        Assert.Equal(3, users.Count);
        listen.AssertSql(0, "SELECT t.\"id\",t.\"name\",t.\"phone\",t.\"age\" FROM \"doc_user\" t LEFT JOIN (SELECT t1.\"uid\" AS \"__group_key1__\",(COUNT(?) <= 0) AS \"__none2__\" FROM \"doc_bank_card\" t1 WHERE t1.\"type\" = ? AND (NOT (t1.\"code\" LIKE CONCAT((?)::TEXT,'%'))) GROUP BY t1.\"uid\") t2 ON t2.\"__group_key1__\" = t.\"id\" WHERE COALESCE(t2.\"__none2__\",?) = ?");
        listen.AssertParams(0, "1(Integer),储蓄卡(String),33123(String),true(Boolean),true(Boolean)");
        listen.AssertSql(1, "SELECT t.\"id\",t.\"uid\",t.\"code\",t.\"type\",t.\"bank_id\" FROM \"doc_bank_card\" t WHERE t.\"uid\" IN (?,?,?)");
    }

    [Fact, Upstream("pgsql/QueryTest19.java#testAllGroupJoin2")]
    public async Task TestAllGroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<DocUser>().GroupJoinAll().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToListAsync(Ct);
        Assert.Equal(3, users.Count);
        listen.AssertSql(0, "SELECT t.\"id\",t.\"name\",t.\"phone\",t.\"age\" FROM \"doc_user\" t LEFT JOIN (SELECT t1.\"uid\" AS \"__group_key1__\",(COUNT(?) <= 0) AS \"__none2__\" FROM \"doc_bank_card\" t1 WHERE t1.\"type\" = ? AND (NOT (t1.\"code\" LIKE CONCAT((?)::TEXT,'%') AND t1.\"code\" LIKE CONCAT((?)::TEXT,'%'))) GROUP BY t1.\"uid\") t2 ON t2.\"__group_key1__\" = t.\"id\" WHERE COALESCE(t2.\"__none2__\",?) = ?");
        listen.AssertParams(0, "1(Integer),储蓄卡(String),33123(String),45678(String),true(Boolean),true(Boolean)");
        listen.AssertSql(1, "SELECT t.\"id\",t.\"uid\",t.\"code\",t.\"type\",t.\"bank_id\" FROM \"doc_bank_card\" t WHERE t.\"uid\" IN (?,?,?)");
    }

    // 同一个 UUID 存进原生 uuid 列与文本列，读回相等
    [Fact, Upstream("pgsql/QueryTest19.java#testUUId")]
    public async Task TestUUId()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(UUIDEntity)], Ct);
        await Db.CodeFirst.CreateTablesAsync([typeof(UUIDEntity)], Ct);
        var uuid = Guid.NewGuid();
        await Db.Insert(new UUIDEntity { Id1 = uuid, Id2 = uuid }).ExecuteAsync(Ct);
        var read = await Db.Query<UUIDEntity>().FirstAsync(Ct);
        Assert.Equal((uuid, uuid), (read.Id1, read.Id2));
    }

    /// <summary>对应 Java：pgsql/MyClass（分组结果按别名映射）。</summary>
    private sealed class MyClass
    {
        public string? Type1 { get; set; }

        public decimal? Result { get; set; }
    }

    // Java 用例没有断言：分组的求和做四则运算后取 3 位小数；第二种写法是原生 SQL 片段
    [Fact, Upstream("pgsql/QueryTest19.java#testItem")]
    public async Task TestItem()
    {
        await Db.CodeFirst.CreateTablesAsync([typeof(PgItem)], Ct);
        await Db.Query<PgItem>().GroupBy(p => p.Type)
            .Select(g => new Draft<string?, decimal>(g.Key,
                Math.Round(100m - (decimal)((g.Sum(o => o.Column2) + g.Sum(o => o.Column3)) * 100m / g.Sum(o => o.Column4 == 0m ? null : o.Column4))!, 3)))
            .ToListAsync(Ct);
        await Db.Query<PgItem>().GroupBy(p => p.Type)
            .Select(g => new MyClass
            {
                Type1 = g.Key,
                Result = Sql.Raw<decimal?>("round(100 - (sum({0}) + sum({1})) * 100.0 / nullif(sum({2}), 0)::numeric, 3)",
                    g.Column(o => o.Column2), g.Column(o => o.Column3), g.Column(o => o.Column4)),
            })
            .ToListAsync(Ct);
    }
}
