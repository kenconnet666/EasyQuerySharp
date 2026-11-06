using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Sharding;

/// <summary>对应 Java：entity/TopicSharding（按 id 取模分成 t_topic_sharding_0 … _2，规则见 <see cref="ShardingFixture"/>）。</summary>
[Table("t_topic_sharding")]
public class TopicSharding
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicShardingTime（按 create_time 按月分表，id 末 6 位是月份作额外分片键，默认按 create_time 降序）。</summary>
[Table("t_topic_sharding_time")]
public class TopicShardingTime
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicShardingDataSource（按 create_time 按年分库，各库里表名不变）。</summary>
[Table("t_topic_sharding_ds")]
public class TopicShardingDataSource
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/TopicShardingDataSourceTime（按 create_time 按年分库、按月分表）。</summary>
[Table("t_topic_sharding_ds_time")]
public class TopicShardingDataSourceTime
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：dto/TopicShardingGroup（按标题分组的计数）。</summary>
public class TopicShardingGroup
{
    public int? IdCount { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }
}
