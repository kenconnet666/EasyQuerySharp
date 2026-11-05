using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 列在 SQL 里的读写转换（对应 Java 的 ColumnValueSQLConverter，常用于库端加解密），分四处：
/// <list type="bullet">
/// <item>查询的列写成 <see cref="Read"/>（如 <c>AES_DECRYPT(from_base64({0}),{1})</c>，别名仍是列名）；</item>
/// <item>插入、更新的值写成 <see cref="Write"/>（如 <c>to_base64(AES_ENCRYPT({0},{1}))</c>）；</item>
/// <item>条件、排序里的列写成 <see cref="Column"/>，默认原列；</item>
/// <item>条件里与列比较的值写成 <see cref="CompareValue"/>，默认同 <see cref="Write"/>。</item>
/// </list>
/// 两种常见搭配：比较密文（列原样、值加密，默认）；比较明文（<see cref="Column"/> 解密、<see cref="CompareValue"/> 为 null 即原值）。
/// 模板里 <c>{0}</c> 是列或值，<c>{1}</c> 起依次是 <see cref="Arguments"/>（作为参数，如密钥）。
/// 用法：<c>[Column(SqlConversion = typeof(MyAesConversion))] public string? Phone { get; set; }</c>；子类要有无参构造函数。
/// </summary>
public abstract class ColumnSqlConversion
{
    /// <summary>查询时列的写法；<c>{0}</c> 是列。</summary>
    public abstract string Read { get; }

    /// <summary>插入、更新时值的写法；<c>{0}</c> 是值。</summary>
    public abstract string Write { get; }

    /// <summary>条件、排序等表达式里列的写法；null（默认）时用原列。</summary>
    public virtual string? Column => null;

    /// <summary>条件里与列比较的值的写法；默认同 <see cref="Write"/>，null 表示原值。</summary>
    public virtual string? CompareValue => Write;

    /// <summary>模板里 <c>{1}</c> 起的参数值。</summary>
    public virtual IReadOnlyList<object?> Arguments => [];

    /// <summary>查询时的列：<see cref="Read"/> 套在列外面。</summary>
    internal SqlExpr ReadOf(SqlExpr column) => Apply(Read, column);

    /// <summary>写入时的值：<see cref="Write"/> 套在值外面。</summary>
    internal SqlExpr WriteOf(SqlExpr value) => Apply(Write, value);

    /// <summary>条件里比较的值：<see cref="CompareValue"/> 套在值外面，没有时原样。</summary>
    internal SqlExpr CompareOf(SqlExpr value) => CompareValue is null ? value : Apply(CompareValue, value);

    /// <summary>表达式里的列：<see cref="Column"/> 套在列外面并记下原列；没有时返回 null。</summary>
    internal SqlExpr? ColumnOf(ColumnExpr column) => Column is null ? null : Apply(Column, column, column);

    private FragmentExpr Apply(string template, SqlExpr operand, ColumnExpr? converted = null) =>
        new(template, [operand, .. Arguments.Select(a => ExpressionTranslator.Parameter(a, a?.GetType() ?? typeof(object)))]) { ConvertedColumn = converted };
}
