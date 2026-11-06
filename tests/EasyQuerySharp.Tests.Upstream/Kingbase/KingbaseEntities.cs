using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Kingbase;

/// <summary>对应 Java：kingbase/BaseEntity（<c>deleted</c> 只是普通列，没有逻辑删除）。</summary>
public abstract class BaseEntity
{
    [Column(PrimaryKey = true, Comment = "记录标识;记录标识")]
    public string? Id { get; set; }

    [UpdateIgnore]
    [Column(Comment = "创建时间;创建时间")]
    public DateTime? CreateTime { get; set; }

    [Column(Comment = "修改时间;修改时间")]
    public DateTime? UpdateTime { get; set; }

    [UpdateIgnore]
    [Column(Comment = "创建人;创建人")]
    public string? CreateBy { get; set; }

    [Column(Comment = "修改人;修改人")]
    public string? UpdateBy { get; set; }

    [UpdateIgnore]
    [Column(Comment = "是否删除;是否删除")]
    public bool? Deleted { get; set; }

    [UpdateIgnore]
    [Column(Comment = "删除人")]
    public string? DeleteBy { get; set; }

    [UpdateIgnore]
    [Column(Comment = "删除时间")]
    public DateTime? DeleteTime { get; set; }
}

/// <summary>对应 Java：kingbase/HelpEduSubjectEntity（在线学习专题，经关联表多对多到视频，视频按 order_by 排）。</summary>
[Table("help_edu_subject", NotFoundMessage = "未找到对应的在线学习配置信息")]
public class HelpEduSubjectEntity : BaseEntity
{
    public string? Name { get; set; }

    public int? OrderBy { get; set; }

    [Navigate(RelationType.ManyToMany, Mapping = typeof(HelpCategoryAndSubjectEntity),
        SelfMappingProperties = [nameof(HelpCategoryAndSubjectEntity.SubjectId)], TargetMappingProperties = [nameof(HelpCategoryAndSubjectEntity.ContextId)],
        OrderBy = nameof(HelpEduVideoEntity.OrderBy))]
    public List<HelpEduVideoEntity>? HelpEduVideoList { get; set; }
}

/// <summary>对应 Java：kingbase/HelpEduVideoEntity（安全教育视频）。</summary>
[Table("help_edu_video", NotFoundMessage = "未找到对应的安全教育视频信息")]
public class HelpEduVideoEntity : BaseEntity
{
    [Column(Comment = "排序")]
    public int? OrderBy { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(HelpCategoryAndSubjectEntity.ContextId)])]
    public List<HelpCategoryAndSubjectEntity>? CategoryAndSubjectList { get; set; }
}

/// <summary>对应 Java：kingbase/HelpCategoryAndSubjectEntity（专题与视频的关联表）。</summary>
[Table("help_category_and_subject", NotFoundMessage = "未找到对应的在线学习关联信息")]
public class HelpCategoryAndSubjectEntity : BaseEntity
{
    [Column(Comment = "安全教育类目id")]
    public string? ContextId { get; set; }

    [Column(Comment = "在线学习专题id")]
    public string? SubjectId { get; set; }
}
