namespace EasyQuerySharp.Tests.Upstream.Queries;

// QueryTest.java 里 join 后的投影、计数包装、分组派生表用例
public sealed partial class QueryTest
{
    private const string BlogColumnsWithoutId =
        "`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top`";

    private const string GroupedBlogScore =
        "SELECT IFNULL(t1.`id`,?) AS `id`,SUM(t1.`score`) AS `score` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL GROUP BY IFNULL(t1.`id`,?)";

    // Java 的 nullOrDefault("") 与 nullOrEmpty() 都渲染成 IFNULL(x, '')，C# 统一写 ?? ""
    [Fact, Upstream("QueryTest.java#query16_1_1"), Upstream("QueryTest.java#query16_1_2"), Upstream("QueryTest.java#query16_1_3")]
    public void Query16_1_1()
    {
        var query = Db.Query<Topic>()
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, t1) => t1.Title != null)
            .GroupBy((t, t1) => t1.Id ?? "")
            .Select(g => new BlogEntity { Id = g.Key, Score = g.Sum(x => x.Item2.Score) });
        Assert.Equal("SELECT COUNT(1) FROM (" + GroupedBlogScore + ") t2", query.SelectRaw("COUNT(1)").ToSql());
        Assert.Equal(GroupedBlogScore + " LIMIT 2 OFFSET 2", query.Limit(2, 2).ToSql());
        Assert.Equal("SELECT COUNT(t2.`id`) FROM (" + GroupedBlogScore + ") t2", query.Select(o => Sql.Count(o.Id)).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query19_2"), Upstream("QueryTest.java#query19_3")]
    public void Query19_2()
    {
        var grouped = Db.Query<Topic>().Where(o => o.Id == "3").GroupBy(o => o.Id)
            .Select(g => new TopicGroupTestDTO { Id = g.Key, IdCount = (int)g.Count(x => x.Id) });
        var sql = Db.Query<BlogEntity>().LeftJoin(grouped, (a, b) => a.Id == b.Id)
            .Where((a, b) => a.Id != null && a.Id == "3")
            .ToSql();
        Assert.Equal("SELECT t." + Prefixed("t", BlogColumns)[2..] + " FROM `t_blog` t LEFT JOIN (SELECT t1.`id` AS `id`,COUNT(t1.`id`) AS `id_count` FROM `t_topic` t1 WHERE t1.`id` = ? GROUP BY t1.`id`) t3 ON t.`id` = t3.`id` WHERE t.`deleted` = ? AND t.`id` IS NOT NULL AND t.`id` = ?", sql);
    }

    // Java 的 FETCHER.allFieldsExclude(id)：C# 用成员初始化列出要的列
    private static readonly System.Linq.Expressions.Expression<Func<Topic, BlogEntity, BlogEntity>> BlogWithoutId = (t, t1) => new BlogEntity
    {
        CreateTime = t1.CreateTime, UpdateTime = t1.UpdateTime, CreateBy = t1.CreateBy, UpdateBy = t1.UpdateBy, Deleted = t1.Deleted,
        Title = t1.Title, Content = t1.Content, Url = t1.Url, Star = t1.Star, PublishTime = t1.PublishTime, Score = t1.Score,
        Status = t1.Status, Order = t1.Order, IsTop = t1.IsTop, Top = t1.Top,
    };

    [Fact, Upstream("QueryTest.java#query17")]
    public void Query17()
    {
        var query = Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, t1) => t1.Title != null && t.Id == "3")
            .Select(BlogWithoutId);
        var body = "SELECT " + Prefixed("t1", BlogColumnsWithoutId) + " FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL AND t.`id` = ?";
        Assert.Equal("SELECT COUNT(1) FROM (" + body + ") t2", query.SelectRaw("COUNT(1)").ToSql());
        Assert.Equal(body + " LIMIT 2 OFFSET 2", query.Limit(2, 2).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query26")]
    public void Query26()
    {
        var sql = Db.Query<Topic>().AsTable("t_topic_123")
            .InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id).AsTable("x_t_blog")
            .Where((t, t1) => t1.Title != null && t.Id == "3")
            .Select((t, t1) => t1)
            .ToSql();
        Assert.Equal("SELECT " + Prefixed("t1", BlogColumns) + " FROM `t_topic_123` t INNER JOIN `x_t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL AND t.`id` = ?", sql);
    }

    [Fact, Upstream("QueryTest.java#query40")]
    public async Task Query40()
    {
        var query = Db.Query<Topic>().InnerJoin<BlogEntity>((t, t1) => t.Id == t1.Id)
            .Where((t, t1) => t1.Title != null)
            .OrderByDescending((t, t1) => t1.PublishTime)
            .Select((t, t1) => new BlogEntity { PublishTime = t1.PublishTime, Id = t1.Id, Score = t1.Score });
        using var listen = SqlCapture.Listen();
        await query.CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(*) FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL");
        Assert.Equal("SELECT t1.`publish_time`,t1.`id`,t1.`score` FROM `t_topic` t INNER JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t1.`title` IS NOT NULL ORDER BY t1.`publish_time` DESC LIMIT 20",
            query.Limit(0, 20).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query78")]
    public async Task Query78()
    {
        var query = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id == "2")
            .Select((t, t1) => new BlogEntityVO2
            {
                Title = t1.Title, Content = t1.Content, Url = t1.Url, Star = t1.Star, PublishTime = t1.PublishTime, Score = t1.Score,
                Status = t1.Status, Order = t1.Order, IsTop = t1.IsTop, Top = t1.Top, Id = t.Id,
            });
        Assert.NotNull(await query.FirstOrDefaultAsync(Ct));
        Assert.Equal("SELECT t1.`title`,t1.`content`,t1.`url`,t1.`star`,t1.`publish_time`,t1.`score`,t1.`status`,t1.`order`,t1.`is_top`,t1.`top`,t.`id` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ? LIMIT 1",
            query.Take(1).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query79", NotApplicable = "Java 允许投影里两列都映射到 id、后者覆盖前者；C# 成员初始化同一成员不能赋值两次")]
    public void Query79() { }

    [Fact, Upstream("QueryTest.java#query68")]
    public async Task Query68()
    {
        var now = DateTime.Now;
        var query = Db.Query<SysUser>().Where(o => o.Id == "123xxx")
            .Select(o => new SysUser { Id = o.Id, Username = o.Username, Phone = o.Phone, IdCard = o.IdCard, Address = o.Address, CreateTime = o.CreateTime ?? now });
        Assert.Equal("SELECT t.`id`,t.`username`,t.`phone`,t.`id_card`,t.`address`,IFNULL(t.`create_time`,?) AS `create_time` FROM `easy-query-test`.`t_sys_user` t WHERE t.`id` = ?", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }
}
