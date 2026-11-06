namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/H2QueryRelationTest.java（按 uid 关联的订单与账号：带出、隐式子查询、导航固定条件、DTO 自动带出）。改在 SQLite 上跑（见 <see cref="H2Fixture"/>）。
/// </summary>
public sealed class H2QueryRelationTest(H2Fixture fixture) : H2Test(fixture)
{
    private const string OrdersWithXiaoMingAccount =
        "SELECT t.id,t.uid,t.uname,t.price FROM t_tb_order t WHERE EXISTS(SELECT 1 FROM t_tb_account t1 WHERE (t1.uid = t.uid AND t1.uname = ?) AND t1.uname LIKE ? LIMIT 1)";

    [Fact, Upstream("h2/H2QueryRelationTest.java#tbTest1")]
    public async Task TbTest1()
    {
        await ClearAsync();
        try
        {
            await Db.Insert(new[]
            {
                Order("tbOrder1", "小明", 1), Order("tbOrder2", "小明", 2), Order("tbOrder3", "小明", 3), Order("tbOrder4", "小红", 4),
                Order("tbOrder5", "小明", 5), Order("tbOrder6", "小红", 6), Order("tbOrder7", "小蓝", 6), Order("tbOrder8", "小青", 8),
            }).ExecuteAsync(Ct);
            await Db.Insert(new[]
            {
                Account("tbAccount1", "小明"), Account("tbAccount2", "小蓝"), Account("tbAccount3", "小红"),
                Account("tbAccount4", "小紫"), Account("tbAccount5", "小明"), Account("tbAccount6", "小明"),
            }).ExecuteAsync(Ct);

            using (var listen = SqlCapture.Listen())
            {
                var orders = await Db.Query<TbOrder>().Include(o => o.Accounts).ToListAsync(Ct);
                Assert.Equal(2, listen.Events.Count);
                listen.AssertSql(0, "SELECT id,uid,uname,price FROM t_tb_order");
                listen.AssertSql(1, "SELECT t.id,t.uid,t.uname,t.account FROM t_tb_account t WHERE t.uid IN (?,?,?,?)");
                listen.AssertParams(1, "小明(String),小红(String),小蓝(String),小青(String)");
                Assert.Equal(3, orders.Single(o => o.Id == "tbOrder1").Accounts!.Count);
                Assert.Empty(orders.Single(o => o.Id == "tbOrder8").Accounts!);
            }

            using (var listen = SqlCapture.Listen())
            {
                var orders = await Db.Query<TbOrder>().Where(o => o.Accounts!.Any(a => a.Uname!.Contains("小明"))).ToListAsync(Ct);
                listen.AssertSql("SELECT t.id,t.uid,t.uname,t.price FROM t_tb_order t WHERE EXISTS(SELECT 1 FROM t_tb_account t1 WHERE t1.uid = t.uid AND t1.uname LIKE ? LIMIT 1)");
                listen.AssertParams("%小明%(String)");
                Assert.Equal(4, orders.Count);
            }

            // myAccounts 带固定条件 uname = '小明'
            using (var listen = SqlCapture.Listen())
            {
                var orders = await Db.Query<TbOrder>().Where(o => o.MyAccounts!.Any(a => a.Uname!.Contains("小明"))).ToListAsync(Ct);
                listen.AssertSql(OrdersWithXiaoMingAccount);
                listen.AssertParams("小明(String),%小明%(String)");
                Assert.Equal(4, orders.Count);
            }

            // DTO 自动带出 myAccounts：带出查询也带上固定条件
            using (var listen = SqlCapture.Listen())
            {
                var orders = await Db.Query<TbOrder>().Where(o => o.MyAccounts!.Any(a => a.Uname!.Contains("小明"))).Select<TbOrderDTO>().ToListAsync(Ct);
                Assert.Equal(2, listen.Events.Count);
                listen.AssertSql(0, OrdersWithXiaoMingAccount);
                listen.AssertParams(0, "小明(String),%小明%(String)");
                listen.AssertSql(1, "SELECT t.id,t.uid,t.uname,t.account FROM t_tb_account t WHERE (t.uid IN (?) AND t.uname = ?)");
                listen.AssertParams(1, "小明(String),小明(String)");
                Assert.All(orders, o => Assert.Equal(3, o.MyAccounts!.Count));
            }
        }
        finally
        {
            await ClearAsync();
        }
    }

    private async Task ClearAsync()
    {
        await Db.Delete<TbOrder>().Where(t => t.Id != null).AllowDeleteStatement(true).ExecuteAsync(Ct);
        await Db.Delete<TbAccount>().Where(t => t.Id != null).AllowDeleteStatement(true).ExecuteAsync(Ct);
    }

    private static TbOrder Order(string id, string user, int price) => new() { Id = id, Uid = user, Uname = user, Price = price };

    private static TbAccount Account(string id, string user) => new() { Id = id, Uid = user, Uname = user, Account = id };
}
