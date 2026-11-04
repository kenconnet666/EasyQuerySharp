using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>QueryTest18 里的 include：Java 的 include(s -> s.blogs(), b -> b.include(…)) 对应 C# 的 Include(s => s.Blogs, q => q.Include(…))。</summary>
public sealed partial class QueryTest18
{
    [Fact, Upstream("QueryTest18.java#test14")]
    public async Task Test14()
    {
        var users = await Db.Query<SysUser>().Include(s => s.MyBlog).Include(s => s.Blogs, b => b.Include(x => x.Users)).ToListAsync(Ct);
        Assert.NotEmpty(users);
        Assert.All(users, u => Assert.NotNull(u.Blogs));

        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b2) => t.Id == b2.Id)
            .Select((t1, b2) => new { t1.Id, t1.Stars, b2.CreateTime }).Distinct().ToPageAsync(1, 2, Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT COUNT(*) FROM (SELECT DISTINCT t.`id`,t.`stars`,t1.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id`) t2");
        listen.AssertParams(0, "false(Boolean)");
        listen.AssertSql(1, "SELECT DISTINCT t.`id`,t.`stars`,t1.`create_time` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` LIMIT 2");
    }

    // include 之后在内存里给每个用户的博客排序
    [Fact, Upstream("QueryTest18.java#esss")]
    public async Task Esss()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(s => s.Blogs).ToListAsync(Ct);
        foreach (var user in users) user.Blogs = [.. user.Blogs!.OrderBy(b => b.Score)];
        Assert.Equal(2, listen.Events.Count);
        Assert.StartsWith("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,", listen.Events[1].Statement.Text, StringComparison.Ordinal);
        Assert.EndsWith("FROM `t_blog` t WHERE t.`deleted` = ? AND t.`title` IN (" + string.Join(",", Enumerable.Repeat("?", users.Count)) + ")", listen.Events[1].Statement.Text, StringComparison.Ordinal);
        Assert.All(users, u => Assert.All(u.Blogs!, b => Assert.Equal(u.Id, b.Title)));
    }
}
