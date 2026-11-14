namespace EasyQuerySharp.Tests.Live;

// C# 的写法与语义：整数相除、Math / 日期 / 可空值的映射、集合导航上的 LINQ 方法、带条件的终结方法。每个库上结果都与 C# 一致
public abstract partial class LiveSuite
{
    // int / int 向零截断（MySQL、Oracle、DuckDB… 的 / 得到小数）：30 / 7 = 4、(30 - 40) / 7 = -1、17 / 7 = 2、(17 - 40) / 7 = -3
    [Fact]
    public async Task DividesIntegersTowardZero()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().Where(u => u.Age != null).OrderBy(u => u.Id)
            .Select(u => new Draft<int?, int?>(u.Age / 7, (u.Age - 40) / 7)).ToListAsync(Ct);
        Assert.Equal([(4, -1), (2, -3)], rows.Select(r => (r.Value1, r.Value2)));
        Assert.Equal(["alice"], await db.Query<User>().Where(u => u.Age / 7 == 4).Select(u => u.Name).ToListAsync(Ct));
        // 有一边是小数时照常是小数除法
        Assert.Equal(4.25m, await db.Query<Post>().Where(p => p.Title == "orm tips").Select(p => p.Amount / 2 - 1).SingleAsync(Ct));
    }

    // Math.Max / Min / Clamp、x.Date、DateOnly.FromDateTime、GetValueOrDefault
    [Fact]
    public async Task MapsMathDateAndNullableHelpers()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id).Select(u => new
        {
            Max = Math.Max(u.Age ?? 0, 20),
            Min = Math.Min(u.Age ?? 0, 20),
            Clamp = Math.Clamp(u.Age ?? 0, 18, 25),
            Age = u.Age.GetValueOrDefault(),
            Or5 = u.Age.GetValueOrDefault(5),
        }).ToListAsync(Ct);
        Assert.Equal([(30, 20, 25, 30, 30), (20, 0, 18, 0, 5), (20, 17, 18, 17, 17)], rows.Select(r => (r.Max, r.Min, r.Clamp, r.Age, r.Or5)));

        var day = CreatedAt.Date;
        Assert.Equal(3, await db.Query<User>().CountAsync(u => u.CreatedAt.Date == day, Ct));
        Assert.Equal(3, await db.Query<User>().CountAsync(u => DateOnly.FromDateTime(u.CreatedAt) == DateOnly.FromDateTime(day), Ct));
        Assert.Equal(day, await db.Query<User>().Where(u => u.Name == "alice").Select(u => u.CreatedAt.Date).SingleAsync(Ct));
    }

    // 集合导航上的 Exists、Select(..).Contains(v)、MaxBy / MinBy、排序后的 LastOrDefault
    [Fact]
    public async Task TranslatesMoreLinqOnNavigations()
    {
        var db = await SeededAsync();
        Assert.Equal(["alice"], await db.Query<User>().Where(u => u.Posts!.Exists(p => p.Amount > 100)).Select(u => u.Name).ToListAsync(Ct));
        Assert.Equal(["bob"], await db.Query<User>().Where(u => u.Posts!.Select(p => p.Title).Contains("hello")).Select(u => u.Name).ToListAsync(Ct));
        var rows = await db.Query<User>().OrderBy(u => u.Id).Select(u => new
        {
            Top = u.Posts!.MaxBy(p => p.Amount)!.Title,
            Low = u.Posts!.MinBy(p => p.Amount)!.Title,
            Last = u.Posts!.OrderBy(p => p.Amount).ThenBy(p => p.Id).LastOrDefault()!.Title,
        }).ToListAsync(Ct);
        Assert.Equal([("sql 100%", "orm tips", "sql 100%"), ("hello", "orm_v2", "hello"), ((string?)null, (string?)null, (string?)null)],
            rows.Select(r => ((string?)r.Top, (string?)r.Low, (string?)r.Last)));
    }

    // 带条件的终结方法；AllAsync 把条件是 NULL 的行（bob 没有年龄）算作不满足，与 C# 一致
    [Fact]
    public async Task RunsTerminalsWithAPredicate()
    {
        var db = await SeededAsync();
        var users = db.Query<User>();
        Assert.True(await users.AnyAsync(u => u.Age > 25, Ct));
        Assert.False(await users.AnyAsync(u => u.Age > 99, Ct));
        Assert.Equal(2, await users.CountAsync(u => u.CompanyId != null, Ct));
        Assert.Equal("carol", (await users.FirstAsync(u => u.Age < 20, Ct)).Name);
        Assert.Null(await users.FirstOrDefaultAsync(u => u.Name == "nobody", Ct));
        Assert.Equal("bob", (await users.SingleAsync(u => u.Name == "bob", Ct)).Name);
        Assert.Null(await users.SingleOrDefaultAsync(u => u.Name == "nobody", Ct));
        Assert.True(await users.AllAsync(u => u.Name != "", Ct));
        Assert.False(await users.AllAsync(u => u.Age > 10, Ct));
        Assert.True(await users.Where(u => u.Age != null).AllAsync(u => u.Age > 10, Ct));
        Assert.True(await users.Where(u => u.Name == "nobody").AllAsync(u => u.Age > 99, Ct));

        Assert.Equal(["alice", "bob", "carol"], await users.OrderBy(u => u.Id).Select(u => u.Name).ToArrayAsync(Ct));
        var byName = await users.ToDictionaryAsync(u => u.Name, Ct);
        Assert.Equal(30, byName["alice"].Age);
        var ages = await users.ToDictionaryAsync(u => u.Name, u => u.Age, Ct);
        Assert.Equal((30, (int?)null, 17), (ages["alice"], ages["bob"], ages["carol"]));
    }
}
