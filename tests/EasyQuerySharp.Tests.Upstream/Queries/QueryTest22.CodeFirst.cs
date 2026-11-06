namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>对应 Java：QueryTest22.java 里要连库的用例（先 syncTable 建 doc_part，再查询）；其余用例只比对 SQL，见 <see cref="QueryTest22"/>。</summary>
public sealed class QueryTest22CodeFirst(MySqlFixture fixture) : MySqlTest(fixture)
{
    // Java 不断言：按“建设”银行卡的银行 id 计数排序（count(x -> x.bank().id()) 写成 Select(..).LongCount()）
    [Fact, Upstream("QueryTest22.java#testManyOrder")]
    public async Task TestManyOrder()
    {
        await Db.CodeFirst.SyncTablesAsync([typeof(DocPart)], Ct);
        var users = await Db.Query<DocUser>()
            .OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Select(x => x.Bank!.Id).LongCount())
            .ToListAsync(Ct);
        Assert.NotNull(users);
    }
}
