namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>
/// 对应 Java：dameng/UpdateJoinTest.java。达梦的带连接更新与 MySQL 同形：<c>UPDATE "a" t INNER JOIN "b" t1 ON … SET t."c" = t1."d"</c>
/// （Oracle 要绕成 MERGE）；冲突时更新与 Oracle 一样写成从 DUAL 取值的 MERGE。
/// </summary>
public sealed class DamengUpdateJoinTest : DamengTest
{
    // Java 在实库上执行后删掉这一行，不断言
    [Fact, Upstream("dameng/UpdateJoinTest.java#testTopicInsertOrUpdate", Deviation = "Java 在达梦实库上执行（不断言）；C# 只核对渲染出的 MERGE，实库验证在 3.1 冒烟里做")]
    public void TestTopicInsertOrUpdate() =>
        SqlOnly.AssertStatement(Db.Insert(new DamengMyTopic { Id = "-111", Stars = 1, Title = "123123", CreateTime = new DateTime(2020, 1, 1, 1, 1, 0) }).OnConflictUpdate().ToStatement(),
            "MERGE INTO \"MY_TOPIC\" t1 USING (SELECT ? AS \"ID\",? AS \"STARS\",? AS \"TITLE\",? AS \"CREATE_TIME\" FROM DUAL ) t2 ON (t1.\"ID\" = t2.\"ID\") WHEN MATCHED THEN UPDATE SET t1.\"STARS\" = t2.\"STARS\",t1.\"TITLE\" = t2.\"TITLE\",t1.\"CREATE_TIME\" = t2.\"CREATE_TIME\" WHEN NOT MATCHED THEN INSERT (\"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\") VALUES (t2.\"ID\",t2.\"STARS\",t2.\"TITLE\",t2.\"CREATE_TIME\")",
            "-111(String),1(Integer),123123(String),2020-01-01T01:01(LocalDateTime)");

    [Fact, Upstream("dameng/UpdateJoinTest.java#testUpdateJoin1")]
    public void TestUpdateJoin1() =>
        SqlOnly.AssertStatement(Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.User!.Name }).Where(c => c.User!.Name!.Contains("123")).ToStatement(),
            "UPDATE \"doc_bank_card\" t INNER JOIN \"doc_user\" t1 ON t1.\"ID\" = t.\"UID\" SET t.\"TYPE\" = t1.\"NAME\" WHERE t1.\"NAME\" LIKE ?", "%123%(String)");

    [Fact, Upstream("dameng/UpdateJoinTest.java#testUpdateJoin2")]
    public void TestUpdateJoin2() =>
        SqlOnly.AssertStatement(Db.Update<DocBankCard>().Set(c => new DocBankCard { Type = c.Bank!.Name }).Where(c => c.User!.Name!.Contains("123")).ToStatement(),
            "UPDATE \"doc_bank_card\" t INNER JOIN \"doc_bank\" t1 ON t1.\"ID\" = t.\"BANK_ID\" INNER JOIN \"doc_user\" t2 ON t2.\"ID\" = t.\"UID\" SET t.\"TYPE\" = t1.\"NAME\" WHERE t2.\"NAME\" LIKE ?", "%123%(String)");
}
