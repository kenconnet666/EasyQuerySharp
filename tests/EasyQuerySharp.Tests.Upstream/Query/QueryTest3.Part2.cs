using EasyQuerySharp.Filtering;
using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

// QueryTest3.java 后半：标量投影、动态条件、流式读取、OR 组、原生排序、动态排序、泛型主键、原生 SQL 分页
public sealed partial class QueryTest3
{
    [Fact, Upstream("QueryTest3.java#queryBasic1")]
    public async Task QueryBasic1()
    {
        var query = Db.Query<Topic>().Where(o => o.Id == "1").Select(o => o.Id);
        Assert.Equal(["1"], await query.ToListAsync(Ct));
        Assert.Equal("SELECT t.`id` FROM `t_topic` t WHERE t.`id` = ?", query.ToSql());
    }

    [Fact, Upstream("QueryTest3.java#queryBasic2")]
    public async Task QueryBasic2()
    {
        var query = Db.Query<Topic>().Where(o => o.Id == "1").Select(o => o.Stars);
        Assert.Equal([101], await query.ToListAsync(Ct));
        Assert.Equal("SELECT t.`stars` FROM `t_topic` t WHERE t.`id` = ?", query.ToSql());
    }

    // Java 每个条件带一个布尔开关：C# 用 WhereIf；区间整体用 Sql.Group 保持括号
    [Fact, Upstream("QueryTest3.java#dynamicWhere")]
    public async Task DynamicWhere()
    {
        foreach (var order in new decimal?[] { 1m, null })
        {
            string? content = "标题";
            DateTime? begin = DateTime.Now, end = DateTime.Now;
            List<int> statuses = [1, 2];
            var query = Db.Query<BlogEntity>()
                .WhereIf(!string.IsNullOrWhiteSpace(content), b => b.Content!.Contains(content!))
                .WhereIf(order != null, b => b.Order == order)
                .WhereIf(begin != null && end != null, b => Sql.Group(b.PublishTime >= begin && b.PublishTime <= end))
                .WhereIf(statuses.Count > 0, b => statuses.Contains(b.Status!.Value));
            Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `content` LIKE ?" + (order is null ? "" : " AND `order` = ?")
                         + " AND (`publish_time` >= ? AND `publish_time` <= ?) AND `status` IN (?,?)", query.ToSql());
            Assert.Empty(await query.ToListAsync(Ct));
        }
    }

    /// <summary>对应 Java：dto/BlogQuery2Request。</summary>
    public sealed class BlogQuery2Request
    {
        [Filter] public string? Title { get; set; }

        [Filter] public string? Content { get; set; }

        [Filter(FilterOp.Equal)] public int? Star { get; set; }

        [Filter(FilterOp.GreaterThanOrEqual, Property = "PublishTime")] public DateTime? PublishTimeBegin { get; set; }

        [Filter(FilterOp.LessThanOrEqual, Property = "PublishTime")] public DateTime? PublishTimeEnd { get; set; }

        [Filter(FilterOp.GreaterThanOrEqual)] public decimal? Score { get; set; }

        [Filter(FilterOp.LessThanOrEqual)] public int? Status { get; set; }

        [Filter(FilterOp.Equal)] public decimal? Order { get; set; }

        [Filter(FilterOp.NotEqual)] public bool? IsTop { get; set; }

        [Filter(FilterOp.In, Property = "Status")] public List<int> StatusList { get; set; } = [];

        [Filter(FilterOp.NotIn, Property = "Status")] public List<int> StatusNotList { get; set; } = [];
    }

    [Fact, Upstream("QueryTest3.java#dynamicWhere1")]
    public async Task DynamicWhere1()
    {
        var full = new BlogQuery2Request { Order = 1m, Content = "标题", PublishTimeBegin = DateTime.Now, PublishTimeEnd = DateTime.Now, StatusList = [1, 2] };
        var q1 = Db.Query<BlogEntity>().FilterBy(full);
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `content` LIKE ? AND `publish_time` >= ? AND `publish_time` <= ? AND `order` = ? AND `status` IN (?,?)", q1.ToSql());
        Assert.Empty(await q1.ToListAsync(Ct));
        var partial = new BlogQuery2Request { Content = "标题", PublishTimeEnd = DateTime.Now, StatusList = [1, 2] };
        var q2 = Db.Query<BlogEntity>().FilterBy(partial);
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `content` LIKE ? AND `publish_time` <= ? AND `status` IN (?,?)", q2.ToSql());
        Assert.Empty(await q2.ToListAsync(Ct));
    }

    // Java 的 toStreamResult：C# 的 ToAsyncEnumerable（fetch size 由驱动决定）
    [Theory, Upstream("QueryTest3.java#query9"), Upstream("QueryTest3.java#query9_x"), Upstream("QueryTest3.java#query9_1")]
    [InlineData(0)]
    public async Task Query9(int _)
    {
        var begin = new DateTime(2020, 1, 1, 1, 1, 1);
        var i = 0;
        await foreach (var blog in Db.Query<BlogEntity>().Where(o => o.Star <= 100).OrderBy(o => o.CreateTime).ToAsyncEnumerable(Ct))
        {
            var index = i.ToString();
            Assert.Equal(index, blog.Id);
            Assert.Equal(index, blog.CreateBy);
            Assert.Equal(begin.AddDays(i), blog.CreateTime);
            Assert.Equal("title" + index, blog.Title);
            Assert.Equal(i, blog.Star);
            Assert.Equal(1.2m * i, blog.Order);
            Assert.Equal(i % 2 == 0, blog.IsTop);
            Assert.False(blog.Deleted);
            i++;
        }
        Assert.Equal(100, i);
    }

    [Fact, Upstream("QueryTest3.java#queryToList1")]
    public async Task QueryToList1() => Assert.NotEmpty(await Db.Query<Topic>().ToListAsync(Ct));

    [Fact, Upstream("QueryTest3.java#orTest")]
    public void OrTest() =>
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE (`title` LIKE ? OR `id` LIKE ?)",
            Db.Query<Topic>().Where(t => t.Title!.Contains("123") || t.Id!.Contains("123")).ToSql());

    // Java 的 or 组里条件全部不生效时整组消失：C# 里不加这些条件
    [Fact, Upstream("QueryTest3.java#orTest1"), Upstream("QueryTest3.java#orTest2")]
    public void OrTest1() =>
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `create_time` = ?",
            Db.Query<Topic>().Where(t => t.CreateTime == DateTime.Now).WhereIf(false, t => t.Title!.Contains("123") || t.Id!.Contains("123")).ToSql());

    [Fact, Upstream("QueryTest3.java#orTest3"), Upstream("QueryTest3.java#orTest4")]
    public void OrTest3()
    {
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`", Db.Query<Topic>().WhereIf(false, t => t.CreateTime == DateTime.Now).ToSql());
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ?", Db.Query<BlogEntity>().WhereIf(false, t => t.CreateTime == DateTime.Now).ToSql());
    }

    [Fact, Upstream("QueryTest3.java#orTest5")]
    public async Task OrTest5() =>
        Assert.Equal(10, (await Db.Query<BlogEntity>().WhereIf(false, t => t.CreateTime == DateTime.Now).ToPageAsync(1, 10, Ct)).Rows.Count);

    [Fact, Upstream("QueryTest3.java#extendsUserTest", NotApplicable = "检查的是 Java 生成的 DataReader 链（反射字段），C# 用表达式编译的物化器，没有对应结构")]
    public void ExtendsUserTest() { }

    [Fact, Upstream("QueryTest3.java#testSelectAs1")]
    public void TestSelectAs1() =>
        Assert.Equal("SELECT 100 - t.`id` AS `stars` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ?",
            Db.Query<BlogEntity>().Where(o => o.Id == "123").Select(o => new Topic { Stars = Sql.Raw<int?>("100 - {0}", o.Id) }).ToSql());

    [Fact, Upstream("QueryTest3.java#testOrder1")]
    public void TestOrder1() =>
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? AND `id` = ? ORDER BY `id` ASC,user_name ASC",
            Db.Query<BlogEntity>().Where(o => o.Id == "123").OrderBy(o => o.Id).OrderByRaw("user_name " + (1 == 1 ? "ASC" : "DESC")).ToSql());

    /// <summary>对应 Java：QueryTest3.UISort（属性名 → 是否升序）。</summary>
    private sealed class UiSort(IEnumerable<(string Property, bool Ascending)> sort) : ISortRequest
    {
        public void ConfigureSort(SortBuilder builder)
        {
            foreach (var (property, ascending) in sort) builder.OrderBy(property, ascending);
        }
    }

    [Fact, Upstream("QueryTest3.java#orderTest1")]
    public void OrderTest1() =>
        Assert.Equal($"SELECT {BlogColumns} FROM `t_blog` WHERE `deleted` = ? ORDER BY `id` ASC,`title` DESC",
            Db.Query<BlogEntity>().SortBy(new UiSort([("id", true), ("title", false)])).ToSql());

    [Fact, Upstream("QueryTest3.java#orderTest2")]
    public void OrderTest2()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Query<BlogEntity>().SortBy(new UiSort([("id1", true), ("title", false)])));
        Assert.Contains("id1", ex.Message, StringComparison.Ordinal);
    }

    [Fact, Upstream("QueryTest3.java#testGenericKey")]
    public void TestGenericKey() =>
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` LIMIT 1", Db.Query<TopicGenericKey>().Take(1).ToSql());

    [Fact, Upstream("QueryTest3.java#testGenericKey1")]
    public async Task TestGenericKey1()
    {
        var item = Assert.Single(await Db.Query<TopicGenericKey>().WhereById("1").Take(1).ToListAsync(Ct));
        Assert.Equal("1", item.Id);
    }

    // Java 的自定义分页器 MyPager：C# 的 ToPageAsync 已返回总数与当前页
    [Fact, Upstream("QueryTest3.java#testGenericKey2")]
    public async Task TestGenericKey2()
    {
        var page = await Db.Query<TopicGenericKey>().WhereById("1").ToPageAsync(1, 2, Ct);
        Assert.Equal(1, page.Total);
        Assert.Equal("1", page.Rows[0].Id);
        Assert.Single((await Db.Query<TopicGenericKey>().WhereById("1").Select(t => new TopicTypeVO { Id = t.Id, Stars = t.Stars, Title = t.Title }).ToPageAsync(1, 2, Ct)).Rows);
    }

    [Fact, Upstream("QueryTest3.java#nativePage1")]
    public async Task NativePage1()
    {
        Assert.Equal(20, (await Db.QuerySql<Dictionary<string, object?>>("select * from t_topic").ToPageAsync(1, 20, Ct)).Rows.Count);
        Assert.Equal(20, (await Db.QuerySql<Dictionary<string, object?>>("select * from t_topic").ToPageAsync(2, 20, Ct)).Rows.Count);
    }

    [Fact, Upstream("QueryTest3.java#nativePage2")]
    public async Task NativePage2()
    {
        var query = Db.QuerySql<Dictionary<string, object?>>("select * from t_topicxx");
        Assert.Equal("SELECT COUNT(*) FROM (select * from t_topicxx) t", (await Assert.ThrowsAsync<EasyQueryException>(() => query.CountAsync(Ct))).Sql);
        Assert.Equal("SELECT * FROM (select * from t_topicxx) t LIMIT 10 OFFSET 1", (await Assert.ThrowsAsync<EasyQueryException>(() => query.Limit(1, 10).ToListAsync(Ct))).Sql);
        Assert.Equal("SELECT * FROM (select * from t_topicxx) t LIMIT 10 OFFSET 2", (await Assert.ThrowsAsync<EasyQueryException>(() => query.Limit(2, 10).ToListAsync(Ct))).Sql);
    }

    [Fact, Upstream("QueryTest3.java#nativePage3")]
    public async Task NativePage3()
    {
        var page = await Db.QuerySql<Dictionary<string, object?>>("select * from t_topic where id = ?", "1").ToPageAsync(1, 20, Ct);
        Assert.Single(page.Rows);
        Assert.Equal(1, page.Total);
    }

    // Java 的库里 blog 1 的 content 被其它用例改成了含 "-" 的值；C# 的种子数据是 content1，按实际内容过滤
    [Fact, Upstream("QueryTest3.java#nativePage4", Deviation = "过滤值按 C# 种子数据的 content 调整（Java 依赖被其它用例改过的数据）")]
    public async Task NativePage4()
    {
        var page = await Db.QuerySql<BlogEntity>("select * from t_blog where id = ?", "1").Where(o => o.Content!.Contains("content")).ToPageAsync(1, 20, Ct);
        Assert.Single(page.Rows);
        Assert.Equal(1, page.Total);
    }

    // nativePage5 把常量参数化，nativePage6 把常量写成字面量
    [Theory, Upstream("QueryTest3.java#nativePage5", Deviation = "同 nativePage4"), Upstream("QueryTest3.java#nativePage6", Deviation = "同 nativePage4")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativePage5(bool literal)
    {
        var source = Db.QuerySql<BlogEntity>("select * from t_blog where id = ?", "1").Where(o => o.Content!.Contains("content"));
        var page = literal
            ? await source.Select(t => new BlogEntity { Id = t.Id, Content = Sql.Raw<string>("CONCAT('MySQL','5.7-',{0})", t.Id) }).ToPageAsync(1, 20, Ct)
            : await source.Select(t => new BlogEntity { Id = t.Id, Content = Sql.Raw<string>("CONCAT({0},{1},{2})", "MySQL", "5.7-", t.Id) }).ToPageAsync(1, 20, Ct);
        Assert.Equal(1, page.Total);
        var blog = Assert.Single(page.Rows);
        Assert.Equal("1", blog.Id);
        Assert.Equal("MySQL5.7-1", blog.Content);
    }
}
