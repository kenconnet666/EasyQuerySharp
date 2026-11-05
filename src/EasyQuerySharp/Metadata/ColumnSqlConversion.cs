namespace EasyQuerySharp.Metadata;

/// <summary>
/// 列在 SQL 里的读写转换（对应 Java 的 ColumnValueSQLConverter，常用于库端加解密）：
/// 查询时列写成 <see cref="Read"/>（如 <c>AES_DECRYPT(from_base64({0}),{1})</c>，别名仍是列名），
/// 插入、更新与条件里和它比较的值写成 <see cref="Write"/>（如 <c>to_base64(AES_ENCRYPT({0},{1}))</c>）。
/// 模板里 <c>{0}</c> 是列或值，<c>{1}</c> 起依次是 <see cref="Arguments"/>（作为参数，如密钥）。
/// 用法：<c>[Column(SqlConversion = typeof(MyAesConversion))] public string? Phone { get; set; }</c>；子类要有无参构造函数。
/// </summary>
public abstract class ColumnSqlConversion
{
    /// <summary>查询时列的写法；<c>{0}</c> 是列。</summary>
    public abstract string Read { get; }

    /// <summary>写入或比较时值的写法；<c>{0}</c> 是值。</summary>
    public abstract string Write { get; }

    /// <summary>模板里 <c>{1}</c> 起的参数值。</summary>
    public virtual IReadOnlyList<object?> Arguments => [];

    /// <summary>查询时的列：<see cref="Read"/> 套在列外面。</summary>
    internal SqlAst.SqlExpr ReadOf(SqlAst.SqlExpr column) => Apply(Read, column);

    /// <summary>写入或比较时的值：<see cref="Write"/> 套在值外面。</summary>
    internal SqlAst.SqlExpr WriteOf(SqlAst.SqlExpr value) => Apply(Write, value);

    private SqlAst.FragmentExpr Apply(string template, SqlAst.SqlExpr operand) =>
        new(template, [operand, .. Arguments.Select(a => Translation.ExpressionTranslator.Parameter(a, a?.GetType() ?? typeof(object)))]);
}
