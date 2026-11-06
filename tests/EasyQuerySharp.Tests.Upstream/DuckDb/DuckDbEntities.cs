using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>
/// 对应 Java：h2/domain/ALLTYPE（各种数值、日期时间、布尔、字符串、UUID 列，可空与基本类型各一组）。
/// Java 的 Byte 是有符号的，对应 sbyte；java.util.Date 对应 DateTime，java.sql.Date 对应 DateOnly，java.sql.Time 对应 TimeOnly。
/// </summary>
[Table("t_all_type")]
public class ALLTYPE
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public decimal? NumberDecimal { get; set; }

    public float? NumberFloat { get; set; }

    public double? NumberDouble { get; set; }

    public short? NumberShort { get; set; }

    public int? NumberInteger { get; set; }

    public long? NumberLong { get; set; }

    public sbyte? NumberByte { get; set; }

    public DateTime? TimeLocalDateTime { get; set; }

    public DateOnly? TimeLocalDate { get; set; }

    public TimeOnly? TimeLocalTime { get; set; }

    public DateTime? OnlyDate { get; set; }

    public DateOnly? SqlDate { get; set; }

    public TimeOnly? OnlyTime { get; set; }

    public bool? Enable { get; set; }

    public string? Value { get; set; }

    public Guid? Uid { get; set; }

    public float NumberFloatBasic { get; set; }

    public double NumberDoubleBasic { get; set; }

    public short NumberShortBasic { get; set; }

    public int NumberIntegerBasic { get; set; }

    public long NumberLongBasic { get; set; }

    public sbyte NumberByteBasic { get; set; }

    public bool EnableBasic { get; set; }
}

/// <summary>
/// 对应 Java：duckdb/ExcelDuck（表是 DuckDB 的表函数 <c>read_xlsx('./ducktest.xlsx',sheet='Sheet1')</c>）。
/// Java 把表函数写在 <c>@Table(value = …, keyword = false)</c> 里；C# 的表名是普通标识符，改在查询上用 AsTableSegment 整体替换表引用。
/// </summary>
[Table("excel_duck")]
public class ExcelDuck
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(ExcelDuck2.Uid)])]
    public List<ExcelDuck2>? ExcelDuck2List { get; set; }
}

/// <summary>对应 Java：duckdb/ExcelDuck2（表函数 <c>read_xlsx('./ducktest.xlsx',sheet='Sheet2')</c>）。</summary>
[Table("excel_duck2")]
public class ExcelDuck2
{
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }
}

/// <summary>C# 独有：JSON 列（DuckDB 的 JSON 类型），用来验证 Sql.Json 的取值、长度、含键。</summary>
[Table("t_duck_json")]
public class DuckJsonDoc
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    [Column(DbType = "JSON")]
    public string? Doc { get; set; }
}
