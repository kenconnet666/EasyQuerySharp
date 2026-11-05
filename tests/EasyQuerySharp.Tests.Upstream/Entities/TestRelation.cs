using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

// 对应 Java：entity/testrelation：用户 → 角色 → 路由，两段多对多共用一张中间表，按 type 区分（Java 的 JoinType 额外条件）

[Table("t_test_user")]
public class TestUserEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Password { get; set; }

    private static Expression<Func<TestJoinEntity, bool>> UserRoleJoin => j => j.Type == 1;

    [Navigate(RelationType.ManyToMany, Mapping = typeof(TestJoinEntity), SelfMappingProperties = [nameof(TestJoinEntity.FirstId)],
        TargetMappingProperties = [nameof(TestJoinEntity.SecondId)], ExtraMappingFilter = nameof(UserRoleJoin))]
    public List<TestRoleEntity>? Roles { get; set; }
}

[Table("t_test_role")]
public class TestRoleEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Remark { get; set; }

    private static Expression<Func<TestJoinEntity, bool>> RoleRouteJoin => j => j.Type == 2;

    [Navigate(RelationType.ManyToMany, Mapping = typeof(TestJoinEntity), SelfMappingProperties = [nameof(TestJoinEntity.FirstId)],
        TargetMappingProperties = [nameof(TestJoinEntity.SecondId)], ExtraMappingFilter = nameof(RoleRouteJoin))]
    public List<TestRouteEntity>? Routes { get; set; }
}

[Table("t_test_route")]
public class TestRouteEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? RequestPath { get; set; }
}

[Table("t_test_join")]
public class TestJoinEntity
{
    [Column(PrimaryKey = true)]
    public string? FirstId { get; set; }

    [Column(PrimaryKey = true)]
    public string? SecondId { get; set; }

    [Column(PrimaryKey = true)]
    public int? Type { get; set; }
}
