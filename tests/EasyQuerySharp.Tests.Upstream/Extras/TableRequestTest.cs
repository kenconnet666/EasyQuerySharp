using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>列表页请求的解析（<see cref="TableRequest.Parse(string?)"/>，与 TS 版的 parseTableRequest 相同的参数约定）；按请求查询的用例在 LiveSuite。</summary>
public sealed class TableRequestTest
{
    [Fact]
    public void ReadsPagingSortsTotalFiltersAndSearches()
    {
        var request = TableRequest.Parse("?page=2&pageSize=20&sort=-amount,name&total=368&filter.status=draft&filter.status=paid&filter.empty=&search.supplier=%20启航%20&keyword=泵");
        Assert.Equal((2, 20, 368L), (request.Page, request.PageSize, request.Total));
        Assert.Equal([new TableSort("amount", true), new TableSort("name", false)], request.Sorts);
        Assert.Equal(["draft", "paid"], Assert.Single(request.Filters, f => f.Key == "status").Value);
        Assert.DoesNotContain("empty", request.Filters.Keys);
        Assert.Equal("启航", request.Searches["supplier"]);
        Assert.Equal(["泵"], request.Params["keyword"]);
    }

    // 拆好的参数也认；没写页码、条数时是 1、10；排序也认 `amount desc` 的写法；+ 是空格
    [Fact]
    public void ReadsParsedParametersWithDefaults()
    {
        var request = TableRequest.Parse([KeyValuePair.Create("sort", (string?)"amount desc, id"), KeyValuePair.Create("filter.age", (string?)"30")]);
        Assert.Equal((1, 10, (long?)null), (request.Page, request.PageSize, request.Total));
        Assert.Equal([new TableSort("amount", true), new TableSort("id", false)], request.Sorts);
        Assert.Equal(["30"], request.Filters["age"]);
        Assert.Equal("a b", TableRequest.Parse("search.name=a+b").Searches["name"]);
    }

    [Fact]
    public void RejectsBadPagingValues()
    {
        Assert.Throws<ArgumentException>(() => TableRequest.Parse("page=0"));
        Assert.Throws<ArgumentException>(() => TableRequest.Parse("pageSize=5000"));
        Assert.Throws<ArgumentException>(() => TableRequest.Parse("total=-1"));
    }
}
