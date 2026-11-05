namespace EasyQuerySharp;

/// <summary><see cref="Sql.DateTrunc(DateTime, DateTruncUnit)"/> 截断到的粒度：截到该粒度的起点，更小的部分清零（对应 Java 的 DateTruncEnum）。</summary>
public enum DateTruncUnit
{
    /// <summary>年初。</summary>
    Year,

    /// <summary>季度初。</summary>
    Quarter,

    /// <summary>月初。</summary>
    Month,

    /// <summary>周一（ISO 周）。</summary>
    Week,

    /// <summary>当天零点。</summary>
    Day,

    /// <summary>整点。</summary>
    Hour,

    /// <summary>整分。</summary>
    Minute,

    /// <summary>整秒。</summary>
    Second,
}
