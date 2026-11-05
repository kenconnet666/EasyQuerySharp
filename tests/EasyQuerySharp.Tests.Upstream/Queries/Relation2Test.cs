namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：Relation2Test.java（导航用默认的自身主键：对一导航的隐式 JOIN、集合导航的条件聚合）。
/// Java 的 toNumber(..) 对应 <c>Sql.Cast&lt;T&gt;</c>，asAnyType 只改类型：C# 经 object 强转。
/// </summary>
public sealed class Relation2Test(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static async Task AssertQuery<T>(Query<T> query, string sql, string parameters)
    {
        using var listen = SqlCapture.Listen();
        await query.ToListAsync(Ct);
        listen.AssertSql(sql);
        listen.AssertParams(parameters);
    }

    [Fact, Upstream("Relation2Test.java#test")]
    public Task Test() =>
        AssertQuery(Db.Query<MySchoolStudent>().Where(m => m.SchoolStudentAddress!.Id == "1"), "SELECT t.`id`,t.`class_id`,t.`name` FROM `my_school_student` t LEFT JOIN `school_student_address` t1 ON t1.`student_id` = t.`id` WHERE t1.`id` = ?", "1(String)");

    [Fact, Upstream("Relation2Test.java#test1")]
    public Task Test1() =>
        AssertQuery(Db.Query<MySchoolStudent>().Where(m => m.SchoolStudentAddress!.Id!.Contains("1")).OrderBy(m => m.SchoolStudentAddress!.Id), "SELECT t.`id`,t.`class_id`,t.`name` FROM `my_school_student` t LEFT JOIN `school_student_address` t1 ON t1.`student_id` = t.`id` WHERE t1.`id` LIKE ? ORDER BY t1.`id` ASC", "%1%(String)");

    [Fact, Upstream("Relation2Test.java#test2")]
    public Task Test2() =>
        AssertQuery(Db.Query<MySchoolStudent>().Where(m => m.SchoolStudentAddress!.Id!.Contains("1") && m.SchoolClass!.Name!.Contains("2")).OrderBy(m => m.SchoolStudentAddress!.Id),
            "SELECT t.`id`,t.`class_id`,t.`name` FROM `my_school_student` t LEFT JOIN `school_student_address` t1 ON t1.`student_id` = t.`id` LEFT JOIN `my_school_class` t2 ON t2.`id` = t.`class_id` WHERE t1.`id` LIKE ? AND t2.`name` LIKE ? ORDER BY t1.`id` ASC", "%1%(String),%2%(String)");

    [Fact, Upstream("Relation2Test.java#test3")]
    public Task Test3() =>
        AssertQuery(Db.Query<MySchoolStudent>().Where(m => m.Name!.Contains("小明") && m.SchoolClass!.Name == "一班"), "SELECT t.`id`,t.`class_id`,t.`name` FROM `my_school_student` t LEFT JOIN `my_school_class` t1 ON t1.`id` = t.`class_id` WHERE t.`name` LIKE ? AND t1.`name` = ?", "%小明%(String),一班(String)");

    [Fact, Upstream("Relation2Test.java#test5")]
    public Task Test5() =>
        AssertQuery(Db.Query<MySchoolClass>().Where(m => m.SchoolStudents!.Where(x => x.Name!.Contains("123")).LongCount() > 1L), "SELECT t.`id`,t.`name` FROM `my_school_class` t WHERE (SELECT COUNT(*) FROM `my_school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?) > ?", "%123%(String),1(Long)");

    [Fact, Upstream("Relation2Test.java#test6")]
    public Task Test6() =>
        AssertQuery(Db.Query<MySchoolClass>().Where(m => m.SchoolStudents!.Any(x => x.Name!.Contains("123"))), "SELECT t.`id`,t.`name` FROM `my_school_class` t WHERE EXISTS(SELECT 1 FROM `my_school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ? LIMIT 1)", "%123%(String)");

    [Fact, Upstream("Relation2Test.java#test7")]
    public Task Test7() =>
        AssertQuery(Db.Query<MySchoolClass>().Where(m => m.SchoolStudents!.Where(x => x.Name!.Contains("1234")).Sum(x => (decimal?)Sql.Cast<long>(x.Name)) > 1m),
            "SELECT t.`id`,t.`name` FROM `my_school_class` t WHERE IFNULL((SELECT SUM(CAST(t1.`name` AS SIGNED)) FROM `my_school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?),0) > ?", "%1234%(String),1(BigDecimal)");

    [Fact, Upstream("Relation2Test.java#test8")]
    public Task Test8() =>
        AssertQuery(Db.Query<MySchoolClass>().Where(m => m.SchoolStudents!.Where(x => x.Name!.Contains("1234")).Average(x => (decimal?)Sql.Cast<decimal>(x.Name)) > 1m),
            "SELECT t.`id`,t.`name` FROM `my_school_class` t WHERE IFNULL((SELECT AVG(CAST(t1.`name` AS DECIMAL(36,18))) FROM `my_school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?),0) > ?", "%1234%(String),1(BigDecimal)");

    [Fact, Upstream("Relation2Test.java#test9"), Upstream("Relation2Test.java#test9_1")]
    public Task Test9() =>
        AssertQuery(Db.Query<MySchoolClass>().Where(m => m.SchoolStudents!.Where(x => x.Name!.Contains("1234")).Sum(x => (decimal?)Sql.Cast<decimal>(x.Name)) > 1m),
            "SELECT t.`id`,t.`name` FROM `my_school_class` t WHERE IFNULL((SELECT SUM(CAST(t1.`name` AS DECIMAL(36,18))) FROM `my_school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?),0) > ?", "%1234%(String),1(BigDecimal)");

    [Fact, Upstream("Relation2Test.java#test10"), Upstream("Relation2Test.java#test10_1")]
    public Task Test10() =>
        AssertQuery(Db.Query<MySchoolClass>().Where(m => m.SchoolStudents!.Where(x => x.Name!.Contains("1234")).Max(x => (decimal?)(object)x.Name!) > 1m),
            "SELECT t.`id`,t.`name` FROM `my_school_class` t WHERE (SELECT MAX(t1.`name`) FROM `my_school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?) > ?", "%1234%(String),1(BigDecimal)");
}
