namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 读取函数按查询形状缓存（编译一次，之后复用）：同样形状、挂着不同对象的查询（不同的换算委托、各自的带出）
/// 必须各用各的，不能拿到上一次的。
/// </summary>
public sealed class ShaperCacheTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact]
    public async Task SameShapeUsesEachQuerysOwnConverter()
    {
        foreach (var suffix in new[] { "-a", "-b" })
        {
            var rows = await Db.Query<Topic>().Where(t => t.Id == "1")
                .Select(t => new Draft<string?>(Sql.ClientSide(t.Title, x => x + suffix)))
                .ToListAsync(Ct);
            Assert.EndsWith(suffix, rows[0].Value1);
        }
    }

    [Fact]
    public async Task SameShapeFillsEachQuerysOwnIncludes()
    {
        foreach (var id in new[] { "1", "2" })
        {
            var blog = await Db.Query<BlogEntity>().Where(b => b.Id == id).FirstAsync(Ct);
            var topics = await Db.Query<Topic>().Where(t => t.Id == id).ToListAsync(Ct);
            Assert.Equal(id, blog.Id);
            Assert.Equal(id, Assert.Single(topics).Id);
        }
    }
}
