using SysBankCard = EasyQuerySharp.Tests.Upstream.Mysql8.SysBankCard;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Mssql;

/// <summary>
/// 对应 Java：mssql/MsSQLQueryTest.java（SQL Server 上的主题查询：分页、字符串比较、分组拼接、更新、BigDecimal 运算、批量插入、表提示）。
/// Java 的 MsSQLMyTopic1 与 MsSQLMyTopic 是同一张表，只是属性名首字母大写，C# 的属性本来就是大写开头，共用一个实体。
/// </summary>
public sealed class MsSQLQueryTest(MsSqlFixture fixture) : MsSqlTest(fixture)
{
    private const string TopicSql = "SELECT [Id],[Stars],[Title],[CreateTime] FROM [MyTopic]";

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query0"), Upstream("mssql/MsSQLQueryTest.java#query2")]
    public async Task Query0()
    {
        var query = Db.Query<MsSQLMyTopic>().Where(o => o.Id == "123xxx");
        Assert.Equal(TopicSql + " WHERE [Id] = ?", query.ToSql());
        Assert.Null(await query.FirstOrDefaultAsync(Ct));
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query1"), Upstream("mssql/MsSQLQueryTest.java#query3")]
    public async Task Query1()
    {
        var query = Db.Query<MsSQLMyTopic>().Where(o => o.Id == "1");
        Assert.Equal(TopicSql + " WHERE [Id] = ?", query.ToSql());
        var topic = await query.FirstOrDefaultAsync(Ct);
        Assert.NotNull(topic);
        Assert.NotNull(topic.Id);
        Assert.NotNull(topic.Stars);
        Assert.NotNull(topic.Title);
        Assert.NotNull(topic.CreateTime);
    }

    // 没有排序时 SQL Server 的 OFFSET 要求 ORDER BY：补主键
    [Fact, Upstream("mssql/MsSQLQueryTest.java#query4")]
    public async Task Query4()
    {
        var page = await Db.Query<MsSQLMyTopic>().Where(o => o.Id != null).ToPageAsync(2, 20, Ct);
        Assert.Equal(20, page.Rows.Count);
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query5")]
    public async Task Query5()
    {
        var page = await Db.Query<MsSQLMyTopic>().Where(o => o.Id != null).OrderBy(o => o.Id).ToPageAsync(2, 20, Ct);
        Assert.Equal(20, page.Rows.Count);
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query6")]
    public async Task Query6()
    {
        var page = await Db.Query<MsSQLMyTopic>().Where(o => o.Id != null).OrderBy(o => o.Stars).ToPageAsync(2, 20, Ct);
        Assert.Equal(20, page.Rows.Count);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal((i + 20).ToString(System.Globalization.CultureInfo.InvariantCulture), page.Rows[i].Id);
            Assert.Equal(i + 120, page.Rows[i].Stars);
        }
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query7")]
    public async Task Query7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MsSQLMyTopic>().Where(o => string.Compare(o.Title, "1") == 1).ToListAsync(Ct);
        listen.AssertSql(TopicSql + " WHERE (CASE WHEN [Title] = ? THEN 0 WHEN [Title] > ? THEN 1 ELSE -1 END) = ?");
        listen.AssertParams("1(String),1(String),1(Integer)");
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query8")]
    public async Task Query8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MsSQLMyTopic>().Where(o => string.Compare(o.Title, o.Id) == 1).ToListAsync(Ct);
        listen.AssertSql(TopicSql + " WHERE (CASE WHEN [Title] = [Id] THEN 0 WHEN [Title] > [Id] THEN 1 ELSE -1 END) = ?");
        listen.AssertParams("1(Integer)");
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#query9")]
    public async Task Query9()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MsSQLMyTopic>().GroupBy(o => o.Title).Select(g => new Draft<string?, string>(g.Key, g.StringJoin(x => x.Id, ","))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.[Title] AS [Value1],STRING_AGG(t.[Id], ?) AS [Value2] FROM [MyTopic] t GROUP BY t.[Title]");
        listen.AssertParams(",(String)");
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#update1")]
    public async Task Update1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Update<MsSQLMyTopic>().Set(t => t.Title, "123xx")
            .Where(m => m.Id == null && Sql.Exists(Db.Query<MsSQLMyTopic>().Where(m1 => m1.Id == m.Id))).ExecuteAsync(Ct);
        listen.AssertSql("UPDATE t SET t.[Title] = ? FROM [MyTopic] t WHERE t.[Id] IS NULL AND EXISTS(SELECT 1 FROM [MyTopic] t1 WHERE t1.[Id] = t.[Id])");
        listen.AssertParams("123xx(String)");
    }

    // 以下四个用例用 t_calc 做 decimal 运算；用例自己清理插入的行，t_calc 在固件里建好
    private static MsSQLCalc Calc(string id, string column3) => new() { Id = id, Column1 = -8.87m, Column2 = -1.15m, Column3 = decimal.Parse(column3, System.Globalization.CultureInfo.InvariantCulture) };

    // (-8.87 + -1.15) / 1.0000 在 SQL Server 里是 decimal(38,19)，读回的值与 -10.02 相等
    [Fact, Upstream("mssql/MsSQLQueryTest.java#testBigDecimal")]
    public async Task TestBigDecimal()
    {
        try
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereById("1").ExecuteAsync(Ct);
            await Db.Insert(Calc("1", "1.0000")).ExecuteAsync(Ct);

            var list = await Db.Query<MsSQLCalc>()
                .Select(m => new Draft<decimal?>(m.Id == null ? 1m : (m.Column1 + m.Column2) / m.Column3))
                .ToListAsync(Ct);
            Assert.NotEmpty(list);
            Assert.All(list, row => Assert.Equal(-10.02m, row.Value1));
        }
        finally
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereById("1").ExecuteAsync(Ct);
        }
    }

    [Fact, Upstream("mssql/MsSQLQueryTest.java#testBigDecima2")]
    public async Task TestBigDecima2()
    {
        try
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereById("1").ExecuteAsync(Ct);
            await Db.Insert(Calc("1", "1.0000")).ExecuteAsync(Ct);

            var list = await Db.Query<MsSQLCalc>()
                .Select(m => new MsSQLCalc { Column1 = m.Id == null ? 0m : (m.Column1 + m.Column2) / m.Column3 })
                .ToListAsync(Ct);
            Assert.NotEmpty(list);
            Assert.All(list, row => Assert.Equal(-10.02m, row.Column1));
        }
        finally
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereById("1").ExecuteAsync(Ct);
        }
    }

    // 自身 decimal 列与派生表里的同一列相等连接。Java 的 Map 投影写成 Draft，别名是 value1 / value2 而不是 aa / bb（用例没有断言 SQL）
    [Fact, Upstream("mssql/MsSQLQueryTest.java#testBigDecima4")]
    public async Task TestBigDecima4()
    {
        try
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereById("1").ExecuteAsync(Ct);
            await Db.Insert(Calc("1", "1.0000")).ExecuteAsync(Ct);

            var select = Db.Query<MsSQLCalc>().Select(m => new Draft<decimal?, string?>(m.Column1, m.Id));
            var list = await Db.Query<MsSQLCalc>()
                .LeftJoin(select, (m, o2) => m.Column1 == o2.Value1)
                .Where((a, b) => b.Value2 == a.Id)
                .ToListAsync(Ct);
            Assert.Single(list);
        }
        finally
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereById("1").ExecuteAsync(Ct);
        }
    }

    // 一批四行（decimal 的小数位各不相同）走 .Batch()；SQL Server 的驱动支持 DbBatch，一组一个往返
    [Fact, Upstream("mssql/MsSQLQueryTest.java#testBatchInsert")]
    public async Task TestBatchInsert()
    {
        string[] ids = ["2", "3", "4", "5"];
        try
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereByIds(ids).ExecuteAsync(Ct);
            var calcs = new[] { Calc("2", "1.10"), Calc("3", "1.0010"), Calc("4", "1.234"), Calc("5", "1.0000") };
            await Db.Insert(calcs).Batch().ExecuteAsync(Ct);

            var list = await Db.Query<MsSQLCalc>().OrderBy(c => c.Id).ToListAsync(Ct);
            Assert.Equal(ids, list.Select(c => c.Id));
            Assert.Equal([1.1m, 1.001m, 1.234m, 1m], list.Select(c => c.Column3));
        }
        finally
        {
            await Db.Delete<MsSQLCalc>().AllowDeleteStatement(true).WhereByIds(ids).ExecuteAsync(Ct);
        }
    }

    // SQL Server 的 CONCAT 跳过 NULL
    [Fact, Upstream("mssql/MsSQLQueryTest.java#concat1")]
    public async Task Concat1()
    {
        var rows = await Db.FromSql<DynamicRow>("select CONCAT('a', null , 'b') as [aa] ").ToListAsync(Ct);
        Assert.Equal("ab", rows[0]["aa"]);
    }

    // 表提示指向不存在的索引，SQL Server 报错；SQL 已发出并被监听器记下，对应 Java 的 try / catch
    [Fact, Upstream("mssql/MsSQLQueryTest.java#testWithIndex")]
    public async Task TestWithIndex()
    {
        using var listen = SqlCapture.Listen();
        var query = Db.Query<SysUser>()
            .AsTableSegment((table, alias) => alias is null ? table + " WITH(INDEX(myIndex))" : table + " " + alias + " WITH(INDEX(myIndex))")
            .LeftJoin<SysBankCard>((user, card) => user.Id == card.Uid);
        await Assert.ThrowsAnyAsync<Exception>(() => query.ToListAsync(Ct));
        listen.AssertSql("SELECT t.[Id],t.[Name],t.[Phone],t.[Age],t.[CreateTime] FROM [t_sys_user] t WITH(INDEX(myIndex)) LEFT JOIN [t_bank_card] t1 ON t.[Id] = t1.[Uid]");
    }
}
