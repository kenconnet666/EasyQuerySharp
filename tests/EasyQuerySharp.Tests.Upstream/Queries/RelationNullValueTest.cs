namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>对应 Java：RelationNullValueTest.java（关联键是空字符串时照常带出）。</summary>
public sealed class RelationNullValueTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 用例没有断言，只验证能执行；空字符串不是 null，两个 One1 都按 "" 匹配到 One2
    [Fact, Upstream("RelationNullValueTest.java#one2oneTest")]
    public async Task One2oneTest()
    {
        var list = await Db.Query<One1>().Include(o => o.One2).ToListAsync(Ct);
        Assert.All(list, o => Assert.NotNull(o.One2));
    }
}
