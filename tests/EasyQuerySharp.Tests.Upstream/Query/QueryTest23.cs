using EasyQuerySharp.Tests.Upstream.Doc.Entities;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：QueryTest23.java（导航里取第几行、多对多的分组合并、字符串格式化、数学函数等）。
/// Java 的 element(n) / first() 对应 C# 的 ElementAt(n) / First()。
/// </summary>
public sealed class QueryTest23(MySqlFixture fixture) : MySqlTest(fixture)
{
    private const string BlogColumns = "SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ?";

    // 取第几行：ROW_NUMBER 派生表按外键分区，没有写排序时按主键升序（Java 的 partitionOrder = KEY_ASC）
    [Fact, Upstream("QueryTest23.java#testElement3")]
    public async Task TestElement3()
    {
        await Db.Query<DocUser>().OrderBy(user => user.Age).ToListAsync(Ct);
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>().GroupJoin(x => x.BankCards)
            .Select(user => new Draft<string?, string?>(user.BankCards!.Where(o => o.Type == "123").Max(x => x.Code), user.BankCards!.Where(o => o.Type == "123").ElementAt(0).Type))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`__max2__` AS `value1`,t5.`type` AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX(t1.`code`) AS `__max2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`id`,t3.`uid`,t3.`code`,t3.`type`,t3.`bank_id`,(ROW_NUMBER() OVER (PARTITION BY t3.`uid` ORDER BY t3.`id` ASC)) AS `__row__` FROM `doc_bank_card` t3 WHERE t3.`type` = ?) t5 ON (t5.`uid` = t.`id` AND t5.`__row__` = ?)");
        listen.AssertParams("123(String),123(String),1(Integer)");
    }

    // 多对多的分组合并：目标表 INNER JOIN 中间表，按中间表的列分组
    [Fact, Upstream("QueryTest23.java#manyJoinMany2Many")]
    public async Task ManyJoinMany2Many()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().GroupJoin(x => x.SchoolTeachers).Where(s => s.SchoolTeachers!.Where(x => x.Name!.Contains("小明")).LongCount() == 1L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t LEFT JOIN (SELECT t2.`class_id` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `school_teacher` t1 INNER JOIN `school_class_teacher` t2 ON t1.`id` = t2.`teacher_id` WHERE t1.`name` LIKE ? GROUP BY t2.`class_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE IFNULL(t4.`__count2__`,0) = ?");
        listen.AssertParams("1(Integer),%小明%(String),1(Long)");
    }

    [Fact, Upstream("QueryTest23.java#fetcherOrder")]
    public async Task FetcherOrder() =>
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(b => new Draft<decimal?>(Sql.Raw<decimal?>("({0}+{1})/{2}", b.Score, b.Star, b.Order))).ToListAsync(Ct));

    [Fact, Upstream("QueryTest23.java#testAddMulty"), Upstream("QueryTest23.java#testAddMulty2")]
    public async Task TestAddMulty()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Select(b => new Draft<decimal?>((b.Order + b.Star) / b.Score)).ToListAsync(Ct);
        listen.AssertSql("SELECT ((t.`order` + t.`star`) / t.`score`) AS `value1` FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    private const string CardUserJoin = " FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid` WHERE (t.`id` = ? OR t.`id` IS NOT NULL)";

    // 去重后分页：多列时计数包一层，单列时 COUNT(DISTINCT 列)
    [Fact, Upstream("QueryTest23.java#testPageDistinct"), Upstream("QueryTest23.java#testPageDistinct2"), Upstream("QueryTest23.java#testPageDistinct3")]
    public async Task TestPageDistinct()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().Where(c => c.Id == "123" || c.Id != null).Select(c => new Draft<string?, string?>(c.Code, c.User!.Name)).Distinct().ToPageAsync(1, 2, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM (SELECT DISTINCT t.`code` AS `value1`,t1.`name` AS `value2`" + CardUserJoin + ") t2");
        listen.AssertParams(0, "123(String)");
        await Db.Query<DocBankCard>().Where(c => c.Id == "123" || c.Id != null).Select(c => new Draft<string?, string?>(c.Code, c.User!.Name)).Distinct().ToListAsync(Ct);
        listen.AssertSql("SELECT DISTINCT t.`code` AS `value1`,t1.`name` AS `value2`" + CardUserJoin);
        await Db.Query<DocBankCard>().Where(c => c.Id == "123" || c.Id != null).Select(c => new Draft<string?>(c.User!.Name)).Distinct().ToPageAsync(1, 2, Ct);
        listen.AssertSql("SELECT COUNT(DISTINCT t1.`name`)" + CardUserJoin);
    }

    // 外层 lambda 的列可以写进集合导航的条件里
    [Fact, Upstream("QueryTest23.java#anyTest")]
    public async Task AnyTest()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>()
            .Where(user => user.Phone == "123" && user.UserBooks!.Any(x => user.Name == x.Name && user.Phone == "123") && user.Phone == "123")
            .Select(user => new Draft<string?>(user.BankCards!.Where(x => user.Name == x.Code && user.Phone == "123" && x.Code == "123").Max(x => x.Type)))
            .ToListAsync(Ct);
        listen.AssertSql("SELECT (SELECT MAX(t2.`type`) FROM `doc_bank_card` t2 WHERE t2.`uid` = t.`id` AND t.`name` = t2.`code` AND t.`phone` = ? AND t2.`code` = ?) AS `value1` FROM `doc_user` t WHERE t.`phone` = ? AND EXISTS(SELECT 1 FROM `doc_user_book` t1 WHERE t1.`uid` = t.`id` AND t.`name` = t1.`name` AND t.`phone` = ? LIMIT 1) AND t.`phone` = ?");
        listen.AssertParams("123(String),123(String),123(String),123(String),123(String)");
    }

    [Fact, Upstream("QueryTest23.java#testaaa")]
    public async Task Testaaa() =>
        Assert.NotNull(await Db.Query<Topic>().Where(o => o.Id.In(Db.Query<BlogEntity>().Where(x => o.Id == "123").Select(x => x.Id))).ToListAsync(Ct));

    [Fact, Upstream("QueryTest23.java#testJoin"), Upstream("QueryTest23.java#testJoin1"), Upstream("QueryTest23.java#testJoin2"), Upstream("QueryTest23.java#testJoin3")]
    public async Task TestJoin()
    {
        Assert.NotNull(await Db.Query<DocBankCard>().Where(c => c.User!.Name == "小明").ToListAsync(Ct));
        Assert.NotNull(await Db.Query<DocBankCard>().Where(c => c.User!.Phone!.Contains("1234") && c.Bank!.Name == "工商银行").ToListAsync(Ct));
        Assert.NotNull(await Db.Query<DocBankCard>().Where(c => c.User!.Name == "小明" && c.Bank!.Name == "工商银行").OrderBy(c => c.Code)
            .Select(c => new Draft<string?, string?, string?>(c.User!.Name, c.Bank!.Name, c.Code)).ToListAsync(Ct));
        Assert.NotNull(await Db.Query<DocBankCard>().Where(c => c.Bank!.Name == "工商银行").OrderBy(c => c.Code).Select(c => new Draft<string?, string?>(c.Bank!.Name, c.Code)).ToListAsync(Ct));
        Assert.NotNull(await Db.Query<DocUser>().Where(user => user.BankCards!.Where(card => card.Bank!.Name == "工商银行").LongCount() > 2L && !user.BankCards!.Any(card => card.Bank!.Name == "建设银行")).ToListAsync(Ct));
        Assert.NotNull(await Db.Query<DocUser>().GroupJoin(u => u.BankCards)
            .Where(user => user.BankCards!.Where(card => card.Bank!.Name == "工商银行").LongCount() > 2L && !user.BankCards!.Any(card => card.Bank!.Name == "建设银行"))
            .OrderByIf(false, user => user.Name).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest23.java#testJoin4", NotApplicable = "Java 替换实体映射规则的服务（PropertyFirstEntityMappingRule），C# 的列名映射由 NameConversion 决定，没有对应的服务替换")]
    public void TestJoin4() { }

    // Java 的 stringFormat：C# 的 string.Format / 插值字符串，{n} 可以重复
    [Fact, Upstream("QueryTest23.java#testConcat"), Upstream("QueryTest23.java#testConcat3")]
    public async Task TestConcat()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => string.Format("你好:{0}我叫{1}你好吗?我今年{2}岁了,{0}你呢", b.Title, b.Star, 12) == "比较一下").ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " AND CONCAT(?,`title`,?,`star`,?,?,?,`title`,?) = ?");
        listen.AssertParams("false(Boolean),你好:(String),我叫(String),你好吗?我今年(String),12(Integer),岁了,(String),你呢(String),比较一下(String)");
        await Db.Query<BlogEntity>().Where(b => string.Format("你好:{0}我叫{1}你好吗?我今年{1}岁了", b.Title, b.Star) == "比较一下").ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " AND CONCAT(?,`title`,?,`star`,?,`star`,?) = ?");
        listen.AssertParams("false(Boolean),你好:(String),我叫(String),你好吗?我今年(String),岁了(String),比较一下(String)");
    }

    // 格式串里的占位符超出参数个数：翻译时报错
    [Fact, Upstream("QueryTest23.java#testConcat2")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2241", Justification = "故意写错占位符，验证翻译时报错")]
    public async Task TestConcat2() =>
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<BlogEntity>().Where(b => string.Format("你好:{0}我叫{1}你好吗?我今年{2}岁了,{3}你呢", b.Title, b.Star, 12) == "比较一下").ToListAsync(Ct));

    [Fact, Upstream("QueryTest23.java#datetimeformat"), Upstream("QueryTest23.java#formatMySQL")]
    public async Task DateTimeFormat()
    {
        const string format1 = "yyyy年MM-01 HH:mm分ss秒";
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>().Select(d => new Draft<DateTime?, string>(d.CreateTime, d.CreateTime!.Value.ToString(format1))).ToListAsync(Ct);
        Assert.All(list, item => Assert.Equal(item.Value1!.Value.ToString(format1, System.Globalization.CultureInfo.InvariantCulture), item.Value2));
        listen.AssertSql("SELECT t.`create_time` AS `value1`,DATE_FORMAT(t.`create_time`,'%Y年%m-01 %H:%i分%s秒') AS `value2` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    // Java 的 orderBy(sql("{0} ASC")) / caseWhen：C# 的 OrderByRaw / 三元运算
    [Fact, Upstream("QueryTest23.java#testOrderSQL"), Upstream("QueryTest23.java#testOrderSQL1")]
    public async Task TestOrderSQL()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().OrderByRaw(b => Sql.Raw<object>("{0} ASC", b.Order)).ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " ORDER BY `order` ASC");
        await Db.Query<BlogEntity>().OrderBy(b => b.Title == "1" ? 1 : b.Title == "2" ? 2 : 3).ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " ORDER BY (CASE WHEN `title` = ? THEN ? WHEN `title` = ? THEN ? ELSE ? END) ASC");
        listen.AssertParams("false(Boolean),1(String),1(Integer),2(String),2(Integer),3(Integer)");
    }

    [Fact, Upstream("QueryTest23.java#testMany2Many")]
    public async Task TestMany2Many()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<UserAccount>().Where(uc => uc.Books!.Any(ub => ub.Name == "JAVA开发")).Select(uc => new Draft<string?, long>(uc.Name, uc.Books!.LongCount())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,(SELECT COUNT(*) FROM `t_user_book` t2 WHERE t2.`uid` = t.`uid`) AS `value2` FROM `t_user_account` t WHERE EXISTS(SELECT 1 FROM `t_user_book` t1 WHERE t1.`uid` = t.`uid` AND t1.`name` = ? LIMIT 1)");
        listen.AssertParams("JAVA开发(String)");
    }

    // Java 的 GROUP_JOIN_NOT_ALLOW_AUTO_MERGE 只影响自动合并，这条查询两种写法生成同一条 SQL
    [Fact, Upstream("QueryTest23.java#testMany2Many2"), Upstream("QueryTest23.java#testMany2Many2_1")]
    public async Task TestMany2Many2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<UserAccount>().GroupJoin(uc => uc.Books).Where(uc => uc.Books!.Any(ub => ub.Name == "JAVA开发"))
            .Select(uc => new Draft<string?, long>(uc.Name, uc.Books!.LongCount())).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`name` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2` FROM `t_user_account` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t1.`name` = ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,COUNT(*) AS `__count3__` FROM `t_user_book` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`uid` WHERE IFNULL(t2.`__any2__`,?) = ?");
        listen.AssertParams("JAVA开发(String),1(Integer),false(Boolean),true(Boolean)");
    }

    [Fact, Upstream("QueryTest23.java#testQueryConcat")]
    public async Task TestQueryConcat() =>
        Assert.NotNull(await Db.Query<BlogEntity>().Where(b => b.Title == string.Concat(b.Id, "*", b.Content) && b.Title == $"{b.Id}*{b.Content}").ToListAsync(Ct));

    [Fact, Upstream("QueryTest23.java#testMath"), Upstream("QueryTest23.java#testMathx1")]
    public async Task TestMath()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>()
            .Select(b => new Draft<decimal?, decimal, int, decimal, decimal, double>(b.Score, Math.Abs(b.Score!.Value), Math.Sign(b.Score!.Value), Math.Floor(b.Score!.Value), Math.Ceiling(b.Score!.Value), Math.Log((double)b.Score!.Value)))
            .ToListAsync(Ct);
        Assert.NotEmpty(list);
        listen.AssertSql("SELECT t.`score` AS `value1`,ABS(t.`score`) AS `value2`,SIGN(t.`score`) AS `value3`,FLOOR(t.`score`) AS `value4`,CEILING(t.`score`) AS `value5`,LOG(t.`score`) AS `value6` FROM `t_blog` t WHERE t.`deleted` = ?");
        await Db.Query<BlogEntity>().Select(b => new Draft<double>(Math.Sin((double)b.Score!.Value))).ToListAsync(Ct);
        listen.AssertSql("SELECT SIN(t.`score`) AS `value1` FROM `t_blog` t WHERE t.`deleted` = ?");
    }

    [Fact, Upstream("QueryTest23.java#testMathx2")]
    public async Task TestMathx2()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<BlogEntity>()
            .Select(b => new Draft<decimal?, decimal, int, decimal, double, double>(b.Score, Math.Round(b.Score!.Value), Math.Sign(b.Score!.Value), Math.Round(b.Score!.Value, 3), Math.Exp((double)b.Score!.Value), Math.Log10((double)b.Score!.Value)))
            .ToListAsync(Ct);
        Assert.All(list, item => Assert.Equal(Math.Round(item.Value1!.Value, 3, MidpointRounding.AwayFromZero), item.Value4));
        listen.AssertSql("SELECT t.`score` AS `value1`,ROUND(t.`score`) AS `value2`,SIGN(t.`score`) AS `value3`,ROUND(t.`score`,?) AS `value4`,EXP(t.`score`) AS `value5`,LOG10(t.`score`) AS `value6` FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("3(Integer),false(Boolean)");
    }

    [Fact, Upstream("QueryTest23.java#testaaa1")]
    public async Task Testaaa1()
    {
        Assert.NotNull(await Db.Query<BlogEntity>().GroupBy(b => b.Title).Having(g => g.Distinct().Count(s => s.Star) > 1L).Keys().ToListAsync(Ct));
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t) => t.Id == t.Id).GroupBy((b, t) => b.Title)
            .Select(g => new Draft<string?, long>(g.Key, g.Count(e => e.Item2.Title))).ToListAsync(Ct));
        Assert.NotNull(await Db.Query<BlogEntity>().LeftJoin<Topic>((b, t) => t.Id == t.Id).GroupBy((b, t) => b.Title)
            .Select(g => new Draft<string?, long>(g.Key, g.Distinct().Count(e => e.Item1.Star))).ToListAsync(Ct));
    }

    [Fact, Upstream("QueryTest23.java#testIndexOf")]
    public async Task TestIndexOf()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.IndexOf("30%") > -1).ToListAsync(Ct);
        listen.AssertSql(BlogColumns + " AND (LOCATE(?,`title`) - 1) > ?");
        listen.AssertParams("false(Boolean),30%(String),-1(Integer)");
    }

    [Fact, Upstream("QueryTest23.java#subquery", NotApplicable = "Java 取 JDBC 流式结果的 ResultSetMetaData；C# 的流式读取（ToAsyncEnumerable）不暴露驱动的元数据")]
    public void Subquery() { }

    // 子查询里引用外层的列做分组、HAVING、排序
    [Fact, Upstream("QueryTest23.java#testAdd", Deviation = "Java 在子查询的 WHERE 后多一个空格（WHERE  t.`id` = …），C# 一个空格")]
    public async Task TestAdd()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Id == "123" && Sql.Exists(Db.Query<Topic>().Where(t => b.Id == (t.Id ?? "1")).GroupBy(t => t.Id)
            .Having(g => Sql.Count(b.Id) > 1L).OrderBy(g => b.Id).Select(g => g.Key))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`id` = ? AND EXISTS(SELECT 1 FROM `t_topic` t1 WHERE t.`id` = IFNULL(t1.`id`,?) GROUP BY t1.`id` HAVING COUNT(t.`id`) > ? ORDER BY t.`id` ASC)");
        listen.AssertParams("false(Boolean),123(String),1(String),1(Long)");
    }
}
