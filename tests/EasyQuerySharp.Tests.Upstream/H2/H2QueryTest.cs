using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/H2QueryTest.java（多表 LEFT JOIN、各类型写入后读回、按属性的条件值过滤、建表语句）。改在 SQLite 上跑（见 <see cref="H2Fixture"/>）。
/// Java 的 ALLTYPE 与 ALLTYPE1 只差 Lombok 与手写访问器，C# 合成 <see cref="ALLTYPE"/>，*_proxy 用例与对应的非 proxy 用例相同、只是主键不同。
/// </summary>
public sealed class H2QueryTest(H2Fixture fixture) : H2Test(fixture)
{
    private const string DefColumns = "SELECT t.id,t.user_name,t.nickname,t.enable,t.score,t.mobile,t.avatar,t.number,t.status,t.created,t.options FROM t_def_table t LEFT JOIN t_def_table_left1 t1 ON t.id = t1.def_id";
    private const string Join2 = " LEFT JOIN t_def_table_left2 t2 ON t.id = t2.def1_id";
    private const string Join3As2 = " LEFT JOIN t_def_table_left3 t2 ON t.id = t2.def2_id";
    private const string Join3 = " LEFT JOIN t_def_table_left3 t3 ON t.id = t3.def2_id";
    private const string DdlDeviation = "建表语句由 SQLite 方言生成（单行、INTEGER / TIMESTAMP 等标准类型名）；Java 是 H2 的多行格式（INT、TIMESTAMP(3)）";

    // Java 先 toList 断言只有一行，再 toSQL 断言 SQL
    [Fact, Upstream("h2/H2QueryTest.java#leftJoin1")]
    public async Task LeftJoin1()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).Where((t, t1) => t.Id == "1" && t1.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + " WHERE t.id = ? AND t1.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin1_1")]
    public async Task LeftJoin1_1()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).Where((t, _) => t.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + " WHERE t.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin2")]
    public async Task LeftJoin2()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).LeftJoin<DefTableLeft2>((t, _, t2) => t.Id == t2.Def1Id)
            .Where((t, t1, t2) => t.Id == "1" && t1.Id == "1" && t2.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + Join2 + " WHERE t.id = ? AND t1.id = ? AND t2.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin2_1")]
    public async Task LeftJoin2_1()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).LeftJoin<DefTableLeft2>((t, _, t2) => t.Id == t2.Def1Id)
            .Where((t, _, _) => t.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + Join2 + " WHERE t.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin3")]
    public async Task LeftJoin3()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).LeftJoin<DefTableLeft3>((t, _, t3) => t.Id == t3.Def2Id)
            .Where((t, t1, t2) => t.Id == "1" && t1.Id == "1" && t2.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + Join3As2 + " WHERE t.id = ? AND t1.id = ? AND t2.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin3_1")]
    public async Task LeftJoin3_1()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).LeftJoin<DefTableLeft3>((t, _, t3) => t.Id == t3.Def2Id)
            .Where((t, _, _) => t.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + Join3As2 + " WHERE t.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin4")]
    public async Task LeftJoin4()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).LeftJoin<DefTableLeft2>((t, _, t2) => t.Id == t2.Def1Id)
            .LeftJoin<DefTableLeft3>((t, _, _, t3) => t.Id == t3.Def2Id)
            .Where((t, t1, t2, t3) => t.Id == "1" && t1.Id == "1" && t2.Id == "1" && t3.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + Join2 + Join3 + " WHERE t.id = ? AND t1.id = ? AND t2.id = ? AND t3.id = ?", query.ToSql());
    }

    [Fact, Upstream("h2/H2QueryTest.java#leftJoin4_1")]
    public async Task LeftJoin4_1()
    {
        var query = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId).LeftJoin<DefTableLeft2>((t, _, t2) => t.Id == t2.Def1Id)
            .LeftJoin<DefTableLeft3>((t, _, _, t3) => t.Id == t3.Def2Id)
            .Where((t, _, _, _) => t.Id == "1");
        Assert.Single(await query.ToListAsync(Ct));
        Assert.Equal(DefColumns + Join2 + Join3 + " WHERE t.id = ?", query.ToSql());
    }

    // 写入一行各类型的值，按主键读回后逐个属性比较
    private async Task AssertRoundTrip(string id)
    {
        var inserted = ALLTYPE.Sample(id);
        Assert.Equal(1, await Db.Insert(inserted).ExecuteAsync(Ct));
        var read = await Db.Query<ALLTYPE>().WhereById(id).FirstOrDefaultAsync(Ct);
        Assert.NotNull(read);
        Assert.Equivalent(inserted, read, strict: true);
    }

    // 同上，读回时按同名属性映射到 VO（Java 的 select(ALLTYPEVO1.class)）
    private async Task AssertRoundTripToVo(string id)
    {
        var inserted = ALLTYPE.Sample(id);
        Assert.Equal(1, await Db.Insert(inserted).ExecuteAsync(Ct));
        var read = await Db.Query<ALLTYPE>().WhereById(id).Select<ALLTYPEVO1>().FirstOrDefaultAsync(Ct);
        Assert.NotNull(read);
        Assert.Equivalent(inserted, read, strict: true);
    }

    [Fact, Upstream("h2/H2QueryTest.java#allTypeTest1")]
    public Task AllTypeTest1() => AssertRoundTrip("123");

    [Fact, Upstream("h2/H2QueryTest.java#allTypeTest1_1")]
    public Task AllTypeTest1_1() => AssertRoundTrip("1234x");

    [Fact, Upstream("h2/H2QueryTest.java#allTypeTest1_2")]
    public Task AllTypeTest1_2() => AssertRoundTripToVo("1235");

    [Fact, Upstream("h2/H2QueryTest.java#allTypeTest1_proxy")]
    public Task AllTypeTest1_proxy() => AssertRoundTrip("123456");

    [Fact, Upstream("h2/H2QueryTest.java#allTypeTest1_1_proxy")]
    public Task AllTypeTest1_1_proxy() => AssertRoundTrip("1234567");

    [Fact, Upstream("h2/H2QueryTest.java#allTypeTest1_2_proxy")]
    public Task AllTypeTest1_2_proxy() => AssertRoundTripToVo("1235678");

    // Java 写入 13 行 ALLTYPESharding（按 number_integer 分表），整表查询、倒序、倒序再分页，比较每一行
    [Fact(Skip = "等 4.4 分库分表"), Upstream("h2/H2QueryTest.java#allTypeTestSharding1", Pending = true)]
    public Task AllTypeTestSharding1() => Task.CompletedTask;

    // Java 的 filterConfigure((t, p, v) -> …) 按属性名决定过滤与否；C# 的 UseValueFilter 只对之后追加的 Where 生效，
    // 恒保留的条件写在它之前，恒去掉的条件不写，其余条件按 NotNullOrBlank 过滤。SQL 与 Java 相同
    [Fact, Upstream("h2/H2QueryTest.java#conditionTest1")]
    public void ConditionTest1()
    {
        var id = "";
        string? userName = null;
        var nickname = "BBB";
        bool? leftEnable = true;

        // id 恒保留，其余为空时去掉
        var sql = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId)
            .Where((t, _) => t.Id == id)
            .UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where((t, t1) => t.UserName == userName && t.Nickname == nickname && t1.Enable == leftEnable)
            .ToSql();
        Assert.Equal(DefColumns + " WHERE t.id = ? AND t.nickname = ? AND t1.enable = ?", sql);

        // enable 恒去掉，其余为空时去掉
        sql = Db.Query<DefTable>().LeftJoin<DefTableLeft1>((t, t1) => t.Id == t1.DefId)
            .UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where((t, _) => t.Id == id && t.UserName == userName && t.Nickname == nickname)
            .ToSql();
        Assert.Equal(DefColumns + " WHERE t.nickname = ?", sql);
    }

    [Fact, Upstream("h2/H2QueryTest.java#testDDL", Deviation = DdlDeviation)]
    public async Task TestDDL()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(Topic)], Ct);
        using var listen = SqlCapture.Listen();
        await Db.CodeFirst.CreateTablesAsync([typeof(Topic)], Ct);
        listen.AssertSql("CREATE TABLE IF NOT EXISTS t_topic (id VARCHAR(255) NOT NULL, stars INTEGER NULL, title VARCHAR(255) NULL, create_time TIMESTAMP NULL, PRIMARY KEY (id))");
    }

    [Fact, Upstream("h2/H2QueryTest.java#testDDL2", Deviation = DdlDeviation)]
    public async Task TestDDL2()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(MultiPrimaryEntity)], Ct);
        using var listen = SqlCapture.Listen();
        await Db.CodeFirst.CreateTablesAsync([typeof(MultiPrimaryEntity)], Ct);
        listen.AssertSql("CREATE TABLE IF NOT EXISTS t_multi_primary (id1 VARCHAR(255) NOT NULL, id2 VARCHAR(255) NOT NULL, PRIMARY KEY (id1,id2))");
    }

    // Java 回归 H2 方言的注释写法（列注释内联、表注释写成独立的 COMMENT ON TABLE）；SQLite 没有表注释与列注释，建表语句不带注释
    [Fact, Upstream("h2/H2QueryTest.java#testTableCommentDDL", Deviation = "SQLite 不支持表注释与列注释，建表语句里没有 COMMENT；Java 断言 H2 的 COMMENT ON TABLE 语句")]
    public async Task TestTableCommentDDL()
    {
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(H2TableCommentEntity)], Ct);
        using var listen = SqlCapture.Listen();
        await Db.CodeFirst.CreateTablesAsync([typeof(H2TableCommentEntity)], Ct);
        listen.AssertSql("CREATE TABLE IF NOT EXISTS t_h2_table_comment (id VARCHAR(255) NOT NULL, name VARCHAR(255) NULL, PRIMARY KEY (id))");
        Assert.Equal(0, await Db.Query<H2TableCommentEntity>().CountAsync(Ct));
    }
}
