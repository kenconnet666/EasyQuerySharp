using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mssql;

/// <summary>对应 Java：mssql/entity/MsSQLMyTopic（也是 MsSQLMyTopic1：同一张表，属性名首字母大写，在 UpperCamelCase 命名下列名相同）。</summary>
[Table("MyTopic")]
public class MsSQLMyTopic
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public int? Stars { get; set; }

    public string? Title { get; set; }

    public DateTime? CreateTime { get; set; }
}

/// <summary>对应 Java：mssql/entity/MsSQLCalc。</summary>
[Table("t_calc")]
public class MsSQLCalc
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    [Column(DbType = "decimal(18,2)")]
    public decimal? Column1 { get; set; }

    [Column(DbType = "decimal(18,4)")]
    public decimal? Column2 { get; set; }

    [Column(DbType = "decimal(18,4)")]
    public decimal? Column3 { get; set; }
}

/// <summary>对应 Java：mssql/entity/MSSQLA（列名显式写成大写开头，用默认命名约定的库也是这个列名）。</summary>
[Table("t_mssql_a")]
public class MSSQLA
{
    [Column("Id")]
    public string? Id { get; set; }

    [Column("Code")]
    public string? Code { get; set; }

    [Column("Name")]
    public string? Name { get; set; }
}

/// <summary>对应 Java：mssql/entity/MSSQLB。</summary>
[Table("t_mssql_b")]
public class MSSQLB
{
    [Column("Id")]
    public string? Id { get; set; }

    [Column("Code")]
    public string? Code { get; set; }

    [Column("Name")]
    public string? Name { get; set; }
}

/// <summary>对应 Java：mssql/MSSQLADTO。</summary>
public class MSSQLADTO
{
    public string? Code { get; set; }

    public string? Name { get; set; }
}
