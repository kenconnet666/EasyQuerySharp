using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/TopicAutoNative（主键不自增，由调用方给值）。</summary>
[Table("t_topic_auto1")]
public class TopicAutoNative
{
    [Column(PrimaryKey = true)]
    public int? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：entity/CustomIncrement（主键由数据库函数 mysqlNextId() 生成）。</summary>
[Table("custom_increment")]
public class CustomIncrement
{
    [Column(PrimaryKey = true, Generated = true, GeneratedSql = "mysqlNextId()")]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }
}

/// <summary>对应 Java：entity/TestInc（自增主键，批量插入回填）。</summary>
[Table("t_test_inc")]
public class TestInc
{
    [Column(PrimaryKey = true, Generated = true)]
    public int? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}
