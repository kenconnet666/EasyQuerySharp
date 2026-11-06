using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：live/HanaDialectLiveTest.java（HANA 实库上的增删改查、MERGE、导航更新删除、LIKE 转义、分页、函数、聚合）。
/// HANA 要 12 GB 内存，本机按需启动（infra/dev 的 hana profile），C# 还没有 HANA 实库夹具；这里只核对 SQL，实库验证留到 HANA 冒烟。
/// </summary>
public sealed class HanaDialectLiveTest
{
    private const string LiveDeferred = "Java 在 HANA 实库上执行并核对结果；C# 只核对同一组操作的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2）";
    private const string UserColumns = "\"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\"";

    private static EasyDb Hana => SqlOnly.Db("hana");

    private static ForkUser User() => new() { Id = "u1", Name = "n1", Age = 20, CompanyId = "c1", Attrs = "{}", CreatedAt = new DateTime(2026, 11, 6, 9, 30, 0) };

    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#plainCrudAndForUpdate", Deviation = LiveDeferred)]
    public void PlainCrudAndForUpdate()
    {
        const string values = "u1(String),n1(String),20(Integer),c1(String),{}(String),2026-11-06T09:30(LocalDateTime)";
        SqlOnly.AssertStatement(Hana.Insert(User()).ToStatement(), $"INSERT INTO \"t_fork_user\" ({UserColumns}) VALUES (?,?,?,?,?,?)", values);
        SqlOnly.AssertStatement(Hana.Update(User()).ToStatement(),
            "UPDATE \"t_fork_user\" SET \"name\" = ?,\"age\" = ?,\"company_id\" = ?,\"attrs\" = ?,\"created_at\" = ? WHERE \"id\" = ?",
            "n1(String),20(Integer),c1(String),{}(String),2026-11-06T09:30(LocalDateTime),u1(String)");
        SqlOnly.AssertStatement(Hana.Delete(User()).ToStatement()!, "DELETE FROM \"t_fork_user\" WHERE \"id\" = ?", "u1(String)");

        using var tx = Hana.BeginTransaction();
        var byId = $"SELECT {UserColumns} FROM \"t_fork_user\" WHERE \"id\" = ?";
        Assert.Equal(byId + " FOR UPDATE", Hana.Query<ForkUser>().Where(u => u.Id == "u1").ForUpdate().ToSql());
        Assert.Equal(byId + " FOR UPDATE NOWAIT", Hana.Query<ForkUser>().Where(u => u.Id == "u1").ForUpdateNoWait().ToSql());
        Assert.Equal(byId + " FOR UPDATE IGNORE LOCKED", Hana.Query<ForkUser>().Where(u => u.Id == "u1").ForUpdateSkipLocked().ToSql());
    }

    // 冲突时更新指定的列；忽略冲突时没有 WHEN MATCHED
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#mergeUpsert", Deviation = LiveDeferred)]
    public void MergeUpsert()
    {
        const string source = "MERGE INTO \"t_fork_user\" t1 USING (SELECT ? AS \"id\",? AS \"name\",? AS \"age\",? AS \"company_id\",? AS \"attrs\",? AS \"created_at\" FROM DUMMY) t2 ON (t1.\"id\" = t2.\"id\")";
        const string insert = " WHEN NOT MATCHED THEN INSERT (\"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\")"
                              + " VALUES (t2.\"id\",t2.\"name\",t2.\"age\",t2.\"company_id\",t2.\"attrs\",t2.\"created_at\")";
        Assert.Equal(source + " WHEN MATCHED THEN UPDATE SET t1.\"name\" = t2.\"name\",t1.\"age\" = t2.\"age\"" + insert,
            Hana.Insert(User()).OnConflictUpdate(u => new { u.Name, u.Age }).ToSql());
        Assert.Equal(source + insert, Hana.Insert(User()).OnConflictIgnore().ToSql());
    }

    // SET 的值也可以取导航上的列（在 USING 的查询里取）；删除的条件一起放进 EXISTS
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#joinedUpdateAndDelete", Deviation = LiveDeferred)]
    public void JoinedUpdateAndDelete()
    {
        SqlOnly.AssertStatement(Hana.Update<ForkUser>().Set(u => u.Attrs, u => u.Company!.Name).Set(u => u.Age, 1).Where(u => u.Company!.Name != null && u.Age > 18).ToStatement(),
            "MERGE INTO \"t_fork_user\" t USING (SELECT t.\"id\" AS \"eq_key_0\",t1.\"name\" AS \"eq_value_0\",? AS \"eq_value_1\" FROM \"t_fork_user\" t"
            + " INNER JOIN \"t_fork_company\" t1 ON t1.\"id\" = t.\"company_id\" WHERE t1.\"name\" IS NOT NULL AND t.\"age\" > ?) eq_src ON (t.\"id\" = eq_src.\"eq_key_0\")"
            + " WHEN MATCHED THEN UPDATE SET t.\"attrs\" = eq_src.\"eq_value_0\",t.\"age\" = eq_src.\"eq_value_1\"",
            "1(Integer),18(Integer)");
        SqlOnly.AssertStatement(Hana.Delete<ForkUser>().Where(u => u.Company!.Name == "acme" && u.Age > 18).ToStatement()!,
            "DELETE FROM \"t_fork_user\" t WHERE EXISTS(SELECT 1 FROM \"t_fork_company\" t1 WHERE t1.\"id\" = t.\"company_id\" AND t1.\"name\" = ? AND t.\"age\" > ?)",
            "acme(String),18(Integer)");
    }

    // 值里的 % _ \ 按字面匹配：转义后写 ESCAPE '\'
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#likeEscapesWildcards", Deviation = LiveDeferred)]
    public void LikeEscapesWildcards()
    {
        var where = $"SELECT {UserColumns} FROM \"t_fork_user\" WHERE ";
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Name!.Contains("50%")).ToStatement(), where + "\"name\" LIKE ? ESCAPE '\\'", "%50\\%%(String)");
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Name!.StartsWith("a_b")).ToStatement(), where + "\"name\" LIKE ? ESCAPE '\\'", "a\\_b%(String)");
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Name!.EndsWith("\\_")).ToStatement(), where + "\"name\" LIKE ? ESCAPE '\\'", "%\\\\\\_(String)");
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => !u.Name!.Contains("%")).ToStatement(), where + "\"name\" NOT LIKE ? ESCAPE '\\'", "%\\%%(String)");
        // 没有通配符时是普通的 LIKE
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Name!.Contains("ab")).ToStatement(), where + "\"name\" LIKE ?", "%ab%(String)");
    }

    // 分页、隐式 LEFT JOIN、投影里的关联子查询一起用
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#pagingImplicitJoinAndSubquery", Deviation = LiveDeferred)]
    public void PagingImplicitJoinAndSubquery() =>
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Company!.Name == "acme").OrderBy(u => u.Id).Skip(2).Take(3)
                .Select(u => new Draft<string?, string?, long>(u.Id, u.Company!.Name, u.Posts!.LongCount())).ToStatement(),
            "SELECT t.\"id\" AS \"value1\",t1.\"name\" AS \"value2\",(SELECT COUNT(*) FROM \"t_fork_post\" t2 WHERE t2.\"user_id\" = t.\"id\") AS \"value3\""
            + " FROM \"t_fork_user\" t LEFT JOIN \"t_fork_company\" t1 ON t1.\"id\" = t.\"company_id\" WHERE t1.\"name\" = ? ORDER BY t.\"id\" ASC LIMIT 3 OFFSET 2",
            "acme(String)");

    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#stringAndNullFunctions", Deviation = LiveDeferred)]
    public void StringAndNullFunctions() =>
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Select(u => new Draft<string?, string?, string?, string?, int, string?, string?, bool>(
                u.Name!.Trim(), u.Name!.ToUpper(), u.Name!.ToLower(), u.Name!.Replace("a", "b"), u.Name!.Length, u.Name!.PadLeft(5, '0'),
                u.Name.NullIf(""), u.Name.EqualsNullSafe(u.Attrs))).ToStatement(),
            "SELECT TRIM(t.\"name\") AS \"value1\",UPPER(t.\"name\") AS \"value2\",LOWER(t.\"name\") AS \"value3\",REPLACE(t.\"name\",?,?) AS \"value4\","
            + "LENGTH(t.\"name\") AS \"value5\",LPAD(t.\"name\", 5, ?) AS \"value6\",NULLIF(t.\"name\", ?) AS \"value7\","
            + "(CASE WHEN (((t.\"name\" = t.\"attrs\") OR (t.\"name\" IS NULL AND t.\"attrs\" IS NULL))) THEN TRUE WHEN NOT ((((t.\"name\" = t.\"attrs\") OR (t.\"name\" IS NULL AND t.\"attrs\" IS NULL)))) THEN FALSE ELSE NULL END) AS \"value8\""
            + " FROM \"t_fork_user\" t",
            "a(String),b(String),0(String),(String)");

    // 加减、取部分、相差、截断
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#dateFunctions", Deviation = LiveDeferred)]
    public void DateFunctions()
    {
        var at = new DateTime(2026, 1, 1);
        var sql = Hana.Query<ForkUser>().Select(u => new Draft<DateTime?, DateTime?, DateTime?, int, int, int, int, int, double, DateTime?>(
            u.CreatedAt!.Value.AddYears(1), u.CreatedAt!.Value.AddMonths(2), u.CreatedAt!.Value.AddHours(3),
            u.CreatedAt!.Value.Year, u.CreatedAt!.Value.Day, u.CreatedAt!.Value.Second, u.CreatedAt!.Value.DayOfYear, (int)u.CreatedAt!.Value.DayOfWeek,
            (u.CreatedAt!.Value - at).TotalHours, u.CreatedAt.DateTrunc(DateTruncUnit.Week))).ToSql();
        Assert.Equal("SELECT ADD_YEARS(t.\"created_at\",?) AS \"value1\",ADD_MONTHS(t.\"created_at\",?) AS \"value2\",ADD_SECONDS(t.\"created_at\",(?)*3600) AS \"value3\","
                     + "YEAR(t.\"created_at\") AS \"value4\",DAYOFMONTH(t.\"created_at\") AS \"value5\",CAST(FLOOR(SECOND(t.\"created_at\")) AS INTEGER) AS \"value6\","
                     + "DAYOFYEAR(t.\"created_at\") AS \"value7\",MOD(WEEKDAY(t.\"created_at\")+1,7) AS \"value8\","
                     + "CAST(SECONDS_BETWEEN(?,t.\"created_at\")/3600 AS BIGINT) AS \"value9\","
                     + "ADD_DAYS(TO_TIMESTAMP(TO_NVARCHAR(t.\"created_at\",'YYYY-MM-DD'),'YYYY-MM-DD'),-WEEKDAY(t.\"created_at\")) AS \"value10\" FROM \"t_fork_user\" t", sql);
    }

    // 条件当值（集合导航的 Any、分组里的计数比较）写成三值的 CASE；字符串聚合先转成 NVARCHAR
    [Fact, Upstream("fork/live/HanaDialectLiveTest.java#aggregatesAndBooleanProjection", Deviation = LiveDeferred)]
    public void AggregatesAndBooleanProjection()
    {
        SqlOnly.AssertStatement(Hana.Query<ForkPost>().GroupBy(p => p.UserId)
                .Select(g => new Draft<string?, long, decimal?, decimal?, string, bool>(g.Key, g.Count(), g.Sum(p => p.Amount), g.Max(p => p.Amount), g.StringJoin(p => p.Title, ","), g.Count() > 1)).ToStatement(),
            "SELECT t.\"user_id\" AS \"value1\",COUNT(*) AS \"value2\",SUM(t.\"amount\") AS \"value3\",MAX(t.\"amount\") AS \"value4\",STRING_AGG(TO_NVARCHAR(t.\"title\"), ?) AS \"value5\","
            + "(CASE WHEN (COUNT(*) > ?) THEN TRUE WHEN NOT ((COUNT(*) > ?)) THEN FALSE ELSE NULL END) AS \"value6\" FROM \"t_fork_post\" t GROUP BY t.\"user_id\"",
            ",(String),1(Long),1(Long)");
        Assert.Throws<NotSupportedException>(() => Hana.Query<ForkPost>().GroupBy(p => p.UserId)
            .Select(g => new Draft<string?, string>(g.Key, g.Distinct().StringJoin(p => p.Title, ","))).ToSql());
    }
}

/// <summary>对应 Java fork：live/HanaKeysAndTypesLiveTest.java（自增键回填、批量回填、LocalDateTime 往返）。</summary>
public sealed class HanaKeysAndTypesLiveTest
{
    private const string LiveDeferred = "Java 在 HANA 实库上插入并读回生成的键；C# 只核对插入语句、取键的查询与建表语句，实库验证留到 HANA 冒烟（ROADMAP 3.2）";

    private static EasyDb Hana => SqlOnly.Db("hana");

    /// <summary>自增主键的表。</summary>
    [Table("t_hana_identity")]
    public sealed class IdentityRow
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public string? Name { get; set; }
    }

    // INSERT 不写自增列，执行后在同一连接上取 CURRENT_IDENTITY_VALUE()；建表写 IDENTITY 列
    [Fact, Upstream("fork/live/HanaKeysAndTypesLiveTest.java#generatedKeyIsFilledBack", Deviation = LiveDeferred)]
    public void GeneratedKeyIsFilledBack()
    {
        SqlOnly.AssertStatement(Hana.Insert(new IdentityRow { Name = "a" }).FillGeneratedKeys().ToStatement(), "INSERT INTO \"t_hana_identity\" (\"name\") VALUES (?)", "a(String)");
        Assert.Equal("SELECT CURRENT_IDENTITY_VALUE() FROM DUMMY", HanaDialect.Instance.LastInsertIdQuery);
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 288 BEGIN END; EXEC 'CREATE COLUMN TABLE \"t_hana_identity\""
                     + " (\"id\" BIGINT GENERATED BY DEFAULT AS IDENTITY NOT NULL, \"name\" NVARCHAR(255) NULL, PRIMARY KEY (\"id\"))'; END",
            Hana.CodeFirst.CreateTableSql(typeof(IdentityRow)));
    }

    // 批量插入每行一条 INSERT（HANA 的 VALUES 只能一行），每条之后各取一次键
    [Fact, Upstream("fork/live/HanaKeysAndTypesLiveTest.java#generatedKeysForBatch", Deviation = LiveDeferred)]
    public void GeneratedKeysForBatch()
    {
        var rows = new[] { new IdentityRow { Name = "a" }, new IdentityRow { Name = "b" } };
        Assert.Equal("INSERT INTO \"t_hana_identity\" (\"name\") VALUES (?)", Hana.Insert(rows).FillGeneratedKeys().Batch().ToSql());
        Assert.NotNull(HanaDialect.Instance.LastInsertIdQuery);
    }

    [Fact(Skip = "需要 HANA 实库"), Upstream("fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeRoundTrip", Pending = true)]
    public void LocalDateTimeRoundTrip()
    {
    }

    // Java 验证 JVM 时区的夏令时缺口里的 LocalDateTime 不被 JDBC 挪动；.NET 的 DateTime 不经过本机时区，要在实库上确认驱动也不转换
    [Fact(Skip = "需要 HANA 实库"), Upstream("fork/live/HanaKeysAndTypesLiveTest.java#localDateTimeSurvivesDstGapOfJvmZone", Pending = true)]
    public void LocalDateTimeSurvivesDstGapOfJvmZone()
    {
    }
}

/// <summary>对应 Java fork：live/HanaTextLiveTest.java（建 FULLTEXT INDEX 后 CONTAINS / FUZZY / SCORE）。</summary>
public sealed class HanaTextLiveTest
{
    private static EasyDb Hana => SqlOnly.Db("hana");

    [Fact, Upstream("fork/live/HanaTextLiveTest.java#containsFuzzyAndScore",
        Deviation = "Java 在 HANA 实库上建全文索引后检索；C# 只核对检索与删表的 SQL，实库验证留到 HANA 冒烟（ROADMAP 3.2）")]
    public void ContainsFuzzyAndScore()
    {
        SqlOnly.AssertStatement(Hana.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Fuzzy("easy qurey", 0.7))
                .OrderByDescending(p => Sql.Hana.Text(p.Title).Score()).Select(p => p.Id).ToStatement(),
            "SELECT t.\"id\" FROM \"t_fork_post\" t WHERE CONTAINS(t.\"title\",?,FUZZY(0.7)) ORDER BY SCORE() DESC", "easy qurey(String)");
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 259 BEGIN END; EXEC 'DROP TABLE \"t_fork_post\"'; END",
            HanaDialect.Instance.DropTableIfExistsSql(Hana.Metadata.Get<ForkPost>()));
    }
}

/// <summary>对应 Java fork：live/JsonValueConverterLiveTest.java 的 HANA 部分（JSON 存进 NCLOB 列）。</summary>
public sealed class JsonValueConverterHanaLiveTest
{
    private static EasyDb Hana => SqlOnly.Db("hana");

    /// <summary>ForkProfile 的 HANA 版：JSON 列声明成 NCLOB。</summary>
    [Table("t_fork_profile")]
    public sealed class NclobProfile
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        [Column(DbType = "NCLOB", Converter = typeof(JsonValueConverter<List<ForkProfile.Address>>))]
        public List<ForkProfile.Address>? Addresses { get; set; }
    }

    [Fact, Upstream("fork/live/JsonValueConverterLiveTest.java#hanaNclobColumn",
        Deviation = "Java 在 HANA 实库上往返 NCLOB 列；C# 只核对建表与写入的 SQL（JSON 序列化为字符串参数），实库验证留到 HANA 冒烟（ROADMAP 3.2）")]
    public void HanaNclobColumn()
    {
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 288 BEGIN END; EXEC 'CREATE COLUMN TABLE \"t_fork_profile\""
                     + " (\"id\" NVARCHAR(255) NOT NULL, \"addresses\" NCLOB NULL, PRIMARY KEY (\"id\"))'; END",
            Hana.CodeFirst.CreateTableSql(typeof(NclobProfile)));
        SqlOnly.AssertStatement(Hana.Insert(new NclobProfile { Id = "p1", Addresses = [new() { City = "Hangzhou", Zip = 310000 }] }).ToStatement(),
            "INSERT INTO \"t_fork_profile\" (\"id\",\"addresses\") VALUES (?,?)", "p1(String),[{\"city\":\"Hangzhou\",\"zip\":310000}](String)");
    }
}
