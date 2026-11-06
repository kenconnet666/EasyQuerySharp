using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/school/SchoolClass（一对多学生、经中间表多对多老师、一对一 Topic）。</summary>
[Table("school_class")]
public class SchoolClass
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SchoolStudent.ClassId)])]
    public List<SchoolStudent>? SchoolStudents { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(SchoolClassTeacher),
        SelfMappingProperties = [nameof(SchoolClassTeacher.ClassId)], TargetMappingProperties = [nameof(SchoolClassTeacher.TeacherId)])]
    public List<SchoolTeacher>? SchoolTeachers { get; set; }

    [Navigate(RelationType.OneToOne)]
    public Topic? Topic { get; set; }
}

/// <summary>对应 Java：entity/school/SchoolStudent。</summary>
[Table("school_student")]
public class SchoolStudent
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ClassId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ClassId)], TargetProperties = [nameof(SchoolClass.Id)])]
    public SchoolClass? SchoolClass { get; set; }

    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(SchoolStudentAddress.StudentId)])]
    public SchoolStudentAddress? SchoolStudentAddress { get; set; }
}

/// <summary>对应 Java：entity/school/SchoolStudent2（与 SchoolStudent 同表，班级声明成一对一）。</summary>
[Table("school_student")]
public class SchoolStudent2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ClassId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(ClassId)], TargetProperties = [nameof(SchoolClass.Id)])]
    public SchoolClass? SchoolClass { get; set; }
}

/// <summary>对应 Java：entity/school/SchoolTeacher。</summary>
[Table("school_teacher")]
public class SchoolTeacher
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(SchoolClassTeacher), SelfProperties = [nameof(Id)],
        SelfMappingProperties = [nameof(SchoolClassTeacher.TeacherId)], TargetProperties = [nameof(SchoolClass.Id)],
        TargetMappingProperties = [nameof(SchoolClassTeacher.ClassId)])]
    public List<SchoolClass>? SchoolClasses { get; set; }
}

/// <summary>对应 Java：entity/school/SchoolClassTeacher（班级与老师的中间表）。</summary>
[Table("school_class_teacher")]
public class SchoolClassTeacher
{
    [Column(PrimaryKey = true)]
    public string? ClassId { get; set; }

    [Column(PrimaryKey = true)]
    public string? TeacherId { get; set; }
}

/// <summary>对应 Java：entity/school/SchoolStudentAddress。</summary>
[Table("school_student_address")]
public class SchoolStudentAddress
{
    public string? Id { get; set; }

    public string? StudentId { get; set; }

    public string? Address { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(StudentId)], TargetProperties = [nameof(SchoolStudent.Id)])]
    public SchoolStudent? SchoolStudent { get; set; }
}

/// <summary>对应 Java：entity/school/MySchoolClass（班级一对多学生，导航用默认的自身主键）。</summary>
[Table("my_school_class")]
public class MySchoolClass
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(MySchoolStudent.ClassId)])]
    public List<MySchoolStudent>? SchoolStudents { get; set; }
}

/// <summary>对应 Java：entity/school/MySchoolStudent。</summary>
[Table("my_school_student")]
public class MySchoolStudent
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ClassId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ClassId)], TargetProperties = [nameof(MySchoolClass.Id)])]
    public MySchoolClass? SchoolClass { get; set; }

    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(MySchoolStudentAddress.StudentId)])]
    public MySchoolStudentAddress? SchoolStudentAddress { get; set; }
}

/// <summary>对应 Java：entity/school/MySchoolStudentAddress（与 SchoolStudentAddress 同表）。</summary>
[Table("school_student_address")]
public class MySchoolStudentAddress
{
    public string? Id { get; set; }

    public string? StudentId { get; set; }

    public string? Address { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(StudentId)], TargetProperties = [nameof(MySchoolStudent.Id)])]
    public MySchoolStudent? SchoolStudent { get; set; }
}
