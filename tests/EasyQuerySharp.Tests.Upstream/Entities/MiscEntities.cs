using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：navigateflat/MyUserHome（表 my_user，没有主键）。</summary>
[Table("my_user")]
public class MyUserHome
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }
}

/// <summary>对应 Java：navigateflat/MyUserHome2（多级 schema <c>a.dbo</c>，各级分别加引号）。库里没有这个 schema，只比对 SQL。</summary>
[Table("my_user", Schema = "a.dbo")]
public class MyUserHome2
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }
}

/// <summary>对应 Java：entity/MyCategory（自关联的一对多）。</summary>
[Table("category")]
public class MyCategory
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ParentId)])]
    public List<MyCategory>? Children { get; set; }
}

/// <summary>对应 Java：entity/MyCategoryVO。</summary>
public class MyCategoryVO
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public long? Count { get; set; }
}

/// <summary>对应 Java：vo/MyUnion（原生 SQL 查询的结果，一列 task_status）。</summary>
public class MyUnion
{
    public string? TaskStatus { get; set; }
}

/// <summary>对应 Java：dto/MyCategoryDTO（DTO 上声明指向自身的导航，树结果投影成 DTO）。</summary>
public class MyCategoryDTO
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)])]
    public List<MyCategoryDTO>? Children { get; set; }
}

/// <summary>对应 Java：entity/vo/MyCategoryVO4（树结果连接另一张表后投影成 DTO）。</summary>
public class MyCategoryVO4
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public string? JoinName { get; set; }

    public long? Deep1 { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)])]
    public List<MyCategoryVO4>? Children { get; set; }
}

/// <summary>对应 Java：entity/vo/MyCategoryVO2（分类树的 DTO）。</summary>
public class MyCategoryVO2
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)])]
    public List<MyCategoryVO2>? Children { get; set; }
}

/// <summary>对应 Java：entity/vo/MyCategoryVO3（多一个层级属性 Deep，层级列叫 deep 时才有值）。</summary>
public class MyCategoryVO3
{
    public string? Id { get; set; }

    public string? ParentId { get; set; }

    public string? Name { get; set; }

    public long? Deep { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ParentId)])]
    public List<MyCategoryVO3>? Children { get; set; }
}

/// <summary>对应 Java：entity/notable/QueryLargeColumnTestEntity（库里没有这张表，只为拿到出错的 SQL）。</summary>
[Table("query_large_column_test")]
public class QueryLargeColumnTestEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Content { get; set; }
}

/// <summary>对应 Java：entity/UnknownTable（库里没有这张表）。</summary>
[Table("t_unknown")]
public class UnknownTable
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Age { get; set; }

    public int? Age1 { get; set; }

    public long? Money { get; set; }

    public long? Money1 { get; set; }

    public DateTime? Birthday { get; set; }

    public DateTime? Birthday1 { get; set; }
}

/// <summary>对应 Java：entity/NoKeyEntity（没有主键）。</summary>
[Table("t_nokey")]
public class NoKeyEntity
{
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：mysql8/entity/TopicJson（JSON 存成普通文本列）。</summary>
[Table("t_test_json")]
public class TopicJson
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column(DbType = "varchar(255)")]
    public string? ExtraJson { get; set; }

    [Column(DbType = "varchar(255)")]
    public string? ExtraJsonArray { get; set; }
}

/// <summary>对应 Java：pgsql/PgTopicJson（jsonb 列，属性是 JSON 文本）。</summary>
[Table("t_test_json")]
public class PgTopicJson
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column(DbType = "jsonb")]
    public string? ExtraJson { get; set; }

    [Column(DbType = "jsonb")]
    public string? ExtraJsonArray { get; set; }
}

/// <summary>对应 Java：pgsql/TopicExtraJson。</summary>
public class TopicExtraJson
{
    public bool? Success { get; set; }

    public int? Age { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：pgsql/PgTopicJson2（jsonb 列，属性是对象与对象列表）。</summary>
[Table("t_test_json2")]
public class PgTopicJson2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column(DbType = "jsonb", Converter = typeof(JsonValueConverter<TopicExtraJson>))]
    public TopicExtraJson? ExtraJson { get; set; }

    [Column(DbType = "jsonb", Converter = typeof(JsonValueConverter<List<TopicExtraJson>>))]
    public List<TopicExtraJson>? ExtraJsonArray { get; set; }
}

/// <summary>对应 Java：pgsql/TopicJson2VO（按同名列投影，JSON 列照样转换）。</summary>
public class TopicJson2VO
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column(Converter = typeof(JsonValueConverter<TopicExtraJson>))]
    public TopicExtraJson? ExtraJson { get; set; }

    [Column(Converter = typeof(JsonValueConverter<List<TopicExtraJson>>))]
    public List<TopicExtraJson>? ExtraJsonArray { get; set; }
}
