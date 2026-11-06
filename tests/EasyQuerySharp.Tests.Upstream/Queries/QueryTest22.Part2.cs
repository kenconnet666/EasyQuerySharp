namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>QueryTest22 后半：分组合并里的导航条件、经对一导航 / 连接表的合并、嵌套聚合、字符串聚合，以及对照的相关子查询写法。</summary>
public sealed partial class QueryTest22
{
    private const string CountByPhone = UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `doc_bank_card` t1 LEFT JOIN `doc_user` t3 ON t3.`id` = t1.`uid` WHERE t3.`phone` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) = ?";

    // 聚合条件里的导航在分组子查询里 JOIN（编号排在派生表之后）；同一个查询对象可以反复执行
    [Fact, Upstream("QueryTest22.java#testManyGroup11"), Upstream("QueryTest22.java#testManyGroup12")]
    public void TestManyGroup11()
    {
        long? c = 1L;
        var query = Users().Where(user => user.BankCards!.Where(bc => bc.User!.Phone == "12345").LongCount() == c);
        SqlOnly.AssertStatement(query.ToStatement(), CountByPhone, "1(Integer),12345(String),1(Long)");
        SqlOnly.AssertStatement(query.ToStatement(), CountByPhone, "1(Integer),12345(String),1(Long)");
    }

    // 声明的 JOIN 先编号；ON 里用到的分组合并 JOIN 写在它前面
    [Fact, Upstream("QueryTest22.java#testManyGroup13")]
    public void TestManyGroup13() =>
        SqlOnly.AssertStatement(Users()
                .LeftJoin<BlogEntity>((user, b) => user.BankCards!.Where(bc => bc.User!.Phone == "12345").Max(x => x.Type) == b.Title)
                .Where((user, b) => b.Score > 0m && user.Name!.Contains("abc")).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t2.`uid` AS `__group_key1__`,MAX(t2.`type`) AS `__max2__` FROM `doc_bank_card` t2 LEFT JOIN `doc_user` t4 ON t4.`id` = t2.`uid` WHERE t4.`phone` = ? GROUP BY t2.`uid`) t3 ON t3.`__group_key1__` = t.`id` LEFT JOIN `t_blog` t1 ON t1.`deleted` = ? AND t3.`__max2__` = t1.`title` WHERE t1.`score` > ? AND t.`name` LIKE ?",
            "12345(String),false(Boolean),0(BigDecimal),%abc%(String)");

    // GroupJoin 的配置条件写在聚合条件之前
    [Fact, Upstream("QueryTest22.java#testManyGroup14")]
    public void TestManyGroup14()
    {
        long? c = 1L;
        SqlOnly.AssertStatement(Db.Query<DocUser>().GroupJoin(o => o.BankCards, q => q.Where(o => o.Code!.EndsWith("coder")))
                .Where(user => user.BankCards!.Where(bc => bc.User!.Phone == "12345").LongCount() == c).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `doc_bank_card` t1 LEFT JOIN `doc_user` t3 ON t3.`id` = t1.`uid` WHERE t1.`code` LIKE ? AND t3.`phone` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) = ?",
            "1(Integer),%coder(String),12345(String),1(Long)");
    }

    // 经对一导航到达的集合也能合并：先隐式 JOIN 用户，再按用户合并
    [Fact, Upstream("QueryTest22.java#testManyGroup15")]
    public void TestManyGroup15()
    {
        long? c = 1L;
        SqlOnly.AssertStatement(Db.Query<DocBankCard>().GroupJoin(o => o.User!.UserBooks)
                .Where(card => card.User!.UserBooks!.Where(bc => bc.Name == "12345").LongCount() == c).ToStatement(),
            "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id` FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid` LEFT JOIN (SELECT t2.`uid` AS `__group_key1__`,COUNT(?) AS `__count2__` FROM `doc_user_book` t2 WHERE t2.`name` = ? GROUP BY t2.`uid`) t3 ON t3.`__group_key1__` = t1.`id` WHERE IFNULL(t3.`__count2__`,0) = ?",
            "1(Integer),12345(String),1(Long)");
    }

    // 连接查询里合并第二张表上的集合导航
    [Fact, Upstream("QueryTest22.java#testManyGroup16")]
    public void TestManyGroup16() =>
        SqlOnly.AssertStatement(Db.Query<BlogEntity>().LeftJoin<DocUser>((b, user) => b.Id == user.Id).GroupJoin((b, user) => user.UserBooks)
                .Where((b, user) => user.UserBooks!.Where(x => x.Name!.Contains("123")).Max(x => x.Name) == "你好").ToStatement(),
            "SELECT t.`id`,t.`create_time`,t.`update_time`,t.`create_by`,t.`update_by`,t.`deleted`,t.`title`,t.`content`,t.`url`,t.`star`,t.`publish_time`,t.`score`,t.`status`,t.`order`,t.`is_top`,t.`top` FROM `t_blog` t LEFT JOIN `doc_user` t1 ON t.`id` = t1.`id` LEFT JOIN (SELECT t2.`uid` AS `__group_key1__`,MAX(t2.`name`) AS `__max2__` FROM `doc_user_book` t2 WHERE t2.`name` LIKE ? GROUP BY t2.`uid`) t3 ON t3.`__group_key1__` = t1.`id` WHERE t.`deleted` = ? AND t3.`__max2__` = ?",
            "%123%(String),false(Boolean),你好(String)");

    private const string BookAny = UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__` FROM `doc_user_book` t1 WHERE t1.`name` LIKE ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?";

    // Java 的 flatElement().name().like(…)：C# 的 Any(x => x.Name.Contains(…))；与 Where(…).Any() 是同一个聚合
    [Fact, Upstream("QueryTest22.java#testManyGroup17"), Upstream("QueryTest22.java#testManyGroup18")]
    public void TestManyGroup17()
    {
        var books = Db.Query<DocUser>().GroupJoin(u => u.UserBooks);
        SqlOnly.AssertStatement(books.Where(user => user.UserBooks!.Any(x => x.Name!.Contains("123"))).ToStatement(), BookAny,
            "1(Integer),%123%(String),false(Boolean),true(Boolean)");
        SqlOnly.AssertStatement(books.Where(user => user.UserBooks!.Any(x => x.Name!.Contains("123")) && user.UserBooks!.Where(x => x.Name!.Contains("123")).Any()).ToStatement(),
            BookAny + " AND IFNULL(t2.`__any2__`,?) = ?", "1(Integer),%123%(String),false(Boolean),true(Boolean),false(Boolean),true(Boolean)");
    }

    // Java 的 count().rangeOpenClosed(1L, 2L)：C# 的 RangeOpenClosed；Java 用例只执行不断言
    [Fact, Upstream("QueryTest22.java#testManyGroup29")]
    public void TestManyGroup29() =>
        Assert.Contains("(SELECT COUNT(*) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id`) > ?", Db.Query<DocUser>()
            .Where(user => user.BankCards!.LongCount().RangeOpenClosed(1L, 2L))
            .Select(user => new Draft<long, long>(user.BankCards!.Where(bc => bc.Type == "1").LongCount(), user.BankCards!.Where(bc => bc.Type == "2").LongCount()))
            .ToSql(), StringComparison.Ordinal);

    [Fact, Upstream("QueryTest22.java#testManyGroup39")]
    public void TestManyGroup39()
    {
        var counts = Db.Query<DocBankCard>().GroupBy(c => c.Uid)
            .Select(g => new Draft<string?, long, long, long>(g.Key, g.Count(), g.Where(x => x.Type == "1").Count(), g.Where(x => x.Type == "2").Count()));
        Assert.NotNull(Db.Query<DocUser>().LeftJoin(counts, (user, l2) => user.Id == l2.Value1).Where((user, l2) => l2.Value2.RangeOpenClosed(1L, 2L))
            .Select((user, l2) => new Draft<long, long>(l2.Value3, l2.Value4)).ToSql());
    }

    private const string ByJianshe = UserColumns + " ORDER BY ";

    // Java 用例没有断言（先 syncTable 建 doc_part 再执行）：按某类卡所属银行的计数排序，
    // Java 的 count(x -> x.bank().id()) 对应 Select(..).LongCount()，计非 NULL 的值
    [Fact, Upstream("QueryTest22.java#testManyOrder")]
    public void TestManyOrder() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Select(x => x.Bank!.Id).LongCount()).ToStatement(),
            ByJianshe + "(SELECT COUNT(t2.`id`) FROM `doc_bank_card` t1 INNER JOIN `doc_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`uid` = t.`id` AND t1.`type` = ?) ASC",
            "建设(String)");

    // 嵌套的导航聚合：外层 SUM 套内层 COUNT(DISTINCT)；不合并时 SUM 子查询外包 IFNULL(…,0)
    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct1")]
    public void TestOrderCountDistinct1() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Sum(x => x.Parts!.Select(p => p.Id).Distinct().LongCount())).ToStatement(),
            ByJianshe + "IFNULL((SELECT SUM((SELECT COUNT(DISTINCT t2.`id`) FROM `doc_part` t2 WHERE t2.`card_id` = t1.`id`)) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?),0) ASC",
            "建设(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct2")]
    public void TestOrderCountDistinct2() =>
        SqlOnly.AssertStatement(Users().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Sum(x => x.Parts!.Select(p => p.Id).Distinct().LongCount())).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM((SELECT COUNT(DISTINCT t3.`id`) FROM `doc_part` t3 WHERE t3.`card_id` = t1.`id`)) AS `__sum2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY IFNULL(t2.`__sum2__`,0) ASC",
            "建设(String)");

    // Java 的 distinct().count(x -> x.bank().id())：C# 的 Select(x => x.Bank.Id).Distinct().LongCount()
    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct3")]
    public void TestOrderCountDistinct3() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Select(x => x.Bank!.Id).Distinct().LongCount()).ToStatement(),
            ByJianshe + "(SELECT COUNT(DISTINCT t2.`id`) FROM `doc_bank_card` t1 INNER JOIN `doc_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t1.`uid` = t.`id` AND t1.`type` = ?) ASC",
            "建设(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct4")]
    public void TestOrderCountDistinct4() =>
        SqlOnly.AssertStatement(Users().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Select(x => x.Bank!.Id).Distinct().LongCount()).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(DISTINCT t3.`id`) AS `__count2__` FROM `doc_bank_card` t1 INNER JOIN `doc_bank` t3 ON t3.`id` = t1.`bank_id` WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY IFNULL(t2.`__count2__`,0) ASC",
            "建设(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct5")]
    public void TestOrderCountDistinct5() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Sum(x => x.Parts!.Select(p => p.Id).Distinct().LongCount())).ToStatement(),
            ByJianshe + "IFNULL((SELECT SUM(DISTINCT (SELECT COUNT(DISTINCT t2.`id`) FROM `doc_part` t2 WHERE t2.`card_id` = t1.`id`)) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?),0) ASC",
            "建设(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct6")]
    public void TestOrderCountDistinct6() =>
        SqlOnly.AssertStatement(Users().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Sum(x => x.Parts!.Select(p => p.Id).Distinct().LongCount())).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM(DISTINCT (SELECT COUNT(DISTINCT t3.`id`) FROM `doc_part` t3 WHERE t3.`card_id` = t1.`id`)) AS `__sum2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY IFNULL(t2.`__sum2__`,0) ASC",
            "建设(String)");

    // 全部集合导航合并成分组 JOIN：内层的计数也合并成分组 LEFT JOIN
    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct6_1")]
    public void TestOrderCountDistinct6_1() =>
        SqlOnly.AssertStatement(Users().GroupJoinAll().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Sum(x => x.Parts!.Select(p => p.Id).Distinct().LongCount())).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM(DISTINCT IFNULL(t4.`__count2__`,0)) AS `__sum2__` FROM `doc_bank_card` t1 LEFT JOIN (SELECT t3.`card_id` AS `__group_key1__`,COUNT(DISTINCT t3.`id`) AS `__count2__` FROM `doc_part` t3 GROUP BY t3.`card_id`) t4 ON t4.`__group_key1__` = t1.`id` WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY IFNULL(t2.`__sum2__`,0) ASC",
            "建设(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct7")]
    public void TestOrderCountDistinct7() =>
        SqlOnly.AssertStatement(Users().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Sum(x => x.Parts!.Select(p => p.Id ?? "x").Distinct().LongCount())).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM(DISTINCT (SELECT COUNT(DISTINCT IFNULL(t3.`id`,?)) FROM `doc_part` t3 WHERE t3.`card_id` = t1.`id`)) AS `__sum2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY IFNULL(t2.`__sum2__`,0) ASC",
            "x(String),建设(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct8")]
    public void TestOrderCountDistinct8() =>
        SqlOnly.AssertStatement(Users().OrderBy(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Average(x => long.Parse(x.Type!))).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,AVG(DISTINCT CAST(t1.`type` AS SIGNED)) AS `__avg2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` ORDER BY IFNULL(t2.`__avg2__`,0) ASC",
            "建设(String)");

    private const string MaxType = UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX(t1.`type`) AS `__max2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE t2.`__max2__` = ?";
    private const string MinType = UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MIN(t1.`type`) AS `__min2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE t2.`__min2__` = ?";

    // MAX / MIN 上的 Distinct 不影响结果，SQL 里不写
    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct9"), Upstream("QueryTest22.java#testOrderCountDistinct10")]
    public void TestOrderCountDistinct9()
    {
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Max(x => x.Type) == "maxtype").ToStatement(), MaxType, "建设(String),maxtype(String)");
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.Where(bk => bk.Type == "建设").Max(x => x.Type) == "maxtype").ToStatement(), MaxType, "建设(String),maxtype(String)");
    }

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct11")]
    public void TestOrderCountDistinct11() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>().Where(user => user.BankCards!.Where(bk => bk.Type == "建设").Max(x => x.Type) == "maxtype").ToStatement(),
            UserColumns + " WHERE (SELECT MAX(t1.`type`) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?) = ?", "建设(String),maxtype(String)");

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct12"), Upstream("QueryTest22.java#testOrderCountDistinct13")]
    public void TestOrderCountDistinct12()
    {
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.Where(bk => bk.Type == "建设").Distinct().Min(x => x.Type) == "maxtype").ToStatement(), MinType, "建设(String),maxtype(String)");
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.Where(bk => bk.Type == "建设").Min(x => x.Type) == "maxtype").ToStatement(), MinType, "建设(String),maxtype(String)");
    }

    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct14")]
    public void TestOrderCountDistinct14() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>().Where(user => user.BankCards!.Where(bk => bk.Type == "建设").Min(x => x.Type) == "maxtype").ToStatement(),
            UserColumns + " WHERE (SELECT MIN(t1.`type`) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?) = ?", "建设(String),maxtype(String)");

    // Java 的 none(条件)：C# 的 !Any(条件)
    [Fact, Upstream("QueryTest22.java#testOrderCountDistinct15")]
    public void TestOrderCountDistinct15() =>
        SqlOnly.AssertStatement(Users().Where(user => !user.BankCards!.Any(bk => bk.Type == "建设")).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) <= 0) AS `__none2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__none2__`,?) = ?",
            "1(Integer),建设(String),true(Boolean),true(Boolean)");

    // 复合键导航的合并：多个 __group_keyN__，ON 带括号
    [Fact, Upstream("QueryTest22.java#testDoubleField")]
    public void TestDoubleField() =>
        SqlOnly.AssertStatement(Db.Query<MyComUser>().GroupJoin(x => x.MySignUps)
                .Where(m => m.MySignUps!.Where(x => x.UserId == "123").LongCount() == 1L)
                .Select(m => new Draft<long, long>(m.MySignUps!.Where(x => x.UserId == "123").LongCount(),
                    m.MySignUps!.Where(x => x.UserId == "123").Select(x => x.Content).Distinct().LongCount()))
                .ToStatement(),
            "SELECT IFNULL(t2.`__count3__`,0) AS `value1`,IFNULL(t2.`__count4__`,0) AS `value2` FROM `my_com_user` t LEFT JOIN (SELECT t1.`com_id` AS `__group_key1__`,t1.`user_id` AS `__group_key2__`,COUNT(?) AS `__count3__`,COUNT(DISTINCT t1.`content`) AS `__count4__` FROM `my_sign_up` t1 WHERE t1.`user_id` = ? GROUP BY t1.`com_id`,t1.`user_id`) t2 ON (t2.`__group_key1__` = t.`com_id` AND t2.`__group_key2__` = t.`user_id`) WHERE IFNULL(t2.`__count3__`,0) = ?",
            "1(Integer),123(String),1(Long)");

    // Java 的 joining(x -> x.code(), ",")：C# 的 string.Join(",", nav.Select(x => x.Code))
    [Fact, Upstream("QueryTest22.java#testSelectAnyElement")]
    public void TestSelectAnyElement() =>
        SqlOnly.AssertStatement(Users()
                .Select(user => new Draft<string, string>(string.Join(",", user.BankCards!.Where(o => o.Type == "123").Select(x => x.Code)), string.Join(",", user.BankCards!.Select(x => x.Code))))
                .ToStatement(),
            "SELECT t2.`__joining2__` AS `value1`,t2.`__joining3__` AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,GROUP_CONCAT((CASE WHEN t1.`type` = ? THEN t1.`code` ELSE NULL END) SEPARATOR ?) AS `__joining2__`,GROUP_CONCAT(t1.`code` SEPARATOR ?) AS `__joining3__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`",
            "123(String),,(String),,(String)");

    [Fact, Upstream("QueryTest22.java#testSelectAnyElement2", Deviation = "Java 第二个子查询的别名跳过了 t2（写成 t3），C# 按顺序编号为 t2")]
    public void TestSelectAnyElement2() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>()
                .Select(user => new Draft<string, string>(string.Join(",", user.BankCards!.Where(o => o.Type == "123").Select(x => x.Code)), string.Join(",", user.BankCards!.Select(x => x.Code))))
                .ToStatement(),
            "SELECT (SELECT GROUP_CONCAT(t1.`code` SEPARATOR ?) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id` AND t1.`type` = ?) AS `value1`,(SELECT GROUP_CONCAT(t2.`code` SEPARATOR ?) FROM `doc_bank_card` t2 WHERE t2.`uid` = t.`id`) AS `value2` FROM `doc_user` t",
            ",(String),123(String),,(String)");

    [Fact, Upstream("QueryTest22.java#testSelectAnyElement3")]
    public void TestSelectAnyElement3() =>
        SqlOnly.AssertStatement(Users()
                .Select(user => new Draft<string?, string?>(user.BankCards!.Where(o => o.Type == "123").Max(x => x.Code), user.BankCards!.Min(x => x.Type)))
                .ToStatement(),
            "SELECT t2.`__max2__` AS `value1`,t2.`__min3__` AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,MAX((CASE WHEN t1.`type` = ? THEN t1.`code` ELSE NULL END)) AS `__max2__`,MIN(t1.`type`) AS `__min3__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`",
            "123(String)");

    [Fact, Upstream("QueryTest22.java#testManyJoinNone")]
    public void TestManyJoinNone() =>
        SqlOnly.AssertStatement(Users()
                .Where(user => user.BankCards!.Any() && user.BankCards!.Where(x => x.Type == "123").Any() && !user.BankCards!.Any() && !user.BankCards!.Where(x => x.Type == "123").Any())
                .ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,(COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) > 0) AS `__any3__`,(COUNT(*) <= 0) AS `__none4__`,(COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) <= 0) AS `__none5__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__any3__`,?) = ? AND IFNULL(t2.`__none4__`,?) = ? AND IFNULL(t2.`__none5__`,?) = ?",
            "123(String),1(Integer),123(String),1(Integer),false(Boolean),true(Boolean),false(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean),true(Boolean)");

    [Fact, Upstream("QueryTest22.java#testManyJoinCount")]
    public void TestManyJoinCount() =>
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.LongCount() == 0L && user.BankCards!.Where(x => x.Type == "123").LongCount() > 0L).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count3__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) = ? AND IFNULL(t2.`__count3__`,0) > ?",
            "123(String),1(Integer),0(Long),0(Long)");
}

/// <summary>对应 Java：AggregateFxVO。</summary>
public sealed class AggregateFxVO
{
    public long? Count { get; set; }
}

/// <summary>QueryTest22 里基于 t_blog 的用例（实库执行）。</summary>
public sealed class QueryTest22Blog(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("QueryTest22.java#testBigDecimal")]
    public async Task TestBigDecimal()
    {
        using var listen = SqlCapture.Listen();
        Assert.NotEmpty(await Db.Query<BlogEntity>().Select(o => o.Score).ToListAsync(Ct));
        listen.AssertSql("SELECT t.`score` FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("false(Boolean)");
    }

    [Fact, Upstream("QueryTest22.java#testAdd")]
    public async Task TestAdd()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().MaxAsync(o => o.Star + 1L, Ct);
        listen.AssertSql("SELECT MAX((t.`star` + ?)) FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("1(Long),false(Boolean)");
    }

    // 原生片段里引用日期相差：Java 的 duration(x).toDays() 对应 C# 的 (x - a).TotalDays
    [Fact, Upstream("QueryTest22.java#testAggregateFx")]
    public async Task TestAggregateFx()
    {
        using var listen = SqlCapture.Listen();
        var vo = await Db.Query<BlogEntity>()
            .Select(b => new AggregateFxVO { Count = Sql.Raw<long?>("sum(if({0} < {1}, 1, 0))", (long)(new DateTime(2025, 1, 1) - b.CreateTime!.Value).TotalDays, 2) })
            .SingleOrDefaultAsync(Ct) ?? new AggregateFxVO();
        Assert.NotNull(vo);
        listen.AssertSql("SELECT sum(if(timestampdiff(DAY, t.`create_time`, ?) < ?, 1, 0)) AS `count` FROM `t_blog` t WHERE t.`deleted` = ?");
        listen.AssertParams("2025-01-01T00:00(LocalDateTime),2(Integer),false(Boolean)");
    }

    // Java 的 orderBy(false, …)：C# 的 OrderByIf(false, …)
    [Fact, Upstream("QueryTest22.java#testaa")]
    public async Task Testaa()
    {
        const bool useOrder = false;
        Assert.NotNull(await Db.Query<BlogEntity>()
            .Where(b => b.Title!.Contains("123") || b.Id!.Contains("123") || b.Star == 1)
            .OrderByIf(useOrder, b => b.Title == "123" ? 1 : b.Title == "456" ? 2 : 3)
            .OrderByIf(useOrder, b => b.Star)
            .ToListAsync(Ct));
    }
}
