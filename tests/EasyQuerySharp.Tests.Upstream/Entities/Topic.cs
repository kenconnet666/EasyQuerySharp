using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/Topic。</summary>
[Table("t_topic")]
public class Topic
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }

    [ColumnIgnore]
    public string? Alias { get; set; }
}

/// <summary>对应 Java：entity/TopicUpdate（映射到 t_topic，配合 TopicUpdateInterceptor 测试更新拦截）。</summary>
[Table("t_topic")]
public class TopicUpdate
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }

    [ColumnIgnore]
    public string? Alias { get; set; }
}

/// <summary>对应 Java：entity/TopicAuto（自增主键）。</summary>
[Table("t_topic_auto")]
public class TopicAuto
{
    [Column(PrimaryKey = true, Generated = true)]
    public int? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/LogicDelTopic。</summary>
[Table("t_logic_del_topic")]
public class LogicDelTopic
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    public bool? Deleted { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/LogicDelTopicCustom（自定义逻辑删除策略 MyLogicDelStrategy）。</summary>
[Table("t_logic_del_topic_custom")]
public class LogicDelTopicCustom
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    [LogicDelete(LogicDeleteStrategy.Custom, StrategyName = nameof(LogicDelete.MyLogicDelStrategy))]
    public DateTime? DeletedAt { get; set; }

    public string? DeletedUser { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicInterceptor。</summary>
[Table("t_topic_interceptor")]
public class TopicInterceptor
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    [UpdateIgnore]
    public DateTime? CreateTime { get; set; }

    [UpdateIgnore]
    public string? CreateBy { get; set; }

    public DateTime? UpdateTime { get; set; }

    public string? UpdateBy { get; set; }

    [UpdateIgnore]
    public string? TenantId { get; set; }
}

/// <summary>对应 Java：entity/TopicLarge（title 整行更新时忽略、差异更新时照写；title1 总是忽略）。</summary>
[Table("t_topic")]
public class TopicLarge
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    [UpdateIgnore(WhenTrackedChanged = true)]
    public string? Title { get; set; }

    [UpdateIgnore]
    public string? Title1 { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：dto/TopicUnion（title 列别名 abc）。</summary>
public class TopicUnion
{
    public string? Id { get; set; }

    public int? Stars { get; set; }

    [Column("abc")]
    public string? Title { get; set; }
}

/// <summary>对应 Java：dto/TopicSubQueryBlog（多一个子查询列 blogCount）。</summary>
public class TopicSubQueryBlog
{
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }

    public long? BlogCount { get; set; }
}

/// <summary>对应 Java：entity/Topicx（映射 t_topic、没有主键的普通类）。</summary>
[Table("t_topic")]
public class Topicx
{
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicFile（同表 t_topic 的另一个实体）。</summary>
[Table("t_topic")]
public class TopicFile
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}
