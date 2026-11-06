using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;
using TreeC = EasyQuerySharp.Tests.Upstream.Mysql8.TreeC;

namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>
/// 对应 Java：dameng/DamengQueryTest.java（达梦上的主题查询：ROWNUM 分页、日期格式、分组合并、LISTAGG、拼接式 LIKE、几列取最大最小）。
/// Java 的 like / startsWith 在达梦上把值拼进 SQL：<c>LIKE (TO_CHAR(?)||'%')</c>，C# 对应 <c>Sql.LikeStartsWith</c> 等。
/// </summary>
public sealed class DamengQueryTest : DamengTest
{
    private const string LiveOnly = "Java 在达梦实库上执行并断言结果（或只执行不断言）；C# 只核对渲染出的 SQL，实库验证在 3.1 冒烟里做";

    private const string IncludeLiveOnly = "Java 还断言带出银行卡的第二条语句与结果：要先在实库上读出用户才会发出，C# 只核对第一条，带出与结果在 3.1 冒烟里验证";

    private const string TopicSql = "SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\" FROM \"MY_TOPIC\"";

    private const string UsersSql = "SELECT t.\"ID\",t.\"NAME\",t.\"PHONE\",t.\"AGE\",t.\"CREATE_TIME\" FROM \"t_sys_user\" t";

    // Java 用原生 SQL 验证达梦的 || 把 NULL 当空串（'a' || null || 'b' 得 'ab'）；C# 核对字符串拼接在达梦上写成 ||
    [Fact, Upstream("dameng/DamengQueryTest.java#concat1", Deviation = "Java 用原生 SQL 在实库上验证 || 遇到 NULL 当空串；C# 核对字符串拼接写成 ||，实库验证在 3.1 冒烟里做")]
    public void Concat1() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Select(t => new Draft<string>(t.Title + "a" + t.Id)).ToStatement(),
            "SELECT t.\"TITLE\" || ? || t.\"ID\" AS \"VALUE1\" FROM \"MY_TOPIC\" t", "a(String)");

    // 视图实体上的树查询：视图的 CTE 在递归 CTE 之前定义；达梦与 Oracle 一样不写 RECURSIVE、要写列名清单
    [Fact, Upstream("dameng/DamengQueryTest.java#cteViewTree1", Deviation = LiveOnly)]
    public void CteViewTree1() =>
        Assert.Equal("WITH \"m8_treec\" AS (SELECT t1.\"ID\",t1.\"NAME\",t2.\"APID\" AS \"PID\" FROM \"m8_treea\" t1 LEFT JOIN \"m8_treeb\" t2 ON t1.\"ID\" = t2.\"AID\") " +
                     ",\"as_tree_cte\" (\"cte_deep\",\"ID\",\"NAME\",\"PID\") AS ( (SELECT 0 AS \"cte_deep\",t4.\"ID\",t4.\"NAME\",t4.\"PID\" FROM \"m8_treec\" t4)  UNION ALL  " +
                     "(SELECT t5.\"cte_deep\" + 1 AS \"cte_deep\",t9.\"ID\",t9.\"NAME\",t9.\"PID\" FROM \"as_tree_cte\" t5 INNER JOIN \"m8_treec\" t9 ON t9.\"PID\" = t5.\"ID\") ) " +
                     "SELECT t.\"ID\",t.\"NAME\",t.\"PID\",t.\"cte_deep\" FROM \"as_tree_cte\" t",
            Db.Query<TreeC>().AsTreeCte().ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#query0")]
    public void Query0() => Assert.Equal(TopicSql + " WHERE \"ID\" = ?", Db.Query<DamengMyTopic>().Where(o => o.Id == "123xxx").ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#query0_1")]
    public void Query0_1() => Assert.Equal(TopicSql + " WHERE \"ID\" = ? AND ROWNUM < 2", Db.Query<DamengMyTopic>().Where(o => o.Id == "123xxx").Take(1).ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#query1")]
    public void Query1() =>
        Assert.Equal("SELECT rt.* FROM(SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\", ROWNUM AS \"__rownum__\" FROM \"MY_TOPIC\" WHERE \"ID\" = ? AND ROWNUM < 21) rt WHERE rt.\"__rownum__\" > 10",
            Db.Query<DamengMyTopic>().Where(o => o.Id == "123xxx").Skip(10).Take(10).ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#query2")]
    public void Query2() =>
        Assert.Equal("SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\" FROM \"MY_TOPIC\" WHERE \"ID\" = ? ORDER BY \"CREATE_TIME\" ASC) rt WHERE ROWNUM < 21) rt1 WHERE rt1.\"__rownum__\" > 10",
            Db.Query<DamengMyTopic>().Where(o => o.Id == "123xxx").Skip(10).Take(10).OrderBy(o => o.CreateTime).ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#query3")]
    public void Query3() =>
        Assert.Equal("SELECT rt.* FROM (SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\" FROM \"MY_TOPIC\" WHERE \"ID\" = ? ORDER BY \"CREATE_TIME\" ASC) rt WHERE ROWNUM < 11",
            Db.Query<DamengMyTopic>().Where(o => o.Id == "123xxx").Take(10).OrderBy(o => o.CreateTime).ToSql());

    // 分页（第 2 页、每页 20 条）：先计数，再按 ROWNUM 取这一页
    [Fact, Upstream("dameng/DamengQueryTest.java#query4", Deviation = LiveOnly)]
    public void Query4()
    {
        var query = Db.Query<DamengMyTopic>().Where(o => o.Id != null);
        Assert.Equal("SELECT COUNT(*) FROM \"MY_TOPIC\" WHERE \"ID\" IS NOT NULL", query.ToCountStatement().Text);
        Assert.Equal("SELECT rt.* FROM(SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\", ROWNUM AS \"__rownum__\" FROM \"MY_TOPIC\" WHERE \"ID\" IS NOT NULL AND ROWNUM < 41) rt WHERE rt.\"__rownum__\" > 20",
            query.Skip(20).Take(20).ToSql());
    }

    [Fact, Upstream("dameng/DamengQueryTest.java#query5", Deviation = LiveOnly)]
    public void Query5()
    {
        var query = Db.Query<DamengMyTopic>().Where(o => o.Id != null).Where(o => o.Id != "xxasdasda").OrderBy(o => o.Id)
            .Select(s => new DamengMyTopicDTO { Title = s.Id });
        SqlOnly.AssertStatement(query.ToCountStatement(), "SELECT COUNT(*) FROM \"MY_TOPIC\" WHERE \"ID\" IS NOT NULL AND \"ID\" <> ?", "xxasdasda(String)");
        SqlOnly.AssertStatement(query.Skip(20).Take(20).ToStatement(),
            "SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT t.\"ID\" AS \"TITLE\" FROM \"MY_TOPIC\" t WHERE t.\"ID\" IS NOT NULL AND t.\"ID\" <> ? ORDER BY t.\"ID\" ASC) rt WHERE ROWNUM < 41) rt1 WHERE rt1.\"__rownum__\" > 20",
            "xxasdasda(String)");
    }

    [Fact, Upstream("dameng/DamengQueryTest.java#query6", Deviation = LiveOnly)]
    public void Query6()
    {
        Assert.Equal(TopicSql, Db.Query<DamengMyTopic>().ToSql());
        Assert.Equal("SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\" FROM \"MY_TOPIC\" WHERE \"ID\" IS NOT NULL ORDER BY \"STARS\" ASC) rt WHERE ROWNUM < 41) rt1 WHERE rt1.\"__rownum__\" > 20",
            Db.Query<DamengMyTopic>().Where(o => o.Id != null).OrderBy(o => o.Stars).Skip(20).Take(20).ToSql());
    }

    // 沿 MyTopics（标题等于本条 ID 的主题）展开成树
    [Fact, Upstream("dameng/DamengQueryTest.java#query7", Deviation = LiveOnly)]
    public void Query7() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(o => o.Id == "1").AsTreeCte().ToStatement(),
            "WITH \"as_tree_cte\" (\"cte_deep\",\"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\") AS ( (SELECT 0 AS \"cte_deep\",t1.\"ID\",t1.\"STARS\",t1.\"TITLE\",t1.\"CREATE_TIME\" FROM \"MY_TOPIC\" t1 WHERE t1.\"ID\" = ?)  UNION ALL  " +
            "(SELECT t2.\"cte_deep\" + 1 AS \"cte_deep\",t3.\"ID\",t3.\"STARS\",t3.\"TITLE\",t3.\"CREATE_TIME\" FROM \"as_tree_cte\" t2 INNER JOIN \"MY_TOPIC\" t3 ON t3.\"TITLE\" = t2.\"ID\") ) " +
            "SELECT t.\"ID\",t.\"STARS\",t.\"TITLE\",t.\"CREATE_TIME\",t.\"cte_deep\" FROM \"as_tree_cte\" t", "1(String)");

    // 分组后分页：计数包一层；有分组时 ROWNUM 写在外层
    [Fact, Upstream("dameng/DamengQueryTest.java#query16")]
    public void Query16()
    {
        var query = Db.Query<DamengMyTopic>().LeftJoin<DamengMyTopic>((d, d2) => d.Id == d2.Id)
            .GroupBy((_, d2) => d2.Title).Having(g => g.Count(x => x.Item1.Id) != -1L)
            .Select(g => new DamengMyTopic { Stars = (int)g.Count() });
        SqlOnly.AssertStatement(query.ToCountStatement(),
            "SELECT COUNT(*) FROM (SELECT COUNT(*) AS \"STARS\" FROM \"MY_TOPIC\" t LEFT JOIN \"MY_TOPIC\" t1 ON t.\"ID\" = t1.\"ID\" GROUP BY t1.\"TITLE\" HAVING COUNT(t.\"ID\") <> ?) t2", "-1(Long)");
        SqlOnly.AssertStatement(query.Skip(1).Take(1).ToStatement(),
            "SELECT rt1.* FROM (SELECT rt.*, ROWNUM AS \"__rownum__\" FROM (SELECT COUNT(*) AS \"STARS\" FROM \"MY_TOPIC\" t LEFT JOIN \"MY_TOPIC\" t1 ON t.\"ID\" = t1.\"ID\" GROUP BY t1.\"TITLE\" HAVING COUNT(t.\"ID\") <> ?) rt WHERE ROWNUM < 3) rt1 WHERE rt1.\"__rownum__\" > 1", "-1(Long)");
    }

    [Fact, Upstream("dameng/DamengQueryTest.java#query17", Deviation = LiveOnly)]
    public void Query17() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(d => d.Title!.Contains("123")).Skip(2).Take(10).ToStatement(),
            "SELECT rt.* FROM(SELECT \"ID\",\"STARS\",\"TITLE\",\"CREATE_TIME\", ROWNUM AS \"__rownum__\" FROM \"MY_TOPIC\" WHERE \"TITLE\" LIKE ? AND ROWNUM < 13) rt WHERE rt.\"__rownum__\" > 2", "%123%(String)");

    // 日期格式：格式记号转成 TO_CHAR 的记号，其余文字用双引号括起来（Java 还在实库上比对格式化结果）
    [Theory]
    [InlineData("yyyy-MM-01", "'YYYY-MM-\"01\"'"), Upstream("dameng/DamengQueryTest.java#queryFormat1")]
    [InlineData("yyyy年MM-01", "'YYYY\"年\"MM-\"01\"'"), Upstream("dameng/DamengQueryTest.java#queryFormat2")]
    [InlineData("yyyy年MM-01 HH时mm分ss秒", "'YYYY\"年\"MM-\"01\" HH24\"时\"MI\"分\"SS\"秒\"'"), Upstream("dameng/DamengQueryTest.java#queryFormat3")]
    [InlineData("yyyy年MM-01 HH:mm分ss秒", "'YYYY\"年\"MM-\"01\" HH24:MI\"分\"SS\"秒\"'"), Upstream("dameng/DamengQueryTest.java#queryFormat4")]
    public void QueryFormat(string format, string sqlFormat) =>
        Assert.Equal("SELECT t.\"CREATE_TIME\" AS \"VALUE1\",TO_CHAR(t.\"CREATE_TIME\"," + sqlFormat + ") AS \"VALUE2\" FROM \"MY_TOPIC\" t",
            Db.Query<DamengMyTopic>().Select(d => new Draft<DateTime?, string>(d.CreateTime, d.CreateTime!.Value.ToString(format))).ToSql());

    // 指定按分组合并的导航：Any 写成 LEFT JOIN 分组子查询里的 CASE 值
    [Fact, Upstream("dameng/DamengQueryTest.java#subQueryTest1")]
    public void SubQueryTest1() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().GroupJoin(s => s.MyTopics).Where(d => d.MyTopics!.Any()).ToStatement(),
            "SELECT t.\"ID\",t.\"STARS\",t.\"TITLE\",t.\"CREATE_TIME\" FROM \"MY_TOPIC\" t LEFT JOIN (SELECT t1.\"TITLE\" AS \"__group_key1__\",(CASE WHEN (COUNT(*) > 0) THEN ? ELSE ? END) AS \"__any2__\" FROM \"MY_TOPIC\" t1 GROUP BY t1.\"TITLE\") t2 ON t2.\"__group_key1__\" = t.\"ID\" WHERE NVL(t2.\"__any2__\",?) = ?",
            "true(Boolean),false(Boolean),false(Boolean),true(Boolean)");

    // 日期比较；两个日期相差的秒数：终点转成带时区的时间戳再减起点（Java 的 DamengDateTimeDuration2SQLFunction）
    [Fact, Upstream("dameng/DamengQueryTest.java#test1", Deviation = LiveOnly)]
    public void Test1()
    {
        var now = new DateTime(2024, 1, 2, 3, 4, 5);
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(d => d.CreateTime >= now).ToStatement(), TopicSql + " WHERE \"CREATE_TIME\" >= ?", "2024-01-02T03:04:05(LocalDateTime)");
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(x => (long)(x.CreateTime!.Value - now).TotalSeconds > 100L).ToStatement(),
            TopicSql + " WHERE (EXTRACT(DAY FROM (CAST(\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-?))*86400+EXTRACT(HOUR FROM (CAST(\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-?))*3600+EXTRACT(MINUTE FROM (CAST(\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-?))*60+EXTRACT(SECOND FROM (CAST(\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-?))) > ?",
            "2024-01-02T03:04:05(LocalDateTime),2024-01-02T03:04:05(LocalDateTime),2024-01-02T03:04:05(LocalDateTime),2024-01-02T03:04:05(LocalDateTime),100(Long)");
    }

    [Fact, Upstream("dameng/DamengQueryTest.java#test111", Deviation = LiveOnly)]
    public void Test111() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Select(d => new Draft<string>(d.CreateTime!.Value.ToString("yyyy-MM") + "-01")).ToStatement(),
            "SELECT TO_CHAR(t.\"CREATE_TIME\",'YYYY-MM') || ? AS \"VALUE1\" FROM \"MY_TOPIC\" t", "-01(String)");

    // 储蓄卡全部以 33123 开头（没有储蓄卡也算）：All 写成 NOT EXISTS(… NOT 条件)，子查询只取 1 行（ROWNUM < 2）
    [Fact, Upstream("dameng/DamengQueryTest.java#testAll", Deviation = IncludeLiveOnly)]
    public void TestAll() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().Include(u => u.BankCards)
                .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToStatement(),
            UsersSql + " WHERE NOT (EXISTS(SELECT 1 FROM \"t_bank_card\" t1 WHERE t1.\"UID\" = t.\"ID\" AND t1.\"TYPE\" = ? AND (NOT (t1.\"CODE\" LIKE (TO_CHAR(?)||'%'))) AND ROWNUM < 2))",
            "储蓄卡(String),33123(String)");

    [Fact, Upstream("dameng/DamengQueryTest.java#testAll1", Deviation = IncludeLiveOnly)]
    public void TestAll1() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().Include(u => u.BankCards)
                .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => (c.Code ?? "").LikeStartsWith("33123"))).ToStatement(),
            UsersSql + " WHERE NOT (EXISTS(SELECT 1 FROM \"t_bank_card\" t1 WHERE t1.\"UID\" = t.\"ID\" AND t1.\"TYPE\" = ? AND (NOT (NVL(t1.\"CODE\",?) LIKE (TO_CHAR(?)||'%'))) AND ROWNUM < 2))",
            "储蓄卡(String),(String),33123(String)");

    [Fact, Upstream("dameng/DamengQueryTest.java#testAll2", Deviation = IncludeLiveOnly)]
    public void TestAll2() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().Include(u => u.BankCards)
                .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToStatement(),
            UsersSql + " WHERE NOT (EXISTS(SELECT 1 FROM \"t_bank_card\" t1 WHERE t1.\"UID\" = t.\"ID\" AND t1.\"TYPE\" = ? AND (NOT (t1.\"CODE\" LIKE (TO_CHAR(?)||'%') AND t1.\"CODE\" LIKE (TO_CHAR(?)||'%'))) AND ROWNUM < 2))",
            "储蓄卡(String),33123(String),45678(String)");

    [Fact, Upstream("dameng/DamengQueryTest.java#testAllGroupJoin", Deviation = IncludeLiveOnly)]
    public void TestAllGroupJoin() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().GroupJoinAll().Include(u => u.BankCards)
                .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123"))).ToStatement(),
            UsersSql + " LEFT JOIN (SELECT t1.\"UID\" AS \"__group_key1__\",(CASE WHEN (COUNT(?) <= 0) THEN ? ELSE ? END) AS \"__none2__\" FROM \"t_bank_card\" t1 WHERE t1.\"TYPE\" = ? AND (NOT (t1.\"CODE\" LIKE (TO_CHAR(?)||'%'))) GROUP BY t1.\"UID\") t2 ON t2.\"__group_key1__\" = t.\"ID\" WHERE NVL(t2.\"__none2__\",?) = ?",
            "1(Integer),true(Boolean),false(Boolean),储蓄卡(String),33123(String),true(Boolean),true(Boolean)");

    [Fact, Upstream("dameng/DamengQueryTest.java#testAllGroupJoin2", Deviation = IncludeLiveOnly)]
    public void TestAllGroupJoin2() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().GroupJoinAll().Include(u => u.BankCards)
                .Where(u => u.BankCards!.Where(c => c.Type == "储蓄卡").All(c => c.Code.LikeStartsWith("33123") && c.Code.LikeStartsWith("45678"))).ToStatement(),
            UsersSql + " LEFT JOIN (SELECT t1.\"UID\" AS \"__group_key1__\",(CASE WHEN (COUNT(?) <= 0) THEN ? ELSE ? END) AS \"__none2__\" FROM \"t_bank_card\" t1 WHERE t1.\"TYPE\" = ? AND (NOT (t1.\"CODE\" LIKE (TO_CHAR(?)||'%') AND t1.\"CODE\" LIKE (TO_CHAR(?)||'%'))) GROUP BY t1.\"UID\") t2 ON t2.\"__group_key1__\" = t.\"ID\" WHERE NVL(t2.\"__none2__\",?) = ?",
            "1(Integer),true(Boolean),false(Boolean),储蓄卡(String),33123(String),45678(String),true(Boolean),true(Boolean)");

    private const string BooleanValueSql = "SELECT (CASE WHEN (EXISTS(SELECT 1 FROM \"t_bank_card\" t2 WHERE t2.\"UID\" = t.\"ID\" AND ROWNUM < 2)) THEN ? ELSE ? END) AS \"VALUE1\",(CASE WHEN (NOT (EXISTS(SELECT 1 FROM \"t_bank_card\" t3 WHERE t3.\"UID\" = t.\"ID\" AND ROWNUM < 2))) THEN ? ELSE ? END) AS \"VALUE2\" FROM \"t_sys_user\" t WHERE EXISTS(SELECT 1 FROM \"t_bank_card\" t1 WHERE t1.\"UID\" = t.\"ID\" AND ROWNUM < 2)";

    // Java 的 anyValue() / noneValue() 与 expression().valueOf(() -> any())：C# 投影里的 Any() / !Any()；没有布尔类型，条件当值写成 CASE
    [Fact, Upstream("dameng/DamengQueryTest.java#testBooleanValue"), Upstream("dameng/DamengQueryTest.java#testBooleanValue2")]
    public void TestBooleanValue() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().Where(u => u.BankCards!.Any()).Select(u => new Draft<bool, bool>(u.BankCards!.Any(), !u.BankCards!.Any())).ToStatement(),
            BooleanValueSql, "true(Boolean),false(Boolean),true(Boolean),false(Boolean)");

    // 时间加减后再相差：天、秒、分（Java 断言 -1、-2、-3 与 -1443）。两个常量之间的相差 C# 在本地就算出来了，不进 SQL
    [Fact, Upstream("dameng/DamengQueryTest.java#testDraft9", Deviation = "Java 在达梦实库上断言结果；C# 只核对列与列之间的加减、相差渲染出的 SQL（两个常量之间的 C# 在本地算好，不写进 SQL），实库验证在 3.1 冒烟里做")]
    public void TestDraft9()
    {
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().WhereById("123456zz9")
                .Select(o => new Draft<int, int, int>((o.CreateTime!.Value - o.CreateTime!.Value.AddDays(1)).Days,
                    (int)(o.CreateTime!.Value - o.CreateTime!.Value.AddSeconds(2)).TotalSeconds,
                    (int)(o.CreateTime!.Value - o.CreateTime!.Value.AddDays(1).AddMinutes(3)).TotalMinutes)).ToStatement(),
            "SELECT EXTRACT(DAY FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-(t.\"CREATE_TIME\"+?))) AS \"VALUE1\"," +
            "(EXTRACT(DAY FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-(t.\"CREATE_TIME\"+(?)/86400.0)))*86400+EXTRACT(HOUR FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-(t.\"CREATE_TIME\"+(?)/86400.0)))*3600+EXTRACT(MINUTE FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-(t.\"CREATE_TIME\"+(?)/86400.0)))*60+EXTRACT(SECOND FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-(t.\"CREATE_TIME\"+(?)/86400.0)))) AS \"VALUE2\"," +
            "(EXTRACT(DAY FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-((t.\"CREATE_TIME\"+?)+(?)/1440.0)))*1440+EXTRACT(HOUR FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-((t.\"CREATE_TIME\"+?)+(?)/1440.0)))*60+EXTRACT(MINUTE FROM (CAST(t.\"CREATE_TIME\" AS TIMESTAMP WITH TIME ZONE)-((t.\"CREATE_TIME\"+?)+(?)/1440.0)))) AS \"VALUE3\"" +
            " FROM \"MY_TOPIC\" t WHERE t.\"ID\" = ?");
    }

    // 分组后投影成实体再分页：有分组时 ROWNUM 写在外层
    [Fact, Upstream("dameng/DamengQueryTest.java#testEasyQuery", Deviation = LiveOnly)]
    public void TestEasyQuery() =>
        Assert.Equal("SELECT rt.* FROM (SELECT t.\"TITLE\" AS \"TITLE\" FROM \"MY_TOPIC\" t WHERE t.\"ID\" IS NULL GROUP BY t.\"TITLE\") rt WHERE ROWNUM < 10",
            Db.Query<DamengMyTopic>().Where(d => d.Id == null).GroupBy(d => d.Title).Select(g => new DamengMyTopic { Title = g.Key }).Take(9).ToSql());

    // 不排序的字符串聚合不写 WITHIN GROUP（与 Oracle 不同）
    [Fact, Upstream("dameng/DamengQueryTest.java#testJoin1", Deviation = LiveOnly)]
    public void TestJoin1() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().GroupBy(o => o.Title).Select(g => new Draft<string?, string>(g.Key, g.StringJoin(x => x.Id, ","))).ToStatement(),
            "SELECT t.\"TITLE\" AS \"VALUE1\",LISTAGG(TO_CHAR(t.\"ID\"), ?) AS \"VALUE2\" FROM \"MY_TOPIC\" t GROUP BY t.\"TITLE\"", ",(String)");

    // 组内排序写成 WITHIN GROUP(ORDER BY …)
    [Fact, Upstream("dameng/DamengQueryTest.java#testJoining")]
    public void TestJoining() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().GroupJoinAll()
                .Where(u => string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").OrderBy(c => c.OpenTime).Select(c => c.Type)) == "123").ToStatement(),
            UsersSql + " LEFT JOIN (SELECT t1.\"UID\" AS \"__group_key1__\",LISTAGG(TO_CHAR(t1.\"TYPE\"), ?) WITHIN GROUP(ORDER BY t1.\"OPEN_TIME\" ASC) AS \"__joining2__\" FROM \"t_bank_card\" t1 WHERE t1.\"TYPE\" = ? GROUP BY t1.\"UID\") t2 ON t2.\"__group_key1__\" = t.\"ID\" WHERE t2.\"__joining2__\" = ?",
            ",(String),储蓄卡(String),123(String)");

    [Fact, Upstream("dameng/DamengQueryTest.java#testJoining1")]
    public void TestJoining1() =>
        SqlOnly.AssertStatement(Db.Query<SysUser>().GroupJoinAll()
                .Where(u => string.Join(",", u.BankCards!.Where(c => c.Type == "储蓄卡").Select(c => c.Type)) == "123").ToStatement(),
            UsersSql + " LEFT JOIN (SELECT t1.\"UID\" AS \"__group_key1__\",LISTAGG(TO_CHAR(t1.\"TYPE\"), ?) AS \"__joining2__\" FROM \"t_bank_card\" t1 WHERE t1.\"TYPE\" = ? GROUP BY t1.\"UID\") t2 ON t2.\"__group_key1__\" = t.\"ID\" WHERE t2.\"__joining2__\" = ?",
            ",(String),储蓄卡(String),123(String)");

    // 几列取最大 / 最小：与 Oracle 相同，每个值 SELECT … FROM DUAL 后 UNION ALL
    [Fact, Upstream("dameng/DamengQueryTest.java#testMaxColumns1")]
    public void TestMaxColumns1() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(d => Sql.MaxColumns(d.Id, d.Title, d.Title ?? d.Id) == "123").ToStatement(),
            TopicSql + " WHERE (SELECT MAX(__mmc.__col__) FROM (SELECT \"ID\" AS __col__ FROM DUAL UNION ALL SELECT \"TITLE\" AS __col__ FROM DUAL UNION ALL SELECT NVL(\"TITLE\",\"ID\") AS __col__ FROM DUAL) __mmc) = ?",
            "123(String)");

    [Fact, Upstream("dameng/DamengQueryTest.java#testMaxColumns2")]
    public void TestMaxColumns2() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(d => Sql.MaxColumns(d.Id, d.Title ?? d.Id) == "123").ToStatement(),
            TopicSql + " WHERE (SELECT MAX(__mmc.__col__) FROM (SELECT \"ID\" AS __col__ FROM DUAL UNION ALL SELECT NVL(\"TITLE\",\"ID\") AS __col__ FROM DUAL) __mmc) = ?",
            "123(String)");

    // Java 的 asStr() 只改类型、不写 SQL：C# 用 (string)(object) 表达
    [Fact, Upstream("dameng/DamengQueryTest.java#testMaxColumns3")]
    public void TestMaxColumns3() =>
        SqlOnly.AssertStatement(Db.Query<DamengMyTopic>().Where(d => Sql.MinColumns(d.Id, (string?)(object?)d.Stars) == "123").ToStatement(),
            TopicSql + " WHERE (SELECT MIN(__mmc.__col__) FROM (SELECT \"ID\" AS __col__ FROM DUAL UNION ALL SELECT \"STARS\" AS __col__ FROM DUAL) __mmc) = ?",
            "123(String)");

    // DTO 的列名按命名约定是 IS_DELETE / ROW_NUMBER，与实体的显式列名不同：投影写别名（Java 还在实库上读回四个值）
    [Fact, Upstream("dameng/DamengQueryTest.java#testQuery")]
    public void TestQuery() =>
        Assert.Equal("SELECT t.\"ID\",t.\"NAME\",t.\"ISDELETE\" AS \"IS_DELETE\",t.\"ROWNUMBER\" AS \"ROW_NUMBER\" FROM \"MY_CUSTOM\" t",
            Db.Query<DamengNameCustom>().Select(o => new DamengNameCustomDTO { Id = o.Id, Name = o.Name, IsDelete = o.IsDelete, RowNumber = o.RowNumber }).ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#testQueryAlias")]
    public void TestQueryAlias() =>
        Assert.Equal("SELECT t.\"ID\",t.\"NAME\",t.\"ROWNUMBER\" AS \"ROW_NUMBER\",t.\"ISDELETE\" AS \"IS_DELETE\" FROM \"MY_CUSTOM\" t",
            Db.Query<DamengNameCustom>().Select<DamengNameCustomDTO>().ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#testQueryAlias2")]
    public void TestQueryAlias2() =>
        Assert.Equal("SELECT t.\"ID\",t.\"NAME\",t.\"ROWNUMBER\",t.\"ISDELETE\" FROM \"MY_CUSTOM\" t", Db.Query<DamengNameCustom>().Select<DamengNameCustomDTO2>().ToSql());

    [Fact, Upstream("dameng/DamengQueryTest.java#testQueryAlias3")]
    public void TestQueryAlias3() =>
        Assert.Equal("SELECT t.\"ID\",t.\"NAME\",t.\"ROWNUMBER\",t.\"ISDELETE\" FROM \"MY_CUSTOM\" t",
            Db.Query<DamengNameCustom>().Select(o => new DamengNameCustomDTO2 { Id = o.Id, Name = o.Name, RowNumber = o.RowNumber, IsDelete = o.IsDelete }).ToSql());

    // 按格式化后的小时分组计数
    [Fact, Upstream("dameng/DamengQueryTest.java#testaa", Deviation = LiveOnly)]
    public void Testaa() =>
        Assert.Equal("SELECT TO_CHAR(t.\"CREATE_TIME\",'YYYYMMDDHH24') AS \"HOUR_STR\",COUNT(*) AS \"COUNT\" FROM \"MY_TOPIC\" t GROUP BY TO_CHAR(t.\"CREATE_TIME\",'YYYYMMDDHH24')",
            Db.Query<DamengMyTopic>().GroupBy(d => d.CreateTime!.Value.ToString("yyyyMMddHH")).Select(g => new DamengHourCount { HourStr = g.Key, Count = g.Count() }).ToSql());
}
