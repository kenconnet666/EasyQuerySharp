using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// C# 版独有、没有对应 Java 用例的公开方法（便捷写法、C# 风格的入口），确保每个公开方法至少有一个用例。
/// 不标 <c>[Upstream]</c>，不计入对标统计。
/// </summary>
public sealed class PublicApiTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact]
    public async Task FindAsyncQueriesByPrimaryKey()
    {
        using var listen = SqlCapture.Listen();
        var topic = await Db.Query<Topic>().FindAsync("1", Ct);
        Assert.Equal("1", topic?.Id);
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? LIMIT 1");
        Assert.Null(await Db.Query<Topic>().FindAsync("no-such-id", Ct));
    }

    [Fact]
    public async Task TransactionAsyncCommitsOrRollsBack()
    {
        const string committed = "tx-commit", rolledBack = "tx-rollback";
        await Db.Delete<Topic>().WhereByIds(new[] { committed, rolledBack }).ExecuteAsync(Ct);

        await Db.TransactionAsync(() => Db.Insert(new Topic { Id = committed, Stars = 1, Title = "t", CreateTime = DateTime.Now }).ExecuteAsync(Ct), cancellationToken: Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.TransactionAsync(async () =>
        {
            await Db.Insert(new Topic { Id = rolledBack, Stars = 1, Title = "t", CreateTime = DateTime.Now }).ExecuteAsync(Ct);
            throw new InvalidOperationException("中途失败");
        }, cancellationToken: Ct));

        Assert.NotNull(await Db.Query<Topic>().FindAsync(committed, Ct));
        Assert.Null(await Db.Query<Topic>().FindAsync(rolledBack, Ct));
        await Db.Delete<Topic>().WhereById(committed).ExecuteAsync(Ct);
    }

    // 不再追踪后按实体更新写全部列，而不是只写变化的列
    [Fact]
    public async Task RemoveTrackingFallsBackToFullUpdate()
    {
        using var listen = SqlCapture.Listen();
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().FirstAsync(Ct);
            Assert.True(Db.RemoveTracking(topic));
            Assert.False(Db.RemoveTracking(topic));
            await Db.Update(topic).ExecuteAsync(Ct);
        }
        listen.AssertSql("UPDATE `t_topic` SET `stars` = ?,`title` = ?,`create_time` = ? WHERE `id` = ?");
    }

    [Fact]
    public void SetIfSkipsFalseConditions() =>
        Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ?",
            Db.Update<Topic>().SetIf(true, t => t.Title, "a").SetIf(false, t => t.Stars, 1).WhereById("x").ToSql());

    [Fact]
    public async Task WindowFrameWithOffsets()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<Topic>()
            .Select(t => new Draft<string?, int?>(t.Id, Sql.Over.Sum(t.Stars, w => w.OrderBy(t.Id).Rows(WindowBound.Preceding(1), WindowBound.Following(1)))))
            .ToListAsync(Ct);
        Assert.NotEmpty(rows);
        Assert.Contains("ROWS BETWEEN 1 PRECEDING AND 1 FOLLOWING", listen.Last.Statement.Text);
    }
}
