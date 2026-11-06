using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>对应 Java：dameng/entity/DamengMyTopic（列名按 UpperUnderlined 命名，标题相同的主题互为子项）。</summary>
[Table("MY_TOPIC")]
public class DamengMyTopic
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Title)])]
    public List<DamengMyTopic>? MyTopics { get; set; }
}

/// <summary>对应 Java：dameng/entity/DamengMyTopicDTO。</summary>
public class DamengMyTopicDTO
{
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：dameng/entity/DamengNameCustom（两列显式写列名，与按命名约定推出的 ROW_NUMBER / IS_DELETE 不同）。</summary>
[Table("MY_CUSTOM")]
public class DamengNameCustom
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column("ROWNUMBER")]
    public int? RowNumber { get; set; }

    [Column("ISDELETE")]
    public int? IsDelete { get; set; }
}

/// <summary>对应 Java：dameng/vo/DamengNameCustomDTO（列名按命名约定，与实体的显式列名不同）。</summary>
public class DamengNameCustomDTO
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? RowNumber { get; set; }

    public int? IsDelete { get; set; }
}

/// <summary>对应 Java：dameng/vo/DamengNameCustomDTO2（列名与实体一致）。</summary>
public class DamengNameCustomDTO2
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Column("ROWNUMBER")]
    public int? RowNumber { get; set; }

    [Column("ISDELETE")]
    public int? IsDelete { get; set; }
}

/// <summary>对应 Java：DamengQueryTest.VO（按小时分组计数）。</summary>
public class DamengHourCount
{
    public string? HourStr { get; set; }

    public long? Count { get; set; }
}
