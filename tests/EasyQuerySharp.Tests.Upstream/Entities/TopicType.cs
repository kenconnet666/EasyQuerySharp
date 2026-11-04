using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：enums/TopicTypeEnum。C# 枚举本身就是整数，按底层值存取，不需要 EnumConverter。</summary>
public enum TopicTypeEnum
{
    Student = 1,
    Teacher = 3,
    Classer = 9,
}

/// <summary>对应 Java：entity/TopicType（主题类型存成整数）。</summary>
[Table("t_topic_type")]
public class TopicType
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    // C# 成员名不能与类型同名，属性改名，列名保持 topic_type
    [Column("topic_type")]
    public int? TopicTypeValue { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicTypeTest1 / TopicTypeTest2（主题类型直接是枚举）。</summary>
[Table("t_topic_type")]
public class TopicTypeTest1
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    [Column("topic_type")]
    public TopicTypeEnum? TopicType { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicTypeJson（标题列存 JSON）。</summary>
[Table("t_topic_type")]
public class TopicTypeJson
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    [Column(Converter = typeof(JsonValueConverter<TopicTypeJsonValue>))]
    public TopicTypeJsonValue? Title { get; set; }

    [Column("topic_type")]
    public int? TopicType { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicTypeJsonValue。</summary>
public class TopicTypeJsonValue
{
    public string? Name { get; set; }

    public int? Age { get; set; }
}

/// <summary>对应 Java：dto/TopicTypeVO（DTO 上用 [Column] 改列别名）。</summary>
public class TopicTypeVO
{
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    [Column("topic_type1")]
    public TopicTypeEnum? TopicType { get; set; }

    public DateTime? CreateTime { get; set; }
}
