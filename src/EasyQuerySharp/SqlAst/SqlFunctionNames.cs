namespace EasyQuerySharp.SqlAst;

/// <summary>
/// 与方言无关的函数名。翻译器产出 <see cref="FunctionExpr"/> 时用这些名字，方言的函数表决定最终写法
/// （见 <see cref="Dialects.SqlDialect.RegisterFunctions"/>）；没有登记的按 <c>NAME(a,b)</c> 原样输出。
/// 参数约定写在每个名字的注释里。
/// </summary>
public static class SqlFunctionNames
{
    // ---------- 通用 ----------

    /// <summary>(a, b)：a 为 NULL 时取 b。MySQL IFNULL，其余 COALESCE / ISNULL / NVL。</summary>
    public const string NullOrDefault = "NullOrDefault";

    /// <summary>(a, b)：值相等时 1 否则 0（Java 的 equalsWith）。</summary>
    public const string EqualsWith = "EqualsWith";

    /// <summary>(a, b)：a = b 时 NULL，否则 a。</summary>
    public const string NullIf = "NULLIF";

    // ---------- 字符串 ----------

    /// <summary>(a, b, …)：拼接。MySQL / PG / SQL Server 的 CONCAT，Oracle 的 ||。</summary>
    public const string Concat = "Concat";

    /// <summary>(s)：字符数。</summary>
    public const string Length = "Length";

    /// <summary>(s)：小写。</summary>
    public const string Lower = "LOWER";

    /// <summary>(s)：大写。</summary>
    public const string Upper = "UPPER";

    /// <summary>(s)：去两端空白。</summary>
    public const string Trim = "Trim";

    /// <summary>(s)：去左侧空白。</summary>
    public const string TrimStart = "LTRIM";

    /// <summary>(s)：去右侧空白。</summary>
    public const string TrimEnd = "RTRIM";

    /// <summary>(s, from, to)：替换。</summary>
    public const string Replace = "REPLACE";

    /// <summary>(s, start, [length])：截取，start 从 1 开始（翻译器已把 C# 的 0 基下标加 1）。</summary>
    public const string SubString = "SubString";

    /// <summary>(s, length, [pad])：左侧补齐到 length。</summary>
    public const string PadLeft = "PadLeft";

    /// <summary>(s, length, [pad])：右侧补齐到 length。</summary>
    public const string PadRight = "PadRight";

    /// <summary>(a, b)：比较，相等 0、大于 1、小于 -1。</summary>
    public const string CompareTo = "CompareTo";

    /// <summary>(s, sub)：sub 在 s 里的位置（从 1 开始，0 表示没有）。</summary>
    public const string IndexOf = "IndexOf";

    // ---------- 数值 ----------

    /// <summary>(x)：绝对值。</summary>
    public const string Abs = "ABS";

    /// <summary>(x, [digits])：四舍五入。</summary>
    public const string Round = "ROUND";

    /// <summary>(x)：向下取整。</summary>
    public const string Floor = "FLOOR";

    /// <summary>(x)：向上取整。</summary>
    public const string Ceiling = "Ceiling";

    /// <summary>(x)：符号。</summary>
    public const string Sign = "SIGN";

    /// <summary>(x, y)：x 的 y 次方。</summary>
    public const string Power = "POWER";

    /// <summary>(x)：平方根。</summary>
    public const string Sqrt = "SQRT";

    /// <summary>(x)：e 的 x 次方。</summary>
    public const string Exp = "EXP";

    /// <summary>(x)：自然对数。</summary>
    public const string Ln = "Ln";

    /// <summary>(x)：以 10 为底的对数。</summary>
    public const string Log10 = "LOG10";

    /// <summary>(x, [digits])：截断。</summary>
    public const string Truncate = "Truncate";

    // ---------- 聚合 ----------

    /// <summary>(x)：计数。</summary>
    public const string Count = "COUNT";

    /// <summary>(x)：去重计数，渲染为 COUNT(DISTINCT x)。</summary>
    public const string CountDistinct = "CountDistinct";

    /// <summary>(x)：求和。</summary>
    public const string Sum = "SUM";

    /// <summary>(x)：去重求和。</summary>
    public const string SumDistinct = "SumDistinct";

    /// <summary>(x)：最大值。</summary>
    public const string Max = "MAX";

    /// <summary>(x)：最小值。</summary>
    public const string Min = "MIN";

    /// <summary>(x)：平均值。</summary>
    public const string Avg = "AVG";

    /// <summary>(x)：去重平均值。</summary>
    public const string AvgDistinct = "AvgDistinct";
}
