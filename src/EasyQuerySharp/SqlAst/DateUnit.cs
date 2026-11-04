namespace EasyQuerySharp.SqlAst;

/// <summary>日期加减与相差的单位。</summary>
public enum DateUnit
{
    /// <summary>年。</summary>
    Year,

    /// <summary>月。</summary>
    Month,

    /// <summary>日。</summary>
    Day,

    /// <summary>时。</summary>
    Hour,

    /// <summary>分。</summary>
    Minute,

    /// <summary>秒。</summary>
    Second,

    /// <summary>毫秒。</summary>
    Millisecond,
}

/// <summary>日期取部分（<c>x.Year</c>、<c>x.DayOfWeek</c> 等）。</summary>
public enum DatePart
{
    /// <summary>年。</summary>
    Year,

    /// <summary>月（1–12）。</summary>
    Month,

    /// <summary>日（1–31）。</summary>
    Day,

    /// <summary>时（0–23）。</summary>
    Hour,

    /// <summary>分。</summary>
    Minute,

    /// <summary>秒。</summary>
    Second,

    /// <summary>一年中的第几天（1 起）。</summary>
    DayOfYear,

    /// <summary>星期几：周日 0，周六 6（与 C# 的 DayOfWeek 一致）。</summary>
    DayOfWeek,

    /// <summary>ISO 星期几：周一 1，周日 7（<c>Sql.IsoDayOfWeek</c>，对应 Java 的 dayOfWeekSunDayIsLastDay）。</summary>
    IsoDayOfWeek,
}
