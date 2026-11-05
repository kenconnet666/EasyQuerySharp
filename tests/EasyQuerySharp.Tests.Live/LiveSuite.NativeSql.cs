namespace EasyQuerySharp.Tests.Live;

// 原生 SQL 与执行事件：插值参数、集合展开、标识符引号；每条语句（含失败的、事务里的）都通知监听器
public abstract partial class LiveSuite
{
    [Fact]
    public async Task RunsNativeSqlWithBoundValuesExpandedListsAndQuotedIdentifiers()
    {
        var db = await SeededAsync();
        // 标识符都用 Sql.Identifier：各库引号不同，Oracle、HANA 不加引号的名字还会被折成大写
        string[] names = ["alice", "bob"];
        var count = await db.SqlQueryAsync<long>(
            $"SELECT COUNT(*) FROM {Sql.Identifier("t_user")} WHERE {Sql.Identifier("age")} > {20} AND {Sql.Identifier("name")} IN ({names})", Ct);
        Assert.Equal([1L], count);
        long[] ids = [1, 2];
        Assert.Equal(2, await db.ExecuteSqlAsync($"UPDATE {Sql.Identifier("t_post")} SET {Sql.Identifier("title")} = {"renamed"} WHERE {Sql.Identifier("id")} IN ({ids})", Ct));
        Assert.Equal(2, await db.Query<Post>().Where(p => p.Title == "renamed").CountAsync(Ct));
    }

    [Fact]
    public async Task RejectsEmptyListsInsteadOfSendingABrokenStatement()
    {
        var db = await SeededAsync();
        long[] none = [];
        await Assert.ThrowsAsync<ArgumentException>(() => db.SqlQueryAsync<long>($"SELECT {Sql.Identifier("id")} FROM {Sql.Identifier("t_user")} WHERE {Sql.Identifier("id")} IN ({none})", Ct));
    }

    [Fact]
    public async Task ReportsEveryStatementWithRowsDurationAndErrors()
    {
        var db = await SeededAsync();
        using var listen = SqlCapture.Listen();
        await db.Query<User>().ToListAsync(Ct);
        await db.Delete<Post>().Where(p => p.Id == 1).ExecuteAsync(Ct);
        await Assert.ThrowsAsync<EasyQueryException>(() => db.SqlQueryAsync<long>($"SELECT * FROM {Sql.Identifier("eqs_no_such_table")}", Ct));
        await db.TransactionAsync(() => db.Query<Company>().CountAsync(Ct), cancellationToken: Ct);

        var events = listen.Events;
        Assert.Equal([(3L, true), (1L, true)], events.Take(2).Select(e => (e.Rows ?? -1, e.Exception is null)));
        Assert.NotNull(events[2].Exception);
        Assert.Contains(events.Skip(3), e => e.Statement.Text.Contains("COUNT", StringComparison.Ordinal));
        Assert.All(events, e => Assert.True(e.Elapsed >= TimeSpan.Zero));
    }
}
