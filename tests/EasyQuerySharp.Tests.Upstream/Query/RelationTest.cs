using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.Infrastructure;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：RelationTest.java 的省市区带出（include）、班级学生的导航条件、投影到导航对象。
/// Java 的 include(o -> o.cities(), cq -> cq.include(…)) 对应 C# 的 Include(o => o.Cities, q => q.Include(…))。
/// </summary>
public sealed class RelationTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static void AssertTree(List<Province> provinces)
    {
        foreach (var province in provinces)
        {
            Assert.NotNull(province.Cities);
            foreach (var city in province.Cities)
            {
                Assert.Equal(province.Code, city.ProvinceCode);
                Assert.NotNull(city.Areas);
                Assert.All(city.Areas, area => Assert.Equal((city.Code, province.Code), (area.CityCode, area.ProvinceCode)));
            }
        }
    }

    [Fact, Upstream("RelationTest.java#provinceTest")]
    public async Task ProvinceTest()
    {
        var list = await Db.Query<Province>().Include(o => o.Cities, cq => cq.Include(s => s.Areas)).ToListAsync(Ct);
        Assert.Equal(2, list.Count);
        AssertTree(list);
    }

    [Theory, Upstream("RelationTest.java#provinceTest6"), Upstream("RelationTest.java#provinceTest7")]
    [InlineData(100, 2)]
    [InlineData(1, 1)]
    public async Task ProvinceTest6(int size, int expected)
    {
        var page = await Db.Query<Province>().Include(o => o.Cities, cq => cq.Include(s => s.Areas)).ToPageAsync(1, size, Ct);
        Assert.Equal(expected, page.Rows.Count);
        AssertTree([.. page.Rows]);
    }

    // 带出的子查询可以再加条件：每个省只带出绍兴市，绍兴市只带出越城区
    [Fact, Upstream("RelationTest.java#provinceTest1")]
    public async Task ProvinceTest1()
    {
        var list = await Db.Query<Province>()
            .Include(p => p.Cities, pq => pq.Include(s => s.Areas, aq => aq.Where(x => x.Code == "330602")).Where(x => x.Code == "3306"))
            .ToListAsync(Ct);
        Assert.Equal(2, list.Count);
        AssertTree(list);
        foreach (var city in list.SelectMany(p => p.Cities!))
        {
            Assert.Equal("绍兴市", city.Name);
            Assert.All(city.Areas!, a => Assert.Equal("越城区", a.Name));
        }
    }

    [Fact, Upstream("RelationTest.java#provinceTest5")]
    public async Task ProvinceTest5() =>
        Assert.Single(await Db.Query<Province>().WhereById("33").Include(o => o.Cities).ToListAsync(Ct));

    [Fact, Upstream("RelationTest.java#schoolTest1")]
    public async Task SchoolTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Any(x => x.Name!.Contains("小明"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ? LIMIT 1)");
        listen.AssertParams("%小明%(String)");
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Any(x => x.Name!.Contains("小明") && x.ClassId!.Contains("13"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ? AND t1.`class_id` LIKE ? LIMIT 1)");
        listen.AssertParams("%小明%(String),%13%(String)");
    }

    [Fact, Upstream("RelationTest.java#schoolTest2")]
    public async Task SchoolTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolStudent>().Where(s => s.SchoolClass!.Name!.Contains("一班")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t LEFT JOIN `school_class` t1 ON t1.`id` = t.`class_id` WHERE t1.`name` LIKE ?");
        listen.AssertParams("%一班%(String)");
    }

    [Fact, Upstream("RelationTest.java#schoolTest3")]
    public async Task SchoolTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Any(x => x.SchoolStudentAddress!.Address!.Contains("xx路"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 LEFT JOIN `school_student_address` t2 ON t2.`student_id` = t1.`id` WHERE t1.`class_id` = t.`id` AND t2.`address` LIKE ? LIMIT 1)");
        listen.AssertParams("%xx路%(String)");
    }

    // Java 在 any 里写两条 / 链两个 where 再 any：C# 都是 && 或链式 Where
    [Fact, Upstream("RelationTest.java#schoolTest4"), Upstream("RelationTest.java#schoolTest5")]
    public async Task SchoolTest4()
    {
        const string sql = "SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 LEFT JOIN `school_student_address` t2 ON t2.`student_id` = t1.`id` WHERE t1.`class_id` = t.`id` AND t2.`address` LIKE ? AND t1.`name` LIKE ? LIMIT 1)";
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Any(x => x.SchoolStudentAddress!.Address!.Contains("xx路") && x.Name!.Contains("小明"))).ToListAsync(Ct);
        listen.AssertSql(sql);
        listen.AssertParams("%xx路%(String),%小明%(String)");
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Where(x => x.SchoolStudentAddress!.Address!.Contains("xx路")).Where(x => x.Name!.Contains("小明")).Any()).ToListAsync(Ct);
        listen.AssertSql(sql);
    }

    [Fact, Upstream("RelationTest.java#schoolTest6")]
    public async Task SchoolTest6()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => s.SchoolTeachers!.Any(x => x.Name!.Contains("x"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_teacher` t1 WHERE EXISTS(SELECT 1 FROM `school_class_teacher` t2 WHERE t2.`teacher_id` = t1.`id` AND t2.`class_id` = t.`id` LIMIT 1) AND t1.`name` LIKE ? LIMIT 1)");
        listen.AssertParams("%x%(String)");
    }

    [Fact, Upstream("RelationTest.java#schoolTest7")]
    public async Task SchoolTest7()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Where(x => x.Name!.StartsWith("张")).LongCount() == 5L).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t WHERE (SELECT COUNT(*) FROM `school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?) = ?");
        listen.AssertParams("张%(String),5(Long)");
    }

    // Java 的 where(…).none() / none(…)：C# 的 !Any(…)
    [Fact, Upstream("RelationTest.java#schoolTest8")]
    public async Task SchoolTest8()
    {
        const string sql = "SELECT t.`id`,t.`name` FROM `school_class` t WHERE NOT (EXISTS(SELECT 1 FROM `school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ? AND t1.`class_id` LIKE ? LIMIT 1))";
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => !s.SchoolStudents!.Where(x => x.Name!.Contains("小明") && x.ClassId!.Contains("13")).Any()).ToListAsync(Ct);
        listen.AssertSql(sql);
        await Db.Query<SchoolClass>().Where(s => !s.SchoolStudents!.Any(x => x.Name!.Contains("小明") && x.ClassId!.Contains("13"))).ToListAsync(Ct);
        listen.AssertSql(sql);
        listen.AssertParams("%小明%(String),%13%(String)");
    }

    [Fact, Upstream("RelationTest.java#schoolTest8_1")]
    public async Task SchoolTest8_1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Any() && !s.SchoolStudents!.Any(x => x.Name != "小明")).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 WHERE t1.`class_id` = t.`id` LIMIT 1) AND NOT (EXISTS(SELECT 1 FROM `school_student` t2 WHERE t2.`class_id` = t.`id` AND t2.`name` <> ? LIMIT 1))");
        listen.AssertParams("小明(String)");
    }

    // 投影到对一导航：整个导航对象、它的列、再往下一层的导航
    [Fact, Upstream("RelationTest.java#testSub")]
    public async Task TestSub()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolStudent>().Where(s => s.Name!.Contains("123")).Select(s => s.SchoolClass).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id`,t1.`name` FROM `school_student` t LEFT JOIN `school_class` t1 ON t1.`id` = t.`class_id` WHERE t.`name` LIKE ?");
        listen.AssertParams("%123%(String)");
    }

    [Fact, Upstream("RelationTest.java#testSub1")]
    public async Task TestSub1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolStudent>().Where(s => s.Name!.Contains("123")).Select(s => s.SchoolClass!.Name).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`name` FROM `school_student` t LEFT JOIN `school_class` t1 ON t1.`id` = t.`class_id` WHERE t.`name` LIKE ?");
    }

    [Fact, Upstream("RelationTest.java#testSub2")]
    public async Task TestSub2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolStudent>().Where(s => s.Name!.Contains("123")).Select(s => s.SchoolStudentAddress!.SchoolStudent).ToListAsync(Ct);
        listen.AssertSql("SELECT t2.`id`,t2.`class_id`,t2.`name` FROM `school_student` t LEFT JOIN `school_student_address` t1 ON t1.`student_id` = t.`id` LEFT JOIN `school_student` t2 ON t2.`id` = t1.`student_id` WHERE t.`name` LIKE ?");
    }

    // Java 的 select(new StringProxy(…)) 与 selectColumn(…) 生成同一条 SQL
    [Fact, Upstream("RelationTest.java#testSub3"), Upstream("RelationTest.java#testSub4")]
    public async Task TestSub3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SchoolStudent>().Where(s => s.Name!.Contains("123")).Select(s => s.SchoolClass!.Name!.ToLower()).ToListAsync(Ct);
        listen.AssertSql("SELECT LOWER(t1.`name`) FROM `school_student` t LEFT JOIN `school_class` t1 ON t1.`id` = t.`class_id` WHERE t.`name` LIKE ?");
        listen.AssertParams("%123%(String)");
    }
}
