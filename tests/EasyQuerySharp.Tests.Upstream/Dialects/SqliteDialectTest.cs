using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>SQLite 方言自己的写法（C# 独有）：没有 LPAD / RPAD，用 zeroblob 拼出填充字符。</summary>
public sealed class SqliteDialectTest
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void PadsWithRepeatedCharactersInsteadOfLpad() =>
        SqlOnly.AssertStatement(SqlOnly.Db("sqlite").Query<Topic>().Where(t => t.Title!.PadLeft(5, '0') == "00abc").Select(t => t.Title!.PadRight(4)).ToStatement(),
            "SELECT (t.\"title\" || substr(replace(hex(zeroblob(4)), '00', ' '), 1, 4 - length(t.\"title\"))) FROM \"t_topic\" t "
            + "WHERE (substr(replace(hex(zeroblob(5)), '00', ?), 1, 5 - length(t.\"title\")) || t.\"title\") = ?",
            "0(String),00abc(String)");

    // 只补到 n 个字符，字符串更长时原样返回（与 .NET 一致）；NULL 仍是 NULL
    [Fact]
    public async Task PadsOnlyShorterStrings()
    {
        var cs = $"Data Source=eqs-sqlite-pad-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync(Ct);
        var db = new EasyDb(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(cs)));
        await db.ExecuteSqlRawAsync("CREATE TABLE t_topic (id TEXT PRIMARY KEY, stars INTEGER, title TEXT, create_time TEXT)", cancellationToken: Ct);
        await db.ExecuteSqlRawAsync("INSERT INTO t_topic (id, title) VALUES ('1', 'ab'), ('2', 'abcdefg'), ('3', NULL)", cancellationToken: Ct);
        var rows = await db.Query<Topic>().OrderBy(t => t.Id).Select(t => new Draft<string?, string?>(t.Title!.PadLeft(5, '*'), t.Title!.PadRight(5))).ToListAsync(Ct);
        Assert.Equal([("***ab", "ab   "), ("abcdefg", "abcdefg"), (null, null)], rows.Select(r => (r.Value1, r.Value2)));
    }
}
