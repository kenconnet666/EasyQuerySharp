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

    /// <summary>(a, b)：null 安全的相等（两边都为 NULL 也算相等），作为条件使用。MySQL <c>&lt;=&gt;</c>、PG <c>IS NOT DISTINCT FROM</c>、SQLite <c>IS</c>。</summary>
    public const string NullSafeEquals = "NullSafeEquals";

    /// <summary>(predicate)：已加括号的条件当作布尔值。默认原样写出，SQL Server / Oracle 写成 <c>(CASE WHEN 条件 THEN ? ELSE ? END)</c>。</summary>
    public const string BooleanValue = "BooleanValue";

    /// <summary>(value, csv)：value 在逗号分隔的列表 csv 里，作为条件使用。MySQL <c>FIND_IN_SET</c>，PG <c>= ANY(string_to_array(..))</c>，其余拼逗号后查位置。</summary>
    public const string InCsv = "InCsv";

    /// <summary>(json, key)：对象成员，仍是 JSON。</summary>
    public const string JsonGet = "JsonGet";

    /// <summary>(json, index)：数组元素，仍是 JSON。</summary>
    public const string JsonGetIndex = "JsonGetIndex";

    /// <summary>(json, key)：对象成员的文本值。</summary>
    public const string JsonText = "JsonText";

    /// <summary>(json, index)：数组元素的文本值。</summary>
    public const string JsonTextIndex = "JsonTextIndex";

    /// <summary>(json)：数组长度。</summary>
    public const string JsonLength = "JsonLength";

    /// <summary>(json, key)：对象是否有这个键，作为条件使用。</summary>
    public const string JsonContainsKey = "JsonContainsKey";

    /// <summary>(key)：<c>GROUPING(key)</c>，ROLLUP / CUBE 的小计行里被汇总掉的键为 1。</summary>
    public const string Grouping = "GROUPING";

    /// <summary>(a, b, …)：同一行里几个值中最大的。MySQL / PG 的 GREATEST，SQLite 的多参数 MAX，SQL Server 的 VALUES，其余 UNION ALL 派生表。</summary>
    public const string MaxColumns = "MaxColumns";

    /// <summary>(a, b, …)：同一行里几个值中最小的，写法同 <see cref="MaxColumns"/>。</summary>
    public const string MinColumns = "MinColumns";

    // ---------- 字符串 ----------

    /// <summary>(a, b, …)：拼接。MySQL / PG / SQL Server 的 CONCAT，Oracle 的 ||。</summary>
    public const string Concat = "Concat";

    /// <summary>(x)：拼接里的一项，NULL 当空串（C# 的拼接语义）；默认原样输出，拼接遇到 NULL 得到 NULL 的库（MySQL、HANA、Db2、ClickHouse、TDengine）写成 <c>COALESCE(x, '')</c>。</summary>
    public const string NullAsEmpty = "NullAsEmpty";

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

    // ---------- 日期 ----------

    /// <summary>(x, 'C# 格式串' 字面量)：按格式转成字符串，方言把 yyyy / MM / dd / HH / mm / ss 转成自己的格式符。</summary>
    public const string DateFormat = "DateFormat";

    /// <summary>()：数据库当前时间（本地）。</summary>
    public const string Now = "Now";

    /// <summary>()：数据库当前 UTC 时间。</summary>
    public const string UtcNow = "UtcNow";

    /// <summary>随机数：<c>RAND()</c>（PG / SQLite 为 <c>RANDOM()</c>，Oracle 为 <c>DBMS_RANDOM.VALUE()</c>），多用于随机排序。</summary>
    public const string Random = "Random";

    /// <summary>(x, n)：日期加上 n 个 <paramref name="unit"/>。</summary>
    public static string DateAdd(DateUnit unit) => "DateAdd." + unit;

    /// <summary>(x)：取日期的一部分。</summary>
    public static string DatePartOf(DatePart part) => "DatePart." + part;

    /// <summary>(x)：截断到 <paramref name="unit"/> 的起点（Java 的 dateTrunc）。</summary>
    public static string DateTrunc(DateTruncUnit unit) => "DateTrunc." + unit;

    /// <summary>(start, end)：end 减 start 的整数 <paramref name="unit"/> 数（截断）。</summary>
    public static string DateDiff(DateUnit unit) => "DateDiff." + unit;

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

    /// <summary>(x, base)：以 base 为底的对数（C# 的 Math.Log(x, newBase)）。SQL Server 写成 <c>LOG(x, base)</c>，其余底数在前 <c>LOG(base, x)</c>。</summary>
    public const string LogBase = "LogBase";

    /// <summary>(x, [digits])：截断。</summary>
    public const string Truncate = "Truncate";

    /// <summary>(a, b)：整数相除，结果向零截断（C# 的 <c>int / int</c>）；默认写 <c>(a / b)</c>，相除得到小数的库由方言改写。</summary>
    public const string IntegerDivide = "IntegerDivide";

    /// <summary>(x)：日期时间的日期部分（<c>DateOnly.FromDateTime(x)</c>）；默认按天截断，日期存成文本的库（SQLite）写成只有日期的文本。</summary>
    public const string ToDateOnly = "ToDateOnly";

    /// <summary>(y, x)：点 (x, y) 与 x 轴正方向的夹角（弧度）。</summary>
    public const string Atan2 = "ATAN2";

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

    /// <summary>(x, separator)：字符串聚合。</summary>
    public const string StringJoin = "StringJoin";

    /// <summary>(x, separator)：去重字符串聚合。</summary>
    public const string StringJoinDistinct = "StringJoinDistinct";
}
