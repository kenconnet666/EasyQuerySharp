namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>对应 Java：duckdb/DuckDBQueryTest.java（日期加减与相差、日期格式 strftime、字符串格式化、read_xlsx 表函数）。</summary>
public sealed class DuckDBQueryTest(DuckDbFixture fixture) : DuckDbTest(fixture)
{
    private const string BlogColumns = "SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ?";

    // 时间加减与相差的天、时、分、秒（可为负）。Java 的 duration(..).toDays() 对应 C# 的 (a - b).Days / TotalHours / TotalMinutes / TotalSeconds，
    // testDraft9_1 与 testDraft9 的查询相同（Java 只是多留了一段被注释掉的写法）
    [Fact, Upstream("duckdb/DuckDBQueryTest.java#testDraft9"), Upstream("duckdb/DuckDBQueryTest.java#testDraft9_1")]
    public async Task TestDraft9()
    {
        const string id = "123456zz9";
        await Db.Delete<BlogEntity>().WhereById(id).DisableLogicDelete().ExecuteAsync(Ct);
        var blog = new BlogEntity
        {
            Id = id, CreateBy = "z", CreateTime = new DateTime(2022, 1, 2, 3, 4, 5), UpdateBy = "z", UpdateTime = new DateTime(2022, 2, 3, 4, 5, 6),
            Title = "titlez", Content = "contentz", Url = "http://blog.easy-query.com/z", Star = 1, Score = 1.2m, Status = 1, Order = 1.2m,
            IsTop = false, Top = true, Deleted = false,
        };
        await Db.Insert(blog).ExecuteAsync(Ct);
        try
        {
            var added = await Db.Query<BlogEntity>().WhereById(id)
                .Select(o => new Draft<DateTime?, DateTime?, DateTime?>(o.CreateTime!.Value.AddDays(1), o.CreateTime!.Value.AddSeconds(2), o.CreateTime!.Value.AddMinutes(3)))
                .FirstOrDefaultAsync(Ct);
            Assert.Equal(new DateTime(2022, 1, 3, 3, 4, 5), added!.Value1);
            Assert.Equal(new DateTime(2022, 1, 2, 3, 4, 7), added.Value2);
            Assert.Equal(new DateTime(2022, 1, 2, 3, 7, 5), added.Value3);

            var diff = await Db.Query<BlogEntity>().WhereById(id)
                .Select(o => new Draft<int, int, int, int, int, int, int>(
                    (o.CreateTime!.Value - o.UpdateTime!.Value).Days,
                    (int)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalHours,
                    (int)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalMinutes,
                    (int)(o.CreateTime!.Value - o.UpdateTime!.Value).TotalSeconds,
                    (o.CreateTime!.Value - o.CreateTime!.Value.AddDays(1)).Days,
                    (int)(o.CreateTime!.Value - o.CreateTime!.Value.AddSeconds(2)).TotalSeconds,
                    (int)(o.CreateTime!.Value - o.CreateTime!.Value.AddMinutes(3)).TotalMinutes))
                .FirstOrDefaultAsync(Ct);
            Assert.NotNull(diff);
            Assert.Equal(-32, diff.Value1);
            Assert.Equal(-769, diff.Value2);
            Assert.Equal(-46141, diff.Value3);
            Assert.Equal(-2768461, diff.Value4);
            Assert.Equal(-1, diff.Value5);
            Assert.Equal(-2, diff.Value6);
            Assert.Equal(-3, diff.Value7);
            var minutes = await Db.Query<BlogEntity>().WhereById(id)
                .Select(o => (int)(o.CreateTime!.Value - o.CreateTime!.Value.AddDays(1).AddMinutes(3)).TotalMinutes).FirstOrDefaultAsync(Ct);
            Assert.Equal(-1443, minutes);
        }
        finally
        {
            await Db.Delete<BlogEntity>().WhereById(id).DisableLogicDelete().ExecuteAsync(Ct);
        }
    }

    // 带中文的格式串转成 strftime 的模式，结果与 .NET 按同样格式格式化一致；datetimeformat1 与 datetimeformat 的查询相同
    [Fact, Upstream("duckdb/DuckDBQueryTest.java#datetimeformat"), Upstream("duckdb/DuckDBQueryTest.java#datetimeformat1")]
    public async Task DateTimeFormat()
    {
        const string format = "yyyy年MM-01 HH:mm分ss秒";
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(d => new Draft<DateTime?, string>(d.CreateTime, d.CreateTime!.Value.ToString(format))).ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list, item => Assert.Equal(item.Value1!.Value.ToString(format, System.Globalization.CultureInfo.InvariantCulture), item.Value2));
        listen.AssertSql("SELECT t.\"create_time\" AS \"value1\",strftime(t.\"create_time\", '%Y年%m-01 %H:%M分%S秒') AS \"value2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("duckdb/DuckDBQueryTest.java#formatMySQL")]
    public async Task FormatMySQL()
    {
        const string format1 = "yyyy年MM-01 HH:mm分ss秒";
        const string format2 = "yyyy-MM-dd HH:mm:ss";
        const string format3 = "yyyy/MM-/01 HH时mm分ss秒";
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(t => new Draft<DateTime?, string, string, string>(
            t.CreateTime, t.CreateTime!.Value.ToString(format1), t.CreateTime!.Value.ToString(format2), t.CreateTime!.Value.ToString(format3))).ToListAsync(Ct);
        Assert.NotEmpty(list);
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        Assert.All(list, item =>
        {
            Assert.Equal(item.Value1!.Value.ToString(format1, invariant), item.Value2);
            Assert.Equal(item.Value1!.Value.ToString(format2, invariant), item.Value3);
            Assert.Equal(item.Value1!.Value.ToString(format3, invariant), item.Value4);
        });
        listen.AssertSql("SELECT t.\"create_time\" AS \"value1\",strftime(t.\"create_time\", '%Y年%m-01 %H:%M分%S秒') AS \"value2\",strftime(t.\"create_time\", '%Y-%m-%d %H:%M:%S') AS \"value3\",strftime(t.\"create_time\", '%Y/%m-/01 %H时%M分%S秒') AS \"value4\" FROM \"t_blog\" t WHERE t.\"deleted\" = ?");
        listen.AssertParams("false(Boolean)");
    }

    // Java 的 stringFormat：C# 的 string.Format / 插值字符串，{n} 可以重复
    [Fact, Upstream("duckdb/DuckDBQueryTest.java#testConcat"), Upstream("duckdb/DuckDBQueryTest.java#testConcat3")]
    public async Task TestConcat()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => string.Format("你好:{0}我叫{1}你好吗?我今年{2}岁了,{0}你呢", b.Title, b.Star, 12) == "比较一下").ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " AND CONCAT(?,\"title\",?,\"star\",?,?,?,\"title\",?) = ?");
        listen.AssertParams("false(Boolean),你好:(String),我叫(String),你好吗?我今年(String),12(Integer),岁了,(String),你呢(String),比较一下(String)");
        await Db.Query<BlogEntity>().Where(b => string.Format("你好:{0}我叫{1}你好吗?我今年{1}岁了", b.Title, b.Star) == "比较一下").ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " AND CONCAT(?,\"title\",?,\"star\",?,\"star\",?) = ?");
        listen.AssertParams("false(Boolean),你好:(String),我叫(String),你好吗?我今年(String),岁了(String),比较一下(String)");
    }

    // 格式串里的占位符超出参数个数：翻译时报错
    [Fact, Upstream("duckdb/DuckDBQueryTest.java#testConcat2")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2241", Justification = "故意写错占位符，验证翻译时报错")]
    public async Task TestConcat2() =>
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<BlogEntity>().Where(b => string.Format("你好:{0}我叫{1}你好吗?我今年{2}岁了,{3}你呢", b.Title, b.Star, 12) == "比较一下").ToListAsync(Ct));

    // ---- read_xlsx：DuckDB 的表函数当表用。Java 把函数写在 @Table 里（keyword = false，不加引号）；C# 在查询上用 AsTableSegment 整体替换表引用。
    // 只核对 SQL、不执行：read_xlsx 在 excel 扩展里（要联网安装），还要有 ducktest.xlsx
    private const string ReadXlsx = "只核对 SQL、不执行：read_xlsx 在 excel 扩展里（要联网安装），还要有 ducktest.xlsx；Java 的表函数写在 @Table 里，C# 用 AsTableSegment 替换表引用";

    private static string Sheet(string sheet, string? alias) => $"read_xlsx('./ducktest.xlsx',sheet='{sheet}')" + (alias is null ? "" : " " + alias);

    private static string Sheet1(string table, string? alias) => Sheet("Sheet1", alias);

    private static string Sheet2(string table, string? alias) => Sheet("Sheet2", alias);

    private static EasyDb Duck => SqlOnly.Db("duckdb");

    [Fact, Upstream("duckdb/DuckDBQueryTest.java#readExcel1", Deviation = ReadXlsx)]
    public void ReadExcel1()
    {
        var statement = Duck.Query<ExcelDuck>().AsTableSegment(Sheet1).Where(e => e.Name == "name2").ToStatement();
        SqlOnly.AssertStatement(statement, "SELECT \"id\",\"name\",\"age\" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') WHERE \"name\" = ?", "name2(String)");
    }

    [Fact, Upstream("duckdb/DuckDBQueryTest.java#readExcel2", Deviation = ReadXlsx)]
    public void ReadExcel2()
    {
        var statement = Duck.Query<ExcelDuck>().AsTableSegment(Sheet1).Where(e => e.ExcelDuck2List!.AsTableSegment(Sheet2).Any(s => s.Id == "2")).ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t.\"id\",t.\"name\",t.\"age\" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t WHERE EXISTS(SELECT 1 FROM read_xlsx('./ducktest.xlsx',sheet='Sheet2') t1 WHERE t1.\"uid\" = t.\"id\" AND t1.\"id\" = ? LIMIT 1)",
            "2(String)");
    }

    // Java 的 ALL_SUB_QUERY_GROUP_JOIN 行为：子查询合并成分组 JOIN
    [Fact, Upstream("duckdb/DuckDBQueryTest.java#readExcel3", Deviation = ReadXlsx)]
    public void ReadExcel3()
    {
        var statement = Duck.Query<ExcelDuck>().AsTableSegment(Sheet1)
            .GroupJoin(e => e.ExcelDuck2List, q => q.AsTableSegment(Sheet2))
            .Where(e => e.ExcelDuck2List!.Any(s => s.Id == "2")).ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t.\"id\",t.\"name\",t.\"age\" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t LEFT JOIN (SELECT t1.\"uid\" AS \"__group_key1__\",(COUNT(?) > 0) AS \"__any2__\" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet2') t1 WHERE t1.\"id\" = ? GROUP BY t1.\"uid\") t2 ON t2.\"__group_key1__\" = t.\"id\" WHERE COALESCE(t2.\"__any2__\",?) = ?",
            "1(Integer),2(String),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("duckdb/DuckDBQueryTest.java#readExcel4", Deviation = ReadXlsx)]
    public void ReadExcel4()
    {
        var statement = Duck.Query<ExcelDuck>().AsTableSegment(Sheet1).Where(e => e.Name!.Substring(4, 1) == "4")
            .Select(e => new Draft<string?, string>(e.Name, e.Name!.Substring(4, 1))).ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t.\"name\" AS \"value1\",SUBSTR(t.\"name\",5,1) AS \"value2\" FROM read_xlsx('./ducktest.xlsx',sheet='Sheet1') t WHERE SUBSTR(t.\"name\",5,1) = ?",
            "4(String)");
    }
}
