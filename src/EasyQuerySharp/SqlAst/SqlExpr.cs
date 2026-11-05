using System.Diagnostics.CodeAnalysis;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.SqlAst;

/// <summary>
/// 与方言无关的 SQL 表达式节点。翻译器把 C# 表达式树翻译成它，<see cref="Rendering.SqlRenderer"/> 再按方言渲染成文本。
/// 节点在翻译完成后不再修改。
/// </summary>
public abstract class SqlExpr;

/// <summary>某张表上的一列：<c>t.`id`</c>。</summary>
public sealed class ColumnExpr(TableSource table, string name, ColumnMetadata? column = null) : SqlExpr
{
    /// <summary>所属的表来源。</summary>
    public TableSource Table { get; } = table;

    /// <summary>列名（未加引号）。</summary>
    public string Name { get; } = name;

    /// <summary>映射信息；派生表上的列为 null。</summary>
    public ColumnMetadata? Column { get; } = column;

    /// <summary>派生表上内层写了 AS 的列：外层投影到同名成员时同样写 AS（与 Java 一致）。</summary>
    public bool KeepAlias { get; init; }
}

/// <summary>绑定的参数值，渲染为占位符。</summary>
public sealed class ParameterExpr(object? value, Type clrType) : SqlExpr
{
    /// <summary>参数值（已经过值转换器）。</summary>
    public object? Value { get; } = value;

    /// <summary>值的 CLR 类型（用于驱动类型推断与测试里的参数文本）。</summary>
    public Type ClrType { get; } = clrType;

    /// <summary>写入的目标列声明的数据库类型（<c>[Column(DbType = …)]</c>）；方言据此给参数加类型转换（如 PG 的 jsonb）。</summary>
    public string? ColumnDbType { get; init; }
}

/// <summary>原样输出的 SQL 片段，不含参数：<c>1</c>、<c>NULL</c>、<c>*</c>。</summary>
public sealed class LiteralExpr(string sql) : SqlExpr
{
    /// <summary>原样输出的文本。</summary>
    public string Sql { get; } = sql;

    /// <summary><c>*</c></summary>
    public static LiteralExpr Star { get; } = new("*");

    /// <summary><c>NULL</c></summary>
    public static LiteralExpr Null { get; } = new("NULL");

    /// <summary><c>1</c></summary>
    public static LiteralExpr One { get; } = new("1");
}

/// <summary>二元运算符。</summary>
public enum SqlBinaryOperator
{
    /// <summary>=</summary>
    Equal,
    /// <summary>&lt;&gt;</summary>
    NotEqual,
    /// <summary>&lt;</summary>
    LessThan,
    /// <summary>&lt;=</summary>
    LessThanOrEqual,
    /// <summary>&gt;</summary>
    GreaterThan,
    /// <summary>&gt;=</summary>
    GreaterThanOrEqual,
    /// <summary>+</summary>
    Add,
    /// <summary>-</summary>
    Subtract,
    /// <summary>*</summary>
    Multiply,
    /// <summary>/</summary>
    Divide,
    /// <summary>%（部分方言渲染成 MOD 函数）</summary>
    Modulo,
}

/// <summary>二元运算：比较或算术。</summary>
public sealed class BinaryExpr(SqlExpr left, SqlBinaryOperator op, SqlExpr right) : SqlExpr
{
    /// <summary>左操作数。</summary>
    public SqlExpr Left { get; } = left;

    /// <summary>运算符。</summary>
    public SqlBinaryOperator Operator { get; } = op;

    /// <summary>右操作数。</summary>
    public SqlExpr Right { get; } = right;

    /// <summary>是否比较运算（结果是布尔）。</summary>
    public bool IsComparison => Operator <= SqlBinaryOperator.GreaterThanOrEqual;
}

/// <summary>AND / OR 组。AND 平铺渲染；OR 组总是加括号（与 Java 版一致）。</summary>
public sealed class LogicalExpr(bool isAnd, IReadOnlyList<SqlExpr> operands) : SqlExpr
{
    /// <summary>true 为 AND，false 为 OR。</summary>
    public bool IsAnd { get; } = isAnd;

    /// <summary>操作数，至少一个。</summary>
    public IReadOnlyList<SqlExpr> Operands { get; } = operands;

    /// <summary>显式分组：不被外层同类的 AND / OR 平铺；AND 组渲染时整体加括号（<see cref="Sql.Group"/>），OR 本来就加括号。</summary>
    public bool IsGrouped { get; private init; }

    /// <summary>显式分组的 AND 组。</summary>
    public static LogicalExpr Grouped(IReadOnlyList<SqlExpr> operands) => new(true, operands) { IsGrouped = true };

    /// <summary>显式分组的 OR 组：作为外层 OR 的一项时仍单独带括号（如按上限拆开的 IN 列表）。</summary>
    public static LogicalExpr GroupedOr(IReadOnlyList<SqlExpr> operands) => new(false, operands) { IsGrouped = true };

    /// <summary>合并成 AND：嵌套的 AND 平铺（显式分组的除外）；只有一个操作数时返回它本身。</summary>
    public static SqlExpr And(IEnumerable<SqlExpr> operands) => Combine(true, operands);

    /// <summary>合并成 OR：嵌套的 OR 平铺；只有一个操作数时返回它本身。</summary>
    public static SqlExpr Or(IEnumerable<SqlExpr> operands) => Combine(false, operands);

    private static SqlExpr Combine(bool isAnd, IEnumerable<SqlExpr> operands)
    {
        var flat = new List<SqlExpr>();
        foreach (var o in operands)
        {
            if (o is LogicalExpr { IsGrouped: false } l && l.IsAnd == isAnd) flat.AddRange(l.Operands);
            else flat.Add(o);
        }
        return flat.Count == 1 ? flat[0] : new LogicalExpr(isAnd, flat);
    }
}

/// <summary>NOT：渲染为 <c>NOT (x)</c>。</summary>
public sealed class NotExpr(SqlExpr operand) : SqlExpr
{
    /// <summary>被取反的表达式。</summary>
    public SqlExpr Operand { get; } = operand;
}

/// <summary><c>x IS NULL</c> / <c>x IS NOT NULL</c>。</summary>
public sealed class IsNullExpr(SqlExpr operand, bool negated) : SqlExpr
{
    /// <summary>被判断的表达式。</summary>
    public SqlExpr Operand { get; } = operand;

    /// <summary>true 为 IS NOT NULL。</summary>
    public bool Negated { get; } = negated;
}

/// <summary><c>x IN (?,?)</c> 或 <c>x IN (SELECT …)</c>；值列表为空时渲染为恒假 / 恒真条件。</summary>
public sealed class InExpr : SqlExpr
{
    /// <summary>值列表形式。</summary>
    public InExpr(SqlExpr operand, IReadOnlyList<SqlExpr> values, bool negated)
    {
        Operand = operand;
        Values = values;
        Negated = negated;
    }

    /// <summary>子查询形式。</summary>
    public InExpr(SqlExpr operand, SelectQuery subQuery, bool negated)
    {
        Operand = operand;
        SubQuery = subQuery;
        Negated = negated;
    }

    /// <summary>被判断的表达式。</summary>
    public SqlExpr Operand { get; }

    /// <summary>值列表；子查询形式时为 null。</summary>
    public IReadOnlyList<SqlExpr>? Values { get; }

    /// <summary>子查询；值列表形式时为 null。</summary>
    public SelectQuery? SubQuery { get; }

    /// <summary>true 为 NOT IN。</summary>
    public bool Negated { get; }
}

/// <summary>LIKE 的匹配方式：方言据此选择写法（PG 用 CONCAT + ESCAPE，Oracle / SQLite 改写成 INSTR）。</summary>
public enum LikeMode
{
    /// <summary>原样的模式，% 与 _ 由调用方写（Sql.Like）。</summary>
    Raw,

    /// <summary>包含（string.Contains）。</summary>
    Contains,

    /// <summary>以…开头（string.StartsWith）。</summary>
    StartsWith,

    /// <summary>以…结尾（string.EndsWith）。</summary>
    EndsWith,
}

/// <summary>
/// <c>x LIKE ?</c> / <c>x NOT LIKE ?</c>。<see cref="Pattern"/> 是已经拼好 % 的模式（MySQL 等直接用）；
/// 非 <see cref="LikeMode.Raw"/> 时 <see cref="Value"/> 是未加 % 的原值，方言可以改用自己的写法。
/// </summary>
public sealed class LikeExpr(SqlExpr operand, SqlExpr pattern, bool negated, LikeMode mode = LikeMode.Raw, SqlExpr? value = null) : SqlExpr
{
    /// <summary>被匹配的表达式。</summary>
    public SqlExpr Operand { get; } = operand;

    /// <summary>模式（通常是已带 % 的参数，或 CONCAT('%',列,'%')）。</summary>
    public SqlExpr Pattern { get; } = pattern;

    /// <summary>true 为 NOT LIKE。</summary>
    public bool Negated { get; } = negated;

    /// <summary>匹配方式。</summary>
    public LikeMode Mode { get; } = mode;

    /// <summary>未加 % 的原值（参数或列）；<see cref="LikeMode.Raw"/> 时为 null。</summary>
    public SqlExpr? Value { get; } = value;

    /// <summary>
    /// 不是原样模式、且值是含通配符（% 或 _）的常量：只有这时 Java 版才改用 ESCAPE / INSTR 的安全写法，其余情况都是普通的 LIKE ?。
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasWildcardValue => Mode != LikeMode.Raw && Value is ParameterExpr { Value: string text } && text.AsSpan().IndexOfAny("%_") >= 0;

    /// <summary>取反后的同一个 LIKE。</summary>
    public LikeExpr Negate() => new(Operand, Pattern, !Negated, Mode, Value);
}

/// <summary>
/// 函数调用。<see cref="Name"/> 是与方言无关的函数名（见 <see cref="SqlFunctionNames"/>），
/// 由方言的函数表决定最终写法；方言没有登记时按 <c>NAME(arg,…)</c> 输出。
/// </summary>
public sealed class FunctionExpr(string name, IReadOnlyList<SqlExpr> arguments) : SqlExpr
{
    /// <summary>是否本身就是条件（如 null 安全相等）；是时用在 WHERE 里不再补 <c>= true</c>。</summary>
    public bool IsPredicate { get; init; }

    /// <summary>函数名。</summary>
    public string Name { get; } = name;

    /// <summary>参数。</summary>
    public IReadOnlyList<SqlExpr> Arguments { get; } = arguments;
}

/// <summary>
/// 带占位的原生 SQL 片段：<c>{0}</c>、<c>{1}</c> 处依次替换为参数表达式的渲染结果（对应 Java 的 sqlSegment / sqlNativeSegment）。
/// </summary>
public sealed class FragmentExpr(string template, IReadOnlyList<SqlExpr> arguments, bool isPredicate = false) : SqlExpr
{
    /// <summary>模板文本。</summary>
    public string Template { get; } = template;

    /// <summary>模板参数。</summary>
    public IReadOnlyList<SqlExpr> Arguments { get; } = arguments;

    /// <summary>片段本身是条件（<c>Sql.Raw&lt;bool&gt;</c>），用在 WHERE 里不再补 <c>= true</c>。</summary>
    public bool IsPredicate { get; } = isPredicate;
}

/// <summary><c>CASE WHEN … THEN … ELSE … END</c>。</summary>
public sealed class CaseExpr(IReadOnlyList<(SqlExpr When, SqlExpr Then)> branches, SqlExpr? @else) : SqlExpr
{
    /// <summary>分支。</summary>
    public IReadOnlyList<(SqlExpr When, SqlExpr Then)> Branches { get; } = branches;

    /// <summary>ELSE 部分；为 null 时不写 ELSE。</summary>
    public SqlExpr? Else { get; } = @else;
}

/// <summary><c>EXISTS (SELECT …)</c> / <c>NOT EXISTS (…)</c>。</summary>
public sealed class ExistsExpr(SelectQuery query, bool negated) : SqlExpr
{
    /// <summary>子查询。</summary>
    public SelectQuery Query { get; } = query;

    /// <summary>true 为 NOT EXISTS。</summary>
    public bool Negated { get; } = negated;

    /// <summary>集合导航上的 Any 生成的 EXISTS：取反时写成 <c>NOT (EXISTS(…))</c> 而不是 NOT EXISTS（与 Java 的 none 一致）。</summary>
    public bool FromNavigation { get; init; }
}

/// <summary>标量子查询 <c>(SELECT …)</c>。</summary>
public sealed class SubQueryExpr(SelectQuery query) : SqlExpr
{
    /// <summary>子查询。</summary>
    public SelectQuery Query { get; } = query;

    /// <summary>子查询之后再占一个别名编号（Java 的显式子查询 selectColumn 会多建一张匿名表，之后的表编号跳过一个）。</summary>
    public bool ReserveExtraAlias { get; init; }
}

/// <summary>
/// 类型转换（对应 Java 的 toNumber / asStr / cast）：写法因方言而异，渲染时由 <see cref="Dialects.SqlDialect.CastTemplate"/> 决定，
/// 如 MySQL 的 <c>CAST(x AS SIGNED)</c>、PostgreSQL 的 <c>(x)::int4</c>。
/// </summary>
public sealed class CastExpr(SqlExpr operand, Type targetType) : SqlExpr
{
    /// <summary>被转换的表达式。</summary>
    public SqlExpr Operand { get; } = operand;

    /// <summary>目标 CLR 类型（可空类型已取底层类型）。</summary>
    public Type TargetType { get; } = Nullable.GetUnderlyingType(targetType) ?? targetType;
}

/// <summary>
/// 空串判断（对应 Java 的 isEmpty / isBlank 及其取反，C# 的 <c>string.IsNullOrEmpty</c> / <c>IsNullOrWhiteSpace</c>）：
/// <c>(x IS NULL OR x = '' [OR LTRIM(x) = ''])</c>，取反为 <c>(x IS NOT NULL AND x &lt;&gt; '' [AND LTRIM(x) &lt;&gt; ''])</c>。
/// </summary>
public sealed class BlankCheckExpr(SqlExpr operand, bool whitespace, bool negated) : SqlExpr
{
    /// <summary>被判断的表达式。</summary>
    public SqlExpr Operand { get; } = operand;

    /// <summary>true 时空白字符串也算空（IsNullOrWhiteSpace / isBlank）。</summary>
    public bool Whitespace { get; } = whitespace;

    /// <summary>true 为"不为空"。</summary>
    public bool Negated { get; } = negated;

    /// <summary>取反。</summary>
    public BlankCheckExpr Negate() => new(Operand, Whitespace, !Negated);
}

/// <summary>
/// 窗口函数 <c>f OVER (PARTITION BY … ORDER BY … frame)</c>。<see cref="Compact"/> 为 true 时（LAG / LEAD / FIRST_VALUE 等取值函数）
/// 不加外层括号、列表用 "," 分隔；否则整体加括号、列表用 ", " 分隔（两种写法都与 Java 一致）。
/// 集合导航取元素时编行号用 <see cref="RowNumber"/>：加括号、列表用 "," 分隔。
/// </summary>
public sealed class WindowExpr(SqlExpr function, IReadOnlyList<SqlExpr> partitions, IReadOnlyList<OrderByItem> orders, string? frame, bool compact, string? separator = null) : SqlExpr
{
    /// <summary>集合导航按所属行分区编行号：<c>(ROW_NUMBER() OVER (PARTITION BY … ORDER BY …))</c>。</summary>
    public static WindowExpr RowNumber(IReadOnlyList<SqlExpr> partitions, IReadOnlyList<OrderByItem> orders) =>
        new(new FragmentExpr("ROW_NUMBER()", []), partitions, orders, null, compact: false, separator: ",");

    /// <summary>PARTITION BY / ORDER BY 列表的分隔符。</summary>
    public string Separator { get; } = separator ?? (compact ? "," : ", ");

    /// <summary>OVER 之前的函数。</summary>
    public SqlExpr Function { get; } = function;

    /// <summary>PARTITION BY 的值。</summary>
    public IReadOnlyList<SqlExpr> Partitions { get; } = partitions;

    /// <summary>ORDER BY 的项。</summary>
    public IReadOnlyList<OrderByItem> Orders { get; } = orders;

    /// <summary>frame 子句，如 <c>ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW</c>；没有时为 null。</summary>
    public string? Frame { get; } = frame;

    /// <summary>取值函数的紧凑写法。</summary>
    public bool Compact { get; } = compact;
}

/// <summary>
/// 带组内排序的字符串聚合（Java 的 orderBy(…).joining(…)）：MySQL 的 <c>GROUP_CONCAT(x ORDER BY … SEPARATOR ?)</c>，
/// PG 的 <c>STRING_AGG(x, ? ORDER BY …)</c>。写法由方言决定（见 <see cref="Dialects.SqlDialect.WriteOrderedStringJoin"/>）。
/// </summary>
public sealed class OrderedStringJoinExpr(SqlExpr value, SqlExpr separator, bool distinct, IReadOnlyList<OrderByItem> orders) : SqlExpr
{
    /// <summary>被拼接的值。</summary>
    public SqlExpr Value { get; } = value;

    /// <summary>分隔符。</summary>
    public SqlExpr Separator { get; } = separator;

    /// <summary>去重拼接。</summary>
    public bool Distinct { get; } = distinct;

    /// <summary>组内排序，至少一项。</summary>
    public IReadOnlyList<OrderByItem> Orders { get; } = orders;
}

/// <summary>
/// 计算列（<see cref="Metadata.ComputedAttribute"/>）展开后的表达式：渲染与 <see cref="Inner"/> 相同；
/// 比较时按普通列看待（Java 的计算列不触发"列 比较 函数"时的多余空格）。
/// </summary>
public sealed class ComputedExpr(SqlExpr inner) : SqlExpr
{
    /// <summary>展开后的表达式。</summary>
    public SqlExpr Inner { get; } = inner;
}
