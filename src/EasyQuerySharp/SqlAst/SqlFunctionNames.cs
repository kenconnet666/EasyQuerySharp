namespace EasyQuerySharp.SqlAst;

/// <summary>
/// 与方言无关的函数名。翻译器产出 <see cref="FunctionExpr"/> 时用这些名字，方言在 <c>WriteFunction</c> 里决定最终写法；
/// 方言没有特殊处理的按 <c>NAME(a,b)</c> 原样输出，所以这里的值就是最常见的写法。
/// </summary>
public static class SqlFunctionNames
{
    /// <summary>空值替换：MySQL 的 IFNULL，其余方言的 COALESCE。</summary>
    public const string NullOrDefault = "IFNULL";

    /// <summary>字符串拼接：MySQL 的 CONCAT，PG / SQLite 的 ||。</summary>
    public const string Concat = "CONCAT";

    /// <summary>字符串长度（字符数）。</summary>
    public const string Length = "CHAR_LENGTH";

    /// <summary>计数。</summary>
    public const string Count = "COUNT";

    /// <summary>求和。</summary>
    public const string Sum = "SUM";

    /// <summary>最大值。</summary>
    public const string Max = "MAX";

    /// <summary>最小值。</summary>
    public const string Min = "MIN";

    /// <summary>平均值。</summary>
    public const string Avg = "AVG";
}
