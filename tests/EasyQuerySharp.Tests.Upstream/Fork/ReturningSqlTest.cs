namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：dialect/ReturningSqlTest（UPDATE / DELETE … RETURNING 的 SQL；没有 RETURNING 的库渲染时拒绝）。</summary>
public sealed class ReturningSqlTest
{
    [Fact, Upstream("fork/dialect/ReturningSqlTest.java#deleteAndUpdateReturning")]
    public void DeleteAndUpdateReturning()
    {
        var db = SqlOnly.Db("pgsql");
        SqlOnly.AssertStatement(db.Delete<ForkPost>().Where(p => p.UserId == "u1").Returning().ToStatement(),
            "DELETE FROM \"t_fork_post\" WHERE \"user_id\" = ? RETURNING *", "u1(String)");
        SqlOnly.AssertStatement(db.Update<ForkPost>().Set(p => p.Title, "x").Where(p => p.UserId == "u1").Returning().ToStatement(),
            "UPDATE \"t_fork_post\" SET \"title\" = ? WHERE \"user_id\" = ? RETURNING *", "x(String),u1(String)");
        // 只取选择器用到的列
        Assert.Equal("UPDATE \"t_fork_post\" SET \"title\" = ? WHERE \"user_id\" = ? RETURNING \"id\",\"title\"",
            db.Update<ForkPost>().Set(p => p.Title, "x").Where(p => p.UserId == "u1").Returning(p => new { p.Id, Upper = p.Title!.ToUpperInvariant() }).ToSql());
    }

    [Theory, Upstream("fork/dialect/ReturningSqlTest.java#otherDialectsAreRejected")]
    [InlineData("mysql")]
    [InlineData("mssql")]
    [InlineData("oracle")]
    public void OtherDialectsAreRejected(string dialect)
    {
        var db = SqlOnly.Db(dialect);
        Assert.Throws<NotSupportedException>(() => db.Delete<ForkPost>().Where(p => p.UserId == "u1").Returning().ToSql());
        Assert.Throws<NotSupportedException>(() => db.Update<ForkPost>().Set(p => p.Title, "x").Where(p => p.UserId == "u1").Returning().ToSql());
    }

    [Fact]
    public void ReturningNeedsAConditionalWriteOnTheTableItself()
    {
        var db = SqlOnly.Db("pgsql");
        Assert.Throws<InvalidOperationException>(() => db.Delete(new ForkPost { Id = "p1" }).Returning());
        Assert.Throws<NotSupportedException>(() => db.Delete<ForkUser>().Where(u => u.Posts!.Any(p => p.Amount > 1)).Returning(u => u.Posts).ToSql());
    }
}
