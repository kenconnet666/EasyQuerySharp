namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：UpdateDeleteJoinTest.java。SET 或条件里用到对一导航时，MySQL 写成 <c>UPDATE t INNER JOIN … SET</c> /
/// <c>DELETE t FROM … INNER JOIN</c>；先翻译 SET 再翻译条件，别名按出现顺序编号。
/// </summary>
public sealed class UpdateDeleteJoinTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("UpdateDeleteJoinTest.java#testUpdateJoin1")]
    public async Task TestUpdateJoin1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `doc_bank_card` t INNER JOIN `doc_user` t1 ON t1.`id` = t.`uid` SET t.`type` = t1.`name` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("UpdateDeleteJoinTest.java#testUpdateJoin2")]
    public async Task TestUpdateJoin2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.Bank!.Name }).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE `doc_bank_card` t INNER JOIN `doc_bank` t1 ON t1.`id` = t.`bank_id` INNER JOIN `doc_user` t2 ON t2.`id` = t.`uid` SET t.`type` = t1.`name` WHERE t2.`name` LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("UpdateDeleteJoinTest.java#testDeleteJoin1"), Upstream("UpdateDeleteJoinTest.java#testDeleteJoin2")]
    public async Task TestDeleteJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Delete<DocBankCard>().AllowDeleteStatement(true).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("DELETE t FROM `doc_bank_card` t INNER JOIN `doc_user` t1 ON t1.`id` = t.`uid` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%123%(String)");
    }
}
