namespace EasyQuerySharp.Tests.Live;

// 函数库（对应官方 func/*）：字符串补位 / 去空格 / 比较 / 模板、数学、日期各部分与格式化、MaxColumns / MinColumns、字符串聚合、JSON 函数、窗口取值函数
public abstract partial class LiveSuite
{
    // 去空格、比较、字符串模板、反向的 StartsWith / EndsWith
    [Fact]
    public async Task RunsStringLibraryFunctions()
    {
        var db = await SeededAsync();
        var alice = await db.Query<User>().Where(u => u.Name == "alice")
            .Select(u => new StringLibrary(("  " + u.Name + "  ").TrimStart().TrimEnd(), u.Name.CompareTo("bob"), u.Name.CompareTo("alice"), "用户 " + u.Name + "，" + u.Age + " 岁"))
            .SingleAsync(Ct);
        Assert.Equal(new StringLibrary("alice", -1, 0, "用户 alice，30 岁"), alice);

        var names = await db.Query<User>().Where(u => !u.Name.StartsWith("a") && !u.Name.EndsWith("ol")).Select(u => u.Name).ToListAsync(Ct);
        Assert.Equal(["bob"], names);
    }

    private sealed record StringLibrary(string Trimmed, int Compare, int Same, string Label);

    [Fact]
    public async Task PadsStrings()
    {
        SkipUnless(Options.PadFunctions, "LPAD / RPAD");
        var db = await SeededAsync();
        var row = await db.Query<User>().Where(u => u.Name == "alice")
            .Select(u => new Draft<string, string, string, string>(u.Name.PadLeft(7, '*'), u.Name.PadRight(7, '-'), u.Name.PadLeft(6), u.Name.PadRight(3)))
            .SingleAsync(Ct);
        // 够长时与 LPAD / RPAD 一样截成前 n 个字符（.NET 的 PadLeft 不截断）
        Assert.Equal(("**alice", "alice--", " alice", "ali"), (row.Value1, row.Value2, row.Value3, row.Value4));
    }

    // 第 1 篇帖子金额 10.50
    [Fact]
    public async Task RunsMathLibraryFunctions()
    {
        var db = await SeededAsync();
        var row = await db.Query<Post>().Where(p => p.Title == "orm tips")
            .Select(p => new MathLibrary(
                Math.Sign(p.Amount - 20m), Math.Pow((double)p.Amount, 2), Math.Sqrt((double)p.Amount * 4),
                Math.Log((double)p.Amount), Math.Log10((double)p.Amount * 10), Math.Atan2((double)p.Amount, (double)p.Amount)))
            .SingleAsync(Ct);
        Assert.Equal(-1, row.Sign);
        Assert.Equal(110.25, row.Power, 9);
        Assert.Equal(Math.Sqrt(42), row.Sqrt, 9);
        Assert.Equal(Math.Log(10.5), row.Ln, 9);
        Assert.Equal(Math.Log10(105), row.Log10, 9);
        Assert.Equal(Math.PI / 4, row.Atan2, 9);
    }

    private sealed record MathLibrary(int Sign, double Power, double Sqrt, double Ln, double Log10, double Atan2);

    [Fact]
    public async Task TruncatesDecimals()
    {
        SkipUnless(Options.TruncateWithScale, "带位数的 TRUNC");
        var db = await SeededAsync();
        Assert.Equal(10m, await db.Query<Post>().Where(p => p.Title == "orm tips").Select(p => Math.Truncate(p.Amount)).SingleAsync(Ct));
    }

    // C# 的 Math.Log(x, newBase) 与多数库的 LOG(base, x) 参数顺序相反：10.5 - 2.5 = 8 的以 2 为底的对数是 3
    [Fact]
    public async Task ComputesLogarithmsWithABase()
    {
        var db = await SeededAsync();
        var log = await db.Query<Post>().Where(p => p.Title == "orm tips").Select(p => Math.Log((double)p.Amount - 2.5, 2)).SingleAsync(Ct);
        Assert.Equal(3, log, 9);
    }

    // 2026-01-02T03:04:05.006 是星期五
    [Fact]
    public async Task RunsDateLibraryFunctions()
    {
        var db = await SeededAsync();
        var row = await db.Query<User>().Where(u => u.Name == "alice")
            .Select(u => new DateLibrary(
                u.CreatedAt.Hour, u.CreatedAt.Minute, u.CreatedAt.Second, u.CreatedAt.DayOfYear, (int)u.CreatedAt.DayOfWeek, u.CreatedAt.IsoDayOfWeek(),
                u.CreatedAt.ToString("yyyy/MM/dd HH:mm:ss 写入"), u.CreatedAt.AddHours(2), u.CreatedAt.AddYears(1), u.CreatedAt.AddDays(u.Age!.Value),
                (u.CreatedAt.AddHours(5).AddMinutes(30) - u.CreatedAt).TotalHours, (u.CreatedAt - u.CreatedAt.AddMinutes(90)).TotalMinutes))
            .SingleAsync(Ct);
        Assert.Equal((3, 4, 5, 2, 5, 5), (row.Hour, row.Minute, row.Second, row.DayOfYear, row.DayOfWeek, row.IsoDayOfWeek));
        Assert.Equal("2026/01/02 03:04:05 写入", row.Text);
        // 有的库的日期加减会丢掉毫秒，只比到秒
        Assert.Equal(new DateTime(2026, 1, 2, 5, 4, 5), row.Later.AddTicks(-row.Later.Ticks % TimeSpan.TicksPerSecond));
        Assert.Equal(new DateTime(2027, 1, 2, 3, 4, 5), row.NextYear.AddTicks(-row.NextYear.Ticks % TimeSpan.TicksPerSecond));
        Assert.Equal(new DateTime(2026, 2, 1, 3, 4, 5), row.ByColumn.AddTicks(-row.ByColumn.Ticks % TimeSpan.TicksPerSecond));
        Assert.Equal((5d, -90d), (row.Hours, row.Minutes));
        Assert.Equal(3, await db.Query<User>().Where(u => Sql.Now() > u.CreatedAt).CountAsync(Ct));
        // 达梦、openGauss 的容器时区是 Asia/Shanghai：UTC 时间取成本地时间会差 8 小时
        var utcNow = await db.Query<User>().Where(u => u.Name == "alice").Select(u => Sql.UtcNow()).SingleAsync(Ct);
        Assert.InRange(utcNow, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(10));
    }

    // 方言接受的每个格式符，结果都与 .NET 格式化读回的同一个值一致；不接受的在渲染时报不支持，不当文字输出
    [Fact]
    public async Task FormatsDatesLikeDotNetOrRefuses()
    {
        var db = await SeededAsync();
        var alice = db.Query<User>().Where(u => u.Name == "alice");
        var createdAt = await alice.Select(u => u.CreatedAt).SingleAsync(Ct);
        string[] formats =
        [
            "yyyy", "yy", "MM", "M", "dd", "d", "HH", "H", "hh", "h", "mm", "m", "ss", "s", "fff", "ffffff", "tt",
            "yyyy-MM-ddTHH:mm", "'at' HH \\h", "yyyy\\%MM", "yyyy%MM", "%d",
        ];
        var supported = 0;
        foreach (var format in formats)
        {
            var query = alice.Select(u => u.CreatedAt.ToString(format));
            string text;
            try
            {
                text = await query.SingleAsync(Ct);
            }
            catch (NotSupportedException e) when (e.Message.Contains($"\"{format}\"", StringComparison.Ordinal) || e.Message.Contains("的日期格式不支持", StringComparison.Ordinal))
            {
                continue;
            }
            Assert.True(createdAt.ToString(format, System.Globalization.CultureInfo.InvariantCulture) == text, $"{format}: {text} ({query.ToSql()})");
            supported++;
        }
        Assert.True(supported >= 6, $"只支持 {supported} 种格式");
    }

    private sealed record DateLibrary(
        int Hour, int Minute, int Second, int DayOfYear, int DayOfWeek, int IsoDayOfWeek, string Text, DateTime Later, DateTime NextYear, DateTime ByColumn, double Hours, double Minutes);

    // 几个值取最大 / 最小，NULL 不参与
    [Fact]
    public async Task TakesTheGreatestAndLeastIgnoringNull()
    {
        var db = await SeededAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new Draft<int?, int?>(Sql.MaxColumns(u.Age, 20), Sql.MinColumns(u.Age, 20)))
            .ToListAsync(Ct);
        // bob 的年龄是 NULL：PG / SQL Server / Oracle 忽略它；SQLite 的 MAX(a, b) 与 MySQL 系的 GREATEST 返回 NULL
        int? bobMax = Options.ExtremesIgnoreNull ? 20 : null;
        int? bobMin = Options.ExtremesIgnoreNull ? 20 : null;
        Assert.Equal([(30, 20), (bobMax, bobMin), (20, 17)], rows.Select(r => (r.Value1, r.Value2)));
    }

    // 分组里与集合导航上的字符串聚合；分组合并后的导航同样可以聚合
    [Fact]
    public async Task JoinsStringsInGroupsAndOnNavigations()
    {
        var db = await SeededAsync();
        var grouped = await db.Query<Post>().GroupBy(p => p.UserId).OrderBy(g => g.Key)
            .Select(g => new Draft<long, string>(g.Key, g.StringJoin(p => p.Title, "|")))
            .ToListAsync(Ct);
        // 组内顺序库不保证，拆开排序后比较
        Assert.Equal([["orm tips", "sql 100%"], ["hello", "orm_v2"]], grouped.Select(r => r.Value2.Split('|').Order(StringComparer.Ordinal).ToArray()));

        var users = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new Draft<string?>(string.Join(",", u.Posts!.OrderBy(p => p.Id).Select(p => p.Title))))
            .ToListAsync(Ct);
        Assert.Equal(["orm tips,sql 100%", "orm_v2,hello", null], users.Select(r => r.Value1));
    }

    // 倒序拼接：ORDER BY 写进聚合函数里才能决定拼接顺序
    [Fact]
    public async Task JoinsStringsInDescendingOrderOnNavigations()
    {
        var db = await SeededAsync();
        var users = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new Draft<string?>(string.Join("、", u.Posts!.OrderByDescending(p => p.Id).Select(p => p.Title))))
            .ToListAsync(Ct);
        Assert.Equal(["sql 100%、orm tips", "hello、orm_v2", null], users.Select(r => r.Value1));
    }

    // 种子里 alice 的属性是 {"tags":["vip"]}，其余是 {"tags":[]}
    [Fact]
    public async Task RunsJsonFunctions()
    {
        SkipUnless(Options.JsonFunctions, "Sql.Json 函数");
        var db = await SeededAsync();
        var rows = await db.Query<User>().OrderBy(u => u.Id)
            .Select(u => new Draft<string?, bool>(Sql.Json(u.Attrs).Get("tags").Text(0), Sql.Json(u.Attrs).ContainsKey("tags")))
            .ToListAsync(Ct);
        Assert.Equal([("vip", true), (null, true), (null, true)], rows.Select(r => (r.Value1, r.Value2)));
        Assert.Equal(["alice"], await db.Query<User>().Where(u => Sql.Json(u.Attrs).Get("tags").Text(0) == "vip").Select(u => u.Name).ToListAsync(Ct));
        if (Options.JsonLength)
            Assert.Equal([1, 0, 0], await db.Query<User>().OrderBy(u => u.Id).Select(u => Sql.Json(u.Attrs).Get("tags").Length()).ToListAsync(Ct));
    }

    // 窗口取值函数：每个用户第一篇与最后一篇（最后一个值要把 frame 拉到分区末尾）、第二个值
    [Fact]
    public async Task RunsWindowValueFunctions()
    {
        var db = await SeededAsync();
        var rows = await db.Query<Post>().OrderBy(p => p.Id)
            .Select(p => new Draft<string, string>(
                Sql.Over.FirstValue(p.Title, w => w.PartitionBy(p.UserId).OrderBy(p.Id)),
                Sql.Over.LastValue(p.Title, w => w.PartitionBy(p.UserId).OrderBy(p.Id).Rows(WindowBound.UnboundedPreceding, WindowBound.UnboundedFollowing))))
            .ToListAsync(Ct);
        Assert.Equal(("orm tips", "sql 100%"), (rows[0].Value1, rows[0].Value2));
        Assert.Equal(("orm_v2", "hello"), (rows[3].Value1, rows[3].Value2));
    }

    [Fact]
    public async Task RunsNthValueWhereTheDatabaseHasIt()
    {
        SkipUnless(Options.NthValue, "NTH_VALUE");
        var db = await SeededAsync();
        var second = await db.Query<Post>().OrderBy(p => p.Id)
            .Select(p => Sql.Over.NthValue(p.Title, 2, w => w.PartitionBy(p.UserId).OrderBy(p.Id).Rows(WindowBound.UnboundedPreceding, WindowBound.UnboundedFollowing)))
            .ToListAsync(Ct);
        Assert.Equal(["sql 100%", "sql 100%", "hello", "hello"], second);
    }
}
