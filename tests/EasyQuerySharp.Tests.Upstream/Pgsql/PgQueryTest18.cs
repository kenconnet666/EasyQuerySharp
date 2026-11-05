using EasyQuerySharp.Tests.Upstream.Mysql8;

namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>对应 Java：pgsql/QueryTest18.java（LIKE 的值含 % _ 时转义并写 ESCAPE；PG 上拼接的值转成 TEXT）。</summary>
public sealed class PgQueryTest18(PgFixture fixture) : PgTest(fixture)
{
    private const string Columns = "SELECT \"column1\",\"column2\",\"column3\" FROM \"no_key_table\" WHERE ";

    // Java 用例里自己写的 startsWith：值含通配符时转义后用原生片段
    private static string Escape(string input) => input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    [Fact, Upstream("pgsql/QueryTest18.java#testLikeStartsWith")]
    public async Task TestLikeStartsWith()
    {
        const string id = "testLikeStartsWith";
        await Db.CodeFirst.CreateTablesAsync([typeof(TableNoKey)], Ct);
        await Db.Delete<TableNoKey>().Where(t => t.Column1 == id).ExecuteAsync(Ct);
        try
        {
            await Db.Insert(new TableNoKey { Column1 = id, Column2 = "小_明%在哪里" }).ExecuteAsync(Ct);
            var pattern = Escape("小_明%");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => Sql.Raw<bool>("{0} LIKE CONCAT({1},'%') ESCAPE '\\'", t.Column2, pattern)).ToListAsync(Ct));

            using var listen = SqlCapture.Listen();
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2!.StartsWith("小_明%")).ToListAsync(Ct));
            listen.AssertSql(Columns + "\"column2\" LIKE CONCAT((?)::TEXT,'%') ESCAPE '\\'");
            listen.AssertParams("小\\_明\\%(String)");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2!.Contains("小_明%")).ToListAsync(Ct));
            listen.AssertSql(Columns + "\"column2\" LIKE CONCAT('%',(?)::TEXT,'%') ESCAPE '\\'");
            listen.AssertParams("小\\_明\\%(String)");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2!.EndsWith("小_明%在哪里")).ToListAsync(Ct));
            listen.AssertSql(Columns + "\"column2\" LIKE CONCAT('%',(?)::TEXT) ESCAPE '\\'");
            listen.AssertParams("小\\_明\\%在哪里(String)");
            Assert.NotEmpty(await Db.Query<TableNoKey>().Where(t => t.Column2!.StartsWith("小_明%") && t.Column2.Contains("小_明%")).ToListAsync(Ct));
            listen.AssertSql(Columns + "\"column2\" LIKE CONCAT((?)::TEXT,'%') ESCAPE '\\' AND \"column2\" LIKE CONCAT('%',(?)::TEXT,'%') ESCAPE '\\'");
            listen.AssertParams("小\\_明\\%(String),小\\_明\\%(String)");
        }
        finally
        {
            await Db.Delete<TableNoKey>().Where(t => t.Column1 == id).ExecuteAsync(Ct);
        }
    }
}
