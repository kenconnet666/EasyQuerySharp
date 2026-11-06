namespace EasyQuerySharp.Tests.Upstream.Mssql;

/// <summary>
/// 对应 Java：mssql/MsSQLUpdateJoinTest.java。SQL Server 把连接写在 <c>UPDATE t SET … FROM [表] t INNER JOIN …</c> /
/// <c>DELETE t FROM [表] t INNER JOIN …</c> 里，SET 的列带表别名。
/// </summary>
public sealed class MsSQLUpdateJoinTest(MsSqlFixture fixture) : MsSqlTest(fixture)
{
    [Fact, Upstream("mssql/MsSQLUpdateJoinTest.java#testUpdateJoin1")]
    public async Task TestUpdateJoin1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE t SET t.[Type] = t1.[Name] FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Uid] WHERE t1.[Name] LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("mssql/MsSQLUpdateJoinTest.java#testUpdateJoin2")]
    public async Task TestUpdateJoin2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.Bank!.Name }).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE t SET t.[Type] = t1.[Name] FROM [doc_bank_card] t INNER JOIN [doc_bank] t1 ON t1.[Id] = t.[BankId] INNER JOIN [doc_user] t2 ON t2.[Id] = t.[Uid] WHERE t2.[Name] LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("mssql/MsSQLUpdateJoinTest.java#testDeleteJoin1"), Upstream("mssql/MsSQLUpdateJoinTest.java#testDeleteJoin2")]
    public async Task TestDeleteJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Delete<DocBankCard>().AllowDeleteStatement(true).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("DELETE t FROM [doc_bank_card] t INNER JOIN [doc_user] t1 ON t1.[Id] = t.[Uid] WHERE t1.[Name] LIKE ?");
        listen.AssertParams("%123%(String)");
    }
}
