using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/M8IncludeLimitTest.java（Include 的子数据带 Take：每个父对象各取一段；子实体有不自动查询的列；主查询分页时两种取段方式结果都完整）。
/// Java 的 includes(nav, s -&gt; s.orderBy(…).limit(n)) 对应 <c>Include(nav, q =&gt; q.OrderBy(…).Take(n))</c>，includeLimitMode 对应 <see cref="EasyDbOptions.IncludeLimitMode"/>。
/// </summary>
public sealed class M8IncludeLimitTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private async Task InsertDataAsync()
    {
        await Db.Delete<M8IncludeLimitTopic>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8IncludeLimitChild>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Insert(Topic("t1"), Topic("t2"), Topic("t3")).ExecuteAsync(Ct);
        await Db.Insert(Child("c1", "t1", 1), Child("c2", "t1", 2), Child("c3", "t1", 3), Child("c4", "t2", 1), Child("c5", "t2", 2), Child("c6", "t3", 1)).ExecuteAsync(Ct);
    }

    private static M8IncludeLimitTopic Topic(string id) => new() { Id = id, Name = "topic" + id[1..] };

    private static M8IncludeLimitChild Child(string id, string parentId, int order) => new() { Id = id, ParentId = parentId, Name = "child" + id[1..], Order = order };

    [Fact, Upstream("mysql8/M8IncludeLimitTest.java#testIncludeLimit")]
    public async Task TestIncludeLimit()
    {
        await InsertDataAsync();
        var list = await Db.Query<M8IncludeLimitTopic>().Include(m => m.Children, s => s.OrderBy(x => x.Order).Take(2)).ToListAsync(Ct);
        Assert.Equal(3, list.Count);
        Assert.Equal(["c1", "c2"], list.Single(o => o.Id == "t1").Children!.Select(c => c.Id));
        Assert.Equal(["c6"], list.Single(o => o.Id == "t3").Children!.Select(c => c.Id));
    }

    // 主查询分页（跳过前两个）不影响每个父对象各取一段
    [Fact, Upstream("mysql8/M8IncludeLimitTest.java#testIncludeLimitPagingPartition")]
    public async Task TestIncludeLimitPagingPartition()
    {
        await InsertDataAsync();
        await AssertPage2Async(Db);
    }

    [Fact, Upstream("mysql8/M8IncludeLimitTest.java#testIncludeLimitPagingUnionAll")]
    public async Task TestIncludeLimitPagingUnionAll()
    {
        await InsertDataAsync();
        var unionAll = new EasyDb(o => { o.UseMySql(new MySqlDataSource(TestDatabases.MySql.TryConnectionString(Mysql8Fixture.Database)!)); o.IncludeLimitMode = IncludeLimitMode.UnionAll; });
        await AssertPage2Async(unionAll);
    }

    private static async Task AssertPage2Async(EasyDb db)
    {
        var page2 = await db.Query<M8IncludeLimitTopic>().OrderBy(m => m.Id).Limit(2, 1).Include(m => m.Children, s => s.OrderBy(x => x.Order).Take(2)).ToListAsync(Ct);
        var topic = Assert.Single(page2);
        Assert.Equal("t3", topic.Id);
        Assert.Equal(["c6"], topic.Children!.Select(c => c.Id));
    }
}
