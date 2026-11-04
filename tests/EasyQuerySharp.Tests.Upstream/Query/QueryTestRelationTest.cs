using EasyQuerySharp.Filtering;
using EasyQuerySharp.Tests.Upstream.Doc.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：QueryTestRelationTest.java。复合键导航（ON 写成带括号的 (a AND b)）、条件值过滤去掉条件时连带去掉隐式 JOIN、
/// 子查询引用外层的隐式 JOIN。
/// </summary>
public sealed class QueryTestRelationTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string SignUpJoin = "SELECT t.`id`,t.`com_id`,t.`user_id`,t.`time`,t.`content` FROM `my_sign_up` t LEFT JOIN `my_com_user` t1 ON (t1.`com_id` = t.`com_id` AND t1.`user_id` = t.`user_id`)";

    [Fact, Upstream("QueryTestRelationTest.java#test1")]
    public async Task Test1()
    {
        await Db.Query<MySignUp>().Where(m => m.ComUser!.MyCompany!.Name!.Contains("123")).ToListAsync(Ct);
        await Db.Query<MyComUser>().Select(m => new Draft<long>(m.MySignUps!.Count())).ToListAsync(Ct);
        using var listen = SqlCapture.Listen();
        const string companyName = "公司";
        await Db.Query<MyComUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(su => su.MyCompany!.Name!.Contains(companyName)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`com_id`,t.`user_id`,t.`gw` FROM `my_com_user` t LEFT JOIN `my_company_info` t1 ON t1.`id` = t.`com_id` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%公司%(String)");
    }

    // 条件值过滤只作用于 WHERE，投影里的 CASE 照常
    [Fact, Upstream("QueryTestRelationTest.java#test1_1")]
    public async Task Test1_1()
    {
        using var listen = SqlCapture.Listen();
        const string companyName = "公司";
        await Db.Query<MyComUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(su => su.MyCompany!.Name!.Contains(companyName))
            .Select(m => new Draft<int>(m.MyCompany!.Name!.Contains(companyName) ? 1 : 0)).ToListAsync(Ct);
        listen.AssertSql("SELECT (CASE WHEN t1.`name` LIKE ? THEN ? ELSE ? END) AS `value1` FROM `my_com_user` t LEFT JOIN `my_company_info` t1 ON t1.`id` = t.`com_id` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%公司%(String),1(Integer),0(Integer),%公司%(String)");
    }

    // 空串被过滤掉：条件和它带来的隐式 JOIN 一起消失
    [Fact, Upstream("QueryTestRelationTest.java#test2", Deviation = "条件被过滤后 Java 不再 JOIN 但仍给主表写别名 t，C# 单表不写别名")]
    public async Task Test2()
    {
        using var listen = SqlCapture.Listen();
        const string companyName = "";
        await Db.Query<MyComUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(su => su.MyCompany!.Name!.Contains(companyName)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`com_id`,`user_id`,`gw` FROM `my_com_user`");
        await Db.Query<MyComUser>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(su => su.MyCompany!.Name!.Contains(companyName))
            .Select(m => new Draft<string?, string?>(m.Gw, m.MyCompany!.Name)).ToListAsync(Ct);
    }

    // Java 在 lambda 里用 if 决定加不加条件：C# 的 WhereIf
    [Fact, Upstream("QueryTestRelationTest.java#test3")]
    public async Task Test3()
    {
        using var listen = SqlCapture.Listen();
        const string companyName = "";
        await Db.Query<MyComUser>().WhereIf(!string.IsNullOrWhiteSpace(companyName), su => su.MyCompany!.Name!.Contains(companyName)).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`com_id`,`user_id`,`gw` FROM `my_com_user`");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_1"), Upstream("QueryTestRelationTest.java#relationTest1")]
    public async Task RelationTest0_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().Where(m => m.ComUser!.UserId == "" && m.ComId == "123").ToListAsync(Ct);
        listen.AssertSql(SignUpJoin + " WHERE t1.`user_id` = ? AND t.`com_id` = ?");
        listen.AssertParams("(String),123(String)");
        await Db.Query<MySignUp>().Where(m => m.ComUser!.UserId == "1234" && m.ComId == "123").ToListAsync(Ct);
        listen.AssertParams("1234(String),123(String)");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_2", Deviation = "同 test2：条件被过滤后 Java 仍给主表写别名 t")]
    public async Task RelationTest0_2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(m => m.ComUser!.UserId == "" && m.ComId == "123").ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`com_id`,`user_id`,`time`,`content` FROM `my_sign_up` WHERE `com_id` = ?");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_x")]
    public async Task RelationTest0_x()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().Where(m => Sql.Raw<bool>("{0} = {1}", "123", m.ComUser!.UserId) && m.ComId == "123").ToListAsync(Ct);
        listen.AssertSql(SignUpJoin + " WHERE ? = t1.`user_id` AND t.`com_id` = ?");
        listen.AssertParams("123(String),123(String)");
    }

    // 子查询里引用外层的导航：隐式 JOIN 加在外层，子查询引用 t1
    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_3")]
    public async Task RelationTest0_3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(m => m.ComId.In(Db.Query<MySignUp>().Where(m1 => m1.ComId == m.ComUser!.ComId).Select(m1 => m1.ComId)) && m.ComId == "123").ToListAsync(Ct);
        listen.AssertSql(SignUpJoin + " WHERE t.`com_id` IN (SELECT t2.`com_id` FROM `my_sign_up` t2 WHERE t2.`com_id` = t1.`com_id`) AND t.`com_id` = ?");
        listen.AssertParams("123(String)");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_4")]
    public async Task RelationTest0_4()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank)
            .Where(m => Sql.Exists(Db.Query<MySignUp>().Where(m1 => m1.ComId == m.ComUser!.ComId)) && m.ComId == "123").ToListAsync(Ct);
        listen.AssertSql(SignUpJoin + " WHERE EXISTS(SELECT 1 FROM `my_sign_up` t2 WHERE t2.`com_id` = t1.`com_id`) AND t.`com_id` = ?");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_5")]
    public async Task RelationTest0_5()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(m => m.ComId == "123")
            .Select(m => new { Id = Sql.SubQuery(Db.Query<MySignUp>().Where(m1 => m1.ComId == m.ComUser!.ComId).Select(m1 => Sql.Count())) }).ToListAsync(Ct);
        listen.AssertSql("SELECT (SELECT COUNT(*) FROM `my_sign_up` t2 WHERE t2.`com_id` = t1.`com_id`) AS `id` FROM `my_sign_up` t LEFT JOIN `my_com_user` t1 ON (t1.`com_id` = t.`com_id` AND t1.`user_id` = t.`user_id`) WHERE t.`com_id` = ?");
    }

    // Java 的 orderBy(sql("{0} ASC")) 与普通排序生成同一条 SQL
    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_6"), Upstream("QueryTestRelationTest.java#relationTest0_7")]
    public async Task RelationTest0_6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(m => m.ComId == "123").OrderBy(m => m.ComUser!.Id).ToListAsync(Ct);
        listen.AssertSql(SignUpJoin + " WHERE t.`com_id` = ? ORDER BY t1.`id` ASC");
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(m => m.ComId == "123").OrderByRaw(m => Sql.Raw<object>("{0} ASC", m.ComUser!.Id)).ToListAsync(Ct);
        listen.AssertSql(SignUpJoin + " WHERE t.`com_id` = ? ORDER BY t1.`id` ASC");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest0_8")]
    public async Task RelationTest0_8()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().UseValueFilter(ValueFilter.NotNullOrBlank).Where(m => m.ComId == "123")
            .Select(m => new Draft<object?>(Sql.Raw<object>("{0}", m.ComUser!.ComId))).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`com_id` AS `value1` FROM `my_sign_up` t LEFT JOIN `my_com_user` t1 ON (t1.`com_id` = t.`com_id` AND t1.`user_id` = t.`user_id`) WHERE t.`com_id` = ?");
    }

    [Fact, Upstream("QueryTestRelationTest.java#relationTest2")]
    public async Task RelationTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<MySignUp>().Where(m => m.ComId == "123").Select(m => new Draft<string?, string?>(m.Id, m.ComUser!.Gw)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id` AS `value1`,t1.`gw` AS `value2` FROM `my_sign_up` t LEFT JOIN `my_com_user` t1 ON (t1.`com_id` = t.`com_id` AND t1.`user_id` = t.`user_id`) WHERE t.`com_id` = ?");
        listen.AssertParams("123(String)");
    }
}
