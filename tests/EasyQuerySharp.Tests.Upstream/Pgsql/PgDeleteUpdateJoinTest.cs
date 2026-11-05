namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>
/// 对应 Java：pgsql/DeleteUpdateJoinTest.java。PostgreSQL 把连接的表写在 <c>UPDATE … SET … FROM</c> / <c>DELETE FROM … USING</c> 里，
/// ON 条件按顺序并入 WHERE；SET 的列不带表别名。
/// </summary>
public sealed class PgDeleteUpdateJoinTest(PgFixture fixture) : PgTest(fixture)
{
    [Fact, Upstream("pgsql/DeleteUpdateJoinTest.java#testUpdateJoin1")]
    public async Task TestUpdateJoin1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE \"doc_bank_card\" t SET \"type\" = t1.\"name\" FROM \"doc_user\" t1 WHERE t1.\"id\" = t.\"uid\" AND t1.\"name\" LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("pgsql/DeleteUpdateJoinTest.java#testUpdateJoin2")]
    public async Task TestUpdateJoin2()
    {
        // Java 先手写执行一条同形状的 SQL，确认 PostgreSQL 接受这种写法
        await Db.ExecuteSqlAsync("UPDATE \"doc_bank_card\" t SET \"type\" = t1.\"name\" FROM \"doc_bank\" t1 , \"doc_user\" t2 WHERE t1.\"id\" = t.\"bank_id\" AND t2.\"id\" = t1.\"id\" AND t2.\"name\" LIKE '123'", cancellationToken: Ct);
        using var listen = SqlCapture.Listen();
        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.Bank!.Name }).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE \"doc_bank_card\" t SET \"type\" = t1.\"name\" FROM \"doc_bank\" t1 , \"doc_user\" t2 WHERE t1.\"id\" = t.\"bank_id\" AND t2.\"id\" = t.\"uid\" AND t2.\"name\" LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("pgsql/DeleteUpdateJoinTest.java#testUpdateJoin3")]
    public async Task TestUpdateJoin3()
    {
        Assert.Equal(0, await Db.Query<DocBankCard>().Where(c => c.Type == "小明姓名").CountAsync(Ct));
        var card = await Db.Query<DocBankCard>().Where(c => c.User!.Name!.Contains("小明姓名")).SingleAsync(Ct);
        Assert.Equal("储蓄卡", card.Type);

        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("小明姓名")).ExecuteAsync(Ct);

        var updated = await Db.Query<DocBankCard>().Where(c => c.User!.Name!.Contains("小明姓名")).SingleAsync(Ct);
        Assert.Equal("小明姓名", updated.Type);
        Assert.Equal(1, await Db.Query<DocBankCard>().Where(c => c.Type == "小明姓名").CountAsync(Ct));
    }

    [Fact, Upstream("pgsql/DeleteUpdateJoinTest.java#testUpdateJoin4")]
    public async Task TestUpdateJoin4()
    {
        Assert.Equal(0, await Db.Query<DocBankCard>().Where(c => c.Type == "建设银行").CountAsync(Ct));
        var card = await Db.Query<DocBankCard>().Where(c => c.User!.Name!.Contains("小红姓名")).SingleAsync(Ct);
        Assert.Equal("储蓄卡", card.Type);

        await Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.Bank!.Name }).Where(c => c.User!.Name!.Contains("小红姓名")).ExecuteAsync(Ct);

        var updated = await Db.Query<DocBankCard>().Where(c => c.User!.Name!.Contains("小红姓名")).SingleAsync(Ct);
        Assert.Equal("建设银行", updated.Type);
        Assert.Equal(1, await Db.Query<DocBankCard>().Where(c => c.Type == "建设银行").CountAsync(Ct));
    }

    [Fact, Upstream("pgsql/DeleteUpdateJoinTest.java#testDeleteJoin1"), Upstream("pgsql/DeleteUpdateJoinTest.java#testDeleteJoin2")]
    public async Task TestDeleteJoin()
    {
        using var listen = SqlCapture.Listen();
        await Db.Delete<DocBankCard>().AllowDeleteStatement(true).Where(c => c.User!.Name!.Contains("123")).ExecuteAsync(Ct);
        listen.AssertSql("DELETE FROM \"doc_bank_card\" t USING \"doc_user\" t1 WHERE t1.\"id\" = t.\"uid\" AND t1.\"name\" LIKE ?");
        listen.AssertParams("%123%(String)");
    }
}
