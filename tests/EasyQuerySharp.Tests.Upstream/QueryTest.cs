using EasyQuerySharp.Querying;
using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>对应 Java：QueryTest.java。</summary>
public sealed class QueryTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns =
        "`id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top`";

    private static string Prefixed(string alias, string columns) => alias + "." + columns.Replace(",`", "," + alias + ".`");

    [Fact, Upstream("QueryTest.java#query0")]
    public async Task Query0()
    {
        using var listen = SqlCapture.Listen();
        var sysUser = await Db.Query<SysUser>().Where(s => s.Id == "123xxx").FirstOrDefaultAsync(Ct);
        Assert.Null(sysUser);
        listen.AssertSql("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE `id` = ? LIMIT 1");
        listen.AssertParams("123xxx(String)");
    }

    [Fact, Upstream("QueryTest.java#query0_1")]
    public async Task Query0_1()
    {
        using var listen = SqlCapture.Listen();
        var sysUser = await Db.Query<SysUser>().AsAlias("x").Where(s => s.Id == "123xxx").FirstOrDefaultAsync(Ct);
        Assert.Null(sysUser);
        listen.AssertSql("SELECT x.`id`,x.`create_time`,x.`username`,x.`phone`,x.`id_card`,x.`address` FROM `easy-query-test`.`t_sys_user` x WHERE x.`id` = ? LIMIT 1");
        listen.AssertParams("123xxx(String)");
    }

    [Fact, Upstream("QueryTest.java#query0_2")]
    public async Task Query0_2()
    {
        using var listen = SqlCapture.Listen();
        var sysUser = await Db.Query<SysUser>().Where(s => s.Id == "123xxx").SingleOrDefaultAsync(Ct);
        Assert.Null(sysUser);
        listen.AssertSql("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE `id` = ?");
        listen.AssertParams("123xxx(String)");
    }

    [Fact, Upstream("QueryTest.java#query1")]
    public async Task Query1()
    {
        using var listen = SqlCapture.Listen();
        var blog = await Db.Query<BlogEntity>().AsAlias("x").Where(s => s.Id == "123").FirstOrDefaultAsync(Ct);
        Assert.Null(blog);
        listen.AssertSql($"SELECT {Prefixed("x", BlogColumns)} FROM `t_blog` x WHERE x.`deleted` = ? AND x.`id` = ? LIMIT 1");
        listen.AssertParams("false(Boolean),123(String)");
    }

    [Fact, Upstream("QueryTest.java#query2")]
    public async Task Query2()
    {
        using var listen = SqlCapture.Listen();
        var blog = await Db.Query<BlogEntity>().Where(b => b.Id == "97").FirstOrDefaultAsync(Ct);
        Assert.NotNull(blog);
        Assert.Equal("97", blog.Id);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ? LIMIT 1");
        listen.AssertParams("false(Boolean),97(String)");
    }

    [Fact, Upstream("QueryTest.java#query3")]
    public async Task Query3()
    {
        using var listen = SqlCapture.Listen();
        var blog = await Db.Query<BlogEntity>().Where(b => b.Id == "97").Take(1).SelectRaw(" 1 ").FirstOrDefaultAsync(Ct);
        Assert.NotNull(blog);
        Assert.Null(blog.Id);
        listen.AssertSql("SELECT  1  FROM `t_blog` WHERE `deleted` = ? AND `id` = ? LIMIT 1");
        listen.AssertParams("false(Boolean),97(String)");
    }

    [Fact, Upstream("QueryTest.java#query4")]
    public async Task Query4()
    {
        using var listen = SqlCapture.Listen();
        var blog = await Db.Query<BlogEntity>().Where(b => b.Id == "97").Take(1).SelectRaw("1 as id").FirstOrDefaultAsync(Ct);
        Assert.NotNull(blog);
        Assert.Equal("1", blog.Id);
        listen.AssertSql("SELECT 1 as id FROM `t_blog` WHERE `deleted` = ? AND `id` = ? LIMIT 1");
        listen.AssertParams("false(Boolean),97(String)");
    }

    [Fact, Upstream("QueryTest.java#query5")]
    public async Task Query5()
    {
        using var listen = SqlCapture.Listen();
        List<string> ids = ["97", "98"];
        var blogs = await Db.Query<BlogEntity>().Where(b => ids.Contains(b.Id!)).ToListAsync(Ct);
        Assert.Equal(2, blogs.Count);
        Assert.False(blogs[0].IsTop);
        Assert.False(blogs[0].Top);
        Assert.True(blogs[1].IsTop);
        Assert.True(blogs[1].Top);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` IN (?,?)");
        listen.AssertParams("false(Boolean),97(String),98(String)");
    }

    [Fact, Upstream("QueryTest.java#query6")]
    public async Task Query6()
    {
        using var listen = SqlCapture.Listen();
        var blogs = await Db.Query<BlogEntity>().WhereById("97").ToListAsync(Ct);
        Assert.Single(blogs);
        Assert.Equal("97", blogs[0].Id);
        Assert.False(blogs[0].IsTop);
        Assert.False(blogs[0].Top);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ?");
        listen.AssertParams("false(Boolean),97(String)");
    }

    [Fact, Upstream("QueryTest.java#query7")]
    public async Task Query7()
    {
        var blogs = await Db.Query<BlogEntity>().Where(b => b.Star <= 100).OrderBy(b => b.CreateTime).ToListAsync(Ct);
        var begin = new DateTime(2020, 1, 1, 1, 1, 1);
        for (var i = 0; i < blogs.Count; i++)
        {
            var blog = blogs[i];
            var index = i.ToString();
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

    [Fact, Upstream("QueryTest.java#query8")]
    public async Task Query8()
    {
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` LIMIT 1", Db.Query<Topic>().Take(1).ToSql());
        Assert.Single(await Db.Query<Topic>().Take(1).ToListAsync(Ct));
        Assert.NotNull(await Db.Query<Topic>().FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query9")]
    public async Task Query9()
    {
        Assert.NotNull(await Db.Query<Topic>().Where(o => o.Id == "3").FirstOrDefaultAsync(Ct));
        Assert.Single(await Db.Query<Topic>().Where(o => o.Id == "3").ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query10")]
    public async Task Query10()
    {
        var sql = Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).Where(o => o.Id == "3").ToSql();
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?", sql);
        Assert.NotNull(await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id).Where(o => o.Id == "3").FirstOrDefaultAsync(Ct));

        var joined = Db.Query<Topic>()
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, b2) => b2.Title != null && t.Id == "3")
            .Select((t, t1) => t1);
        Assert.Equal($"SELECT {Prefixed("t1", BlogColumns)} FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL AND t.`id` = ?", joined.ToSql());
        Assert.NotNull(await joined.ToListAsync(Ct));

        // Java 的 FETCHER.allFieldsExclude(id)：C# 用成员初始化列出要的列，同名列不写 AS，SQL 一致
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, b2) => b2.Title != null && t.Id == "3")
            .Select((t, t1) => new BlogEntity
            {
                CreateTime = t1.CreateTime, UpdateTime = t1.UpdateTime, CreateBy = t1.CreateBy, UpdateBy = t1.UpdateBy, Deleted = t1.Deleted,
                Title = t1.Title, Content = t1.Content, Url = t1.Url, Star = t1.Star, PublishTime = t1.PublishTime, Score = t1.Score,
                Status = t1.Status, Order = t1.Order, IsTop = t1.IsTop, Top = t1.Top,
            })
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`create_time`,t1.`update_time`,t1.`create_by`,t1.`update_by`,t1.`deleted`,t1.`title`,t1.`content`,t1.`url`,t1.`star`,t1.`publish_time`,t1.`score`,t1.`status`,t1.`order`,t1.`is_top`,t1.`top` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL AND t.`id` = ?");
        listen.AssertParams("false(Boolean),3(String)");
    }

    [Fact, Upstream("QueryTest.java#query10_1")]
    public void Query10_1()
    {
        var sql = Db.Query<Topic>().AsAlias("y").LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).AsAlias("z").Where(o => o.Id == "3").ToSql();
        Assert.Equal("SELECT y.`id`,y.`stars`,y.`title`,y.`create_time` FROM `t_topic` y LEFT JOIN `t_blog` z ON z.`deleted` = ? AND y.`id` = z.`id` WHERE y.`id` = ?", sql);
    }

    [Fact, Upstream("QueryTest.java#query11")]
    public async Task Query11()
    {
        using var listen = SqlCapture.Listen();
        var sub = Db.Query<Topic>().Where(o => o.Id == "3");
        List<string> titles = ["1", "2", "3"];
        var topics = await Db.Query<BlogEntity>()
            .LeftJoin(sub, (a, b) => a.Id == b.Id)
            .Where(o => o.Id != null && o.Id == "3")
            .Where((a, b) => a.Id != null && a.Id == "3")
            .Where((a, b) => a.Id != null && b.Id == "3")
            .Where((a, b) => a.Id != null && b.Id == "3" && titles.Any(title => a.Title!.Contains(title)))
            .ToListAsync(Ct);
        Assert.Single(topics);
        listen.AssertSql($"SELECT {Prefixed("t", BlogColumns)} FROM `t_blog` t LEFT JOIN (SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time` FROM `t_topic` t1 WHERE t1.`id` = ?) t2 ON t.`id` = t2.`id` WHERE t.`deleted` = ? AND t.`id` IS NOT NULL AND t.`id` = ? AND t.`id` IS NOT NULL AND t.`id` = ? AND t.`id` IS NOT NULL AND t2.`id` = ? AND t.`id` IS NOT NULL AND t2.`id` = ? AND (t.`title` LIKE ? OR t.`title` LIKE ? OR t.`title` LIKE ?)");
        listen.AssertParams("3(String),false(Boolean),3(String),3(String),3(String),3(String),%1%(String),%2%(String),%3%(String)");
    }

    [Fact, Upstream("QueryTest.java#query11_1")]
    public void Query11_1()
    {
        var sub = Db.Query<Topic>().Where(t => t.Id == "3");
        var sql = Db.Query<BlogEntity>().LeftJoin(sub, (a, b) => a.Id == b.Id).Where(o => o.Id != null && o.Id == "3").ToSql();
        Assert.Equal($"SELECT {Prefixed("t", BlogColumns)} FROM `t_blog` t LEFT JOIN (SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time` FROM `t_topic` t1 WHERE t1.`id` = ?) t2 ON t.`id` = t2.`id` WHERE t.`deleted` = ? AND t.`id` IS NOT NULL AND t.`id` = ?", sql);
    }

    [Fact, Upstream("QueryTest.java#query111")]
    public void Query111()
    {
        var sql = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where(o => o.Id == "3").Limit(1, 2).ToSql();
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ? LIMIT 2 OFFSET 1", sql);
    }

    [Fact, Upstream("QueryTest.java#query100")]
    public async Task Query100()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct);
        Assert.Equal(1.5, avg);
    }

    [Fact, Upstream("QueryTest.java#query101")]
    public async Task Query101()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct) ?? 1d;
        Assert.Equal(1.5, avg);
    }

    [Fact, Upstream("QueryTest.java#query101_1")]
    public async Task Query101_1()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct);
        Assert.Equal(1.5, avg);
    }

    [Fact, Upstream("QueryTest.java#query102")]
    public async Task Query102()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct) ?? -1d;
        Assert.Equal(-1d, avg);
    }

    [Fact, Upstream("QueryTest.java#query102_1")]
    public async Task Query102_1()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageDecimalAsync(o => o.Score, Ct) ?? 0m;
        Assert.Equal(1.2m, avg);
    }

    [Fact, Upstream("QueryTest.java#query102_2")]
    public async Task Query102_2()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).AverageDecimalAsync(o => o.Score, Ct);
        Assert.Null(avg);
    }

    [Fact, Upstream("QueryTest.java#query103")]
    public async Task Query103()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).AverageDecimalAsync(o => o.Star, Ct) ?? 0m;
        Assert.Equal(0m, avg);
    }

    [Fact, Upstream("QueryTest.java#query104")]
    public async Task Query104()
    {
        var avg = await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageDecimalAsync(o => o.Star, Ct) ?? 0m;
        Assert.Equal(1.5m, avg);
    }

    [Fact, Upstream("QueryTest.java#query27")]
    public void Query27()
    {
        var sql = Db.Query<BlogEntity>().Select<BlogEntityTest>().ToSql();
        Assert.Equal("SELECT t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ?", sql);
    }

    [Fact, Upstream("QueryTest.java#query30")]
    public async Task Query30()
    {
        var query = Db.QuerySql<BlogEntity>("SELECT * FROM `t_blog` t").Where(o => o.Id == "123");
        Assert.Equal("SELECT * FROM (SELECT * FROM `t_blog` t) t WHERE t.`id` = ?", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query31")]
    public async Task Query31()
    {
        var query = Db.QuerySql<BlogEntity>("SELECT * FROM `t_blog` t")
            .DisableInterceptors()
            .Where(o => o.Id == "123")
            .Select(o => new BlogEntity { Id = o.Id, Content = o.Content });
        Assert.Equal("SELECT t.`id`,t.`content` FROM (SELECT * FROM `t_blog` t) t WHERE t.`id` = ?", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query37")]
    public async Task Query37()
    {
        var newContent = Guid.NewGuid().ToString();
        Assert.Equal(1, await Db.ExecuteSqlAsync("update t_blog set content='" + newContent + "' where id='1'", cancellationToken: Ct));
        var blog = await Db.Query<BlogEntity>().WhereById("1").FirstOrDefaultAsync(Ct);
        Assert.Equal(newContent, blog?.Content);
    }

    [Fact, Upstream("QueryTest.java#query38")]
    public async Task Query38()
    {
        var newContent = Guid.NewGuid().ToString();
        Assert.Equal(1, await Db.ExecuteSqlAsync("update t_blog set content=? where id=?", [newContent, "1"], Ct));
        var blog = await Db.Query<BlogEntity>().WhereById("1").FirstOrDefaultAsync(Ct);
        Assert.Equal(newContent, blog?.Content);
    }

    [Fact, Upstream("QueryTest.java#query39", Deviation = "Java 的第 4、5 块把 url 选了两次（allFields 再加 url），C# 成员初始化不能给同一成员赋两次值，这两块只选一次 url")]
    public async Task Query39()
    {
        const string allColumns = "t.`title`,t.`content`,t.`url` AS `my_url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top`";

        var urlOnly = Db.Query<BlogEntity>().Select(o => new BlogEntityTest2 { Url = o.Url });
        Assert.Equal("SELECT t.`url` AS `my_url` FROM `t_blog` t WHERE t.`deleted` = ?", urlOnly.ToSql());
        Assert.True((await urlOnly.ToListAsync(Ct)).Count >= 100);

        var auto = Db.Query<BlogEntity>().Select<BlogEntityTest2>();
        Assert.Equal($"SELECT {allColumns} FROM `t_blog` t WHERE t.`deleted` = ?", auto.ToSql());
        var list = await auto.ToListAsync(Ct);
        Assert.True(list.Count >= 100);
        Assert.All(list, x => Assert.NotNull(x.Url));

        var excludeTitle = Db.Query<BlogEntity>().Select(o => new BlogEntityTest2
        {
            Content = o.Content, Url = o.Url, Star = o.Star, PublishTime = o.PublishTime, Score = o.Score,
            Status = o.Status, Order = o.Order, IsTop = o.IsTop, Top = o.Top,
        });
        Assert.Equal("SELECT t.`content`,t.`url` AS `my_url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ?", excludeTitle.ToSql());
        Assert.True((await excludeTitle.ToListAsync(Ct)).Count >= 100);
    }

    [Fact, Upstream("QueryTest.java#query42")]
    public async Task Query42()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "123").Where(o => o.Title == o.Url);
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ? AND `title` = `url`", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query45")]
    public async Task Query45()
    {
        var where = Db.Query<BlogEntity>().Where(o => o.Id == "123");
        var sql = Db.Query<Topic>().Where(o => Sql.Exists(where.Where(q => q.Id == o.Id))).ToSql();
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ? AND t1.`id` = t.`id`)", sql);

        var sub = Db.Query<BlogEntity>().Where(o => o.Id == "1");
        var x = await Db.Query<Topic>().Where(o => Sql.Exists(sub.Where(q => q.Id == o.Id))).ToListAsync(Ct);
        Assert.Single(x);
        Assert.Equal("1", x[0].Id);
    }

    [Fact, Upstream("QueryTest.java#query46")]
    public async Task Query46()
    {
        var sql = Db.Query<Topic>().Where(o => Sql.NotExists(Db.Query<BlogEntity>().Where(q => q.Id == "123").Where(q => q.Id == o.Id))).ToSql();
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE NOT EXISTS(SELECT 1 FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ? AND t1.`id` = t.`id`)", sql);

        var x = await Db.Query<Topic>().Where(o => Sql.NotExists(Db.Query<BlogEntity>().Where(q => q.Id == "1").Where(q => q.Id == o.Id))).ToListAsync(Ct);
        Assert.True(x.Count >= 99);
        Assert.DoesNotContain(x, t => t.Id == "1");
    }

    [Fact, Upstream("QueryTest.java#query47")]
    public async Task Query47()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "98");
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ?", query.ToSql());
        var blog = await query.FirstOrDefaultAsync(Ct);
        Assert.NotNull(blog);
        Assert.Equal("98", blog.Id);
        Assert.Equal("title98", blog.Title);
        Assert.True(blog.Top);
        Assert.True(blog.IsTop);
        Assert.Equal("content98", blog.Content);
        Assert.Equal("http://blog.easy-query.com/98", blog.Url);
        Assert.Equal(98, blog.Star);
        Assert.Null(blog.PublishTime);
        Assert.Equal(1.20m, blog.Score);
        Assert.Equal(1, blog.Status);
        Assert.Equal(117.60m, blog.Order);
    }

    [Fact, Upstream("QueryTest.java#query48")]
    public async Task Query48()
    {
        var id = Guid.NewGuid().ToString();
        Assert.Null(await Db.Query<BlogEntity>().Where(o => o.Id == id).MaxAsync(o => o.Star, Ct));
        Assert.Equal(12345, await Db.Query<BlogEntity>().Where(o => o.Id == id).MaxAsync(o => o.Star, Ct) ?? 12345);
    }

    [Fact, Upstream("QueryTest.java#query49")]
    public async Task Query49()
    {
        var id = Guid.NewGuid().ToString();
        Assert.Null(await Db.Query<BlogEntity>().Where(o => o.Id == id).MinAsync(o => o.Star, Ct));
        Assert.Equal(4567, await Db.Query<BlogEntity>().Where(o => o.Id == id).MinAsync(o => o.Star, Ct) ?? 4567);
    }

    [Fact, Upstream("QueryTest.java#query51")]
    public async Task Query51()
    {
        var sql = Db.Query<Topic>().Where(o => o.Id != null).Select(o => o.Id).Distinct().ToSql();
        Assert.Equal("SELECT DISTINCT t.`id` FROM `t_topic` t WHERE t.`id` IS NOT NULL", sql);
        var list = await Db.Query<Topic>().Where(o => o.Id != null).Select(o => o.Id).Distinct().ToListAsync(Ct);
        Assert.True(list.Count >= 100);
    }

    [Fact, Upstream("QueryTest.java#query58")]
    public void Query58()
    {
        var idQuery = Db.Query<BlogEntity>().Where(o => o.Id == "123").Select(o => o.Id);
        var sql = Db.Query<Topic>().Where(o => o.Id.In(idQuery)).ToSql();
        Assert.Equal("SELECT t.`id`,t.`stars`,t.`title`,t.`create_time` FROM `t_topic` t WHERE t.`id` IN (SELECT t1.`id` FROM `t_blog` t1 WHERE t1.`deleted` = ? AND t1.`id` = ?)", sql);
    }

    [Fact, Upstream("QueryTest.java#query5_1")]
    public async Task Query5_1()
    {
        using var listen = SqlCapture.Listen();
        string[] ids = ["97", "98"];
        var blogs = await Db.Query<BlogEntity>().Where(b => ids.Contains(b.Id!)).ToListAsync(Ct);
        Assert.Equal(2, blogs.Count);
        Assert.False(blogs[0].IsTop);
        Assert.True(blogs[1].Top);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` IN (?,?)");
        listen.AssertParams("false(Boolean),97(String),98(String)");
    }

    [Fact, Upstream("QueryTest.java#query5_2")]
    public async Task Query5_2()
    {
        using var listen = SqlCapture.Listen();
        string[] ids = ["97", "98"];
        var blogs = await Db.Query<BlogEntity>().Where(b => b.Star <= 100 && !ids.Contains(b.Id!)).ToListAsync(Ct);
        Assert.Equal(98, blogs.Count);
        listen.AssertSql($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `star` <= ? AND `id` NOT IN (?,?)");
        listen.AssertParams("false(Boolean),100(Integer),97(String),98(String)");
    }

    [Fact, Upstream("QueryTest.java#query59")]
    public async Task Query59()
    {
        var ids = Db.Query<BlogEntity>().Where(o => o.Id == "123").Select(o => o.Id);
        Assert.Empty(await Db.Query<Topic>().Where(o => o.Id.In(ids)).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query60")]
    public async Task Query60()
    {
        var ids = Db.Query<BlogEntity>().Where(o => o.Id == "1").Select(o => o.Id);
        Assert.Single(await Db.Query<Topic>().Where(o => o.Id.In(ids)).ToListAsync(Ct));
    }

    private const string Vo1Sql = "SELECT t.`score`,t.`status` AS `status1`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? LIMIT 1";

    [Fact, Upstream("QueryTest.java#query72")]
    public async Task Query72()
    {
        var vo = await Db.Query<BlogEntity>().Where(o => o.Id == "2").Select<BlogEntityVO1>().FirstOrDefaultAsync(Ct);
        Assert.NotNull(vo);
        Assert.Equal(1, vo.Status);
        Assert.Equal(Vo1Sql, Db.Query<BlogEntity>().Where(o => o.Id == "2").Select<BlogEntityVO1>().Take(1).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query73")]
    public async Task Query73()
    {
        Assert.NotNull(await Db.Query<BlogEntity>().Where(o => o.Id == "2").Select<BlogEntityVO1>().FirstOrDefaultAsync(Ct));
        Assert.Equal(Vo1Sql, Db.Query<BlogEntity>().Where(o => o.Id == "2").Select<BlogEntityVO1>().Take(1).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query74")]
    public async Task Query74()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "2").SelectExcept<BlogEntityVO1>(o => o.Id);
        Assert.NotNull(await query.FirstOrDefaultAsync(Ct));
        Assert.Equal(Vo1Sql, query.Take(1).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query75")]
    public async Task Query75()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "2").Select(o => new BlogEntityVO1 { Score = o.Order });
        Assert.Equal(1.2m * 2, (await query.FirstOrDefaultAsync(Ct))?.Score);
        Assert.Equal("SELECT t.`order` AS `score` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? LIMIT 1", query.Take(1).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query76")]
    public async Task Query76()
    {
        var query = Db.Query<BlogEntity>().Where(o => o.Id == "2").SelectExcept<BlogEntityVO1>(o => o.Score);
        Assert.Null((await query.FirstOrDefaultAsync(Ct))?.Score);
        Assert.Equal("SELECT t.`status` AS `status1`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? LIMIT 1", query.Take(1).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query82")]
    public async Task Query82()
    {
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx").OrderByDescending(s => s.Id).ThenByDescending(s => s.IdCard);
        Assert.Equal("SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE `id` = ? ORDER BY `id` DESC,`id_card` DESC", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    private const string SysUserSelect = "SELECT `id`,`create_time`,`username`,`phone`,`id_card`,`address` FROM `easy-query-test`.`t_sys_user` WHERE `id` = ? ORDER BY ";

    [Fact, Upstream("QueryTest.java#query83"), Upstream("QueryTest.java#query83_1")]
    public async Task Query83()
    {
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx").OrderBy(s => s.Id).ThenBy(s => s.IdCard);
        Assert.Equal(SysUserSelect + "`id` ASC,`id_card` ASC", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query84"), Upstream("QueryTest.java#query85"), Upstream("QueryTest.java#query88")]
    public async Task Query84()
    {
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx")
            .OrderByDescendingIf(false, s => s.Id).OrderByDescendingIf(false, s => s.IdCard)
            .OrderByDescendingIf(true, s => s.IdCard).OrderByDescendingIf(true, s => s.Id);
        Assert.Equal(SysUserSelect + "`id_card` DESC,`id` DESC", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query86"), Upstream("QueryTest.java#query87")]
    public async Task Query86()
    {
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx")
            .OrderByIf(false, s => s.Id).OrderByIf(false, s => s.IdCard)
            .OrderBy(s => s.IdCard).ThenBy(s => s.Id);
        Assert.Equal(SysUserSelect + "`id_card` ASC,`id` ASC", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query88_1")]
    public void Query88_1()
    {
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx").OrderByDescending(s => s.IdCard).ThenByDescending(s => s.Id);
        Assert.Equal(SysUserSelect + "`id_card` DESC,`id` DESC", query.ToSql());
    }

    [Fact, Upstream("QueryTest.java#query90_1")]
    public async Task Query90_1() =>
        Assert.Null(await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).SumAsync(o => o.Score, Ct));

    [Fact, Upstream("QueryTest.java#query92")]
    public async Task Query92() =>
        Assert.Equal(2, await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).MaxAsync(o => o.Star, Ct) ?? 0);

    [Fact, Upstream("QueryTest.java#query93")]
    public async Task Query93() =>
        Assert.Equal(-1, await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).MaxAsync(o => o.Star, Ct) ?? -1);

    [Fact, Upstream("QueryTest.java#query94")]
    public async Task Query94() =>
        Assert.Null(await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).MaxAsync(o => o.Star, Ct));

    [Fact, Upstream("QueryTest.java#query95")]
    public async Task Query95() =>
        Assert.Equal(9, await Db.Query<BlogEntity>().Where(o => new[] { "1x", "9" }.Contains(o.Id!)).MaxAsync(o => o.Star, Ct));

    [Fact, Upstream("QueryTest.java#query96")]
    public async Task Query96() =>
        Assert.Equal(1.5d, await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct) ?? 0d);

    [Fact, Upstream("QueryTest.java#query97")]
    public async Task Query97() =>
        Assert.Equal(1.5d, await Db.Query<BlogEntity>().Where(o => new[] { "1", "2" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct));

    [Fact, Upstream("QueryTest.java#query98")]
    public async Task Query98() =>
        Assert.Null(await Db.Query<BlogEntity>().Where(o => new[] { "1x", "2x" }.Contains(o.Id!)).AverageAsync(o => o.Star, Ct));

    [Fact, Upstream("QueryTest.java#query16")]
    public void Query16()
    {
        var sql = Db.Query<Topic>()
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, b2) => b2.Title != null)
            .GroupBy((t, b2) => b2.Id)
            .Select(g => new BlogEntity { Id = g.Key, Score = g.Sum(e => e.Item2.Score) });
        const string inner = "SELECT t1.`id` AS `id`,SUM(t1.`score`) AS `score` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL GROUP BY t1.`id`";
        Assert.Equal($"SELECT COUNT(1) FROM ({inner}) t2", sql.SelectRaw("COUNT(1)").ToSql());
        Assert.Equal(inner + " LIMIT 2 OFFSET 2", sql.Limit(2, 2).ToSql());
        Assert.Equal($"SELECT COUNT(t2.`id`) FROM ({inner}) t2", sql.Select(o => Sql.Count(o.Id)).ToSql());
    }

    private Query<TopicGroupTestDTO> TopicGroup(string id) => Db.Query<Topic>()
        .Where(o => o.Id == id)
        .GroupBy(o => o.Id)
        .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(e => e.Id) });

    [Fact, Upstream("QueryTest.java#query19")]
    public async Task Query19()
    {
        var topics = await Db.Query<BlogEntity>()
            .LeftJoin(TopicGroup("3"), (a, b) => a.Id == b.Id)
            .Where(o => o.Id != null && o.Id == "3")
            .ToListAsync(Ct);
        Assert.Single(topics);
    }

    [Fact, Upstream("QueryTest.java#query19_1")]
    public void Query19_1()
    {
        var sql = Db.Query<BlogEntity>()
            .LeftJoin(TopicGroup("3"), (a, b) => a.Id == b.Id)
            .Where(o => o.Id != null && o.Id == "3")
            .ToSql();
        Assert.Equal($"SELECT {Prefixed("t", BlogColumns)} FROM `t_blog` t LEFT JOIN (SELECT t1.`id` AS `id`,COUNT(t1.`id`) AS `id_count` FROM `t_topic` t1 WHERE t1.`id` = ? GROUP BY t1.`id`) t3 ON t.`id` = t3.`id` WHERE t.`deleted` = ? AND t.`id` IS NOT NULL AND t.`id` = ?", sql);
    }

    [Fact, Upstream("QueryTest.java#query20")]
    public async Task Query20()
    {
        var sql = TopicGroup("3");
        Assert.Equal("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `id_count` FROM `t_topic` t WHERE t.`id` = ? GROUP BY t.`id`", sql.ToSql());
        var list = await sql.ToListAsync(Ct);
        Assert.Single(list);
        Assert.Equal(1, list[0].IdCount);
        Assert.Equal("3", list[0].Id);
    }

    [Fact, Upstream("QueryTest.java#query21", Deviation = "Java 的 Select.of(key1(), …) 不写别名而 .as(…) 写别名；C# 成员初始化无法区分这两种写法，分组投影统一写别名")]
    public async Task Query21()
    {
        var sql = TopicGroup("3");
        Assert.Equal("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `id_count` FROM `t_topic` t WHERE t.`id` = ? GROUP BY t.`id`", sql.ToSql());
        var list = await sql.ToListAsync(Ct);
        Assert.Equal(1, list[0].IdCount);
    }

    [Fact, Upstream("QueryTest.java#query43")]
    public async Task Query43()
    {
        var first = await Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(b => b.Id)
            .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(e => e.Id) })
            .OrderBy(o => o.Id).FirstOrDefaultAsync(Ct);
        Assert.Null(first);
        var second = await Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(b => b.Id)
            .OrderBy(g => g.Key)
            .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(e => e.Id) })
            .FirstOrDefaultAsync(Ct);
        Assert.Null(second);
    }

    [Fact, Upstream("QueryTest.java#query44")]
    public void Query44()
    {
        var ordered = Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(b => b.Id)
            .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(e => e.Id) })
            .OrderBy(o => o.Id);
        Assert.Equal("SELECT t1.`id` AS `id`,t1.`id_count` AS `id_count` FROM (SELECT t.`id` AS `id`,COUNT(t.`id`) AS `id_count` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY t.`id`) t1 ORDER BY t1.`id` ASC", ordered.ToSql());

        var groupOrdered = Db.Query<BlogEntity>().Where(o => o.Id == "123").GroupBy(b => b.Id)
            .OrderBy(g => g.Key)
            .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(e => e.Id) });
        Assert.Equal("SELECT t.`id` AS `id`,COUNT(t.`id`) AS `id_count` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? GROUP BY t.`id` ORDER BY t.`id` ASC", groupOrdered.ToSql());
    }

    [Fact, Upstream("QueryTest.java#query108")]
    public async Task Query108()
    {
        Assert.Equal(33, await Db.Query<BlogEntity>().Where(o => new[] { "11", "22" }.Contains(o.Id!)).SumAsync(o => o.Star, Ct));
        Assert.Equal(33, await Db.Query<BlogEntity>().WhereByIds(new[] { "11", "22" }).SumAsync(o => o.Star, Ct));
        Assert.Equal(35, await Db.Query<BlogEntity>().WhereByIds(new[] { "13", "22" }).SumAsync(o => o.Star, Ct));
    }
}
