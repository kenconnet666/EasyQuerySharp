using EasyQuerySharp.Filtering;
using EasyQuerySharp.Querying;
using EasyQuerySharp.Tests.Upstream.Doc.Entities;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：QueryTest22.java（集合导航聚合的分组合并 subQueryToGroupJoin）。
/// Java 的 subQueryToGroupJoin(o -&gt; o.bankCards()) 对应 C# 的 GroupJoin(o =&gt; o.BankCards)，subQueryConfigure 对应 GroupJoin 的第二个参数。
/// Java 用例执行时依赖文档示例库的数据，这里只比对 SQL。
/// </summary>
public sealed partial class QueryTest22
{
    private static EasyDb Db => SqlOnly.Db("mysql");

    private const string UserColumns = "SELECT t.`id`,t.`name`,t.`phone`,t.`age` FROM `doc_user` t";

    private static Query<DocUser> Users() => Db.Query<DocUser>().GroupJoin(o => o.BankCards);

    // 相同的聚合（同条件、同值）只占一列；条件不同的写成 CASE WHEN
    [Fact, Upstream("QueryTest22.java#testManyGroup")]
    public void TestManyGroup() =>
        SqlOnly.AssertStatement(Users().Where(user =>
                user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 123
                && user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 456
                && user.BankCards!.Where(x => x.Type == "456").Sum(o => int.Parse(o.Code!)) == 789).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM((CASE WHEN t1.`type` = ? THEN CAST(t1.`code` AS SIGNED) ELSE ? END)) AS `__sum2__`,SUM((CASE WHEN t1.`type` = ? THEN CAST(t1.`code` AS SIGNED) ELSE ? END)) AS `__sum3__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__sum2__`,0) = ? AND IFNULL(t2.`__sum2__`,0) = ? AND IFNULL(t2.`__sum3__`,0) = ?",
            "123(String),0(Integer),456(String),0(Integer),123(Integer),456(Integer),789(Integer)");

    // 全部聚合的条件相同：条件提到分组子查询的 WHERE（Java 的 configure(disableLogicDelete) 对没有逻辑删除的表没有影响）
    [Fact, Upstream("QueryTest22.java#testManyGroupx")]
    public void TestManyGroupx() =>
        SqlOnly.AssertStatement(Users().Where(user =>
                user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 123
                && user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 123).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM(CAST(t1.`code` AS SIGNED)) AS `__sum2__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__sum2__`,0) = ? AND IFNULL(t2.`__sum2__`,0) = ?",
            "123(String),123(Integer),123(Integer)");

    [Fact, Upstream("QueryTest22.java#testManyGroupJoin")]
    public void TestManyGroupJoin() =>
        SqlOnly.AssertStatement(Users().Where(user =>
                user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 123
                && user.BankCards!.Where(x => x.Type == "456").Sum(o => int.Parse(o.Code!)) == 789).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM((CASE WHEN t1.`type` = ? THEN CAST(t1.`code` AS SIGNED) ELSE ? END)) AS `__sum2__`,SUM((CASE WHEN t1.`type` = ? THEN CAST(t1.`code` AS SIGNED) ELSE ? END)) AS `__sum3__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__sum2__`,0) = ? AND IFNULL(t2.`__sum3__`,0) = ?",
            "123(String),0(Integer),456(String),0(Integer),123(Integer),789(Integer)");

    // 条件、排序、投影里的聚合都登记到同一个分组子查询；GroupJoin 的第二个参数给子查询加条件（条件里的导航在子查询里 JOIN）
    [Fact, Upstream("QueryTest22.java#testManyGroupJoin2"),
     Upstream("QueryTest22.java#testManyGroupJoin2_1")]
    public void TestManyGroupJoin2() =>
        SqlOnly.AssertStatement(Db.Query<DocUser>()
                .GroupJoin(o => o.BankCards, q => q.Where(x => x.Bank!.Name == "银行" && x.Type!.Contains("45678")))
                .Where(user =>
                    user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 123
                    && user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)) == 456
                    && user.BankCards!.Where(x => x.Type == "456").Sum(o => int.Parse(o.Code!)) == 789)
                .OrderBy(user => user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)))
                .ThenByDescending(user => user.BankCards!.Where(x => x.Type == "123").Max(o => o.Code))
                .Select(user => new Draft<string?, int, string?>(user.Id,
                    user.BankCards!.Where(x => x.Type == "123").Sum(o => int.Parse(o.Code!)),
                    user.BankCards!.Where(x => x.Type == "123").Min(o => o.Code)))
                .ToStatement(),
            "SELECT t.`id` AS `value1`,IFNULL(t3.`__sum2__`,0) AS `value2`,t3.`__min5__` AS `value3` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,SUM((CASE WHEN t1.`type` = ? THEN CAST(t1.`code` AS SIGNED) ELSE ? END)) AS `__sum2__`,SUM((CASE WHEN t1.`type` = ? THEN CAST(t1.`code` AS SIGNED) ELSE ? END)) AS `__sum3__`,MAX((CASE WHEN t1.`type` = ? THEN t1.`code` ELSE NULL END)) AS `__max4__`,MIN((CASE WHEN t1.`type` = ? THEN t1.`code` ELSE NULL END)) AS `__min5__` FROM `doc_bank_card` t1 INNER JOIN `doc_bank` t2 ON t2.`id` = t1.`bank_id` WHERE t2.`name` = ? AND t1.`type` LIKE ? GROUP BY t1.`uid`) t3 ON t3.`__group_key1__` = t.`id` WHERE IFNULL(t3.`__sum2__`,0) = ? AND IFNULL(t3.`__sum2__`,0) = ? AND IFNULL(t3.`__sum3__`,0) = ? ORDER BY IFNULL(t3.`__sum2__`,0) ASC,t3.`__max4__` DESC",
            "123(String),0(Integer),456(String),0(Integer),123(String),123(String),银行(String),%45678%(String),123(Integer),456(Integer),789(Integer)");

    [Fact, Upstream("QueryTest22.java#testManyGroupJoin2_2", NotApplicable = "Java 在条件中途写 filter，之后引用的聚合另起一个分组 JOIN；C# 的子查询条件统一写在 GroupJoin 的配置里")]
    public void TestManyGroupJoin2_2() { }

    [Fact, Upstream("QueryTest22.java#testManyGroupJoin3")]
    public void TestManyGroupJoin3() =>
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.Where(x => x.Code!.StartsWith("400")).Any())
                .Select(user => new Draft<string?, int, int, int>(user.Id,
                    user.BankCards!.Where(x => x.Type == "工商").Count(), user.BankCards!.Where(x => x.Type == "建设").Count(),
                    user.BankCards!.Where(x => x.Type == "农业").Count()))
                .ToStatement(),
            "SELECT t.`id` AS `value1`,IFNULL(t2.`__count3__`,0) AS `value2`,IFNULL(t2.`__count4__`,0) AS `value3`,IFNULL(t2.`__count5__`,0) AS `value4` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT((CASE WHEN t1.`code` LIKE ? THEN ? ELSE NULL END)) > 0) AS `__any2__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count3__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count4__`,COUNT((CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count5__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ?",
            "400%(String),1(Integer),工商(String),1(Integer),建设(String),1(Integer),农业(String),1(Integer),false(Boolean),true(Boolean)");

    private const string CountGroup2Where = " WHERE IFNULL(t2.`__count2__`,0) = ? AND IFNULL(t2.`__count3__`,0) >= ? AND IFNULL(t2.`__count3__`,0) >= ? AND IFNULL(t2.`__count4__`,0) = ? AND (IFNULL(t2.`__count2__`,0) = ? OR IFNULL(t2.`__count4__`,0) = ?)";

    // Java 的 count(o -> o.type())：C# 的 Select(o => o.Type).LongCount()（SQL 的 COUNT(列)）
    private static Query<DocUser> CountGroup() => Users().Where(user =>
        user.BankCards!.LongCount() == 1L
        && user.BankCards!.Select(o => o.Type).LongCount() >= 2L
        && user.BankCards!.Select(o => o.Type).LongCount() >= 3L
        && user.BankCards!.Where(x => x.Type == "123").Distinct().LongCount() == 123L
        && (user.BankCards!.LongCount() == 1L || user.BankCards!.Where(x => x.Type == "123").Distinct().LongCount() == 456L));

    [Fact, Upstream("QueryTest22.java#testManyGroup2")]
    public void TestManyGroup2() =>
        SqlOnly.AssertStatement(CountGroup().ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__`,COUNT(t1.`type`) AS `__count3__`,COUNT(DISTINCT (CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count4__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`" + CountGroup2Where,
            "123(String),1(Integer),1(Long),2(Long),3(Long),123(Long),1(Long),456(Long)");

    [Fact, Upstream("QueryTest22.java#testManyGroup3")]
    public void TestManyGroup3() =>
        SqlOnly.AssertStatement(CountGroup()
                .Select(user => new Draft<long, long, long>(user.BankCards!.Select(o => o.Type).LongCount(),
                    user.BankCards!.Where(x => x.Type == "123").Distinct().LongCount(), user.BankCards!.Where(x => x.Type == "124").Distinct().LongCount()))
                .ToStatement(),
            "SELECT IFNULL(t2.`__count3__`,0) AS `value1`,IFNULL(t2.`__count4__`,0) AS `value2`,IFNULL(t2.`__count5__`,0) AS `value3` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__`,COUNT(t1.`type`) AS `__count3__`,COUNT(DISTINCT (CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count4__`,COUNT(DISTINCT (CASE WHEN t1.`type` = ? THEN ? ELSE NULL END)) AS `__count5__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id`" + CountGroup2Where,
            "123(String),1(Integer),124(String),1(Integer),1(Long),2(Long),3(Long),123(Long),1(Long),456(Long)");

    // Java 的 any() / none() / anyValue() / noneValue()：C# 的 Any() / !Any()，作为值时是 IFNULL(列, 默认)
    [Fact, Upstream("QueryTest22.java#testManyGroup4")]
    public void TestManyGroup4() =>
        SqlOnly.AssertStatement(Users()
                .Where(user => user.BankCards!.Any() && !user.BankCards!.Any() && user.BankCards!.Any() == false && !user.BankCards!.Any() == true)
                .Select(user => new Draft<bool, bool>(user.BankCards!.Any(), !user.BankCards!.Any()))
                .ToStatement(),
            "SELECT IFNULL(t2.`__any2__`,?) AS `value1`,IFNULL(t2.`__none3__`,?) AS `value2` FROM `doc_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,(COUNT(*) <= 0) AS `__none3__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__none3__`,?) = ? AND IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__none3__`,?) = ?",
            "false(Boolean),true(Boolean),false(Boolean),true(Boolean),true(Boolean),true(Boolean),false(Boolean),false(Boolean),true(Boolean),true(Boolean)");

    [Fact, Upstream("QueryTest22.java#testManyGroup5")]
    public void TestManyGroup5() =>
        SqlOnly.AssertStatement(Users().Where(user => user.BankCards!.Where(x => x.Type == "1").Any() && !user.BankCards!.Where(x => x.Type == "1").Any()).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,(COUNT(?) > 0) AS `__any2__`,(COUNT(?) <= 0) AS `__none3__` FROM `doc_bank_card` t1 WHERE t1.`type` = ? GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? AND IFNULL(t2.`__none3__`,?) = ?",
            "1(Integer),1(Integer),1(String),false(Boolean),true(Boolean),true(Boolean),true(Boolean)");

    [Fact, Upstream("QueryTest22.java#testManyGroup6", NotApplicable = "Java 在 where 里只写聚合不写比较时什么也不生成；C# 的条件必须是布尔表达式")]
    public void TestManyGroup6() { }

    // 条件值过滤去掉了条件：没有聚合被引用，也就不 JOIN
    [Fact, Upstream("QueryTest22.java#testManyGroup7")]
    public void TestManyGroup7()
    {
        long? c = null;
        SqlOnly.AssertStatement(Db.Query<DocUser>().UseValueFilter(ValueFilter.NotNullOrBlank).GroupJoin(o => o.BankCards).Where(user => user.BankCards!.LongCount() == c).ToStatement(),
            UserColumns);
    }

    [Fact, Upstream("QueryTest22.java#testManyGroup8"), Upstream("QueryTest22.java#testManyGroup10")]
    public void TestManyGroup8()
    {
        long? c = 1L;
        SqlOnly.AssertStatement(Db.Query<DocUser>().UseValueFilter(ValueFilter.NotNullOrBlank).GroupJoin(o => o.BankCards).Where(user => user.BankCards!.LongCount() == c).ToStatement(),
            UserColumns + " LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `doc_bank_card` t1 GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__count2__`,0) = ?",
            "1(Long)");
    }

    // GroupJoinIf(false, …)：照常翻译成相关子查询
    [Fact, Upstream("QueryTest22.java#testManyGroup9")]
    public void TestManyGroup9()
    {
        long? c = 1L;
        SqlOnly.AssertStatement(Db.Query<DocUser>().UseValueFilter(ValueFilter.NotNullOrBlank).GroupJoinIf(false, o => o.BankCards).Where(user => user.BankCards!.LongCount() == c).ToStatement(),
            UserColumns + " WHERE (SELECT COUNT(*) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id`) = ?",
            "1(Long)");
    }
}
