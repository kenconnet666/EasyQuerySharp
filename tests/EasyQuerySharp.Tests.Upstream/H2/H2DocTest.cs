namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/H2DocTest.java（文档示例：显式 JOIN 与隐式导航投影到 VO、导航计数、标量子查询、原生片段、实体与对一导航判空）。
/// Java 的 H2 库里没有 doc_* 表，用例只断言 SQL、吞掉执行错误；这里在 SQLite 上建好表并写入数据（见 <see cref="H2Fixture"/>），SQL 相同，另外核对结果。
/// Java 的 proxy 写法（new BankCardVOProxy() + selectAll）与 class 写法（Select.of + FETCHER.allFields()）SQL 相同，C# 都写成成员初始化，两个用例共用一个测试。
/// </summary>
public sealed class H2DocTest(H2Fixture fixture) : H2Test(fixture)
{
    private const string BankCardColumns = "SELECT t.id,t.uid,t.code,t.type,t1.name AS user_name,t2.name AS bank_name FROM doc_bank_card t";
    private const string UserColumns = "SELECT t.id,t.name,t.phone,t.age,";

    [Fact, Upstream("h2/H2DocTest.java#selectProxyTest1"), Upstream("h2/H2DocTest.java#selectClassTest1")]
    public async Task SelectProxyTest1()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<DocBankCard>().LeftJoin<DocUser>((c, u) => c.Uid == u.Id).LeftJoin<DocBank>((c, _, b) => c.BankId == b.Id)
            .Where((c, u, _) => u.Name!.Contains("小明") && c.Type == "储蓄卡")
            .Select((c, u, b) => new BankCardVO { Id = c.Id, Uid = c.Uid, Code = c.Code, Type = c.Type, UserName = u.Name, BankName = b.Name })
            .ToListAsync(Ct);
        listen.AssertSql(BankCardColumns + " LEFT JOIN doc_user t1 ON t.uid = t1.id LEFT JOIN doc_bank t2 ON t.bank_id = t2.id WHERE t1.name LIKE ? AND t.type = ?");
        listen.AssertParams("%小明%(String),储蓄卡(String)");
        AssertXiaoMingCard(list);
    }

    // Java 的 FETCHER.id().code()：只取两列
    [Fact, Upstream("h2/H2DocTest.java#selectProxyTest1_1")]
    public async Task SelectProxyTest1_1()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<DocBankCard>().Where(c => c.Type == "储蓄卡").Select(c => new DocBankCard { Id = c.Id, Code = c.Code }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.id,t.code FROM doc_bank_card t WHERE t.type = ?");
        listen.AssertParams("储蓄卡(String)");
        Assert.Equal(2, list.Count);
        Assert.All(list, c => Assert.Null(c.Type));
    }

    // 隐式导航：user 可空写成 LEFT JOIN，bank 标了 required 写成 INNER JOIN
    [Fact, Upstream("h2/H2DocTest.java#selectProxyTest2"), Upstream("h2/H2DocTest.java#selectClassTest2")]
    public async Task SelectProxyTest2()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<DocBankCard>().Where(c => c.User!.Name!.Contains("小明") && c.Type == "储蓄卡")
            .Select(c => new BankCardVO { Id = c.Id, Uid = c.Uid, Code = c.Code, Type = c.Type, UserName = c.User!.Name, BankName = c.Bank!.Name })
            .ToListAsync(Ct);
        listen.AssertSql(BankCardColumns + " LEFT JOIN doc_user t1 ON t1.id = t.uid INNER JOIN doc_bank t2 ON t2.id = t.bank_id WHERE t1.name LIKE ? AND t.type = ?");
        listen.AssertParams("%小明%(String),储蓄卡(String)");
        AssertXiaoMingCard(list);
    }

    // 集合导航的计数写成关联子查询
    [Fact, Upstream("h2/H2DocTest.java#selectProxyTest3"), Upstream("h2/H2DocTest.java#selectClassTest3")]
    public async Task SelectProxyTest3()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<DocUser>().Where(u => u.Name!.Contains("小明"))
            .Select(u => new DocUserVO { Id = u.Id, Name = u.Name, Phone = u.Phone, Age = u.Age, CardCount = u.BankCards!.Count() })
            .ToListAsync(Ct);
        listen.AssertSql(UserColumns + "(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card_count FROM doc_user t WHERE t.name LIKE ?");
        listen.AssertParams("%小明%(String)");
        Assert.Equal(1, Assert.Single(list).CardCount);
    }

    // 手写的标量子查询（Java 的 setSubQuery / subQueryColumn），SQL 与导航计数相同
    [Fact, Upstream("h2/H2DocTest.java#selectProxyTest4"), Upstream("h2/H2DocTest.java#selectClassTest4")]
    public async Task SelectProxyTest4()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<DocUser>().Where(u => u.Name!.Contains("小明"))
            .Select(u => new DocUserVO
            {
                Id = u.Id, Name = u.Name, Phone = u.Phone, Age = u.Age,
                CardCount = Sql.SubQuery(Db.Query<DocBankCard>().Where(c => c.Uid == u.Id).Select(c => Sql.Count())),
            })
            .ToListAsync(Ct);
        listen.AssertSql(UserColumns + "(SELECT COUNT(*) FROM doc_bank_card t1 WHERE t1.uid = t.id) AS card_count FROM doc_user t WHERE t.name LIKE ?");
        listen.AssertParams("%小明%(String)");
        Assert.Equal(1, Assert.Single(list).CardCount);
    }

    // 原生 SQL 片段（Java 的 setSQL / sqlSegment）
    [Fact, Upstream("h2/H2DocTest.java#selectProxyTest5"), Upstream("h2/H2DocTest.java#selectClassTest5")]
    public async Task SelectProxyTest5()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<DocUser>().Where(u => u.Name!.Contains("小明"))
            .Select(u => new DocUserVO { Id = u.Id, Name = u.Name, Phone = u.Phone, Age = u.Age, CardCount = Sql.Raw<long?>("IFNULL({0},1)", u.Age) })
            .ToListAsync(Ct);
        listen.AssertSql(UserColumns + "IFNULL(t.age,1) AS card_count FROM doc_user t WHERE t.name LIKE ?");
        listen.AssertParams("%小明%(String)");
        Assert.Equal(1, Assert.Single(list).CardCount);
    }

    // Java 的 user.isNull() / isNotNull()：实体整体判空即主键判空
    [Fact, Upstream("h2/H2DocTest.java#testToOne")]
    public async Task TestToOne()
    {
        using var listen = SqlCapture.Listen();
        Assert.Empty(await Db.Query<DocUser>().Where(u => u.Name!.Contains("小明") && u == null && u != null).ToListAsync(Ct));
        listen.AssertSql("SELECT id,name,phone,age FROM doc_user WHERE name LIKE ? AND id IS NULL AND id IS NOT NULL");
        listen.AssertParams("%小明%(String)");
    }

    // 对一导航判空：LEFT JOIN 后判目标表主键
    [Fact, Upstream("h2/H2DocTest.java#testToOne1")]
    public async Task TestToOne1()
    {
        using var listen = SqlCapture.Listen();
        Assert.Empty(await Db.Query<DocBankCard>().Where(c => c.Type!.Contains("储蓄卡") && c.User == null && c.User != null).ToListAsync(Ct));
        listen.AssertSql("SELECT t.id,t.uid,t.code,t.type,t.bank_id FROM doc_bank_card t LEFT JOIN doc_user t1 ON t1.id = t.uid WHERE t.type LIKE ? AND t1.id IS NULL AND t1.id IS NOT NULL");
        listen.AssertParams("%储蓄卡%(String)");
    }

    private static void AssertXiaoMingCard(List<BankCardVO> list)
    {
        var card = Assert.Single(list);
        Assert.Equal("小明工商", card.Id);
        Assert.Equal("小明姓名", card.UserName);
        Assert.Equal("工商银行", card.BankName);
    }
}
