using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/BaseEntity。</summary>
public class BaseEntity
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    [Column(Comment = "创建时间;创建时间")]
    public DateTime? CreateTime { get; set; }

    [Column(Comment = "修改时间;修改时间")]
    public DateTime? UpdateTime { get; set; }

    [Column(Comment = "创建人;创建人")]
    public string? CreateBy { get; set; }

    [Column(Comment = "修改人;修改人")]
    public string? UpdateBy { get; set; }

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    [UpdateIgnore]
    [Column(Comment = "是否删除;是否删除")]
    public bool? Deleted { get; set; }
}

/// <summary>对应 Java：entity/BaseGenericEntity&lt;TKey&gt;（主键类型由子类指定的基类）。</summary>
public abstract class BaseGenericEntity<TKey>
{
    [Column(PrimaryKey = true)]
    public TKey? Id { get; set; }
}

/// <summary>对应 Java：entity/TopicGenericKey（主键来自泛型基类）。</summary>
[Table("t_topic")]
public class TopicGenericKey : BaseGenericEntity<string>
{
    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}
