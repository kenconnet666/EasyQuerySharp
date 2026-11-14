using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>
/// 对应 Java：entity/UserExtra（全名、年龄是计算列，Java 用 FullName / UserAge 的 ColumnValueSQLConverter）。
/// 年龄 = 向上取整(出生到现在的天数 / 365)。
/// </summary>
[Table("t_user_extra")]
public class UserExtra
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime? Birthday { get; set; }

    [Computed(nameof(FullNameSql))]
    public string? FullName { get; set; }

    [Computed(nameof(AgeSql))]
    public int? Age { get; set; }

    private static Expression<Func<UserExtra, string?>> FullNameSql => u => string.Concat(u.FirstName, u.LastName);

    private static Expression<Func<UserExtra, int?>> AgeSql => u => (int?)Math.Ceiling((double)(Sql.Now() - u.Birthday!.Value).Days / 365);
}

/// <summary>对应 Java：entity/school/SchoolClassAggregate（学生数是计算列：关联学生的计数子查询）。</summary>
[Table("school_class")]
public class SchoolClassAggregate
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SchoolStudent.ClassId)])]
    public List<SchoolStudent>? SchoolStudents { get; set; }

    [Computed(nameof(StudentSizeSql))]
    public long? StudentSize { get; set; }

    private static Expression<Func<SchoolClassAggregate, long?>> StudentSizeSql => c => c.SchoolStudents!.Select(s => s.Id).Count();
}

/// <summary>对应 Java：entity/school/SchoolClassAggregateProp（同上，但学生数默认不选出，要显式投影）。</summary>
[Table("school_class")]
public class SchoolClassAggregateProp
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SchoolStudent.ClassId)])]
    public List<SchoolStudent>? SchoolStudents { get; set; }

    [Computed(nameof(StudentSizeSql)), Column(AutoSelect = false)]
    public long? StudentSize { get; set; }

    private static Expression<Func<SchoolClassAggregateProp, long?>> StudentSizeSql => c => c.SchoolStudents!.Select(s => s.Id).Count();
}
