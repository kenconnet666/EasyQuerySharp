using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

// QueryTest.java 里的 whereObject / orderByObject 用例（C# 的 FilterBy / SortBy）
public sealed partial class QueryTest
{
    [Fact, Upstream("QueryTest.java#query15")]
    public void Query15() =>
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `create_time` > ?",
            Db.Query<Topic>().FilterBy(new TopicRequest { CreateTimeBegin = DateTime.Now }).ToSql());

    // 只允许按 createTime 排序：按 id 排序时报错
    [Fact, Upstream("QueryTest.java#query18")]
    public void Query18()
    {
        var request = new TopicRequest { CreateTimeBegin = DateTime.Now, Orders = ["id"] };
        Assert.Throws<InvalidOperationException>(() => Db.Query<Topic>().FilterBy(request).SortBy(request));
        request.Orders = ["createTime"];
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `create_time` > ? ORDER BY `create_time` ASC LIMIT 1",
            Db.Query<Topic>().FilterBy(request).SortBy(request).Take(1).ToSql());
        Assert.Equal("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `create_time` > ? ORDER BY `create_time` ASC",
            Db.Query<Topic>().FilterBy(request).SortBy(request).ToSql());
    }
}
