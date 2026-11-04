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

    [Fact, Upstream("QueryTest.java#query108")]
    public async Task Query108()
    {
        Assert.Equal(33, await Db.Query<BlogEntity>().Where(o => new[] { "11", "22" }.Contains(o.Id!)).SumAsync(o => o.Star, Ct));
        Assert.Equal(33, await Db.Query<BlogEntity>().WhereByIds(new[] { "11", "22" }).SumAsync(o => o.Star, Ct));
        Assert.Equal(35, await Db.Query<BlogEntity>().WhereByIds(new[] { "13", "22" }).SumAsync(o => o.Star, Ct));
    }
}
