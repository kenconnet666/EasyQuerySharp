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
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ?");
        Assert.Null(await Db.Query<Topic>().FindAsync("no-such-id", Ct));
    }

    // 用版本表（只有 VersionTest 按固定 id 读写）做实验，不改动别的用例会计数的表
    [Fact]
    public async Task TransactionAsyncCommitsOrRollsBack()
    {
        const string committed = "tx-commit", rolledBack = "tx-rollback";
        SysUserVersionLong User(string id) => new() { Id = id, Version = 1L, CreateTime = DateTime.Now };
        await Db.Delete<SysUserVersionLong>().WhereByIds(new[] { committed, rolledBack }).IgnoreVersion().ExecuteAsync(Ct);

        await Db.TransactionAsync(() => Db.Insert(User(committed)).ExecuteAsync(Ct), cancellationToken: Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.TransactionAsync(async () =>
        {
            await Db.Insert(User(rolledBack)).ExecuteAsync(Ct);
            throw new InvalidOperationException("中途失败");
        }, cancellationToken: Ct));

        Assert.NotNull(await Db.Query<SysUserVersionLong>().FindAsync(committed, Ct));
        Assert.Null(await Db.Query<SysUserVersionLong>().FindAsync(rolledBack, Ct));
        await Db.Delete<SysUserVersionLong>().WhereById(committed).IgnoreVersion().ExecuteAsync(Ct);
    }

    // 追踪的实体没有变化时不写；不再追踪后按实体更新写全部列。只渲染不执行，不改动共用的数据
    [Fact]
    public async Task RemoveTrackingFallsBackToFullUpdate()
    {
        using (Db.BeginTracking())
        {
            var topic = await Db.Query<Topic>().FirstAsync(Ct);
            Assert.Throws<InvalidOperationException>(() => Db.Update(topic).ToSql());
            Assert.True(Db.RemoveTracking(topic));
            Assert.False(Db.RemoveTracking(topic));
            Assert.Equal("UPDATE `t_topic` SET `stars` = ?,`title` = ?,`create_time` = ? WHERE `id` = ?", Db.Update(topic).ToSql());
        }
    }

    [Fact]
    public void SetIfSkipsFalseConditions() =>
        Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ?",
            Db.Update<Topic>().SetIf(true, t => t.Title, "a").SetIf(false, t => t.Stars, 1).WhereById("x").ToSql());

    [Fact]
    public void ChangesWritesOnlyTheColumnsThatDiffer()
    {
        var before = new Topic { Id = "1", Title = "a", Stars = 1, CreateTime = new DateTime(2020, 1, 1) };
        var after = new Topic { Id = "1", Title = "b", Stars = 1, CreateTime = before.CreateTime };
        Assert.Equal("UPDATE `t_topic` SET `title` = ? WHERE `id` = ?", Db.Update(after).Changes(before).ToSql());
        // 没有不同的列：不发语句
        Assert.Throws<InvalidOperationException>(() => Db.Update(new Topic { Id = "1", Title = "a" }).Changes(new Topic { Id = "1", Title = "a" }).ToSql());
        Assert.Throws<InvalidOperationException>(() => Db.Update(after, before).Changes(before));
    }

    [Fact]
    public void DecrementIfSkipsFalseConditions() =>
        Assert.Equal("UPDATE `t_topic` SET `stars` = `stars` - ? WHERE `id` = ?",
            Db.Update<Topic>().DecrementIf(true, t => t.Stars, 1).DecrementIf(false, t => t.Stars, 2).WhereById("x").ToSql());

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

    // 沿用查询排序的 LAG 不能随深分页把排序整体反转，否则取到的是下一行
    [Fact]
    public async Task InheritedWindowOrderIsNotReversedForDeepPages()
    {
        var query = Db.Query<Topic>().OrderBy(t => t.Id).Select(t => new Draft<string?, string?>(t.Id, Sql.Over.Lag(t.Id, 1)));
        var total = await query.CountAsync(Ct);
        using var listen = SqlCapture.Listen();
        var page = await query.ToPageAsync(total / 5, 5, Ct);
        Assert.EndsWith("ORDER BY t.`id` ASC LIMIT 5 OFFSET " + (total / 5 - 1) * 5, listen.Last.Statement.Text);
        for (var i = 1; i < page.Rows.Count; i++) Assert.Equal(page.Rows[i - 1].Value1, page.Rows[i].Value2);
    }

    [Fact]
    public void OptionsAreFrozenOnceAnEasyDbUsesThem()
    {
        var options = new EasyDbOptions().UseSqlServer();
        Assert.Same(EasyQuerySharp.Dialects.SqlServerDialect.Instance, options.Dialect);
        Assert.Same(EasyQuerySharp.Dialects.OracleDialect.Instance, new EasyDbOptions().UseOracle().Dialect);
        _ = new EasyDb(options);
        Assert.Throws<InvalidOperationException>(() => options.MaxInClauseSize = 1000);
        Assert.Throws<InvalidOperationException>(() => options.UseOracle());
        Assert.Throws<InvalidOperationException>(() => options.AddListener(SqlCapture.Instance));
    }
}
