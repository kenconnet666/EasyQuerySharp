using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：entity/base/Province（省 → 市一对多）。</summary>
[Table("t_province")]
public class Province
{
    [Column(PrimaryKey = true)]
    public string? Code { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(City.ProvinceCode)])]
    public List<City>? Cities { get; set; }
}

/// <summary>对应 Java：entity/base/City（市 → 区县一对多）。</summary>
[Table("t_city")]
public class City
{
    [Column(PrimaryKey = true)]
    public string? Code { get; set; }

    public string? ProvinceCode { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Area.CityCode)])]
    public List<Area>? Areas { get; set; }

    /// <summary>不映射，由 FillOne 填充。</summary>
    [ColumnIgnore]
    public Province? Province { get; set; }
}

/// <summary>对应 Java：entity/base/Area。</summary>
[Table("t_area")]
public class Area
{
    [Column(PrimaryKey = true)]
    public string? Code { get; set; }

    public string? ProvinceCode { get; set; }

    public string? CityCode { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：MyProvinceVO（分组结果，Cities 由 FillMany 填充）。</summary>
public class MyProvinceVO
{
    public string? MyCode { get; set; }

    public long? Count { get; set; }

    [ColumnIgnore]
    public List<City>? Cities { get; set; }
}
