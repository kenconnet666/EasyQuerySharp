using System.Linq.Expressions;
using EasyQuerySharp.Commands;
using EasyQuerySharp.Dialects;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>
/// 对应 Java fork：dialect/HanaSqlTest.java（SAP HANA 方言只渲染、不连库）。
/// fork 仓库不在本机：有 SQL 的用例按快照里的 SQL 逐字断言，快照里没有 SQL 的按方法名与同组用例里 Java 的写法规则断言。
/// </summary>
public sealed class HanaSqlTest
{
    private const string UserColumns = "\"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\"";
    private const string AliasedUserColumns = "t.\"id\",t.\"name\",t.\"age\",t.\"company_id\",t.\"attrs\",t.\"created_at\"";

    private static EasyDb Hana => SqlOnly.Db("hana");

    [Fact, Upstream("fork/dialect/HanaSqlTest.java#pagingUsesLimitOffset")]
    public void PagingUsesLimitOffset()
    {
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Name!.Contains("a")).OrderBy(u => u.Id).Skip(10).Take(20).ToStatement(),
            $"SELECT {UserColumns} FROM \"t_fork_user\" WHERE \"name\" LIKE ? ORDER BY \"id\" ASC LIMIT 20 OFFSET 10", "%a%(String)");
        // OFFSET 前必须有 LIMIT
        Assert.EndsWith(" ORDER BY \"id\" ASC LIMIT 2147483647 OFFSET 10", Hana.Query<ForkUser>().OrderBy(u => u.Id).Skip(10).ToSql());
    }

    // HANA 的关联子查询不支持 LIMIT：集合导航的 EXISTS 不写 LIMIT 1（其他方言写）
    [Fact, Upstream("fork/dialect/HanaSqlTest.java#existsSubqueryHasNoLimit")]
    public void ExistsSubqueryHasNoLimit() =>
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Where(u => u.Company!.Name == "acme" && u.Posts!.Any(p => p.Title == "orm")).ToStatement(),
            $"SELECT {AliasedUserColumns} FROM \"t_fork_user\" t LEFT JOIN \"t_fork_company\" t1 ON t1.\"id\" = t.\"company_id\""
            + " WHERE t1.\"name\" = ? AND EXISTS(SELECT 1 FROM \"t_fork_post\" t2 WHERE t2.\"user_id\" = t.\"id\" AND t2.\"title\" = ?)",
            "acme(String),orm(String)");

    // 拼接逐对加括号；IndexOf 用 LOCATE；日期格式里的文字单独拼接；条件当值写成三值的 CASE
    [Fact, Upstream("fork/dialect/HanaSqlTest.java#functions")]
    public void Functions() =>
        SqlOnly.AssertStatement(Hana.Query<ForkUser>().Select(u => new Draft<string?, string?, int, int, string, DateTime?, bool>(
                u.Name + "-" + u.Id, u.Name!.Substring(0, 4), u.Name!.IndexOf("a"), u.Age ?? 0,
                u.CreatedAt!.Value.ToString("yyyy年MM月dd日 HH:mm"), u.CreatedAt!.Value.AddDays(1), u.Posts!.Any())).ToStatement(),
            "SELECT ((t.\"name\" || ?) || t.\"id\") AS \"value1\",SUBSTRING(t.\"name\",1,4) AS \"value2\",(LOCATE(t.\"name\",?) - 1) AS \"value3\",COALESCE(t.\"age\",?) AS \"value4\","
            + "(TO_NVARCHAR(t.\"created_at\",'YYYY') || '年' || TO_NVARCHAR(t.\"created_at\",'MM') || '月' || TO_NVARCHAR(t.\"created_at\",'DD') || '日 ' || TO_NVARCHAR(t.\"created_at\",'HH24:MI')) AS \"value5\","
            + "ADD_DAYS(t.\"created_at\",?) AS \"value6\","
            + "(CASE WHEN (EXISTS(SELECT 1 FROM \"t_fork_post\" t1 WHERE t1.\"user_id\" = t.\"id\")) THEN TRUE WHEN NOT ((EXISTS(SELECT 1 FROM \"t_fork_post\" t1 WHERE t1.\"user_id\" = t.\"id\"))) THEN FALSE ELSE NULL END) AS \"value7\""
            + " FROM \"t_fork_user\" t",
            "-(String),a(String),0(Integer),1(Long)");

    // 格式符和夹在格式符之间的 - / : . , 空格留在 TO_NVARCHAR 的格式串里，其余文字（含单引号）单独拼接
    [Fact, Upstream("fork/dialect/HanaSqlTest.java#dateFormatTranslation")]
    public void DateFormatTranslation()
    {
        string Format(string format) => Hana.Query<ForkUser>().Select(u => u.CreatedAt!.Value.ToString(format)).ToSql()["SELECT ".Length..^" FROM \"t_fork_user\" t".Length];

        Assert.Equal("TO_NVARCHAR(t.\"created_at\",'YYYY-MM-DD HH24:MI:SS')", Format("yyyy-MM-dd HH:mm:ss"));
        Assert.Equal("TO_NVARCHAR(t.\"created_at\",'YY/MM/DD HH12:MI:SS.FF3')", Format("yy/MM/dd hh:mm:ss.fff"));
        Assert.Equal("(TO_NVARCHAR(t.\"created_at\",'YYYY') || '年' || TO_NVARCHAR(t.\"created_at\",'MM') || '月')", Format("yyyy年MM月"));
        Assert.Equal("(TO_NVARCHAR(t.\"created_at\",'HH24:MI') || ' o''clock')", Format("HH:mm o'clock"));
    }

    // 更新走对一导航时写成按主键对上的 MERGE；删除写成 EXISTS
    [Fact, Upstream("fork/dialect/HanaSqlTest.java#joinedUpdateIsMergeAndJoinedDeleteIsExists")]
    public void JoinedUpdateIsMergeAndJoinedDeleteIsExists()
    {
        SqlOnly.AssertStatement(Hana.Update<ForkUser>().Set(u => u.Age, 30).Where(u => u.Company!.Name == "acme").ToStatement(),
            "MERGE INTO \"t_fork_user\" t USING (SELECT t.\"id\" AS \"eq_key_0\",? AS \"eq_value_0\" FROM \"t_fork_user\" t INNER JOIN \"t_fork_company\" t1 ON t1.\"id\" = t.\"company_id\""
            + " WHERE t1.\"name\" = ?) eq_src ON (t.\"id\" = eq_src.\"eq_key_0\") WHEN MATCHED THEN UPDATE SET t.\"age\" = eq_src.\"eq_value_0\"",
            "30(Integer),acme(String)");
        SqlOnly.AssertStatement(Hana.Delete<ForkUser>().Where(u => u.Company!.Name == "acme").ToStatement()!,
            "DELETE FROM \"t_fork_user\" t WHERE EXISTS(SELECT 1 FROM \"t_fork_company\" t1 WHERE t1.\"id\" = t.\"company_id\" AND t1.\"name\" = ?)",
            "acme(String)");
    }

    [Fact, Upstream("fork/dialect/HanaSqlTest.java#upsertIsMerge")]
    public void UpsertIsMerge() =>
        SqlOnly.AssertStatement(Hana.Insert(new ForkUser { Id = "u1", Name = "n1" }).Columns(ColumnStrategy.OnlyNotNull).OnConflictUpdate().ToStatement(),
            "MERGE INTO \"t_fork_user\" t1 USING (SELECT ? AS \"id\",? AS \"name\" FROM DUMMY) t2 ON (t1.\"id\" = t2.\"id\")"
            + " WHEN MATCHED THEN UPDATE SET t1.\"name\" = t2.\"name\" WHEN NOT MATCHED THEN INSERT (\"id\",\"name\") VALUES (t2.\"id\",t2.\"name\")",
            "u1(String),n1(String)");
}

/// <summary>
/// 对应 Java fork：dialect/HanaTextSqlTest.java（<c>Sql.Hana.Text(..)</c> 全文检索：CONTAINS、FUZZY、SCORE）。
/// 快照里没有这两条的 SQL，写法按 Java 的格式（参数间无空格）。
/// </summary>
public sealed class HanaTextSqlTest
{
    private static EasyDb Hana => SqlOnly.Db("hana");

    // 普通条件、隐式子查询、投影与排序里都能用
    [Fact, Upstream("fork/dialect/HanaTextSqlTest.java#containsAndFuzzy")]
    public void ContainsAndFuzzy()
    {
        SqlOnly.AssertStatement(Hana.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Contains("easy orm")).ToStatement(),
            "SELECT \"id\",\"user_id\",\"title\",\"amount\" FROM \"t_fork_post\" WHERE CONTAINS(\"title\",?)", "easy orm(String)");
        SqlOnly.AssertStatement(Hana.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title, p.UserId).Fuzzy("easy qurey", 0.8))
                .OrderByDescending(p => Sql.Hana.Text(p.Title).Score())
                .Select(p => new Draft<string?, double>(p.Id, Sql.Hana.Text(p.Title).Score())).ToStatement(),
            "SELECT t.\"id\" AS \"value1\",SCORE() AS \"value2\" FROM \"t_fork_post\" t WHERE CONTAINS((t.\"title\",t.\"user_id\"),?,FUZZY(0.8)) ORDER BY SCORE() DESC",
            "easy qurey(String)");
        Assert.Equal("SELECT t.\"id\",t.\"name\",t.\"age\",t.\"company_id\",t.\"attrs\",t.\"created_at\" FROM \"t_fork_user\" t"
                     + " WHERE EXISTS(SELECT 1 FROM \"t_fork_post\" t1 WHERE t1.\"user_id\" = t.\"id\" AND CONTAINS(t1.\"title\",?))",
            Hana.Query<ForkUser>().Where(u => u.Posts!.Any(p => Sql.Hana.Text(p.Title).Contains("orm"))).ToSql());
    }

    [Fact, Upstream("fork/dialect/HanaTextSqlTest.java#wrongDialectAndBadThreshold")]
    public void WrongDialectAndBadThreshold()
    {
        Assert.Contains("Sql.Hana.Text", Assert.Throws<NotSupportedException>(() =>
            SqlOnly.Db("mysql").Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Contains("orm")).ToSql()).Message);
        // 阈值写进 SQL，只接受 0–1
        Assert.Contains("0 到 1", Assert.ThrowsAny<ArgumentException>(() =>
            Hana.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Fuzzy("orm", 1.5)).ToSql()).Message);
        Assert.ThrowsAny<ArgumentException>(() => Hana.Query<ForkPost>().Where(p => Sql.Hana.Text(p.Title).Fuzzy("orm", -0.1)).ToSql());
    }
}

/// <summary>
/// HANA 方言自身的写法（Java 用例没覆盖到的部分，C# 独有）：位置参数、截断与数值函数、类型转换、建表删表的匿名块、递归 CTE。
/// </summary>
public sealed class HanaDialectTest
{
    private static EasyDb Hana => SqlOnly.Db("hana");

    // 只取投影那一列的 SQL
    private static string Column<T>(Expression<Func<ForkUser, T>> value)
    {
        var selector = Expression.Lambda<Func<ForkUser, Draft<T>>>(Expression.New(typeof(Draft<T>).GetConstructors()[0], value.Body), value.Parameters);
        var sql = Hana.Query<ForkUser>().Select(selector).ToSql();
        return sql["SELECT ".Length..sql.IndexOf(" AS \"value1\"", StringComparison.Ordinal)];
    }

    // HANA 的客户端驱动只认位置参数
    [Fact]
    public void ParametersArePositional() =>
        Assert.EndsWith("WHERE \"id\" = ? AND \"age\" > ?", Hana.Query<ForkUser>().Where(u => u.Id == "u1" && u.Age > 1).ToStatement().CommandText);

    [Fact]
    public void DateTruncFormatsAndParsesBack()
    {
        Assert.Equal("TO_TIMESTAMP(TO_NVARCHAR(t.\"created_at\",'YYYY')||'-01-01','YYYY-MM-DD')", Column(u => u.CreatedAt.DateTrunc(DateTruncUnit.Year)));
        Assert.Equal("ADD_MONTHS(TO_TIMESTAMP(TO_NVARCHAR(t.\"created_at\",'YYYY-MM')||'-01','YYYY-MM-DD'),-MOD(MONTH(t.\"created_at\")-1,3))",
            Column(u => u.CreatedAt.DateTrunc(DateTruncUnit.Quarter)));
        Assert.Equal("TO_TIMESTAMP(TO_NVARCHAR(t.\"created_at\",'YYYY-MM-DD HH24')||':00:00','YYYY-MM-DD HH24:MI:SS')", Column(u => u.CreatedAt.DateTrunc(DateTruncUnit.Hour)));
    }

    // 没有 TRUNC / LOG10，对数的底数在前；GREATEST 原生支持
    [Fact]
    public void NumericFunctions()
    {
        Assert.Equal("ROUND(t.\"age\",0,ROUND_DOWN)", Column(u => Math.Truncate((double)u.Age!.Value)));
        Assert.Equal("LOG(10,t.\"age\")", Column(u => Math.Log10(u.Age!.Value)));
        Assert.Equal("LOG(?,t.\"age\")", Column(u => Math.Log(u.Age!.Value, 2)));
        Assert.Equal("GREATEST(t.\"age\",?,?)", Column(u => Sql.MaxColumns(u.Age, 1, 2)));
    }

    [Fact]
    public void CastsUseHanaConversions()
    {
        Assert.Equal("TO_NVARCHAR(t.\"age\")", Column(u => Convert.ToString(u.Age)));
        Assert.Equal("CAST(t.\"name\" AS INTEGER)", Column(u => int.Parse(u.Name!)));
        Assert.Equal("TO_TIMESTAMP(t.\"name\")", Column(u => DateTime.Parse(u.Name!)));
    }

    // HANA 没有 IF [NOT] EXISTS：放进匿名块，忽略"已存在 / 不存在"；没有带时区的时间类型
    [Fact]
    public void DdlIgnoresExistingAndMissingTables()
    {
        Assert.Equal("DO BEGIN DECLARE EXIT HANDLER FOR SQL_ERROR_CODE 288 BEGIN END; EXEC 'CREATE COLUMN TABLE \"t_fork_user\" (\"id\" NVARCHAR(255) NOT NULL, \"name\" NVARCHAR(255) NULL,"
                     + " \"age\" INTEGER NULL, \"company_id\" NVARCHAR(255) NULL, \"attrs\" NVARCHAR(255) NULL, \"created_at\" TIMESTAMP NULL, PRIMARY KEY (\"id\"))'; END",
            Hana.CodeFirst.CreateTableSql(typeof(ForkUser)));
        Assert.Throws<NotSupportedException>(() => Hana.CodeFirst.CreateTableSql(typeof(ForkEvent)));
    }

    [Fact]
    public void RecursiveCteIsRejected() => Assert.Throws<NotSupportedException>(() => HanaDialect.Instance.RecursiveWith);
}
