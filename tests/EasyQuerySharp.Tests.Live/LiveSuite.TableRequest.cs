using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Live;

// 列表页的请求（TableRequest）：表头筛选、关键字筛选、排序只认结果里的列，带了总数时不再计数（与 TS 版 table.test 相同的数据与断言）
public abstract partial class LiveSuite
{
    private static Querying.Query<PersonRow> People(EasyDb db) =>
        db.Query<User>().Select(u => new PersonRow { Name = u.Name, Age = u.Age, Company = u.Company!.Name });

    /// <summary>列表的一行：用户名、年龄、公司名（导航带出的列）。</summary>
    public sealed class PersonRow
    {
        public string Name { get; set; } = "";

        public int? Age { get; set; }

        public string? Company { get; set; }
    }

    // 表头筛选按列的类型读值（数字列的 30）、关键字筛选是包含、排序按结果列；数出总数
    [Fact]
    public async Task TableRequestFiltersSearchesAndSortsResultColumns()
    {
        var db = await SeededAsync();
        var page = await People(db).ToPageAsync(TableRequest.Parse("filter.age=30&filter.age=17&search.name=a&sort=-age"), Ct);
        Assert.Equal(["alice", "carol"], page.Rows.Select(r => r.Name));
        Assert.Equal((2, 10), (page.Total, page.PageSize));
    }

    // 筛选、排序可以用导航带出的列；带了 total 时不再计数
    [Fact]
    public async Task TableRequestWithTotalSkipsTheCount()
    {
        var db = await SeededAsync();
        using var listen = SqlCapture.Listen();
        var page = await People(db).ToPageAsync(TableRequest.Parse("filter.company=acme&sort=name&total=99&pageSize=1"), Ct);
        Assert.Equal(["alice"], page.Rows.Select(r => r.Name));
        Assert.Equal(99, page.Total);
        Assert.DoesNotContain(listen.Events, e => e.Statement.Text.Contains("COUNT(", StringComparison.Ordinal));
    }

    // 只认结果里的列：不在结果里、不能搜索的列，以及读不成列类型的值都报错；没有投影时结果列就是实体的列
    [Fact]
    public async Task TableRequestOnlyAcceptsResultColumns()
    {
        var db = await SeededAsync();
        Assert.Contains("Name, Age, Company", (await Assert.ThrowsAsync<ArgumentException>(() => People(db).ToPageAsync(TableRequest.Parse("sort=attrs"), Ct))).Message);
        await Assert.ThrowsAsync<ArgumentException>(() => People(db).ToPageAsync(TableRequest.Parse("search.age=3"), Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => People(db).ToPageAsync(TableRequest.Parse("filter.age=x"), Ct));
        Assert.Equal("carol", Assert.Single((await db.Query<User>().ToPageAsync(TableRequest.Parse("sort=-name&pageSize=1"), Ct)).Rows).Name);
    }

    // 路径不能顺着导航走到别的表：实体查询上的 company.name 是关联表的列，不在结果里（否则请求能逐个探测关联表的列）
    [Fact]
    public async Task TableRequestDoesNotFollowNavigations()
    {
        var db = await SeededAsync();
        foreach (var query in new[] { "filter.company.name=acme", "search.company.name=a", "sort=company.name", "sort=-posts.title" })
            Assert.Contains("不是值对象", (await Assert.ThrowsAsync<ArgumentException>(() => db.Query<User>().ToPageAsync(TableRequest.Parse(query), Ct))).Message);
    }
}
