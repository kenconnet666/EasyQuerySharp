namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/ExampleTest.java（文档示例：集合排序后取第一个、Any、由查询定义的"最近一张卡"导航）。</summary>
public sealed class ExampleTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // Java 用例没有断言：文档里的基本写法（取一个、全部、条件、原生片段、排序、跳过空值的值过滤）；最后一段的空值条件不生成
    [Fact, Upstream("mysql8/ExampleTest.java#sampleTest1")]
    public async Task SampleTest1()
    {
        await Db.Query<M8User>().Where(m => m.Name == "xiao ming").SingleOrDefaultAsync(Ct);
        await Db.Query<M8User>().ToListAsync(Ct);
        await Db.Query<M8User>().Where(m => m.Name.LikeContains("zhang san") && m.Name.LikeContains("zhang san") && m.Age > 20).ToListAsync(Ct);
        await Db.Query<M8User>().Where(m => m.Name.LikeContains("zhang san") && m.Age > 20 && Sql.Raw<bool>("{0} != {1}", m.Name, "小明")).OrderBy(m => m.Age).ToListAsync(Ct);
        await Db.Query<M8User>().Where(m => m.Name.LikeContains("zhang san") && m.Age > 20).OrderBy(m => m.Age).ThenByDescending(m => m.Name).ThenByDescending(m => m.Name).ToListAsync(Ct);

        const string name = "zhang san";
        int? age = null;
        using var listen = SqlCapture.Listen();
        await Db.Query<M8User>().UseValueFilter(Filtering.ValueFilter.NotNullOrBlank).Where(m => m.Name.LikeContains(name) && m.Age > age).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`name`,`age`,`create_time` FROM `m8_user` WHERE `name` LIKE CONCAT('%',?,'%')");
        listen.AssertParams("zhang san(String)");
    }

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
