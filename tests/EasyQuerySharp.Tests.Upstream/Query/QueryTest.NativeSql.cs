using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

// QueryTest.java 里的原生 SQL、字典结果、值转换（枚举 / JSON）用例
public sealed partial class QueryTest
{
    [Fact, Upstream("QueryTest.java#query32")]
    public async Task Query32() =>
        Assert.Single(await Db.SqlQueryAsync<BlogEntity>("SELECT * FROM `t_blog` t where t.id=?", ["1"], Ct));

    [Fact, Upstream("QueryTest.java#query33")]
    public async Task Query33() =>
        Assert.True((await Db.SqlQueryAsync<BlogEntity>("SELECT * FROM `t_blog` t", cancellationToken: Ct)).Count >= 100);

    [Fact, Upstream("QueryTest.java#query34")]
    public async Task Query34() =>
        Assert.Empty(await Db.SqlQueryAsync<BlogEntity>("SELECT * FROM `t_blog` t where t.id=?", ["1xx"], Ct));

    [Fact, Upstream("QueryTest.java#query35")]
    public async Task Query35() =>
        Assert.True((await Db.SqlQueryAsync<Dictionary<string, object?>>("SELECT * FROM `t_blog` t", cancellationToken: Ct)).Count >= 100);

    [Fact, Upstream("QueryTest.java#query36")]
    public async Task Query36() =>
        Assert.Single(await Db.SqlQueryAsync<Dictionary<string, object?>>("SELECT * FROM `t_blog` t  where t.id=?", ["1"], Ct));

    [Theory, Upstream("QueryTest.java#query7_1"), Upstream("QueryTest.java#query7_2")]
    [InlineData("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `star` <= ? ORDER BY `create_time` ASC")]
    // 列别名是属性名（createTime）时也能映射
    [InlineData("SELECT `id`,`create_time` AS createTime,`update_time` AS updateTime,`create_by` AS createBy,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time` AS publishTime,`score`,`status`,`order`,`is_top`  AS isTop,`top` FROM `t_blog` WHERE `deleted` = ? AND `star` <= ? ORDER BY `create_time` ASC")]
    public async Task Query7_1(string sql)
    {
        var blogs = await Db.SqlQueryAsync<BlogEntity>(sql, [false, 100], Ct);
        var begin = new DateTime(2020, 1, 1, 1, 1, 1);
        Assert.Equal(100, blogs.Count);
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

    [Fact, Upstream("QueryTest.java#query22")]
    public async Task Query22() =>
        Assert.Equal(100, (await Db.Query<BlogEntity>().Where(o => o.Star <= 100).ToDictionariesAsync(Ct)).Count);

    [Fact, Upstream("QueryTest.java#query23")]
    public async Task Query23()
    {
        var map = await Db.Query<BlogEntity>().FirstDictionaryOrDefaultAsync(Ct);
        Assert.NotNull(map);
        // 键忽略大小写
        Assert.True(map.ContainsKey("create_Time"));
    }

    [Fact, Upstream("QueryTest.java#query80", Deviation = "C# 投影的成员名不会重复；重复列名读成字典时报错，用原生 SQL 验证")]
    public async Task Query80()
    {
        var query = Db.Query<Topic>().LeftJoin<BlogEntity>((t, t1) => t.Id == t1.Id).Where((t, t1) => t.Id == "2")
            .Select((t, t1) => new { BlogId = t1.Id, t.Id });
        // 两列同名 id：读成字典时报重复列名，而不是静默覆盖
        var sql = query.ToSql();
        Assert.Equal("SELECT t1.`id` AS `blog_id`,t.`id` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t.`id` = t1.`id` WHERE t.`id` = ?", sql);
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() =>
            Db.SqlQueryAsync<Dictionary<string, object?>>("SELECT t1.`id`,t.`id` FROM `t_topic` t LEFT JOIN `t_blog` t1 ON t.`id` = t1.`id` WHERE t.`id` = ?", ["2"], Ct));
        Assert.Contains("id", ex.Message, StringComparison.Ordinal);
    }

    private async Task ResetTopicType(string id)
    {
        var existing = await Db.Query<TopicType>().WhereById(id).FirstOrDefaultAsync(Ct);
        if (existing is not null) Assert.Equal(1, await Db.Delete(existing).ExecuteAsync(Ct));
        Assert.Null(await Db.Query<TopicType>().WhereById(id).FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("QueryTest.java#query69")]
    public async Task Query69()
    {
        await ResetTopicType("123");
        var row = new TopicType { Id = "123", Stars = 123, Title = "title123", TopicTypeValue = (int)TopicTypeEnum.Teacher, CreateTime = DateTime.Now };
        Assert.Equal(1, await Db.Insert(row).ExecuteAsync(Ct));
        // C# 的属性不能与类同名，TopicType.TopicTypeValue 与 VO 的 TopicType 名字不同，用成员初始化显式映射
        var vo = await Db.Query<TopicType>().WhereById("123")
            .Select(t => new TopicTypeVO { Id = t.Id, Stars = t.Stars, Title = t.Title, TopicType = (TopicTypeEnum?)t.TopicTypeValue, CreateTime = t.CreateTime })
            .FirstOrDefaultAsync(Ct);
        Assert.NotNull(vo);
        Assert.Equal(TopicTypeEnum.Teacher, vo.TopicType);
    }

    [Fact, Upstream("QueryTest.java#query70"), Upstream("QueryTest.java#query70_1")]
    public async Task Query70()
    {
        await ResetTopicType("123");
        var row = new TopicTypeTest1 { Id = "123", Stars = 123, Title = "title123", TopicType = TopicTypeEnum.Classer, CreateTime = DateTime.Now };
        Assert.Equal(1, await Db.Insert(row).ExecuteAsync(Ct));
        var read = await Db.Query<TopicTypeTest1>().WhereById("123").FirstOrDefaultAsync(Ct);
        Assert.NotNull(read);
        Assert.Equal(TopicTypeEnum.Classer, read.TopicType);
        Assert.Equal((int)TopicTypeEnum.Classer, (await Db.Query<TopicType>().WhereById("123").FirstAsync(Ct)).TopicTypeValue);
    }

    [Fact, Upstream("QueryTest.java#query71")]
    public async Task Query71()
    {
        await ResetTopicType("1231");
        var row = new TopicTypeJson
        {
            Id = "1231", Stars = 123, Title = new TopicTypeJsonValue { Name = "123", Age = 456 },
            TopicType = (int)TopicTypeEnum.Classer, CreateTime = DateTime.Now,
        };
        Assert.Equal(1, await Db.Insert(row).ExecuteAsync(Ct));
        var read = await Db.Query<TopicTypeJson>().WhereById("1231").FirstOrDefaultAsync(Ct);
        Assert.NotNull(read?.Title);
        Assert.Equal("123", read.Title.Name);
        Assert.Equal(456, read.Title.Age);
        Assert.Equal((int)TopicTypeEnum.Classer, read.TopicType);
    }
}
