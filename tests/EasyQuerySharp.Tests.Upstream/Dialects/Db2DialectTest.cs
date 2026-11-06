using Company = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.Company;
using SysUser = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>
/// Db2 方言的写法（Java 只有 ForUpdateDialectToSQLSmokeTest 的 db2 一条，这里是 C# 独有的）：
/// OFFSET / FETCH 分页、写入与 MERGE、连接、分组、函数表、行锁与建表。只渲染，不连库。
/// </summary>
public sealed class Db2DialectTest
{
    private const string TopicColumns = "SELECT \"id\",\"stars\",\"title\",\"create_time\" FROM \"t_topic\"";
    private const string Day2025 = "2025-01-01T00:00(LocalDateTime)";
    private static readonly DateTime Day = new(2025, 1, 1);

    private static EasyDb Db2 => SqlOnly.Db("db2");

    // 跳过行数而没有排序时按分组键、再不然按第一个主键排序（与 Java 的 DB2QuerySQLExpression 一致）
    [Fact]
    public void PagingUsesOffsetFetch()
    {
        SqlOnly.AssertStatement(Db2.Query<Topic>().Where(t => t.Title == "a").OrderBy(t => t.Id).Skip(10).Take(5).ToStatement(),
            TopicColumns + " WHERE \"title\" = ? ORDER BY \"id\" ASC OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY", "a(String)");
        Assert.Equal(TopicColumns + " FETCH FIRST 3 ROWS ONLY", Db2.Query<Topic>().Take(3).ToSql());
        Assert.Equal(TopicColumns + " ORDER BY \"id\" OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY", Db2.Query<Topic>().Skip(10).Take(5).ToSql());
        Assert.Equal("SELECT t.\"title\" AS \"value1\",COUNT(*) AS \"value2\" FROM \"t_topic\" t GROUP BY t.\"title\" ORDER BY t.\"title\" OFFSET 2 ROWS FETCH NEXT 2 ROWS ONLY",
            Db2.Query<Topic>().GroupBy(t => t.Title).Select(g => new Draft<string?, long>(g.Key, g.Count())).Skip(2).Take(2).ToSql());
    }

    // SET 的列不带别名；关联子查询里目标表带别名
    [Fact]
    public void Writes()
    {
        SqlOnly.AssertStatement(Db2.Insert(new Topic { Id = "1", Stars = 2, Title = "t", CreateTime = Day }).ToStatement(),
            "INSERT INTO \"t_topic\" (\"id\",\"stars\",\"title\",\"create_time\") VALUES (?,?,?,?)", "1(String),2(Integer),t(String)," + Day2025);
        SqlOnly.AssertStatement(Db2.Update(new Topic { Id = "1", Title = "x" }).ToStatement(),
            "UPDATE \"t_topic\" SET \"stars\" = ?,\"title\" = ?,\"create_time\" = ? WHERE \"id\" = ?", "null(null),x(String),null(null),1(String)");
        SqlOnly.AssertStatement(Db2.Delete<Company>().Where(c => c.Users!.Where(u => u.Age > 18).Any()).ToStatement()!,
            "DELETE FROM \"t_company\" t WHERE EXISTS(SELECT 1 FROM \"t_user\" t1 WHERE t1.\"company_id\" = t.\"id\" AND t1.\"age\" > ? FETCH FIRST 1 ROWS ONLY)", "18(Integer)");
        Assert.Equal("SELECT IDENTITY_VAL_LOCAL() FROM SYSIBM.SYSDUMMY1", Db2.Options.Dialect.LastInsertIdQuery);
    }

    // 冲突处理写成 MERGE，来源是 VALUES（Db2 的 SELECT 不能省 FROM，选择列表里也不能有无类型的参数）
    [Fact]
    public void UpsertUsesMergeWithValues()
    {
        const string merge = "MERGE INTO \"t_topic\" t1 USING (VALUES (?,?,?,?)) t2 (\"id\",\"stars\",\"title\",\"create_time\") ON (t1.\"id\" = t2.\"id\")";
        const string insert = " WHEN NOT MATCHED THEN INSERT (\"id\",\"stars\",\"title\",\"create_time\") VALUES (t2.\"id\",t2.\"stars\",t2.\"title\",t2.\"create_time\")";
        var topic = new Topic { Id = "1", Stars = 2, Title = "t", CreateTime = Day };
        SqlOnly.AssertStatement(Db2.Insert(topic).OnConflictUpdate(t => t.Title).ToStatement(),
            merge + " WHEN MATCHED THEN UPDATE SET \"title\" = t2.\"title\"" + insert, "1(String),2(Integer),t(String)," + Day2025);
        Assert.Equal(merge + insert, Db2.Insert(topic).OnConflictIgnore().ToSql());
    }

    [Fact]
    public void JoinAndGroupBy()
    {
        SqlOnly.AssertStatement(Db2.Query<BlogEntity>().LeftJoin<Topic>((b, t) => b.Id == t.Id).Where((_, t) => t.Title == "x").Select((b, t) => new Draft<string?, string?>(b.Id, t.Title)).ToStatement(),
            "SELECT t.\"id\" AS \"value1\",t1.\"title\" AS \"value2\" FROM \"t_blog\" t LEFT JOIN \"t_topic\" t1 ON t.\"id\" = t1.\"id\" WHERE t.\"deleted\" = ? AND t1.\"title\" = ?", "false(Boolean),x(String)");
        // LISTAGG 的分隔符必须是常量，写成字面量
        Assert.Equal("SELECT t.\"stars\" AS \"value1\",LISTAGG(CAST(t.\"title\" AS VARCHAR(4000)), ',') AS \"value2\" FROM \"t_topic\" t GROUP BY t.\"stars\"",
            Db2.Query<Topic>().GroupBy(t => t.Stars).Select(g => new Draft<int?, string>(g.Key, g.StringJoin(x => x.Title, ","))).ToSql());
    }

    // 字符串按字符（CODEUNITS32）；拼接用 ||（Db2 的 CONCAT 只有两个参数）；日期用带单位的时长、DAYS / MIDNIGHT_SECONDS 相减、TRUNC_TIMESTAMP
    [Fact]
    public void FunctionTable()
    {
        Assert.Equal("SELECT CHARACTER_LENGTH(t.\"title\",CODEUNITS32) AS \"value1\",SUBSTRING(t.\"title\",2,2,CODEUNITS32) AS \"value2\",(LOCATE(?,t.\"title\",1,CODEUNITS32) - 1) AS \"value3\",t.\"id\" || t.\"title\" AS \"value4\" FROM \"t_topic\" t",
            Db2.Query<Topic>().Select(t => new Draft<int, string, int, string>(t.Title!.Length, t.Title!.Substring(1, 2), t.Title!.IndexOf("x"), t.Id + t.Title)).ToSql());
        Assert.Equal("SELECT (t.\"create_time\" + (?) DAYS) AS \"value1\",(t.\"create_time\" + ((?) * 1000) MICROSECONDS) AS \"value2\",YEAR(t.\"create_time\") AS \"value3\",(DAYOFWEEK(t.\"create_time\")-1) AS \"value4\",DAYOFWEEK_ISO(t.\"create_time\") AS \"value5\" FROM \"t_topic\" t",
            Db2.Query<Topic>().Select(t => new Draft<DateTime, DateTime, int, int, int>(t.CreateTime!.Value.AddDays(2), t.CreateTime!.Value.AddMilliseconds(5), t.CreateTime!.Value.Year,
                (int)t.CreateTime!.Value.DayOfWeek, t.CreateTime!.Value.IsoDayOfWeek())).ToSql());
        Assert.Equal("SELECT ((CAST(DAYS(?) - DAYS(t.\"create_time\") AS BIGINT) * 86400 + MIDNIGHT_SECONDS(?) - MIDNIGHT_SECONDS(t.\"create_time\")) / 3600) AS \"value1\" FROM \"t_topic\" t",
            Db2.Query<Topic>().Select(t => new Draft<long>((long)(Day - t.CreateTime!.Value).TotalHours)).ToSql());
        Assert.Equal("SELECT VARCHAR_FORMAT(t.\"create_time\",'YYYY-MM-DD HH24:MI:SS') AS \"value1\",TRUNC_TIMESTAMP(t.\"create_time\",'MM') AS \"value2\",CAST(t.\"create_time\" AS TIMESTAMP(0)) AS \"value3\",(CURRENT_TIMESTAMP - CURRENT_TIMEZONE) AS \"value4\" FROM \"t_topic\" t",
            Db2.Query<Topic>().Select(t => new Draft<string, DateTime?, DateTime?, DateTime>(t.CreateTime!.Value.ToString("yyyy-MM-dd HH:mm:ss"), t.CreateTime.DateTrunc(DateTruncUnit.Month),
                t.CreateTime.DateTrunc(DateTruncUnit.Second), Sql.UtcNow())).ToSql());
        // Db2 的 ATAN2 是 (x, y)
        Assert.Equal("SELECT (LN(t.\"stars\") / LN(?)) AS \"value1\",ATAN2(?,t.\"stars\") AS \"value2\",CAST(t.\"title\" AS DECIMAL(31,18)) AS \"value3\" FROM \"t_topic\" t",
            Db2.Query<Topic>().Select(t => new Draft<double, double, decimal>(Math.Log((double)t.Stars!, 2), Math.Atan2((double)t.Stars!, 1), Sql.Cast<decimal>(t.Title))).ToSql());
        Assert.Equal(TopicColumns + " WHERE (\"title\" IS NOT DISTINCT FROM \"id\")", Db2.Query<Topic>().Where(t => t.Title.EqualsNullSafe(t.Id)).ToSql());
    }

    // 驱动执行的文本里参数按值的类型写成 CAST（CASE 的结果、函数参数、带单位的时长里不能有无类型的参数标记）；Text 仍用 ? 占位
    [Fact]
    public void CommandTextCastsParameters()
    {
        var statement = Db2.Query<Topic>().Where(t => t.Stars > 1 && t.Title == "a" && t.CreateTime < Day).Select(t => t.Stars > 3 ? 1.5m : 2m).ToStatement();
        Assert.Equal("SELECT (CASE WHEN t.\"stars\" > CAST(@p0 AS INTEGER) THEN CAST(@p1 AS DECIMAL(31,1)) ELSE CAST(@p2 AS DECIMAL(31,0)) END) FROM \"t_topic\" t "
            + "WHERE t.\"stars\" > CAST(@p3 AS INTEGER) AND t.\"title\" = CAST(@p4 AS VARCHAR(4000)) AND t.\"create_time\" < CAST(@p5 AS TIMESTAMP)", statement.CommandText);
    }

    // 带导航的更新写成按主键对上的 MERGE、删除写成 EXISTS（Db2 的 UPDATE / DELETE 不能连接）
    [Fact]
    public void WritesThroughToOneNavigations()
    {
        Assert.Equal("MERGE INTO \"t_user\" t USING (SELECT t.\"id\" AS \"eq_key_0\",? AS \"eq_value_0\" FROM \"t_user\" t "
            + "INNER JOIN \"t_company\" t1 ON t1.\"id\" = t.\"company_id\" WHERE t1.\"name\" = ?) eq_src ON (t.\"id\" = eq_src.\"eq_key_0\") WHEN MATCHED THEN UPDATE SET t.\"name\" = eq_src.\"eq_value_0\"",
            Db2.Update<SysUser>().Set(u => u.Name, "a").Where(u => u.Company!.Name == "x").ToSql());
        Assert.Equal("DELETE FROM \"t_user\" t WHERE EXISTS(SELECT 1 FROM \"t_company\" t1 WHERE t1.\"id\" = t.\"company_id\" AND t1.\"name\" = ?)",
            Db2.Delete<SysUser>().Where(u => u.Company!.Name == "x").ToSql());
    }

    // 格式串里只能有格式符与分隔符，其余文字单独拼接；JSON 的逐级取值合并成一条路径（路径不能从数组根开始）
    [Fact]
    public void DateFormatTextAndJsonPaths() =>
        Assert.Equal("SELECT (VARCHAR_FORMAT(t.\"create_time\",'YYYY') || '年' || VARCHAR_FORMAT(t.\"create_time\",'MM') || '月 ' || VARCHAR_FORMAT(t.\"create_time\",'HH24:MI')) AS \"value1\","
            + "JSON_VALUE(t.\"title\", '$.a[0].b') AS \"value2\",(JSON_EXISTS(t.\"title\", '$.a.c')) AS \"value3\" FROM \"t_topic\" t",
            Db2.Query<Topic>().Select(t => new Draft<string, string?, bool>(t.CreateTime!.Value.ToString("yyyy年MM月 HH:mm"),
                Sql.Json(t.Title).Get("a").Get(0).Text("b"), Sql.Json(t.Title).Get("a").ContainsKey("c"))).ToSql());

    [Fact]
    public void LikeEscapesWildcardValues() =>
        SqlOnly.AssertStatement(Db2.Query<Topic>().Where(t => t.Title!.Contains("30%") && !t.Title!.StartsWith("a_")).ToStatement(),
            TopicColumns + " WHERE \"title\" LIKE ? ESCAPE '\\' AND \"title\" NOT LIKE ? ESCAPE '\\'", "%30\\%%(String),a\\_%(String)");

    // FOR UPDATE WITH RS 锁到事务结束；SKIP LOCKED DATA；没有 NOWAIT
    [Fact]
    public void RowLocks()
    {
        using var tx = Db2.BeginTransaction();
        Assert.Equal(TopicColumns + " WHERE \"id\" = ? FOR UPDATE WITH RS", Db2.Query<Topic>().WhereById("1").ForUpdate().ToSql());
        Assert.Equal(TopicColumns + " WHERE \"id\" = ? FOR UPDATE SKIP LOCKED DATA", Db2.Query<Topic>().WhereById("1").ForUpdateSkipLocked().ToSql());
        Assert.Contains("NOWAIT", Assert.Throws<NotSupportedException>(() => Db2.Query<Topic>().WhereById("1").ForUpdateNoWait().ToSql()).Message);
    }

    // 字符串列按字符计长；自增列 GENERATED BY DEFAULT AS IDENTITY
    [Fact]
    public void CreateTable() =>
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"t_topic_auto\" (\"id\" INTEGER GENERATED BY DEFAULT AS IDENTITY NOT NULL, \"stars\" INTEGER NULL, \"title\" VARCHAR(255 CODEUNITS32) NULL, \"create_time\" TIMESTAMP NULL, PRIMARY KEY (\"id\"))",
            Db2.CodeFirst.CreateTableSql(typeof(TopicAuto)));
}
