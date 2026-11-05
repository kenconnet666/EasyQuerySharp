namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/ExampleTest.java（文档示例：集合排序后取第一个、Any、由查询定义的"最近一张卡"导航）。</summary>
public sealed class ExampleTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // Java 用例没有断言：三种写法筛出"最近开的卡是某类型"的银行
    [Fact, Upstream("mysql8/ExampleTest.java#test2")]
    public async Task Test2()
    {
        await Db.Query<SysBank>().Where(bank => bank.BankCards!.OrderByDescending(card => card.OpenTime).First().Type == "123").ToListAsync(Ct);
        await Db.Query<SysBank>().Where(bank => bank.BankCards!.Any(card => card.Type == "123")).ToListAsync(Ct);
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().Where(bank => bank.FirstBankCard!.Type == "123").ToListAsync(Ct);
        // 导航到由查询定义的实体：隐式 JOIN 它的 CTE
        Assert.StartsWith("WITH `bank_card_first` AS (", listen.Last.Statement.Text);
    }
}
