namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>
/// 日期 <c>ToString(格式)</c> 的格式串转换（C# 独有，没有对应的 Java 用例）：各方言的格式符、格式串里文字的转义、
/// .NET 的引号与 <c>\</c> 转义、<c>%</c> 前缀，以及方言没有的格式符报不支持。结果与 .NET 一致由实库套件的 FormatsDatesLikeDotNetOrRefuses 核对。
/// </summary>
public sealed class DateFormatTest
{
    private static string Format(string dialect, string format) =>
        SqlOnly.Db(dialect).Query<Topic>().Select(t => t.CreateTime!.Value.ToString(format)).ToSql()
            .Replace("SELECT ", "", StringComparison.Ordinal).Split(" FROM ")[0];

    [Theory]
    [InlineData("mysql", "yy/M/d h:mm tt", "DATE_FORMAT(t.`create_time`,'%y/%c/%e %l:%i %p')")]
    [InlineData("mysql", "yyyy\\%MM", "DATE_FORMAT(t.`create_time`,'%Y%%%m')")]
    [InlineData("mysql", "yyyy%MM", "DATE_FORMAT(t.`create_time`,'%Y%c%c')")]
    [InlineData("pgsql", "yyyy-MM-ddTHH:mm 'at' h", "TO_CHAR((t.\"create_time\")::TIMESTAMP,'YYYY-MM-DD\"T\"HH24:MI \"at\" FMHH12')")]
    [InlineData("pgsql", "yyyy年M月d日", "TO_CHAR((t.\"create_time\")::TIMESTAMP,'YYYY年FMMM月FMDD日')")]
    [InlineData("sqlite", "hh:mm tt", "strftime('%I:%M %p', t.\"create_time\")")]
    [InlineData("oracle", "'at' HH \\h yyyy年MM-01", "TO_CHAR(t.\"create_time\",'\"at\" HH24 \"h\" YYYY\"年\"MM-\"01\"')")]
    [InlineData("mssql", "'at' HH:mm", "(? + SUBSTRING(CONVERT(CHAR(8), t.[create_time], 24), 1, 2) + ? + SUBSTRING(CONVERT(CHAR(8), t.[create_time], 24), 4, 2))")]
    [InlineData("hana", "yyyy年MM月 hh", "(TO_NVARCHAR(t.\"create_time\",'YYYY') || '年' || TO_NVARCHAR(t.\"create_time\",'MM') || '月 ' || TO_NVARCHAR(t.\"create_time\",'HH12'))")]
    public void ConvertsSpecifiersAndEscapesText(string dialect, string format, string expected) =>
        Assert.Equal(expected, Format(dialect, format));

    // 方言没有的格式符、依赖区域设置的标准格式都报不支持，不当文字输出
    [Theory]
    [InlineData("sqlite", "d", "标准格式串")]
    [InlineData("sqlite", "yyyy/M/d", "\"M\"")]
    [InlineData("mysql", "HH:mm:ss.fff", "\"fff\"")]
    [InlineData("mssql", "HH:mm tt", "\"tt\"")]
    [InlineData("oracle", "HH:mm:ss.fff", "\"fff\"")]
    [InlineData("db2", "dddd", "\"dddd\"")]
    public void RefusesWhatTheDialectCannotFormat(string dialect, string format, string message) =>
        Assert.Contains(message, Assert.Throws<NotSupportedException>(() => Format(dialect, format)).Message);
}
