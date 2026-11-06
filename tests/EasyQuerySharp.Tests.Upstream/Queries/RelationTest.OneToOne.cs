using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Queries;

// RelationTest.java#testOneToOne 是一个约 80 段的大方法（relationInit 种数据、relationRemove 清理），拆成本文件里的多个测试：
// 每个测试自己种数据、用完清理；Upstream 只标在第 1 段上，其余测试的注释里写明对应 Java 的行号。
// 期望 SQL 取自 Java；C# 写法或行为不同的地方写在各测试的注释里（偏离 / 疑似缺口 / Skip 的理由）。
public sealed partial class RelationTest
{
    private static readonly string[] SchoolStudentIds = ["1", "2", "3"];
    private static readonly string[] SchoolClassIds = ["class1", "class2", "class3"];
    private static readonly string[] SchoolTeacherIds = ["teacher1", "teacher2"];

    // 对应 Java 的 relationInit：学生 1、2、3 依次在 class1、class2、class1；老师 1 带 class1，老师 2 带 class1、class2；每个学生一条地址
    private async Task SchoolSeedAsync()
    {
        await SchoolClearAsync();
        await Db.Insert(SchoolStudentIds.Select((id, i) => new SchoolStudent { Id = id, ClassId = "class" + (i % 2 + 1), Name = "学生" + id })).ExecuteAsync(Ct);
        await Db.Insert(new SchoolClass { Id = "class1", Name = "班级1" }, new SchoolClass { Id = "class2", Name = "班级2" }, new SchoolClass { Id = "class3", Name = "班级3" })
            .ExecuteAsync(Ct);
        await Db.Insert(SchoolStudentIds.Select(id => new SchoolStudentAddress { Id = "address" + id, StudentId = id, Address = "地址" + id })).ExecuteAsync(Ct);
        await Db.Insert(new SchoolTeacher { Id = "teacher1", Name = "老师1" }, new SchoolTeacher { Id = "teacher2", Name = "老师2" }).ExecuteAsync(Ct);
        await Db.Insert(new SchoolClassTeacher { ClassId = "class1", TeacherId = "teacher1" }, new SchoolClassTeacher { ClassId = "class1", TeacherId = "teacher2" },
            new SchoolClassTeacher { ClassId = "class2", TeacherId = "teacher2" }).ExecuteAsync(Ct);
    }

    // 对应 Java 的 relationRemove
    private async Task SchoolClearAsync()
    {
        await Db.Delete<SchoolStudent>().WhereByIds(SchoolStudentIds).ExecuteAsync(Ct);
        await Db.Delete<SchoolStudentAddress>().Where(a => SchoolStudentIds.Contains(a.StudentId)).ExecuteAsync(Ct);
        await Db.Delete<SchoolClass>().Where(c => SchoolClassIds.Contains(c.Id)).ExecuteAsync(Ct);
        await Db.Delete<SchoolTeacher>().Where(t => SchoolTeacherIds.Contains(t.Id)).ExecuteAsync(Ct);
        await Db.Delete<SchoolClassTeacher>().Where(l => SchoolClassIds.Contains(l.ClassId)).ExecuteAsync(Ct);
        await Db.Delete<SchoolClassTeacher>().Where(l => SchoolTeacherIds.Contains(l.TeacherId)).ExecuteAsync(Ct);
    }

    private async Task WithSchoolDataAsync(Func<SqlCapture.Scope, Task> body)
    {
        await SchoolSeedAsync();
        try
        {
            using var listen = SqlCapture.Listen();
            await body(listen);
        }
        finally
        {
            await SchoolClearAsync();
        }
    }

    // ---------- DTO：Java 的 dto/autodto 与 entity/school/dto。自动映射按同名属性与导航对应，导航上的 @Navigate 不用写 ----------

    /// <summary>Java 的 SchoolClassAO：班级名与学生的 id、名字。</summary>
    public sealed class SchoolClassAO
    {
        public string? Name { get; set; }

        public List<SchoolStudentAO>? SchoolStudents { get; set; }

        /// <summary>学生。</summary>
        public sealed class SchoolStudentAO
        {
            public string? Id { get; set; }

            public string? Name { get; set; }
        }
    }

    /// <summary>Java 的 SchoolClassAOProp：平铺班级下学生的 id。</summary>
    public sealed class SchoolClassAOProp
    {
        public string? Name { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }
    }

    /// <summary>Java 的 SchoolClassAOProp2：再平铺老师们带的班级的 id。</summary>
    public sealed class SchoolClassAOProp2
    {
        public string? Name { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Id")]
        public List<string?>? SchoolTeachersClassIds { get; set; }
    }

    /// <summary>Java 的 SchoolClassAOProp3 / SchoolClassAOProp11（11 用 MappingPath 常量声明学生 id 的路径，C# 里都是 MapPath）：再平铺班级的名字。</summary>
    public sealed class SchoolClassAOProp3
    {
        public string? Name { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Id")]
        public List<string?>? SchoolTeachersClassIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Name")]
        public List<string?>? SchoolTeachersClassId1s { get; set; }
    }

    /// <summary>Java 的 SchoolClassAOProp4：再把老师们带的班级整个平铺成实体列表。</summary>
    public sealed class SchoolClassAOProp4
    {
        public string? Name { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Id")]
        public List<string?>? SchoolTeachersClassIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Name")]
        public List<string?>? SchoolTeachersClassId1s { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses")]
        public List<SchoolClass>? SchoolTeachersClassList { get; set; }
    }

    /// <summary>Java 的 SchoolClassAOProp5 / 14 / 15（三者一样，15 只多了 Java 的日志配置）：老师们带的班级，DTO 里只要班级的 id。</summary>
    public sealed class SchoolClassAOProp5
    {
        public string? Name { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses")]
        public List<SchoolStudentAO>? SchoolTeachersClassList { get; set; }

        /// <summary>只有 id 的班级（Java 里叫 SchoolStudentAO）。</summary>
        public sealed class SchoolStudentAO
        {
            public string? Id { get; set; }
        }
    }

    /// <summary>Java 的 SchoolClassAOProp6：班级名、额外的 name1、学生 id、老师们带的班级的名字与实体。</summary>
    public sealed class SchoolClassAOProp6
    {
        public string? Name { get; set; }

        public string? Name1 { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Name")]
        public List<string?>? SchoolTeachersClassId1s { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses")]
        public List<SchoolClass>? SchoolTeachersClassList { get; set; }
    }

    /// <summary>Java 的 SchoolClassAOProp8：学生上再平铺学生所在班级的名字（对一路径放进列表）。</summary>
    public sealed class SchoolClassAOProp8
    {
        public string? Name { get; set; }

        public List<SchoolStudentAO>? SchoolStudents { get; set; }

        /// <summary>学生。</summary>
        public sealed class SchoolStudentAO
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            [MapPath("SchoolClass.Name")]
            public List<string?>? ClassNames { get; set; }
        }
    }

    /// <summary>
    /// Java 的 SchoolClassAOProp9：Name1 是经主题的 JOIN 列（@NavigateJoin("topic.stars")）。Java 的 name1 是 String，
    /// C# 的自动映射不做 int → string 转换，用 int?。
    /// </summary>
    public sealed class SchoolClassAOProp9
    {
        public string? Name { get; set; }

        [MapPath("Topic.Stars", Join = true)]
        public int? Name1 { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Name")]
        public List<string?>? SchoolTeachersClassId1s { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses")]
        public List<SchoolClass>? SchoolTeachersClassList { get; set; }
    }

    /// <summary>Java 的 SchoolClassAOProp10 / 16（两者一样）：学生上再取地址（JOIN 列）和班级名（带出）。</summary>
    public sealed class SchoolClassAOProp10
    {
        public string? Name { get; set; }

        public List<SchoolStudentAO>? SchoolStudents { get; set; }

        /// <summary>学生。</summary>
        public sealed class SchoolStudentAO
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            [MapPath("SchoolStudentAddress.Address", Join = true)]
            public string? StuAddress { get; set; }

            [MapPath("SchoolClass.Name")]
            public List<string?>? ClassNames { get; set; }
        }
    }

    /// <summary>Java 的 SchoolClassAOProp12：老师们带的班级既平铺字段、又整个平铺成非实体的对象列表（Java 报错）。</summary>
    public sealed class SchoolClassAOProp12
    {
        public string? Name { get; set; }

        [MapPath("SchoolStudents.Id")]
        public List<string?>? SchoolStudentsIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Id")]
        public List<string?>? SchoolTeachersClassIds { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses.Name")]
        public List<string?>? SchoolTeachersClassId1s { get; set; }

        [MapPath("SchoolTeachers.SchoolClasses")]
        public List<SchoolStudentAO>? SchoolTeachersClassList { get; set; }

        /// <summary>班级的 id、名字。</summary>
        public sealed class SchoolStudentAO
        {
            public string? Id { get; set; }

            public string? Name { get; set; }
        }
    }

    /// <summary>Java 的 SchoolStudentDTOAO111：学生的地址平铺进来，班级是嵌套对象。班级写在地址前，带出查询的顺序才与 Java 一致（Java 先处理 @Navigate、再处理平铺）。</summary>
    public sealed class SchoolStudentDTOAO111
    {
        public string? Id { get; set; }

        public string? ClassId { get; set; }

        public string? Name { get; set; }

        public InternalSchoolClass? SchoolClass { get; set; }

        [MapPath("SchoolStudentAddress.Address")]
        public string? Address { get; set; }

        /// <summary>班级。</summary>
        public sealed class InternalSchoolClass
        {
            public string? Id { get; set; }

            public string? Name { get; set; }
        }
    }

    /// <summary>Java 的 SchoolStudentDTOAO222：班级声明成一对一的学生（SchoolStudent2）上的嵌套对象。</summary>
    public sealed class SchoolStudentDTOAO222
    {
        public string? Id { get; set; }

        public string? ClassId { get; set; }

        public string? Name { get; set; }

        public InternalSchoolClass? SchoolClass { get; set; }

        /// <summary>班级。</summary>
        public sealed class InternalSchoolClass
        {
            public string? Id { get; set; }

            public string? Name { get; set; }
        }
    }

    /// <summary>Java 的 SchoolStudentDTOxxx：班级既是平铺的 SchoolClass.Name、又是整个 SchoolClass 属性（Java 报错）。</summary>
    public sealed class SchoolStudentDTOxxx
    {
        public string? Id { get; set; }

        public string? ClassId { get; set; }

        public string? Name { get; set; }

        [MapPath("SchoolClass.Name")]
        public string? SchoolClassName { get; set; }

        public SchoolClass? SchoolClass { get; set; }

        public InternalSchoolStudentAddress? SchoolStudentAddress { get; set; }

        /// <summary>地址。</summary>
        public sealed class InternalSchoolStudentAddress
        {
            public string? Id { get; set; }

            public string? StudentId { get; set; }

            public string? Address { get; set; }
        }
    }

    /// <summary>Java 的 SchoolClassVO：班级、学生（含班级、地址，互相引用）、老师。</summary>
    public sealed class SchoolClassVO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<SchoolStudentVO>? SchoolStudents { get; set; }

        public List<SchoolTeacherVO>? SchoolTeachers { get; set; }
    }

    /// <summary>Java 的 SchoolStudentVO。</summary>
    public sealed class SchoolStudentVO
    {
        public string? Id { get; set; }

        public string? ClassId { get; set; }

        public string? Name { get; set; }

        public SchoolClassVO? SchoolClass { get; set; }

        public SchoolStudentAddressVO? SchoolStudentAddress { get; set; }
    }

    /// <summary>Java 的 SchoolStudentAddressVO（Name 没有对应的列）。</summary>
    public sealed class SchoolStudentAddressVO
    {
        public string? Id { get; set; }

        public string? StudentId { get; set; }

        public string? Address { get; set; }

        public string? Name { get; set; }

        public SchoolStudentVO? SchoolStudent { get; set; }
    }

    /// <summary>Java 的 SchoolTeacherVO。</summary>
    public sealed class SchoolTeacherVO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>Java 的 SchoolClass1VO（Name1 没有对应的列）。</summary>
    public sealed class SchoolClass1VO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Name1 { get; set; }

        public List<SchoolStudentVO>? SchoolStudents { get; set; }
    }

    /// <summary>Java 的 SchoolClassOnlyVO：学生里没有班级，一路不回头。</summary>
    public sealed class SchoolClassOnlyVO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<SchoolStudentOnlyVO>? SchoolStudents { get; set; }

        public List<SchoolTeacherOnlyVO>? SchoolTeachers { get; set; }
    }

    /// <summary>Java 的 SchoolStudentOnlyVO。</summary>
    public sealed class SchoolStudentOnlyVO
    {
        public string? Id { get; set; }

        public string? ClassId { get; set; }

        public string? Name { get; set; }

        public SchoolStudentAddressOnlyVO? SchoolStudentAddress { get; set; }
    }

    /// <summary>Java 的 SchoolStudentAddressOnlyVO。</summary>
    public sealed class SchoolStudentAddressOnlyVO
    {
        public string? Id { get; set; }

        public string? StudentId { get; set; }

        public string? Address { get; set; }

        public string? Name { get; set; }

        public SchoolStudentOnlyVO? SchoolStudent { get; set; }
    }

    /// <summary>Java 的 SchoolTeacherOnlyVO。</summary>
    public sealed class SchoolTeacherOnlyVO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    // ---------- 共用的期望 SQL 与参数 ----------

    private const string ClassKeys3 = "class1(String),class2(String),class3(String)";
    private const string ClassKeys2 = "class1(String),class2(String)";
    private const string TeacherKeys2 = "teacher1(String),teacher2(String)";
    private const string StudentKeys3 = "1(String),2(String),3(String)";

    private const string ClassRows = "SELECT t.`id`,t.`name` FROM `school_class` t";
    private const string StudentsOfClasses = "SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t WHERE t.`class_id` IN (?,?,?)";
    private const string AddressesOfStudents = "SELECT t.`id`,t.`student_id`,t.`address` FROM `school_student_address` t WHERE t.`student_id` IN (?,?,?)";
    private const string TeacherLinksOfClasses = "SELECT `class_id`,`teacher_id` FROM `school_class_teacher` WHERE `class_id` IN (?,?,?)";
    private const string TeachersOfLinks = "SELECT t.`id`,t.`name` FROM `school_teacher` t WHERE t.`id` IN (?,?)";
    private const string ClassLinksOfTeachers = "SELECT `teacher_id`,`class_id` FROM `school_class_teacher` WHERE `teacher_id` IN (?,?)";
    private const string FlatClassRows = "SELECT t.`name`,t.`id` AS `__relation__id` FROM `school_class` t";

    // 平铺学生 id：集合路径上的标量，单表不写别名
    private static void AssertFlatStudentQuery(SqlCapture.Scope listen, int index)
    {
        listen.AssertSql(index, "SELECT `id`,`class_id` AS `__relation__classId` FROM `school_student` WHERE `class_id` IN (?,?,?)");
        listen.AssertParams(index, ClassKeys3);
    }

    // 老师们带的班级：班级 → 中间表 → 老师 → 中间表 → 班级，每一步一条，最后一条取班级（SQL 因要的列不同而不同，由 classesSql 传入）
    private static void AssertFlatTeacherClassQueries(SqlCapture.Scope listen, int first, string classesSql)
    {
        listen.AssertSql(first, TeacherLinksOfClasses);
        listen.AssertParams(first, ClassKeys3);
        listen.AssertSql(first + 1, "SELECT `id` AS `__relation__id` FROM `school_teacher` WHERE `id` IN (?,?)");
        listen.AssertParams(first + 1, TeacherKeys2);
        listen.AssertSql(first + 2, ClassLinksOfTeachers);
        listen.AssertParams(first + 2, TeacherKeys2);
        listen.AssertSql(first + 3, classesSql);
        listen.AssertParams(first + 3, ClassKeys2);
    }

    // 班级 1：学生 1、3；班级 2：学生 2；班级 3：没有学生也没有老师。老师 1 带班级 1，老师 2 带班级 1、2，所以班级 1、2 的老师们带的班级都是 {class1, class2}
    private static void AssertFlatRow(string? className, List<string?>? studentIds, List<string?>? teacherClassIds = null, List<string?>? teacherClassNames = null,
        List<SchoolClass>? teacherClasses = null)
    {
        string?[] expectedStudents = className switch { "班级1" => ["1", "3"], "班级2" => ["2"], _ => [] };
        Assert.Equal(expectedStudents, studentIds!.Order());
        if (className is not ("班级1" or "班级2")) return;
        if (teacherClassIds is not null) Assert.Equal(["class1", "class2"], teacherClassIds.Order());
        if (teacherClassNames is not null) Assert.Equal(["班级1", "班级2"], teacherClassNames.Order());
        if (teacherClasses is null) return;
        Assert.Equal(2, teacherClasses.Count);
        Assert.Contains(teacherClasses, c => c is { Id: "class1", Name: "班级1" });
        Assert.Contains(teacherClasses, c => c is { Id: "class2", Name: "班级2" });
    }

    // 带出班级的 VO：每个班级都有学生、老师列表，学生都带地址
    private static void AssertClassVos(List<SchoolClassVO> list)
    {
        Assert.NotEmpty(list);
        foreach (var vo in list)
        {
            Assert.NotNull(vo.SchoolStudents);
            Assert.NotNull(vo.SchoolTeachers);
            Assert.All(vo.SchoolStudents, student => Assert.NotNull(student.SchoolStudentAddress));
        }
    }

    private static void AssertClassOnlyVos(List<SchoolClassOnlyVO> list)
    {
        Assert.NotEmpty(list);
        foreach (var vo in list)
        {
            Assert.NotNull(vo.SchoolStudents);
            Assert.NotNull(vo.SchoolTeachers);
            Assert.All(vo.SchoolStudents, student => Assert.NotNull(student.SchoolStudentAddress));
        }
    }

    // 带学生、地址、老师的 VO：班级 → 学生 → 地址 → 中间表 → 老师（teachersSql 是最后一条老师的查询，有的用例给老师加了条件和排序）
    private static void AssertClassVoQueries(SqlCapture.Scope listen, string teachersSql)
    {
        Assert.Equal(5, listen.Events.Count);
        listen.AssertSql(0, ClassRows);
        listen.AssertSql(1, StudentsOfClasses);
        listen.AssertParams(1, ClassKeys3);
        listen.AssertSql(2, AddressesOfStudents);
        listen.AssertParams(2, StudentKeys3);
        listen.AssertSql(3, TeacherLinksOfClasses);
        listen.AssertParams(3, ClassKeys3);
        listen.AssertSql(4, teachersSql);
        listen.AssertParams(4, TeacherKeys2);
    }

    // 带出学生取前两条：每个班级一段（取整个学生实体、按这个班级过滤、取两条），合并成 UNION ALL；Java 的 BaseTest 与 C# 的夹具都用 UNION ALL 取段
    private static string StudentsUnionAll(string conditionBeforeClass, int parts) =>
        "SELECT t1.`id`,t1.`class_id`,t1.`name` FROM ( "
        + string.Join("  UNION ALL  ", Enumerable.Repeat("(SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t WHERE " + conditionBeforeClass + "t.`class_id` = ? LIMIT 2)", parts))
        + " ) t1";

    // 重复出现在 Java 各段末尾的带出：学生，加条件的学生。Java 没有断言 SQL，只核对每个班级都带出了学生列表
    private async Task AssertIncludeStudentsAsync()
    {
        var classes = await Db.Query<SchoolClass>().Include(o => o.SchoolStudents).ToListAsync(Ct);
        await Db.Query<SchoolClass>().Include(o => o.SchoolStudents, students => students.Where(x => x.Name!.Contains("123"))).ToListAsync(Ct);
        Assert.NotEmpty(classes);
        Assert.All(classes, c => Assert.NotNull(c.SchoolStudents));
    }

    // Java 的学生数子查询里 COUNT 列带 AS `id`、且多占一个别名编号（selectColumn 建的匿名表）；C# 的计算列写成 Select(s => s.Id).Count()，不写别名、不跳号
    private static string WithoutCountAlias(string javaSql) => javaSql
        .Replace("`) AS `id` FROM", "`) FROM", StringComparison.Ordinal)
        .Replace("t3", "t2", StringComparison.Ordinal);

    // ---------- 测试 ----------

    // 第 1 段（186-241）：include2 的多段路径（经过集合的 flatElement 再到班级、老师）写成嵌套的 Include，每段的条件写在各自的配置里；同一张学生表的两段合并成一个 Include
    [Fact, Upstream("RelationTest.java#testOneToOne",
         Deviation = "约 80 段拆成本文件的多个测试，Upstream 只标在这里。写法不同：include2 写成嵌套的 Include；flatElement 链的 toList 写成 SelectMany（INNER JOIN、保留重复，不比较 SQL，加 Distinct 与 Java 的去重对齐）。"
                     + "行为不同：selectAutoInclude 的平铺元素是实体时 Java 连它的导航一起带出（27 条查询）、C# 只带实体本身（6 条）；循环引用的 VO（SchoolClassVO ↔ SchoolStudentVO）Java 多展开一层、C# 不展开；"
                     + "Java 的 select(Class) 只映射已 Include 的导航、C# 的 Select<TDto>() 总是自动带出。没有对应的：DTO 定义校验的 3 段异常、联表上带额外列的 selectAutoInclude（Skip，理由见各测试）")]
    public Task TestOneToOne_IncludeFlatPaths() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>()
            .Include(s => s.SchoolTeachers, teachers => teachers.Include(t => t.SchoolClasses, classes => classes.Where(c => c.Name!.Contains("123"))))
            .Include(s => s.SchoolStudents, students => students.Where(st => st.Name != "123")
                .Include(st => st.SchoolClass, owner => owner.Where(c => c.SchoolStudents!.Any(x => x.Name == "123"))))
            .ToListAsync(Ct);
        Assert.Equal(7, listen.Events.Count);
        listen.AssertSql(0, "SELECT `id`,`name` FROM `school_class`");
        listen.AssertSql(1, TeacherLinksOfClasses);
        listen.AssertParams(1, ClassKeys3);
        listen.AssertSql(2, TeachersOfLinks);
        listen.AssertParams(2, TeacherKeys2);
        listen.AssertSql(3, ClassLinksOfTeachers);
        listen.AssertParams(3, TeacherKeys2);
        listen.AssertSql(4, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE t.`name` LIKE ? AND t.`id` IN (?,?)");
        listen.AssertParams(4, "%123%(String),class1(String),class2(String)");
        listen.AssertSql(5, "SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t WHERE t.`name` <> ? AND t.`class_id` IN (?,?,?)");
        listen.AssertParams(5, "123(String),class1(String),class2(String),class3(String)");
        listen.AssertSql(6, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` = ? LIMIT 1) AND t.`id` IN (?,?)");
        listen.AssertParams(6, "123(String),class1(String),class2(String)");
    });

    // 第 2 段（243-302）：先 Include 学生（带地址）和按条件排序的老师，再投影到 VO
    [Fact]
    public Task TestOneToOne_IncludeOrderedTeachersIntoVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>()
            .Include(s => s.SchoolStudents, students => students.Include(st => st.SchoolStudentAddress))
            .Include(s => s.SchoolTeachers, teachers => teachers.Where(t => t.Id != null).OrderBy(t => t.Name))
            .Select<SchoolClassVO>().ToListAsync(Ct);
        AssertClassVoQueries(listen, "SELECT t.`id`,t.`name` FROM `school_teacher` t WHERE t.`id` IS NOT NULL AND t.`id` IN (?,?) ORDER BY t.`name` ASC");
        AssertClassVos(list);
    });

    // 第 3 段（303-393）与第 47 段（2104-2185）：Include 学生（带地址）和老师，再投影到 VO。第 47 段在 Java 里是 include(…).select(SchoolClassVO.class)，两段 SQL 一样
    [Fact]
    public Task TestOneToOne_IncludeStudentsAddressAndTeachersIntoVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>()
            .Include(s => s.SchoolStudents, students => students.Include(st => st.SchoolStudentAddress))
            .Include(s => s.SchoolTeachers)
            .Select<SchoolClassVO>().ToListAsync(Ct);
        AssertClassVoQueries(listen, TeachersOfLinks);
        AssertClassVos(list);
    });

    // 第 4 段（394-419）：学生的地址包含“绍兴市”，contains 在 Java 里是 LIKE CONCAT('%',?,'%')（不是 like），C# 写 Sql.LikeContains
    [Fact]
    public Task TestOneToOne_ExistsAddressLikeContains() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().Where(s => s.SchoolStudents!.Any(x => Sql.LikeContains(x.SchoolStudentAddress!.Address, "绍兴市"))).ToListAsync(Ct);
        Assert.Single(listen.Events);
        listen.AssertSql(0, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE EXISTS(SELECT 1 FROM `school_student` t1 LEFT JOIN `school_student_address` t2 ON t2.`student_id` = t1.`id` WHERE t1.`class_id` = t.`id` AND t2.`address` LIKE CONCAT('%',?,'%') LIMIT 1)");
        listen.AssertParams(0, "绍兴市(String)");
    });

    // 第 5 段（420-464）与第 29 段（1437-1470）：沿学生、地址、学生、地址展开。偏离：Java 的 toList(flatElement…) 先只查关联键、再逐层带出（5 条与 3 条查询），
    // C# 的 SelectMany 加对一导航的投影是一条 JOIN；只核对结果
    [Fact]
    public Task TestOneToOne_SelectManyStudentChains() => WithSchoolDataAsync(async _ =>
    {
        var looped = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolStudents)
            .Select(st => st.SchoolStudentAddress!.SchoolStudent!.SchoolStudentAddress).ToListAsync(Ct);
        Assert.Equal(["address1", "address2", "address3"], looped.Select(a => a!.Id).Order());
        var addresses = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolStudents).Select(st => st.SchoolStudentAddress).ToListAsync(Ct);
        Assert.Equal(["address1", "address2", "address3"], addresses.Select(a => a!.Id).Order());
    });

    // 第 6 段（465-485）：带出的学生子查询只选 name，关联键另外带出
    [Fact]
    public Task TestOneToOne_IncludeSelectedColumns() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().Include(s => s.SchoolStudents, students => students.Select(o => new SchoolStudent { Name = o.Name })).ToListAsync(Ct);
        listen.AssertSql(0, "SELECT `id`,`name` FROM `school_class`");
        listen.AssertSql(1, "SELECT t.`name`,t.`class_id` AS `__relation__classId` FROM `school_student` t WHERE t.`class_id` IN (?,?,?)");
        listen.AssertParams(1, ClassKeys3);
    });

    // 第 7 段（487-499）：班级声明成一对一，而学生 1、3 都在 class1，Java 在带出时报“关联值重复，请确认是一对一还是一对多”
    [Fact(Skip = "缺：Java 带出一对一时校验关联值有没有重复（EasyQueryInvalidOperationException），C# 没有这项校验")]
    public Task TestOneToOne_DuplicateRelationValueThrows() => WithSchoolDataAsync(async _ =>
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<SchoolStudent2>().Select<SchoolStudentDTOAO222>().ToListAsync(Ct));
    });

    // 第 8 段（500-547）、第 9 段（549-596）、第 10 段（597-643）、第 14 段（704-750）：老师们带的班级平铺成只有 id 的对象列表。
    // 四段 SQL 一样，8 的 Java 另有 configure(setPrintNavSQL(false))（只管日志，C# 没有对应）
    [Fact]
    public Task TestOneToOne_FlatClassesOfTeachersIdOnly() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp5>().ToListAsync(Ct);
        Assert.NotEmpty(list.SelectMany(c => c.SchoolTeachersClassList!));
        Assert.Equal(5, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        AssertFlatTeacherClassQueries(listen, 1, "SELECT t.`id` FROM `school_class` t WHERE t.`id` IN (?,?)");
    });

    // 第 11 段（644-673）：学生的地址平铺、班级嵌套成对象
    [Fact]
    public Task TestOneToOne_AutoIncludeStudentWithFlatAddress() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolStudent>().Select<SchoolStudentDTOAO111>().ToListAsync(Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t");
        listen.AssertSql(1, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE t.`id` IN (?,?)");
        listen.AssertParams(1, ClassKeys2);
        listen.AssertSql(2, "SELECT `address`,`student_id` AS `__relation__studentId` FROM `school_student_address` WHERE `student_id` IN (?,?,?)");
        listen.AssertParams(2, StudentKeys3);
        Assert.All(list, s => Assert.Equal("地址" + s.Id, s.Address));
        Assert.All(list, s => Assert.Equal(s.ClassId, s.SchoolClass!.Id));
    });

    // 第 12 段（675-685）与第 30–33 段（1514-1675）：沿老师、班级展开（flatElement 链的 toList）。偏离：Java 先只查键、逐层带出并在内存里去重（5 条与 3 条查询），
    // C# 的 SelectMany 是 INNER JOIN、保留重复，加 Distinct 与 Java 的去重对齐；只核对结果
    [Fact]
    public Task TestOneToOne_SelectManyTeacherChains() => WithSchoolDataAsync(async _ =>
    {
        var teacherNames = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolTeachers).Select(t => t.Name).Distinct().ToListAsync(Ct);
        Assert.Equal(["老师1", "老师2"], teacherNames.Order());

        // 第 30 段：flatElement(z -> z.FETCHER.name().id().name())，Java 的列写了两次 name，这里取 name、id
        var fetched = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolTeachers).SelectMany(t => t.SchoolClasses)
            .Select(c => new SchoolClass { Name = c.Name, Id = c.Id }).Distinct().ToListAsync(Ct);
        Assert.Equal(["class1", "class2"], fetched.Select(c => c.Id).Order());

        // 第 31 段
        var classNames = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolTeachers).SelectMany(t => t.SchoolClasses).Select(c => c.Name).Distinct().ToListAsync(Ct);
        Assert.Equal(["班级1", "班级2"], classNames.Order());

        // 第 32 段
        var classes = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolTeachers).SelectMany(t => t.SchoolClasses).Distinct().ToListAsync(Ct);
        Assert.Equal(["class1", "class2"], classes.Select(c => c.Id).Order());

        // 第 33 段
        var teachers = await Db.Query<SchoolClass>().SelectMany(c => c.SchoolTeachers).Distinct().ToListAsync(Ct);
        Assert.Equal(["teacher1", "teacher2"], teachers.Select(t => t.Id).Order());
    });

    // 第 13 段（686-702）：同一条路径既平铺字段（班级 id、名字）、又整个平铺成非实体对象，Java 报 EasyQueryInvalidOperationException
    [Fact(Skip = "缺：Java 校验 NavigateFlat 不能同时包含非数据库对象和它的属性，C# 没有这项校验（两种写法在 C# 里各自独立带出）")]
    public Task TestOneToOne_FlatAndObjectOnSamePathThrows() => WithSchoolDataAsync(async _ =>
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<SchoolClass>().Select<SchoolClassAOProp12>().ToListAsync(Ct));
    });

    // 第 15 段（751-767）：班级既是平铺的 SchoolClass.Name、又是整个 SchoolClass 属性，Java 报“应同时出现在 @Navigate 和 @NavigateFlat 里”
    [Fact(Skip = "缺：Java 校验 selectAutoInclude 的关联属性要同时出现在 Navigate 与 NavigateFlat 里，C# 没有这项校验")]
    public Task TestOneToOne_NavigateAndFlatMismatchThrows() => WithSchoolDataAsync(async _ =>
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Db.Query<SchoolStudent>().Select<SchoolStudentDTOxxx>().ToListAsync(Ct));
    });

    // 第 16 段（768-841）：平铺学生 id、经主题 JOIN 的星数、老师们带的班级（名字与整个实体）。
    // 偏离：Java 里平铺元素是 SchoolClass 实体时连它的导航一起带出（27 条查询），C# 只带实体本身（6 条）。
    // 同一条路径上的几个平铺属性（班级名字、班级实体）合并成一组带出查询，取实体；集合路径上的标量单表不写别名
    [Fact]
    public Task TestOneToOne_FlatPathsWithTopicJoinAndEntityList() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp9>().ToListAsync(Ct);
        Assert.Equal(6, listen.Events.Count);
        listen.AssertSql(0, "SELECT t1.`stars` AS `name1`,t.`name`,t.`id` AS `__relation__id` FROM `school_class` t LEFT JOIN `t_topic` t1 ON t1.`id` = t.`id`");
        AssertFlatStudentQuery(listen, 1);
        AssertFlatTeacherClassQueries(listen, 2, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE t.`id` IN (?,?)");
        foreach (var p in list)
        {
            Assert.Null(p.Name1);
            AssertFlatRow(p.Name, p.SchoolStudentsIds, teacherClassNames: p.SchoolTeachersClassId1s, teacherClasses: p.SchoolTeachersClassList);
        }
    });

    // 第 17 段（843-922）：同上，主查询按主题条件过滤（or 写成 ||）
    [Fact]
    public Task TestOneToOne_FlatPathsWithTopicJoinAndFilter() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Where(s => s.Topic!.Title != "123xxxxx123" || s.Topic!.Id == null).Select<SchoolClassAOProp9>().ToListAsync(Ct);
        Assert.Equal(6, listen.Events.Count);
        listen.AssertSql(0, "SELECT t1.`stars` AS `name1`,t.`name`,t.`id` AS `__relation__id` FROM `school_class` t LEFT JOIN `t_topic` t1 ON t1.`id` = t.`id` WHERE (t1.`title` <> ? OR t1.`id` IS NULL)");
        listen.AssertParams(0, "123xxxxx123(String)");
        AssertFlatStudentQuery(listen, 1);
        AssertFlatTeacherClassQueries(listen, 2, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE t.`id` IN (?,?)");
        foreach (var p in list)
        {
            Assert.Null(p.Name1);
            AssertFlatRow(p.Name, p.SchoolStudentsIds, teacherClassNames: p.SchoolTeachersClassId1s, teacherClasses: p.SchoolTeachersClassList);
        }
    });

    // 第 18 段（923-992）与第 26 段（1265-1334）：平铺学生 id、老师们带的班级的 id 和名字。第 18 段的学生 id 路径在 Java 里是 MappingPath 常量，C# 里同样是 MapPath。
    // 同一条路径上的两个平铺属性（班级 id、班级名字）合并成一组带出查询（6 条），只取这两列
    [Fact]
    public Task TestOneToOne_FlatPathsThroughTeachers() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp3>().ToListAsync(Ct);
        Assert.Equal(6, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        AssertFlatStudentQuery(listen, 1);
        AssertFlatTeacherClassQueries(listen, 2, "SELECT `id`,`name` FROM `school_class` WHERE `id` IN (?,?)");
        foreach (var p in list) AssertFlatRow(p.Name, p.SchoolStudentsIds, p.SchoolTeachersClassIds, p.SchoolTeachersClassId1s);
    });

    // 第 19 段（993-1021）与第 20 段（1022-1050）：学生上的地址是 JOIN 列、班级名是带出。
    // 对一路径放进 List<string> 属性（班级名字）时，列表里只有这一个值
    [Fact]
    public Task TestOneToOne_FlatNavigateJoinInNestedDto() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().Select<SchoolClassAOProp10>().ToListAsync(Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        listen.AssertSql(1, "SELECT t.`id`,t.`name`,t1.`address` AS `stu_address`,t.`class_id` AS `__relation__classId` FROM `school_student` t LEFT JOIN `school_student_address` t1 ON t1.`student_id` = t.`id` WHERE t.`class_id` IN (?,?,?)");
        listen.AssertParams(1, ClassKeys3);
        listen.AssertSql(2, "SELECT `name`,`id` AS `__relation__id` FROM `school_class` WHERE `id` IN (?,?)");
        listen.AssertParams(2, ClassKeys2);
    });

    // 第 21 段（1052-1057）：用学生的地址列填班级 VO（Java 的 select(SchoolClassVO.class, x -> x.schoolStudentAddress().FETCHER.allFields())，没有断言），
    // 同名的只有 id；C# 写成显式投影
    [Fact]
    public Task TestOneToOne_SelectVoFromAddressColumns() => WithSchoolDataAsync(async _ =>
    {
        var list = await Db.Query<SchoolStudent>().Where(s => s.Name!.Contains("123")).Select(s => new SchoolClassVO { Id = s.SchoolStudentAddress!.Id }).ToListAsync(Ct);
        Assert.Empty(list);
    });

    // 第 22 段（1058-1087）：学生上平铺班级名放进列表，Java 断言不报错、有学生的班级里至少一个学生有班级名。
    // 对一路径放进 List<string> 属性
    [Fact]
    public Task TestOneToOne_FlatToOneIntoList() => WithSchoolDataAsync(async _ =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp8>().ToListAsync(Ct);
        Assert.Contains(list, c => c.SchoolStudents!.Count > 0);
        foreach (var students in list.Select(c => c.SchoolStudents!).Where(s => s.Count > 0))
            Assert.Contains(students, s => s.ClassNames!.Count > 0);
    });

    // 第 23 段（1088-1108）：只取对一导航上的一列
    [Fact]
    public Task TestOneToOne_SelectAddressThroughNavigation() => WithSchoolDataAsync(async listen =>
    {
        var addresses = await Db.Query<SchoolStudent>().Select(s => s.SchoolStudentAddress!.Address).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`address` FROM `school_student` t LEFT JOIN `school_student_address` t1 ON t1.`student_id` = t.`id`");
        Assert.Equal(3, addresses.Count);
    });

    // 第 24 段（1109-1185）：班级左连主题，额外列 t2.stars 的空值默认 1 写进 name1。下面的写法只是对照：C# 的 Query 联表后没有 Select<TDto>()，
    // 额外列只能用 [MapExpression] 引用源实体（不能引用联表的 t2），期望 SQL 里的 ON t.id = t1.id 也不是隐式导航 JOIN 的写法
    [Fact(Skip = "缺：联表查询上没有 Select<TDto>()（Java 的 selectAutoInclude(X.class, (s, t2) -> Select.of(t2.stars().nullOrDefault(1).as(…)))）")]
    public Task TestOneToOne_JoinWithExtraColumnIntoDto() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp6>().ToListAsync(Ct);
        Assert.Equal(6, listen.Events.Count);
        listen.AssertSql(0, "SELECT IFNULL(t1.`stars`,?) AS `name1`,t.`name`,t.`id` AS `__relation__id` FROM `school_class` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id`");
        listen.AssertParams(0, "1(Integer)");
        AssertFlatStudentQuery(listen, 1);
        AssertFlatTeacherClassQueries(listen, 2, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE t.`id` IN (?,?)");
        foreach (var p in list)
        {
            Assert.Equal("1", p.Name1);
            AssertFlatRow(p.Name, p.SchoolStudentsIds, teacherClassNames: p.SchoolTeachersClassId1s, teacherClasses: p.SchoolTeachersClassList);
        }
    });

    // 第 25 段（1186-1264）：平铺学生 id、班级 id、班级名字、班级实体。偏离同第 16 段（Java 27 条，C# 实体只带本身）
    [Fact]
    public Task TestOneToOne_FlatPathsWithEntityListAndNames() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp4>().ToListAsync(Ct);
        Assert.Equal(6, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        AssertFlatStudentQuery(listen, 1);
        AssertFlatTeacherClassQueries(listen, 2, "SELECT t.`id`,t.`name` FROM `school_class` t WHERE t.`id` IN (?,?)");
        foreach (var p in list)
            AssertFlatRow(p.Name, p.SchoolStudentsIds, p.SchoolTeachersClassIds, p.SchoolTeachersClassId1s, p.SchoolTeachersClassList);
    });

    // 第 27 段（1335-1399）：平铺学生 id 和班级 id
    [Fact]
    public Task TestOneToOne_FlatPathsThroughTeachersIds() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp2>().ToListAsync(Ct);
        Assert.Equal(6, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        AssertFlatStudentQuery(listen, 1);
        AssertFlatTeacherClassQueries(listen, 2, "SELECT `id` FROM `school_class` WHERE `id` IN (?,?)");
        foreach (var p in list) AssertFlatRow(p.Name, p.SchoolStudentsIds, p.SchoolTeachersClassIds);
    });

    // 第 28 段（1400-1436）：只平铺学生 id
    [Fact]
    public Task TestOneToOne_FlatStudentIds() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassAOProp>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        AssertFlatStudentQuery(listen, 1);
        foreach (var p in list) AssertFlatRow(p.Name, p.SchoolStudentsIds);
    });

    // 第 34 段（1677-1694）：班级名过滤，投影成学生名含 456 的计数（Draft 的列别名 value1）
    [Fact]
    public Task TestOneToOne_StudentCountDraft() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().Where(s => s.Name!.Contains("123"))
            .Select(s => new Draft<long>(s.SchoolStudents!.Where(st => st.Name!.Contains("456")).LongCount())).ToListAsync(Ct);
        Assert.Single(listen.Events);
        listen.AssertSql(0, "SELECT (SELECT COUNT(*) FROM `school_student` t1 WHERE t1.`class_id` = t.`id` AND t1.`name` LIKE ?) AS `value1` FROM `school_class` t WHERE t.`name` LIKE ?");
        listen.AssertParams(0, "%456%(String),%123%(String)");
    });

    // 第 35 段（1696-1700）：先查出学生，再给这些学生加载班级
    [Fact]
    public Task TestOneToOne_LoadIncludeClassOfStudents() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().ToListAsync(Ct);
        await Db.LoadIncludeAsync(students, x => x.SchoolClass, Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s => Assert.NotNull(s.SchoolClass));
    });

    // 第 36 段（1718-1771）与第 37 段（1773-1816）：先查出班级，再给这些班级加载学生（每个班级学生数：2、1、0）。
    // 第 37 段在 Java 里用 easyQueryClient.loadInclude(list, "schoolStudents")，按属性名字符串加载，C# 没有这个写法，用同一个 LoadIncludeAsync；末尾的两次 Include 查询两段相同
    [Fact]
    public Task TestOneToOne_LoadIncludeStudentsOfClasses() => WithSchoolDataAsync(async listen =>
    {
        var classes = await Db.Query<SchoolClass>().ToListAsync(Ct);
        await Db.LoadIncludeAsync(classes, x => x.SchoolStudents, Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT `id`,`name` FROM `school_class`");
        listen.AssertSql(1, "SELECT `id`,`class_id`,`name` FROM `school_student` WHERE `class_id` IN (?,?,?)");
        listen.AssertParams(1, ClassKeys3);
        Assert.Equal(2, classes.Single(c => c.Id == "class1").SchoolStudents!.Count);
        Assert.Single(classes.Single(c => c.Id == "class2").SchoolStudents!);
        Assert.Empty(classes.Single(c => c.Id == "class3").SchoolStudents!);
        await AssertIncludeStudentsAsync();
    });

    // 第 38 段（1818-1862）与第 39 段（1864-1908）：Include 学生再投影到 DTO（Java 里 include(s -> s.with("schoolStudents")) 按属性名字符串，C# 写 Include(lambda)，两段一样）
    [Fact]
    public Task TestOneToOne_IncludeThenAutoIncludeDto() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().Include(s => s.SchoolStudents).Select<SchoolClassAO>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, FlatClassRows);
        listen.AssertSql(1, "SELECT t.`id`,t.`name`,t.`class_id` AS `__relation__classId` FROM `school_student` t WHERE t.`class_id` IN (?,?,?)");
        listen.AssertParams(1, ClassKeys3);
        await AssertIncludeStudentsAsync();
    });

    // 第 40 段（1925-1939）与第 41 段（1940-1951）：计算列学生数，在 SELECT 里写成子查询、在条件里展开。
    // 偏离：别名编号与 COUNT 的 AS 同 QueryTest15（Java 的计数子查询多占一个别名、COUNT 列带 AS `id`）；C# 里 WHERE 的子查询与 SELECT 的子查询谁先拿别名，按先翻译 WHERE 推测
    [Fact]
    public Task TestOneToOne_ComputedStudentSize() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClassAggregate>().Where(s => s.StudentSize > 0L).ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t.`id`,t.`name`,(SELECT COUNT(t3.`id`) AS `id` FROM `school_student` t3 WHERE t3.`class_id` = t.`id`) AS `student_size` FROM `school_class` t WHERE (SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) > ?"));
        await Db.Query<SchoolClassAggregate>().ToListAsync(Ct);
        listen.AssertSql(WithoutCountAlias("SELECT t.`id`,t.`name`,(SELECT COUNT(t1.`id`) AS `id` FROM `school_student` t1 WHERE t1.`class_id` = t.`id`) AS `student_size` FROM `school_class` t"));
    });

    // 第 42 段（1952-1991）：带出学生按条件过滤、每个班级取两条，再投影到 VO。没有学生满足条件，所以学生下面的地址不再查询（共 2 条）
    [Fact]
    public Task TestOneToOne_IncludeLimitedStudentsIntoVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>()
            .Include(s => s.SchoolStudents, students => students.Where(st => st.Name!.Contains("123")).Take(2))
            .Select<SchoolClass1VO>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, ClassRows);
        listen.AssertSql(1, StudentsUnionAll("t.`name` LIKE ? AND ", 3));
        listen.AssertParams(1, "%123%(String),class1(String),%123%(String),class2(String),%123%(String),class3(String)");
        Assert.All(list, c => Assert.Empty(c.SchoolStudents!));
    });

    // 第 44 段（2004-2069）：自动带出班级 VO。偏离：Java 的 selectAutoInclude 会在学生的班级、地址所属的学生处多展开一层（9 条，第 2、3、4、6 条），
    // C# 遇到引用到自己的 DTO 就不展开，只有 5 条（对应 Java 的第 0、1、5、7、8 条）
    [Fact]
    public Task TestOneToOne_AutoIncludeClassVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassVO>().ToListAsync(Ct);
        AssertClassVoQueries(listen, TeachersOfLinks);
        AssertClassVos(list);
    });

    // 第 45 段（2070-2086）与第 46 段（2087-2103）：Include 学生再投影成 VO。偏离：Java 的 select(Class) 只映射已 Include 的导航，所以最后一条就是学生；
    // C# 的 Select<TDto>() 总是自动带出地址、老师，学生是第 2 条
    [Fact]
    public Task TestOneToOne_IncludeStudentsThenSelectVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Include(s => s.SchoolStudents).Select<SchoolClassVO>().ToListAsync(Ct);
        listen.AssertSql(1, StudentsOfClasses);
        listen.AssertParams(1, ClassKeys3);
        Assert.All(list, vo => Assert.NotNull(vo.SchoolStudents));
    });

    // 第 48 段（2186-2240）：自动带出不回头的 VO。偏离：Java 在地址所属的学生处多展开一层（6 条，第 3 条），C# 遇到引用到自己的 DTO 不展开，5 条
    [Fact]
    public Task TestOneToOne_AutoIncludeOnlyVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>().Select<SchoolClassOnlyVO>().ToListAsync(Ct);
        Assert.Equal(5, listen.Events.Count);
        listen.AssertSql(0, ClassRows);
        listen.AssertSql(1, StudentsOfClasses);
        listen.AssertParams(1, ClassKeys3);
        listen.AssertSql(2, AddressesOfStudents);
        listen.AssertParams(2, StudentKeys3);
        listen.AssertSql(3, TeacherLinksOfClasses);
        listen.AssertParams(3, ClassKeys3);
        listen.AssertSql(4, TeachersOfLinks);
        listen.AssertParams(4, TeacherKeys2);
        AssertClassOnlyVos(list);
    });

    // 第 49 段（2242-2302）：Include 学生（带地址）和老师，再投影成不回头的 VO
    [Fact]
    public Task TestOneToOne_IncludeThenSelectOnlyVo() => WithSchoolDataAsync(async listen =>
    {
        var list = await Db.Query<SchoolClass>()
            .Include(s => s.SchoolStudents, students => students.Include(st => st.SchoolStudentAddress))
            .Include(s => s.SchoolTeachers)
            .Select<SchoolClassOnlyVO>().ToListAsync(Ct);
        Assert.Equal(5, listen.Events.Count);
        listen.AssertSql(0, ClassRows);
        listen.AssertSql(1, StudentsOfClasses);
        listen.AssertParams(1, ClassKeys3);
        listen.AssertSql(2, AddressesOfStudents);
        listen.AssertParams(2, StudentKeys3);
        listen.AssertSql(3, TeacherLinksOfClasses);
        listen.AssertParams(3, ClassKeys3);
        listen.AssertSql(4, TeachersOfLinks);
        listen.AssertParams(4, TeacherKeys2);
        AssertClassOnlyVos(list);
    });

    // 第 50 段（2304-2316）：投影成学生的班级对象
    [Fact]
    public Task TestOneToOne_SelectClassOfStudents() => WithSchoolDataAsync(async listen =>
    {
        var classes = await Db.Query<SchoolStudent>().Select(s => s.SchoolClass).ToListAsync(Ct);
        listen.AssertSql("SELECT t1.`id`,t1.`name` FROM `school_student` t LEFT JOIN `school_class` t1 ON t1.`id` = t.`class_id`");
        Assert.Equal(3, classes.Count);
    });

    // 第 51 段（2317-2334）：Include 学生之后只选班级的 name。投影回同一实体时 Include 照样带出（最后一条是学生的查询），
    // 带出查询按 loadInclude 的格式，单表不写别名
    [Fact]
    public Task TestOneToOne_IncludeThenSelectName() => WithSchoolDataAsync(async listen =>
    {
        var query = Db.Query<SchoolClass>().Include(s => s.SchoolStudents).Select(s => new SchoolClass { Name = s.Name });
        await query.ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`class_id`,`name` FROM `school_student` WHERE `class_id` IN (?,?,?)");
        listen.AssertParams(ClassKeys3);
        Assert.Equal("SELECT t.`name`,t.`id` AS `__relation__id` FROM `school_class` t", query.ToSql());
    });

    // 第 52 段（2335-2348）与第 53 段（2349-2362）：Include 学生取前两条（每个班级一段 UNION ALL）、Include 学生加条件
    [Fact]
    public Task TestOneToOne_IncludeLimitAndFilter() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().Include(o => o.SchoolStudents, students => students.Take(2)).ToListAsync(Ct);
        listen.AssertSql(StudentsUnionAll("", 3));
        listen.AssertParams(ClassKeys3);
        await Db.Query<SchoolClass>().Include(o => o.SchoolStudents, students => students.Where(y => y.Name!.Contains("123"))).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t WHERE t.`name` LIKE ? AND t.`class_id` IN (?,?,?)");
        listen.AssertParams("%123%(String),class1(String),class2(String),class3(String)");
    });

    // 第 54 段（2363-2376）：Include 学生取前两条，每组只带 1 个班级（Java 的第三个参数）。最后一组只有 class3：Java 写成 IN (?) LIMIT 2。
    // UNION ALL 取段只有一个父键时整条查询直接取段
    [Fact]
    public Task TestOneToOne_IncludeLimitWithGroupSizeOne() => WithSchoolDataAsync(async listen =>
    {
        await Db.Query<SchoolClass>().IncludeGroupSize(1).Include(o => o.SchoolStudents, students => students.Take(2)).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`id`,t.`class_id`,t.`name` FROM `school_student` t WHERE t.`class_id` IN (?) LIMIT 2");
        listen.AssertParams("class3(String)");
    });

    // 第 55 段（2377-2383）与重复的 2384-2390：没有 Include 时导航不带出
    [Fact]
    public Task TestOneToOne_WithoutIncludeNavigationIsNull() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().ToListAsync(Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s => Assert.Null(s.SchoolStudentAddress));
    });

    // 第 56 段（2393-2401）：每组只带 1 个学生的班级（Java 的 include(…, 1)，C# 的 IncludeGroupSize(1)）
    [Fact]
    public Task TestOneToOne_IncludeClassWithGroupSizeOne() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().IncludeGroupSize(1).Include(o => o.SchoolClass).ToListAsync(Ct);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.Equal(s.ClassId, s.SchoolClass.Id);
        });
    });

    // 第 57 段（2402-2416）：不走拦截器的 Include（Java 的 noInterceptor，C# 的 DisableInterceptors）
    [Fact]
    public Task TestOneToOne_IncludeWithoutInterceptors() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().DisableInterceptors().Include(t => t.SchoolClass).ToListAsync(Ct);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.Equal(s.ClassId, s.SchoolClass.Id);
        });
    });

    // 第 58 段（2417-2433）与第 59 段（2434-2448）：Include 班级之后投影成学生 VO（Java 的 select 里 selectAll 加 schoolClass().set(…)，C# 的自动映射对应同名导航），
    // 两段写法不同、效果一样
    [Fact]
    public Task TestOneToOne_IncludeClassThenSelectStudentVo() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().Include(o => o.SchoolClass).Select<SchoolStudentVO>().ToListAsync(Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.Equal(s.ClassId, s.SchoolClass.Id);
            Assert.NotNull(s.SchoolClass.Name);
        });
    });

    // 第 60 段（2449-2470）：Include 班级时配置不追踪、关逻辑删除；每组 20 个；再投影成学生 VO
    [Fact]
    public Task TestOneToOne_IncludeClassWithConfigAndGroupSize() => WithSchoolDataAsync(async _ =>
    {
        var noTracking = await Db.Query<SchoolStudent>().Include(o => o.SchoolClass, classes => classes.AsNoTracking().DisableLogicDelete()).ToListAsync(Ct);
        var grouped = await Db.Query<SchoolStudent>().IncludeGroupSize(20).Include(o => o.SchoolClass).ToListAsync(Ct);
        var students = await Db.Query<SchoolStudent>().Include(o => o.SchoolClass).Select<SchoolStudentVO>().ToListAsync(Ct);
        Assert.All(noTracking, s => Assert.NotNull(s.SchoolClass));
        Assert.All(grouped, s => Assert.NotNull(s.SchoolClass));
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.Equal(s.ClassId, s.SchoolClass.Id);
            Assert.NotNull(s.SchoolClass.Name);
        });
    });

    // 第 61 段（2471-2485）：同上，每组 1 个
    [Fact]
    public Task TestOneToOne_IncludeClassGroupSizeOneThenSelectStudentVo() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().IncludeGroupSize(1).Include(s => s.SchoolClass).Select<SchoolStudentVO>().ToListAsync(Ct);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.Equal(s.ClassId, s.SchoolClass.Id);
            Assert.NotNull(s.SchoolClass.Name);
        });
    });

    // 第 62 段（2486-2504）：学生 VO 里的班级只设 id（Java 的 schoolClass().set(s.schoolClass(), (self, target) -> self.id().set(target.id()))），
    // C# 写成显式投影，班级的列走 JOIN，不需要 Include；班级的名字是 null
    [Fact]
    public Task TestOneToOne_SelectStudentVoWithClassIdOnly() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>()
            .Select(s => new SchoolStudentVO { Id = s.Id, ClassId = s.ClassId, Name = s.Name, SchoolClass = new SchoolClassVO { Id = s.SchoolClass!.Id } })
            .ToListAsync(Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.Equal(s.ClassId, s.SchoolClass.Id);
            Assert.Null(s.SchoolClass.Name);
        });
    });

    // 第 63 段（2505-2512）：Include 地址时配置追踪、关逻辑删除
    [Fact]
    public Task TestOneToOne_IncludeAddressWithTrackingConfig() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().Include(s => s.SchoolStudentAddress, addresses => addresses.AsTracking().DisableLogicDelete()).ToListAsync(Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s => Assert.NotNull(s.SchoolStudentAddress));
    });

    // 第 64 段（2513-2525）与第 65 段（2526-2537）：Include 地址（追踪 / 不追踪、关逻辑删除）之后投影成学生 VO；两段 SQL 一样，只差追踪选项，C# 里不追踪是默认
    [Fact]
    public Task TestOneToOne_IncludeAddressThenSelectStudentVo() => WithSchoolDataAsync(async _ =>
    {
        var tracked = await Db.Query<SchoolStudent>().Include(o => o.SchoolStudentAddress, addresses => addresses.AsTracking().DisableLogicDelete()).Select<SchoolStudentVO>().ToListAsync(Ct);
        var untracked = await Db.Query<SchoolStudent>().Include(s => s.SchoolStudentAddress, addresses => addresses.AsNoTracking().DisableLogicDelete()).Select<SchoolStudentVO>().ToListAsync(Ct);
        Assert.All(tracked, s => Assert.NotNull(s.SchoolStudentAddress));
        Assert.All(untracked, s => Assert.NotNull(s.SchoolStudentAddress));
    });

    // 第 66 段（2538-2551）与第 67 段（2553-2563）：Include 学生、加条件的 Include、每组 1 个的 Include，每个班级都带出学生列表
    [Fact]
    public Task TestOneToOne_IncludeStudentsLists() => WithSchoolDataAsync(async _ =>
    {
        await AssertIncludeStudentsAsync();
        var grouped = await Db.Query<SchoolClass>().IncludeGroupSize(1).Include(s => s.SchoolStudents).ToListAsync(Ct);
        Assert.NotEmpty(grouped);
        Assert.All(grouped, c => Assert.NotNull(c.SchoolStudents));
    });

    // 第 68 段（2564-2577）：Include 学生之后投影成班级 VO（Java 的 select 里 selectAll 加 schoolStudents().set(…)）
    [Fact]
    public Task TestOneToOne_IncludeStudentsThenSelectClassVo() => WithSchoolDataAsync(async _ =>
    {
        var classes = await Db.Query<SchoolClass>().Include(s => s.SchoolStudents).Select<SchoolClassVO>().ToListAsync(Ct);
        Assert.NotEmpty(classes);
        Assert.All(classes, c => Assert.NotNull(c.SchoolStudents));
    });

    // 第 69 段（2579-2588）：Include 地址和班级
    [Fact]
    public Task TestOneToOne_IncludeAddressAndClass() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().Include(s => s.SchoolStudentAddress).Include(s => s.SchoolClass).ToListAsync(Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolClass);
            Assert.NotNull(s.SchoolStudentAddress);
            Assert.Equal(s.Id, s.SchoolStudentAddress.StudentId);
        });
    });

    // 第 70 段（2589-2599）：地址上再 Include 回学生，同时 Include 班级
    [Fact]
    public Task TestOneToOne_IncludeNestedOneToOne() => WithSchoolDataAsync(async _ =>
    {
        var student = await Db.Query<SchoolStudent>()
            .Include(s => s.SchoolStudentAddress, addresses => addresses.Include(x => x.SchoolStudent))
            .Include(s => s.SchoolClass)
            .Where(o => o.Id == "3").FirstOrDefaultAsync(Ct);
        Assert.NotNull(student);
        Assert.NotNull(student.SchoolStudentAddress);
        Assert.NotNull(student.SchoolStudentAddress.SchoolStudent);
        Assert.NotNull(student.SchoolClass);
    });

    // 第 71 段（2600-2608）：Include 地址时加了没有数据满足的条件，地址为 null
    [Fact]
    public Task TestOneToOne_IncludeAddressFilteredToNothing() => WithSchoolDataAsync(async _ =>
    {
        var student = await Db.Query<SchoolStudent>()
            .Include(s => s.SchoolStudentAddress, addresses => addresses.Where(x => x.Id == "x"))
            .Include(s => s.SchoolClass)
            .Where(o => o.Id == "3").FirstOrDefaultAsync(Ct);
        Assert.NotNull(student);
        Assert.Null(student.SchoolStudentAddress);
    });

    // 第 72 段（2609-2619）：Include 地址，同时内连班级
    [Fact]
    public Task TestOneToOne_IncludeWithInnerJoin() => WithSchoolDataAsync(async _ =>
    {
        var students = await Db.Query<SchoolStudent>().Include(t => t.SchoolStudentAddress).InnerJoin<SchoolClass>((t1, t2) => t1.ClassId == t2.Id).ToListAsync(Ct);
        Assert.Equal(3, students.Count);
        Assert.All(students, s =>
        {
            Assert.NotNull(s.SchoolStudentAddress);
            Assert.Equal(s.Id, s.SchoolStudentAddress.StudentId);
        });
    });

    // 第 73 段（2621-2629）：每组 1 个班级的老师 Include
    [Fact]
    public Task TestOneToOne_IncludeTeachersWithGroupSizeOne() => WithSchoolDataAsync(async _ =>
    {
        var classes = await Db.Query<SchoolClass>().IncludeGroupSize(1).Include(o => o.SchoolTeachers).ToListAsync(Ct);
        Assert.NotEmpty(classes);
        Assert.All(classes, c => Assert.NotNull(c.SchoolTeachers));
    });

    // 第 74 段（2657-2677）：Include 老师和学生，班级 1 有 2 位老师、班级 2 有 1 位、班级 3 没有
    [Fact]
    public Task TestOneToOne_IncludeTeachersAndStudents() => WithSchoolDataAsync(async _ =>
    {
        var classes = await Db.Query<SchoolClass>().Include(o => o.SchoolTeachers).Include(o => o.SchoolStudents).ToListAsync(Ct);
        Assert.Equal(3, classes.Count);
        Assert.All(classes, c => Assert.NotNull(c.SchoolStudents));
        Assert.Equal(2, classes.Single(c => c.Id == "class1").SchoolTeachers!.Count);
        Assert.Single(classes.Single(c => c.Id == "class2").SchoolTeachers!);
        Assert.Empty(classes.Single(c => c.Id == "class3").SchoolTeachers!);
    });

    // 第 75 段（2679-2692）：老师上 Include 班级，老师 1 带 1 个班级、老师 2 带 2 个
    [Fact]
    public Task TestOneToOne_IncludeClassesOfTeachers() => WithSchoolDataAsync(async _ =>
    {
        var teachers = await Db.Query<SchoolTeacher>().Include(o => o.SchoolClasses).ToListAsync(Ct);
        Assert.Equal(2, teachers.Count);
        Assert.Single(teachers.Single(t => t.Id == "teacher1").SchoolClasses!);
        Assert.Equal(2, teachers.Single(t => t.Id == "teacher2").SchoolClasses!.Count);
    });
}
