using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/entity/M8User（经 m8_user_role 多对多角色）。</summary>
[Table("m8_user")]
public class M8User
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8UserRole.UserId)], Mapping = typeof(M8UserRole),
        TargetProperties = [nameof(M8Role.Id)], TargetMappingProperties = [nameof(M8UserRole.RoleId)])]
    public List<M8Role>? Roles { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8User2（主键不叫 id 的多对多与一对多）。</summary>
[Table("m8_user2")]
public class M8User2
{
    [Column(PrimaryKey = true)]
    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public int? UserAge { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(UserId)], SelfMappingProperties = [nameof(M8UserRole2.UserId)], Mapping = typeof(M8UserRole2),
        TargetProperties = [nameof(M8Role2.RoleId)], TargetMappingProperties = [nameof(M8UserRole2.RoleId)])]
    public List<M8Role2>? Roles { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(UserId)], TargetProperties = [nameof(M8UserBook.UserId)])]
    public List<M8UserBook>? Books { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Role（经 m8_role_menu 多对多菜单）。</summary>
[Table("m8_role")]
public class M8Role
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8RoleMenu.RoleId)], Mapping = typeof(M8RoleMenu),
        TargetProperties = [nameof(M8Menu.Id)], TargetMappingProperties = [nameof(M8RoleMenu.MenuId)])]
    public List<M8Menu>? Menus { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Mysql8.M8SaveA.Id)])]
    public M8SaveA? M8SaveA { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Role2。</summary>
[Table("m8_role2")]
public class M8Role2
{
    [Column(PrimaryKey = true)]
    public string? RoleId { get; set; }

    public string? RoleName { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Menu。</summary>
[Table("m8_menu")]
public class M8Menu
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Path { get; set; }

    public string? OwnerId { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(OwnerId)], TargetProperties = [nameof(M8MenuOwner.Id)])]
    public M8MenuOwner? MenuOwner { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8MenuOwner（与 M8Menu 同表，只映射 id、name）。</summary>
[Table("m8_menu")]
public class M8MenuOwner
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8RoleMenu。</summary>
[Table("m8_role_menu")]
public class M8RoleMenu
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? RoleId { get; set; }

    public string? MenuId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserBook。</summary>
[Table("m8_user_book")]
public class M8UserBook
{
    [Column(PrimaryKey = true)]
    public string? BookId { get; set; }

    public string? UserId { get; set; }

    public string? BookName { get; set; }

    public decimal? BookPrice { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserRole。</summary>
[Table("m8_user_role")]
public class M8UserRole
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? RoleId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8Province（省 → 市 → 区的三层一对多）。</summary>
[Table("m8_province")]
public class M8Province
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8City.Pid)])]
    public List<M8City>? Cities { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8City。</summary>
[Table("m8_city")]
public class M8City
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8Area.Cid)])]
    public List<M8Area>? Areas { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8Area.Cid)])]
    public List<M8Area>? Areas2 { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8Area。</summary>
[Table("m8_area")]
public class M8Area
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Cid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8AreaBuild.Aid)])]
    public List<M8AreaBuild>? Builds { get; set; }
}

/// <summary>对应 Java：mysql8/entity/many/M8AreaBuild。</summary>
[Table("m8_build")]
public class M8AreaBuild
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Aid { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Parent（一对多与经中间表的多对多子项，order 是关键字列）。</summary>
[Table("m8_parent")]
public class M8Parent
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Order { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8Child.ParentId)], OrderBy = nameof(M8Child.Order))]
    public List<M8Child>? Children { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8ParentChild.ParentId)], Mapping = typeof(M8ParentChild),
        TargetProperties = [nameof(M8Child.Id)], TargetMappingProperties = [nameof(M8ParentChild.ChildId)], OrderBy = nameof(M8Child.Order))]
    public List<M8Child>? Children2 { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8Child。</summary>
[Table("m8_child")]
public class M8Child
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public int? Order { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8ParentChild。</summary>
[Table("m8_parent_child")]
public class M8ParentChild
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? ChildId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/BatchInsert（自增主键）。</summary>
[Table("m8_batch")]
public class BatchInsert
{
    [Column(PrimaryKey = true, Generated = true)]
    public long? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserRole2。</summary>
[Table("m8_user_role2")]
public class M8UserRole2
{
    [Column(PrimaryKey = true)]
    public string? UserRoleId { get; set; }

    public string? UserId { get; set; }

    public string? RoleId { get; set; }
}

/// <summary>对应 Java：mysql8/entity/TableNoKey（没有主键的表）。</summary>
[Table("no_key_table")]
public class TableNoKey
{
    public string? Column1 { get; set; }

    public string? Column2 { get; set; }

    public string? Column3 { get; set; }
}

/// <summary>对应 Java：mysql8/entity/OffsetChunkTest（按偏移分批读取的数据）。</summary>
[Table("offset_chunk_test")]
public class OffsetChunkTest
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Seq { get; set; }

    public string? Status { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserProtect（与 M8User 同表，受保护的查询过滤只作用于它）。</summary>
[Table("m8_user")]
public class M8UserProtect
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserTest（没有主键的用户表，用于多条件组合）。</summary>
[Table("m8_user_test")]
public class M8UserTest
{
    public long? Id { get; set; }

    public int? Sex { get; set; }

    public int? Age { get; set; }

    public string? UserName { get; set; }
}

/// <summary>对应 Java：mysql8/dto/BankCardGroupBO（分组结果，DTO 上声明了一对一导航到用户）。</summary>
public class BankCardGroupBO
{
    public string? Uid { get; set; }

    public long Count { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Uid)], TargetProperties = [nameof(SysUser.Id)])]
    public SysUser? User { get; set; }
}

/// <summary>对应 Java：mysql8/entity/Comment（评论按 parent_id 自关联成树）。</summary>
[Table("t_comment", NotFoundMessage = "未找到对应的评论信息")]
public class Comment
{
    [Column(PrimaryKey = true, Comment = "评论id")]
    public string? Id { get; set; }

    [Column(Comment = "父id")]
    public string? ParentId { get; set; }

    [Column(Comment = "帖子内容")]
    public string? Content { get; set; }

    [Column(Comment = "用户id")]
    public string? UserId { get; set; }

    [Column(Comment = "帖子id")]
    public string? PostId { get; set; }

    [Column(Comment = "回复时间")]
    public DateTime? CreateAt { get; set; }

    /// <summary>子评论。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)])]
    public List<Comment>? Children { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8UserTemp（由查询定义的实体：age 为空的用户，查询时写成 WITH `m8_user_temp` AS (…)）。</summary>
public class M8UserTemp : ICteView<M8UserTemp>
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(M8UserRole.UserId)], Mapping = typeof(M8UserRole),
        TargetProperties = [nameof(M8Role.Id)], TargetMappingProperties = [nameof(M8UserRole.RoleId)], PartitionOrder = PartitionOrder.None)]
    public List<M8Role>? Roles { get; set; }

    public static Query<M8UserTemp> View(EasyDb db) => db.Query<M8User>().Where(m => m.Age == null).Select(m => new M8UserTemp { Id = m.Id, Name = m.Name });
}

/// <summary>对应 Java：mysql8/entity/M8UserTemp2（视图里带窗口函数列）。</summary>
public class M8UserTemp2 : ICteView<M8UserTemp2>
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public long? RowNumber { get; set; }

    public static Query<M8UserTemp2> View(EasyDb db) => db.Query<M8User>().Where(m => m.Age == null).Select(m => new M8UserTemp2
    {
        Id = m.Id, Name = m.Name, Age = m.Age, RowNumber = Sql.Over.RowNumber(w => w.PartitionBy(m.Age).OrderByDescending(m.CreateTime)),
    });
}

/// <summary>对应 Java：mysql8/entity/save/M8SaveA（聚合保存的根；这里先用到 id、name）。</summary>
[Table("m8_save_a")]
public class M8SaveA
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：dto/MyConfigLogicDelete（实体上不标注，逻辑删除由夹具里的 ApplyLogicDelete 配置）。</summary>
[Table("my_config_logic")]
public class MyConfigLogicDelete
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public bool? Deleted { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8IncludeLimitTopic（Include 带 Take 的场景）。</summary>
[Table("m8_include_limit_topic")]
public class M8IncludeLimitTopic
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(M8IncludeLimitChild.ParentId)], OrderBy = nameof(M8IncludeLimitChild.Order))]
    public List<M8IncludeLimitChild>? Children { get; set; }
}

/// <summary>对应 Java：mysql8/entity/M8IncludeLimitChild（deleted 列不自动查询）。</summary>
[Table("m8_include_limit_child")]
public class M8IncludeLimitChild
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public int? Order { get; set; }

    [Column(AutoSelect = false)]
    public bool? Deleted { get; set; }
}

/// <summary>对应 Java：mysql8/TreeA（视图 TreeC 的来源）。</summary>
[Table("m8_treea")]
public class TreeA
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/TreeB（TreeA 的父子关系）。</summary>
[Table("m8_treeb")]
public class TreeB
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Aid { get; set; }

    public string? Apid { get; set; }
}

/// <summary>对应 Java：mysql8/view/TreeC（由 TreeA LEFT JOIN TreeB 定义的视图实体，自引用成树）。</summary>
[Table("m8_treec")]
public class TreeC : ICteView<TreeC>
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Pid { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Pid)])]
    public List<TreeC>? TreeCList { get; set; }

    public static Query<TreeC> View(EasyDb db) => db.Query<TreeA>().LeftJoin<TreeB>((a, b) => a.Id == b.Aid)
        .Select((a, b) => new TreeC { Id = a.Id, Name = a.Name, Pid = b.Apid });
}

/// <summary>对应 Java：mysql8/dto/MyComment（评论树投影成 DTO：DTO 上声明指向自身的导航，层级列同名时带上层级）。</summary>
public class MyComment
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Content { get; set; }

    public string? UserId { get; set; }

    public string? PostId { get; set; }

    public DateTime? CreateAt { get; set; }

    public long? Deep { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)])]
    public List<MyComment>? Children { get; set; }
}

/// <summary>对应 Java：mysql8/dto/MyComment3（DTO 上没有声明导航：按源实体的 Children 导航与同名属性拼树）。</summary>
public class MyComment3
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Content { get; set; }

    public string? UserId { get; set; }

    public string? PostId { get; set; }

    public DateTime? CreateAt { get; set; }

    public long? Deep { get; set; }

    public List<MyComment3>? Children { get; set; }
}

/// <summary>对应 Java：mysql8/dto/MyComment4（缺上级键 ParentId，拼不成树）。</summary>
public class MyComment4
{
    public string? Id { get; set; }

    public string? Content { get; set; }

    public string? UserId { get; set; }

    public string? PostId { get; set; }

    public DateTime? CreateAt { get; set; }

    public List<MyComment4>? Children { get; set; }
}

/// <summary>对应 Java：mysql8/entity/condition/MyTask（按 version 决定挂哪张子表 / 扩展表）。</summary>
[Table("my_task")]
public class MyTask
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public long? Version { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(MySubTask1.TaskId)])]
    [IncludeOn(nameof(Version), 1L)]
    public List<MySubTask1>? MySubTask1List { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(MySubTask2.TaskId)])]
    [IncludeOn(nameof(Version), 2L)]
    public List<MySubTask2>? MySubTask2List { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(MyTask1Ext.TaskId)])]
    [IncludeOn(nameof(Version), 1L)]
    public MyTask1Ext? MyTask1Ext { get; set; }

    [Navigate(RelationType.OneToOne, SelfProperties = [nameof(Id)], TargetProperties = [nameof(MyTask2Ext.TaskId)])]
    [IncludeOn(nameof(Version), 2L)]
    public MyTask2Ext? MyTask2Ext { get; set; }
}

/// <summary>对应 Java：mysql8/entity/condition/MySubTask1。</summary>
[Table("my_task_sub1")]
public class MySubTask1
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? TaskId { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/condition/MySubTask2。</summary>
[Table("my_task_sub2")]
public class MySubTask2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? TaskId { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/condition/MyTask1Ext。</summary>
[Table("my_task1_ext")]
public class MyTask1Ext
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? TaskId { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/condition/MyTask2Ext。</summary>
[Table("my_task2_ext")]
public class MyTask2Ext
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? TaskId { get; set; }

    public string? Name { get; set; }
}
