namespace EasyQuerySharp.Tests.Live;

// 函数与 CASE：字符串 / 数学 / 日期加减与相差 / 类型转换与 NULLIF / CASE WHEN
public abstract partial class LiveSuite
{
    // 第 1 篇帖子的标题是 'orm tips'；C# 下标从 0 起，IndexOf 找不到是 -1
    [Fact]
    public async Task RunsStringFunctions()
    {
        var db = await SeededAsync();
        var row = await db.Query<Post>().Where(p => p.Id == 1)
            .Select(p => new StringParts(p.Title.Substring(0, 3), p.Title.Substring(4), p.Title.Replace("orm", "ORM"), p.Title.IndexOf("tips"), p.Title.IndexOf("zzz")))
            .SingleAsync(Ct);
        Assert.Equal(new StringParts("orm", "tips", "ORM tips", 4, -1), row);
    }

    private sealed record StringParts(string Head, string Tail, string Replaced, int Found, int Missing);

    // 第 3 篇帖子金额 1.25；alice 30 岁
    [Fact]
    public async Task RunsMathFunctions()
    {
        var db = await SeededAsync();
        var post = await db.Query<Post>().Where(p => p.Id == 3)
            .Select(p => new MathParts(Math.Round(p.Amount, 1), Math.Floor(p.Amount), Math.Ceiling(p.Amount), Math.Abs(p.Amount - 5m)))
            .SingleAsync(Ct);
        Assert.Equal(new MathParts(1.3m, 1m, 2m, 3.75m), post);

        var user = await db.Query<User>().Where(u => u.Name == "alice")
            .Select(u => new Draft<int, int>(u.Age!.Value % 7, Math.Abs(u.Age!.Value - 40)))
            .SingleAsync(Ct);
        Assert.Equal((2, 10), (user.Value1, user.Value2));
    }

    private sealed record MathParts(decimal Rounded, decimal Floor, decimal Ceil, decimal Abs);

    // created_at = 2026-01-02 03:04:05；日期相差按整数天截断
    [Fact]
    public async Task AddsDaysAndMonthsAndDiffsDates()
    {
        var db = await SeededAsync();
        var row = await db.Query<User>().Where(u => u.Name == "alice")
            .Select(u => new DateAdds(
                u.CreatedAt.AddDays(30).Month, u.CreatedAt.AddDays(30).Day, u.CreatedAt.AddDays(-2).Year,
                u.CreatedAt.AddMonths(11).Month, u.CreatedAt.AddMonths(12).Year, (u.CreatedAt - new DateTime(2025, 12, 31)).Days))
            .SingleAsync(Ct);
        Assert.Equal(new DateAdds(2, 1, 2025, 12, 2027, 2), row);
    }

    private sealed record DateAdds(int Plus30Month, int Plus30Day, int Minus2Year, int Plus11Month, int Plus12Year, int Diff);

    [Fact]
    public async Task CastsAndNullIfs()
    {
        var db = await SeededAsync();
        var row = await db.Query<User>().Where(u => u.Name == "alice")
            .Select(u => new CastParts(Sql.Cast<string>(u.Age), Sql.Cast<double>(u.Age), Sql.Cast<decimal>(u.Age), u.Age.NullIf(30), u.Age.NullIf(31)))
            .SingleAsync(Ct);
        Assert.Equal(("30", 30d, 30m, null, 30), (row.Text, row.Double, row.Decimal, row.Nulled, row.Kept));
    }

    private sealed record CastParts(string Text, double Double, decimal Decimal, int? Nulled, int? Kept);

    // bob 的年龄是 NULL，比较结果是 NULL，落到 ELSE
    [Fact]
    public async Task BuildsCaseWhenWithLiteralAndExpressionBranches()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new CaseParts(u.Name, u.Age < 18 ? "minor" : u.Age < 65 ? "adult" : "unknown", u.Age >= 18 ? 1 : 0, u.Age == null ? u.Name.ToUpper() : null))
            .ToListAsync(Ct);
        Assert.Equal(
        [
            new CaseParts("alice", "adult", 1, null),
            new CaseParts("bob", "unknown", 0, "BOB"),
            new CaseParts("carol", "minor", 0, null),
        ], rows);
    }

    private sealed record CaseParts(string Name, string Group, int Adult, string? Label);

    // CASE 当条件，也当聚合的参数（金额大于 5 的帖子数：10.50 与 2000.00 两篇）
    [Fact]
    public async Task UsesCaseInsideConditionsAndAggregates()
    {
        var db = await SeededAsync();
        Assert.Equal(["alice"], await db.Query<User>().Where(u => (u.Age >= 18 ? "adult" : "minor") == "adult").Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(2, await db.Query<Post>().SumAsync(p => p.Amount > 5m ? 1 : 0, Ct));
    }
}
