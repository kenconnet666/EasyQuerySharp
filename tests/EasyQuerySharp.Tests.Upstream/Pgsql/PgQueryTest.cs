namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>
/// 对应 Java：pgsql/QueryTest.java（PostgreSQL 上的博客查询：按主键、IN、原生投影、字符串拼接、时间差、窗口函数投影成 Part 后再过滤、CTE）。
/// Java 的 rowNumberOver() / rankOver() / sumOver(x)… 对应 <c>Sql.Over.RowNumber / Rank / Sum…</c>，duration(…).toDays() 对应 <c>(a - b).TotalDays</c>。
/// </summary>
public sealed class PgQueryTest(PgFixture fixture) : PgTest(fixture)
{
    [Fact, Upstream("pgsql/QueryTest.java#query1")]
    public async Task Query1()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "123");
        Assert.Equal("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ?", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("pgsql/QueryTest.java#query2")]
    public async Task Query2()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "97");
        Assert.Equal("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ?", query.ToSql());
        Assert.Equal("97", (await query.FirstOrDefaultAsync(Ct))?.Id);
    }

    // Java 的 select(" 1 ") 仍按实体读出；C# 的 SelectRaw 同样按实体读，id 为空
    [Fact, Upstream("pgsql/QueryTest.java#query3")]
    public async Task Query3()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "97").Take(1).SelectRaw(" 1 ");
        Assert.Equal("SELECT  1  FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ? LIMIT 1", query.ToSql());
        var blog = await query.FirstOrDefaultAsync(Ct);
        Assert.NotNull(blog);
        Assert.Null(blog.Id);
    }

    [Fact, Upstream("pgsql/QueryTest.java#query4")]
    public async Task Query4()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "97").Take(1).SelectRaw("1 as id");
        Assert.Equal("SELECT 1 as id FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ? LIMIT 1", query.ToSql());
        Assert.Equal("1", (await query.FirstOrDefaultAsync(Ct))?.Id);
    }

    [Fact, Upstream("pgsql/QueryTest.java#query5")]
    public async Task Query5()
    {
        string[] ids = ["97", "98"];
        var query = Db.Query<BlogEntity>().Where(o => ids.Contains(o.Id));
        Assert.Equal("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" IN (?,?)", query.ToSql());
        var blogs = await query.OrderBy(o => o.Id).ToListAsync(Ct);
        Assert.Equal(2, blogs.Count);
        Assert.False(blogs[0].IsTop);
        Assert.False(blogs[0].Top);
        Assert.True(blogs[1].IsTop);
        Assert.True(blogs[1].Top);
    }

    [Fact, Upstream("pgsql/QueryTest.java#query6")]
    public async Task Query6()
    {
        var query = Db.Query<BlogEntity>().WhereById("97");
        Assert.Equal("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND \"id\" = ?", query.ToSql());
        var blog = Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal("97", blog.Id);
        Assert.False(blog.IsTop);
        Assert.False(blog.Top);
    }

    // 种子数据逐列读回（时间、小数、布尔）
    [Fact, Upstream("pgsql/QueryTest.java#query7")]
    public async Task Query7()
    {
        var blogs = await Db.Query<BlogEntity>().OrderBy(o => o.CreateTime).ToListAsync(Ct);
        var begin = new DateTime(2020, 1, 1, 1, 1, 1);
        for (var i = 0; i < blogs.Count; i++)
        {
            var blog = blogs[i];
            var index = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(index, blog.Id);
            Assert.Equal(index, blog.CreateBy);
            Assert.Equal(begin.AddDays(i), blog.CreateTime);
            Assert.Equal(index, blog.UpdateBy);
            Assert.Equal(begin.AddDays(i), blog.UpdateTime);
            Assert.Equal("title" + index, blog.Title);
            Assert.Equal("http://blog.easy-query.com/" + index, blog.Url);
            Assert.Equal(i, blog.Star);
            Assert.Equal(1.2m, blog.Score);
            Assert.Equal(i % 3 == 0 ? 0 : 1, blog.Status);
            Assert.Equal(1.2m * i, blog.Order);
            Assert.Equal(i % 2 == 0, blog.IsTop);
            Assert.Equal(i % 2 == 0, blog.Top);
            Assert.False(blog.Deleted);
        }
    }

    [Fact, Upstream("pgsql/QueryTest.java#query8")]
    public async Task Query8()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "97").Select(s => string.Concat(s.Id, s.Id));
        Assert.Equal("SELECT CONCAT(t.\"id\",t.\"id\") FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"id\" = ?", query.ToSql());
        Assert.Equal("9797", await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("pgsql/QueryTest.java#query9")]
    public async Task Query9()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "97").Select(s => string.Concat(s.Id, s.Title));
        Assert.Equal("SELECT CONCAT(t.\"id\",t.\"title\") FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"id\" = ?", query.ToSql());
        Assert.Equal("97title97", await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("pgsql/QueryTest.java#query10")]
    public void Query10()
    {
        Assert.Equal("SELECT COALESCE(t.\"id\",?) FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"id\" = ?", Db.Query<BlogEntity>().Where(o => o.Id == "97").Select(b => b.Id ?? "1").ToSql());
    }

    // 时间加减与相差的天、时、分、秒（截断取整，可为负）
    [Fact, Upstream("pgsql/QueryTest.java#testDraft9"), Upstream("pgsql/QueryTest.java#testDraft9_1")]
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
            var diff = await Db.Query<BlogEntity>().WhereById(id)
                .Select(o => new Draft<int, int, int, int, int, int, int>(
                    (o.UpdateTime!.Value - o.CreateTime!.Value).Days * -1 + 0 == 0 ? 0 : (o.CreateTime!.Value - o.UpdateTime!.Value).Days,
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

    // 中文日期格式（Java 的 format 对应 ToString(格式)）
    [Fact, Upstream("pgsql/QueryTest.java#test123")]
    public async Task Test123()
    {
        await Db.Query<BlogEntity>().Where(m => m.CreateTime!.Value.ToString("yyyy年MM月dd日") == "2022年01月01日" || m.Id != null).ToListAsync(Ct);
        Assert.Equal("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND TO_CHAR((\"create_time\")::TIMESTAMP,'YYYY年MM月DD日') = ?", Db.Query<BlogEntity>().Where(m => m.CreateTime!.Value.ToString("yyyy年MM月dd日") == "2022年01月01日").ToSql());
    }

    private Query<BlogEntity> After2020() => Db.Query<BlogEntity>().Where(b => b.CreateTime > new DateTime(2020, 1, 1, 1, 1, 0));

    // 窗口函数投影成 Part 后按实体列与窗口值过滤；同一窗口再投影成 Draft
    private static async Task AssertPart<TValue>(Query<Part<BlogEntity, TValue>> parts, string method, TValue expectedValue)
    {
        using var listen = SqlCapture.Listen();
        var rows = await parts.ToListAsync(Ct);
        listen.AssertSql(ExpectedSql[method]);
        listen.AssertParams(ExpectedParams[method]);
        Assert.All(rows, r => Assert.Equal(expectedValue, r.Value1));
    }

    private static async Task AssertDraft<TValue>(Query<Draft<TValue, string?>> drafts, string method)
    {
        using var listen = SqlCapture.Listen();
        await drafts.ToListAsync(Ct);
        listen.AssertSql(ExpectedSql[method]);
        listen.AssertParams(ExpectedParams[method]);
    }

    private static readonly Dictionary<string, string> ExpectedSql = new()
    {
        ["testPartitionBy1"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(ROW_NUMBER() OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy2"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(ROW_NUMBER() OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy3"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(RANK() OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy3_1"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(RANK() OVER (PARTITION BY t.\"title\" ORDER BY COALESCE(t.\"create_time\",?) ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy4"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(RANK() OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy5"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(DENSE_RANK() OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy6"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(DENSE_RANK() OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy7"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(SUM(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy8"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(SUM(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy9"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(AVG(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy10"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(AVG(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy11"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(MAX(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy12"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(MAX(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy13"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(MIN(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy14"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(MIN(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy15"] = "SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\",t1.\"__part__column1\" AS \"__part__column1\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(COUNT(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy16"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(COUNT(t.\"star\") OVER (PARTITION BY t.\"title\" ORDER BY t.\"create_time\" ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
        ["testPartitionBy16_1"] = "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"id\" AS \"value2\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(COUNT(t.\"star\") OVER (PARTITION BY t.\"title\")) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?",
    };

    private static readonly Dictionary<string, string> ExpectedParams = new()
    {
        ["testPartitionBy1"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy2"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy3"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy3_1"] = "2020-01-01T01:01(LocalDateTime),false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy4"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy5"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy6"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy7"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Integer)",
        ["testPartitionBy8"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Integer)",
        ["testPartitionBy9"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(BigDecimal)",
        ["testPartitionBy10"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(BigDecimal)",
        ["testPartitionBy11"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Integer)",
        ["testPartitionBy12"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Integer)",
        ["testPartitionBy13"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Integer)",
        ["testPartitionBy14"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Integer)",
        ["testPartitionBy15"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy16"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
        ["testPartitionBy16_1"] = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)",
    };

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy1")]
    public Task TestPartitionBy1() => AssertPart(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.RowNumber(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L), "testPartitionBy1", 1L);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy2")]
    public Task TestPartitionBy2() => AssertDraft(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.RowNumber(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L).Select(p => new Draft<long, string?>(p.Value1, p.Entity.Id)), "testPartitionBy2");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy3")]
    public Task TestPartitionBy3() => AssertPart(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Rank(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L), "testPartitionBy3", 1L);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy3_1")]
    public Task TestPartitionBy3_1() => AssertPart(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Rank(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime ?? new DateTime(2020, 1, 1, 1, 1, 0)))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L), "testPartitionBy3_1", 1L);

    /// <summary>Java 的 BlogPartitionVO：博客的标题、链接与排名。</summary>
    public sealed class BlogPartitionVO
    {
        public string? Title { get; set; }

        public string? Url { get; set; }

        public long? Rank { get; set; }
    }

    // Java 的 FETCHER.allFields() 按 VO 的同名属性选列（VO 上有 title、url）
    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy3_2")]
    public async Task TestPartitionBy3_2()
    {
        using var listen = SqlCapture.Listen();
        var list = await After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Rank(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime ?? new DateTime(2020, 1, 1, 1, 1, 0)))))
            .Where(p => p.Entity.Star > 1 && p.Value1 < 2L)
            .Select(p => new BlogPartitionVO { Title = p.Entity.Title, Url = p.Entity.Url, Rank = p.Value1 }).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.\"title\",t1.\"url\",t1.\"__part__column1\" AS \"rank\" FROM (SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\",(RANK() OVER (PARTITION BY t.\"title\" ORDER BY COALESCE(t.\"create_time\",?) ASC)) AS \"__part__column1\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ?");
        listen.AssertParams("2020-01-01T01:01(LocalDateTime),false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long)");
        Assert.All(list, v => Assert.NotNull(v.Rank));
    }

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy4")]
    public Task TestPartitionBy4() => AssertDraft(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Rank(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L).Select(p => new Draft<long, string?>(p.Value1, p.Entity.Id)), "testPartitionBy4");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy5")]
    public Task TestPartitionBy5() => AssertPart(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.DenseRank(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L), "testPartitionBy5", 1L);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy6")]
    public Task TestPartitionBy6() => AssertDraft(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.DenseRank(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L).Select(p => new Draft<long, string?>(p.Value1, p.Entity.Id)), "testPartitionBy6");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy7")]
    public Task TestPartitionBy7() => AssertPart(After2020().Select(b => new Part<BlogEntity, int?>(b, Sql.Over.Sum(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2), "testPartitionBy7", 1);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy8")]
    public Task TestPartitionBy8() => AssertDraft(After2020().Select(b => new Part<BlogEntity, int?>(b, Sql.Over.Sum(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2).Select(p => new Draft<int?, string?>(p.Value1, p.Entity.Id)), "testPartitionBy8");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy9")]
    public Task TestPartitionBy9() => AssertPart(After2020().Select(b => new Part<BlogEntity, decimal?>(b, Sql.Over.Avg((decimal?)b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2m), "testPartitionBy9", 1m);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy10")]
    public Task TestPartitionBy10() => AssertDraft(After2020().Select(b => new Part<BlogEntity, decimal?>(b, Sql.Over.Avg((decimal?)b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2m).Select(p => new Draft<decimal?, string?>(p.Value1, p.Entity.Id)), "testPartitionBy10");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy11")]
    public Task TestPartitionBy11() => AssertPart(After2020().Select(b => new Part<BlogEntity, int?>(b, Sql.Over.Max(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2), "testPartitionBy11", 1);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy12")]
    public Task TestPartitionBy12() => AssertDraft(After2020().Select(b => new Part<BlogEntity, int?>(b, Sql.Over.Max(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2).Select(p => new Draft<int?, string?>(p.Value1, p.Entity.Id)), "testPartitionBy12");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy13")]
    public Task TestPartitionBy13() => AssertPart(After2020().Select(b => new Part<BlogEntity, int?>(b, Sql.Over.Min(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2), "testPartitionBy13", 1);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy14")]
    public Task TestPartitionBy14() => AssertDraft(After2020().Select(b => new Part<BlogEntity, int?>(b, Sql.Over.Min(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2).Select(p => new Draft<int?, string?>(p.Value1, p.Entity.Id)), "testPartitionBy14");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy15")]
    public Task TestPartitionBy15() => AssertPart(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Count(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L), "testPartitionBy15", 1L);

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy16")]
    public Task TestPartitionBy16() => AssertDraft(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Count(b.Star, w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L).Select(p => new Draft<long, string?>(p.Value1, p.Entity.Id)), "testPartitionBy16");

    [Fact, Upstream("pgsql/QueryTest.java#testPartitionBy16_1")]
    public Task TestPartitionBy16_1() => AssertDraft(After2020().Select(b => new Part<BlogEntity, long>(b, Sql.Over.Count(b.Star, w => w.PartitionBy(b.Title))))
        .Where(p => p.Entity.Star > 1 && p.Value1 < 2L).Select(p => new Draft<long, string?>(p.Value1, p.Entity.Id)), "testPartitionBy16_1");

    [Fact, Upstream("pgsql/QueryTest.java#testNullDefault")]
    public async Task TestNullDefault()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => (b.Id ?? "123") == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"title\",\"content\",\"url\",\"star\",\"publish_time\",\"score\",\"status\",\"order\",\"is_top\",\"top\" FROM \"t_blog\" WHERE \"deleted\" = ? AND COALESCE(\"id\",?) = ?");
        listen.AssertParams("false(Boolean),123(String),123(String)");
    }

    // 同一个查询作为两个不同名字的 CTE 连接
    [Fact, Upstream("pgsql/QueryTest.java#testToWithAs2")]
    public async Task TestToWithAs2()
    {
        var topics = Db.Query<BlogEntity>().Where(t => t.Id == "456");
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>()
            .LeftJoin(topics.AsCte("aa"), (t, t1) => t.Id == t1.Id)
            .LeftJoin(topics.AsCte("bb"), (t, t1, t2) => t.Id == t2.Id)
            .Where((t, t1, t2) => t.Id == "123" && t2.Id == "t2123").ToListAsync(Ct);
        listen.AssertSql("WITH \"aa\" AS (SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\" FROM \"t_blog\" t1 WHERE t1.\"deleted\" = ? AND t1.\"id\" = ?) ,\"bb\" AS (SELECT t1.\"id\",t1.\"create_time\",t1.\"update_time\",t1.\"create_by\",t1.\"update_by\",t1.\"deleted\",t1.\"title\",t1.\"content\",t1.\"url\",t1.\"star\",t1.\"publish_time\",t1.\"score\",t1.\"status\",t1.\"order\",t1.\"is_top\",t1.\"top\" FROM \"t_blog\" t1 WHERE t1.\"deleted\" = ? AND t1.\"id\" = ?) SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\" FROM \"t_blog\" t LEFT JOIN \"aa\" t2 ON t2.\"deleted\" = ? AND t.\"id\" = t2.\"id\" LEFT JOIN \"bb\" t3 ON t3.\"deleted\" = ? AND t.\"id\" = t3.\"id\" WHERE t.\"deleted\" = ? AND t.\"id\" = ? AND t3.\"id\" = ?");
        listen.AssertParams("false(Boolean),456(String),false(Boolean),456(String),false(Boolean),false(Boolean),false(Boolean),123(String),t2123(String)");
    }

    // Java 用例没有断言：与当前时间相差的天数
    [Fact, Upstream("pgsql/QueryTest.java#testDuration")]
    public async Task TestDuration()
    {
        var now = DateTime.Now;
        await Db.Query<BlogEntity>().Where(t => (now - t.CreateTime!.Value).Days > 1).ToListAsync(Ct);
    }
}
