using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>
/// 对应 Java：entity/relation/DynamicExtraFilter（按作用域开关导航上的动态固定条件）。
/// 导航的固定条件每次查询都重新取值，所以属性里读这个开关就能按需返回 null。
/// </summary>
public static class DynamicExtraFilter
{
    private static readonly AsyncLocal<bool?> Current = new();

    /// <summary>当前作用域是否启用（默认启用）。</summary>
    public static bool Enabled => Current.Value ?? true;

    /// <summary>在作用域内开关，Dispose 时恢复。</summary>
    public static IDisposable CreateScope(bool enable)
    {
        var previous = Current.Value;
        Current.Value = enable;
        return new Scope(() => Current.Value = previous);
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>
/// 对应 Java：entity/relation/RelationUser。导航的固定条件对应 Java 的 BookNavigateExtraFilterStrategy：
/// books 只要 book_type = 1，historyBooks 只要 2022 年以前的，teachers 经中间表（type = 1），动态开关打开时还要求 name = '12345'。
/// </summary>
[Table("relation_user")]
public class RelationUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(RelationBook.UserId)], ExtraFilter = nameof(UserBookFilter))]
    public List<RelationBook>? Books { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(RelationBook.UserId)], ExtraFilter = nameof(HistoryBookFilter))]
    public List<RelationBook>? HistoryBooks { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(RelationRoute), SelfMappingProperties = [nameof(RelationRoute.FirstId)],
        TargetMappingProperties = [nameof(RelationRoute.SecondId)], ExtraFilter = nameof(TeacherFilter), ExtraMappingFilter = nameof(TeacherRouteFilter))]
    public List<RelationTeacher>? Teachers { get; set; }

    private static Expression<Func<RelationBook, bool>> UserBookFilter => b => b.BookType == 1;

    private static Expression<Func<RelationBook, bool>> HistoryBookFilter => b => b.CreateTime <= new DateTime(2022, 1, 1);

    private static Expression<Func<RelationTeacher, bool>>? TeacherFilter => DynamicExtraFilter.Enabled ? t => t.Name == "12345" : null;

    private static Expression<Func<RelationRoute, bool>> TeacherRouteFilter => r => r.Type == 1;
}

/// <summary>对应 Java：entity/relation/RelationBook（学生与老师的书放在同一张表，按 book_type 区分）。</summary>
[Table("relation_book")]
public class RelationBook
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? Name { get; set; }

    public int? BookType { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/relation/RelationTeacher（books 只要 book_type = 2；users 经中间表反向关联）。</summary>
[Table("relation_teacher")]
public class RelationTeacher
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(RelationBook.UserId)], ExtraFilter = nameof(TeacherBookFilter))]
    public List<RelationBook>? Books { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(RelationRoute), SelfMappingProperties = [nameof(RelationRoute.SecondId)],
        TargetMappingProperties = [nameof(RelationRoute.FirstId)])]
    public List<RelationUser>? Users { get; set; }

    private static Expression<Func<RelationBook, bool>> TeacherBookFilter => b => b.BookType == 2;
}

/// <summary>对应 Java：entity/relation/RelationRoute（多对多中间表，type 区分关系种类）。</summary>
[Table("relation_route")]
public class RelationRoute
{
    [Column(PrimaryKey = true)]
    public string? FirstId { get; set; }

    [Column(PrimaryKey = true)]
    public string? SecondId { get; set; }

    [Column(PrimaryKey = true)]
    public int? Type { get; set; }
}

/// <summary>对应 Java：entity/relation/MyRelationUser（与 RelationUser 相同的导航，表是 t_user）。</summary>
[Table("t_user")]
public class MyRelationUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(RelationBook.UserId)], ExtraFilter = nameof(UserBookFilter))]
    public List<RelationBook>? Books { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(RelationBook.UserId)], ExtraFilter = nameof(HistoryBookFilter))]
    public List<RelationBook>? HistoryBooks { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(RelationRoute), SelfMappingProperties = [nameof(RelationRoute.FirstId)],
        TargetMappingProperties = [nameof(RelationRoute.SecondId)], ExtraFilter = nameof(TeacherFilter), ExtraMappingFilter = nameof(TeacherRouteFilter))]
    public List<RelationTeacher>? Teachers { get; set; }

    private static Expression<Func<RelationBook, bool>> UserBookFilter => b => b.BookType == 1;

    private static Expression<Func<RelationBook, bool>> HistoryBookFilter => b => b.CreateTime <= new DateTime(2022, 1, 1);

    private static Expression<Func<RelationTeacher, bool>>? TeacherFilter => DynamicExtraFilter.Enabled ? t => t.Name == "12345" : null;

    private static Expression<Func<RelationRoute, bool>> TeacherRouteFilter => r => r.Type == 1;
}
