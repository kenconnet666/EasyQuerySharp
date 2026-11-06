using SysBankCard = EasyQuerySharp.Tests.Upstream.Mysql8.SysBankCard;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Mssql;

// 拼接式 LIKE：Java 的 like(concat) / likeMatchLeft / likeMatchRight / startsWith 在 SQL Server 上写成 LIKE ('%'+CAST(v AS NVARCHAR(MAX))+'%')。
// C# 对应 Sql.LikeContains / LikeStartsWith / LikeEndsWith（值在 SQL 里拼 %）。
public sealed partial class MsSQLQueryTest
{
    [Fact, Upstream("mssql/MsSQLQueryTest.java#testLikeConcat1")]
    public async Task TestLikeConcat1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MsSQLMyTopic>().Where(m => Sql.LikeContains(m.Title, string.Concat("%", m.Id, "%"))).ToListAsync(Ct);
        listen.AssertSql(TopicSql + " WHERE [Title] LIKE ('%'+CAST(CONCAT(?,[Id],?) AS NVARCHAR(MAX))+'%')");
        listen.AssertParams("%(String),%(String)");
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testLikeConcat2")]
    public async Task TestLikeConcat2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MsSQLMyTopic>().Where(m => Sql.LikeStartsWith(m.Title, string.Concat("%", m.Id, "%"))).ToListAsync(Ct);
        listen.AssertSql(TopicSql + " WHERE [Title] LIKE (CAST(CONCAT(?,[Id],?) AS NVARCHAR(MAX))+'%')");
        listen.AssertParams("%(String),%(String)");
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testLikeConcat3")]
    public async Task TestLikeConcat3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MsSQLMyTopic>().Where(m => Sql.LikeEndsWith(m.Title, string.Concat("%", m.Id, "%"))).ToListAsync(Ct);
        listen.AssertSql(TopicSql + " WHERE [Title] LIKE ('%'+CAST(CONCAT(?,[Id],?) AS NVARCHAR(MAX)))");
        listen.AssertParams("%(String),%(String)");
    }

    private const string UsersSql = "SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t";

    private const string CardsOfU2 = "SELECT t.[Id],t.[Uid],t.[Code],t.[Type],t.[BankId],t.[OpenTime] FROM [t_bank_card] t WHERE t.[Uid] IN (?)";

    // 储蓄卡全部满足条件（没有储蓄卡也算）：只有 u2；带出的银行卡与条件一致
    private static void AssertOnlyU2(IReadOnlyList<SysUser> users, Func<SysBankCard, bool> all)
    {
        Assert.Single(users);
        Assert.All(users, u => Assert.True(u.BankCards!.Where(c => c.Type == "储蓄卡").All(all)));
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testAll")]
    public async Task TestAll()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true);
        listen.AssertSql(0, UsersSql + " WHERE NOT (EXISTS(SELECT TOP 1 1 FROM [t_bank_card] t1 WHERE t1.[Uid] = t.[Id] AND t1.[Type] = ? AND (NOT (t1.[Code] LIKE (CAST(? AS NVARCHAR(MAX))+'%')))))");
        listen.AssertParams(0, "储蓄卡(String),33123(String)");
        listen.AssertSql(1, CardsOfU2);
        listen.AssertParams(1, "u2(String)");
        Assert.Equal(2, listen.Events.Count);
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testAll1")]
    public async Task TestAll1()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => (c.Code ?? "").LikeStartsWith("33123"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => (c.Code ?? "").StartsWith("33123", StringComparison.Ordinal));
        listen.AssertSql(0, UsersSql + " WHERE NOT (EXISTS(SELECT TOP 1 1 FROM [t_bank_card] t1 WHERE t1.[Uid] = t.[Id] AND t1.[Type] = ? AND (NOT (ISNULL(t1.[Code],?) LIKE (CAST(? AS NVARCHAR(MAX))+'%')))))");
        listen.AssertParams(0, "储蓄卡(String),(String),33123(String)");
        listen.AssertSql(1, CardsOfU2);
        listen.AssertParams(1, "u2(String)");
        Assert.Equal(2, listen.Events.Count);
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testAll2")]
    public async Task TestAll2()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true && c.Code.StartsWith("45678", StringComparison.Ordinal));
        listen.AssertSql(0, UsersSql + " WHERE NOT (EXISTS(SELECT TOP 1 1 FROM [t_bank_card] t1 WHERE t1.[Uid] = t.[Id] AND t1.[Type] = ? AND (NOT (t1.[Code] LIKE (CAST(? AS NVARCHAR(MAX))+'%') AND t1.[Code] LIKE (CAST(? AS NVARCHAR(MAX))+'%')))))");
        listen.AssertParams(0, "储蓄卡(String),33123(String),45678(String)");
        listen.AssertSql(1, CardsOfU2);
        listen.AssertParams(1, "u2(String)");
        Assert.Equal(2, listen.Events.Count);
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testAllGroupJoin")]
    public async Task TestAllGroupJoin()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().GroupJoinAll().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToListAsync(Ct);
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true);
        listen.AssertSql(0, UsersSql + " LEFT JOIN (SELECT t1.[Uid] AS [__group_key1__],(CASE WHEN (COUNT(?) <= 0) THEN ? ELSE ? END) AS [__none2__] FROM [t_bank_card] t1 WHERE t1.[Type] = ? AND (NOT (t1.[Code] LIKE (CAST(? AS NVARCHAR(MAX))+'%'))) GROUP BY t1.[Uid]) t2 ON t2.[__group_key1__] = t.[Id] WHERE ISNULL(t2.[__none2__],?) = ?");
        listen.AssertParams(0, "1(Integer),true(Boolean),false(Boolean),储蓄卡(String),33123(String),true(Boolean),true(Boolean)");
        listen.AssertSql(1, CardsOfU2);
        listen.AssertParams(1, "u2(String)");
        Assert.Equal(2, listen.Events.Count);
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testAllGroupJoin2")]
    public async Task TestAllGroupJoin2()
    {
        using var listen = SqlCapture.Listen();
        var users = await Db.Query<SysUser>().GroupJoinAll().Include(u => u.BankCards)
            .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Id.LikeStartsWith("45678"))).ToListAsync(Ct);
        // Java 的第二个条件是 bc.id().startsWith("45678")，C# 的 AssertOnlyU2 对应写成同样的条件
        AssertOnlyU2(users, c => c.Code?.StartsWith("33123", StringComparison.Ordinal) == true && c.Id!.StartsWith("45678", StringComparison.Ordinal));
        listen.AssertSql(0, UsersSql + " LEFT JOIN (SELECT t1.[Uid] AS [__group_key1__],(CASE WHEN (COUNT(?) <= 0) THEN ? ELSE ? END) AS [__none2__] FROM [t_bank_card] t1 WHERE t1.[Type] = ? AND (NOT (t1.[Code] LIKE (CAST(? AS NVARCHAR(MAX))+'%') AND t1.[Id] LIKE (CAST(? AS NVARCHAR(MAX))+'%'))) GROUP BY t1.[Uid]) t2 ON t2.[__group_key1__] = t.[Id] WHERE ISNULL(t2.[__none2__],?) = ?");
        listen.AssertParams(0, "1(Integer),true(Boolean),false(Boolean),储蓄卡(String),33123(String),45678(String),true(Boolean),true(Boolean)");
        listen.AssertSql(1, CardsOfU2);
        listen.AssertParams(1, "u2(String)");
        Assert.Equal(2, listen.Events.Count);
    }
}
