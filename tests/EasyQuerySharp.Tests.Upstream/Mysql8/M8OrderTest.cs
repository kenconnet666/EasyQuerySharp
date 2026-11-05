namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/M8OrderTest.java（必有的集合导航合并成分组 INNER JOIN、取第一个元素）。
/// Java 的 flatElement() 对应 C# 的 Any；表只在 Java 的开发库里，这里只比对 SQL。
/// </summary>
public sealed class M8OrderTest
{
    private static Query<M8Order> Orders => SqlOnly.Db("mysql").Query<M8Order>().GroupJoin(s => s.OrderItems);

    private const string GroupedAny = "SELECT t.`id`,t.`no`,t.`price`,t.`create_time` FROM `t_order` t INNER JOIN (SELECT t1.`order_id` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `t_order_item` t1 WHERE ";

    [Fact, Upstream("mysql8/M8OrderTest.java#testSubQuery")]
    public void TestSubQuery() =>
        SqlOnly.AssertStatement(Orders.Where(m => m.OrderItems!.Any(i => i.Price > 1m)).ToStatement(),
            GroupedAny + "t1.`price` > ? GROUP BY t1.`order_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t2.`__any2__` = ?", "1(Integer),1(BigDecimal),true(Boolean)");

    // 取第一个元素：组内不排序（PartitionOrder.None）
    [Fact, Upstream("mysql8/M8OrderTest.java#testSubQuery2")]
    public void TestSubQuery2() =>
        SqlOnly.AssertStatement(Orders.Where(m => m.OrderItems!.First().Price > 1m).ToStatement(),
            "SELECT t.`id`,t.`no`,t.`price`,t.`create_time` FROM `t_order` t INNER JOIN (SELECT t1.`id`,t1.`order_id`,t1.`price`,t1.`create_time`,(ROW_NUMBER() OVER (PARTITION BY t1.`order_id`)) AS `__row__` FROM `t_order_item` t1) t3 ON (t3.`order_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`price` > ?",
            "1(Integer),1(BigDecimal)");

    [Fact, Upstream("mysql8/M8OrderTest.java#testSubQuery3")]
    public void TestSubQuery3() =>
        SqlOnly.AssertStatement(Orders.Where(m => m.OrderItems!.Any(i => i.Price == null)).ToStatement(),
            GroupedAny + "t1.`price` IS NULL GROUP BY t1.`order_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE t2.`__any2__` = ?", "1(Integer),true(Boolean)");

    // 按主键降序取元素，但目标没有主键：报错而不是悄悄不排
    [Fact, Upstream("mysql8/M8OrderTest.java#testSubQuery4", Deviation = "异常信息用中文，说明缺主键、该怎样声明排序")]
    public void TestSubQuery4()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Orders.Where(m => m.OrderItem2s!.First().Price > 1m).ToStatement());
        Assert.Contains("没有主键", ex.Message);
    }
}
