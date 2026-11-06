using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：live/HanaDialectLiveTest.java（HANA 实库上的增删改查、MERGE、导航更新删除、LIKE 转义、分页、函数、聚合）。
/// 在 <see cref="HanaFixture"/> 的 schema 里执行，核对 Java 的 SQL 文本与读回的结果；没有配置 HANA 时跳过。
/// </summary>
public sealed class HanaDialectLiveTest(HanaFixture hana)
{
    private const string UserColumns = "\"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\"";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTime CreatedAt = new(2026, 11, 6, 9, 30, 0);

    private static ForkUser User(string id = "u1", string? name = "n1", int? age = 20, string? companyId = "c1") =>
        new() { Id = id, Name = name, Age = age, CompanyId = companyId, Attrs = "{}", CreatedAt = CreatedAt };

    private async Task<EasyDb> FreshAsync()
    {
        var db = hana.Db;
        Type[] tables = [typeof(ForkPost), typeof(ForkUser), typeof(ForkCompany)];
        await db.CodeFirst.DropTablesIfExistsAsync(tables, Ct);
        await db.CodeFirst.CreateTablesAsync(tables, Ct);
        return db;
    }

    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#plainCrudAndForUpdate")]
    public async Task PlainCrudAndForUpdate()
    {
        var db = await FreshAsync();
        using var listen = SqlCapture.Listen();

        await db.Insert(User()).ExecuteAsync(Ct);
        listen.AssertSql($"INSERT INTO \"t_fork_user\" ({UserColumns}) VALUES (?,?,?,?,?,?)");
        listen.AssertParams("u1(String),n1(String),20(Integer),c1(String),{}(String),2026-11-06T09:30(LocalDateTime)");

        await using (var tx = db.BeginTransaction())
        {
            var byId = $"SELECT {UserColumns} FROM \"t_fork_user\" WHERE \"id\" = ?";
            Assert.Single(await db.Query<ForkUser>().Where(u => u.Id == "u1").ForUpdate().ToListAsync(Ct));
            listen.AssertSql(byId + " FOR UPDATE");
            Assert.Single(await db.Query<ForkUser>().Where(u => u.Id == "u1").ForUpdateNoWait().ToListAsync(Ct));
            listen.AssertSql(byId + " FOR UPDATE NOWAIT");
            Assert.Single(await db.Query<ForkUser>().Where(u => u.Id == "u1").ForUpdateSkipLocked().ToListAsync(Ct));
            listen.AssertSql(byId + " FOR UPDATE IGNORE LOCKED");
        }

        Assert.Equal(1, await db.Update(User(age: 21)).ExecuteAsync(Ct));
        listen.AssertSql("UPDATE \"t_fork_user\" SET \"name\" = ?,\"age\" = ?,\"company_id\" = ?,\"attrs\" = ?,\"created_at\" = ? WHERE \"id\" = ?");
        listen.AssertParams("n1(String),21(Integer),c1(String),{}(String),2026-11-06T09:30(LocalDateTime),u1(String)");
        var saved = await db.Query<ForkUser>().WhereById("u1").SingleAsync(Ct);
        Assert.Equal((21, CreatedAt), (saved.Age, saved.CreatedAt));

        Assert.Equal(1, await db.Delete(User()).ExecuteAsync(Ct));
        listen.AssertSql("DELETE FROM \"t_fork_user\" WHERE \"id\" = ?");
        Assert.Equal(0, await db.Query<ForkUser>().CountAsync(Ct));
    }

    // 冲突时更新指定的列；忽略冲突时没有 WHEN MATCHED
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#mergeUpsert")]
    public async Task MergeUpsert()
    {
        var db = await FreshAsync();
        await db.Insert(User()).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        const string source = "MERGE INTO \"t_fork_user\" t1 USING (SELECT ? AS \"id\",? AS \"name\",? AS \"age\",? AS \"company_id\",? AS \"attrs\",? AS \"created_at\" FROM DUMMY) t2 ON (t1.\"id\" = t2.\"id\")";
        const string insert = " WHEN NOT MATCHED THEN INSERT (\"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\")"
                              + " VALUES (t2.\"id\",t2.\"name\",t2.\"age\",t2.\"company_id\",t2.\"attrs\",t2.\"created_at\")";
        var changed = User(name: "n2", age: 30);
        changed.Attrs = "{\"changed\":true}";
        await db.Insert(changed).OnConflictUpdate(u => new { u.Name, u.Age }).ExecuteAsync(Ct);
        listen.AssertSql(source + " WHEN MATCHED THEN UPDATE SET t1.\"name\" = t2.\"name\",t1.\"age\" = t2.\"age\"" + insert);
        var saved = await db.Query<ForkUser>().WhereById("u1").SingleAsync(Ct);
        Assert.Equal(("n2", 30, "{}"), (saved.Name, saved.Age, saved.Attrs));

        await db.Insert(User(name: "n3")).OnConflictIgnore().ExecuteAsync(Ct);
        listen.AssertSql(source + insert);
        await db.Insert(User("u2", "n4")).OnConflictIgnore().ExecuteAsync(Ct);
        Assert.Equal([("u1", "n2"), ("u2", "n4")], (await db.Query<ForkUser>().OrderBy(u => u.Id).ToListAsync(Ct)).Select(u => (u.Id, u.Name)));
    }

    // SET 的值也可以取导航上的列（在 USING 的查询里取）；删除的条件一起放进 EXISTS
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#joinedUpdateAndDelete")]
    public async Task JoinedUpdateAndDelete()
    {
        var db = await FreshAsync();
        await db.Insert(new[] { new ForkCompany { Id = "c1", Name = "acme" }, new ForkCompany { Id = "c2", Name = "globex" }, new ForkCompany { Id = "c3" } }).ExecuteAsync(Ct);
        await db.Insert(new[] { User("u1", age: 20), User("u2", age: 30, companyId: "c2"), User("u3", age: 40, companyId: "c3"), User("u4", age: 50, companyId: null), User("u5", age: 10) })
            .ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        Assert.Equal(1, await db.Delete<ForkUser>().Where(u => u.Company!.Name == "acme" && u.Age > 18).ExecuteAsync(Ct));
        listen.AssertSql("DELETE FROM \"t_fork_user\" t WHERE EXISTS(SELECT 1 FROM \"t_fork_company\" t1 WHERE t1.\"id\" = t.\"company_id\" AND t1.\"name\" = ? AND t.\"age\" > ?)");
        listen.AssertParams("acme(String),18(Integer)");

        Assert.Equal(1, await db.Update<ForkUser>().Set(u => u.Attrs, u => u.Company!.Name).Set(u => u.Age, 1).Where(u => u.Company!.Name != null && u.Age > 18).ExecuteAsync(Ct));
        listen.AssertSql("MERGE INTO \"t_fork_user\" t USING (SELECT t.\"id\" AS \"eq_key_0\",t1.\"name\" AS \"eq_value_0\",? AS \"eq_value_1\" FROM \"t_fork_user\" t"
                         + " INNER JOIN \"t_fork_company\" t1 ON t1.\"id\" = t.\"company_id\" WHERE t1.\"name\" IS NOT NULL AND t.\"age\" > ?) eq_src ON (t.\"id\" = eq_src.\"eq_key_0\")"
                         + " WHEN MATCHED THEN UPDATE SET t.\"attrs\" = eq_src.\"eq_value_0\",t.\"age\" = eq_src.\"eq_value_1\"");
        listen.AssertParams("1(Integer),18(Integer)");

        var rows = await db.Query<ForkUser>().OrderBy(u => u.Id).ToListAsync(Ct);
        Assert.Equal([("u2", "globex", 1), ("u3", "{}", 40), ("u4", "{}", 50), ("u5", "{}", 10)], rows.Select(u => (u.Id, u.Attrs, u.Age ?? 0)));
    }

    // 值里的 % _ \ 按字面匹配：转义后写 ESCAPE '\'
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#likeEscapesWildcards")]
    public async Task LikeEscapesWildcards()
    {
        var db = await FreshAsync();
        string[] names = ["50%", "500", "a_b", "axb", "x\\_", "x_", "ab"];
        await db.Insert(names.Select((n, i) => User($"u{i}", n))).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();
        var where = $"SELECT {UserColumns} FROM \"t_fork_user\" WHERE ";

        async Task<string[]> Names(System.Linq.Expressions.Expression<Func<ForkUser, bool>> predicate) =>
            [.. (await db.Query<ForkUser>().Where(predicate).ToListAsync(Ct)).Select(u => u.Name!).Order(StringComparer.Ordinal)];

        Assert.Equal(["50%"], await Names(u => u.Name!.Contains("50%")));
        listen.AssertSql(where + "\"name\" LIKE ? ESCAPE '\\'");
        listen.AssertParams("%50\\%%(String)");
        Assert.Equal(["a_b"], await Names(u => u.Name!.StartsWith("a_b")));
        listen.AssertParams("a\\_b%(String)");
        Assert.Equal(["x\\_"], await Names(u => u.Name!.EndsWith("\\_")));
        listen.AssertParams("%\\\\\\_(String)");
        Assert.Equal(names.Where(n => n != "50%").Order(StringComparer.Ordinal), await Names(u => !u.Name!.Contains("%")));
        listen.AssertSql(where + "\"name\" NOT LIKE ? ESCAPE '\\'");
        // 没有通配符时是普通的 LIKE
        Assert.Equal(["ab"], await Names(u => u.Name!.Contains("ab")));
        listen.AssertSql(where + "\"name\" LIKE ?");
        listen.AssertParams("%ab%(String)");
    }

    // 分页、隐式 LEFT JOIN、投影里的关联子查询一起用
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#pagingImplicitJoinAndSubquery")]
    public async Task PagingImplicitJoinAndSubquery()
    {
        var db = await FreshAsync();
        await db.Insert(new[] { new ForkCompany { Id = "c1", Name = "acme" }, new ForkCompany { Id = "c2", Name = "globex" } }).ExecuteAsync(Ct);
        await db.Insert(Enumerable.Range(1, 6).Select(i => User($"u{i}")).Append(User("u7", companyId: "c2"))).ExecuteAsync(Ct);
        await db.Insert(new[] { Post("p1", "u3"), Post("p2", "u3"), Post("p3", "u5"), Post("p4", "u7") }).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        var rows = await db.Query<ForkUser>().Where(u => u.Company!.Name == "acme").OrderBy(u => u.Id).Skip(2).Take(3)
            .Select(u => new Draft<string?, string?, long>(u.Id, u.Company!.Name, u.Posts!.LongCount())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"id\" AS \"value1\",t1.\"name\" AS \"value2\",(SELECT COUNT(*) FROM \"t_fork_post\" t2 WHERE t2.\"user_id\" = t.\"id\") AS \"value3\""
                         + " FROM \"t_fork_user\" t LEFT JOIN \"t_fork_company\" t1 ON t1.\"id\" = t.\"company_id\" WHERE t1.\"name\" = ? ORDER BY t.\"id\" ASC LIMIT 3 OFFSET 2");
        listen.AssertParams("acme(String)");
        Assert.Equal([("u3", "acme", 2L), ("u4", "acme", 0L), ("u5", "acme", 1L)], rows.Select(r => (r.Value1, r.Value2, r.Value3)));
    }

    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#stringAndNullFunctions")]
    public async Task StringAndNullFunctions()
    {
        var db = await FreshAsync();
        var same = User("u1", " aBc ");
        same.Attrs = " aBc ";
        var blank = User("u2", "");
        blank.Attrs = null;
        await db.Insert(new[] { same, blank }).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        var rows = await db.Query<ForkUser>().Select(u => new Draft<string?, string?, string?, string?, int, string?, string?, bool>(
            u.Name!.Trim(), u.Name!.ToUpper(), u.Name!.ToLower(), u.Name!.Replace("a", "b"), u.Name!.Length, u.Name!.PadLeft(5, '0'),
            u.Name.NullIf(""), u.Name.EqualsNullSafe(u.Attrs))).ToListAsync(Ct);
        listen.AssertSql("SELECT TRIM(t.\"name\") AS \"value1\",UPPER(t.\"name\") AS \"value2\",LOWER(t.\"name\") AS \"value3\",REPLACE(t.\"name\",?,?) AS \"value4\","
                         + "LENGTH(t.\"name\") AS \"value5\",LPAD(t.\"name\", 5, ?) AS \"value6\",NULLIF(t.\"name\", ?) AS \"value7\","
                         + "(CASE WHEN (((t.\"name\" = t.\"attrs\") OR (t.\"name\" IS NULL AND t.\"attrs\" IS NULL))) THEN TRUE WHEN NOT ((((t.\"name\" = t.\"attrs\") OR (t.\"name\" IS NULL AND t.\"attrs\" IS NULL)))) THEN FALSE ELSE NULL END) AS \"value8\""
                         + " FROM \"t_fork_user\" t");
        listen.AssertParams("a(String),b(String),0(String),(String)");
        Assert.Equal(
            [("", "", "", "", 0, "00000", null, false), ("aBc", " ABC ", " abc ", " bBc ", 5, " aBc ", " aBc ", true)],
            rows.OrderBy(r => r.Value5).Select(r => (r.Value1, r.Value2, r.Value3, r.Value4, r.Value5, r.Value6, r.Value7, r.Value8)));
    }

    // 加减、取部分、相差、截断
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#dateFunctions")]
    public async Task DateFunctions()
    {
        var db = await FreshAsync();
        var user = User();
        user.CreatedAt = new DateTime(2026, 11, 6, 9, 30, 15);
        await db.Insert(user).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        var at = new DateTime(2026, 1, 1);
        var row = await db.Query<ForkUser>().Select(u => new Draft<DateTime?, DateTime?, DateTime?, int, int, int, int, int, double, DateTime?>(
            u.CreatedAt!.Value.AddYears(1), u.CreatedAt!.Value.AddMonths(2), u.CreatedAt!.Value.AddHours(3),
            u.CreatedAt!.Value.Year, u.CreatedAt!.Value.Day, u.CreatedAt!.Value.Second, u.CreatedAt!.Value.DayOfYear, (int)u.CreatedAt!.Value.DayOfWeek,
            (u.CreatedAt!.Value - at).TotalHours, u.CreatedAt.DateTrunc(DateTruncUnit.Week))).SingleAsync(Ct);
        listen.AssertSql("SELECT ADD_YEARS(t.\"created_at\",?) AS \"value1\",ADD_MONTHS(t.\"created_at\",?) AS \"value2\",ADD_SECONDS(t.\"created_at\",(?)*3600) AS \"value3\","
                         + "YEAR(t.\"created_at\") AS \"value4\",DAYOFMONTH(t.\"created_at\") AS \"value5\",CAST(FLOOR(SECOND(t.\"created_at\")) AS INTEGER) AS \"value6\","
                         + "DAYOFYEAR(t.\"created_at\") AS \"value7\",MOD(WEEKDAY(t.\"created_at\")+1,7) AS \"value8\","
                         + "CAST(SECONDS_BETWEEN(?,t.\"created_at\")/3600 AS BIGINT) AS \"value9\","
                         + "ADD_DAYS(TO_TIMESTAMP(TO_NVARCHAR(t.\"created_at\",'YYYY-MM-DD'),'YYYY-MM-DD'),-WEEKDAY(t.\"created_at\")) AS \"value10\" FROM \"t_fork_user\" t");
        // 相差的小时数截断成整数（309 天 9.5 小时）；2026-11-06 是周五，所在周从 11-02（周一）起
        Assert.Equal(new Draft<DateTime?, DateTime?, DateTime?, int, int, int, int, int, double, DateTime?>(
            new DateTime(2027, 11, 6, 9, 30, 15), new DateTime(2027, 1, 6, 9, 30, 15), new DateTime(2026, 11, 6, 12, 30, 15),
            2026, 6, 15, 310, (int)DayOfWeek.Friday, 7425, new DateTime(2026, 11, 2)), row);
    }

    // 条件当值（集合导航的 Any、分组里的计数比较）写成三值的 CASE；字符串聚合先转成 NVARCHAR
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#aggregatesAndBooleanProjection")]
    public async Task AggregatesAndBooleanProjection()
    {
        var db = await FreshAsync();
        await db.Insert(new[] { Post("p1", "u1", 1.5m, "a"), Post("p2", "u1", 2.5m, "b"), Post("p3", "u2", 3m, "c") }).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        var rows = await db.Query<ForkPost>().GroupBy(p => p.UserId)
            .Select(g => new Draft<string?, long, decimal?, decimal?, string, bool>(g.Key, g.Count(), g.Sum(p => p.Amount), g.Max(p => p.Amount), g.StringJoin(p => p.Title, ","), g.Count() > 1))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"user_id\" AS \"value1\",COUNT(*) AS \"value2\",SUM(t.\"amount\") AS \"value3\",MAX(t.\"amount\") AS \"value4\",STRING_AGG(TO_NVARCHAR(t.\"title\"), ?) AS \"value5\","
                         + "(CASE WHEN (COUNT(*) > ?) THEN TRUE WHEN NOT ((COUNT(*) > ?)) THEN FALSE ELSE NULL END) AS \"value6\" FROM \"t_fork_post\" t GROUP BY t.\"user_id\"");
        listen.AssertParams(",(String),1(Long),1(Long)");
        // 没有排序时 STRING_AGG 的先后不定，按拆开排好的比
        Assert.Equal([("u1", 2L, 4.0m, 2.5m, "a,b", true), ("u2", 1L, 3m, 3m, "c", false)],
            rows.OrderBy(r => r.Value1).Select(r => (r.Value1, r.Value2, r.Value3 ?? 0, r.Value4 ?? 0, string.Join(",", r.Value5.Split(',').Order(StringComparer.Ordinal)), r.Value6)));
        Assert.Throws<NotSupportedException>(() => db.Query<ForkPost>().GroupBy(p => p.UserId)
            .Select(g => new Draft<string?, string>(g.Key, g.Distinct().StringJoin(p => p.Title, ","))).ToSql());
    }

    private static ForkPost Post(string id, string userId, decimal amount = 1m, string? title = null) => new() { Id = id, UserId = userId, Title = title ?? id, Amount = amount };
}

/// <summary>对应 Java fork：live/HanaKeysAndTypesLiveTest.java（自增键回填、批量回填、LocalDateTime 往返）。</summary>
public sealed class HanaKeysAndTypesLiveTest(HanaFixture hana)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>自增主键的表。</summary>
    [Table("t_hana_identity")]
    public sealed class IdentityRow
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>时间列的表（对应 Java 的 LocalDateTime 列）。</summary>
    [Table("t_hana_time")]
    public sealed class TimeRow
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        public DateTime At { get; set; }

        public DateTime? Maybe { get; set; }
    }

    private async Task<EasyDb> FreshAsync<T>()
    {
        var db = hana.Db;
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(T)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(T)], Ct);
        return db;
    }

    // INSERT 不写自增列，执行后在同一连接上取 CURRENT_IDENTITY_VALUE()；建表写 IDENTITY 列
    [Fact, Upstream("fork/live/HanaKeysAndTypesLiveTest.java#generatedKeyIsFilledBack")]
    public async Task GeneratedKeyIsFilledBack()
    {
        var db = await FreshAsync<IdentityRow>();
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 288 BEGIN END; EXEC 'CREATE COLUMN TABLE \"t_hana_identity\""
                     + " (\"id\" BIGINT GENERATED BY DEFAULT AS IDENTITY NOT NULL, \"name\" NVARCHAR(255) NULL, PRIMARY KEY (\"id\"))'; END",
            db.CodeFirst.CreateTableSql(typeof(IdentityRow)));
        Assert.Equal("SELECT CURRENT_IDENTITY_VALUE() FROM DUMMY", HanaDialect.Instance.LastInsertIdQuery);
        using var listen = SqlCapture.Listen();

        var first = new IdentityRow { Name = "a" };
        await db.Insert(first).FillGeneratedKeys().ExecuteAsync(Ct);
        listen.AssertSql(0, "INSERT INTO \"t_hana_identity\" (\"name\") VALUES (?)");
        listen.AssertParams(0, "a(String)");
        var second = new IdentityRow { Name = "b" };
        await db.Insert(second).FillGeneratedKeys().ExecuteAsync(Ct);

        Assert.True(first.Id > 0 && second.Id > first.Id, $"{first.Id}, {second.Id}");
        Assert.Equal("b", (await db.Query<IdentityRow>().WhereById(second.Id).SingleAsync(Ct)).Name);
    }

    // 批量插入每行一条 INSERT（HANA 的 VALUES 只能一行），每条之后各取一次键
    [Fact, Upstream("fork/live/HanaKeysAndTypesLiveTest.java#generatedKeysForBatch")]
    public async Task GeneratedKeysForBatch()
    {
        var db = await FreshAsync<IdentityRow>();
        var rows = new[] { new IdentityRow { Name = "a" }, new IdentityRow { Name = "b" }, new IdentityRow { Name = "c" } };
        Assert.Equal("INSERT INTO \"t_hana_identity\" (\"name\") VALUES (?)", db.Insert(rows).FillGeneratedKeys().Batch().ToSql());

        await db.Insert(rows).FillGeneratedKeys().Batch().ExecuteAsync(Ct);
        Assert.Equal(3, rows.Select(r => r.Id).Distinct().Count(id => id > 0));
        var saved = await db.Query<IdentityRow>().OrderBy(r => r.Id).ToListAsync(Ct);
        Assert.Equal(rows.Select(r => (r.Id, r.Name)), saved.Select(r => (r.Id, r.Name)));
    }

    // 读回的值与写入的逐位相同（经 Sap.Data.Hana 往返精确到微秒，第 7 位小数写入时被舍掉）；按值能查到
    [Fact, Upstream("fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeRoundTrip")]
    public async Task LocalDateTimeRoundTrip()
    {
        var db = await FreshAsync<TimeRow>();
        var at = new DateTime(2026, 11, 6, 9, 30, 15, 123).AddTicks(4560);
        await db.Insert(new[] { new TimeRow { Id = "t1", At = at, Maybe = at }, new TimeRow { Id = "t2", At = DateTime.MinValue.AddYears(2000) } }).ExecuteAsync(Ct);

        var saved = await db.Query<TimeRow>().WhereById("t1").SingleAsync(Ct);
        Assert.Equal((at, at), (saved.At, saved.Maybe!.Value));
        Assert.Null((await db.Query<TimeRow>().WhereById("t2").SingleAsync(Ct)).Maybe);
        Assert.Equal(["t1"], await db.Query<TimeRow>().Where(r => r.At == at).Select(r => r.Id).ToListAsync(Ct));
    }

    // Java 验证 JVM 时区夏令时缺口里的 LocalDateTime 不被 JDBC 挪动；.NET 的 DateTime 不经过本机时区，这里确认驱动也原样写入、读回
    // （美国、欧洲 2026 年春季拨表的缺口各取一个）
    [Fact, Upstream("fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeSurvivesDstGapOfJvmZone")]
    public async Task LocalDateTimeSurvivesDstGapOfJvmZone()
    {
        var db = await FreshAsync<TimeRow>();
        DateTime[] gaps = [new(2026, 3, 8, 2, 30, 0), new(2026, 3, 29, 2, 30, 0)];
        await db.Insert(gaps.Select((g, i) => new TimeRow { Id = $"t{i}", At = g })).ExecuteAsync(Ct);

        Assert.Equal(gaps, (await db.Query<TimeRow>().OrderBy(r => r.Id).ToListAsync(Ct)).Select(r => r.At));
        Assert.Equal(["t1"], await db.Query<TimeRow>().Where(r => r.At.Hour == 2 && r.At.Month == 3 && r.At.Day == 29).Select(r => r.Id).ToListAsync(Ct));
    }
}

/// <summary>对应 Java fork：live/HanaTextLiveTest.java（建 FULLTEXT INDEX 后 CONTAINS / FUZZY / SCORE）。</summary>
public sealed class HanaTextLiveTest(HanaFixture hana)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Upstream("fork/live/HanaTextLiveTest.java#containsFuzzyAndScore")]
    public async Task ContainsFuzzyAndScore()
    {
        var db = hana.Db;
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 259 BEGIN END; EXEC 'DROP TABLE \"t_fork_post\"'; END",
            HanaDialect.Instance.DropTableIfExistsSql(db.Metadata.Get<ForkPost>()));
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(ForkPost)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(ForkPost)], Ct);
        // 同步维护的全文索引：写入后马上能检索
        await hana.ExecuteAsync("CREATE FULLTEXT INDEX \"idx_fork_post_title\" ON \"t_fork_post\" (\"title\") SYNC", Ct);
        await db.Insert(new[]
        {
            new ForkPost { Id = "p1", UserId = "u1", Title = "easy query orm", Amount = 1m },
            new ForkPost { Id = "p2", UserId = "u1", Title = "hello world", Amount = 1m },
            new ForkPost { Id = "p3", UserId = "u2", Title = "easy query easy query", Amount = 1m },
        }).ExecuteAsync(Ct);
        using var listen = SqlCapture.Listen();

        var ids = await db.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Fuzzy("easy qurey", 0.7))
            .OrderByDescending(p => Sql.Hana.Text(p.Title).Score()).Select(p => p.Id).ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"id\" FROM \"t_fork_post\" t WHERE CONTAINS(t.\"title\",?,FUZZY(0.7)) ORDER BY SCORE() DESC");
        listen.AssertParams("easy qurey(String)");
        Assert.Equal(["p1", "p3"], ids.Order(StringComparer.Ordinal));
        Assert.Equal(["p1"], await db.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Contains("orm")).Select(p => p.Id).ToListAsync(Ct));
    }
}

/// <summary>对应 Java fork：live/JsonValueConverterLiveTest.java 的 HANA 部分（JSON 存进 NCLOB 列）。</summary>
public sealed class JsonValueConverterHanaLiveTest(HanaFixture hana)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>ForkProfile 的 HANA 版：JSON 列声明成 NCLOB。</summary>
    [Table("t_fork_profile")]
    public sealed class NclobProfile
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        [Column(DbType = "NCLOB", Converter = typeof(JsonValueConverter<List<ForkProfile.Address>>))]
        public List<ForkProfile.Address>? Addresses { get; set; }
    }

    [Fact, Upstream("fork/live/JsonValueConverterLiveTest.java#hanaNclobColumn")]
    public async Task HanaNclobColumn()
    {
        var db = hana.Db;
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 288 BEGIN END; EXEC 'CREATE COLUMN TABLE \"t_fork_profile\""
                     + " (\"id\" NVARCHAR(255) NOT NULL, \"addresses\" NCLOB NULL, PRIMARY KEY (\"id\"))'; END",
            db.CodeFirst.CreateTableSql(typeof(NclobProfile)));
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(NclobProfile)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(NclobProfile)], Ct);
        using var listen = SqlCapture.Listen();

        // 超过 NVARCHAR 上限（5000）的 JSON 也能存取
        var many = Enumerable.Range(0, 400).Select(i => new ForkProfile.Address { City = $"city-{i}", Zip = 310000 + i }).ToList();
        await db.Insert(new NclobProfile { Id = "p1", Addresses = [new() { City = "Hangzhou", Zip = 310000 }] }).ExecuteAsync(Ct);
        listen.AssertSql("INSERT INTO \"t_fork_profile\" (\"id\",\"addresses\") VALUES (?,?)");
        listen.AssertParams("p1(String),[{\"city\":\"Hangzhou\",\"zip\":310000}](String)");
        await db.Insert(new NclobProfile { Id = "p2", Addresses = many }).ExecuteAsync(Ct);
        await db.Insert(new NclobProfile { Id = "p3" }).ExecuteAsync(Ct);

        var saved = (await db.Query<NclobProfile>().OrderBy(p => p.Id).ToListAsync(Ct)).ToDictionary(p => p.Id!);
        Assert.Equal([("Hangzhou", 310000)], saved["p1"].Addresses!.Select(a => (a.City, a.Zip ?? 0)));
        Assert.Equal(many.Select(a => (a.City, a.Zip)), saved["p2"].Addresses!.Select(a => (a.City, a.Zip)));
        Assert.Null(saved["p3"].Addresses);
    }
}
