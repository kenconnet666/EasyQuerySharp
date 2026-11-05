using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：DirectRelationTest.java。直达导航沿五张表的链逐级 JOIN（复合键的 ON 带括号）。
/// Java 用例执行前会建表造数（这里在 MySqlFixture 里造）；JOIN 的用例只比对 SQL。
/// </summary>
public sealed class DirectRelationTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string Chain = " FROM `direct1` t LEFT JOIN `direct2` t1 ON t1.`c7` = t.`c1` LEFT JOIN `direct3` t2 ON (t2.`c13` = t1.`c8` AND t2.`c14` = t1.`c9`) LEFT JOIN `direct4` t3 ON t3.`c20` = t2.`c15` LEFT JOIN `direct5` t4 ON (t4.`c22` = t3.`c16` AND t4.`c21` = t3.`c18`)";

    [Fact, Upstream("DirectRelationTest.java#test3")]
    public void Test3() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mysql").Query<Direct1>().Where(d => d.Direct4!.C16 == "123" && d.Direct5!.C21 == "1234").ToStatement(),
            "SELECT t.`c1`,t.`c2`,t.`c3`,t.`c4`,t.`c5`" + Chain + " WHERE t3.`c16` = ? AND t4.`c21` = ?", "123(String),1234(String)");

    // 条件里先用到 Direct5（建出整条链），投影里的 Direct4 复用已有的 JOIN
    [Fact, Upstream("DirectRelationTest.java#testJoin1")]
    public void TestJoin1() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mysql").Query<Direct1>().Where(d => d.Direct5!.C22 == "123").Select(d => new Draft<string?>(d.Direct4!.C18)).ToStatement(),
            "SELECT t3.`c18` AS `value1`" + Chain + " WHERE t4.`c22` = ?", "123(String)");

    /// <summary>对应 Java：dto/Direct1DTO（Direct2.Direct3 平铺成嵌套对象，对象上再平铺 Direct4 的 c20）。</summary>
    public sealed class Direct1DTO
    {
        public string? C1 { get; set; }

        public string? C2 { get; set; }

        public string? C3 { get; set; }

        public string? C4 { get; set; }

        [MapPath("Direct2.Direct3")]
        public InternalDirect3? Direct3 { get; set; }

        /// <summary>Direct3 的投影。</summary>
        public sealed class InternalDirect3
        {
            public string? C11 { get; set; }

            public string? C12 { get; set; }

            public string? C13 { get; set; }

            [MapPath("Direct4.C20")]
            public string? C20 { get; set; }
        }
    }

    /// <summary>对应 Java：dto/Direct1DTO2（同上，不取 c20）。</summary>
    public sealed class Direct1DTO2
    {
        public string? C1 { get; set; }

        public string? C2 { get; set; }

        public string? C3 { get; set; }

        public string? C4 { get; set; }

        [MapPath("Direct2.Direct3")]
        public InternalDirect3? Direct3 { get; set; }

        /// <summary>Direct3 的投影。</summary>
        public sealed class InternalDirect3
        {
            public string? C11 { get; set; }

            public string? C12 { get; set; }

            public string? C13 { get; set; }
        }
    }

    private const string Direct1Rows = "SELECT t.`c1`,t.`c2`,t.`c3`,t.`c4` FROM `direct1` t";
    private const string Direct2Links = "SELECT `c8` AS `__relation__c8`,`c9` AS `__relation__c9`,`c7` AS `__relation__c7` FROM `direct2` WHERE `c7` IN (?,?)";

    // 先查 Direct2 把 c1 换成复合键 (c8, c9)，再按复合键查 Direct3；Direct3 上的平铺再按 c15 查 Direct4
    [Fact, Upstream("DirectRelationTest.java#test4")]
    public async Task Test4()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Direct1>().SelectAutoInclude<Direct1DTO>().ToListAsync(Ct);
        listen.AssertSql(0, Direct1Rows);
        listen.AssertSql(1, Direct2Links);
        listen.AssertParams(1, "1(String),2(String)");
        listen.AssertSql(2, "SELECT t.`c11`,t.`c12`,t.`c13`,t.`c14` AS `__relation__c14`,t.`c15` AS `__relation__c15` FROM `direct3` t WHERE ((t.`c13` =? AND t.`c14` =?))");
        listen.AssertParams(2, "8(String),9(String)");
        foreach (var dto in list)
        {
            if (dto.C1 == "1")
            {
                Assert.Equal("11", dto.Direct3!.C11);
                Assert.Equal("15", dto.Direct3.C20);
            }
            else Assert.Null(dto.Direct3);
        }
    }

    [Fact, Upstream("DirectRelationTest.java#test5")]
    public async Task Test5()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Direct1>().SelectAutoInclude<Direct1DTO2>().ToListAsync(Ct);
        listen.AssertSql(0, Direct1Rows);
        listen.AssertSql(1, Direct2Links);
        listen.AssertParams(1, "1(String),2(String)");
        foreach (var dto in list)
        {
            if (dto.C1 == "1") Assert.Equal("11", dto.Direct3!.C11);
            else Assert.Null(dto.Direct3);
        }
    }

    private const string Direct1All = "SELECT `c1`,`c2`,`c3`,`c4`,`c5` FROM `direct1`";

    // 直达导航的带出：沿路径逐段查中间表（本段目标列在前、下一段自身列在后，不写别名），最后按目标键查 Direct4
    [Fact, Upstream("DirectRelationTest.java#test")]
    public async Task Test()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Direct1>().Include(d => d.Direct4).ToListAsync(Ct);
        listen.AssertSql(0, Direct1All);
        listen.AssertSql(1, "SELECT `c7`,`c8`,`c9` FROM `direct2` WHERE `c7` IN (?,?)");
        listen.AssertParams(1, "1(String),2(String)");
        listen.AssertSql(2, "SELECT `c13`,`c14`,`c15` FROM `direct3` WHERE ((`c13` =? AND `c14` =?))");
        listen.AssertParams(2, "8(String),9(String)");
        listen.AssertSql(3, "SELECT t.`c16`,t.`c17`,t.`c18`,t.`c19`,t.`c20` FROM `direct4` t WHERE t.`c20` IN (?)");
        listen.AssertParams(3, "15(String)");
        Assert.NotNull(list.Single(d => d.C1 == "1").Direct4);
        Assert.Null(list.Single(d => d.C1 == "2").Direct4);
    }

    // 再多走一段：Direct4 → Direct5 是复合键
    [Fact, Upstream("DirectRelationTest.java#test2")]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Direct1>().Include(d => d.Direct5).ToListAsync(Ct);
        listen.AssertSql(0, Direct1All);
        listen.AssertSql(1, "SELECT `c7`,`c8`,`c9` FROM `direct2` WHERE `c7` IN (?,?)");
        listen.AssertParams(1, "1(String),2(String)");
        listen.AssertSql(2, "SELECT `c13`,`c14`,`c15` FROM `direct3` WHERE ((`c13` =? AND `c14` =?))");
        listen.AssertParams(2, "8(String),9(String)");
        listen.AssertSql(3, "SELECT `c20`,`c16`,`c18` FROM `direct4` WHERE `c20` IN (?)");
        listen.AssertParams(3, "15(String)");
        listen.AssertSql(4, "SELECT t.`c21`,t.`c22`,t.`c23`,t.`c24`,t.`c25` FROM `direct5` t WHERE ((t.`c22` =? AND t.`c21` =?))");
        listen.AssertParams(4, "20(String),22(String)");
        Assert.NotNull(list.Single(d => d.C1 == "1").Direct5);
        Assert.Null(list.Single(d => d.C1 == "2").Direct5);
    }
}
