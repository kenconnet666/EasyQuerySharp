using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

// 对应 Java：entity/navf：用户、角色、路由、资源经同一张 sys_role_join 多对多，按 type 区分（Java 的 RoleJoinType 额外条件）。
// 这几张表不在建表脚本里（Java 用例也只验证第一条 SQL 后报错）。

[Table("sys_user")]
public class NavfUser
{
    [Column(PrimaryKey = true)]
    public long? Id { get; set; }

    public string? Username { get; set; }

    public int? Status { get; set; }

    public string? RealName { get; set; }

    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public DateTime? CreateTime { get; set; }

    public DateTime? LoginTime { get; set; }

    [LogicDelete(LogicDeleteStrategy.DeletedTimestamp)]
    public long? Deleted { get; set; }

    private static Expression<Func<NavfRoleJoin, bool>> UserJoin => j => j.Type == "user";

    [Navigate(RelationType.ManyToMany, Mapping = typeof(NavfRoleJoin), SelfMappingProperties = [nameof(NavfRoleJoin.BizId)],
        TargetMappingProperties = [nameof(NavfRoleJoin.RoleId)], ExtraMappingFilter = nameof(UserJoin))]
    public List<NavfRole>? Roles { get; set; }
}

[Table("sys_role")]
public class NavfRole
{
    [Column(PrimaryKey = true)]
    public long? Id { get; set; }

    public string? Name { get; set; }

    public string? Permission { get; set; }

    [LogicDelete(LogicDeleteStrategy.DeletedTimestamp)]
    public long? Deleted { get; set; }

    private static Expression<Func<NavfRoleJoin, bool>> RouteJoin => j => j.Type == "route";

    private static Expression<Func<NavfRoleJoin, bool>> ResourceJoin => j => j.Type == "res";

    [Navigate(RelationType.ManyToMany, Mapping = typeof(NavfRoleJoin), SelfMappingProperties = [nameof(NavfRoleJoin.RoleId)],
        TargetMappingProperties = [nameof(NavfRoleJoin.BizId)], ExtraMappingFilter = nameof(RouteJoin))]
    public List<NavfRoute>? Routes { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(NavfRoleJoin), SelfMappingProperties = [nameof(NavfRoleJoin.RoleId)],
        TargetMappingProperties = [nameof(NavfRoleJoin.BizId)], ExtraMappingFilter = nameof(ResourceJoin))]
    public List<NavfResource>? Resources { get; set; }
}

[Table("sys_route")]
public class NavfRoute
{
    [Column(PrimaryKey = true)]
    public long? Id { get; set; }

    public string? Name { get; set; }

    public int? Type { get; set; }

    public string? Path { get; set; }

    public string? Component { get; set; }

    public string? Describe { get; set; }

    public int? Sort { get; set; }

    public long? ParentId { get; set; }

    public bool? Hidden { get; set; }

    public string? ActiveMenu { get; set; }

    public string? Redirect { get; set; }
}

[Table("sys_resource")]
public class NavfResource
{
    [Column(PrimaryKey = true)]
    public long? Id { get; set; }

    public string? Name { get; set; }

    public string? Path { get; set; }

    public string? Method { get; set; }

    public string? PermissionMark { get; set; }
}

[Table("sys_role_join")]
public class NavfRoleJoin
{
    public long? RoleId { get; set; }

    public long? BizId { get; set; }

    /// <summary>业务类型：user / res / route。</summary>
    public string? Type { get; set; }
}
